using Marten;

namespace VietAIS.TCFlow.BuildingBlocks.EventSourcing.Configuration;

public static class TcFlowEventStoreConfiguration
{
    public static void Configure(StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Events.MetadataConfig.EnableAll();
        options.Events.EnableStrictStreamIdentityEnforcement = true;
        options.Projections.MaxConcurrentRebuildsPerDatabase = 4;
    }
}
