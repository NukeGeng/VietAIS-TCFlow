namespace VietAIS.TCFlow.Modules.Projects.Projections;

public sealed class ProjectPortfolioSummary
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsSuspended { get; set; }

    public DateTimeOffset LastChangedAtUtc { get; set; }

    public long Version { get; set; }
}
