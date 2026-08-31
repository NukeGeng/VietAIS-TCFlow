# GOAL2 Migration Matrix

Status: `PROPOSED`. This inventory records the intended disposition of the
v0.1 source tree. A classification becomes `CONFIRMED` only after the owning
milestone verifies behavior and the final diff.

Definitions:

- `KEEP`: retain as-is except compatible maintenance.
- `PORT`: preserve behavior/contracts while adapting to the clean vNext baseline.
- `REWRITE`: replace implementation because the target architecture requires a
  different ownership or persistence model.
- `REMOVE`: delete only after dependency, behavior, and migration evidence
  proves it is obsolete.

| Current area | Classification | Target owner | Rationale and required evidence |
| --- | --- | --- | --- |
| `GOAL.md` and P14 acceptance evidence | KEEP | Documentation | Historical v0.1 product and verification record; add status labels, never rewrite results as GOAL2 proof. |
| `src/api/framework` FullStackHero 2.0.4 infrastructure | PORT | M1 backend baseline | Re-establish required identity/authorization/infrastructure behavior on a clean FullStackHero v10 baseline; compare public contracts and security behavior. |
| `src/api/server` composition root | REWRITE | Host / M1–M2 | Compose clean v10 modules, Marten Event Store, Wolverine, async daemon, health, and observability. |
| `src/api/modules/RepositoryIntelligence/Management` | REWRITE + PORT behavior | Projects, AccessControl, TaskFlow, Architecture | Split mixed document-centric responsibilities into owning contexts; retain validated routes/permissions only through explicit contract decisions and migration tests. |
| `src/api/modules/RepositoryIntelligence/GitHub` | PORT | Integrations + RepositoryIntelligence | Preserve signed webhook, installation scope, immutable revision fetch, idempotency, and audit; separate provider infrastructure from analysis work. |
| Repository Intelligence Marten business documents | REWRITE selectively | Owning contexts | Classify each as Event Store, Projection, or Operational Document; migrate business histories and retain operational records only when justified. |
| FullStackHero sample `Catalog` and `Todo` modules | REMOVE candidate | M1 baseline | Do not carry demo modules into the product unless a runtime dependency is proven. Removal requires build, route, migration, and UI-reference checks. |
| `src/analyzers/core`, `vue`, `aspnet`, `marten`, `contracts` | PORT | RepositoryIntelligence | Preserve deterministic analysis and evidence boundaries; update framework syntax and add event/aggregate/projection/message detection. |
| `src/analyzers/knowledge`, `governance`, `reasoning`, `monitoring` | PORT + selective REWRITE | RepositoryIntelligence | Preserve precision/reconciliation behavior; move query-oriented graph/search views to rebuildable async projections and durable processing where valuable. |
| Analyzer fixtures and stable-identity contracts | KEEP + EXTEND | RepositoryIntelligence tests | Existing fixtures are regression baselines; add .NET 10/FSH v10, domain-event, projection, Wolverine, and RabbitMQ fixtures. |
| `src/tests/RepositoryIntelligence.IntegrationTests` | PORT + SPLIT | Context integration suites | Keep behavioral assertions; reorganize by bounded context and add stream, projection, rebuild, concurrency, inbox/outbox, broker, and architecture tests. |
| `src/apps/vue` | PORT + REORGANIZE | Frontend | Retain working flows, default Vietnamese/English switch, and permission UX; organize routes/stores/clients by bounded context and update contracts incrementally. |
| `src/aspire` | REWRITE | Host / M1, M10, M13 | Move to the target Aspire baseline and add async daemon/durable storage/RabbitMQ topology, health, secrets, and observability. |
| `src/Shared` | REVIEW then PORT/REMOVE | BuildingBlocks | Keep only true stable cross-cutting contracts; move domain-specific types into their owning bounded contexts. Architecture tests must prevent a new dumping ground. |
| EF/identity migrations from FullStackHero | PORT | PlatformAdministration / AccessControl | Follow v10 upstream ownership and provide explicit user/tenant/role data migration; do not mix it silently with domain event streams. |
| `deploy/self-host` | REWRITE | M13 operations | Keep v0.1 instructions versioned until the vNext stack proves PostgreSQL, RabbitMQ, daemon, API, Vue, backup/restore, replay/rebuild, and upgrade behavior. |
| `licenses/fullstackhero-2.0.4-rc` | KEEP | Legal | Preserve attribution for distributed historical source; add v10 notices according to its actual license when imported. |

## Per-model inventory requirement

Before migrating a bounded context, expand its row into a model-level table:

| Model | Current store | Target category | Stream/projection name | Migration rule | Reconciliation evidence |
| --- | --- | --- | --- | --- | --- |
| GitHub installation and delivery metadata | v0.1 integration documents | Operational Document | `GitHubOperationalMigrationDocument` in Integrations | Retain only the explicit non-secret metadata whitelist; reject secret-bearing properties, require source/project identity, and make retries idempotent | Source-reference/kind/hash reconciliation plus redaction and idempotency tests |
| Global AI provider configuration | v0.1 system configuration document | Event Store | `GlobalAiProvider` + inline `GlobalAiProviderCurrent` | Keep provider identity, display name, enabled state, updater, and timestamp in a system-scoped stream; never attach it to a project stream | Typed mapper, stream marker/hash, projection readback, and repeat-apply test |
| Global system settings | v0.1 system configuration document | Event Store | `GlobalSystemSettings` + inline `GlobalSystemSettingsCurrent` | Keep platform name, timezone, support URL, updater, and timestamp in a system-scoped stream | Typed mapper, URI validation, projection readback, and repeat-apply test |
| Platform policy | v0.1 system configuration document | Event Store | `PlatformPolicy` + inline `PlatformPolicyCurrent` | Preserve project/repository platform limits while retaining the vNext AI policy fields; reject invalid repository limits | Typed import event, invariant validation, projection readback, and repeat-apply test |
| EventStorming board and child records | v0.1 board documents | Event Store | `StormingBoard` + inline board canvas | Require explicit board/node source ids; append typed board events in deterministic order | Board/node/link/hotspot/order counts and replay comparison |
| Architecture model and child records | v0.1 architecture documents | Event Store | `ArchitectureModel` + inline architecture view | Require explicit model/module/entity source ids; append typed model events in deterministic order | Model/module/entity/relationship/drift counts and replay comparison |

No legacy table/document may be deleted until its target row, identity mapping,
dry run, pre/post counts, invariants, and rollback path are verified.

The first model-level apply slices are now implemented for `Project`,
`ProjectState`, `ProjectRole`, `ProjectMembership`, `Plan`, `Requirement`,
`Milestone`, `EngineeringTask`, `TaskVersion`, `TaskEvidence`, `AnalysisRun`,
`SourceArtifact`, `SourceImpact`, EventStorming board records, Architecture
model records, `GlobalAiProviderConfiguration`, `GlobalSystemSettings`, and
`PlatformPolicy`: typed project, access, planning, task, repository-analysis,
board, architecture, and platform-administration events are appended to
deterministic streams, with
source-reference/hash markers and inline read models updated in the same
Marten transaction. Task snapshots use `TaskProposed` plus
`TaskLifecycleReconciled` so migration does not invent transition history;
task history, repository artifacts/impacts, board changes, and architecture
facts remain typed, replayable events.
This confirms only these model-level mapper/writers; it does not confirm the
full matrix or authorize deletion of any v0.1 document.

The first executable inventory/planning slice is
`src/vnext/Tools/Goal2Migration`. It supports a deterministic dry run and a
versioned operational ledger (`--ledger --apply`). The ledger records source
hashes and target identities so a cutover can be resumed without duplicating a
source record. It is not the business Event Store writer: each model-level row
must still document the source export field mapping, target event
payload/upcaster, pre/post count, invariant checks, and rollback record before
that bounded context is allowed to append to the Event Store. The tool's
`--apply-marten --connection` mode is the approved Projects, AccessControl,
Planning, TaskFlow, RepositoryIntelligence, EventStorming, Architecture,
PlatformAdministration, and Integrations exception to this statement.
Integrations writes only whitelisted
operational metadata and rejects secret-bearing properties; it remains
fail-closed for all other bounded-context kinds until their typed mappers and
reconciliation evidence are added.

The same tool provides a read-only Marten marker/hash check via
`--reconcile-marten --connection`. It verifies expected source references and
payload hashes in initialized target event streams without provisioning schema
or writing business state. A successful check is direct evidence for
stream-level idempotency only; it does not replace semantic invariant counts,
operational-document reconciliation, backup/restore, or rollback rehearsal.

## Contract migration rule

For every HTTP route, permission code, domain event, integration message,
configuration key, projection schema, and analyzer fixture:

1. Identify producer and all consumers.
2. Decide retain, version, adapt, or retire.
3. Add compatibility or cutover tests.
4. Record data/event upcasting when history is affected.
5. Remove the legacy contract only after runtime evidence shows no consumer
   depends on it.

## Migration completion evidence

- Clean vNext build and architecture tests.
- Aggregate reconstruction and expected-version concurrency tests.
- Inline and async projection rebuild from the migrated Event Store.
- Durable message and webhook redelivery idempotency.
- Permission/audit parity with v0.1 retained behavior.
- Pre/post migration counts plus domain-invariant reconciliation.
- Backup restore and rollback rehearsal in an isolated environment.
