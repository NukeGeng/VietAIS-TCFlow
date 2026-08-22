using System.Text.Json;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Marten;
using Xunit;

namespace VietAIS.TCFlow.Analyzers.Marten.Tests;

public sealed class MartenAnalyzerFixtureTests
{
    [Fact]
    public async Task TcFlowFixtureMatchesDocumentsSessionsOperationsAndMissingSaveGroundTruth()
    {
        var fixtureRoot = Path.Combine(RepositoryRoot, "samples", "marten-full-application");
        var result = await AnalyzeFixture(fixtureRoot);
        var expected = JsonSerializer.Deserialize<ExpectedAnalysis>(
            await File.ReadAllTextAsync(
                Path.Combine(fixtureRoot, "expected", "marten-analysis.json"),
                TestContext.Current.CancellationToken),
            AnalysisJson.Options)!;

        var documents = result.Artifacts
            .Where(artifact => artifact.Kind == ArtifactKind.MartenDocument)
            .Select(artifact => new ExpectedDocument(
                artifact.Name,
                artifact.Path,
                bool.Parse(artifact.Metadata["schemaConfigured"])))
            .OrderBy(document => document.Name, StringComparer.Ordinal)
            .ToArray();
        AssertJsonEqual(expected.Documents, documents);

        var sessions = result.Artifacts
            .Where(artifact => artifact.Kind == ArtifactKind.MartenSession)
            .Select(artifact => new ExpectedSession(
                artifact.Name,
                artifact.Metadata["sessionType"],
                bool.Parse(artifact.Metadata["hasSaveChanges"]),
                int.Parse(artifact.Metadata["writeCount"])))
            .OrderBy(session => SessionOrder(session.Name))
            .ToArray();
        AssertJsonEqual(expected.Sessions, sessions);

        var operations = result.Artifacts
            .Where(artifact => artifact.Kind == ArtifactKind.MartenOperation)
            .Select(artifact =>
            {
                var evidence = result.Evidence.Single(item => artifact.EvidenceIds.Contains(item.Id));
                return new ExpectedOperation(
                    artifact.Name.Split(':', 2)[0],
                    artifact.Metadata["kind"],
                    artifact.Metadata["documentType"],
                    evidence.Location.StartLine,
                    bool.Parse(artifact.Metadata["pagination"]),
                    bool.Parse(artifact.Metadata["committed"]));
            })
            .OrderBy(operation => operation.Line)
            .ToArray();
        AssertJsonEqual(expected.Operations, operations);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(expected.MissingSave.Code, diagnostic.Code);
        Assert.Equal(expected.MissingSave.Path, diagnostic.Location?.Path);
        Assert.Equal(expected.MissingSave.Line, diagnostic.Location?.StartLine);
        Assert.Equal(expected.MissingSave.Owner, diagnostic.Location?.Symbol);
        Assert.Equal(EvidenceLevel.Confirmed, diagnostic.Level);
    }

    [Fact]
    public async Task DependenciesConnectEndpointAndHandlersToDocuments()
    {
        var fixtureRoot = Path.Combine(RepositoryRoot, "samples", "marten-full-application");
        var result = await AnalyzeFixture(fixtureRoot);
        var project = result.Artifacts.Single(artifact =>
            artifact.Kind == ArtifactKind.MartenDocument && artifact.Name == "Project");
        var repository = result.Artifacts.Single(artifact =>
            artifact.Kind == ArtifactKind.MartenDocument && artifact.Name == "ProjectRepository");
        var featuresPath = "src/api/modules/RepositoryIntelligence/Management/ProjectFeatures.cs";
        var endpointsPath = "src/api/modules/RepositoryIntelligence/Management/ProjectEndpoints.cs";
        var createHandler = HandlerId(featuresPath, "CreateProjectHandler");
        var searchHandler = HandlerId(featuresPath, "SearchProjectsHandler");
        var deleteHandler = HandlerId(featuresPath, "DeleteProjectHandler");
        var missingSaveHandler = HandlerId(featuresPath, "CreateRepositoryWithoutSaveHandler");
        var createEndpoint = StableIdentity.Create(
            "artifact",
            "aspnet",
            ArtifactKind.AspNetEndpoint.ToString(),
            endpointsPath,
            "CreateProject");

        AssertDependency(result, createHandler, project.Id, DependencyKind.Writes);
        AssertDependency(result, createEndpoint, project.Id, DependencyKind.Writes);
        AssertDependency(result, searchHandler, project.Id, DependencyKind.Reads);
        AssertDependency(result, deleteHandler, project.Id, DependencyKind.Reads);
        AssertDependency(result, deleteHandler, project.Id, DependencyKind.Deletes);
        AssertDependency(result, missingSaveHandler, repository.Id, DependencyKind.Writes);
    }

    [Fact]
    public async Task AnalysisIsDeterministicAndDoesNotIntroduceEventSourcing()
    {
        var fixtureRoot = Path.Combine(RepositoryRoot, "samples", "marten-full-application");
        var files = await DiscoverFixture(fixtureRoot);
        var analyzer = new MartenAnalyzer();

        var first = await analyzer.AnalyzeAsync(files, TestContext.Current.CancellationToken);
        var second = await analyzer.AnalyzeAsync(files.Reverse().ToArray(), TestContext.Current.CancellationToken);

        Assert.Equal(
            JsonSerializer.Serialize(first, AnalysisJson.Options),
            JsonSerializer.Serialize(second, AnalysisJson.Options));
        Assert.DoesNotContain(first.Artifacts, artifact => artifact.Name.Contains("Event", StringComparison.Ordinal));
        Assert.DoesNotContain(files, file =>
            file.Content.Contains("StartStream", StringComparison.Ordinal) ||
            file.Content.Contains("Append", StringComparison.Ordinal));
    }

    private static void AssertDependency(
        AnalysisResult result,
        string source,
        string target,
        DependencyKind kind) => Assert.Contains(result.Dependencies, dependency =>
            dependency.SourceArtifactId == source && dependency.Target == target && dependency.Kind == kind);

    private static string HandlerId(string path, string name) => StableIdentity.Create(
        "artifact",
        "aspnet",
        ArtifactKind.Handler.ToString(),
        path,
        name);

    private static int SessionOrder(string name) => name switch
    {
        "CreateProjectHandler" => 0,
        "GetProjectHandler" => 1,
        "SearchProjectsHandler" => 2,
        "DeleteProjectHandler" => 3,
        _ => 4
    };

    private static void AssertJsonEqual<T>(T expected, T actual) => Assert.Equal(
        JsonSerializer.Serialize(expected, AnalysisJson.Options),
        JsonSerializer.Serialize(actual, AnalysisJson.Options));

    private static async Task<AnalysisResult> AnalyzeFixture(string fixtureRoot)
    {
        var files = await DiscoverFixture(fixtureRoot);
        return await new MartenAnalyzer().AnalyzeAsync(files, TestContext.Current.CancellationToken);
    }

    private static Task<IReadOnlyList<RepositoryFile>> DiscoverFixture(string fixtureRoot) =>
        new FileDiscovery().DiscoverAsync(
            fixtureRoot,
            new FileDiscoveryOptions(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".cs" }),
            TestContext.Current.CancellationToken);

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PROJECT_PLAN.md")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
        }
    }

    private sealed record ExpectedAnalysis(
        IReadOnlyList<ExpectedDocument> Documents,
        IReadOnlyList<ExpectedSession> Sessions,
        IReadOnlyList<ExpectedOperation> Operations,
        ExpectedDiagnostic MissingSave);

    private sealed record ExpectedDocument(string Name, string Path, bool SchemaConfigured);

    private sealed record ExpectedSession(
        string Name,
        string SessionType,
        bool HasSaveChanges,
        int WriteCount);

    private sealed record ExpectedOperation(
        string Owner,
        string Kind,
        string DocumentType,
        int Line,
        bool Pagination,
        bool Committed);

    private sealed record ExpectedDiagnostic(string Code, string Path, int Line, string Owner);
}
