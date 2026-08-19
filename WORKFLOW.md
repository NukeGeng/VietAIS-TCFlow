# WORKFLOW.md

# Mandatory Agent Implementation Workflow

## 1. Purpose

This file defines the required lifecycle for any agent implementing, modifying, fixing, or reviewing code in this repository.

Every coding task must pass through:

```text
Understand
    ↓
Inspect
    ↓
Plan
    ↓
Define Verification
    ↓
Implement
    ↓
Build
    ↓
Test
    ↓
Runtime Verify
    ↓
Review Diff
    ↓
Report
```

The workflow exists to reduce:

- Architectural drift.
- Hallucinated implementations.
- Unverified code.
- Contract mismatches.
- Authorization bugs.
- Duplicate logic.
- Regression risk.
- Agent output that technically runs but does not satisfy the intended behavior.

---

# 2. Phase 0 — Read Governing Documents

Before touching code:

```text
Read GOAL.md
    ↓
Read AGENTS.md
    ↓
Read WORKFLOW.md
    ↓
Read Task / Feature Spec
```

The agent must understand:

- Product goal.
- Current scope.
- Architecture.
- Technology constraints.
- Permission model.
- Authority model.
- Acceptance criteria.

Output of this phase:

```text
Task Understanding
```

---

# 3. Phase 1 — Restate the Task Internally

Identify:

```text
What needs to change?
Why?
For whom?
Which behavior should exist afterward?
What must remain unchanged?
```

Separate:

```text
Requirement
```

from:

```text
Implementation idea
```

Do not assume the implementation approach before inspecting the repository.

---

# 4. Phase 2 — Locate the Affected Area

Find:

- Relevant module.
- Feature folder.
- Endpoint.
- Vue component.
- DTO.
- Validator.
- Marten document.
- Authorization policy.
- Tests.
- Configuration.
- Related interfaces.
- Related source analyzer artifacts.

Do not inspect only the target file.

Inspect enough neighboring code to understand the local convention.

---

# 5. Phase 3 — Learn Existing Convention

Before writing new code, identify examples of similar functionality already in the repository.

For backend:

```text
Existing endpoint
Existing request DTO
Existing response DTO
Existing validator
Existing Marten usage
Existing authorization
Existing error handling
Existing tests
```

For frontend:

```text
Existing page
Existing component
Existing composable
Existing API service
Existing store
Existing validation
Existing permission check
```

For analyzer:

```text
Existing parser
Existing artifact model
Existing dependency extractor
Existing fixture
Existing analyzer tests
```

Document mentally:

```text
Pattern to follow
```

---

# 6. Phase 4 — Dependency and Impact Analysis

Before implementation, identify what the change can affect.

Check:

```text
Public APIs
Request contracts
Response contracts
Permissions
Database/Marten documents
Frontend consumers
Backend consumers
Tests
Audit
Authority policies
Convention profile
Analyzer output
```

For a public contract change:

```text
Producer
    ↓
Contract
    ↓
Consumers
```

All affected consumers must be considered.

---

# 7. Phase 5 — Authorization Analysis

If the feature touches protected resources:

Determine:

```text
Required permission
Resource scope
Component scope
System vs project scope
Owner/Admin behavior
```

Never reduce this to a role-name check.

Verify expected behavior for:

```text
Unauthenticated
Unauthorized
Authorized
```

---

# 8. Phase 6 — Persistence Analysis

If Marten is involved, determine:

```text
Read or Write?
```

Reads generally use:

```text
IQuerySession
```

Writes generally use:

```text
IDocumentSession
```

Determine:

- Document type.
- Identity.
- Query requirements.
- Transaction boundary.
- SaveChanges requirement.
- Concurrency considerations where relevant.

Do not introduce repository abstractions by default.

---

# 9. Phase 7 — Define Acceptance Criteria

Before coding, write or extract concrete completion conditions.

Example:

```text
Given an authenticated Project Owner
When POST /api/projects is called with valid input
Then:
- project is stored in Marten
- creator becomes Project Owner
- default project state is initialized
- audit entry is created
- response is 201
```

Also define negative paths:

```text
Invalid input → 400
Unauthorized → 401
Forbidden → 403
Missing resource → 404
```

Acceptance criteria must be observable.

---

# 10. Phase 8 — Define Verification Strategy

For each acceptance criterion, determine how it will be verified.

Examples:

```text
Unit test
Integration test
API test
Authorization test
Marten persistence test
Frontend test
Analyzer fixture test
Runtime verification
Manual verification
```

Do this before implementation.

---

# 11. Phase 9 — Create or Update Tests

When practical:

1. Add failing test or reproducible verification.
2. Confirm it represents the required behavior.
3. Implement the smallest change needed to pass it.

For bug fixes, prefer regression tests.

For analyzer changes, add fixtures that reproduce the source pattern.

---

# 12. Phase 10 — Implementation Plan

Create a small implementation plan.

Example:

```text
1. Add permission definition.
2. Add request model.
3. Add endpoint.
4. Add Marten persistence.
5. Add audit record.
6. Add authorization tests.
7. Add API integration tests.
```

Avoid a plan containing unrelated refactoring.

---

# 13. Phase 11 — Implement Smallest Correct Change

Implement only what is necessary to satisfy the task.

Follow existing patterns.

Prefer:

```text
Existing abstraction
```

over:

```text
New abstraction
```

unless the existing architecture genuinely cannot represent the requirement.

---

# 14. Phase 12 — Compile Early

After a meaningful implementation unit:

```text
Build affected project
```

Do not wait until the entire task is complete before discovering compile errors.

Fix compile errors before moving to broader verification.

Build success does not mean task completion.

---

# 15. Phase 13 — Run Focused Tests

Run the smallest relevant test set first.

Example:

```text
Feature tests
Module tests
Authorization tests
Analyzer tests
```

Fix failures before running broader suites.

Do not ignore failures related to the change.

---

# 16. Phase 14 — Run Integration Verification

For backend features, verify as applicable:

```text
ASP.NET pipeline
Marten persistence
PostgreSQL behavior
Authorization
Serialization
OpenAPI
Audit
```

If Aspire is relevant:

```text
Start resources
Verify service health
Verify dependencies
```

---

# 17. Phase 15 — Frontend Verification

For Vue changes:

Verify:

```text
Build
Type checking
API contract
Loading behavior
Error behavior
Permission visibility
Navigation
State update
```

Do not rely only on visual rendering.

If backend contract changed, verify actual frontend integration.

---

# 18. Phase 16 — Contract Verification

If frontend/backend contracts are involved:

Compare:

```text
Expected
    ↕
Actual
```

Check:

- HTTP method.
- Route.
- Request fields.
- Field types.
- Optional/required semantics.
- Response fields.
- Error behavior.
- Pagination.
- Validation.
- Authorization.

Do not mark complete when only one side matches the specification.

---

# 19. Phase 17 — Analyzer Verification

For Vue/ASP.NET/Marten analyzer changes:

Use real or fixture source.

Verify extraction output.

Example:

```text
Input source
    ↓
Analyzer
    ↓
Expected artifact JSON
```

Check:

```text
Artifact type
Route
Method
Input
Output
Dependency
Evidence level
```

Do not verify analyzer behavior only by reading implementation code.

---

# 20. Phase 18 — AI Reasoning Verification

When AI is used:

First verify deterministic context.

Then verify:

```text
Retrieved context
AI request
Structured output
Confidence
Evidence
```

AI must not receive irrelevant full-repository context when targeted retrieval is sufficient.

Check whether the AI result respects:

```text
Convention Profile
Authority Policy
Permissions
Existing Tasks
```

---

# 21. Phase 19 — Task Reconciliation Verification

When source changes generate tasks:

Check whether the system correctly chooses:

```text
Create
Update
Merge
Close
Reopen
Ignore
```

Verify that related existing tasks are considered.

Verify source traceability:

```text
Task
→ Change
→ Artifact
→ Evidence
```

---

# 22. Phase 20 — Runtime Quality Verification

For runtime-sensitive features, define measurable verification.

Examples:

```text
Latency
Throughput
Dropped events
Retry behavior
Memory
CPU
Queue length
Connection failures
```

For streaming/audio/realtime features, "it runs" is never sufficient verification.

---

# 23. Phase 21 — Security Verification

Check:

- Secrets are not logged.
- Tokens are not exposed.
- Permission checks exist server-side.
- Frontend-only permission hiding is not relied upon.
- Project Owner cannot access system permission definitions beyond allowed project permissions.
- Project boundaries are enforced.
- Cross-project access is rejected.

---

# 24. Phase 22 — Audit Verification

If the action is administratively or security sensitive, verify audit behavior.

Examples:

```text
Role changed
Permission changed
Owner transferred
AI permission changed
Authority changed
Repository access changed
AI changed task
```

Audit should capture enough information to understand:

```text
Who
What
When
Target
Before
After
```

---

# 25. Phase 23 — Full Relevant Test Suite

Once focused verification passes:

Run the broader relevant test suite.

Examples:

```text
Module suite
Backend suite
Frontend suite
Analyzer suite
Integration suite
```

Do not run unrelated expensive suites unless needed.

---

# 26. Phase 24 — Review the Final Diff

Review every changed file.

Check:

```text
Is this file required?
Is the change scoped?
Is naming consistent?
Is there duplication?
Is a public contract changed?
Is authorization correct?
Is persistence correct?
Are cancellation tokens preserved?
Are tests meaningful?
Was any test weakened?
Did any generated file change accidentally?
```

Remove accidental edits.

---

# 27. Phase 25 — Re-check Acceptance Criteria

Return to the original acceptance criteria.

For each item:

```text
PASS
FAIL
NOT VERIFIED
```

Do not convert `NOT VERIFIED` into `PASS`.

---

# 28. Phase 26 — Final Task Report

The final report should include:

## Summary

What was implemented.

## Changed Areas

Which modules/files were affected.

## Verification

Example:

```text
Build: PASS
Unit tests: PASS
Integration tests: PASS
Runtime check: PASS
```

## Contracts

Any public contract changes.

## Permissions

Any permission or scope changes.

## Persistence

Any Marten document/query/write changes.

## Dependencies

Any package changes.

## Known Limitations

Anything not fully verified.

---

# 29. Bug Fix Workflow

For a bug:

```text
Reproduce
    ↓
Identify root cause
    ↓
Add regression verification
    ↓
Fix smallest scope
    ↓
Run focused verification
    ↓
Run broader regression tests
    ↓
Review diff
```

Do not fix symptoms without understanding the failure when root-cause analysis is practical.

---

# 30. New Feature Workflow

For a feature:

```text
Understand capability
    ↓
Identify contract
    ↓
Identify authority
    ↓
Identify permissions
    ↓
Identify persistence
    ↓
Define acceptance criteria
    ↓
Add tests
    ↓
Implement
    ↓
Verify
```

---

# 31. Permission Feature Workflow

For a new permission:

```text
Define Permission Code
    ↓
Classify System or Project Scope
    ↓
Define Resource Scope
    ↓
Add Permission Definition
    ↓
Enforce Backend Authorization
    ↓
Expose Effective Permission
    ↓
Update Vue UI behavior
    ↓
Add Positive + Negative Tests
    ↓
Audit if administrative
```

---

# 32. Project Owner Workflow

When creating a project:

```text
User creates project
    ↓
Project persisted
    ↓
Creator becomes Project Owner
    ↓
Default project state created
    ↓
Default AI policy created
    ↓
Default authority/convention state created
    ↓
Audit entry created
```

Project Owner may then:

```text
Invite members
Create custom roles
Assign permissions
Assign repository/component access
Configure AI
Configure authority
Configure conventions
```

---

# 33. System Admin Workflow

System Admin operates at platform scope.

Typical workflow:

```text
System Admin
    ↓
System Administration
    ↓
Users / Projects / Permission Definitions / Global Settings / Audit
```

System Admin may inspect and manage platform-level entities without becoming the project-level Owner.

---

# 34. Repository Connection Workflow

Initial local workflow:

```text
Select local repository
    ↓
Validate path
    ↓
Detect technologies
    ↓
Full scan
    ↓
Build repository model
```

GitHub workflow:

```text
Login GitHub
    ↓
Install GitHub App
    ↓
Select repository
    ↓
Grant repository access
    ↓
Initial scan
```

---

# 35. Repository Scan Workflow

```text
File Discovery
    ↓
Technology Detection
    ↓
Vue Analyzer
    ↓
ASP.NET Analyzer
    ↓
Marten Analyzer
    ↓
Artifact Extraction
    ↓
Dependency Extraction
    ↓
Convention Detection
    ↓
Knowledge Graph Build
    ↓
Persist Analysis
```

---

# 36. Incremental Repository Workflow

After the first scan:

```text
Git Event
    ↓
Changed Files
    ↓
Meaningful Change Filter
    ↓
Incremental Parse
    ↓
Update Artifacts
    ↓
Update Dependencies
    ↓
Impact Candidate Search
    ↓
Targeted Retrieval
    ↓
AI Reasoning if required
    ↓
Task Reconciliation
```

Do not re-scan the full repository unless required.

---

# 37. Frontend-only Planning Workflow

```text
Vue Source
    ↓
Extract UI Actions
    ↓
Extract API Calls
    ↓
Extract Request Usage
    ↓
Extract Response Usage
    ↓
Infer Capability
    ↓
Resolve Evidence Level
    ↓
Create Backend Contract Candidate
    ↓
Generate Backend Tasks
```

If no concrete API evidence exists, mark contract proposals as:

```text
INFERRED
or
PROPOSED
```

---

# 38. Backend-only Planning Workflow

```text
ASP.NET Source
    ↓
Extract Endpoints
    ↓
Extract Request/Response
    ↓
Extract Validation
    ↓
Extract Authorization
    ↓
Extract Business Capability
    ↓
Generate Frontend Capability Plan
```

Do not invent visual UX without repository evidence or explicit specification.

---

# 39. Dual-side Contract Workflow

When both frontend and backend exist:

```text
Frontend Expected Contract
        ↕
Backend Actual Contract
        ↓
Compare
        ↓
Match / Mismatch
        ↓
Apply Authority Policy
        ↓
Impact
        ↓
Task
```

---

# 40. Authority Workflow

Example mismatch:

```text
Frontend expects categoryId
Backend does not accept categoryId
```

If:

```text
API Authority = Frontend
```

then consider backend impact.

If:

```text
API Authority = Backend
```

then consider frontend alignment/conflict.

If:

```text
API Authority = OpenAPI
```

compare both implementations with OpenAPI.

---

# 41. Convention Detection Workflow

```text
Repository Scan
    ↓
Observe Patterns
    ↓
Find Repeated Structures
    ↓
Build Convention Profile
```

Examples:

```text
Feature-based folders
Minimal APIs
Static validators
Marten sessions
Request/Response naming
Vue composables
API service pattern
```

AI-generated implementation plans must follow the convention profile.

---

# 42. Task Generation Workflow

```text
Evidence
    ↓
Capability
    ↓
Contract
    ↓
Dependency
    ↓
Authority
    ↓
Convention
    ↓
Impact
    ↓
Existing Task Search
    ↓
AI Policy
    ↓
Suggest or Create Task
```

---

# 43. Task Lifecycle Workflow

```text
Upcoming
    ↓
In Progress
    ↓
Ready for Review
    ↓
Completed
```

Optional terminal/intermediate states:

```text
Blocked
Rejected
Cancelled
```

State transitions must respect project permissions.

---

# 44. Source-Driven Task Update Workflow

When source changes while a task exists:

```text
Source Change
    ↓
Find Related Task
    ↓
Compare New Evidence
    ↓
Update Requirements
    ↓
Update Confidence
    ↓
Update Impact
```

Do not blindly create another task.

---

# 45. Revert Workflow

```text
Change A
    ↓
Task generated
    ↓
Change A reverted
    ↓
Detect revert
    ↓
Re-evaluate task
    ↓
Close / Cancel / Reopen / Keep
```

The decision should be explainable.

---

# 46. Task Completion Verification Workflow

Developer marks work ready:

```text
Task Requirements
      ↓
Actual Source
      ↓
Static Analysis
      ↓
Contract Comparison
      ↓
Missing Requirements?
```

If yes:

```text
Remain In Progress / Request Changes
```

If no:

```text
Ready for Review
```

Human review remains authoritative where policy requires it.

---

# 47. AI Usage Escalation Workflow

Use the cheapest reliable method first.

```text
Deterministic Analysis
        ↓
Enough information?
   ┌────┴────┐
  Yes       No
   │         │
Return     Targeted AI
Result     Reasoning
```

Do not call AI for trivial deterministic facts.

---

# 48. Failure Escalation Workflow

If a task cannot be completed:

```text
Failure
    ↓
Reproduce
    ↓
Diagnose
    ↓
Can fix safely?
  ┌────┴────┐
 Yes       No
  │         │
Fix       Report Blocker
```

Never hide unresolved failures.

---

# 49. Definition of Done

A task is Done only if:

```text
Requirements understood
✓

Architecture respected
✓

Acceptance criteria satisfied
✓

Build passes
✓

Relevant tests pass
✓

Runtime behavior verified where applicable
✓

Authorization verified where applicable
✓

Persistence verified where applicable
✓

Public contracts reviewed
✓

Audit behavior reviewed
✓

Final diff reviewed
✓

No relevant verification skipped silently
✓
```

---

# 50. Core Principle

The workflow exists to ensure that the agent does not merely produce code.

The agent must produce:

> **Verified implementation that matches the repository's architecture, product specification, permissions, conventions, and observable acceptance criteria.**
