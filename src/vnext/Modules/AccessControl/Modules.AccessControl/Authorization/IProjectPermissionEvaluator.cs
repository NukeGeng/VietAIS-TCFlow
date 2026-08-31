using VietAIS.TCFlow.Modules.AccessControl.Contracts.Models;

namespace VietAIS.TCFlow.Modules.AccessControl.Authorization;

public interface IProjectPermissionEvaluator
{
    Task<EffectiveProjectPermissions> GetEffectivePermissionsAsync(
        string userId,
        Guid projectId,
        string? repositoryId,
        ProjectComponentScope? component,
        CancellationToken cancellationToken);

    Task EnsureAuthorizedAsync(
        string? userId,
        Guid projectId,
        string permissionCode,
        string? repositoryId,
        ProjectComponentScope? component,
        CancellationToken cancellationToken);
}
