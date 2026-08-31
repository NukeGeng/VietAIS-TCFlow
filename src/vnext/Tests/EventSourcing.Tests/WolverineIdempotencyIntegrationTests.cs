using JasperFx.Resources;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using VietAIS.TCFlow.BuildingBlocks.Application.Identity;
using VietAIS.TCFlow.BuildingBlocks.Application.Time;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Configuration;
using VietAIS.TCFlow.BuildingBlocks.Messaging;
using VietAIS.TCFlow.Modules.Projects.Configuration;
using VietAIS.TCFlow.Modules.Projects.Contracts.Commands;
using VietAIS.TCFlow.Modules.Projects.Features;
using VietAIS.TCFlow.Modules.Projects.Projections;
using Wolverine;
using Wolverine.Marten;
using Wolverine.Persistence;
using Wolverine.Persistence.Durability;
using Wolverine.Runtime;
using Wolverine.Tracking;

namespace VietAIS.TCFlow.BuildingBlocks.EventSourcing.Tests;

public sealed class WolverineIdempotencyIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("tcflow_wolverine_tests")
        .WithUsername("postgres")
        .WithPassword("integration_test_pwd")
        .WithAutoRemove(true)
        .WithCleanUp(true)
        .Build();

    private IHost _host = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _host = await Host.CreateDefaultBuilder()
            .UseWolverine(options =>
            {
                options.Discovery.DisableConventionalDiscovery()
                    .IncludeType(typeof(ProjectCommandHandlers));
                options.Durability.Mode = DurabilityMode.Solo;
                options.Services.AddSingleton(TimeProvider.System);
                options.Services.AddSingleton<IClock, SystemClock>();
                options.Services.AddSingleton<IIdGenerator, UuidV7IdGenerator>();
                options.Services.AddMarten(storeOptions =>
                {
                    storeOptions.Connection(_postgres.GetConnectionString());
                    TcFlowEventStoreConfiguration.Configure(storeOptions);
                    ProjectsMartenConfiguration.Configure(storeOptions);
                }).IntegrateWithWolverine(
                    integration => integration.MessageStorageSchemaName = "wolverine");
                options.Services.AddResourceSetupOnStartup();
                TcFlowMessagingConfiguration.Configure(options);
            })
            .StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task DurableInboxRejectsDuplicateEnvelopeBeforeASecondBusinessEffect()
    {
        var projectId = Guid.CreateVersion7();
        var command = new CreateProject(
            projectId,
            "Durable TCFlow",
            "owner-1",
            "correlation-1");

        var firstDelivery = await _host.SendMessageAndWaitAsync(command);
        var envelope = firstDelivery.Executed.SingleEnvelope<CreateProject>();

        await AssertSingleBusinessEffectAsync(projectId);

        var runtime = _host.GetRuntime();
        var handler = runtime.Handlers.ChainFor<CreateProject>();
        handler.ShouldNotBeNull();
        handler.IsTransactional.ShouldBeTrue();
        handler.Idempotency.ShouldBe(IdempotencyStyle.Eager);

        envelope.WasPersistedInInbox = false;
        envelope.Attempts = 0;

        await Should.ThrowAsync<DuplicateIncomingEnvelopeException>(
            () => runtime.Storage.Inbox.StoreIncomingAsync(envelope));
        await AssertSingleBusinessEffectAsync(projectId);
    }

    private async Task AssertSingleBusinessEffectAsync(Guid projectId)
    {
        var store = _host.Services.GetRequiredService<IDocumentStore>();
        await using var query = store.QuerySession();
        var events = await query.Events.FetchStreamAsync(projectId);
        var current = await query.LoadAsync<ProjectCurrent>(projectId);

        events.Count.ShouldBe(1);
        current.ShouldNotBeNull();
        current.Name.ShouldBe("Durable TCFlow");
        current.Version.ShouldBe(1);
    }
}
