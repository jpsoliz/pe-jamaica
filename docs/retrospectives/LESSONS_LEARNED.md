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
