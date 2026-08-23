using VietAIS.TCFlow.Analyzers.AspNet;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Marten;

public sealed class MartenAnalyzer : IRepositoryAnalyzer, IRepositoryAnalyzerApplicability
{
    public string Name => "marten-v1";

    public bool Supports(RepositoryFile file) =>
        string.Equals(Path.GetExtension(file.RelativePath), ".cs", StringComparison.OrdinalIgnoreCase);

    public bool SupportsRepository(IReadOnlyCollection<RepositoryFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        return files.Any(file =>
            file.Content.Contains("PackageReference Include=\"Marten\"", StringComparison.OrdinalIgnoreCase) ||
            file.Content.Contains("using Marten", StringComparison.Ordinal) ||
            file.Content.Contains("AddMarten(", StringComparison.Ordinal) ||
            file.Content.Contains("IQuerySession", StringComparison.Ordinal) ||
            file.Content.Contains("IDocumentSession", StringComparison.Ordinal));
    }

    public async Task<AnalysisResult> AnalyzeAsync(
        IReadOnlyCollection<RepositoryFile> files,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        var supportedFiles = files.Where(Supports)
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
        var model = MartenSourceModel.Create(supportedFiles);
        var aspNet = await new AspNetAnalyzer().AnalyzeAsync(supportedFiles, cancellationToken);
        var accumulator = new MartenAnalysisAccumulator(Name);
        BuildResult(model, aspNet, accumulator, cancellationToken);
        return accumulator.Build();
    }

    private static void BuildResult(
        MartenSourceModel model,
        AnalysisResult aspNet,
        MartenAnalysisAccumulator accumulator,
        CancellationToken cancellationToken)
    {
        var documentIds = AddDocuments(model, accumulator);
        var aspNetHandlers = aspNet.Artifacts
            .Where(artifact => artifact.Kind == ArtifactKind.Handler)
            .ToDictionary(
                artifact => $"{artifact.Path}\0{artifact.Name}",
                artifact => artifact.Id,
                StringComparer.Ordinal);

        foreach (var scope in model.Scopes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var operations = model.Operations.Where(operation => operation.Scope == scope).ToArray();
            var hasSave = operations.Any(operation => operation.Kind == MartenOperationKind.SaveChanges);
            var writes = operations.Count(operation =>
                operation.Kind is MartenOperationKind.Store or MartenOperationKind.Delete);
            var sessionEvidence = accumulator.AddEvidence(
                scope.File,
                scope.DeclarationIndex,
                $"{scope.Name} receives {scope.SessionType} as {scope.SessionVariable}.",
                "marten.session",
                scope.SessionVariable);
            var sessionId = accumulator.AddArtifact(
                ArtifactKind.MartenSession,
                scope.Name,
                scope.File,
                [sessionEvidence],
                new Dictionary<string, string>
                {
                    ["sessionType"] = scope.SessionType,
                    ["sessionVariable"] = scope.SessionVariable,
                    ["hasSaveChanges"] = hasSave.ToString().ToLowerInvariant(),
                    ["writeCount"] = writes.ToString()
                });

            var handlerId = ResolveHandlerId(scope, aspNetHandlers);
            var endpointIds = aspNet.Dependencies
                .Where(dependency =>
                    dependency.Kind == DependencyKind.DelegatesTo &&
                    dependency.Target == handlerId)
                .Select(dependency => dependency.SourceArtifactId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            foreach (var operation in operations)
            {
                AddOperation(
                    operation,
                    hasSave,
                    sessionId,
                    handlerId,
                    endpointIds,
                    documentIds,
                    accumulator);
            }

            if (writes > 0 && !hasSave)
            {
                accumulator.AddDiagnostic(new AnalyzerDiagnostic(
                    "MARTEN001",
                    $"{scope.Name} performs {writes} write operation(s) without SaveChangesAsync.",
                    EvidenceLevel.Confirmed,
                    new SourceLocation(
                        scope.File.RelativePath,
                        MartenTextParsing.LineNumber(scope.File.Content, scope.DeclarationIndex),
                        MartenTextParsing.LineNumber(scope.File.Content, scope.DeclarationIndex),
                        scope.Name)));
            }
        }
    }

    private static IReadOnlyDictionary<string, string> AddDocuments(
        MartenSourceModel model,
        MartenAnalysisAccumulator accumulator)
    {
        var documentIds = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var documentTypes = model.SchemaFacts.Select(fact => fact.DocumentType)
            .Concat(model.Operations.Where(operation => operation.DocumentType is not null)
                .Select(operation => operation.DocumentType!))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        foreach (var documentType in documentTypes)
        {
            var schema = model.SchemaFacts.FirstOrDefault(fact => fact.DocumentType == documentType);
            var operation = model.Operations.FirstOrDefault(fact => fact.DocumentType == documentType);
            var evidenceFile = schema?.File ?? operation?.Scope.File;
            if (evidenceFile is null)
            {
                continue;
            }

            var evidenceIndex = schema?.Index ?? operation!.Index;
            var evidence = accumulator.AddEvidence(
                evidenceFile,
                evidenceIndex,
                schema is null
                    ? $"Marten operation uses document {documentType}."
                    : $"Marten schema configures document {documentType}.",
                schema is null ? "marten.document-usage" : "marten.schema",
                documentType);
            var declaration = model.Declarations.GetValueOrDefault(documentType);
            var artifactFile = declaration?.File ?? evidenceFile;
            documentIds[documentType] = accumulator.AddArtifact(
                ArtifactKind.MartenDocument,
                documentType,
                artifactFile,
                [evidence],
                new Dictionary<string, string>
                {
                    ["schemaConfigured"] = (schema is not null).ToString().ToLowerInvariant()
                });
        }

        return documentIds;
    }

    private static void AddOperation(
        MartenOperationFact operation,
        bool hasSave,
        string sessionId,
        string handlerId,
        IReadOnlyList<string> endpointIds,
        IReadOnlyDictionary<string, string> documentIds,
        MartenAnalysisAccumulator accumulator)
    {
        var document = operation.DocumentType ?? string.Empty;
        var operationName = OperationName(operation.Kind);
        var line = MartenTextParsing.LineNumber(operation.Scope.File.Content, operation.Index);
        var evidence = accumulator.AddEvidence(
            operation.Scope.File,
            operation.Index,
            string.IsNullOrEmpty(document)
                ? $"{operation.Scope.Name} calls {operation.Kind}."
                : $"{operation.Scope.Name} performs {operation.Kind} on {document}.",
            $"marten.{operationName}",
            document);
        accumulator.AddArtifact(
            ArtifactKind.MartenOperation,
            $"{operation.Scope.Name}:{operation.Kind}:{document}:{line}",
            operation.Scope.File,
            [evidence],
            new Dictionary<string, string>
            {
                ["kind"] = operationName,
                ["documentType"] = document,
                ["sessionType"] = operation.Scope.SessionType,
                ["pagination"] = operation.HasPagination.ToString().ToLowerInvariant(),
                ["committed"] = (operation.Kind is not (MartenOperationKind.Store or MartenOperationKind.Delete) || hasSave)
                    .ToString()
                    .ToLowerInvariant()
            });

        if (string.IsNullOrEmpty(document) || !documentIds.TryGetValue(document, out var documentId))
        {
            return;
        }

        var dependencyKind = operation.Kind switch
        {
            MartenOperationKind.Store => DependencyKind.Writes,
            MartenOperationKind.Delete => DependencyKind.Deletes,
            _ => DependencyKind.Reads
        };
        accumulator.AddDependency(sessionId, documentId, dependencyKind, evidence);
        if (operation.Scope.IsHandler)
        {
            accumulator.AddDependency(handlerId, documentId, dependencyKind, evidence);
            foreach (var endpointId in endpointIds)
            {
                accumulator.AddDependency(endpointId, documentId, dependencyKind, evidence);
            }
        }
    }

    private static string ResolveHandlerId(
        MartenTypeScope scope,
        IReadOnlyDictionary<string, string> aspNetHandlers) =>
        aspNetHandlers.GetValueOrDefault($"{scope.File.RelativePath}\0{scope.Name}") ??
        StableIdentity.Create(
            "artifact",
            "aspnet",
            ArtifactKind.Handler.ToString(),
            scope.File.RelativePath,
            scope.Name);

    private static string OperationName(MartenOperationKind kind) => kind switch
    {
        MartenOperationKind.Query => "query",
        MartenOperationKind.Load => "load",
        MartenOperationKind.Store => "store",
        MartenOperationKind.Delete => "delete",
        MartenOperationKind.SaveChanges => "saveChanges",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
