using VietAIS.TCFlow.Modules.Projects.Domain;

namespace VietAIS.TCFlow.Modules.Projects.Tests;

public sealed class ProjectAggregateTests
{
    [Fact]
    public void ApplyingEventsReconstructsCurrentState()
    {
        var id = Guid.NewGuid();
        var created = new ProjectCreated(
            id,
            "Payments",
            "owner-1",
            "owner-1",
            "correlation-1",
            DateTimeOffset.UtcNow);
        var renamed = new ProjectRenamed(
            id,
            "Payments Platform",
            "owner-1",
            "correlation-2",
            created.OccurredAtUtc.AddMinutes(1));

        var aggregate = AggregateFrom(created, renamed);

        Assert.Equal(id, aggregate.Id);
        Assert.Equal("Payments Platform", aggregate.Name);
        Assert.Equal("owner-1", aggregate.OwnerId);
        Assert.False(aggregate.IsSuspended);
    }

    [Fact]
    public void SuspendedProjectCannotBeRenamed()
    {
        var id = Guid.NewGuid();
        var created = new ProjectCreated(
            id,
            "Payments",
            "owner-1",
            "owner-1",
            "correlation-1",
            DateTimeOffset.UtcNow);
        var suspended = new ProjectSuspended(
            id,
            "admin-1",
            "correlation-2",
            created.OccurredAtUtc.AddMinutes(1));
        var aggregate = AggregateFrom(created, suspended);

        Assert.Throws<InvalidOperationException>(() => aggregate.Rename(
            "New name",
            "owner-1",
            "correlation-3",
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void LifecycleDecisionsRequireTheExpectedState()
    {
        var id = Guid.NewGuid();
        var created = new ProjectCreated(
            id,
            "Payments",
            "owner-1",
            "owner-1",
            "correlation-1",
            DateTimeOffset.UtcNow);
        var aggregate = AggregateFrom(created);

        var suspended = aggregate.Suspend(
            "admin-1",
            "correlation-2",
            created.OccurredAtUtc.AddMinutes(1));
        aggregate.Apply(suspended);
        Assert.Throws<InvalidOperationException>(() => aggregate.Suspend(
            "admin-1",
            "correlation-duplicate-suspend",
            created.OccurredAtUtc.AddMinutes(1).AddSeconds(1)));
        var activated = aggregate.Activate(
            "admin-1",
            "correlation-3",
            created.OccurredAtUtc.AddMinutes(2));

        Assert.NotNull(activated);
        Assert.True(aggregate.IsSuspended);
        aggregate.Apply(activated);
        Assert.False(aggregate.IsSuspended);
        Assert.Throws<InvalidOperationException>(() => aggregate.Activate(
            "admin-1",
            "correlation-duplicate-activate",
            created.OccurredAtUtc.AddMinutes(3)));
    }

    [Fact]
    public void ProjectNameInvariantIsSharedByCreateAndRenameDecisions()
    {
        var id = Guid.NewGuid();
        var created = new ProjectCreated(
            id,
            "Payments",
            "owner-1",
            "owner-1",
            "correlation-1",
            DateTimeOffset.UtcNow);
        var aggregate = AggregateFrom(created);

        Assert.Throws<ArgumentException>(() => aggregate.Rename(
            "x",
            "owner-1",
            "correlation-2",
            DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => aggregate.Rename(
            new string('x', 151),
            "owner-1",
            "correlation-3",
            DateTimeOffset.UtcNow));
    }

    private static ProjectAggregate AggregateFrom(ProjectCreated created, params object[] events)
    {
        var aggregate = (ProjectAggregate)Activator.CreateInstance(
            typeof(ProjectAggregate),
            nonPublic: true)!;
        aggregate.Apply(created);
        foreach (var @event in events)
        {
            switch (@event)
            {
                case ProjectRenamed renamed:
                    aggregate.Apply(renamed);
                    break;
                case ProjectSuspended suspended:
                    aggregate.Apply(suspended);
                    break;
                case ProjectActivated activated:
                    aggregate.Apply(activated);
                    break;
            }
        }

        return aggregate;
    }
}
