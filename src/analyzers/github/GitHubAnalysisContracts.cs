namespace VietAIS.TCFlow.Analyzers.GitHub;

public enum GitHubAnalysisTrigger
{
    InitialScan,
    Push,
    PullRequest,
    Merge
}

public enum GitHubAnalysisRequestStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Ignored
}

public enum GitHubChangedFileStatus
{
    Added,
    Modified,
    Removed,
    Renamed
}

public sealed record GitHubChangedFileContract(
    string Path,
    GitHubChangedFileStatus Status);

public sealed record GitHubAnalysisRequestContract(
    Guid Id,
    Guid ProjectId,
    Guid RepositoryId,
    GitHubAnalysisTrigger Trigger,
    string? DeliveryId,
    string? BaseRevision,
    string? HeadRevision,
    string? Reference,
    int? PullRequestNumber,
    bool FullScan,
    bool RequiresChangedFileFetch,
    IReadOnlyList<GitHubChangedFileContract> ChangedFiles,
    GitHubAnalysisRequestStatus Status,
    DateTimeOffset RequestedAt,
    string RequestedByType,
    Guid? RequestedBy);
