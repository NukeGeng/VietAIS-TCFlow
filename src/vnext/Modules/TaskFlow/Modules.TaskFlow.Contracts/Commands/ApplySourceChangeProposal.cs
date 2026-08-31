namespace VietAIS.TCFlow.Modules.TaskFlow.Contracts.Commands;

public sealed record ApplySourceChangeProposal(
    Guid ProjectId,
    string SourceChangeKey,
    string Title,
    string? Description,
    string ActorId,
    string CorrelationId,
    string? CausationId = null);
