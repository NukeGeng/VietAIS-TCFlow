using VietAIS.TCFlow.Modules.AccessControl.Contracts.Models;

namespace VietAIS.TCFlow.Modules.AccessControl.Projections;

public sealed class ProjectAccessCurrent
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public List<ProjectRoleView> Roles { get; set; } = [];
    public List<ProjectMemberView> Members { get; set; } = [];
    public long Version { get; set; }
    public DateTimeOffset LastChangedAtUtc { get; set; }
}

public static class AccessControlProjectionNames
{
    public const string Current = "access-control-current";
}
