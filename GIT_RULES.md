# GIT_RULES.md

# Git Workflow Rules for VietAIS TCFlow

## 1. Purpose

This document defines the mandatory Git branching, pull request, merge, and synchronization rules for VietAIS TCFlow.

The repository uses long-lived domain integration branches:

```text
main
frontend
backend
mobile
ai
```

Feature development must never happen directly on these branches.

Every implementation task must be developed on a dedicated short-lived branch created from the correct domain branch.

---

# 2. Branch Hierarchy

```text
main
│
├── frontend
│   ├── feat/frontend/...
│   ├── fix/frontend/...
│   ├── refactor/frontend/...
│   └── test/frontend/...
│
├── backend
│   ├── feat/backend/...
│   ├── fix/backend/...
│   ├── refactor/backend/...
│   └── test/backend/...
│
├── mobile
│   ├── feat/mobile/...
│   ├── fix/mobile/...
│   └── ...
│
└── ai
    ├── feat/ai/...
    ├── fix/ai/...
    ├── refactor/ai/...
    └── test/ai/...
```

---

# 3. Long-Lived Branch Responsibilities

## `main`

`main` is the stable integration branch.

Rules:

- No direct feature development.
- No direct implementation commits.
- No force push.
- No destructive history rewrite.
- Changes enter through reviewed Pull Requests.
- Relevant CI checks must pass before merge.

## `frontend`

Long-lived integration branch for Vue frontend work.

Typical scope:

```text
Vue pages
Vue components
Pinia stores
Vue Router
API clients
Frontend permissions
Frontend tests
```

Frontend feature branches must branch from `frontend`.

## `backend`

Long-lived integration branch for ASP.NET Core / FullStackHero / Marten work.

Typical scope:

```text
ASP.NET endpoints
Marten documents
Authorization
Permission engine
Project management
Repository APIs
Task APIs
Audit
Backend tests
```

Backend feature branches must branch from `backend`.

## `mobile`

Long-lived integration branch for mobile development.

Mobile feature branches must branch from `mobile`.

## `ai`

Long-lived integration branch for repository intelligence and AI work.

Typical scope:

```text
Vue Analyzer
ASP.NET Analyzer
Marten Analyzer
Dependency Graph
Contract Engine
Impact Engine
Task Reconciliation
AI / Codex integration
Benchmarks
```

AI feature branches must branch from `ai`.

---

# 4. Protected Branches

Protected branches:

```text
main
frontend
backend
mobile
ai
```

Mandatory rules:

- Pull Request required.
- No direct implementation work.
- No force push.
- No unreviewed merge.
- Relevant build/test checks must pass.

---

# 5. Feature Branch Rule

Every implementation task must use a short-lived branch.

Frontend example:

```bash
git checkout frontend
git pull origin frontend
git checkout -b feat/frontend/task-board
```

Backend example:

```bash
git checkout backend
git pull origin backend
git checkout -b feat/backend/project-permissions
```

AI example:

```bash
git checkout ai
git pull origin ai
git checkout -b feat/ai/vue-analyzer
```

The base branch must match the task domain.

---

# 6. Branch Naming Convention

Format:

```text
<type>/<domain>/<short-description>
```

Allowed types:

```text
feat
fix
refactor
test
docs
chore
perf
```

Domains:

```text
frontend
backend
mobile
ai
```

Examples:

```text
feat/frontend/task-board
fix/frontend/permission-display

feat/backend/project-owner
fix/backend/effective-permission
refactor/backend/repository-module

feat/ai/vue-analyzer
fix/ai/task-reconciliation
test/ai/impact-benchmark

feat/mobile/project-list
```

Use lowercase kebab-case.

Avoid vague names such as:

```text
update
fix
new-feature
changes
final
```

---

# 7. Domain Ownership Rule

A feature branch must be created from the domain branch that owns the implementation.

```text
Frontend task → frontend
Backend task  → backend
Mobile task   → mobile
AI task       → ai
```

Do not create a backend feature branch from `frontend`, or an AI feature branch from `backend`.

---

# 8. Cross-Domain Features

A business feature may affect multiple domains.

Example:

```text
Feature:
Project Permission Management
```

Split into separate branches:

```text
feat/backend/project-permissions
→ Draft PR → backend
```

```text
feat/frontend/project-permissions
→ Draft PR → frontend
```

Dependencies between tasks must be explicit.

Example:

```text
Frontend task
depends_on:
Backend contract task
```

Do not put unrelated cross-domain implementation into one branch merely for convenience.

---

# 9. Standard Feature Workflow

```text
Domain Branch
    ↓
Create Feature Branch
    ↓
Implement
    ↓
Build
    ↓
Test
    ↓
Push
    ↓
Open Draft PR
    ↓
Target Domain Branch
    ↓
Continue Iteration
    ↓
Ready for Review
    ↓
Review + CI
    ↓
Merge to Domain Branch
```

Example:

```text
backend
    ↓
feat/backend/project-permission
    ↓
Draft PR
    ↓
backend
```

---

# 10. Draft Pull Request Rule

Feature Pull Requests should be opened as Draft PRs early when collaboration or CI feedback is useful.

Draft PR benefits:

- Early visibility.
- CI feedback.
- Review context.
- Progress tracking.

Example:

```text
feat/ai/impact-engine
→ Draft PR
→ ai
```

---

# 11. Ready for Review Rule

A Draft PR may become Ready for Review only when:

```text
Implementation complete
✓

Relevant build passes
✓

Relevant tests pass
✓

Acceptance criteria checked
✓

Public contract changes reviewed
✓

Authorization impact reviewed
✓

Persistence impact reviewed
✓

Final diff reviewed
✓
```

Build success alone is not completion.

---

# 12. Pull Request Target Rule

Correct:

```text
feat/frontend/task-board
→ frontend
```

```text
feat/backend/project-permissions
→ backend
```

```text
feat/ai/impact-engine
→ ai
```

Incorrect under the normal workflow:

```text
feat/backend/project-permissions
→ main
```

Feature branches must normally target their matching domain branch.

---

# 13. Domain-to-Main Integration

The normal path is:

```text
Feature Branch
    ↓
Domain Branch
    ↓
Integration Verification
    ↓
PR
    ↓
main
```

Examples:

```text
feat/backend/project-permissions
→ backend
→ PR backend → main
```

```text
feat/frontend/task-board
→ frontend
→ PR frontend → main
```

---

# 14. Main Integration Rules

Before merging a domain branch into `main`:

- Relevant domain tests must pass.
- Cross-domain contract compatibility must be checked.
- Integration conflicts must be resolved.
- Public API changes must be reviewed.
- Required migrations/config changes must be identified.
- Blocking regressions must not remain.

`main` must remain the most stable branch.

---

# 15. Domain Branch Synchronization

Long-lived domain branches must not drift too far from `main`.

After relevant integrations into `main`, synchronize:

```text
main → frontend
main → backend
main → mobile
main → ai
```

This reduces long-term divergence and merge conflicts.

---

# 16. Sync Before New Feature Work

Before creating a new feature branch:

```bash
git checkout backend
git pull origin backend
git checkout -b feat/backend/example
```

Always start from the latest appropriate domain branch.

---

# 17. Updating an In-Progress Feature Branch

If the domain branch changes significantly while a feature is in progress, update the feature branch before final review.

The team may use merge or rebase according to repository convention.

Never rewrite protected branch history destructively.

---

# 18. Commit Message Convention

Recommended format:

```text
<type>(<domain>): <description>
```

Examples:

```text
feat(backend): add project ownership transfer
fix(frontend): show missing permission reason
feat(ai): add meaningful change filter
test(ai): add reconciliation regression cases
refactor(backend): simplify permission resolver
```

Avoid:

```text
update
fix
changes
done
final
```

---

# 19. Commit Quality Rules

Do not commit:

- Secrets.
- Access tokens.
- Credentials.
- Debug dumps.
- Unnecessary generated files.
- Unrelated formatting changes.
- Dead experimental code.

---

# 20. Pull Request Title Convention

Recommended:

```text
<type>(<domain>): <description>
```

Examples:

```text
feat(backend): implement project permission engine
feat(frontend): add project permission administration
feat(ai): implement Vue API contract extraction
fix(ai): prevent duplicate source-generated tasks
```

---

# 21. Pull Request Description

Every PR should contain:

```text
Summary
- What changed?

Why
- Why is the change required?

Scope
- Which domain/module is affected?

Verification
- What was built/tested?

Contracts
- Any public contract changes?

Permissions
- Any authorization changes?

Dependencies
- Any new dependency?

Known Limitations
- Anything not fully verified?
```

---

# 22. Governing Document Rules

Before a PR is considered complete, the implementation must remain consistent with:

```text
GOAL.md
PRODUCT_CONSTRAINTS.md
PROJECT_PLAN.md
AGENTS.md
WORKFLOW.md
GIT_RULES.md
```

`WORKFLOW.md` defines how work must be verified.

`GOAL.md` defines what the system is building.

`PRODUCT_CONSTRAINTS.md` defines product risks that must not be violated.

`PROJECT_PLAN.md` defines implementation order and dependency boundaries.

---

# 23. AI Coding Agent Branch Rules

A coding agent must identify the task domain before creating a branch.

Example:

```text
Task:
Implement Vue Analyzer

Domain:
ai

Base:
ai

Branch:
feat/ai/vue-analyzer
```

Example:

```text
Task:
Implement Project Owner API

Domain:
backend

Base:
backend

Branch:
feat/backend/project-owner
```

---

# 24. AI Agent Must Not Guess Branch Scope

Before branch creation, inspect:

```text
Task domain
Affected files
Affected module
Correct base branch
```

If a task spans multiple domains, split it into domain-specific tasks and branches whenever practical.

---

# 25. AI Agent Pull Request Rule

After feature implementation:

```text
push branch
    ↓
open Draft PR
    ↓
target matching domain branch
```

The coding agent must not automatically merge unless explicitly authorized.

---

# 26. No Direct Agent Push to Protected Branches

Coding agents must not directly push implementation commits to:

```text
main
frontend
backend
mobile
ai
```

unless an explicit repository-level exception is given.

---

# 27. Review Boundary

The implementation agent must not treat its own code generation as approval.

Required flow:

```text
Implementation
    ↓
Verification
    ↓
Review
    ↓
Merge
```

---

# 28. Merge Strategy

Recommended default for feature PRs:

```text
Squash and Merge
```

when individual feature-branch commit history does not need to be preserved.

The domain-to-main merge strategy may be configured separately.

---

# 29. Delete Short-Lived Branches

After a feature PR is merged, delete the feature branch.

Long-lived branches remain:

```text
main
frontend
backend
mobile
ai
```

---

# 30. Hotfix Rule

Urgent fixes should still identify their domain.

Example:

```text
fix/backend/critical-auth-bypass
```

Normal target:

```text
backend
```

If an emergency requires a direct hotfix to `main`, it is an explicit exception.

Afterward, synchronize the fix back into the relevant domain branch.

No silent divergence is allowed.

---

# 31. Conflict Resolution

When conflicts occur:

1. Understand both changes.
2. Preserve intended behavior from both sides where compatible.
3. Re-run build and tests.
4. Re-check public contracts.
5. Review the final diff.

Do not resolve conflicts by blindly choosing one side.

---

# 32. Cross-Domain Contract Changes

Example:

```text
Backend PR:
POST /api/projects returns owner information

Frontend PR:
consume owner information
```

Use:

```text
backend task → backend branch
frontend task → frontend branch
```

The dependency between the tasks must be explicit.

---

# 33. AI / Repository Intelligence Changes

Changes involving these areas belong primarily to `ai`:

```text
Analyzers
Dependency Graph
Contract Engine
Capability Engine
Impact Engine
Confidence
Explainability
Task Reconciliation
Codex integration
Benchmarks
```

If a feature also requires backend API or Vue UI changes, split those changes into `backend` and `frontend` branches.

---

# 34. Documentation Branches

Pure documentation changes may use:

```text
docs/<domain>/<description>
```

Examples:

```text
docs/backend/permission-model
docs/ai/impact-engine
```

---

# 35. Test Branches

Use:

```text
test/<domain>/<description>
```

Examples:

```text
test/backend/authorization-regression
test/ai/vue-analyzer-fixtures
test/frontend/task-board
```

---

# 36. Refactor Branches

Use:

```text
refactor/<domain>/<description>
```

Refactoring must not silently change public behavior.

---

# 37. Performance Branches

Use:

```text
perf/<domain>/<description>
```

Performance changes should include measurable verification where relevant.

---

# 38. Recommended Flow Summary

```text
                    main
                     ▲
                     │
             Integration PR
                     │
    ┌────────────────┼────────────────┐
    │                │                │
 frontend         backend            ai
    ▲                ▲                ▲
    │                │                │
Draft PR         Draft PR         Draft PR
    │                │                │
feature          feature          feature
branches         branches         branches
```

`mobile` follows the same structure.

---

# 39. Example Frontend Workflow

```text
frontend
    ↓
feat/frontend/task-board
    ↓
Implement Vue task board
    ↓
Build + test
    ↓
Push
    ↓
Draft PR → frontend
    ↓
Review
    ↓
Ready for Review
    ↓
Merge
```

---

# 40. Example Backend Workflow

```text
backend
    ↓
feat/backend/project-permission
    ↓
Implement permission engine
    ↓
Marten + authorization tests
    ↓
Push
    ↓
Draft PR → backend
    ↓
Review
    ↓
Merge
```

---

# 41. Example AI Workflow

```text
ai
    ↓
feat/ai/vue-analyzer
    ↓
Implement analyzer
    ↓
Fixture tests
    ↓
Benchmark/regression verification
    ↓
Push
    ↓
Draft PR → ai
    ↓
Review
    ↓
Merge
```

---

# 42. Multi-Domain Feature Example

Feature:

```text
Source-generated backend tasks displayed in Vue
```

Split into:

```text
feat/ai/task-generation
→ ai
```

```text
feat/backend/task-generation-api
→ backend
```

```text
feat/frontend/task-suggestions
→ frontend
```

Dependency order may be:

```text
AI
↓
Backend API
↓
Frontend UI
```

but each domain keeps its own branch and PR.

---

# 43. Feature Merge Checklist

Before merging a feature branch into its domain branch:

```text
[ ] Correct base branch
[ ] Correct PR target
[ ] Implementation complete
[ ] Relevant build passes
[ ] Relevant tests pass
[ ] Acceptance criteria pass
[ ] Authorization reviewed
[ ] Public contracts reviewed
[ ] Persistence reviewed
[ ] Diff reviewed
[ ] No unrelated changes
[ ] No secrets
[ ] Documentation updated if required
```

---

# 44. Domain-to-Main Checklist

Before merging a domain branch into `main`:

```text
[ ] Domain CI passes
[ ] Integration behavior checked
[ ] Cross-domain contracts compatible
[ ] Required migrations known
[ ] Required configuration known
[ ] No blocking regressions
[ ] Review approved
```

---

# 45. Final Git Rule

The required development path is:

```text
main
    ↓
domain branch
    ↓
feature/fix/refactor/test branch
    ↓
Draft PR
    ↓
domain branch
    ↓
integration verification
    ↓
PR
    ↓
main
```

The coding agent must preserve this workflow unless an explicit repository-level exception is approved.
