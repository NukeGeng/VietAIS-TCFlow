namespace VietAIS.TCFlow.Modules.Projects.Domain;

public sealed class ProjectAggregate
{
    public ProjectAggregate()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string OwnerId { get; private set; } = string.Empty;

    public bool IsSuspended { get; private set; }

    public void Apply(ProjectCreated @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        Id = @event.ProjectId;
        Name = @event.Name;
        OwnerId = @event.OwnerId;
        IsSuspended = false;
    }

    public void Apply(ProjectRenamed @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        Name = @event.Name;
    }

    public void Apply(ProjectSuspended @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        IsSuspended = true;
    }

    public void Apply(ProjectActivated @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        IsSuspended = false;
    }

    public ProjectRenamed Rename(string name, string actorId, string correlationId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (IsSuspended)
        {
            throw new InvalidOperationException("A suspended project cannot be renamed.");
        }

        return new ProjectRenamed(Id, name.Trim(), actorId, correlationId, now);
    }
}
