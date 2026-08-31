using Marten;
using VietAIS.TCFlow.Modules.PlatformAdministration.Contracts.Queries;
using VietAIS.TCFlow.Modules.PlatformAdministration.Projections;

namespace VietAIS.TCFlow.Modules.PlatformAdministration.Features;

public static class PlatformQueries
{
    public static async Task<PlatformPolicyView?> Handle(GetPlatformPolicy query, IQuerySession session, CancellationToken cancellationToken)
    {
        var policy = await session.LoadAsync<PlatformPolicyCurrent>(query.PolicyId, cancellationToken).ConfigureAwait(false);
        return policy is null ? null : new(policy.Id, policy.AllowAiAnalysis, policy.AllowAiTaskSuggestions, policy.AllowAiTaskMutations, policy.ProviderName, policy.ProviderEnabled, policy.Version, policy.LastChangedAtUtc);
    }
}
