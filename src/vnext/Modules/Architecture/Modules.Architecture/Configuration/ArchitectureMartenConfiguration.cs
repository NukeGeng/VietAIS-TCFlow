using JasperFx.Events.Projections;
using Marten;
using VietAIS.TCFlow.Modules.Architecture.Domain;
using VietAIS.TCFlow.Modules.Architecture.Projections;

namespace VietAIS.TCFlow.Modules.Architecture.Configuration;

public static class ArchitectureMartenConfiguration
{
    public static void Configure(StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Events.AddEventType<ArchitectureModelCreated>();
        options.Events.AddEventType<ArchitectureModuleAdded>();
        options.Events.AddEventType<ArchitectureModulesConnected>();
        options.Events.AddEventType<ArchitectureEntityAdded>();
        options.Events.AddEventType<ArchitectureDataRelationshipAdded>();
        options.Events.AddEventType<ArchitectureDriftRecorded>();
        options.Projections.Add<ArchitectureCurrentProjection>(ProjectionLifecycle.Inline);
        options.Projections.Add<ArchitectureOverviewProjection>(ProjectionLifecycle.Async);
    }
}
