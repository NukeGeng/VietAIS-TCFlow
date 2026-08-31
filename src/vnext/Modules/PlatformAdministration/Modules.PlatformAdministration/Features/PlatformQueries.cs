using Marten;
using VietAIS.TCFlow.Modules.PlatformAdministration.Contracts.Queries;
using VietAIS.TCFlow.Modules.PlatformAdministration.Projections;

namespace VietAIS.TCFlow.Modules.PlatformAdministration.Features;

public static class PlatformQueries
{
    public static async Task<PlatformPolicyView?> Handle(GetPlatformPolicy query, IQuerySession session, CancellationToken cancellationToken)
    {
        var policy = await session.LoadAsync<PlatformPolicyCurrent>(query.PolicyId, cancellationToken).ConfigureAwait(false);
        return policy is null
            ? null
            : new(
                policy.Id,
                policy.AllowAiAnalysis,
                policy.AllowAiTaskSuggestions,
                policy.AllowAiTaskMutations,
                policy.ProviderName,
                policy.ProviderEnabled,
                policy.Version,
                policy.LastChangedAtUtc,
                policy.ProjectCreationEnabled,
                policy.RepositoryConnectionsEnabled,
                policy.MaximumRepositoriesPerProject);
    }

    public static async Task<GlobalAiProviderView?> Handle(GetGlobalAiProvider query, IQuerySession session, CancellationToken cancellationToken)
    {
        var provider = await session.LoadAsync<GlobalAiProviderCurrent>(query.ProviderId, cancellationToken).ConfigureAwait(false);
        return provider is null
            ? null
            : new(provider.Id, provider.Kind, provider.DisplayName, provider.IsEnabled, provider.UpdatedAtUtc, provider.UpdatedBy);
    }

    public static async Task<GlobalSystemSettingsView?> Handle(GetGlobalSystemSettings query, IQuerySession session, CancellationToken cancellationToken)
    {
        var settings = await session.LoadAsync<GlobalSystemSettingsCurrent>(query.SettingsId, cancellationToken).ConfigureAwait(false);
        return settings is null
            ? null
            : new(settings.Id, settings.PlatformName, settings.DefaultTimeZone, settings.SupportUrl, settings.UpdatedAtUtc, settings.UpdatedBy);
    }
}
