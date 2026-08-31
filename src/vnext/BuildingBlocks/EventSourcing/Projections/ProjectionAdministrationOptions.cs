namespace VietAIS.TCFlow.BuildingBlocks.EventSourcing.Projections;

public sealed class ProjectionAdministrationOptions
{
    public TimeSpan RebuildTimeout { get; set; } = TimeSpan.FromMinutes(10);

    public ISet<string> AllowedProjectionNames { get; } =
        new HashSet<string>(StringComparer.Ordinal);
}
