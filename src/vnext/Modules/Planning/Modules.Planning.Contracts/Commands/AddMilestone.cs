using VietAIS.TCFlow.BuildingBlocks.Application.Messaging;

namespace VietAIS.TCFlow.Modules.Planning.Contracts.Commands;

public sealed record AddMilestone(
    Guid PlanId,
    Guid MilestoneId,
    string Name,
    DateOnly? TargetDate,
    string ActorId,
    string CorrelationId,
    long ExpectedVersion,
    string? CausationId = null) : ICommand;
