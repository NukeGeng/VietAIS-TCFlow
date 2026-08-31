using Marten.Events.Aggregation;
using VietAIS.TCFlow.Modules.Planning.Contracts.Queries;
using VietAIS.TCFlow.Modules.Planning.Domain;

namespace VietAIS.TCFlow.Modules.Planning.Projections;

public static class PlanningProjectionNames
{
    public const string Current = "planning-current";
    public const string Overview = "planning-overview";
}

public sealed class PlanCurrentProjection : SingleStreamProjection<PlanCurrent, Guid>
{
    public PlanCurrentProjection() => Name = PlanningProjectionNames.Current;

    public static PlanCurrent Create(PlanCreated @event) => new()
    {
        Id = @event.PlanId,
        ProjectId = @event.ProjectId,
        Name = @event.Name,
        Purpose = @event.Purpose,
        LastChangedAtUtc = @event.OccurredAtUtc
    };

    public static void Apply(PlanRenamed @event, PlanCurrent current)
    {
        current.Name = @event.Name;
        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }

    public static void Apply(RequirementAdded @event, PlanCurrent current)
    {
        current.Requirements.Add(new RequirementView(@event.RequirementId, @event.Title, @event.Description));
        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }

    public static void Apply(MilestoneAdded @event, PlanCurrent current)
    {
        current.Milestones.Add(new MilestoneView(@event.MilestoneId, @event.Name, @event.TargetDate));
        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }
}

public sealed class PlanningOverviewProjection : SingleStreamProjection<PlanningOverview, Guid>
{
    public PlanningOverviewProjection() => Name = PlanningProjectionNames.Overview;

    public static PlanningOverview Create(PlanCreated @event) => new()
    {
        Id = @event.PlanId,
        ProjectId = @event.ProjectId,
        Name = @event.Name,
        LastChangedAtUtc = @event.OccurredAtUtc
    };

    public static void Apply(PlanRenamed @event, PlanningOverview current)
    {
        current.Name = @event.Name;
        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }

    public static void Apply(RequirementAdded @event, PlanningOverview current)
    {
        current.RequirementCount++;
        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }

    public static void Apply(MilestoneAdded @event, PlanningOverview current)
    {
        current.MilestoneCount++;
        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }
}
