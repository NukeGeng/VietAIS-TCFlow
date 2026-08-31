using JasperFx.Events.Projections;
using Marten;
using VietAIS.TCFlow.Modules.Planning.Domain;
using VietAIS.TCFlow.Modules.Planning.Projections;

namespace VietAIS.TCFlow.Modules.Planning.Configuration;

public static class PlanningMartenConfiguration
{
    public static void Configure(StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.DatabaseSchemaName = "planning";
        options.Events.AddEventType<PlanCreated>();
        options.Events.AddEventType<PlanRenamed>();
        options.Events.AddEventType<RequirementAdded>();
        options.Events.AddEventType<MilestoneAdded>();
        options.Projections.Add<PlanCurrentProjection>(ProjectionLifecycle.Inline);
        options.Projections.Add<PlanningOverviewProjection>(ProjectionLifecycle.Async);
    }
}
