namespace VietAIS.TCFlow.Modules.Planning.Projections;

public sealed class PlanningOverview
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int RequirementCount { get; set; }
    public int MilestoneCount { get; set; }
    public long Version { get; set; }
    public DateTimeOffset LastChangedAtUtc { get; set; }
}
