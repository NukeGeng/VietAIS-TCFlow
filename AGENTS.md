# AGENTS.md

# Agent Rules for Source-Aware Engineering Planner
#IMPORTANT
không được xoá hay sửa file ngoài folder project này trong máy dù có full access mà không có sự cho phép của tôi
khi muốn xoá gì ngoài phạm vi project thì phải stop process và hỏi tôi khi tôi trả lời thì mới được làm tiếp
## 1. Purpose

This file defines the mandatory rules that every coding agent, AI assistant, or automated implementation agent must follow when working in this repository.

These rules are not suggestions.

They are repository-level constraints.

The agent must treat:

- `GOAL.md` as the product and architecture source of truth.
- `WORKFLOW.md` as the mandatory implementation lifecycle.
- Existing source code as the primary source for current repository conventions.
- Feature/task acceptance criteria as the definition of completion.

The agent must not begin implementation before completing the required preparation steps.

---

# 2. Mandatory Read Order

Before implementing any task, the agent must:

1. Read `GOAL.md`.
2. Read `WORKFLOW.md`.
3. Read the task or feature specification.
4. Inspect the affected source code.
5. Inspect neighboring code in the same module.
6. Identify the existing implementation convention.
7. Identify the relevant authorization, persistence, validation, API, and testing conventions.
8. Identify the acceptance criteria.
9. Identify affected dependencies and public contracts.
10. Only then begin implementation.

The agent must not skip this sequence merely because the requested change appears small.

---

# 3. Source of Truth Priority

When information conflicts, use this priority:

1. Explicit task acceptance criteria.
2. `GOAL.md`.
3. `WORKFLOW.md`.
4. Existing repository architecture and conventions.
5. Existing tests.
6. Existing documentation.
7. Agent assumptions.

If two higher-priority sources conflict, the agent must not silently choose one.

The agent must report the conflict explicitly.

---

# 4. Architecture Rules

The agent must not invent a new architecture when the repository already has an established pattern.

The agent must:

- Preserve the FullStackHero-based backend structure.
- Preserve module boundaries.
- Follow existing feature/module organization.
- Follow existing naming conventions.
- Follow existing dependency injection patterns.
- Follow existing request/response conventions.
- Follow existing validation conventions.
- Follow existing authorization conventions.
- Follow existing error handling conventions.
- Follow existing logging conventions.
- Follow existing testing conventions.

The agent must not introduce:

- A new architectural layer.
- A new repository pattern.
- A new mediator abstraction.
- A new CQRS framework.
- A new service layer.
- A new persistence abstraction.
- A new dependency.

unless the task requires it and the existing architecture cannot satisfy the requirement.

---

# 5. FullStackHero Rules

The backend foundation is FullStackHero dotnet-starter-kit release `2.0.4-rc`.

The agent must preserve the overall structure and conventions of the starter kit.

The agent must not replace existing framework infrastructure only to make implementation easier.

Existing infrastructure should be reused whenever reasonable.

If a business module is added, it should fit the current module architecture instead of bypassing it.

---

# 6. Marten Rules

New business-domain persistence should use Marten according to the project conventions.

The agent must understand the difference between:

- `IQuerySession` for read-oriented operations.
- `IDocumentSession` for write-oriented operations.

The agent must persist writes explicitly where required using:

```csharp
await session.SaveChangesAsync(cancellationToken);
```

The agent must not introduce another persistence mechanism into a Marten-based business module without a concrete requirement.

The agent must not create an unnecessary repository abstraction over Marten merely to imitate Entity Framework patterns.

Marten Event Sourcing must not be introduced unless explicitly required by the task or current project specification.

---

# 7. Authorization Rules

Authorization must be permission-based.

The agent must not hard-code authorization using role names such as:

```csharp
if (user.Role == "Admin")
```

or:

```csharp
if (user.IsOwner)
```

for ordinary authorization decisions.

Authorization should be expressed using:

```text
Permission
+
Resource Scope
+
Component Scope
```

System-level and project-level authorization must remain separate.

The agent must preserve the distinction between:

```text
System Admin
```

and:

```text
Project Owner
```

`System Admin` controls the platform.

`Project Owner` controls only the owned project.

Project Owners may create custom project roles using system-defined permission definitions.

Project Owners must never be able to grant system-level permissions.

---

# 8. Authority Is Not Permission

The agent must not confuse:

```text
Authority Policy
```

with:

```text
Authorization Permission
```

Authority answers:

> Which source is the source of truth?

Permission answers:

> Which actor is allowed to perform an action?

For example:

```text
API Contract Authority = Backend
```

does not mean every backend developer can update authority settings.

Only an actor with the proper permission may do so.

---

# 9. AI Actor Rules

AI is an actor with explicit permissions.

The agent must respect AI policy such as:

```text
ai.analysis.run
ai.task.suggest
ai.task.create
ai.task.update
ai.task.close
ai.code.generate
ai.pull_request.create
```

AI must not automatically perform an action when the configured project policy only allows suggestions.

AI actions must be auditable.

---

# 10. Scope Discipline

The agent must make the smallest correct change that satisfies the task.

The agent must not modify unrelated modules.

The agent must not refactor unrelated code during a feature implementation unless required to complete the task safely.

The agent must not rename public types, routes, permissions, files, or contracts unnecessarily.

The agent must not change a public contract without updating all affected artifacts and tests.

---

# 11. Dependency Rules

The agent must not add a package or dependency merely because it simplifies implementation.

Before adding a dependency, verify:

1. The repository does not already provide equivalent functionality.
2. The standard library/framework cannot reasonably handle the requirement.
3. The dependency is actively maintained and compatible with the project.
4. The dependency does not conflict with existing architecture.
5. The dependency is justified by the task.

The final task report must mention any newly added dependency and why it was necessary.

---

# 12. Public Contract Rules

Public contracts include, but are not limited to:

- HTTP routes.
- Request DTOs.
- Response DTOs.
- OpenAPI contracts.
- Permission codes.
- Domain event contracts.
- Configuration contracts.
- Public interfaces.
- API error responses.

If a public contract changes, the agent must identify all affected consumers.

The agent must update:

- Related implementation.
- Tests.
- Documentation where applicable.
- Frontend/backend contract usages where applicable.
- Analyzer fixtures where applicable.

---

# 13. Vue Rules

Vue 3 + TypeScript + Vite is the primary frontend stack.

The agent must follow existing Vue conventions in the repository.

Before creating a new component, composable, store, service, or type:

- Inspect equivalent existing implementations.
- Reuse existing patterns.
- Preserve naming conventions.
- Preserve API client conventions.
- Preserve state-management conventions.
- Preserve validation conventions.
- Preserve routing conventions.

The agent must not introduce an alternative frontend state-management or API-client pattern without a requirement.

---

# 14. Source Analyzer Rules

Static analysis must be preferred over LLM reasoning for deterministic facts.

Do not use AI reasoning merely to determine facts that can be parsed directly, including:

- File paths.
- Git diffs.
- HTTP methods.
- Literal routes.
- Class names.
- Properties.
- Imports.
- Method calls.
- DTO fields.
- Marten session calls.

AI should be used for:

- Ambiguous business meaning.
- Capability inference.
- Cross-layer impact reasoning.
- Convention-aware task planning.
- Semantic dependency reasoning.
- Task reconciliation.

---

# 15. Evidence Rules

Every non-trivial inferred result should distinguish between:

```text
CONFIRMED
INFERRED
PROPOSED
```

The agent must never present an inferred or proposed behavior as confirmed source truth.

---

# 16. Testing Rules

Build success is not task completion.

Every task must have appropriate verification.

Depending on the task, verification may include:

- Unit tests.
- Integration tests.
- API tests.
- Authorization tests.
- Persistence tests.
- Contract tests.
- Runtime checks.
- UI tests.
- Analyzer fixture tests.
- Manual verification when automation is not practical.

The agent must not:

- Delete tests to make the build pass.
- Disable failing tests without justification.
- Weaken assertions to make tests pass.
- Mock away the behavior being tested.
- Ignore a failing test that is relevant to the task.

---

# 17. Test Quality Rules

Tests must validate behavior, not just implementation details.

When fixing a bug, the agent should first ensure there is a test or reproducible verification demonstrating the failure whenever practical.

When implementing a feature, tests should cover meaningful acceptance criteria.

For permission-sensitive features, include negative-path verification such as:

```text
Unauthorized request → 401
Authenticated but forbidden request → 403
Authorized request → expected result
```

---

# 18. Runtime Verification Rules

For features involving runtime behavior, the agent must verify runtime behavior where practical.

Examples:

- Database persistence.
- API serialization.
- Authorization enforcement.
- Aspire service startup.
- Webhook handling.
- Task reconciliation.
- Analyzer output.
- Frontend/backend contract compatibility.

The agent must not assume that compilation proves runtime correctness.

---

# 19. Performance-Sensitive Rules

For performance-sensitive, realtime, streaming, audio, concurrency, or high-throughput features:

The agent must define measurable success criteria before claiming completion.

Examples:

```text
latency
throughput
memory usage
buffer underrun
packet loss
dropped frames
CPU usage
error rate
```

A feature that "runs" is not sufficient evidence of correctness.

---

# 20. Task Completion Rules

A task is not complete until the agent has:

1. Re-read the task requirements.
2. Verified all acceptance criteria.
3. Built the affected projects.
4. Run relevant tests.
5. Performed runtime/integration verification where applicable.
6. Reviewed the final diff.
7. Checked for unintended changes.
8. Checked authorization implications.
9. Checked public contract implications.
10. Checked persistence implications.
11. Checked audit implications where relevant.
12. Confirmed no relevant test was disabled or weakened.

---

# 21. Diff Review Rules

Before finishing, review the complete diff.

The agent must ask:

- Did I modify anything outside the task scope?
- Did I accidentally change public behavior?
- Did I introduce duplication?
- Did I bypass an existing abstraction?
- Did I violate a repository convention?
- Did I forget a test?
- Did I forget authorization?
- Did I forget audit logging?
- Did I forget cancellation tokens?
- Did I forget persistence commit semantics?
- Did I introduce an unnecessary dependency?

---

# 22. Failure Handling

If implementation fails:

- Diagnose the actual failure.
- Do not hide the failure.
- Do not replace the intended behavior with a weaker behavior.
- Do not silently remove requirements.
- Do not mark the task complete.

If an external limitation prevents completion, document:

```text
What failed
Why it failed
What was verified
What remains incomplete
```

---

# 23. Conflict Handling

If `GOAL.md`, `WORKFLOW.md`, source code, tests, or task requirements conflict materially:

Do not guess.

Do not silently rewrite architecture.

Report:

```text
Conflict
Source A
Source B
Impact
Recommended resolution
```

---

# 24. Documentation Rules

Documentation must be updated when behavior or architecture changes materially.

Do not create documentation for trivial implementation details.

Do update documentation for:

- New public APIs.
- Permission changes.
- New architecture decisions.
- New project-level configuration.
- New analyzer capability.
- New AI policy.
- New deployment/setup requirement.

---

# 25. Audit Rules

Security-sensitive and administration actions must generate audit information when required by the project architecture.

Examples:

- Role changes.
- Permission changes.
- Ownership transfer.
- Repository access changes.
- AI permission changes.
- Authority policy changes.
- AI-generated task updates.

The agent must not implement sensitive administrative actions without considering audit requirements.

---

# 26. Security Rules

The agent must not:

- Hard-code secrets.
- Commit credentials.
- Log access tokens.
- Log passwords.
- Log sensitive authentication data.
- Expose system permissions to project owners.
- Trust frontend authorization checks.

Frontend authorization is UX only.

Backend authorization is authoritative.

---

# 27. Git Rules

The agent must keep changes focused.

Do not modify generated or unrelated files unless required.

Do not reformat the entire repository as part of an unrelated task.

Commit structure, when used, should reflect meaningful task boundaries.

---

# 28. Reconciliation Rules

When implementing task generation or source-change handling:

Do not create duplicate tasks blindly.

Before creating a task, check whether an existing task should be:

```text
Updated
Merged
Closed
Reopened
Ignored
```

Tasks generated from source must remain traceable to their evidence and source changes.

---

# 29. Agent Reporting Format

At task completion, report:

```text
Summary
- What changed

Affected Areas
- Modules/files

Verification
- Build
- Tests
- Runtime checks

Contracts
- Any public contract changes

Permissions
- Any authorization changes

Dependencies
- Any packages added/removed

Known Limitations
- Anything not fully verified
```

Do not claim success if verification is incomplete.

---

# 30. Final Rule

The agent's goal is not to produce the maximum amount of code.

The goal is to produce the smallest verified change that:

- Matches the product goal.
- Follows repository conventions.
- Satisfies acceptance criteria.
- Preserves architecture.
- Passes verification.
- Can be explained and audited.
