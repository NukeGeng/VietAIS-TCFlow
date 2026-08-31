using Marten.Events.Aggregation;
using VietAIS.TCFlow.Modules.TaskFlow.Contracts.Queries;
using VietAIS.TCFlow.Modules.TaskFlow.Domain;
using TaskStatus = VietAIS.TCFlow.Modules.TaskFlow.Contracts.Queries.TaskStatus;

namespace VietAIS.TCFlow.Modules.TaskFlow.Projections;

public static class TaskProjectionNames
{
    public const string Current = "task-current";
    public const string Board = "task-board";
    public const string Analytics = "task-analytics";
}

public sealed class TaskCurrent
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskStatus Status { get; set; }
    public string? AssigneeId { get; set; }
    public bool AiVerificationPassed { get; set; }
    public bool HumanReviewRequested { get; set; }
    public string? SourceChangeKey { get; set; }
    public long Version { get; set; }
    public DateTimeOffset LastChangedAtUtc { get; set; }
}

public sealed class TaskCurrentProjection : SingleStreamProjection<TaskCurrent, Guid>
{
    public TaskCurrentProjection() => Name = TaskProjectionNames.Current;

    public static TaskCurrent Create(TaskProposed e) => new()
    {
        Id = e.TaskId, ProjectId = e.ProjectId, Title = e.Title, Description = e.Description,
        SourceChangeKey = e.SourceChangeKey, Status = TaskStatus.Suggested, Version = 1, LastChangedAtUtc = e.OccurredAtUtc
    };

    public static void Apply(TaskAccepted e, TaskCurrent x) => Set(x, TaskStatus.Upcoming, e.OccurredAtUtc);
    public static void Apply(TaskRejected e, TaskCurrent x) => Set(x, TaskStatus.Rejected, e.OccurredAtUtc);
    public static void Apply(TaskAssigned e, TaskCurrent x) { x.AssigneeId = e.AssigneeId; Set(x, x.Status, e.OccurredAtUtc); }
    public static void Apply(TaskStarted e, TaskCurrent x) => Set(x, TaskStatus.InProgress, e.OccurredAtUtc);
    public static void Apply(TaskBlocked e, TaskCurrent x) => Set(x, TaskStatus.Blocked, e.OccurredAtUtc);
    public static void Apply(AiVerificationCompleted e, TaskCurrent x) { x.AiVerificationPassed = e.Passed; Set(x, x.Status, e.OccurredAtUtc); }
    public static void Apply(ReviewRequested e, TaskCurrent x) { x.HumanReviewRequested = true; Set(x, x.Status, e.OccurredAtUtc); }
    public static void Apply(ReviewApproved e, TaskCurrent x) => Set(x, TaskStatus.ReadyForReview, e.OccurredAtUtc);
    public static void Apply(ReviewRejected e, TaskCurrent x) => Set(x, TaskStatus.InProgress, e.OccurredAtUtc);
    public static void Apply(TaskCompleted e, TaskCurrent x) => Set(x, TaskStatus.Completed, e.OccurredAtUtc);
    public static void Apply(TaskReopened e, TaskCurrent x) => Set(x, TaskStatus.Upcoming, e.OccurredAtUtc);
    public static void Apply(TaskUpdatedFromSourceChange e, TaskCurrent x) { x.Title = e.Title; x.Description = e.Description; Set(x, x.Status, e.OccurredAtUtc); }
    public static void Apply(TaskLifecycleReconciled e, TaskCurrent x)
    {
        x.AssigneeId = e.AssigneeId;
        x.AiVerificationPassed = e.AiVerificationPassed;
        x.HumanReviewRequested = e.HumanReviewRequested;
        Set(x, e.Status, e.OccurredAtUtc);
    }

    private static void Set(TaskCurrent x, TaskStatus status, DateTimeOffset at)
    {
        x.Status = status;
        x.Version++;
        x.LastChangedAtUtc = at;
    }
}
