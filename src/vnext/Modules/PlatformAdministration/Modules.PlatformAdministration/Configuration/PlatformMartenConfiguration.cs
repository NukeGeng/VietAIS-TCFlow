using JasperFx.Events.Projections;
using Marten;
using VietAIS.TCFlow.Modules.PlatformAdministration.Domain;
using VietAIS.TCFlow.Modules.PlatformAdministration.Projections;

namespace VietAIS.TCFlow.Modules.PlatformAdministration.Configuration;

public static class PlatformMartenConfiguration
{
    public static void Configure(StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Events.AddEventType<PlatformPolicyCreated>();
        options.Events.AddEventType<PlatformPolicyUpdated>();
        options.Events.AddEventType<AiProviderConfigured>();
        options.Events.AddEventType<PlatformAdminActionAudited>();
        options.Projections.Add<PlatformPolicyCurrentProjection>(ProjectionLifecycle.Inline);
    }
}
