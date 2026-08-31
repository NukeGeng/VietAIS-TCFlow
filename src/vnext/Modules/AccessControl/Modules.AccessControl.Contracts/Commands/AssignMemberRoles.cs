using VietAIS.TCFlow.BuildingBlocks.Application.Messaging;

namespace VietAIS.TCFlow.Modules.AccessControl.Contracts.Commands;

public sealed record AssignMemberRoles(
    Guid ProjectId,
    string UserId,
    IReadOnlyList<Guid> RoleIds,
    string ActorId,
    string CorrelationId,
    long ExpectedVersion,
    string? CausationId = null) : ICommand;
