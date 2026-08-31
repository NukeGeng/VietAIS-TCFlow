using Marten;
using VietAIS.TCFlow.Modules.Planning.Contracts.Queries;
using VietAIS.TCFlow.Modules.Planning.Projections;

namespace VietAIS.TCFlow.Modules.Planning.Features;

public static class PlanningQueries
{
    public static async Task<PlanView?> Handle(
        GetPlan query,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(session);
        var plan = await session.LoadAsync<PlanCurrent>(query.PlanId, cancellationToken).ConfigureAwait(false);
        return plan is null
            ? null
            : new PlanView(
                plan.Id,
                plan.ProjectId,
                plan.Name,
                plan.Purpose,
                plan.Requirements,
                plan.Milestones,
                plan.Version,
                plan.LastChangedAtUtc);
    }
}
