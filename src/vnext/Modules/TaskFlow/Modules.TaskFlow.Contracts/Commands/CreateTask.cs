namespace VietAIS.TCFlow.Modules.TaskFlow.Contracts.Commands;

public sealed record CreateTask(
    Guid ProjectId,
    string Title,
    string? Description,
    string ActorId,
    string CorrelationId,
    string? SourceChangeKey = null,
    Guid? TaskId = null,
    string? CausationId = null);
