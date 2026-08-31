using Marten;
using VietAIS.TCFlow.Modules.EventStorming.Contracts.Queries;
using VietAIS.TCFlow.Modules.EventStorming.Projections;

namespace VietAIS.TCFlow.Modules.EventStorming.Features;

public static class StormingQueries
{
    public static async Task<BoardView?> Handle(GetBoard query, IQuerySession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var board = await session.LoadAsync<BoardCanvas>(query.BoardId, cancellationToken).ConfigureAwait(false);
        return board is null ? null : new(board.Id, board.ProjectId, board.Name, board.Version, board.Nodes.OrderBy(x => x.Position).ToArray(), board.Connections.ToArray(), board.LastChangedAtUtc);
    }
}
