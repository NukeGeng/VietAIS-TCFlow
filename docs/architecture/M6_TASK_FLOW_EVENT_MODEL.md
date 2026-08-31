# M6 TaskFlow event model

Status: `CONFIRMED` for the vNext reference slice; legacy v0.1 reconciliation
remains part of M13 cutover.

`EngineeringTask` is an event-sourced aggregate. `TaskProposed` starts the
stream, then lifecycle decisions append immutable events. Invalid transitions
throw before `AppendOne`, so no event is written. The lifecycle separates AI
verification from human review and completion requires both a passed AI check
and an approved human review.

The inline `TaskCurrent` projection powers immediate task reads. The async
`TaskBoard` and `TaskAnalytics` projections are reporting views and may lag;
they are rebuildable from the stream by the Marten async daemon.

Source proposals use `SourceChangeKey` as a reconciliation identity. The
handler queries the inline view before creating a stream and updates the
existing task when the same source change changes its title or description.
This keeps repeated analyzer deliveries on one task identity.

Every command carries actor/correlation/causation data, and handlers apply
that metadata to the Marten event transaction. Optimistic concurrency is
enforced through `ExpectedVersion` on lifecycle commands.
