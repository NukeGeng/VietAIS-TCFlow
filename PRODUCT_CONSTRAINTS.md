# PRODUCT_CONSTRAINTS.md

# Product Constraints and Risk Controls

## 1. Purpose

This document defines the product-level constraints that must be respected while
evolving VietAIS TCFlow into an Engineering System Intelligence and Living
Architecture Platform.

`GOAL2.md` defines the current product and architecture direction. `GOAL.md`
retains the source-awareness, precision, explainability, permission, and task
reconciliation principles that `GOAL2.md` extends.

This file describes what must not go wrong while building it.

Every architecture decision, feature specification, implementation plan, and coding task must be checked against these constraints.

---

# 2. Core Product Principle

> **The system should prefer silence over an incorrect task.**

The product must prioritize precision, explainability, human control, and repository awareness over aggressive automation.

The goal is not to generate the largest number of AI tasks.

The goal is to generate the smallest number of useful, explainable, high-confidence engineering actions.

---

# 3. Constraint 01 — Initial Setup Must Not Be Overwhelming

## Risk

The product includes:

- GitHub integration.
- AI/Codex integration.
- Authority Policy.
- Convention Profile.
- Permissions.
- Component Scope.
- AI Permissions.
- Repository analysis.
- Event Storming.
- Architecture and ERD models.
- Event Sourcing consistency.
- Projection lag and rebuild behavior.

If users must configure all of these before seeing value, onboarding becomes too difficult.

## Required behavior

The default onboarding flow must prefer auto-detection.

```text
Connect Repository
    ↓
Scan
    ↓
Detect Technologies
    ↓
Detect Conventions
    ↓
Detect Domains / Events / Aggregates / Projections
    ↓
Suggest Authority
    ↓
User Confirms or Adjusts
```

## Required design

The system should automatically detect where possible:

- Vue.
- ASP.NET Core.
- Marten.
- Minimal APIs.
- Feature-based folders.
- API patterns.
- Naming conventions.
- Common repository boundaries.
- Domain event declarations.
- Aggregate and module boundaries.
- Projection declarations and dependencies.

## Forbidden implementation

Do not require users to manually configure the full system before first analysis.

---

# 4. Constraint 02 — AI Task Noise Must Be Minimized

## Risk

High false-positive rates will cause users to ignore the system.

## Required behavior

Task creation must never follow:

```text
Source Change
→ LLM
→ Create Task
```

It must follow:

```text
Source Change
    ↓
Meaningful Change Filter
    ↓
Dependency Check
    ↓
Authority + Convention
    ↓
Impact Candidate
    ↓
Confidence
    ↓
Task Suggestion / Task Creation
```

## Required design

Low-confidence results should remain suggestions.

High-confidence results may become tasks only when project automation policy permits it.

## Product priority

Precision is more important than recall.

---

# 5. Constraint 03 — Every Impact Must Be Explainable

## Risk

Users will not trust AI-generated tasks without evidence.

## Required behavior

Every non-trivial impact must include:

```text
Source
Change
Affected Artifact
Reason
Evidence
Confidence
```

Example:

```text
Source:
CreateProduct.vue

Change:
+ categoryId

Affected:
CreateProductRequest

Reason:
Frontend request now contains categoryId but backend request does not.

Evidence:
POST /api/products

Confidence:
0.96
```

## Required design

Every generated task must be traceable to:

```text
Commit / Change
→ Artifact
→ Contract / Dependency
→ Impact
→ Task
```

---

# 6. Constraint 04 — Business Context Must Be Repository-Aware

## Risk

Generic AI best practices may conflict with the actual architecture or business conventions of a team.

## Required behavior

Reasoning must use:

```text
Repository Knowledge
+
Convention Profile
+
Authority Policy
+
Current Source
```

## Required design

The system must detect and persist repository conventions.

The AI must not invent architecture when an existing pattern is available.

---

# 7. Constraint 05 — Tasks Must Not Become Stale

## Risk

Source code continues changing after a task is created.

A task may become:

- Incomplete.
- Duplicated.
- Obsolete.
- Reverted.
- Expanded.
- No longer required.

## Required behavior

Every relevant source change must trigger task reconciliation.

Possible outcomes:

```text
Create
Update
Merge
Close
Reopen
Ignore
```

## Required design

Task generation and task reconciliation must be separate concerns.

---

# 8. Constraint 06 — AI Must Not Silently Modify Tasks

## Risk

Users lose control when task requirements change without explanation.

## Required behavior

Task updates must preserve history.

Example:

```text
Task v1
    ↓
New Source Change
    ↓
Proposed Update
    ↓
Task v2
```

The UI should show:

```text
What changed
Why it changed
Which source change caused it
Who or what changed it
```

## Required design

Task versioning or equivalent change history is required.

---

# 9. Constraint 07 — Do Not Become a Weak Engineering Management Clone

## Risk

A generic task-management UI is not enough to differentiate the product.

## Required behavior

The product must remain centered on the connected living system model:

```text
Plan / Requirement
→ Event Storming
→ Architecture / Data Model
→ Source
→ Impact
→ Engineering Plan / Task
```

Kanban is only a presentation and workflow layer.

## Required design

The connection between intended design, actual source, impact, and remaining
engineering work must remain the core differentiator.

The architecture should allow future synchronization with external task systems.

---

# 10. Constraint 08 — Powerful Permissions Must Still Be Understandable

## Risk

RBAC + Permission + Resource Scope + Component Scope can become too complex for normal users.

## Required behavior

Normal users should be able to understand:

```text
What can I do?
Why can I not do this?
Who can grant the missing access?
```

## Required design

Backend authorization remains detailed.

User-facing UI should simplify it.

Example forbidden response:

```text
403 Forbidden
```

Preferred response:

```text
Missing permission:
task.approve

Scope:
Backend component

Request access from:
Project Owner
```

---

# 11. Constraint 09 — Realtime Analysis Must Feel Responsive

## Risk

If every push waits for deep AI reasoning, the system will feel slow.

## Required behavior

Split analysis into:

```text
Fast Path
    ↓
Deterministic / incremental analysis
    ↓
Immediate impact candidate
```

and:

```text
Deep Path
    ↓
AI reasoning
    ↓
Detailed impact / task plan
```

## Required design

Users should receive quick feedback before expensive reasoning completes.

---

# 12. Constraint 10 — AI Verification Must Not Equal Human Approval

## Risk

AI may determine that code appears complete while business behavior remains wrong.

## Required behavior

Separate:

```text
AI Verified
```

from:

```text
Human Approved
```

## Required design

Do not use a single boolean such as:

```text
Completed = true
```

to represent both states.

---

# 13. Constraint 11 — Self-Hosted / LAN Operation Must Be Maintainable

## Risk

Companies using isolated LAN environments may struggle with setup, migration, updates, and diagnostics.

## Required behavior

Self-host deployment should eventually support:

```text
Simple installation
Repeatable startup
Database migration
Health checks
Versioned upgrades
Configuration validation
Projection daemon health and rebuild operations
Wolverine durability diagnostics
RabbitMQ setup and dead-letter diagnostics
Event Store backup/restore and upgrade procedures
```

## Required design

Local development may use Aspire.

Production/self-host packaging must not assume developer tooling.

---

# 14. Constraint 12 — AI Quality Must Be Measurable

## Risk

Without benchmarks, improvements may only appear better subjectively.

## Required behavior

The project must eventually maintain known analysis cases.

Metrics should include:

```text
Precision
Recall
False Positive Rate
False Negative Rate
Task Duplication Rate
Task Reconciliation Accuracy
Projection Rebuild Accuracy
Projection Lag
Durable Message Duplication Rate
Dead-letter Rate
```

## Product priority

Prefer precision over recall.

A feature should not improve recall by severely degrading precision.

---

# 15. Constraint 13 — The Product Must Not Become a Thin LLM Wrapper

## Risk

If the product only sends repository content to Codex/LLM and displays the answer, users can use a coding agent directly.

## Required differentiators

The product must own:

```text
Static Analysis
Repository Knowledge Graph
Planning / Event Storming / Architecture Graph
Domain and Projection Model
Convention Profile
Authority Policy
Explainable Impact
Task Reconciliation
Permission-aware Automation
Source Traceability
```

AI reasoning is only one layer.

---

# 16. Constraint 14 — Living Architecture Must Not Become Static Documentation

## Risk

Planning boards, Event Storming, module maps, ERDs, and architecture documents
can drift away from the source and become another disconnected documentation
system.

## Required behavior

The product must maintain traceable relationships across:

```text
Requirement
→ Feature
→ Command / Domain Event / Aggregate
→ Architecture Module / Data Model
→ Source Artifact
→ Impact
→ Engineering Task / Pull Request
```

Source changes must be able to identify missing or divergent architecture
documentation. Design changes must be traceable to their expected source
implementation.

## Forbidden implementation

Do not store Event Storming, architecture, or ERD artifacts as isolated canvases
with no stable identity or source mapping.

---

# 17. Constraint 15 — Event Sourcing Must Be Purposeful

## Risk

Event Sourcing every record creates unnecessary streams, replay cost, migration
complexity, and operational burden.

## Required behavior

Every persisted concept must be classified as:

```text
Business truth → Event Store
Derived query state → Projection
Infrastructure/runtime state → Operational Document
```

Event Sourcing is justified by business history, invariants, concurrency, audit
value, or temporal reasoning—not by architectural fashion.

## Forbidden implementation

Do not Event Source webhook deliveries, Wolverine inbox/outbox state, retries,
projection checkpoints, caches, temporary analysis jobs, authentication tokens,
or process state.

---

# 18. Constraint 16 — Projection Consistency Must Be Explicit

## Risk

Using Async views where immediate correctness is required can break authorization
or command-followed-by-query flows. Making every view Inline can make writes slow
and tightly coupled.

## Required behavior

Use Inline projections only for critical strongly-consistent current state.
Use Async projections for dashboards, search, analytics, graphs, reporting, and
cross-stream views where eventual consistency is acceptable.

Every important projection must define:

```text
Consumer
Consistency expectation
Replay/rebuild strategy
Failure behavior
Lag observability
```

## Forbidden implementation

Do not hide eventual consistency from API/UI consumers. Do not require hidden
side effects that make a read model impossible to rebuild from history.

---

# 19. Constraint 17 — Durable Messaging Must Not Duplicate Business Effects

## Risk

At-least-once delivery, webhook redelivery, retries, or process restarts can
duplicate events, impacts, tasks, audits, or external notifications.

## Required behavior

Wolverine durable handlers and external consumers must be idempotent where
delivery may repeat. Event Store commits and outgoing integration messages must
be coordinated through the durable outbox.

Retries, failed messages, dead-letter messages, correlation, and causation must
be observable and recoverable.

## Forbidden implementation

Do not use RabbitMQ as the mechanism for Marten Async Projections. Do not publish
external integration messages before the owning business transaction commits.

---

# 20. Constraint 18 — Bounded Contexts Must Remain Isolated

## Risk

A modular monolith can become a distributed-looking monolith if modules access
each other's implementation types, documents, or tables directly.

## Required behavior

Cross-module behavior must use stable Contracts, commands, integration events,
or Wolverine messages. A module owns its aggregates, projections, and data.

Repository Intelligence must not become the owner of generic project lifecycle,
access control, platform administration, or GitHub provider mechanics.

## Forbidden implementation

Do not reference another module's implementation project or query its internal
Marten storage directly merely to save time.

---

# 21. Constraint 19 — Migration Must Preserve Validated Behavior

## Risk

A clean FullStackHero v10 baseline or Event Sourcing rewrite can discard working
authorization, audit, source analysis, GitHub integration, task reconciliation,
or acceptance evidence.

## Required behavior

Every migrated component must receive an evidence-backed decision:

```text
KEEP
PORT
REWRITE
REMOVE
```

Migration proceeds one bounded context and vertical slice at a time. The legacy
path is removed only after behavioral parity or an intentional contract change,
replay, projection rebuild, concurrency, and runtime verification pass.

## Forbidden implementation

Do not perform a big-bang rewrite or treat successful compilation as proof that
a bounded context was migrated correctly.

---

# 22. Constraint 20 — Event-Driven Operations Must Be Observable

## Risk

An event-driven system can appear healthy while projections lag, messages retry,
dead letters accumulate, or causal traces are lost.

## Required behavior

The platform must expose or collect, where applicable:

```text
Correlation ID
Causation ID
Event stream and version
Projection lag / Async Daemon health
Wolverine queue and failed-message health
RabbitMQ queue / unacked / dead-letter health
Analysis and AI reasoning latency
```

Production and self-host diagnostics must not depend solely on developer-only
Aspire tooling.

---

# 23. Progressive Trust

Automation should be configurable by trust level.

Example model:

```text
Level 0
Analyze only

Level 1
Suggest tasks

Level 2
Auto-create high-confidence tasks

Level 3
Auto-update existing tasks

Level 4
Generate code / create PR
```

Project Owner controls the allowed level.

The exact model may evolve, but progressive trust must be preserved as a product principle.

---

# 24. User Control

The system must not take irreversible or high-impact actions without the configured permission and trust policy.

Human users must always be able to understand:

```text
What happened?
Why?
What evidence caused it?
Can I reject or revert it?
```

---

# 25. Quality Decision Rule

Before implementing a feature, ask:

```text
Does this increase task noise?
Does this reduce explainability?
Does this bypass repository conventions?
Does this bypass authority?
Does this reduce user control?
Does this make permissions harder to understand?
Does this increase dependency on raw LLM reasoning?
Does this make source traceability weaker?
Does this make tasks easier to become stale?
Does this create an event stream without business value?
Can every affected projection be rebuilt?
Is the selected consistency mode correct for its consumer?
Can redelivery duplicate a business effect?
Does this cross a module implementation boundary?
Does this remove validated behavior before migration proof exists?
Can operators trace event, projection, and message failures?
```

If yes, the design should be reconsidered.

---

# 26. Definition of Acceptable Product Behavior

The product is behaving correctly when:

```text
A meaningful source change occurs
        ↓
The system identifies only relevant impact candidates
        ↓
The user can see why each impact exists
        ↓
Authority and convention are respected
        ↓
Existing tasks are reconciled before new ones are created
        ↓
Automation respects trust and permission policies
        ↓
AI verification remains distinct from human approval
        ↓
Architecture and source mappings remain traceable
        ↓
Read models converge and can be rebuilt
        ↓
Durable redelivery does not duplicate business effects
```

---

# 27. Final Product Constraint

The product must optimize for developer trust.

Developer trust is considered more important than:

- Maximum automation.
- Maximum number of generated tasks.
- Maximum AI usage.
- Maximum feature count.
