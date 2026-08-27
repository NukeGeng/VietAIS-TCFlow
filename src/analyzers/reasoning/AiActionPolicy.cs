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

    public static AiTaskAction RequiredAction(TaskReconciliationDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.Action == TaskReconciliationAction.Ignore)
        {
            return AiTaskAction.Ignore;
        }

        if (decision.Mutations.Count > 0 &&
            decision.Mutations.Any(mutation => mutation.After.Status == SourceAwareTaskStatus.Upcoming) &&
            decision.Mutations.All(IsSuggestionPromotionMutation))
        {
            return AiTaskAction.Create;
        }

        if (decision.Mutations.Count > 0 && decision.Mutations.All(IsSuggestionLifecycleMutation))
        {
            return AiTaskAction.Suggest;
        }

        return decision.Action switch
        {
            TaskReconciliationAction.Create => AiTaskAction.Create,
            TaskReconciliationAction.Update => AiTaskAction.Update,
            TaskReconciliationAction.Merge => AiTaskAction.Merge,
            TaskReconciliationAction.Close => AiTaskAction.Close,
            TaskReconciliationAction.Reopen => AiTaskAction.Reopen,
            _ => throw new ArgumentOutOfRangeException(
                nameof(decision),
                decision.Action,
                "Unknown reconciliation action.")
        };
    }

    private static bool IsSuggestionLifecycleMutation(TaskMutation mutation) =>
        (mutation.Before is null || mutation.Before.Status is
            SourceAwareTaskStatus.Suggested or SourceAwareTaskStatus.Cancelled) &&
        mutation.After.Status is SourceAwareTaskStatus.Suggested or SourceAwareTaskStatus.Cancelled;

    private static bool IsSuggestionPromotionMutation(TaskMutation mutation) =>
        (mutation.Before is null || mutation.Before.Status is
            SourceAwareTaskStatus.Suggested or SourceAwareTaskStatus.Cancelled) &&
        mutation.After.Status is SourceAwareTaskStatus.Upcoming or SourceAwareTaskStatus.Cancelled;

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
