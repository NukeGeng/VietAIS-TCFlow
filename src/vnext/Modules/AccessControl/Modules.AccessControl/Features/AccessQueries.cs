using Marten;
using VietAIS.TCFlow.Modules.AccessControl.Authorization;
using VietAIS.TCFlow.Modules.AccessControl.Contracts.Models;
using VietAIS.TCFlow.Modules.AccessControl.Contracts.Queries;
using VietAIS.TCFlow.Modules.AccessControl.Projections;

namespace VietAIS.TCFlow.Modules.AccessControl.Features;

public static class AccessQueries
{
    public static async Task<EffectiveProjectPermissions> Handle(
        GetEffectiveProjectPermissions query,
        IProjectPermissionEvaluator evaluator,
        CancellationToken cancellationToken) =>
        await evaluator.GetEffectivePermissionsAsync(
                query.UserId,
                query.ProjectId,
                query.RepositoryId,
                query.Component,
                cancellationToken)
            .ConfigureAwait(false);

    public static async Task<ProjectAccessCurrent?> GetCurrentAsync(
        Guid projectId,
        IQuerySession session,
        CancellationToken cancellationToken) =>
        await session.Query<ProjectAccessCurrent>()
            .SingleOrDefaultAsync(item => item.ProjectId == projectId, cancellationToken)
            .ConfigureAwait(false);
}
