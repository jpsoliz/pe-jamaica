# LESSONS_LEARNED

## Purpose

Capture sprint-level learnings that affect future planning, implementation behavior, and QA expectations. Keep short, actionable, and linked to stories.

## Sprint 1 (Epic 1 Foundation)

### What went well
- Repository structure and Case Folder boundary became clear early.
- Contract-first mindset reduced coupling between WPF/C# and Python adapters.
- File-based artifacts improved recoverability and made review/testing easier.

### What to improve
- Keep acceptance evidence artifacts (fixture manifests and expected output contracts) aligned with each story as soon as the story is started.
- Reduce UI asset drift by centralizing icons and resources in one validated path and validating load in package build checks.

### Action items
- Add stronger checks for DAML and resource packaging consistency in readiness script.
- Add a small onboarding checklist to prevent missing preconditions between model handoffs.

## Sprint 2 (Epic 2 Innola Entry + Transaction Controls)

### What went well
- Transaction flow is now significantly stricter and safer (login/session gating, lock behavior, active transaction lifecycle).
- Live API mode is wired while preserving mock mode.
- Task list and transaction detail loading are now operational and tied to session context.

### What to improve
- Keep icon loading and command enable states explicit after model switches to avoid “blank/disabled” confusion.
- Increase deterministic test coverage for transaction state transitions and preflight gate failures.
- Track unresolved UX edge cases (selection highlighting, auto-launch behavior) as story-level acceptance tests before release.

### Action items
- Add 2-8a follow-up regression tests: lock transitions, approval gating, transaction swap denial/release.
- Add explicit “expected artifact” checks for preflight and extraction gate stories before Epic 3 begins.

## Running notes

- Use this file as sprint-agnostic memory; older lessons should remain and be linked to later retrospectives.

## Handoff Learnings (2026-06-11)

### What we observed
- Handoff friction is reduced when model transitions follow a fixed two-step flow (docs-first + explicit next-story handoff).
- Small, dated state snapshots in `CURRENT_SPRINT.md` prevented rework when model context changed.

### What to improve
- Keep the “next 3 actions” block short and deterministic so new model starts with one implementation target.

### Action item
- Add a one-command handoff macro/checklist so future transitions are consistently executed in under 60 seconds.

## Live Innola Integration Learnings (2026-06-11)

### What we observed
- Production Innola access may require both a Windows client certificate and explicit access-token authentication for ArcGIS Pro desktop calls.
- The Innola task endpoint is an eligible queue for the authenticated user's role/group, not a guaranteed all-transactions search.
- User queue parity must be diagnosed with raw returned count vs visible count before changing filters.

### What to improve
- Add a temporary diagnostics surface for live transaction retrieval: raw records, visible records, hidden by process step, hidden by status/loadable/availability.
- Keep role/group names in handoff notes when testing production queues; current target is role `Plan Reviewer (Computation)` and group `Super Group`.

### Action item
- Before Story 2-8 resumes, implement live queue diagnostics and verify whether mismatched records are filtered locally or not returned by Innola.

## Workflow Rule Planning Learnings (2026-06-12)

### What we observed
- Resolving script plans from transaction metadata/source roles is safer when the result is persisted in the Case Folder manifest and checked again during preflight.
- Intake refresh and transaction load must share the same rule-resolution behavior; otherwise reopened or refreshed cases can drift from the selected transaction.
- Secret redaction must happen after settings-token substitution, not only before.

### What to improve
- Future execution stories should consume the persisted `script_plan` rather than re-deriving script routing from scattered conditionals.

### Action item
- In Story 2-8, validate DWG readiness against the resolved script plan and keep extraction execution deferred to Epic 3.
