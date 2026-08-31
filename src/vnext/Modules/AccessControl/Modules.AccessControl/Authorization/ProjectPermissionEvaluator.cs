using Marten;
using VietAIS.TCFlow.Modules.AccessControl.Contracts.Models;
using VietAIS.TCFlow.Modules.AccessControl.Projections;

namespace VietAIS.TCFlow.Modules.AccessControl.Authorization;

public sealed class ProjectPermissionEvaluator(IQuerySession session) : IProjectPermissionEvaluator
{
    public async Task<EffectiveProjectPermissions> GetEffectivePermissionsAsync(
        string userId,
        Guid projectId,
        string? repositoryId,
        ProjectComponentScope? component,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var current = await session.Query<ProjectAccessCurrent>()
            .SingleOrDefaultAsync(item => item.ProjectId == projectId, cancellationToken)
            .ConfigureAwait(false);
        if (current is null)
        {
            return new EffectiveProjectPermissions(projectId, userId.Trim(), [], []);
        }

        var member = current.Members.FirstOrDefault(item =>
            string.Equals(item.UserId, userId.Trim(), StringComparison.Ordinal) && item.IsActive);
        if (member is null)
        {
            return new EffectiveProjectPermissions(projectId, userId.Trim(), [], []);
        }

        var roleIds = member.RoleIds.Distinct().ToArray();
        var grants = current.Roles
            .Where(role => roleIds.Contains(role.RoleId))
            .SelectMany(role => role.Grants)
            .Where(grant => AppliesTo(grant, repositoryId, component))
            .Distinct()
            .OrderBy(grant => grant.PermissionCode, StringComparer.Ordinal)
            .ToArray();
        return new EffectiveProjectPermissions(projectId, userId.Trim(), grants, roleIds);
    }

    public async Task EnsureAuthorizedAsync(
        string? userId,
        Guid projectId,
        string permissionCode,
        string? repositoryId,
        ProjectComponentScope? component,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("An authenticated project actor is required.");
        }

        var effective = await GetEffectivePermissionsAsync(
            userId,
            projectId,
            repositoryId,
            component,
            cancellationToken).ConfigureAwait(false);
        if (!effective.Has(permissionCode))
        {
            throw new InvalidOperationException(
                $"Permission '{permissionCode}' is not granted for project '{projectId}'.");
        }
    }

    private static bool AppliesTo(
        ProjectPermissionGrant grant,
        string? repositoryId,
        ProjectComponentScope? component) =>
        grant.ResourceScope switch
        {
            ProjectResourceScope.Project or ProjectResourceScope.All => true,
            ProjectResourceScope.Repository =>
                !string.IsNullOrWhiteSpace(repositoryId) &&
                string.Equals(grant.ResourceId, repositoryId, StringComparison.Ordinal),
            ProjectResourceScope.Component => component is not null,
            _ => component is not null || !string.IsNullOrWhiteSpace(repositoryId)
        } && (grant.Components is null || grant.Components.Count == 0 ||
              component is not null && grant.Components.Contains(component.Value));
}
