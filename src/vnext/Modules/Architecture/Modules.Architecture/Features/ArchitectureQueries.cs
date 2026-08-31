using Marten;
using VietAIS.TCFlow.Modules.Architecture.Contracts.Queries;
using VietAIS.TCFlow.Modules.Architecture.Projections;

namespace VietAIS.TCFlow.Modules.Architecture.Features;

public static class ArchitectureQueries
{
    public static async Task<ArchitectureView?> Handle(GetArchitectureModel query, IQuerySession session, CancellationToken cancellationToken)
    {
        var model = await session.LoadAsync<ArchitectureCurrent>(query.ModelId, cancellationToken).ConfigureAwait(false);
        return model is null ? null : new(model.Id, model.ProjectId, model.Name, model.Version, model.Modules, model.ModuleRelationships, model.Entities, model.DataRelationships, model.Drifts, model.LastChangedAtUtc);
    }
}
