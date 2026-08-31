using JasperFx.Events;
using Marten;
using Marten.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Configuration;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Metadata;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Projections;
using VietAIS.TCFlow.Modules.Projects.Configuration;
using VietAIS.TCFlow.Modules.Projects.Domain;
using VietAIS.TCFlow.Modules.Projects.Projections;

namespace VietAIS.TCFlow.BuildingBlocks.EventSourcing.Tests;

public sealed class EventStoreIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("tcflow_event_sourcing_tests")
        .WithUsername("postgres")
        .WithPassword("integration_test_pwd")
        .WithAutoRemove(true)
        .WithCleanUp(true)
        .Build();

    private DocumentStore _store = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _store = DocumentStore.For(options =>
        {
            options.Connection(_postgres.GetConnectionString());
            TcFlowEventStoreConfiguration.Configure(options);
            ProjectsMartenConfiguration.Configure(options);
        });
        await _store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _store.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task AppendReloadAndMetadataPreserveBusinessAndDiagnosticTruth()
    {
        await ResetAsync();
        var projectId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero);
        var metadata = new EventMetadata(
            "owner-1",
            "correlation-1",
            "causation-1",
            projectId,
            TenantId: null,
            Source: "event-sourcing-test");

        await using (var session = _store.LightweightSession())
        {
            session.ApplyEventMetadata(metadata);
            session.Events.StartStream<ProjectAggregate>(
                projectId,
                new ProjectCreated(
                    projectId,
                    "TCFlow",
                    "owner-1",
                    "owner-1",
                    "correlation-1",
                    occurredAt));
            await session.SaveChangesAsync();
        }

        await using var query = _store.QuerySession();
        var aggregate = await query.Events.AggregateStreamAsync<ProjectAggregate>(projectId);
        var current = await query.LoadAsync<ProjectCurrent>(projectId);
        var events = await query.Events.FetchStreamAsync(projectId);
        var persistedMetadata = PersistedEventMetadata.From(events.Single());

        aggregate.ShouldNotBeNull();
        aggregate.Name.ShouldBe("TCFlow");
        current.ShouldNotBeNull();
        current.Name.ShouldBe("TCFlow");
        current.Version.ShouldBe(1);
        persistedMetadata.EventId.ShouldNotBe(Guid.Empty);
        persistedMetadata.StreamId.ShouldBe(projectId);
        persistedMetadata.Version.ShouldBe(1);
        persistedMetadata.ActorId.ShouldBe("owner-1");
        persistedMetadata.CorrelationId.ShouldBe("correlation-1");
        persistedMetadata.CausationId.ShouldBe("causation-1");
        persistedMetadata.ProjectId.ShouldBe(projectId.ToString("D"));
        persistedMetadata.Source.ShouldBe("event-sourcing-test");
    }

    [Fact]
    public async Task ExpectedVersionRejectsAConcurrentSecondWriter()
    {
        await ResetAsync();
        var projectId = await SeedProjectAsync();
        await using var first = _store.LightweightSession();
        await using var second = _store.LightweightSession();
        var firstStream = await first.Events.FetchForWriting<ProjectAggregate>(projectId, 1);
        var secondStream = await second.Events.FetchForWriting<ProjectAggregate>(projectId, 1);
        var now = new DateTimeOffset(2026, 8, 30, 11, 0, 0, TimeSpan.Zero);

        firstStream.AppendOne(firstStream.Aggregate!.Rename("First", "actor-1", "c-1", now));
        secondStream.AppendOne(secondStream.Aggregate!.Rename("Second", "actor-2", "c-2", now));

        await first.SaveChangesAsync();
        await Should.ThrowAsync<EventStreamUnexpectedMaxEventIdException>(
            () => second.SaveChangesAsync());

        await using var query = _store.QuerySession();
        var events = await query.Events.FetchStreamAsync(projectId);
        var current = await query.LoadAsync<ProjectCurrent>(projectId);
        events.Count.ShouldBe(2);
        current.ShouldNotBeNull();
        current.Name.ShouldBe("First");
        current.Version.ShouldBe(2);
    }

    [Fact]
    public async Task InlineAndAsyncProjectionsRebuildAndConvergeFromEventHistory()
    {
        await ResetAsync();
        var projectId = await SeedProjectAsync();
        await using (var session = _store.LightweightSession())
        {
            var stream = await session.Events.FetchForWriting<ProjectAggregate>(projectId, 1);
            stream.AppendOne(stream.Aggregate!.Rename(
                "Rebuilt TCFlow",
                "owner-1",
                "correlation-2",
                new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero)));
            await session.SaveChangesAsync();
        }

        await using (var beforeRebuild = _store.QuerySession())
        {
            var inline = await beforeRebuild.LoadAsync<ProjectCurrent>(projectId);
            var asynchronous = await beforeRebuild.LoadAsync<ProjectPortfolioSummary>(projectId);
            inline.ShouldNotBeNull();
            inline.Name.ShouldBe("Rebuilt TCFlow");
            asynchronous.ShouldBeNull();
        }

        var administration = CreateProjectionAdministration();
        await administration.RebuildAsync(
            ProjectProjectionNames.PortfolioSummary,
            CancellationToken.None);

        await AssertProjectionsConvergedAsync(projectId);
        var statuses = await administration.GetStatusAsync(CancellationToken.None);
        statuses.ShouldContain(status => status.ProjectionName.Contains(
            ProjectProjectionNames.PortfolioSummary,
            StringComparison.Ordinal));

        await _store.Advanced.Clean.DeleteDocumentsByTypeAsync(typeof(ProjectCurrent));
        await _store.Advanced.Clean.DeleteDocumentsByTypeAsync(typeof(ProjectPortfolioSummary));

        await administration.RebuildAsync(ProjectProjectionNames.Current, CancellationToken.None);
        await administration.RebuildAsync(
            ProjectProjectionNames.PortfolioSummary,
            CancellationToken.None);

        await AssertProjectionsConvergedAsync(projectId);
    }

    [Fact]
    public async Task ProjectionAdministrationRejectsUnapprovedRebuilds()
    {
        var administration = CreateProjectionAdministration();

        var failure = await Should.ThrowAsync<InvalidOperationException>(
            () => administration.RebuildAsync("unknown-projection", CancellationToken.None));

        failure.Message.ShouldContain("not approved", Case.Insensitive);
    }

    [Fact]
    public async Task DuplicateCreateDeliveryDoesNotDuplicateBusinessEffect()
    {
        await ResetAsync();
        var projectId = await SeedProjectAsync();

        await using (var duplicate = _store.LightweightSession())
        {
            duplicate.Events.StartStream<ProjectAggregate>(
                projectId,
                new ProjectCreated(
                    projectId,
                    "Duplicate",
                    "owner-1",
                    "owner-1",
                    "duplicate-correlation",
                    DateTimeOffset.UtcNow));

            await Should.ThrowAsync<ExistingStreamIdCollisionException>(
                () => duplicate.SaveChangesAsync());
        }

        await using var query = _store.QuerySession();
        var events = await query.Events.FetchStreamAsync(projectId);
        var current = await query.LoadAsync<ProjectCurrent>(projectId);
        events.Count.ShouldBe(1);
        current.ShouldNotBeNull();
        current.Name.ShouldBe("TCFlow");
        current.Version.ShouldBe(1);
    }

    [Fact]
    public async Task LifecycleEventsPreserveActorMetadataAndUpdateReadModels()
    {
        await ResetAsync();
        var projectId = await SeedProjectAsync();
        await using (var session = _store.LightweightSession())
        {
            session.ApplyEventMetadata(new EventMetadata(
                "admin-1",
                "correlation-suspend",
                CausationId: "causation-create",
                projectId,
                TenantId: null,
                Source: "projects.suspend"));
            var stream = await session.Events.FetchForWriting<ProjectAggregate>(projectId, 1);
            stream.AppendOne(stream.Aggregate!.Suspend(
                "admin-1",
                "correlation-suspend",
                new DateTimeOffset(2026, 8, 30, 13, 0, 0, TimeSpan.Zero)));
            await session.SaveChangesAsync();
        }

        await using (var query = _store.QuerySession())
        {
            var current = await query.LoadAsync<ProjectCurrent>(projectId);
            current.ShouldNotBeNull();
            current.IsSuspended.ShouldBeTrue();
            current.Version.ShouldBe(2);

            var events = await query.Events.FetchStreamAsync(projectId);
            var metadata = PersistedEventMetadata.From(events[^1]);
            metadata.ActorId.ShouldBe("admin-1");
            metadata.CorrelationId.ShouldBe("correlation-suspend");
            metadata.CausationId.ShouldBe("causation-create");
            metadata.Source.ShouldBe("projects.suspend");
        }

        var administration = CreateProjectionAdministration();
        await administration.RebuildAsync(ProjectProjectionNames.PortfolioSummary, CancellationToken.None);
        await using var converged = _store.QuerySession();
        var summary = await converged.LoadAsync<ProjectPortfolioSummary>(projectId);
        summary.ShouldNotBeNull();
        summary.IsSuspended.ShouldBeTrue();
        summary.Version.ShouldBe(2);
    }

    private async Task<Guid> SeedProjectAsync()
    {
        var projectId = Guid.NewGuid();
        await using var session = _store.LightweightSession();
        session.ApplyEventMetadata(new EventMetadata(
            "owner-1",
            "correlation-1",
            CausationId: null,
            projectId,
            TenantId: null,
            Source: "event-sourcing-test"));
        session.Events.StartStream<ProjectAggregate>(
            projectId,
            new ProjectCreated(
                projectId,
                "TCFlow",
                "owner-1",
                "owner-1",
                "correlation-1",
                new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero)));
        await session.SaveChangesAsync();
        return projectId;
    }

    private async Task ResetAsync()
    {
        await _store.Advanced.Clean.DeleteAllEventDataAsync();
        await _store.Advanced.Clean.DeleteAllDocumentsAsync();
    }

    private MartenProjectionAdministration CreateProjectionAdministration()
    {
        var options = new ProjectionAdministrationOptions
        {
            RebuildTimeout = TimeSpan.FromSeconds(30),
        };
        options.AllowedProjectionNames.Add(ProjectProjectionNames.Current);
        options.AllowedProjectionNames.Add(ProjectProjectionNames.PortfolioSummary);

        return new MartenProjectionAdministration(
            _store,
            Options.Create(options),
            NullLogger<MartenProjectionAdministration>.Instance);
    }

    private async Task AssertProjectionsConvergedAsync(Guid projectId)
    {
        await using var query = _store.QuerySession();
        var inline = await query.LoadAsync<ProjectCurrent>(projectId);
        var asynchronous = await query.LoadAsync<ProjectPortfolioSummary>(projectId);

        inline.ShouldNotBeNull();
        asynchronous.ShouldNotBeNull();
        asynchronous.Name.ShouldBe(inline.Name);
        asynchronous.IsSuspended.ShouldBe(inline.IsSuspended);
        asynchronous.Version.ShouldBe(inline.Version);
        asynchronous.LastChangedAtUtc.ShouldBe(inline.LastChangedAtUtc);
    }
}
