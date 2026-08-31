using VietAIS.TCFlow.BuildingBlocks.Application.Messaging;

namespace VietAIS.TCFlow.Modules.Planning.Contracts.Commands;

public sealed record AddRequirement(
    Guid PlanId,
    Guid RequirementId,
    string Title,
    string? Description,
    string ActorId,
    string CorrelationId,
    long ExpectedVersion,
    string? CausationId = null) : ICommand;
