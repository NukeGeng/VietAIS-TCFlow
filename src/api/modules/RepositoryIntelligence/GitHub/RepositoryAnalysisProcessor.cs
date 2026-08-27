using FSH.Framework.Core.Exceptions;
using Marten;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Governance;
using VietAIS.TCFlow.Analyzers.Knowledge;
using VietAIS.TCFlow.Analyzers.Monitoring;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;
using AnalyzerConventionKind = VietAIS.TCFlow.Analyzers.Governance.ConventionKind;
using AnalyzerConventionProfile = VietAIS.TCFlow.Analyzers.Governance.RepositoryConventionProfile;
using ApiConventionProfile = VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management.ConventionProfile;
using ApiConventionProfileStatus = VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management.ConventionProfileStatus;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.GitHub;

internal sealed class GitHubRepositorySnapshotSource(
    IQuerySession session,
    IGitHubAppClient gitHub) : IRepositorySnapshotSource
{
    public async Task<RepositorySnapshot> LoadAsync(
        RepositoryAnalysisWorkItem workItem,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(workItem.ProjectId, out var projectId) ||
            !Guid.TryParse(workItem.RepositoryId, out var repositoryId))
        {
            throw new InvalidOperationException("Repository analysis identities are invalid.");
        }

        var repository = await session.LoadAsync<ProjectRepository>(repositoryId, cancellationToken);
        if (repository is null || repository.ProjectId != projectId ||
            repository.Provider != RepositoryProviderKind.GitHub ||
            repository.Status != RepositoryLifecycleStatus.Active)
        {
            throw new NotFoundException("Active GitHub project repository not found.");
        }

        var access = await session.Query<GitHubRepositoryAccess>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == projectId &&
                    item.ProjectRepositoryId == repositoryId &&
                    item.IsSelected,
                cancellationToken)
            ?? throw new ForbiddenException(
                "Repository is not selected within the project's GitHub App installation.");
        var reference = NormalizeReference(
            workItem.HeadRevision ?? workItem.Reference ?? repository.DefaultBranch);
        var snapshot = await gitHub.GetRepositorySnapshotAsync(
            access.InstallationId,
            access.FullName,
            reference,
            cancellationToken);
        return new RepositorySnapshot(
            snapshot.Revision,
            snapshot.Files.Select(file => new RepositoryFile(
                    file.Path,
                    $"/{file.Path}",
                    file.Content))
                .ToArray());
    }

    private static string NormalizeReference(string value)
    {
        const string branchPrefix = "refs/heads/";
        var reference = value.Trim();
        return reference.StartsWith(branchPrefix, StringComparison.Ordinal)
            ? reference[branchPrefix.Length..]
            : reference;
    }
}

internal sealed class GitHubIncrementalChangeSource(
    IQuerySession session,
    IGitHubAppClient gitHub) : IIncrementalChangeSource
{
    private const string NullRevision = "0000000000000000000000000000000000000000";

    public async Task<IncrementalChangeSet> LoadAsync(
        RepositoryAnalysisWorkItem workItem,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(workItem.ProjectId, out var projectId) ||
            !Guid.TryParse(workItem.RepositoryId, out var repositoryId))
        {
            throw new InvalidOperationException("Repository analysis identities are invalid.");
        }

        var access = await session.Query<GitHubRepositoryAccess>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == projectId &&
                    item.ProjectRepositoryId == repositoryId &&
                    item.IsSelected,
                cancellationToken)
            ?? throw new ForbiddenException(
                "Repository is not selected within the project's GitHub App installation.");
        var beforeRevision = RequiredRevision(workItem.BaseRevision, "base");
        var afterRevision = RequiredRevision(workItem.HeadRevision, "head");
        var beforeTask = LoadSnapshotAsync(access, beforeRevision, cancellationToken);
        var afterTask = LoadSnapshotAsync(access, afterRevision, cancellationToken);
        await Task.WhenAll(beforeTask, afterTask);
        var before = await beforeTask;
        var after = await afterTask;
        var beforeFiles = before.Files.ToDictionary(file => file.Path, StringComparer.Ordinal);
        var afterFiles = after.Files.ToDictionary(file => file.Path, StringComparer.Ordinal);
        var changes = workItem.RequiresContentFetch
            ? DiscoverChanges(beforeFiles, afterFiles)
            : MapDeclaredChanges(workItem.ChangedPaths, beforeFiles, afterFiles);
        if (changes.Count == 0)
        {
            throw new NoAnalyzableRepositoryChangesException(
                "The source event contains no supported text-file changes.");
        }

        return new IncrementalChangeSet(
            changes,
            after.Files.Select(file => new RepositoryFile(
                    file.Path,
                    $"/{file.Path}",
                    file.Content))
                .ToArray());
    }

    private async Task<GitHubRepositorySnapshot> LoadSnapshotAsync(
        GitHubRepositoryAccess access,
        string revision,
        CancellationToken cancellationToken) =>
        string.Equals(revision, NullRevision, StringComparison.Ordinal)
            ? new GitHubRepositorySnapshot(revision, [])
            : await gitHub.GetRepositorySnapshotAsync(
                access.InstallationId,
                access.FullName,
                revision,
                cancellationToken);

    private static IReadOnlyList<SourceFileChange> DiscoverChanges(
        IReadOnlyDictionary<string, GitHubRepositorySnapshotFile> before,
        IReadOnlyDictionary<string, GitHubRepositorySnapshotFile> after) =>
        before.Keys.Concat(after.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(path => CreateDiscoveredChange(path, before, after))
            .Where(change => change is not null)
            .Cast<SourceFileChange>()
            .ToArray();

    private static SourceFileChange? CreateDiscoveredChange(
        string path,
        IReadOnlyDictionary<string, GitHubRepositorySnapshotFile> before,
        IReadOnlyDictionary<string, GitHubRepositorySnapshotFile> after)
    {
        var hasBefore = before.TryGetValue(path, out var beforeFile);
        var hasAfter = after.TryGetValue(path, out var afterFile);
        if (hasBefore && hasAfter &&
            string.Equals(beforeFile!.Content, afterFile!.Content, StringComparison.Ordinal))
        {
            return null;
        }

        var kind = ChangeKind.Added;
        if (hasBefore)
        {
            kind = hasAfter ? ChangeKind.Modified : ChangeKind.Deleted;
        }
        return new SourceFileChange(path, beforeFile?.Content, afterFile?.Content, kind);
    }

    private static IReadOnlyList<SourceFileChange> MapDeclaredChanges(
        IReadOnlyList<RepositoryChangedPath> declared,
        IReadOnlyDictionary<string, GitHubRepositorySnapshotFile> before,
        IReadOnlyDictionary<string, GitHubRepositorySnapshotFile> after) =>
        declared.OrderBy(change => change.Path, StringComparer.Ordinal)
            .Select(change =>
            {
                before.TryGetValue(change.Path, out var beforeFile);
                after.TryGetValue(change.Path, out var afterFile);
                return beforeFile is null && afterFile is null
                    ? null
                    : new SourceFileChange(
                        change.Path,
                        beforeFile?.Content,
                        afterFile?.Content,
                        change.Kind);
            })
            .Where(change => change is not null)
            .Cast<SourceFileChange>()
            .ToArray();

    private static string RequiredRevision(string? revision, string label) =>
        string.IsNullOrWhiteSpace(revision)
            ? throw new InvalidOperationException(
                $"Incremental GitHub analysis requires a {label} revision.")
            : revision.Trim();
}

internal enum RepositoryAnalysisProcessingOutcome
{
    Completed,
    Unsupported,
    Ignored,
    AwaitingReasoning
}

internal sealed record RepositoryAnalysisProcessingResult(
    RepositoryAnalysisProcessingOutcome Outcome,
    string SourceRevision,
    IReadOnlyList<string> Technologies,
    RepositoryKnowledgeGraph Graph,
    int GeneratedTaskCount,
    IReadOnlyList<AnalyzerDiagnostic> Diagnostics);

internal sealed class RepositoryAnalysisProcessor(
    IDocumentSession session,
    InitialRepositoryAnalysisService initialAnalysis,
    IncrementalMonitoringService incrementalAnalysis,
    TimeProvider timeProvider)
{
    private static readonly Guid SystemActorId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    public async Task<RepositoryAnalysisProcessingResult> ProcessAsync(
        RepositoryAnalysisWorkItem workItem,
        CancellationToken cancellationToken)
    {
        if (workItem.Kind == RepositoryAnalysisKind.FullScan &&
            workItem.Trigger == RepositoryAnalysisTrigger.InitialScan)
        {
            return await ProcessInitialAsync(workItem, cancellationToken);
        }

        if (workItem.Kind == RepositoryAnalysisKind.Incremental &&
            workItem.Trigger != RepositoryAnalysisTrigger.InitialScan)
        {
            return await ProcessIncrementalAsync(workItem, cancellationToken);
        }

        throw new InvalidOperationException("Repository analysis work kind and trigger do not match.");
    }

    private async Task<RepositoryAnalysisProcessingResult> ProcessInitialAsync(
        RepositoryAnalysisWorkItem workItem,
        CancellationToken cancellationToken)
    {
        var currentGraph = await new MartenKnowledgeGraphReader(session).LoadAsync(
            workItem.RepositoryId,
            cancellationToken);
        var result = await initialAnalysis.ProcessAsync(
            workItem,
            (currentGraph?.Revision ?? 0) + 1,
            cancellationToken);
        await new MartenKnowledgeGraphWriter(session, timeProvider).SaveAsync(
            result.Graph,
            cancellationToken);
        await new MartenConventionProfileWriter(session, timeProvider).SaveAsync(
            result.Conventions,
            cancellationToken);
        await UpdateProjectConventionProfileAsync(
            workItem,
            result.Conventions,
            result.Status,
            cancellationToken);

        return new RepositoryAnalysisProcessingResult(
            result.Status == InitialRepositoryAnalysisStatus.Unsupported
                ? RepositoryAnalysisProcessingOutcome.Unsupported
                : RepositoryAnalysisProcessingOutcome.Completed,
            result.SourceRevision,
            result.Technologies.Select(technology => technology.Technology.ToString())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            result.Graph,
            GeneratedTaskCount: 0,
            result.Diagnostics);
    }

    private async Task<RepositoryAnalysisProcessingResult> ProcessIncrementalAsync(
        RepositoryAnalysisWorkItem workItem,
        CancellationToken cancellationToken)
    {
        var currentGraph = await new MartenKnowledgeGraphReader(session).LoadAsync(
            workItem.RepositoryId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Incremental analysis requires a completed initial repository graph.");
        IncrementalMonitoringResult result;
        try
        {
            result = await incrementalAnalysis.ProcessAsync(
                workItem,
                currentGraph,
                cancellationToken);
        }
        catch (NoAnalyzableRepositoryChangesException exception)
        {
            return new RepositoryAnalysisProcessingResult(
                RepositoryAnalysisProcessingOutcome.Ignored,
                workItem.HeadRevision!,
                Technologies(currentGraph),
                currentGraph,
                GeneratedTaskCount: 0,
                [new AnalyzerDiagnostic(
                    "ANALYSIS002",
                    exception.Message,
                    EvidenceLevel.Confirmed)]);
        }

        if (result.Graph.Revision > currentGraph.Revision)
        {
            await new MartenKnowledgeGraphWriter(session, timeProvider).SaveAsync(
                result.Graph,
                cancellationToken);
        }
        else
        {
            await session.SaveChangesAsync(cancellationToken);
        }
        var verification = RepositoryTaskVerificationBatch.Empty;
        if (result.Status is not IncrementalMonitoringStatus.Ignored and
            not IncrementalMonitoringStatus.Duplicate)
        {
            if (!Guid.TryParse(workItem.ProjectId, out var projectId) ||
                !Guid.TryParse(workItem.RepositoryId, out var repositoryId))
            {
                throw new InvalidOperationException("Analysis verification identities are invalid.");
            }

            verification = await new RepositoryTaskVerificationService(session, timeProvider)
                .VerifyAsync(projectId, repositoryId, currentGraph, result.Graph, cancellationToken);
            if (verification.CandidateCount > 0)
            {
                await session.SaveChangesAsync(cancellationToken);
            }
        }

        var diagnostics = new List<AnalyzerDiagnostic>
        {
            new(
                result.DeepReasoning is null ? "ANALYSIS003" : "ANALYSIS004",
                result.Reason,
                result.DeepReasoning is null ? EvidenceLevel.Confirmed : EvidenceLevel.Inferred)
        };
        if (verification.CandidateCount > 0)
        {
            diagnostics.Add(new AnalyzerDiagnostic(
                "ANALYSIS006",
                VerificationSummary(verification),
                EvidenceLevel.Confirmed));
        }
        return new RepositoryAnalysisProcessingResult(
            result.Status switch
            {
                IncrementalMonitoringStatus.Ignored or IncrementalMonitoringStatus.Duplicate =>
                    RepositoryAnalysisProcessingOutcome.Ignored,
                IncrementalMonitoringStatus.DeepReasoningQueued =>
                    RepositoryAnalysisProcessingOutcome.AwaitingReasoning,
                _ => RepositoryAnalysisProcessingOutcome.Completed
            },
            workItem.HeadRevision ?? currentGraph.RepositoryId,
            Technologies(result.Graph),
            result.Graph,
            GeneratedTaskCount: 0,
            diagnostics);
    }

    private static string VerificationSummary(RepositoryTaskVerificationBatch verification) =>
        verification.SkippedByPolicyCount > 0
            ? $"Source verification skipped {verification.SkippedByPolicyCount} task(s) because " +
                $"'{ProjectPermissionCodes.AiTaskUpdate}' is not allowed by project AI policy."
            : $"Source verification evaluated {verification.CandidateCount} task(s): " +
                $"{verification.PassedCount} passed, {verification.FailedCount} failed, " +
                $"{verification.InconclusiveCount} inconclusive; {verification.UpdatedCount} task(s) updated.";

    private static string[] Technologies(RepositoryKnowledgeGraph graph) =>
        graph.Artifacts.Select(artifact => artifact.Technology)
            .Where(technology => !string.IsNullOrWhiteSpace(technology))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private async Task UpdateProjectConventionProfileAsync(
        RepositoryAnalysisWorkItem workItem,
        AnalyzerConventionProfile detected,
        InitialRepositoryAnalysisStatus status,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(workItem.ProjectId, out var projectId))
        {
            throw new InvalidOperationException("Analysis project identity is invalid.");
        }

        var current = await session.LoadAsync<ApiConventionProfile>(projectId, cancellationToken)
            ?? throw new NotFoundException("Project convention profile not found.");
        if (status == InitialRepositoryAnalysisStatus.Unsupported)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var updated = current with
        {
            Status = ApiConventionProfileStatus.Confirmed,
            Architectures = Values(detected, AnalyzerConventionKind.Architecture),
            ApiStyles = Values(detected, AnalyzerConventionKind.ApiStyle),
            PersistencePatterns = Values(detected, AnalyzerConventionKind.Persistence),
            ValidationPatterns = Values(detected, AnalyzerConventionKind.Validation),
            DtoPatterns = Values(
                detected,
                AnalyzerConventionKind.RequestDtoNaming,
                AnalyzerConventionKind.ResponseDtoNaming),
            UpdatedAt = now,
            UpdatedBy = SystemActorId
        };
        session.Store(updated);
        await session.SaveChangesAsync(cancellationToken);
    }

    private static string[] Values(
        AnalyzerConventionProfile profile,
        params AnalyzerConventionKind[] kinds) =>
        profile.Observations
            .Where(observation => kinds.Contains(observation.Kind))
            .Select(observation => observation.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
