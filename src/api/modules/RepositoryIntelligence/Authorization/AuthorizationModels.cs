namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

public enum PermissionDefinitionScope
{
    System,
    Project
}

public enum ResourceScopeKind
{
    Workspace,
    Project,
    Repository,
    Component,
    Own,
    Assigned,
    All
}

public enum ComponentScopeKind
{
    Frontend,
    Backend,
    Database,
    Tests,
    Documentation,
    Infrastructure,
    SharedLibrary,
    Service
}

public enum AiTrustLevel
{
    SuggestOnly,
    CreateTasks,
    UpdateTasks,
    CodeGeneration,
    PullRequestCreation
}

public sealed record PermissionDefinition(
    string Id,
    string Description,
    PermissionDefinitionScope Scope,
    ResourceScopeKind[] AllowedResourceScopes,
    ComponentScopeKind[] AllowedComponentScopes);

public sealed record Project(
    Guid Id,
    string Name,
    Guid PrimaryOwnerId,
    DateTimeOffset CreatedAt);

public sealed record RolePermissionGrant(
    string PermissionCode,
    ResourceScopeKind ResourceScope,
    Guid? ResourceId,
    ComponentScopeKind[] ComponentScopes);

public sealed record ProjectRole(
    Guid Id,
    Guid ProjectId,
    string Name,
    bool IsSystemDefined,
    bool IsOwner,
    RolePermissionGrant[] Permissions);

public sealed record MemberRoleAssignment(Guid RoleId, DateTimeOffset AssignedAt, Guid AssignedBy);

public sealed record ProjectMembership(
    Guid Id,
    Guid ProjectId,
    Guid UserId,
    bool IsActive,
    MemberRoleAssignment[] Roles);

public sealed record AiPermissionPolicy(
    Guid Id,
    Guid ProjectId,
    AiTrustLevel TrustLevel,
    string[] AllowedPermissions,
    Guid UpdatedBy,
    DateTimeOffset UpdatedAt)
{
    public bool Allows(string permissionCode) =>
        AllowedPermissions.Contains(permissionCode, StringComparer.Ordinal) &&
        AiTrustPolicy.IsAllowed(TrustLevel, permissionCode);
}

public sealed record AuditRecord(
    Guid Id,
    Guid? ProjectId,
    Guid ActorId,
    string ActorType,
    string Action,
    DateTimeOffset OccurredAt,
    string TargetType,
    string TargetId,
    string? Before,
    string? After);

public sealed record AuthorizationResourceContext(
    Guid ProjectId,
    Guid? RepositoryId = null,
    ComponentScopeKind? Component = null,
    Guid? OwnerUserId = null,
    IReadOnlyCollection<Guid>? AssignedUserIds = null);

public sealed record PermissionGrantTrace(
    string PermissionCode,
    Guid RoleId,
    string RoleName,
    ResourceScopeKind ResourceScope,
    Guid? ResourceId,
    ComponentScopeKind[] ComponentScopes);

public sealed record EffectivePermissionResult(
    Guid ProjectId,
    Guid UserId,
    IReadOnlyList<PermissionGrantTrace> Grants)
{
    public bool HasPermission(string permissionCode) =>
        Grants.Any(grant => string.Equals(grant.PermissionCode, permissionCode, StringComparison.Ordinal));
}
