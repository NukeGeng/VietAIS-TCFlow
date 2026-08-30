namespace VietAIS.TCFlow.Modules.Projects.Projections;

public sealed class ProjectCurrent
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string OwnerId { get; set; } = string.Empty;

    public bool IsSuspended { get; set; }

    public DateTimeOffset LastChangedAtUtc { get; set; }

    public long Version { get; set; }
}
