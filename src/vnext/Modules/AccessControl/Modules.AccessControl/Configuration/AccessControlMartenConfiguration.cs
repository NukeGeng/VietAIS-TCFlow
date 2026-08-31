using JasperFx.Events.Projections;
using Marten;
using VietAIS.TCFlow.Modules.AccessControl.Domain;
using VietAIS.TCFlow.Modules.AccessControl.Projections;

namespace VietAIS.TCFlow.Modules.AccessControl.Configuration;

public static class AccessControlMartenConfiguration
{
    public static void Configure(StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Events.AddEventType<ProjectAccessInitialized>();
        options.Events.AddEventType<ProjectRoleCreated>();
        options.Events.AddEventType<ProjectRolePermissionsUpdated>();
        options.Events.AddEventType<ProjectMemberAdded>();
        options.Events.AddEventType<ProjectMemberRolesAssigned>();
        options.Events.AddEventType<ProjectMemberRemoved>();
        options.Projections.Add<ProjectAccessCurrentProjection>(ProjectionLifecycle.Inline);
    }
}
