using VietAIS.TCFlow.BuildingBlocks.Application.Messaging;

namespace VietAIS.TCFlow.Modules.Projects.Contracts.Commands;

public sealed record CreateProject(
    Guid ProjectId,
    string Name,
    string OwnerId,
    string CorrelationId,
    string? CausationId = null) : ICommand;
