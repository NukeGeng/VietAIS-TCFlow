using Marten.Events.Aggregation;
using VietAIS.TCFlow.Modules.TaskFlow.Contracts.Queries;
using VietAIS.TCFlow.Modules.TaskFlow.Domain;
using TaskStatus = VietAIS.TCFlow.Modules.TaskFlow.Contracts.Queries.TaskStatus;

namespace VietAIS.TCFlow.Modules.TaskFlow.Projections;

public sealed class TaskBoard
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public TaskStatus Status { get; set; }
    public string? AssigneeId { get; set; }
    public DateTimeOffset LastChangedAtUtc { get; set; }
}

public sealed class TaskBoardProjection : SingleStreamProjection<TaskBoard, Guid>
{
    public TaskBoardProjection() => Name = TaskProjectionNames.Board;
    public static TaskBoard Create(TaskProposed e) => new() { Id = e.TaskId, ProjectId = e.ProjectId, Title = e.Title, Status = TaskStatus.Suggested, LastChangedAtUtc = e.OccurredAtUtc };
    public static void Apply(TaskAccepted e, TaskBoard x) => Set(x, TaskStatus.Upcoming, e.OccurredAtUtc);
    public static void Apply(TaskRejected e, TaskBoard x) => Set(x, TaskStatus.Rejected, e.OccurredAtUtc);
    public static void Apply(TaskAssigned e, TaskBoard x) { x.AssigneeId = e.AssigneeId; Set(x, x.Status, e.OccurredAtUtc); }
    public static void Apply(TaskStarted e, TaskBoard x) => Set(x, TaskStatus.InProgress, e.OccurredAtUtc);
    public static void Apply(TaskBlocked e, TaskBoard x) => Set(x, TaskStatus.Blocked, e.OccurredAtUtc);
    public static void Apply(AiVerificationCompleted e, TaskBoard x) => Set(x, x.Status, e.OccurredAtUtc);
    public static void Apply(ReviewRequested e, TaskBoard x) => Set(x, x.Status, e.OccurredAtUtc);
    public static void Apply(ReviewApproved e, TaskBoard x) => Set(x, TaskStatus.ReadyForReview, e.OccurredAtUtc);
    public static void Apply(ReviewRejected e, TaskBoard x) => Set(x, TaskStatus.InProgress, e.OccurredAtUtc);
    public static void Apply(TaskCompleted e, TaskBoard x) => Set(x, TaskStatus.Completed, e.OccurredAtUtc);
    public static void Apply(TaskReopened e, TaskBoard x) => Set(x, TaskStatus.Upcoming, e.OccurredAtUtc);
    public static void Apply(TaskUpdatedFromSourceChange e, TaskBoard x) { x.Title = e.Title; Set(x, x.Status, e.OccurredAtUtc); }
    private static void Set(TaskBoard x, TaskStatus status, DateTimeOffset at) { x.Status = status; x.LastChangedAtUtc = at; }
}

public sealed class TaskAnalytics
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public TaskStatus Status { get; set; }
    public int TransitionCount { get; set; }
    public DateTimeOffset LastChangedAtUtc { get; set; }
}

public sealed class TaskAnalyticsProjection : SingleStreamProjection<TaskAnalytics, Guid>
{
    public TaskAnalyticsProjection() => Name = TaskProjectionNames.Analytics;
    public static TaskAnalytics Create(TaskProposed e) => new() { Id = e.TaskId, ProjectId = e.ProjectId, Status = TaskStatus.Suggested, TransitionCount = 1, LastChangedAtUtc = e.OccurredAtUtc };
    public static void Apply(TaskAccepted e, TaskAnalytics x) => Set(x, TaskStatus.Upcoming, e.OccurredAtUtc);
    public static void Apply(TaskRejected e, TaskAnalytics x) => Set(x, TaskStatus.Rejected, e.OccurredAtUtc);
    public static void Apply(TaskAssigned e, TaskAnalytics x) => Set(x, x.Status, e.OccurredAtUtc);
    public static void Apply(TaskStarted e, TaskAnalytics x) => Set(x, TaskStatus.InProgress, e.OccurredAtUtc);
    public static void Apply(TaskBlocked e, TaskAnalytics x) => Set(x, TaskStatus.Blocked, e.OccurredAtUtc);
    public static void Apply(AiVerificationCompleted e, TaskAnalytics x) => Set(x, x.Status, e.OccurredAtUtc);
    public static void Apply(ReviewRequested e, TaskAnalytics x) => Set(x, x.Status, e.OccurredAtUtc);
    public static void Apply(ReviewApproved e, TaskAnalytics x) => Set(x, TaskStatus.ReadyForReview, e.OccurredAtUtc);
    public static void Apply(ReviewRejected e, TaskAnalytics x) => Set(x, TaskStatus.InProgress, e.OccurredAtUtc);
    public static void Apply(TaskCompleted e, TaskAnalytics x) => Set(x, TaskStatus.Completed, e.OccurredAtUtc);
    public static void Apply(TaskReopened e, TaskAnalytics x) => Set(x, TaskStatus.Upcoming, e.OccurredAtUtc);
    public static void Apply(TaskUpdatedFromSourceChange e, TaskAnalytics x) => Set(x, x.Status, e.OccurredAtUtc);
    private static void Set(TaskAnalytics x, TaskStatus status, DateTimeOffset at) { x.Status = status; x.TransitionCount++; x.LastChangedAtUtc = at; }
}
