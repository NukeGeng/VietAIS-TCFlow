using VietAIS.TCFlow.BuildingBlocks.Application.Messaging;

namespace VietAIS.TCFlow.Modules.Planning.Contracts.Commands;

public sealed record CreatePlan(
    Guid PlanId,
    Guid ProjectId,
    string Name,
    string? Purpose,
    string ActorId,
    string CorrelationId,
    string? CausationId = null) : ICommand;
