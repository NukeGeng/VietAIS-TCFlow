namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

public interface ISystemPermissionEvaluator
{
    Task EnsureAuthorizedAsync(
        Guid userId,
        string permissionCode,
        CancellationToken cancellationToken);
}
