namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

public static class PermissionCatalog
{
    private static readonly ResourceScopeKind[] ProjectScopes =
    [
        ResourceScopeKind.Workspace,
        ResourceScopeKind.Project,
        ResourceScopeKind.Repository,
        ResourceScopeKind.Component,
        ResourceScopeKind.Own,
        ResourceScopeKind.Assigned,
        ResourceScopeKind.All
    ];

    private static readonly ComponentScopeKind[] Components = Enum.GetValues<ComponentScopeKind>();

    private static readonly PermissionDefinition[] Definitions =
    [
        Project(ProjectPermissionCodes.ProjectView),
        Project(ProjectPermissionCodes.ProjectUpdate),
        Project(ProjectPermissionCodes.ProjectDelete),
        Project(ProjectPermissionCodes.ProjectOwnershipTransfer),
        Project(ProjectPermissionCodes.MemberView),
        Project(ProjectPermissionCodes.MemberInvite),
        Project(ProjectPermissionCodes.MemberRemove),
        Project(ProjectPermissionCodes.MemberRoleAssign),
        Project(ProjectPermissionCodes.RoleView),
        Project(ProjectPermissionCodes.RoleCreate),
        Project(ProjectPermissionCodes.RoleUpdate),
        Project(ProjectPermissionCodes.RoleDelete),
        Project(ProjectPermissionCodes.RepositoryView),
        Project(ProjectPermissionCodes.RepositoryCreate),
        Project(ProjectPermissionCodes.RepositoryUpdate),
        Project(ProjectPermissionCodes.RepositoryDelete),
        Project(ProjectPermissionCodes.RepositoryAccessManage),
        Project(ProjectPermissionCodes.ComponentView),
        Project(ProjectPermissionCodes.ComponentCreate),
        Project(ProjectPermissionCodes.ComponentUpdate),
        Project(ProjectPermissionCodes.ComponentDelete),
        Project(ProjectPermissionCodes.FeatureView),
        Project(ProjectPermissionCodes.FeatureCreate),
        Project(ProjectPermissionCodes.FeatureUpdate),
        Project(ProjectPermissionCodes.FeatureDelete),
        Project(ProjectPermissionCodes.SourceView),
        Project(ProjectPermissionCodes.SourceAnalyze),
        Project(ProjectPermissionCodes.AnalysisView),
        Project(ProjectPermissionCodes.AnalysisRun),
        Project(ProjectPermissionCodes.TaskView),
        Project(ProjectPermissionCodes.TaskCreate),
        Project(ProjectPermissionCodes.TaskUpdate),
        Project(ProjectPermissionCodes.TaskStatusUpdate),
        Project(ProjectPermissionCodes.TaskDelete),
        Project(ProjectPermissionCodes.TaskAssign),
        Project(ProjectPermissionCodes.TaskApprove),
        Project(ProjectPermissionCodes.TaskReject),
        Project(ProjectPermissionCodes.TaskComment),
        Project(ProjectPermissionCodes.TaskReview),
        Project(ProjectPermissionCodes.ConventionView),
        Project(ProjectPermissionCodes.ConventionUpdate),
        Project(ProjectPermissionCodes.AuthorityView),
        Project(ProjectPermissionCodes.AuthorityUpdate),
        Project(ProjectPermissionCodes.AiPolicyUpdate),
        Project(ProjectPermissionCodes.AiAnalysisRun),
        Project(ProjectPermissionCodes.AiTaskSuggest),
        Project(ProjectPermissionCodes.AiTaskCreate),
        Project(ProjectPermissionCodes.AiTaskUpdate),
        Project(ProjectPermissionCodes.AiTaskClose),
        Project(ProjectPermissionCodes.AiCodeGenerate),
        Project(ProjectPermissionCodes.AiPullRequestCreate),
        Project(ProjectPermissionCodes.AuditView),
        System(SystemPermissionCodes.UserManage),
        System(SystemPermissionCodes.ProjectInspect),
        System(SystemPermissionCodes.ProjectSuspend),
        System(SystemPermissionCodes.PermissionDefinitionManage),
        System(SystemPermissionCodes.SystemAuditView),
        System(SystemPermissionCodes.AiProviderManage),
        System(SystemPermissionCodes.SystemSettingsManage),
        System(SystemPermissionCodes.PlatformPolicyManage),
        System(SystemPermissionCodes.PlatformUsageView)
    ];

    public static IReadOnlyList<PermissionDefinition> All { get; } = Array.AsReadOnly(Definitions);

    public static IReadOnlyList<PermissionDefinition> ProjectDefinitions { get; } =
        Array.AsReadOnly(Definitions.Where(definition => definition.Scope == PermissionDefinitionScope.Project).ToArray());

    public static bool TryGetProjectDefinition(string code, out PermissionDefinition definition)
    {
        definition = Definitions.FirstOrDefault(item =>
            item.Scope == PermissionDefinitionScope.Project &&
            string.Equals(item.Id, code, StringComparison.Ordinal))!;
        return definition is not null;
    }

    private static PermissionDefinition Project(string code) =>
        new(code, code, PermissionDefinitionScope.Project, ProjectScopes, Components);

    private static PermissionDefinition System(string code) =>
        new(code, code, PermissionDefinitionScope.System, [ResourceScopeKind.All], []);
}

public static class AiTrustPolicy
{
    public static bool IsAllowed(AiTrustLevel trustLevel, string permissionCode) =>
        GetAllowedPermissions(trustLevel).Contains(permissionCode, StringComparer.Ordinal);

    public static IReadOnlyList<string> GetAllowedPermissions(AiTrustLevel trustLevel) => trustLevel switch
    {
        AiTrustLevel.SuggestOnly =>
        [
            ProjectPermissionCodes.AiAnalysisRun,
            ProjectPermissionCodes.AiTaskSuggest
        ],
        AiTrustLevel.CreateTasks =>
        [
            ProjectPermissionCodes.AiAnalysisRun,
            ProjectPermissionCodes.AiTaskSuggest,
            ProjectPermissionCodes.AiTaskCreate
        ],
        AiTrustLevel.UpdateTasks =>
        [
            ProjectPermissionCodes.AiAnalysisRun,
            ProjectPermissionCodes.AiTaskSuggest,
            ProjectPermissionCodes.AiTaskCreate,
            ProjectPermissionCodes.AiTaskUpdate,
            ProjectPermissionCodes.AiTaskClose
        ],
        AiTrustLevel.CodeGeneration =>
        [
            ProjectPermissionCodes.AiAnalysisRun,
            ProjectPermissionCodes.AiTaskSuggest,
            ProjectPermissionCodes.AiTaskCreate,
            ProjectPermissionCodes.AiTaskUpdate,
            ProjectPermissionCodes.AiTaskClose,
            ProjectPermissionCodes.AiCodeGenerate
        ],
        AiTrustLevel.PullRequestCreation =>
        [
            ProjectPermissionCodes.AiAnalysisRun,
            ProjectPermissionCodes.AiTaskSuggest,
            ProjectPermissionCodes.AiTaskCreate,
            ProjectPermissionCodes.AiTaskUpdate,
            ProjectPermissionCodes.AiTaskClose,
            ProjectPermissionCodes.AiCodeGenerate,
            ProjectPermissionCodes.AiPullRequestCreate
        ],
        _ => []
    };
}
