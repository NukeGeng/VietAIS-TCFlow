namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

public interface IProjectPermissionEvaluator
{
    Task<EffectivePermissionResult> GetEffectivePermissionsAsync(
        Guid userId,
        AuthorizationResourceContext resource,
        CancellationToken cancellationToken);

    Task EnsureAuthorizedAsync(
        Guid userId,
        string permissionCode,
        AuthorizationResourceContext resource,
        CancellationToken cancellationToken);
}
