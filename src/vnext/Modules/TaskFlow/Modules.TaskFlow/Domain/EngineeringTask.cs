using VietAIS.TCFlow.Modules.TaskFlow.Contracts.Queries;
using TaskStatus = VietAIS.TCFlow.Modules.TaskFlow.Contracts.Queries.TaskStatus;

namespace VietAIS.TCFlow.Modules.TaskFlow.Domain;

public sealed class EngineeringTask
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TaskStatus Status { get; private set; }
    public string? AssigneeId { get; private set; }
    public string? SourceChangeKey { get; private set; }
    public bool AiVerificationPassed { get; private set; }
    public bool HumanReviewRequested { get; private set; }
    public bool HumanReviewApproved { get; private set; }

    public void Apply(TaskProposed @event)
    {
        Id = @event.TaskId;
        ProjectId = @event.ProjectId;
        Title = @event.Title;
        Description = @event.Description;
        SourceChangeKey = @event.SourceChangeKey;
        Status = TaskStatus.Suggested;
    }

    public void Apply(TaskAccepted @event) => Status = TaskStatus.Upcoming;
    public void Apply(TaskRejected @event) => Status = TaskStatus.Rejected;
    public void Apply(TaskAssigned @event) => AssigneeId = @event.AssigneeId;
    public void Apply(TaskStarted @event) => Status = TaskStatus.InProgress;
    public void Apply(TaskBlocked @event) => Status = TaskStatus.Blocked;
    public void Apply(AiVerificationCompleted @event) => AiVerificationPassed = @event.Passed;
    public void Apply(ReviewRequested @event) => HumanReviewRequested = true;
    public void Apply(ReviewApproved @event) { HumanReviewApproved = true; Status = TaskStatus.ReadyForReview; }
    public void Apply(ReviewRejected @event) { HumanReviewApproved = false; Status = TaskStatus.InProgress; }
    public void Apply(TaskCompleted @event) => Status = TaskStatus.Completed;
    public void Apply(TaskReopened @event) => Status = TaskStatus.Upcoming;
    public void Apply(TaskUpdatedFromSourceChange @event)
    {
        Title = @event.Title;
        Description = @event.Description;
        SourceChangeKey = @event.SourceChangeKey;
    }

    public void Apply(TaskLifecycleReconciled @event)
    {
        Status = @event.Status;
        AssigneeId = @event.AssigneeId;
        AiVerificationPassed = @event.AiVerificationPassed;
        HumanReviewRequested = @event.HumanReviewRequested;
        HumanReviewApproved = @event.HumanReviewApproved;
    }

    public TaskAccepted Accept(string actorId, string correlationId, DateTimeOffset now)
    {
        EnsureIdentity(actorId, correlationId);
        EnsureStatus(TaskStatus.Suggested, TaskStatus.Blocked);
        return new TaskAccepted(Id, actorId.Trim(), correlationId.Trim(), now);
    }

    public TaskRejected Reject(string reason, string actorId, string correlationId, DateTimeOffset now)
    {
        EnsureIdentity(actorId, correlationId);
        EnsureText(reason, 2, 1000, nameof(reason));
        EnsureStatus(TaskStatus.Suggested, TaskStatus.Upcoming);
        return new TaskRejected(Id, reason.Trim(), actorId.Trim(), correlationId.Trim(), now);
    }

    public TaskAssigned Assign(string assigneeId, string actorId, string correlationId, DateTimeOffset now)
    {
        EnsureIdentity(actorId, correlationId);
        EnsureText(assigneeId, 2, 200, nameof(assigneeId));
        EnsureStatus(TaskStatus.Upcoming);
        return new TaskAssigned(Id, assigneeId.Trim(), actorId.Trim(), correlationId.Trim(), now);
    }

    public TaskStarted Start(string actorId, string correlationId, DateTimeOffset now)
    {
        EnsureIdentity(actorId, correlationId);
        EnsureStatus(TaskStatus.Upcoming);
        if (string.IsNullOrWhiteSpace(AssigneeId)) throw new InvalidOperationException("A task must be assigned before it starts.");
        return new TaskStarted(Id, actorId.Trim(), correlationId.Trim(), now);
    }

    public TaskBlocked Block(string reason, string actorId, string correlationId, DateTimeOffset now)
    {
        EnsureIdentity(actorId, correlationId);
        EnsureText(reason, 2, 1000, nameof(reason));
        EnsureStatus(TaskStatus.InProgress, TaskStatus.Upcoming);
        return new TaskBlocked(Id, reason.Trim(), actorId.Trim(), correlationId.Trim(), now);
    }

    public AiVerificationCompleted CompleteAiVerification(bool passed, string summary, string actorId, string correlationId, DateTimeOffset now)
    {
        EnsureIdentity(actorId, correlationId);
        EnsureText(summary, 2, 2000, nameof(summary));
        EnsureStatus(TaskStatus.Suggested, TaskStatus.Upcoming, TaskStatus.InProgress);
        if (AiVerificationPassed) throw new InvalidOperationException("AI verification is already complete.");
        return new AiVerificationCompleted(Id, passed, summary.Trim(), actorId.Trim(), correlationId.Trim(), now);
    }

    public ReviewRequested RequestReview(string actorId, string correlationId, DateTimeOffset now)
    {
        EnsureIdentity(actorId, correlationId);
        EnsureStatus(TaskStatus.InProgress);
        if (!AiVerificationPassed) throw new InvalidOperationException("AI verification must pass before human review.");
        return new ReviewRequested(Id, actorId.Trim(), correlationId.Trim(), now);
    }

    public ReviewApproved ApproveReview(string actorId, string correlationId, DateTimeOffset now)
    {
        EnsureIdentity(actorId, correlationId);
        EnsureStatus(TaskStatus.InProgress);
        if (!HumanReviewRequested) throw new InvalidOperationException("A review must be requested before approval.");
        return new ReviewApproved(Id, actorId.Trim(), correlationId.Trim(), now);
    }

    public ReviewRejected RejectReview(string reason, string actorId, string correlationId, DateTimeOffset now)
    {
        EnsureIdentity(actorId, correlationId);
        EnsureText(reason, 2, 1000, nameof(reason));
        EnsureStatus(TaskStatus.InProgress);
        if (!HumanReviewRequested) throw new InvalidOperationException("A review must be requested before rejection.");
        return new ReviewRejected(Id, reason.Trim(), actorId.Trim(), correlationId.Trim(), now);
    }

    public TaskCompleted Complete(string actorId, string correlationId, DateTimeOffset now)
    {
        EnsureIdentity(actorId, correlationId);
        EnsureStatus(TaskStatus.ReadyForReview);
        if (!AiVerificationPassed || !HumanReviewApproved) throw new InvalidOperationException("AI verification and human approval are required.");
        return new TaskCompleted(Id, actorId.Trim(), correlationId.Trim(), now);
    }

    public TaskReopened Reopen(string reason, string actorId, string correlationId, DateTimeOffset now)
    {
        EnsureIdentity(actorId, correlationId);
        EnsureText(reason, 2, 1000, nameof(reason));
        EnsureStatus(TaskStatus.Completed);
        return new TaskReopened(Id, reason.Trim(), actorId.Trim(), correlationId.Trim(), now);
    }

    public TaskUpdatedFromSourceChange UpdateFromSourceChange(string title, string? description, string sourceChangeKey, string actorId, string correlationId, DateTimeOffset now)
    {
        EnsureIdentity(actorId, correlationId);
        EnsureText(title, 2, 240, nameof(title));
        EnsureText(sourceChangeKey, 2, 300, nameof(sourceChangeKey));
        if (!string.Equals(SourceChangeKey, sourceChangeKey.Trim(), StringComparison.Ordinal)) throw new InvalidOperationException("The source change key does not belong to this task.");
        return new TaskUpdatedFromSourceChange(Id, title.Trim(), NormalizeOptional(description, 2000), sourceChangeKey.Trim(), actorId.Trim(), correlationId.Trim(), now);
    }

    private void EnsureStatus(params TaskStatus[] allowed)
    {
        if (!allowed.Contains(Status)) throw new InvalidOperationException($"Task cannot transition from status '{Status}'.");
    }

    private static void EnsureIdentity(string actorId, string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
    }

    private static void EnsureText(string value, int min, int max, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length is < 2 || normalized.Length > max) throw new ArgumentException($"Value must contain between {min} and {max} characters.", name);
    }

    private static string? NormalizeOptional(string? value, int max)
    {
        if (value is null) return null;
        var normalized = value.Trim();
        if (normalized.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters.", nameof(value));
        return normalized.Length == 0 ? null : normalized;
    }
}
