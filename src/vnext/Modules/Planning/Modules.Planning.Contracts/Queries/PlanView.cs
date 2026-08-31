namespace VietAIS.TCFlow.Modules.Planning.Contracts.Queries;

public sealed record RequirementView(Guid RequirementId, string Title, string? Description);

public sealed record MilestoneView(Guid MilestoneId, string Name, DateOnly? TargetDate);

public sealed record PlanView(
    Guid PlanId,
    Guid ProjectId,
    string Name,
    string? Purpose,
    IReadOnlyList<RequirementView> Requirements,
    IReadOnlyList<MilestoneView> Milestones,
    long Version,
    DateTimeOffset LastChangedAtUtc);
