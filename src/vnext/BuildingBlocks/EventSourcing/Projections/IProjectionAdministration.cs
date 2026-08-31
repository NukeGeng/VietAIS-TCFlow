namespace VietAIS.TCFlow.BuildingBlocks.EventSourcing.Projections;

public interface IProjectionAdministration
{
    Task<IReadOnlyList<ProjectionStatus>> GetStatusAsync(CancellationToken cancellationToken);

    Task RebuildAsync(string projectionName, CancellationToken cancellationToken);
}
