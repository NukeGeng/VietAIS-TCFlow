using VietAIS.TCFlow.BuildingBlocks.Application.Messaging;
using VietAIS.TCFlow.Modules.AccessControl.Contracts.Models;

namespace VietAIS.TCFlow.Modules.AccessControl.Contracts.Commands;

public sealed record UpdateProjectRolePermissions(
    Guid ProjectId,
    Guid RoleId,
    IReadOnlyList<ProjectPermissionGrant> Grants,
    string ActorId,
    string CorrelationId,
    long ExpectedVersion,
    string? CausationId = null) : ICommand;
