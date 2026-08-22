namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.GitHub;

public enum GitHubAccountKind
{
    User,
    Organization
}

public enum GitHubRepositorySelectionKind
{
    Selected,
    All
}

public enum GitHubInstallationStatus
{
    Active,
    Suspended,
    Deleted
}

public enum GitHubAnalysisTriggerKind
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

public sealed record GitHubAppInstallation(
    Guid Id,
    Guid ProjectId,
    long InstallationId,
    long AccountId,
    string AccountLogin,
    GitHubAccountKind AccountKind,
    GitHubRepositorySelectionKind RepositorySelection,
    GitHubInstallationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

public sealed record GitHubRepositoryAccess(
    Guid Id,
    Guid ProjectId,
    Guid ProjectRepositoryId,
    Guid InstallationDocumentId,
    long InstallationId,
    long GitHubRepositoryId,
    string FullName,
    bool IsSelected,
    DateTimeOffset SelectedAt,
    Guid SelectedBy);

public sealed record GitHubChangedFile(string Path, GitHubChangedFileStatus Status);

public sealed record GitHubWebhookDelivery(
    string Id,
    Guid ProjectId,
    Guid ProjectRepositoryId,
    long InstallationId,
    long GitHubRepositoryId,
    string Event,
    string Action,
    string PayloadSha256,
    DateTimeOffset ReceivedAt);

public sealed record RepositoryAnalysisRequest(
    Guid Id,
    Guid ProjectId,
    Guid RepositoryId,
    GitHubAnalysisTriggerKind Trigger,
    string? DeliveryId,
    string? BaseRevision,
    string? HeadRevision,
    string? Reference,
    int? PullRequestNumber,
    bool FullScan,
    bool RequiresChangedFileFetch,
    GitHubChangedFile[] ChangedFiles,
    GitHubAnalysisRequestStatus Status,
    DateTimeOffset RequestedAt,
    string RequestedByType,
    Guid? RequestedBy);

public sealed record ConnectedGitHubRepository(
    Management.ProjectRepository Repository,
    GitHubRepositoryAccess Access);

public sealed record GitHubWebhookReceipt(
    bool Accepted,
    bool Duplicate,
    string Disposition,
    Guid? AnalysisRequestId);

internal sealed record ParsedGitHubWebhook(
    long InstallationId,
    long GitHubRepositoryId,
    string Event,
    string Action,
    GitHubAnalysisTriggerKind Trigger,
    string? BaseRevision,
    string? HeadRevision,
    string? Reference,
    int? PullRequestNumber,
    bool RequiresChangedFileFetch,
    GitHubChangedFile[] ChangedFiles);
