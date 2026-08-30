# GOAL2.md

# VietAIS TCFlow — Architecture & Product Evolution Specification

## 1. Purpose

`GOAL2.md` is the architectural and product evolution specification for **VietAIS TCFlow**.

This document extends the original `GOAL.md`.

The original goals remain valuable as historical product context, especially around:

- Source-aware engineering planning.
- Repository intelligence.
- Source change → impact → engineering task.
- Explainability.
- Task reconciliation.
- Permission-aware automation.
- GitHub integration.
- AI/Codex reasoning.
- Vue + ASP.NET + Marten foundation.

`GOAL2.md` upgrades that foundation into a broader product and architecture:

> **VietAIS TCFlow becomes an Engineering System Intelligence Platform that manages planning, Event Storming, architecture, data models, modules, tasks, repository intelligence, source evolution, documentation, and AI-assisted engineering workflows as one connected living system.**

The new implementation direction is based on:

```text
.NET 10
FullStackHero v10
ASP.NET Core
Vue 3
Marten Event Store
Wolverine
CQRS
DDD
Event Sourcing
Inline Projections
Async Projections
RabbitMQ
.NET Aspire
```

---

# 2. Product Evolution

## 2.1. Previous Product Definition

The initial product was primarily defined as:

```text
Source Change
    ↓
Impact Analysis
    ↓
Engineering Plan
    ↓
Task Generation
    ↓
Task Reconciliation
```

This remains an important capability.

However, it is no longer the full product definition.

---

# 3. New Product Definition

VietAIS TCFlow is now defined as:

> **An Engineering System Intelligence and Living Architecture Platform that connects planning, domain design, Event Storming, architecture diagrams, ERD/data models, source code, repository analysis, engineering tasks, implementation status, and AI reasoning into one continuously evolving system graph.**

The product must help teams answer:

```text
What are we planning?
What domains exist?
What business events exist?
How do modules relate?
How does data relate?
What does the source actually implement?
What changed?
What is affected?
What work remains?
Does documentation still match reality?
```

---

# 4. Core Product Pillars

TCFlow is organized around five major pillars.

```text
                         VietAIS TCFlow
                               │
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
        ▼                      ▼                      ▼
     PLANNING             SYSTEM DESIGN          EXECUTION
        │                      │                      │
        ▼                      ▼                      ▼
  Plans / Roadmaps       Event Storming            Tasks
  Requirements           Architecture              Review
  Features               ERD / Data Model          PR Flow
  Milestones             Module Map                Progress
        │                      │                      │
        └──────────────────────┼──────────────────────┘
                               ▼
                     REPOSITORY INTELLIGENCE
                               │
                               ▼
                        SOURCE CODE / GIT
                               │
                               ▼
                          AI REASONING
```

---

# 5. Living Architecture

A central principle of TCFlow is:

> **Architecture documentation must evolve together with the source code.**

Traditional architecture documents are often static:

```text
README
architecture.md
ERD image
Event Storming board
Jira
GitHub
Confluence
```

These artifacts quickly become outdated.

TCFlow must instead create a connected graph.

Example:

```text
Feature
   │
   ├── Requirement
   ├── Event Storming
   │      ├── Command
   │      ├── Domain Event
   │      ├── Aggregate
   │      └── Policy
   │
   ├── Architecture Module
   ├── Data Model
   ├── API Contract
   ├── Source Artifact
   ├── Engineering Task
   └── Pull Request
```

This graph becomes the living engineering model of the system.

---

# 6. Documentation ↔ Source Synchronization

TCFlow must support both directions.

## 6.1. Documentation → Source

Example:

```text
Event Storming:
ProjectArchived
```

The system can trace or verify:

```text
Modules/Projects/Domain/Events/ProjectArchived.cs
```

## 6.2. Source → Documentation

Example source change:

```csharp
public record ProjectSuspended(...);
```

TCFlow detects:

```text
New Domain Event:
ProjectSuspended

Aggregate:
Project

Module:
Projects

Documentation:
Missing
```

The system may propose:

```text
Update Event Storming
Update Project lifecycle
Update module documentation
Update affected projections
```

---

# 7. Updated Technology Baseline

## Backend

```text
.NET 10
ASP.NET Core
FullStackHero v10
```

FullStackHero v10 is treated as the new clean architectural baseline.

The migration should not be implemented as a simple package-version upgrade from the previous FullStackHero 2.0.4-rc structure.

Preferred strategy:

```text
FullStackHero v10 clean baseline
        ↓
Port TCFlow bounded contexts
        ↓
Rebuild persistence around Marten + Wolverine
        ↓
Preserve business behavior and validated features
```

---

# 8. Architectural Style

The backend architecture is:

```text
Modular Monolith
+
Domain-Driven Design
+
Vertical Slice Architecture
+
CQRS
+
Event Sourcing
```

Modules are organized by bounded context first.

Technical layers exist inside modules where useful.

---

# 9. Command Architecture

The write-side lifecycle is:

```text
CLIENT / API
     │
     ▼
Command / Message
     │
     ▼
Wolverine Handler / Decider
     │
     ▼
Application Use Case
     │
     ▼
Event-Sourced Aggregate
     │
     ├── Business Rules
     ├── Invariants
     └── Behavior
     │
     ▼
Domain Events
     │
     ▼
MARTEN EVENT STORE
```

The aggregate state is reconstructed from domain events.

---

# 10. Decider Rule

TCFlow should follow a decider-oriented event sourcing style.

Preferred flow:

```text
Command
   ↓
Current Aggregate State
   ↓
DECIDE
   ↓
Domain Events
   ↓
APPLY
   ↓
New Aggregate State
```

Handlers should prefer returning events instead of mutating fetched aggregate state directly.

Conceptually:

```csharp
public static TaskAssigned Handle(
    AssignTask command,
    EngineeringTask task)
{
    task.EnsureCanAssign(command.AssigneeId);

    return new TaskAssigned(
        command.AssigneeId);
}
```

The event is the business decision.

State changes through event application.

---

# 11. Query Architecture

CQRS does not mean unnecessary abstraction.

Simple queries may use:

```text
HTTP GET
   ↓
IQuerySession
   ↓
Read Model / Projection
   ↓
Response
```

Queries should read projections, not reconstruct aggregates unless there is a strong domain reason.

---

# 12. Event Store

Marten Event Store is the business-history source of truth for event-sourced aggregates.

Business truth should be represented as domain events.

Examples:

```text
ProjectCreated
ProjectOwnershipTransferred
TaskAssigned
TaskStarted
TaskCompleted
RolePermissionGranted
RepositoryConnected
```

---

# 13. Three Persistence Categories

Not everything should become event sourced.

TCFlow explicitly separates:

```text
1. EVENT STORE
   Business truth

2. PROJECTIONS
   Query/read models

3. OPERATIONAL DOCUMENTS
   Infrastructure/runtime state
```

---

# 14. Event Store Candidates

Strong candidates for Event Sourcing:

```text
Project
ProjectRole
ProjectMembership
Repository
Plan
Milestone
Feature
StormingBoard
ArchitectureModel
EngineeringTask
AuthorityPolicy
ConventionProfile
```

The exact aggregate boundary must follow business invariants.

---

# 15. Data That Should Usually Not Be Event Sourced

Operational/infrastructure state should usually remain documents or framework-managed state.

Examples:

```text
GitHubWebhookDelivery
Wolverine Inbox
Wolverine Outbox
Codex process state
Temporary analysis work item
Projection checkpoints
Async daemon state
Cache
Authentication token state
Retry state
```

Event sourcing these items would add complexity without corresponding business value.

---

# 16. Inline Projections

Inline projections run in the same transaction as event append.

Use Inline projections for:

```text
Critical current state
Immediate query requirements
Authorization-related current state
Command-followed-by-query behavior
Operational read models requiring strong consistency
```

Examples:

```text
ProjectCurrent
TaskCurrent
RepositoryCurrent
MembershipCurrent
RoleCurrent
```

Flow:

```text
Domain Event
    ↓
Marten Event Store
    ↓ same transaction
Inline Projection
    ↓
Critical Read Model
```

---

# 17. Async Projections

Async projections are processed through Marten Async Daemon.

Use them for:

```text
Dashboard
Search
Analytics
Cross-stream Views
Knowledge Graph
Impact Graph
Reporting
Historical Metrics
Project Progress
Engineering Analytics
Architecture Overview
```

Flow:

```text
Domain Events
    ↓
Marten Event Store
    ↓ after commit
Async Daemon
    ↓
Async Read Models
```

Eventual consistency is acceptable for these views.

---

# 18. Projection Organization Rule

Do not organize projections mainly as:

```text
Inline/
Async/
```

Instead organize by read model:

```text
TaskCurrent/
TaskBoard/
TaskAnalytics/
```

Projection configuration decides whether the projection is:

```text
Inline
Async
```

This keeps business intent independent from infrastructure mode.

---

# 19. Messaging Architecture

Domain Events and Integration Events are different concepts.

```text
Domain Event
```

represents business truth inside TCFlow.

```text
Integration Event
```

is a message intended for another module/system boundary.

---

# 20. Wolverine Outbox

When an internal domain event needs to produce an external integration event:

```text
Domain Event
    ↓
Integration Event
    ↓
Wolverine Durable Outbox
    ↓ after successful transaction
RabbitMQ
    ↓
External System
```

The event store transaction and outgoing message must remain coordinated.

---

# 21. RabbitMQ Boundary

RabbitMQ is for external/system integration.

RabbitMQ is not required for Marten Async Projections.

Correct separation:

```text
Domain Events
   ├── Marten Async Daemon
   │      ↓
   │ Internal Read Models
   │
   └── Wolverine Outbox
          ↓
       RabbitMQ
          ↓
      External Systems
```

---

# 22. Wolverine Replaces Custom Queue Infrastructure Where Appropriate

The previous TCFlow implementation contains custom hosted workers and queue-like persistence for repository analysis and AI reasoning.

The restructured architecture should evaluate which workflows can become durable Wolverine messages.

Example:

```text
GitHubWebhookReceived
        ↓
Wolverine
        ↓
AnalyzeRepositoryChange
        ↓
SourceChangeDetected
        ↓
RunDeepReasoning
        ↓
ImpactDetected
        ↓
ReconcileEngineeringPlan
```

Do not keep custom polling workers where Wolverine durability already solves the same problem.

---

# 23. Updated Module Structure

Top-level backend layout:

```text
src/

├── BuildingBlocks/
├── Modules/
├── Analyzers/
├── Host/
├── Apps/
└── Tests/
```

---

# 24. BuildingBlocks

`BuildingBlocks` contains only truly reusable cross-module infrastructure.

```text
BuildingBlocks/

├── Application/
├── Authorization/
├── EventSourcing/
├── Messaging/
├── Observability/
└── Persistence/
```

Examples:

```text
Application/
    Result
    Pagination

Authorization/
    Permission
    CurrentUser

EventSourcing/
    Event metadata
    Aggregate helpers
    Stream conventions

Messaging/
    Wolverine configuration
    Integration event conventions

Persistence/
    Marten extensions

Observability/
    Correlation ID
    OpenTelemetry
```

Forbidden:

```text
BuildingBlocks/
    TaskHelper
    ProjectService
    RepositoryUtility
```

Business logic belongs to the owning module.

---

# 25. Business Modules

```text
Modules/

├── PlatformAdministration/
├── Projects/
├── AccessControl/
├── Planning/
├── EventStorming/
├── Architecture/
├── TaskFlow/
├── RepositoryIntelligence/
└── Integrations/
```

---

# 26. PlatformAdministration Module

Owns platform-level administration.

Responsibilities:

```text
System Admin
Global Settings
Global AI Provider Configuration
Platform Policy
Global Audit Views
System Project Overview
```

This module is system scoped.

---

# 27. Projects Module

Owns project lifecycle.

Aggregate candidates:

```text
Project
```

Events may include:

```text
ProjectCreated
ProjectRenamed
ProjectDescriptionChanged
ProjectOwnershipTransferred
ProjectActivated
ProjectArchived
```

Inline read models:

```text
ProjectCurrent
```

Async views:

```text
ProjectSummary
SystemProjectOverview
```

---

# 28. AccessControl Module

Owns project-level authorization.

Aggregate candidates:

```text
ProjectRole
ProjectMembership
```

Possible events:

```text
ProjectRoleCreated
ProjectRoleRenamed
PermissionGranted
PermissionRevoked

MemberInvited
MemberJoined
RoleAssigned
RoleRemoved
ComponentAccessGranted
ComponentAccessRevoked
MemberRemoved
```

System Admin and Project Owner remain distinct concepts.

---

# 29. Planning Module

Owns engineering planning.

Domain concepts:

```text
Plan
Roadmap
Milestone
Requirement
Feature
Priority
Dependency
```

Example structure:

```text
Planning/

├── Domain/
│   ├── Plans/
│   ├── Roadmaps/
│   ├── Milestones/
│   ├── Requirements/
│   └── Features/
│
├── Features/
│   ├── CreatePlan/
│   ├── AddMilestone/
│   ├── AddRequirement/
│   ├── PrioritizeFeature/
│   └── LinkDependency/
│
└── Projections/
    ├── RoadmapView/
    ├── MilestoneProgress/
    └── PlanningOverview/
```

---

# 30. EventStorming Module

Owns interactive Event Storming models.

Core node types:

```text
Command
DomainEvent
Aggregate
Actor
Policy
ReadModel
ExternalSystem
Hotspot
```

Suggested structure:

```text
EventStorming/

├── Domain/
│   ├── StormingBoard/
│   ├── StormingNode/
│   └── StormingConnection/
│
├── Features/
│   ├── CreateBoard/
│   ├── AddCommand/
│   ├── AddDomainEvent/
│   ├── AddAggregate/
│   ├── AddActor/
│   ├── AddPolicy/
│   ├── ConnectNodes/
│   └── MarkHotspot/
│
└── Projections/
    ├── BoardCanvas/
    └── DomainEventCatalog/
```

---

# 31. Architecture Module

Owns living system design.

Domain concepts:

```text
ArchitectureModel
SystemModule
DataEntity
ArchitectureRelationship
ModuleDependency
DataRelationship
```

Features:

```text
CreateModule
ConnectModules
CreateEntity
AddRelationship
DetectArchitectureDrift
```

Read models:

```text
ModuleMap
ERDView
DependencyView
ArchitectureOverview
```

---

# 32. ERD and Event-Sourced Systems

TCFlow must not assume that every system is CRUD-only.

Architecture UI should support at least two data perspectives:

```text
Domain / Event Model
```

and:

```text
Projection / Read Model
```

For an event-sourced system, an ERD alone is not enough to explain system state.

---

# 33. TaskFlow Module

Owns engineering execution.

Primary aggregate:

```text
EngineeringTask
```

Possible domain events:

```text
TaskProposed
TaskAccepted
TaskRejected
TaskAssigned
TaskUnassigned
TaskStarted
TaskBlocked
RequirementAdded
RequirementRemoved
TaskUpdatedFromSourceChange
AiVerificationCompleted
ReviewRequested
ReviewApproved
ReviewRejected
TaskCompleted
TaskReopened
TaskCancelled
```

Event sourcing naturally replaces much of the manual task-version history previously required.

---

# 34. TaskFlow Read Models

Inline:

```text
TaskCurrent
```

Async:

```text
TaskBoard
DeveloperWorkload
ProjectProgress
TaskSearch
TaskAnalytics
CycleTimeReport
```

---

# 35. TaskFlow Structure Example

```text
Modules/
└── TaskFlow/
    │
    ├── TCFlow.Modules.TaskFlow.Contracts/
    │   ├── Commands/
    │   ├── Events/
    │   ├── IntegrationEvents/
    │   └── DTOs/
    │
    └── TCFlow.Modules.TaskFlow/
        │
        ├── TaskFlowModule.cs
        ├── Authorization/
        ├── Domain/
        │   └── EngineeringTasks/
        │       ├── EngineeringTask.cs
        │       ├── TaskStatus.cs
        │       ├── Events/
        │       └── Rules/
        ├── Features/
        │   ├── CreateTask/
        │   ├── AssignTask/
        │   ├── StartTask/
        │   ├── CompleteTask/
        │   ├── ReopenTask/
        │   ├── GetTask/
        │   └── GetTaskBoard/
        ├── Projections/
        │   ├── TaskCurrent/
        │   ├── TaskBoard/
        │   └── TaskAnalytics/
        ├── Integration/
        │   ├── Consumers/
        │   └── Publishers/
        └── Data/
            ├── MartenConfiguration.cs
            └── ProjectionConfiguration.cs
```

---

# 36. RepositoryIntelligence Module

The restructured `RepositoryIntelligence` module must stop owning unrelated administration, GitHub authorization, and generic project management.

It should own only source-intelligence business concepts.

Responsibilities:

```text
Repository Analysis
Source Change
Evidence
Capability
Contract
Impact
Knowledge Graph
Source-to-System Mapping
Engineering Plan Reconciliation
```

Suggested structure:

```text
RepositoryIntelligence/

├── Domain/
│   ├── AnalysisRuns/
│   ├── SourceChanges/
│   ├── Impacts/
│   └── Evidence/
├── Features/
│   ├── AnalyzeRepository/
│   ├── AnalyzeSourceChange/
│   ├── DetectImpact/
│   ├── InferCapability/
│   ├── CompareContract/
│   └── ReconcileEngineeringPlan/
├── Projections/
│   ├── KnowledgeGraph/
│   ├── ImpactGraph/
│   ├── ContractIndex/
│   └── CapabilityIndex/
└── Integration/
    ├── Analyzers/
    └── Codex/
```

---

# 37. Source Analysis Facts

Repository Intelligence should convert source information into structured evidence.

Possible facts:

```text
SourceChangeDetected
ArtifactAdded
ArtifactRemoved
ArtifactChanged
ContractChanged
DependencyChanged
CapabilityChanged
DomainEventDiscovered
AggregateDiscovered
ProjectionDiscovered
```

Not every file change requires its own aggregate.

---

# 38. Source Analysis Stream Strategy

Avoid one infinitely growing stream per repository.

Potential stream scopes:

```text
analysis-run-{analysisRunId}
```

or:

```text
repository-analysis-{repositoryId}-{commitSha}
```

Stream design should reflect transactional/business boundaries.

---

# 39. Knowledge Graph as Projection

Knowledge Graph should generally be treated as derived state.

```text
Source Facts / Events
        ↓
Async Projection
        ↓
Knowledge Graph
```

It should not become the primary write aggregate.

---

# 40. Impact Graph as Projection

Similarly:

```text
Source Change
    ↓
Evidence
    ↓
Dependency
    ↓
Impact
    ↓
Async Projection
    ↓
Impact Graph
```

The graph can power architecture visualization, engineering impact visualization, source-to-document traceability, and cross-module analysis.

---

# 41. Integrations Module

Owns external system integration.

Examples:

```text
GitHub
RabbitMQ
External Webhooks
Future Jira
Future Linear
Future Azure DevOps
```

GitHub provider mechanics should not live inside Repository Intelligence business logic.

Repository Intelligence consumes normalized integration contracts.

---

# 42. Analyzer Architecture

Technology analyzers remain separated from business modules.

```text
Analyzers/

├── Core/
├── Contracts/
├── Vue/
├── AspNet/
├── Marten/
├── Knowledge/
└── GitHub/
```

Analyzers are technical plugins.

They are not business bounded contexts.

---

# 43. Analyzer Responsibility

Static analyzers should extract deterministic facts.

Examples:

```text
HTTP method
Route
Class
Property
DTO
Marten session usage
Domain Event declaration
Aggregate declaration
Projection declaration
Import
Dependency
```

AI should not be used for deterministic parsing that code can perform reliably.

---

# 44. AI Reasoning Responsibility

AI/Codex should focus on:

```text
Business meaning
Ambiguous capability inference
Cross-layer impact
Architecture drift reasoning
Task planning
Task reconciliation
Documentation reconciliation
Convention-aware reasoning
```

AI remains a reasoning layer, not the only intelligence layer.

---

# 45. Module Dependency Rule

Preferred direction:

```text
Host
 ↓
Modules
 ↓
BuildingBlocks
```

Module-to-module communication should use contracts.

Allowed:

```text
TaskFlow
    ↓
Planning.Contracts
```

Avoid:

```text
TaskFlow
    ↓
Planning implementation
```

A module must not directly depend on another module's implementation internals.

---

# 46. Module Communication

Cross-module communication should use:

```text
Contracts
Commands
Integration Events
Wolverine Messages
```

Example:

```text
Planning
    ↓
FeatureApproved
    ↓
Wolverine
    ↓
TaskFlow
```

Avoid direct internal data access across modules.

---

# 47. Contracts Projects

Where useful, modules should expose a dedicated contracts project.

Example:

```text
Modules/
└── Planning/
    ├── TCFlow.Modules.Planning.Contracts/
    └── TCFlow.Modules.Planning/
```

Contracts may include integration events, public commands, stable shared DTO contracts, and module interfaces.

Do not place internal aggregate implementation in Contracts.

---

# 48. Vue Frontend Organization

The Vue application should mirror backend domain names where practical.

```text
Apps/Web/src/

├── modules/
│   ├── projects/
│   ├── access-control/
│   ├── planning/
│   ├── event-storming/
│   ├── architecture/
│   ├── task-flow/
│   └── repository-intelligence/
├── shared/
│   ├── components/
│   ├── composables/
│   ├── api/
│   └── utils/
├── router/
└── app/
```

This gives developers a predictable mapping:

```text
Frontend                     Backend

planning/                    Planning/
event-storming/              EventStorming/
architecture/                Architecture/
task-flow/                   TaskFlow/
repository-intelligence/     RepositoryIntelligence/
```

---

# 49. Test Organization

Tests should mirror business modules.

```text
Tests/

├── PlatformAdministration/
├── Projects/
├── AccessControl/
├── Planning/
├── EventStorming/
├── Architecture/
├── TaskFlow/
└── RepositoryIntelligence/
```

Example:

```text
Tests/TaskFlow/

├── Domain/
│   └── EngineeringTaskTests.cs
├── Features/
│   └── AssignTaskTests.cs
├── Projections/
│   ├── TaskCurrentTests.cs
│   └── TaskBoardTests.cs
└── Integration/
    └── TaskLifecycleTests.cs
```

---

# 50. Event Sourcing Test Strategy

Event-sourced aggregate tests should focus on:

```text
Given
    historical events

When
    command

Then
    expected events
```

Example:

```text
Given:
TaskProposed
TaskAccepted

When:
AssignTask(UserA)

Then:
TaskAssigned(UserA)
```

Also test invalid invariants.

---

# 51. Projection Test Strategy

Inline and Async projections should be independently testable.

Verify:

```text
Events
    ↓
Projection
    ↓
Expected Read Model
```

Async projection tests should also validate rebuild behavior.

---

# 52. Replay and Rebuild

A critical quality requirement:

> **Read models must be rebuildable from event history.**

Every important projection should have a clear rebuild strategy.

Do not make hidden operational side effects necessary to reconstruct a business read model.

---

# 53. Idempotency

Durable message handlers, webhook handlers, external integration consumers, and projection handlers must be designed for idempotent behavior where required.

At-least-once delivery must not create duplicated business effects.

---

# 54. Optimistic Concurrency

Aggregate streams must use event-stream versioning to protect business invariants from concurrent writes.

Examples:

```text
Two users assign the same task simultaneously
Two users transfer ownership simultaneously
Two updates modify the same role permissions
```

Concurrency conflicts must be handled explicitly.

---

# 55. Event Metadata

Domain events should support useful metadata such as:

```text
EventId
StreamId
Version
Timestamp
ActorId
CorrelationId
CausationId
ProjectId
TenantId if applicable
Source
```

Metadata should help with audit, tracing, debugging, AI reasoning traceability, and cross-module causality.

---

# 56. Audit vs Event History

Event history and audit history overlap but are not identical.

Domain events explain:

```text
What happened in the business?
```

Audit records may additionally explain:

```text
Who attempted what?
From where?
Was access denied?
What administration setting changed?
```

Do not remove necessary audit capabilities simply because Event Sourcing exists.

---

# 57. Planning ↔ Event Storming ↔ Architecture ↔ Source

TCFlow should maintain relationships across the engineering lifecycle.

```text
Requirement
    ↓
Feature
    ↓
Event Storming
    ↓
Aggregate
    ↓
Architecture Module
    ↓
Source Implementation
    ↓
Engineering Task
    ↓
Pull Request
```

This relationship graph is a primary product capability.

---

# 58. Event Storming ↔ Code Mapping

TCFlow should support mappings such as:

```text
Command:
CreateProject
    ↓
CreateProject.cs

Aggregate:
Project
    ↓
Project.cs

Domain Event:
ProjectCreated
    ↓
ProjectCreated.cs

Projection:
ProjectCurrent
    ↓
ProjectCurrentProjection.cs
```

Missing or divergent mappings should be detectable.

---

# 59. Architecture Drift Detection

Architecture module defines intended architecture.

Repository Intelligence observes implemented architecture.

Compare:

```text
Designed Architecture
        ↕
Actual Source Architecture
```

Possible findings:

```text
Module dependency not documented
Documented module no longer exists
Domain Event added but not documented
Projection changed without architecture update
Forbidden dependency introduced
```

---

# 60. Engineering Plan Generation

Source-to-task remains a core capability.

Updated pipeline:

```text
Source Change
    ↓
Static Analysis
    ↓
Evidence
    ↓
Knowledge Graph
    ↓
Architecture Context
    ↓
Planning Context
    ↓
Convention
    ↓
Authority
    ↓
Impact
    ↓
Engineering Plan
    ↓
Task Reconciliation
```

This is broader than the original source-only model.

---

# 61. Task Reconciliation

Engineering plans evolve.

Before creating a task, the system must evaluate existing tasks.

Possible actions:

```text
Create
Update
Merge
Close
Reopen
Ignore
```

Event Sourcing improves this by preserving task lifecycle history naturally.

---

# 62. AI Verification vs Human Approval

Keep separate:

```text
AI Verified
```

and:

```text
Human Approved
```

Event-sourced task workflow should model both states explicitly.

---

# 63. Permission Architecture

The previously approved authorization design remains valid.

System level:

```text
System Admin
```

Project level:

```text
Project Owner
Custom Roles
Permissions
Resource Scope
Component Scope
AI Permission
```

Do not hard-code business authorization by role name.

---

# 64. Current Repository Migration Principle

The existing TCFlow repository already contains valuable business features, analyzers, tests, documentation, GitHub integration, project management, and repository intelligence.

Do not discard validated behavior blindly.

Migration should classify components as:

```text
KEEP
PORT
REWRITE
REMOVE
```

---

# 65. FullStackHero v10 Migration Strategy

Preferred strategy:

```text
1. Create clean FullStackHero v10 baseline
2. Verify .NET 10 + Aspire baseline
3. Introduce TCFlow BuildingBlocks
4. Add Marten
5. Add Wolverine
6. Add Event Sourcing conventions
7. Port bounded contexts one at a time
8. Add projections
9. Port analyzers
10. Port Vue integration
11. Port GitHub integration
12. Replace custom queues where Wolverine fits
13. Add RabbitMQ integration
14. Remove old write models after validation
```

---

# 66. Recommended Bounded Context Migration Order

```text
1. Projects
2. AccessControl
3. Planning
4. TaskFlow
5. EventStorming
6. Architecture
7. RepositoryIntelligence
8. Integrations
9. PlatformAdministration
```

Exact order may change if dependency analysis proves another order safer.

---

# 67. Avoid Overengineering

Even with full Event Sourcing + CQRS:

Do not add abstractions that do not protect a domain boundary.

Avoid:

```text
Repository pattern over Marten
Mediator wrapping Wolverine unnecessarily
Generic CRUD services
Generic BaseAggregate with excessive behavior
Graph database before required
RabbitMQ for internal projection processing
Event sourcing infrastructure-only state
```

---

# 68. Vertical Slice Rule

Feature folders should contain behavior related to one use case.

Example:

```text
Features/
└── AssignTask/
    ├── Command.cs
    ├── Handler.cs
    ├── Validator.cs
    ├── Endpoint.cs
    └── Response.cs
```

Avoid organizing all commands globally and all handlers globally.

---

# 69. Domain Purity

Domain logic should not depend directly on:

```text
ASP.NET
Wolverine
RabbitMQ
Vue
GitHub SDK
HTTP
```

Domain should express business concepts, business rules, business events, and invariants.

Infrastructure adapts these concepts to external systems.

---

# 70. Integration Isolation

External providers must be isolated behind integration contracts.

Examples:

```text
GitHub
Codex
RabbitMQ
Future Jira
Future Linear
```

A provider-specific SDK must not leak throughout business modules.

---

# 71. Aspire

.NET Aspire remains local orchestration and development infrastructure.

Expected development resources:

```text
API
PostgreSQL
Redis if required
RabbitMQ
Vue frontend
Observability
```

Production/self-host packaging should not rely on developer-only Aspire assumptions.

---

# 72. Self-host / LAN Direction

TCFlow must remain capable of internal company deployment.

Future deployment requirements include:

```text
Repeatable installation
Configuration validation
Database setup/migration
Health checks
RabbitMQ setup
Projection daemon health
Wolverine durability health
Upgrade procedures
Backup/restore
```

---

# 73. Observability

The new event-driven architecture requires strong observability.

Track:

```text
Correlation ID
Causation ID
Message ID
Event stream
Projection lag
Async daemon status
Wolverine queue health
RabbitMQ health
Failed messages
Dead-letter messages
Analysis latency
AI reasoning latency
```

---

# 74. AI Reasoning Durability

Deep reasoning jobs may be durable messages.

```text
ImpactCandidateDetected
        ↓
RunDeepReasoning
        ↓
Codex
        ↓
ReasoningCompleted
        ↓
ReconcileEngineeringPlan
```

AI failure must not corrupt domain transaction state.

---

# 75. Source Change Fast Path / Deep Path

Fast path:

```text
Git Change
    ↓
Static Analysis
    ↓
Immediate deterministic result
```

Deep path:

```text
Relevant Context
    ↓
AI Reasoning
    ↓
Semantic impact / plan
```

Deep AI reasoning should not block deterministic feedback.

---

# 76. Explainability

Every semantic impact should remain traceable.

```text
Source:
CreateProduct.vue

Change:
categoryId added

Contract:
POST /api/products

Affected:
CreateProductRequest

Architecture Module:
Catalog

Feature:
Product Categories

Task:
Support categoryId
```

---

# 77. Product Constraint: Precision First

The existing product principle remains:

> **Prefer silence over an incorrect task.**

Precision remains more important than aggressive automation.

---

# 78. Updated Repository Structure

```text
VietAIS-TCFlow/

├── docs/
│   ├── architecture/
│   ├── decisions/
│   ├── specifications/
│   ├── event-storming/
│   └── migration/
│
├── src/
│   ├── BuildingBlocks/
│   │   ├── Application/
│   │   ├── Authorization/
│   │   ├── EventSourcing/
│   │   ├── Messaging/
│   │   ├── Observability/
│   │   └── Persistence/
│   │
│   ├── Modules/
│   │   ├── PlatformAdministration/
│   │   ├── Projects/
│   │   ├── AccessControl/
│   │   ├── Planning/
│   │   ├── EventStorming/
│   │   ├── Architecture/
│   │   ├── TaskFlow/
│   │   ├── RepositoryIntelligence/
│   │   └── Integrations/
│   │
│   ├── Analyzers/
│   │   ├── Core/
│   │   ├── Contracts/
│   │   ├── Vue/
│   │   ├── AspNet/
│   │   ├── Marten/
│   │   ├── Knowledge/
│   │   └── GitHub/
│   │
│   ├── Apps/
│   │   └── Web/
│   ├── Host/
│   │   ├── Api/
│   │   └── AppHost/
│   └── Tests/
│       ├── Unit/
│       ├── Integration/
│       ├── Architecture/
│       └── Benchmarks/
│
├── deploy/
├── GOAL.md
├── GOAL2.md
├── PRODUCT_CONSTRAINTS.md
├── PROJECT_PLAN.md
├── AGENTS.md
├── WORKFLOW.md
└── GIT_RULES.md
```

---

# 79. Architecture Summary

```text
                         CLIENT / API
                              │
                              ▼
                       Command / Query
                              │
                ┌─────────────┴─────────────┐
                │                           │
                ▼                           ▼
          Wolverine Handler              Query
                │                           │
                ▼                           ▼
          Application Use Case         IQuerySession
                │                           │
                ▼                           ▼
       Event-Sourced Aggregate        Read Models
                │
       Business Rules / Invariants
                │
                ▼
          Domain Events
                │
                ▼
         MARTEN EVENT STORE
                │
        ┌───────┴────────┐
        │                │
        ▼                ▼
     INLINE            ASYNC
  PROJECTIONS       PROJECTIONS
        │                │
        ▼                ▼
 Current State      Dashboard
 Critical Views     Search
 Authorization      Knowledge Graph
                    Impact Graph
                    Analytics
        │                │
        └───────┬────────┘
                ▼
             QUERY API
```

---

# 80. Integration Architecture Summary

```text
Domain Event
     │
     ├── Internal Projection
     │      ├── Inline
     │      └── Async
     │
     └── Integration Event
              │
              ▼
       Wolverine Outbox
              │
              ▼
          RabbitMQ
              │
              ▼
       External Systems
```

---

# 81. Product Architecture Graph

```text
Planning
    │
    ▼
Feature / Requirement
    │
    ▼
Event Storming
    │
    ▼
Domain / Aggregate / Event
    │
    ▼
Architecture
    │
    ├── Module Map
    ├── ERD
    └── Dependencies
    │
    ▼
Repository Intelligence
    │
    ▼
Actual Source
    │
    ▼
Impact
    │
    ▼
Engineering Plan
    │
    ▼
TaskFlow
    │
    ▼
Implementation / PR
    │
    └──────────────► feeds back into source analysis
```

---

# 82. New TCFlow Positioning

The previous positioning:

> Source-Aware Engineering Planner

becomes a sub-capability.

The broader positioning is:

> **VietAIS TCFlow — Engineering System Intelligence Platform**

Alternative description:

> **Living Architecture & Engineering Management Platform**

Core promise:

> **TCFlow connects what a system is planned to be, how it is designed, what the source actually implements, and what engineering work remains.**

---

# 83. Key Differentiator

TCFlow should not compete primarily as:

```text
Task Manager
AI Coder
Jira Clone
Code Search Tool
```

Its key differentiator is the connected system model:

```text
Plan
+
Requirement
+
Event Storming
+
Architecture
+
ERD
+
Source Code
+
Impact
+
Task
+
AI
```

as one living engineering graph.

---

# 84. Migration Quality Gates

A bounded context is considered migrated only when:

```text
[ ] Existing behavior preserved or intentionally changed
[ ] Aggregate boundary documented
[ ] Commands defined
[ ] Domain Events defined
[ ] Business invariants tested
[ ] Inline projection defined where required
[ ] Async projections defined where required
[ ] Query endpoints use read models
[ ] Concurrency tested
[ ] Replay/rebuild tested
[ ] Wolverine durability verified where applicable
[ ] Integration events use outbox where applicable
[ ] Old document write model removed only after verification
```

---

# 85. Final Architectural Rules

1. Organize by bounded context first.
2. Use Vertical Slice inside modules.
3. Use Event Sourcing for business history where it provides domain value.
4. Do not Event Source operational infrastructure state.
5. Use Wolverine for commands, durable messages, and transactional messaging.
6. Use Marten Event Store for event-sourced aggregates.
7. Use Inline projections for critical strongly-consistent read models.
8. Use Async projections for dashboards, search, graphs, analytics, and cross-stream views.
9. Use RabbitMQ only for integration messaging, not internal Marten projection processing.
10. Keep analyzers as technical plugins outside business bounded contexts.
11. Keep module implementation isolated; depend on Contracts.
12. Keep Domain independent from infrastructure frameworks.
13. Prefer explicit domain events over direct aggregate mutation.
14. Keep projection folders organized by read-model purpose, not infrastructure mode.
15. Preserve source traceability and explainability.
16. Keep AI reasoning downstream from deterministic static analysis.
17. Preserve Human Approval separately from AI Verification.
18. Preserve System Admin separately from Project Owner.
19. Avoid generic abstractions that add no domain value.
20. Preserve the product principle: **precision before automation**.

---

# 86. Status

`GOAL2.md` is the architectural evolution baseline for the next TCFlow restructuring.

It should be read together with:

```text
GOAL.md
PRODUCT_CONSTRAINTS.md
PROJECT_PLAN.md
AGENTS.md
WORKFLOW.md
GIT_RULES.md
```

When `GOAL.md` and `GOAL2.md` differ specifically on the new architecture, the architectural direction in `GOAL2.md` should be treated as the newer accepted direction.

The next implementation planning pass should therefore regenerate or revise the existing project plan around:

```text
FullStackHero v10
.NET 10
DDD
CQRS
Marten Event Sourcing
Wolverine
Inline Projection
Async Projection
RabbitMQ Integration
Planning
Event Storming
Architecture / ERD
TaskFlow
Repository Intelligence
Living Architecture
```
