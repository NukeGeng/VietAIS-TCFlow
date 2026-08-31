namespace VietAIS.TCFlow.Modules.TaskFlow.Contracts.Queries;

public enum TaskStatus
{
    Suggested,
    Upcoming,
    InProgress,
    Blocked,
    ReadyForReview,
    Completed,
    Rejected,
    Cancelled
}

public sealed record TaskView(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    TaskStatus Status,
    string? AssigneeId,
    bool AiVerificationPassed,
    bool HumanReviewRequested,
    bool HumanReviewApproved,
    string? SourceChangeKey,
    long Version,
    DateTimeOffset LastChangedAtUtc);

public sealed record TaskBoardView(Guid Id, Guid ProjectId, string Title, TaskStatus Status, string? AssigneeId, DateTimeOffset LastChangedAtUtc);

public sealed record TaskAnalyticsView(Guid Id, Guid ProjectId, TaskStatus Status, int TransitionCount, DateTimeOffset LastChangedAtUtc);
