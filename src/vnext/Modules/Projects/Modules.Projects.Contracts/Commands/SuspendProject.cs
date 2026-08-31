using VietAIS.TCFlow.BuildingBlocks.Application.Messaging;

namespace VietAIS.TCFlow.Modules.Projects.Contracts.Commands;

public sealed record SuspendProject(
    Guid ProjectId,
    long ExpectedVersion,
    string ActorId,
    string CorrelationId,
    string? CausationId = null) : ICommand;
