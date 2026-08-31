using VietAIS.TCFlow.Modules.Planning.Contracts.Queries;

namespace VietAIS.TCFlow.Modules.Planning.Projections;

public sealed class PlanCurrent
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Purpose { get; set; }
    public List<RequirementView> Requirements { get; set; } = [];
    public List<MilestoneView> Milestones { get; set; } = [];
    public long Version { get; set; }
    public DateTimeOffset LastChangedAtUtc { get; set; }
}
