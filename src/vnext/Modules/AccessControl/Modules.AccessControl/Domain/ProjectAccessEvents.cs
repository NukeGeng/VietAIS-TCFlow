using VietAIS.TCFlow.Modules.AccessControl.Contracts.Models;

namespace VietAIS.TCFlow.Modules.AccessControl.Domain;

public sealed record ProjectAccessInitialized(
    Guid ProjectId,
    string OwnerId,
    Guid OwnerRoleId,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);

public sealed record ProjectRoleCreated(
    Guid ProjectId,
    Guid RoleId,
    string Name,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);

public sealed record ProjectRolePermissionsUpdated(
    Guid ProjectId,
    Guid RoleId,
    IReadOnlyList<ProjectPermissionGrant> Grants,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);

public sealed record ProjectMemberAdded(
    Guid ProjectId,
    string UserId,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);

public sealed record ProjectMemberRolesAssigned(
    Guid ProjectId,
    string UserId,
    IReadOnlyList<Guid> RoleIds,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);

public sealed record ProjectMemberRemoved(
    Guid ProjectId,
    string UserId,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);
