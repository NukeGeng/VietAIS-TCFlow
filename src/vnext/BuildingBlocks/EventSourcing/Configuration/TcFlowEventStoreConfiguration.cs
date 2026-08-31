using Marten;

namespace VietAIS.TCFlow.BuildingBlocks.EventSourcing.Configuration;

public static class TcFlowEventStoreConfiguration
{
    public static void Configure(StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // All bounded contexts share one Marten store in the modular monolith;
        // module configuration must not overwrite this global schema.
        options.DatabaseSchemaName = "tcflow";
        options.Events.MetadataConfig.EnableAll();
        options.Events.EnableStrictStreamIdentityEnforcement = true;
        options.Projections.MaxConcurrentRebuildsPerDatabase = 4;
    }
}
