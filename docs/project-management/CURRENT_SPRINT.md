# CURRENT_SPRINT

**Last Updated:** 2026-06-12 (Story 2.11 completion checkpoint)
**Sprint Owner:** Sid-well / ArcGIS Pro Parcel Workflow Add-in Team  
**Current Sprint Theme:** Epic 2 — Innola Transaction Entry, Preflight Readiness, and Processing Setup

## 1) Current Objective

Stabilize transaction-driven workflow initialization so the user can:
- login with session-only credentials,
- select available Innola tasks,
- load a transaction and attachments into Case Folder,
- enforce transaction lifecycle rules and panel locking,
- keep live API integration while preserving mock mode,
- resolve a workflow rule/script plan from transaction type + copied source files before extraction begins.

## 2) In Scope

- Keep the ArcGIS Pro 3.6/3.7 implementation boundary intact.
- Continue integration hardening in Epics/Stories:
  - 2-8-validate-dwg-readiness-when-present
  - 2-9-configure-processing-and-credential-profiles
  - 2-10-display-preflight-results-and-gate-extraction
- Maintain contract-first architecture and Case Folder as system-of-record.
- Keep user command gating behavior aligned with:
  - logged-out: Login/Parcel/About only
  - logged-in no transaction: Login/Transaction/Configuration/About
  - active transaction locked: explicit save/cancel or continue path only

## 3) Done / Accepted

- Epic 2 story set from 2.1 through 2.8A marked done in `sprint-status.yaml`.
- Ribbon/button order and live transaction control updates have been stabilized in add-in behavior.
- Transaction details + attachments are now part of transaction load path.
- Mock mode remains available and selectable.
- Live Innola API path now supports the client-certificate requirement for the Jamaica eTitles certificate and uses explicit access-token authentication.
- Transaction panel footer reports user, server, mode, client certificate, and retrieved record count.
- Story 2.11 is done: workflow rule registry, Scenario A two-PDF and Scenario B DWG-aware script plan resolution, manifest persistence, preflight plan gating, intake-refresh re-resolution, and post-substitution secret redaction are implemented.
- Readiness passed on 2026-06-12: validation, Python tests, .NET build, 142-test console harness, and add-in package generation.

## 4) Backlog

### Story backlog
- Live transaction diagnostics: show raw returned count vs visible count and hidden-by-reason counts before changing filters.
- 2-8-validate-dwg-readiness-when-present
- 2-9-configure-processing-and-credential-profiles
- 2-10-display-preflight-results-and-gate-extraction

### Upcoming epics
- Epic 3: Extraction & review before output (stories 3.1–3.5)
- Epic 4: Validation & manual process routing
- Epic 5: Output package + map integration
- Epic 6: Sync readiness / audit / fixtures

## 5) Risks / Blockers

- Add-in packaging/build environment must support ArcGIS Pro SDK targets (MSBuild required).
- Any model switch (3.6 vs 3.7 lane) must keep package assets and settings compatible.
- ArcGIS/Innola integration behavior should be verified in both mock and live modes per story.
- Live queue mismatch risk: Innola `/workflow/my-tasks` returns only tasks available to the authenticated user's roles/groups, and the add-in may still hide server-returned tasks via `innola_process_step`, availability, loadable, or status filters.
- Workflow rule/script plan is now manifest-backed, but later execution stories must keep the “plan only until execution story” boundary intact.

## 6) Next Actions

1. Start Story 2-8: validate DWG readiness when a DWG reference is present.
2. Keep Story 2.11 rule-plan behavior intact while adding DWG readiness checks; do not execute extraction yet.
3. Verify mock and live-mode Case Folder manifests still persist `workflow_profile`, `workflow_rule_id`, `workflow_rule_version`, and `script_plan`.
4. Run code review + acceptance checks before moving Epic 2 to done.
5. Update this file with outcome and risks before starting Epic 3.

## 8) Handoff Snapshot (for model transition)

- **Timestamp:** 2026-06-12 [Story 2.11 completion checkpoint]
- **Current status:** Epic 2 complete through 2.8A plus 2.11; 2-8 remains the next ready-for-dev story.
- **Active risk:** Next stories must not accidentally start extraction execution before planned Epic 3 execution scope.

## 7) Key links

- Current status source: `_bmad-output/implementation-artifacts/sprint-status.yaml`
- Epic definitions: `_bmad-output/planning-artifacts/epics.md`
- Reference architecture: `_bmad-output/planning-artifacts/architecture.md`
- Tooling: `docs/toolchain.md`
