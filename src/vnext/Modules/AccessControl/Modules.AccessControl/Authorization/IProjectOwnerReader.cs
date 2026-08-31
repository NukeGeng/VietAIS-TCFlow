namespace VietAIS.TCFlow.Modules.AccessControl.Authorization;

public interface IProjectOwnerReader
{
    Task<string?> GetOwnerIdAsync(Guid projectId, CancellationToken cancellationToken);
}
