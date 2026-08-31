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
    public const string ProjectDelete = "project.delete";
    public const string ProjectOwnershipTransfer = "project.ownership.transfer";
    public const string MemberView = "member.view";
    public const string MemberInvite = "member.invite";
    public const string MemberRemove = "member.remove";
    public const string MemberRoleAssign = "member.role.assign";
    public const string MemberManage = "member.manage";
    public const string RoleView = "role.view";
    public const string RoleCreate = "role.create";
    public const string RoleUpdate = "role.update";
    public const string RoleDelete = "role.delete";
    public const string RoleManage = "role.manage";
    public const string RepositoryView = "repository.view";
    public const string RepositoryCreate = "repository.create";
    public const string RepositoryUpdate = "repository.update";
    public const string RepositoryDelete = "repository.delete";
    public const string RepositoryAccessManage = "repository.access.manage";
    public const string ComponentView = "component.view";
    public const string ComponentCreate = "component.create";
    public const string ComponentUpdate = "component.update";
    public const string ComponentDelete = "component.delete";
    public const string FeatureView = "feature.view";
    public const string FeatureCreate = "feature.create";
    public const string FeatureUpdate = "feature.update";
    public const string FeatureDelete = "feature.delete";
    public const string SourceView = "source.view";
    public const string SourceAnalyze = "source.analyze";
    public const string AnalysisView = "analysis.view";
    public const string AnalysisRun = "analysis.run";
    public const string TaskView = "task.view";
    public const string TaskCreate = "task.create";
    public const string TaskUpdate = "task.update";
    public const string TaskStatusUpdate = "task.status.update";
    public const string TaskDelete = "task.delete";
    public const string TaskAssign = "task.assign";
    public const string TaskApprove = "task.approve";
    public const string TaskReject = "task.reject";
    public const string TaskComment = "task.comment";
    public const string TaskReview = "task.review";
    public const string ConventionView = "convention.view";
    public const string ConventionUpdate = "convention.update";
    public const string AuthorityView = "authority.view";
    public const string AuthorityUpdate = "authority.update";
    public const string AiPolicyUpdate = "ai.policy.update";
    public const string AiAnalysisRun = "ai.analysis.run";
    public const string AiTaskSuggest = "ai.task.suggest";
    public const string AiTaskCreate = "ai.task.create";
    public const string AiTaskUpdate = "ai.task.update";
    public const string AiTaskClose = "ai.task.close";
    public const string AiCodeGenerate = "ai.code.generate";
    public const string AiPullRequestCreate = "ai.pull_request.create";
    public const string AuditView = "audit.view";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ProjectView,
        ProjectUpdate,
        ProjectDelete,
        ProjectOwnershipTransfer,
        MemberView,
        MemberInvite,
        MemberRemove,
        MemberRoleAssign,
        MemberManage,
        RoleView,
        RoleCreate,
        RoleUpdate,
        RoleDelete,
        RoleManage,
        RepositoryView,
        RepositoryCreate,
        RepositoryUpdate,
        RepositoryDelete,
        RepositoryAccessManage,
        ComponentView,
        ComponentCreate,
        ComponentUpdate,
        ComponentDelete,
        FeatureView,
        FeatureCreate,
        FeatureUpdate,
        FeatureDelete,
        SourceView,
        SourceAnalyze,
        AnalysisView,
        AnalysisRun,
        TaskView,
        TaskCreate,
        TaskUpdate,
        TaskStatusUpdate,
        TaskDelete,
        TaskAssign,
        TaskApprove,
        TaskReject,
        TaskComment,
        TaskReview,
        ConventionView,
        ConventionUpdate,
        AuthorityView,
        AuthorityUpdate,
        AiPolicyUpdate,
        AiAnalysisRun,
        AiTaskSuggest,
        AiTaskCreate,
        AiTaskUpdate,
        AiTaskClose,
        AiCodeGenerate,
        AiPullRequestCreate,
        AuditView
    };

    public static IReadOnlyList<ProjectPermissionGrant> OwnerGrants { get; } =
        All.Order(StringComparer.Ordinal)
            .Select(permission => new ProjectPermissionGrant(
                permission,
                ProjectResourceScope.Project))
            .ToArray();
}
