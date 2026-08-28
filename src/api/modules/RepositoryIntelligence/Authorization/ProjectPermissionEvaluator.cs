using FSH.Framework.Core.Exceptions;
using Marten;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

public sealed class ProjectPermissionEvaluator(IQuerySession session) : IProjectPermissionEvaluator
{
    public async Task<IReadOnlyList<PermissionGrantTrace>> GetProjectPermissionGrantsAsync(
        Guid userId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var state = await session.LoadAsync<ProjectState>(projectId, cancellationToken);
        if (state is not null && state.Status != ProjectLifecycleStatus.Active)
        {
            return [];
        }

        var membership = await session.Query<ProjectMembership>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == projectId && item.UserId == userId && item.IsActive,
                cancellationToken);

        if (membership is null)
        {
            return [];
        }

        var grants = new List<PermissionGrantTrace>();
        foreach (var assignment in membership.Roles)
        {
            var role = await session.LoadAsync<ProjectRole>(assignment.RoleId, cancellationToken);
            if (role is null || role.ProjectId != projectId)
            {
                continue;
            }

            grants.AddRange(role.Permissions.Select(grant => new PermissionGrantTrace(
                    grant.PermissionCode,
                    role.Id,
                    role.Name,
                    grant.ResourceScope,
                    grant.ResourceId,
                    grant.ComponentScopes)));
        }

        return grants
            .OrderBy(grant => grant.PermissionCode, StringComparer.Ordinal)
            .ThenBy(grant => grant.RoleName, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<EffectivePermissionResult> GetEffectivePermissionsAsync(
        Guid userId,
        AuthorizationResourceContext resource,
        CancellationToken cancellationToken)
    {
        var grants = await GetProjectPermissionGrantsAsync(
            userId,
            resource.ProjectId,
            cancellationToken);
        return new EffectivePermissionResult(
            resource.ProjectId,
            userId,
            grants.Where(grant => AppliesTo(grant, userId, resource)).ToArray());
    }

    public async Task EnsureAuthorizedAsync(
        Guid userId,
        string permissionCode,
        AuthorizationResourceContext resource,
        CancellationToken cancellationToken)
    {
        var effective = await GetEffectivePermissionsAsync(userId, resource, cancellationToken);
        if (!effective.HasPermission(permissionCode))
        {
            throw new ForbiddenException(
                $"Permission '{permissionCode}' is not granted for the requested project scope.");
        }
    }

    internal static bool AppliesTo(
        PermissionGrantTrace grant,
        Guid userId,
        AuthorizationResourceContext resource)
    {
        if (grant.ComponentScopes.Length > 0 &&
            (resource.Component is null || !grant.ComponentScopes.Contains(resource.Component.Value)))
        {
            return false;
        }

        return grant.ResourceScope switch
        {
            ResourceScopeKind.Workspace => true,
            ResourceScopeKind.Project => true,
            ResourceScopeKind.All => true,
            ResourceScopeKind.Repository =>
                resource.RepositoryId is not null && grant.ResourceId == resource.RepositoryId,
            ResourceScopeKind.Component => resource.Component is not null,
            ResourceScopeKind.Own => resource.OwnerUserId == userId,
            ResourceScopeKind.Assigned => resource.AssignedUserIds?.Contains(userId) is true,
            _ => false
        };
    }
}
