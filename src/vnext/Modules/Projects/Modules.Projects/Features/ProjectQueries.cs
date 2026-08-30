using Marten;
using VietAIS.TCFlow.Modules.Projects.Contracts.Queries;
using VietAIS.TCFlow.Modules.Projects.Projections;

namespace VietAIS.TCFlow.Modules.Projects.Features;

public static class ProjectQueries
{
    public static async Task<ProjectView?> Handle(
        GetProject query,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(session);

        var current = await session.LoadAsync<ProjectCurrent>(
            query.ProjectId,
            cancellationToken).ConfigureAwait(false);
        return current is null
            ? null
            : new ProjectView(
                current.Id,
                current.Name,
                current.OwnerId,
                current.IsSuspended,
                current.Version,
                current.LastChangedAtUtc);
    }
}
