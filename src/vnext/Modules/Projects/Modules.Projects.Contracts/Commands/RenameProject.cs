using VietAIS.TCFlow.BuildingBlocks.Application.Messaging;

namespace VietAIS.TCFlow.Modules.Projects.Contracts.Commands;

public sealed record RenameProject(
    Guid ProjectId,
    string Name,
    long ExpectedVersion,
    string ActorId,
    string CorrelationId,
    string? CausationId = null) : ICommand;
