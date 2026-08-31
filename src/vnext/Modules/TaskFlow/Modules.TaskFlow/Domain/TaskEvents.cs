using TaskStatus = VietAIS.TCFlow.Modules.TaskFlow.Contracts.Queries.TaskStatus;

namespace VietAIS.TCFlow.Modules.TaskFlow.Domain;

public sealed record TaskProposed(
    Guid TaskId,
    Guid ProjectId,
    string Title,
    string? Description,
    string? SourceChangeKey,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);

public sealed record TaskAccepted(Guid TaskId, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record TaskRejected(Guid TaskId, string Reason, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record TaskAssigned(Guid TaskId, string AssigneeId, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record TaskStarted(Guid TaskId, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record TaskBlocked(Guid TaskId, string Reason, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record AiVerificationCompleted(Guid TaskId, bool Passed, string Summary, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record ReviewRequested(Guid TaskId, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record ReviewApproved(Guid TaskId, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record ReviewRejected(Guid TaskId, string Reason, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record TaskCompleted(Guid TaskId, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record TaskReopened(Guid TaskId, string Reason, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record TaskUpdatedFromSourceChange(Guid TaskId, string Title, string? Description, string SourceChangeKey, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);

/// <summary>
/// Migration/reconciliation snapshot for fields that cannot be represented by
/// a synthetic sequence of user transitions without changing business history.
/// It is emitted only by an approved migration mapper and remains replayable.
/// </summary>
public sealed record TaskLifecycleReconciled(
    Guid TaskId,
    TaskStatus Status,
    string? AssigneeId,
    bool AiVerificationPassed,
    bool HumanReviewRequested,
    bool HumanReviewApproved,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);
