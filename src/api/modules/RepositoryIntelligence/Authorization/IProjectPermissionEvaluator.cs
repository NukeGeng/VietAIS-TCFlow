namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

public interface IProjectPermissionEvaluator
{
    Task<IReadOnlyList<PermissionGrantTrace>> GetProjectPermissionGrantsAsync(
        Guid userId,
        Guid projectId,
        CancellationToken cancellationToken);

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
