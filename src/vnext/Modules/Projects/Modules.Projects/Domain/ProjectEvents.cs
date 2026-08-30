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
