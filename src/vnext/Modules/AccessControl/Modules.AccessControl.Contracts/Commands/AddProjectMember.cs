using VietAIS.TCFlow.BuildingBlocks.Application.Messaging;

namespace VietAIS.TCFlow.Modules.AccessControl.Contracts.Commands;

public sealed record AddProjectMember(
    Guid ProjectId,
    string UserId,
    string ActorId,
    string CorrelationId,
    long ExpectedVersion,
    string? CausationId = null) : ICommand;
