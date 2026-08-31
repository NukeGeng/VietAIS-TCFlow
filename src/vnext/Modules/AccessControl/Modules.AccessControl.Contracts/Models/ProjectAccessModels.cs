namespace VietAIS.TCFlow.Modules.AccessControl.Contracts.Models;

public enum ProjectResourceScope
{
    Project,
    Repository,
    Component,
    Own,
    Assigned,
    All
}

public enum ProjectComponentScope
{
    Frontend,
    Backend,
    Database,
    Tests,
    Documentation,
    Infrastructure,
    SharedLibrary,
    Service
}

public sealed record ProjectPermissionGrant(
    string PermissionCode,
    ProjectResourceScope ResourceScope,
    string? ResourceId = null,
    IReadOnlyList<ProjectComponentScope>? Components = null);

public sealed record ProjectRoleView(
    Guid RoleId,
    string Name,
    bool IsSystemDefined,
    IReadOnlyList<ProjectPermissionGrant> Grants);

public sealed record ProjectMemberView(
    string UserId,
    bool IsActive,
    IReadOnlyList<Guid> RoleIds);

public sealed record EffectiveProjectPermissions(
    Guid ProjectId,
    string UserId,
    IReadOnlyList<ProjectPermissionGrant> Grants,
    IReadOnlyList<Guid> RoleIds)
{
    public bool Has(string permissionCode) =>
        Grants.Any(grant => string.Equals(grant.PermissionCode, permissionCode, StringComparison.Ordinal));
}

public static class ProjectPermissionCatalog
{
    public const string ProjectView = "project.view";
    public const string ProjectUpdate = "project.update";
    public const string MemberView = "member.view";
    public const string MemberManage = "member.manage";
    public const string RoleManage = "role.manage";
    public const string RepositoryView = "repository.view";
    public const string TaskView = "task.view";
    public const string TaskUpdate = "task.update";
    public const string AiAnalysisRun = "ai.analysis.run";
    public const string AiTaskSuggest = "ai.task.suggest";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ProjectView,
        ProjectUpdate,
        MemberView,
        MemberManage,
        RoleManage,
        RepositoryView,
        TaskView,
        TaskUpdate,
        AiAnalysisRun,
        AiTaskSuggest
    };

    public static IReadOnlyList<ProjectPermissionGrant> OwnerGrants { get; } =
        All.Order(StringComparer.Ordinal)
            .Select(permission => new ProjectPermissionGrant(
                permission,
                ProjectResourceScope.Project))
            .ToArray();
}
