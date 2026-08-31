using VietAIS.TCFlow.Modules.EventStorming.Contracts.Commands;
using VietAIS.TCFlow.Modules.EventStorming.Domain;

namespace VietAIS.TCFlow.Modules.EventStorming.Tests;

public sealed class StormingBoardTests
{
    [Fact]
    public void BoardPreservesNodesConnectionsAndOrder()
    {
        var board = new StormingBoard();
        var boardId = Guid.NewGuid();
        board.Apply(new BoardCreated(boardId, Guid.NewGuid(), "Checkout", "user", "c1", DateTimeOffset.UtcNow));
        var commandId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        board.Apply(board.AddNode(commandId, StormingNodeType.Command, "Submit order", null, "user", "c2", DateTimeOffset.UtcNow));
        board.Apply(board.AddNode(eventId, StormingNodeType.DomainEvent, "Order submitted", null, "user", "c3", DateTimeOffset.UtcNow));
        board.Apply(board.Connect(commandId, eventId, "causes", "user", "c4", DateTimeOffset.UtcNow));
        board.Apply(board.Reorder(eventId, 0, "user", "c5", DateTimeOffset.UtcNow));
        Should.Throw<InvalidOperationException>(() => board.Connect(commandId, eventId, "causes", "user", "c6", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void HotspotRequiresAnExistingNode()
    {
        var board = new StormingBoard();
        board.Apply(new BoardCreated(Guid.NewGuid(), Guid.NewGuid(), "Checkout", "user", "c1", DateTimeOffset.UtcNow));
        Should.Throw<KeyNotFoundException>(() => board.MarkHotspot(Guid.NewGuid(), "unknown", "user", "c2", DateTimeOffset.UtcNow));
    }
}
