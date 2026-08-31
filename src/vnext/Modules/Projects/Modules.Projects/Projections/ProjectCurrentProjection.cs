using Marten.Events.Aggregation;
using VietAIS.TCFlow.Modules.Projects.Domain;

namespace VietAIS.TCFlow.Modules.Projects.Projections;

/// <summary>
/// Inline read model used by immediate project and authorization queries.
/// </summary>
public sealed class ProjectCurrentProjection : SingleStreamProjection<ProjectCurrent, Guid>
{
    public ProjectCurrentProjection() => Name = ProjectProjectionNames.Current;

    public static ProjectCurrent Create(ProjectCreated @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return new ProjectCurrent
        {
            Id = @event.ProjectId,
            Name = @event.Name,
            OwnerId = @event.OwnerId,
            LastChangedAtUtc = @event.OccurredAtUtc
        };
    }

    public static void Apply(ProjectRenamed @event, ProjectCurrent current)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(current);
        current.Name = @event.Name;
        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }

    public static void Apply(ProjectSuspended @event, ProjectCurrent current)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(current);
        current.IsSuspended = true;
        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }

    public static void Apply(ProjectActivated @event, ProjectCurrent current)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(current);
        current.IsSuspended = false;
        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }
}
