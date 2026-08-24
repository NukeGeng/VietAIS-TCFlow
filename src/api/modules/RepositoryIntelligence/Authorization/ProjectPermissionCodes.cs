using VietAIS.TCFlow.Shared.Authorization;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

public static class ProjectPermissionCodes
{
    public const string ProjectView = "project.view";
    public const string ProjectUpdate = "project.update";
    public const string ProjectDelete = "project.delete";
    public const string ProjectOwnershipTransfer = "project.ownership.transfer";
    public const string MemberView = "member.view";
    public const string MemberInvite = "member.invite";
    public const string MemberRemove = "member.remove";
    public const string MemberRoleAssign = "member.role.assign";
    public const string RoleView = "role.view";
    public const string RoleCreate = "role.create";
    public const string RoleUpdate = "role.update";
    public const string RoleDelete = "role.delete";
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
}

public static class SystemPermissionCodes
{
    public const string UserManage = TcFlowSystemPermissions.UserManage;
    public const string ProjectInspect = TcFlowSystemPermissions.ProjectInspect;
    public const string ProjectSuspend = TcFlowSystemPermissions.ProjectSuspend;
    public const string PermissionDefinitionManage = TcFlowSystemPermissions.PermissionDefinitionManage;
    public const string SystemAuditView = TcFlowSystemPermissions.SystemAuditView;
    public const string AiProviderManage = TcFlowSystemPermissions.AiProviderManage;
    public const string SystemSettingsManage = TcFlowSystemPermissions.SystemSettingsManage;
    public const string PlatformPolicyManage = TcFlowSystemPermissions.PlatformPolicyManage;
    public const string PlatformUsageView = TcFlowSystemPermissions.PlatformUsageView;
}
