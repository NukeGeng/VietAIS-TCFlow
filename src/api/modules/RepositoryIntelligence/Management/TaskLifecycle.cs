namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public static class TaskLifecycle
{
    private static readonly IReadOnlyDictionary<TaskLifecycleStatus, TaskLifecycleStatus[]> AllowedTransitions =
        new Dictionary<TaskLifecycleStatus, TaskLifecycleStatus[]>
        {
            [TaskLifecycleStatus.Suggested] =
            [
                TaskLifecycleStatus.Upcoming,
                TaskLifecycleStatus.Rejected,
                TaskLifecycleStatus.Cancelled
            ],
            [TaskLifecycleStatus.Upcoming] =
            [
                TaskLifecycleStatus.InProgress,
                TaskLifecycleStatus.Cancelled
            ],
            [TaskLifecycleStatus.InProgress] =
            [
                TaskLifecycleStatus.ReadyForReview,
                TaskLifecycleStatus.Blocked,
                TaskLifecycleStatus.Cancelled
            ],
            [TaskLifecycleStatus.ReadyForReview] =
            [
                TaskLifecycleStatus.Completed,
                TaskLifecycleStatus.Rejected,
                TaskLifecycleStatus.InProgress
            ],
            [TaskLifecycleStatus.Blocked] =
            [
                TaskLifecycleStatus.InProgress,
                TaskLifecycleStatus.Cancelled
            ],
            [TaskLifecycleStatus.Rejected] =
            [
                TaskLifecycleStatus.InProgress,
                TaskLifecycleStatus.Cancelled
            ],
            [TaskLifecycleStatus.Completed] = [],
            [TaskLifecycleStatus.Cancelled] = []
        };

    public static bool CanTransition(TaskLifecycleStatus from, TaskLifecycleStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
}
