using Marten;
using VietAIS.TCFlow.Modules.AccessControl.Authorization;
using VietAIS.TCFlow.Modules.Projects.Projections;

namespace VietAIS.TCFlow.Api;

#pragma warning disable CA1812 // Constructed by the ASP.NET Core DI container.
internal sealed class MartenProjectOwnerReader(IQuerySession session) : IProjectOwnerReader
{
    public async Task<string?> GetOwnerIdAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await session.LoadAsync<ProjectCurrent>(projectId, cancellationToken).ConfigureAwait(false))?.OwnerId;
}
#pragma warning restore CA1812
