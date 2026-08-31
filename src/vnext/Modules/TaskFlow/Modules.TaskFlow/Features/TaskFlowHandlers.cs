using Marten;
using VietAIS.TCFlow.BuildingBlocks.Application.Identity;
using VietAIS.TCFlow.BuildingBlocks.Application.Time;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Metadata;
using VietAIS.TCFlow.Modules.TaskFlow.Contracts.Commands;
using VietAIS.TCFlow.Modules.TaskFlow.Domain;
using VietAIS.TCFlow.Modules.TaskFlow.Projections;
using Wolverine.Attributes;

namespace VietAIS.TCFlow.Modules.TaskFlow.Features;

public sealed record TaskCommandResult(Guid TaskId, long ExpectedVersion, bool Reconciled = false);

[WolverineHandler]
public static class TaskFlowHandlers
{
    public static TaskCommandResult Handle(CreateTask command, IDocumentSession session, IClock clock, IIdGenerator idGenerator)
    {
        Validate(command.ProjectId, command.ActorId, command.CorrelationId);
        var taskId = command.TaskId is { } requested && requested != Guid.Empty ? requested : idGenerator.NewId();
        session.ApplyEventMetadata(new EventMetadata(command.ActorId, command.CorrelationId, command.CausationId, command.ProjectId, null, "taskflow.task.propose"));
        session.Events.StartStream<EngineeringTask>(taskId, new TaskProposed(taskId, command.ProjectId, NormalizeTitle(command.Title), NormalizeOptional(command.Description), NormalizeOptional(command.SourceChangeKey, 300), command.ActorId.Trim(), command.CorrelationId.Trim(), clock.UtcNow));
        return new(taskId, 1);
    }

    public static async Task<TaskCommandResult> Handle(ApplySourceChangeProposal command, IDocumentSession session, IQuerySession query, IClock clock, IIdGenerator idGenerator, CancellationToken cancellationToken)
    {
        Validate(command.ProjectId, command.ActorId, command.CorrelationId);
        var key = NormalizeRequired(command.SourceChangeKey, 300, nameof(command.SourceChangeKey));
        var existing = await query.Query<TaskCurrent>().FirstOrDefaultAsync(x => x.ProjectId == command.ProjectId && x.SourceChangeKey == key, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return Handle(new CreateTask(command.ProjectId, command.Title, command.Description, command.ActorId, command.CorrelationId, key), session, clock, idGenerator);
        }

        var stream = await session.Events.FetchForWriting<EngineeringTask>(existing.Id, existing.Version, cancellationToken).ConfigureAwait(false);
        if (stream.Aggregate is null) throw new KeyNotFoundException($"Task '{existing.Id}' was not found.");
        var title = NormalizeTitle(command.Title);
        var description = NormalizeOptional(command.Description);
        if (string.Equals(stream.Aggregate.Title, title, StringComparison.Ordinal) && string.Equals(stream.Aggregate.Description, description, StringComparison.Ordinal)) return new(existing.Id, existing.Version, true);
        session.ApplyEventMetadata(new EventMetadata(command.ActorId, command.CorrelationId, command.CausationId, command.ProjectId, null, "taskflow.task.reconcile"));
        stream.AppendOne(stream.Aggregate.UpdateFromSourceChange(title, description, key, command.ActorId, command.CorrelationId, clock.UtcNow));
        return new(existing.Id, existing.Version + 1, true);
    }

    public static Task<TaskCommandResult> Handle(AcceptTask command, IDocumentSession s, IClock c, CancellationToken ct) => Transition(command.TaskId, command.ExpectedVersion, command.ActorId, command.CorrelationId, command.CausationId, s, c, ct, (a, x) => a.Accept(x.actor, x.correlation, x.now));
    public static Task<TaskCommandResult> Handle(RejectTask command, IDocumentSession s, IClock c, CancellationToken ct) => Transition(command.TaskId, command.ExpectedVersion, command.ActorId, command.CorrelationId, command.CausationId, s, c, ct, (a, x) => a.Reject(command.Reason, x.actor, x.correlation, x.now));
    public static Task<TaskCommandResult> Handle(AssignTask command, IDocumentSession s, IClock c, CancellationToken ct) => Transition(command.TaskId, command.ExpectedVersion, command.ActorId, command.CorrelationId, command.CausationId, s, c, ct, (a, x) => a.Assign(command.AssigneeId, x.actor, x.correlation, x.now));
    public static Task<TaskCommandResult> Handle(StartTask command, IDocumentSession s, IClock c, CancellationToken ct) => Transition(command.TaskId, command.ExpectedVersion, command.ActorId, command.CorrelationId, command.CausationId, s, c, ct, (a, x) => a.Start(x.actor, x.correlation, x.now));
    public static Task<TaskCommandResult> Handle(BlockTask command, IDocumentSession s, IClock c, CancellationToken ct) => Transition(command.TaskId, command.ExpectedVersion, command.ActorId, command.CorrelationId, command.CausationId, s, c, ct, (a, x) => a.Block(command.Reason, x.actor, x.correlation, x.now));
    public static Task<TaskCommandResult> Handle(CompleteTask command, IDocumentSession s, IClock c, CancellationToken ct) => Transition(command.TaskId, command.ExpectedVersion, command.ActorId, command.CorrelationId, command.CausationId, s, c, ct, (a, x) => a.Complete(x.actor, x.correlation, x.now));
    public static Task<TaskCommandResult> Handle(ReopenTask command, IDocumentSession s, IClock c, CancellationToken ct) => Transition(command.TaskId, command.ExpectedVersion, command.ActorId, command.CorrelationId, command.CausationId, s, c, ct, (a, x) => a.Reopen(command.Reason, x.actor, x.correlation, x.now));
    public static Task<TaskCommandResult> Handle(RequestReview command, IDocumentSession s, IClock c, CancellationToken ct) => Transition(command.TaskId, command.ExpectedVersion, command.ActorId, command.CorrelationId, command.CausationId, s, c, ct, (a, x) => a.RequestReview(x.actor, x.correlation, x.now));
    public static Task<TaskCommandResult> Handle(CompleteAiVerification command, IDocumentSession s, IClock c, CancellationToken ct) => Transition(command.TaskId, command.ExpectedVersion, command.ActorId, command.CorrelationId, command.CausationId, s, c, ct, (a, x) => a.CompleteAiVerification(command.Passed, command.Summary, x.actor, x.correlation, x.now));

    public static Task<TaskCommandResult> Handle(ApproveReview command, IDocumentSession s, IClock c, CancellationToken ct) => Transition(command.TaskId, command.ExpectedVersion, command.ActorId, command.CorrelationId, command.CausationId, s, c, ct, (a, x) => a.ApproveReview(x.actor, x.correlation, x.now));
    public static Task<TaskCommandResult> Handle(RejectReview command, IDocumentSession s, IClock c, CancellationToken ct) => Transition(command.TaskId, command.ExpectedVersion, command.ActorId, command.CorrelationId, command.CausationId, s, c, ct, (a, x) => a.RejectReview(command.Reason, x.actor, x.correlation, x.now));

    private static async Task<TaskCommandResult> Transition(Guid taskId, long expectedVersion, string actorId, string correlationId, string? causationId, IDocumentSession session, IClock clock, CancellationToken cancellationToken, Func<EngineeringTask, TransitionContext, object> decide)
    {
        Validate(taskId, expectedVersion, actorId, correlationId);
        var stream = await session.Events.FetchForWriting<EngineeringTask>(taskId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (stream.Aggregate is null) throw new KeyNotFoundException($"Task '{taskId}' was not found.");
        session.ApplyEventMetadata(new EventMetadata(actorId, correlationId, causationId, stream.Aggregate.ProjectId, null, "taskflow.task.transition"));
        stream.AppendOne(decide(stream.Aggregate, new TransitionContext(actorId.Trim(), correlationId.Trim(), clock.UtcNow)));
        return new(taskId, expectedVersion + 1);
    }

    private sealed record TransitionContext(string actor, string correlation, DateTimeOffset now);

    private static void Validate(Guid projectId, string actorId, string correlationId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(projectId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
    }

    private static void Validate(Guid taskId, long expectedVersion, string actorId, string correlationId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(taskId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
    }

    private static string NormalizeTitle(string value) => NormalizeRequired(value, 240, nameof(value));
    private static string NormalizeRequired(string value, int max, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length < 2 || normalized.Length > max) throw new ArgumentException($"Value must contain between 2 and {max} characters.", name);
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int max = 2000)
    {
        if (value is null) return null;
        var normalized = value.Trim();
        if (normalized.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters.", nameof(value));
        return normalized.Length == 0 ? null : normalized;
    }
}
