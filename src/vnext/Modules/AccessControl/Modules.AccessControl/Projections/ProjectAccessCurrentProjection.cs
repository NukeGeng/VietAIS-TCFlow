using Marten.Events.Aggregation;
using VietAIS.TCFlow.Modules.AccessControl.Contracts.Models;
using VietAIS.TCFlow.Modules.AccessControl.Domain;

namespace VietAIS.TCFlow.Modules.AccessControl.Projections;

public sealed class ProjectAccessCurrentProjection : SingleStreamProjection<ProjectAccessCurrent, Guid>
{
    public ProjectAccessCurrentProjection() => Name = AccessControlProjectionNames.Current;

    public static ProjectAccessCurrent Create(ProjectAccessInitialized @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return new ProjectAccessCurrent
        {
            Id = ProjectAccessStreamIdentity.ForProject(@event.ProjectId),
            ProjectId = @event.ProjectId,
            OwnerId = @event.OwnerId,
            Roles =
            [
                new ProjectRoleView(
                    @event.OwnerRoleId,
                    "Owner",
                    true,
                    ProjectPermissionCatalog.OwnerGrants)
            ],
            Members = [new ProjectMemberView(@event.OwnerId, true, [@event.OwnerRoleId])],
            LastChangedAtUtc = @event.OccurredAtUtc
        };
    }

    public static void Apply(ProjectRoleCreated @event, ProjectAccessCurrent current)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(current);
        current.Roles.Add(new ProjectRoleView(@event.RoleId, @event.Name, false, []));
        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }

    public static void Apply(ProjectRolePermissionsUpdated @event, ProjectAccessCurrent current)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(current);
        var index = current.Roles.FindIndex(role => role.RoleId == @event.RoleId);
        if (index >= 0)
        {
            current.Roles[index] = current.Roles[index] with { Grants = @event.Grants };
        }

        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }

    public static void Apply(ProjectMemberAdded @event, ProjectAccessCurrent current)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(current);
        if (current.Members.All(member => !string.Equals(member.UserId, @event.UserId, StringComparison.Ordinal)))
        {
            current.Members.Add(new ProjectMemberView(@event.UserId, true, []));
        }

        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }

    public static void Apply(ProjectMemberRolesAssigned @event, ProjectAccessCurrent current)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(current);
        var index = current.Members.FindIndex(member =>
            string.Equals(member.UserId, @event.UserId, StringComparison.Ordinal));
        if (index >= 0)
        {
            current.Members[index] = current.Members[index] with { RoleIds = @event.RoleIds };
        }

        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }

    public static void Apply(ProjectMemberRemoved @event, ProjectAccessCurrent current)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(current);
        current.Members.RemoveAll(member =>
            string.Equals(member.UserId, @event.UserId, StringComparison.Ordinal));
        current.LastChangedAtUtc = @event.OccurredAtUtc;
    }
}
