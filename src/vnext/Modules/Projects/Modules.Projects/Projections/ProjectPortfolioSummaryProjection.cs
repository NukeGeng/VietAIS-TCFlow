using Marten.Events.Aggregation;
using VietAIS.TCFlow.Modules.Projects.Domain;

namespace VietAIS.TCFlow.Modules.Projects.Projections;

/// <summary>
/// Async reporting projection. It is intentionally separate from the inline
/// current-state view so dashboard/search lag is explicit and rebuildable.
/// </summary>
public sealed class ProjectPortfolioSummaryProjection : SingleStreamProjection<ProjectPortfolioSummary, Guid>
{
    public ProjectPortfolioSummaryProjection() => Name = ProjectProjectionNames.PortfolioSummary;

    public static ProjectPortfolioSummary Create(ProjectCreated @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return new ProjectPortfolioSummary
        {
            Id = @event.ProjectId,
            Name = @event.Name,
            LastChangedAtUtc = @event.OccurredAtUtc
        };
    }

    public static void Apply(ProjectRenamed @event, ProjectPortfolioSummary current)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(current);
        current.Name = @event.Name;
        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }

    public static void Apply(ProjectSuspended @event, ProjectPortfolioSummary current)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(current);
        current.IsSuspended = true;
        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }

    public static void Apply(ProjectActivated @event, ProjectPortfolioSummary current)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(current);
        current.IsSuspended = false;
        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }
}
