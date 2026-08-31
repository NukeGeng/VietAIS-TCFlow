using JasperFx.Events.Projections;
using Marten;
using VietAIS.TCFlow.Modules.Projects.Domain;
using VietAIS.TCFlow.Modules.Projects.Projections;

namespace VietAIS.TCFlow.Modules.Projects.Configuration;

public static class ProjectsMartenConfiguration
{
    public static void Configure(StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Events.AddEventType<ProjectCreated>();
        options.Events.AddEventType<ProjectRenamed>();
        options.Events.AddEventType<ProjectSuspended>();
        options.Events.AddEventType<ProjectActivated>();
        options.Projections.Add<ProjectCurrentProjection>(ProjectionLifecycle.Inline);
        options.Projections.Add<ProjectPortfolioSummaryProjection>(ProjectionLifecycle.Async);
    }
}
