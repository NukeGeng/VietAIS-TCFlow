namespace VietAIS.TCFlow.Modules.Planning.Domain;

public sealed record PlanCreated(
    Guid PlanId,
    Guid ProjectId,
    string Name,
    string? Purpose,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);

public sealed record PlanRenamed(
    Guid PlanId,
    string Name,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);

public sealed record RequirementAdded(
    Guid PlanId,
    Guid RequirementId,
    string Title,
    string? Description,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);

public sealed record MilestoneAdded(
    Guid PlanId,
    Guid MilestoneId,
    string Name,
    DateOnly? TargetDate,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);
