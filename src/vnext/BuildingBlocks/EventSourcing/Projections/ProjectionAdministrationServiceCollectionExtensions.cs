using Microsoft.Extensions.DependencyInjection;

namespace VietAIS.TCFlow.BuildingBlocks.EventSourcing.Projections;

public static class ProjectionAdministrationServiceCollectionExtensions
{
    public static IServiceCollection AddTcFlowProjectionAdministration(
        this IServiceCollection services,
        Action<ProjectionAdministrationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddSingleton<IProjectionAdministration, MartenProjectionAdministration>();
        return services;
    }
}
