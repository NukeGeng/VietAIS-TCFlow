# M7 EventStorming event model

Status: `CONFIRMED` for the vNext board/reference slice.

`StormingBoard` owns ordered nodes and connections. Board edits are immutable
events (`StormingNodeAdded`, `StormingNodesConnected`, `StormingHotspotMarked`,
and `StormingNodeReordered`) with expected-version optimistic concurrency.
The inline `BoardCanvas` projection preserves the interactive canvas, while
the async `DomainEventCatalog` projection supplies a rebuildable catalog for
cross-board queries. Node identities remain stable when their visual order
changes.
