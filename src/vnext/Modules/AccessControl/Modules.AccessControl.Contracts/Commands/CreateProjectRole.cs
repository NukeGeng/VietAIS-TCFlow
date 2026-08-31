using VietAIS.TCFlow.BuildingBlocks.Application.Messaging;

namespace VietAIS.TCFlow.Modules.AccessControl.Contracts.Commands;

public sealed record CreateProjectRole(
    Guid ProjectId,
    string Name,
    string ActorId,
    string CorrelationId,
    long ExpectedVersion = 0,
    string? CausationId = null) : ICommand;
