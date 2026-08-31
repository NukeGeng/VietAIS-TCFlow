using JasperFx.Events.Projections;
using Marten;
using VietAIS.TCFlow.Modules.EventStorming.Domain;
using VietAIS.TCFlow.Modules.EventStorming.Projections;

namespace VietAIS.TCFlow.Modules.EventStorming.Configuration;

public static class StormingMartenConfiguration
{
    public static void Configure(StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Events.AddEventType<BoardCreated>();
        options.Events.AddEventType<StormingNodeAdded>();
        options.Events.AddEventType<StormingNodesConnected>();
        options.Events.AddEventType<StormingHotspotMarked>();
        options.Events.AddEventType<StormingNodeReordered>();
        options.Projections.Add<BoardCanvasProjection>(ProjectionLifecycle.Inline);
        options.Projections.Add<DomainEventCatalogProjection>(ProjectionLifecycle.Async);
    }
}
