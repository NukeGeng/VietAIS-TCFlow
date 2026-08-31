namespace VietAIS.TCFlow.Modules.Projects.Domain;

public sealed record ProjectCreated(
    Guid ProjectId,
    string Name,
    string OwnerId,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);

public sealed record ProjectRenamed(
    Guid ProjectId,
    string Name,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);

public sealed record ProjectSuspended(
    Guid ProjectId,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);

public sealed record ProjectActivated(
    Guid ProjectId,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);

/// <summary>
/// Records a legacy lifecycle snapshot during a controlled migration. It is a
/// business event because the lifecycle state affects project availability and
/// authorization, while the migration metadata identifies its source record.
/// </summary>
public sealed record ProjectLifecycleReconciled(
    Guid ProjectId,
    bool IsSuspended,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);
