# PRODUCT_CONSTRAINTS.md

# Product Constraints and Risk Controls

## 1. Purpose

This document defines the product-level constraints that must be respected while implementing the Source-Aware Engineering Planner.

`GOAL.md` describes what the product should become.

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

# 9. Constraint 07 — Do Not Become a Weak Jira/Linear Clone

## Risk

A generic task-management UI is not enough to differentiate the product.

## Required behavior

The product must remain centered on:

```text
Source
→ Impact
→ Engineering Plan
```

Kanban is only a presentation and workflow layer.

## Required design

The source intelligence engine must remain the core differentiator.

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
Convention Profile
Authority Policy
Explainable Impact
Task Reconciliation
Permission-aware Automation
Source Traceability
```

AI reasoning is only one layer.

---

# 16. Progressive Trust

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

# 17. User Control

The system must not take irreversible or high-impact actions without the configured permission and trust policy.

Human users must always be able to understand:

```text
What happened?
Why?
What evidence caused it?
Can I reject or revert it?
```

---

# 18. Quality Decision Rule

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
```

If yes, the design should be reconsidered.

---

# 19. Definition of Acceptable Product Behavior

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
```

---

# 20. Final Product Constraint

The product must optimize for developer trust.

Developer trust is considered more important than:

- Maximum automation.
- Maximum number of generated tasks.
- Maximum AI usage.
- Maximum feature count.
