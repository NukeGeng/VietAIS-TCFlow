using VietAIS.TCFlow.Modules.AccessControl.Contracts.Models;

namespace VietAIS.TCFlow.Modules.AccessControl.Domain;

public sealed class ProjectAccessAggregate
{
    private readonly Dictionary<Guid, ProjectRoleState> _roles = [];
    private readonly Dictionary<string, ProjectMemberState> _members = new(StringComparer.Ordinal);

    public Guid ProjectId { get; private set; }
    public string OwnerId { get; private set; } = string.Empty;

    public IReadOnlyCollection<ProjectRoleState> Roles => _roles.Values;
    public IReadOnlyCollection<ProjectMemberState> Members => _members.Values;

    public void Apply(ProjectAccessInitialized @event)
    {
        ProjectId = @event.ProjectId;
        OwnerId = NormalizeIdentity(@event.OwnerId, nameof(@event.OwnerId));
        _roles[@event.OwnerRoleId] = new ProjectRoleState(
            @event.OwnerRoleId,
            "Owner",
            IsSystemDefined: true,
            ProjectPermissionCatalog.OwnerGrants.ToArray());
        _members[OwnerId] = new ProjectMemberState(OwnerId, true, [@event.OwnerRoleId]);
    }

    public void Apply(ProjectRoleCreated @event) =>
        _roles[@event.RoleId] = new ProjectRoleState(@event.RoleId, @event.Name, false, []);

    public void Apply(ProjectRolePermissionsUpdated @event)
    {
        if (_roles.TryGetValue(@event.RoleId, out var role))
        {
            _roles[@event.RoleId] = role with { Grants = @event.Grants.ToArray() };
        }
    }

    public void Apply(ProjectMemberAdded @event) =>
        _members[NormalizeIdentity(@event.UserId, nameof(@event.UserId))] =
            new ProjectMemberState(NormalizeIdentity(@event.UserId, nameof(@event.UserId)), true, []);

    public void Apply(ProjectMemberRolesAssigned @event)
    {
        var userId = NormalizeIdentity(@event.UserId, nameof(@event.UserId));
        if (_members.TryGetValue(userId, out var member))
        {
            _members[userId] = member with { RoleIds = @event.RoleIds.ToArray() };
        }
    }

    public void Apply(ProjectMemberRemoved @event)
    {
        var userId = NormalizeIdentity(@event.UserId, nameof(@event.UserId));
        if (!string.Equals(userId, OwnerId, StringComparison.Ordinal))
        {
            _members.Remove(userId);
        }
    }

    public ProjectRoleCreated CreateRole(string name, Guid roleId, string actorId, string correlationId, DateTimeOffset now)
    {
        ValidateActor(actorId, correlationId);
        name = NormalizeRoleName(name);
        if (_roles.Values.Any(role => string.Equals(role.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A project role with the same name already exists.");
        }

        return new ProjectRoleCreated(ProjectId, roleId, name, actorId.Trim(), correlationId.Trim(), now);
    }

    public ProjectRolePermissionsUpdated UpdatePermissions(
        Guid roleId,
        IReadOnlyList<ProjectPermissionGrant> grants,
        string actorId,
        string correlationId,
        DateTimeOffset now)
    {
        ValidateActor(actorId, correlationId);
        if (!_roles.TryGetValue(roleId, out var role))
        {
            throw new KeyNotFoundException($"Project role '{roleId}' was not found.");
        }

        if (role.IsSystemDefined)
        {
            throw new InvalidOperationException("System-defined project roles cannot be modified.");
        }

        var normalized = ValidateGrants(grants);
        return new ProjectRolePermissionsUpdated(
            ProjectId,
            roleId,
            normalized,
            actorId.Trim(),
            correlationId.Trim(),
            now);
    }

    public ProjectMemberAdded AddMember(string userId, string actorId, string correlationId, DateTimeOffset now)
    {
        ValidateActor(actorId, correlationId);
        userId = NormalizeIdentity(userId, nameof(userId));
        if (_members.TryGetValue(userId, out var existing) && existing.IsActive)
        {
            throw new InvalidOperationException("The project member already exists.");
        }

        return new ProjectMemberAdded(ProjectId, userId, actorId.Trim(), correlationId.Trim(), now);
    }

    public ProjectMemberRolesAssigned AssignRoles(
        string userId,
        IReadOnlyList<Guid> roleIds,
        string actorId,
        string correlationId,
        DateTimeOffset now)
    {
        ValidateActor(actorId, correlationId);
        userId = NormalizeIdentity(userId, nameof(userId));
        if (!_members.TryGetValue(userId, out var member) || !member.IsActive)
        {
            throw new KeyNotFoundException($"Project member '{userId}' was not found.");
        }

        if (roleIds is null || roleIds.Count == 0 || roleIds.Any(roleId => !_roles.ContainsKey(roleId)))
        {
            throw new InvalidOperationException("Every assigned role must belong to the project.");
        }

        if (string.Equals(userId, OwnerId, StringComparison.Ordinal) && !roleIds.Contains(member.RoleIds[0]))
        {
            throw new InvalidOperationException("The project owner must retain the Owner role.");
        }

        return new ProjectMemberRolesAssigned(
            ProjectId,
            userId,
            roleIds.Distinct().ToArray(),
            actorId.Trim(),
            correlationId.Trim(),
            now);
    }

    public ProjectMemberRemoved RemoveMember(string userId, string actorId, string correlationId, DateTimeOffset now)
    {
        ValidateActor(actorId, correlationId);
        userId = NormalizeIdentity(userId, nameof(userId));
        if (!_members.ContainsKey(userId))
        {
            throw new KeyNotFoundException($"Project member '{userId}' was not found.");
        }

        if (string.Equals(userId, OwnerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The project owner cannot be removed.");
        }

        return new ProjectMemberRemoved(ProjectId, userId, actorId.Trim(), correlationId.Trim(), now);
    }

    private static IReadOnlyList<ProjectPermissionGrant> ValidateGrants(IReadOnlyList<ProjectPermissionGrant> grants)
    {
        if (grants is null)
        {
            throw new ArgumentNullException(nameof(grants));
        }

        var normalized = grants.Select(grant =>
        {
            ArgumentNullException.ThrowIfNull(grant);
            if (!ProjectPermissionCatalog.All.Contains(grant.PermissionCode) ||
                grant.ResourceScope is not (ProjectResourceScope.Project or ProjectResourceScope.Repository or ProjectResourceScope.Component or ProjectResourceScope.Own or ProjectResourceScope.Assigned or ProjectResourceScope.All))
            {
                throw new InvalidOperationException("Project roles may grant only defined project permissions and scopes.");
            }

            var components = grant.Components?.Distinct().Order().ToArray() ?? [];
            if (grant.ResourceScope == ProjectResourceScope.Repository && string.IsNullOrWhiteSpace(grant.ResourceId))
            {
                throw new InvalidOperationException("Repository-scoped grants require a repository identifier.");
            }

            return grant with { PermissionCode = grant.PermissionCode.Trim(), Components = components };
        }).ToArray();

        if (normalized.Select(grant => $"{grant.PermissionCode}|{grant.ResourceScope}|{grant.ResourceId}|{string.Join(',', grant.Components!)}").Distinct(StringComparer.Ordinal).Count() != normalized.Length)
        {
            throw new InvalidOperationException("Duplicate permission grants are not allowed.");
        }

        return normalized;
    }

    private static void ValidateActor(string actorId, string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
    }

    private static string NormalizeIdentity(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }

    private static string NormalizeRoleName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        if (normalized.Length is < 2 or > 100)
        {
            throw new ArgumentException("Role name must contain between 2 and 100 characters.", nameof(name));
        }

        return normalized;
    }
}

public sealed record ProjectRoleState(
    Guid RoleId,
    string Name,
    bool IsSystemDefined,
    IReadOnlyList<ProjectPermissionGrant> Grants);

public sealed record ProjectMemberState(
    string UserId,
    bool IsActive,
    IReadOnlyList<Guid> RoleIds);
