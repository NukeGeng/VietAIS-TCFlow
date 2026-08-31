# M2 event-driven building blocks

Status: `CONFIRMED` for the vNext reference slice after the M2 verification
commands listed below. This document does not claim that every bounded context
has been migrated; the milestone plan remains the source of scope and status.

## Ownership

The vNext baseline keeps each concern in one place:

| Concern | Owner | Rule |
| --- | --- | --- |
| Command/query markers and application results | `BuildingBlocks/Application` | Commands mutate state through handlers; queries use read sessions and projections. |
| Clock and generated identifiers | `BuildingBlocks/Application` | Inject `IClock` and `IIdGenerator`; use UUIDv7 for new stream and envelope identities. |
| Domain events and aggregate behavior | Owning module `Domain` folder | Events are past-tense public contracts; aggregates own invariants and do not depend on ASP.NET, Marten, Wolverine, or another module. |
| Event metadata and Marten registration | `BuildingBlocks/EventSourcing` | Every write applies actor, correlation, causation, project/tenant scope, and source metadata before appending. |
| Projection administration | `BuildingBlocks/EventSourcing` | Rebuilds use a strict allowlist and timeout; status reports expose shard sequence, high-water mark, lag, agent status, and heartbeat. |
| Message dispatch and durability | `BuildingBlocks/Messaging` | Wolverine owns handler transactions, durable local queues, inbox/outbox policy, retry policy, and envelope identity. |
| Authorization and validation conventions | existing `BuildingBlocks/Shared/Identity` and FluentValidation conventions | Permission metadata remains the authorization contract; validation is a handler boundary concern and must not be replaced by frontend checks. |

The historical FullStackHero `BuildingBlocks/Eventing` EF/RabbitMQ abstraction
is retained for the v0.1 compatibility surface. New vNext business handlers
must use Wolverine and Marten according to this document; the two mechanisms
must not be silently mixed in one feature.

## Transaction boundary

For a Wolverine handler that receives `IDocumentSession`, Wolverine's Marten
transaction middleware owns `SaveChangesAsync()`. The handler appends events
and returns its result; it must not commit a second transaction. Standalone
scripts, importers, and tests that open a session directly remain responsible
for an explicit `SaveChangesAsync()`.

The durable inbox and business event append are configured to share the Marten
transaction. Durable outgoing messages use Wolverine's outbox. Retry policy is
bounded and applies to transient `TimeoutException` failures. A redelivery is
identified by the persisted Wolverine envelope id; it is rejected before a
second business effect is executed.

## Projection consistency

`ProjectCurrent` is the immediate inline read model used for current state and
command feedback. `ProjectPortfolioSummary` is an asynchronous daemon-owned
read model for reporting and cross-stream views. Both have stable administrative
names, are derived only from event history, and can be rebuilt after their
documents are removed. No projection rebuild is allowed to mutate event history.

Administrative rebuild APIs are intentionally not exposed as unauthenticated
HTTP routes. A future operations endpoint must enforce a platform permission,
record an audit event, and call `IProjectionAdministration` rather than the
Marten daemon directly.

## Verification evidence

From `src/vnext`:

```bash
dotnet restore FSH.Starter.slnx
dotnet build FSH.Starter.slnx --no-restore
dotnet test Tests/EventSourcing.Tests/EventSourcing.Tests.csproj --no-build
```

The integration tests use a PostgreSQL 17 Testcontainer and verify:

- event append/reload and diagnostic metadata;
- expected-version optimistic concurrency;
- inline and async projection rebuild from empty documents and convergence;
- projection-admin allowlisting and status reporting; and
- Wolverine handler transaction/idempotency configuration plus durable-inbox
  duplicate detection without a second project event.

The final M3 and later milestones must add authorization/audit parity and
bounded-context-specific replay evidence before this architecture is treated
as the complete GOAL2 implementation.
