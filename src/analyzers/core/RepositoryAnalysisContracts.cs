namespace VietAIS.TCFlow.Analyzers.Core;

public enum RepositoryAnalysisKind
{
    FullScan,
    Incremental
}

public enum RepositoryAnalysisTrigger
{
    InitialScan,
    Push,
    PullRequest,
    Merge
}

public enum RepositoryAnalysisRequesterKind
{
    User,
    System
}

public sealed record RepositoryChangedPath(
    string Path,
    ChangeKind Kind);

public sealed record RepositoryAnalysisWorkItem(
    string RequestId,
    string ProjectId,
    string RepositoryId,
    string CorrelationId,
    string SourceProvider,
    RepositoryAnalysisKind Kind,
    RepositoryAnalysisTrigger Trigger,
    string? BaseRevision,
    string? HeadRevision,
    string? Reference,
    int? PullRequestNumber,
    bool RequiresContentFetch,
    IReadOnlyList<RepositoryChangedPath> ChangedPaths,
    DateTimeOffset RequestedAt,
    RepositoryAnalysisRequesterKind RequesterKind,
    string? RequestedBy);
