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

    public void Apply(ProjectLifecycleReconciled @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        IsSuspended = @event.IsSuspended;
    }

    public ProjectRenamed Rename(string name, string actorId, string correlationId, DateTimeOffset now)
    {
        ValidateName(name);

        if (IsSuspended)
        {
            throw new InvalidOperationException("A suspended project cannot be renamed.");
        }

        return new ProjectRenamed(Id, name.Trim(), actorId, correlationId, now);
    }

    public ProjectSuspended Suspend(string actorId, string correlationId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        if (IsSuspended)
        {
            throw new InvalidOperationException("A suspended project cannot be suspended again.");
        }

        return new ProjectSuspended(Id, actorId.Trim(), correlationId.Trim(), now);
    }

    public ProjectActivated Activate(string actorId, string correlationId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        if (!IsSuspended)
        {
            throw new InvalidOperationException("An active project cannot be activated again.");
        }

        return new ProjectActivated(Id, actorId.Trim(), correlationId.Trim(), now);
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Trim().Length is < 2 or > 150)
        {
            throw new ArgumentException(
                "Project name must contain between 2 and 150 characters.",
                nameof(name));
        }
    }
}
