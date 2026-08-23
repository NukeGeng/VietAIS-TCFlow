using FSH.Framework.Core.Exceptions;
using Marten;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Governance;
using VietAIS.TCFlow.Analyzers.Knowledge;
using VietAIS.TCFlow.Analyzers.Monitoring;
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

internal sealed record RepositoryAnalysisProcessingResult(
    InitialRepositoryAnalysisResult Analysis,
    int GeneratedTaskCount);

internal sealed class RepositoryAnalysisProcessor(
    IDocumentSession session,
    InitialRepositoryAnalysisService initialAnalysis,
    TimeProvider timeProvider)
{
    private static readonly Guid SystemActorId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    public async Task<RepositoryAnalysisProcessingResult> ProcessAsync(
        RepositoryAnalysisWorkItem workItem,
        CancellationToken cancellationToken)
    {
        if (workItem.Kind != RepositoryAnalysisKind.FullScan ||
            workItem.Trigger != RepositoryAnalysisTrigger.InitialScan)
        {
            throw new NotSupportedException(
                "Incremental GitHub analysis runtime is not available in the initial-scan worker.");
        }

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

        return new RepositoryAnalysisProcessingResult(result, GeneratedTaskCount: 0);
    }

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
