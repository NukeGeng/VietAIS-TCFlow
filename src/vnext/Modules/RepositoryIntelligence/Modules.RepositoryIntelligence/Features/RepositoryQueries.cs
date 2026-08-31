using Marten;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Queries;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Projections;

namespace VietAIS.TCFlow.Modules.RepositoryIntelligence.Features;

public static class RepositoryQueries
{
    public static async Task<AnalysisView?> Handle(GetAnalysis query, IQuerySession session, CancellationToken cancellationToken)
    {
        var current = await session.LoadAsync<AnalysisCurrent>(query.AnalysisRunId, cancellationToken).ConfigureAwait(false);
        return current is null ? null : new(current.Id, current.ProjectId, current.RepositoryId, current.CommitSha, current.Completed, current.Version, current.Artifacts, current.Changes, current.Evidence, current.LastChangedAtUtc);
    }
}
