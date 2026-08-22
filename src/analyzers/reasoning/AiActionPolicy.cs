namespace VietAIS.TCFlow.Analyzers.Reasoning;

public static class AiPermissionCodes
{
    public const string AnalysisRun = "ai.analysis.run";
    public const string TaskSuggest = "ai.task.suggest";
    public const string TaskCreate = "ai.task.create";
    public const string TaskUpdate = "ai.task.update";
    public const string TaskClose = "ai.task.close";
    public const string CodeGenerate = "ai.code.generate";
    public const string PullRequestCreate = "ai.pull_request.create";
}

public sealed class AiPolicyViolationException(string message) : InvalidOperationException(message);

public static class AiActionAuthorizer
{
    public static void EnsureAllowed(AiActionPolicy policy, AiTaskAction action)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var permission = RequiredPermission(action);
        if (!policy.AllowedPermissions.Contains(permission, StringComparer.Ordinal) ||
            !TrustPermissions(policy.TrustLevel).Contains(permission, StringComparer.Ordinal))
        {
            throw new AiPolicyViolationException(
                $"AI action '{action}' requires permission '{permission}' within trust level '{policy.TrustLevel}'.");
        }
    }

    public static string RequiredPermission(AiTaskAction action) => action switch
    {
        AiTaskAction.Analyze => AiPermissionCodes.AnalysisRun,
        AiTaskAction.Suggest or AiTaskAction.Ignore => AiPermissionCodes.TaskSuggest,
        AiTaskAction.Create => AiPermissionCodes.TaskCreate,
        AiTaskAction.Update or AiTaskAction.Merge or AiTaskAction.Reopen => AiPermissionCodes.TaskUpdate,
        AiTaskAction.Close => AiPermissionCodes.TaskClose,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown AI action.")
    };

    public static IReadOnlyList<string> TrustPermissions(AiTrustLevel trustLevel) => trustLevel switch
    {
        AiTrustLevel.SuggestOnly =>
        [
            AiPermissionCodes.AnalysisRun,
            AiPermissionCodes.TaskSuggest
        ],
        AiTrustLevel.CreateTasks =>
        [
            AiPermissionCodes.AnalysisRun,
            AiPermissionCodes.TaskSuggest,
            AiPermissionCodes.TaskCreate
        ],
        AiTrustLevel.UpdateTasks =>
        [
            AiPermissionCodes.AnalysisRun,
            AiPermissionCodes.TaskSuggest,
            AiPermissionCodes.TaskCreate,
            AiPermissionCodes.TaskUpdate,
            AiPermissionCodes.TaskClose
        ],
        AiTrustLevel.CodeGeneration =>
        [
            AiPermissionCodes.AnalysisRun,
            AiPermissionCodes.TaskSuggest,
            AiPermissionCodes.TaskCreate,
            AiPermissionCodes.TaskUpdate,
            AiPermissionCodes.TaskClose,
            AiPermissionCodes.CodeGenerate
        ],
        AiTrustLevel.PullRequestCreation =>
        [
            AiPermissionCodes.AnalysisRun,
            AiPermissionCodes.TaskSuggest,
            AiPermissionCodes.TaskCreate,
            AiPermissionCodes.TaskUpdate,
            AiPermissionCodes.TaskClose,
            AiPermissionCodes.CodeGenerate,
            AiPermissionCodes.PullRequestCreate
        ],
        _ => []
    };
}
