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

public enum RepositoryAnalysisRunStatus
{
    Processing,
    Completed,
    Unsupported,
    Failed
}

public enum GitHubChangedFileStatus
{
    Added,
    Modified,
    Removed,
    Renamed
}

public enum GitHubConnectionStage
{
    Installation,
    UserAuthorization
}

public sealed record GitHubConnectionAttempt(
    string Id,
    Guid ProjectId,
    Guid ActorId,
    GitHubConnectionStage Stage,
    long? InstallationId,
    string? CodeChallenge,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConsumedAt);

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

public sealed record RepositoryAnalysisDiagnostic(
    string Code,
    string Message,
    string EvidenceLevel,
    string? Path);

public sealed record RepositoryAnalysisRun(
    Guid Id,
    Guid ProjectId,
    Guid RepositoryId,
    RepositoryAnalysisRunStatus Status,
    int Attempt,
    string? SourceRevision,
    IReadOnlyList<string> Technologies,
    int ArtifactCount,
    int DependencyCount,
    int ContractCount,
    int MismatchCount,
    int GeneratedTaskCount,
    IReadOnlyList<RepositoryAnalysisDiagnostic> Diagnostics,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

public sealed record RepositoryAnalysisDetails(
    RepositoryAnalysisRequest Request,
    RepositoryAnalysisRun? Run);

public sealed record ConnectedGitHubRepository(
    Management.ProjectRepository Repository,
    GitHubRepositoryAccess Access);

public sealed record GitHubRepositorySummary(
    long Id,
    string Name,
    string FullName,
    bool Private,
    string DefaultBranch,
    string HtmlUrl);

public sealed record GitHubInstallationStart(
    string InstallationUrl,
    DateTimeOffset ExpiresAt);

public sealed record GitHubAuthorizationStart(
    Guid ProjectId,
    string AuthorizationUrl,
    string State,
    string CodeVerifier,
    DateTimeOffset ExpiresAt);

public sealed record GitHubConnectionResult(
    Guid ProjectId,
    GitHubAppInstallation Installation,
    IReadOnlyList<GitHubRepositorySummary> Repositories);

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
