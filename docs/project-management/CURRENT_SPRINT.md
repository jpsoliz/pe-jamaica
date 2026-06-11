# CURRENT_SPRINT

**Last Updated:** 2026-06-11 (handoff checkpoint)
**Sprint Owner:** Sid-well / ArcGIS Pro Parcel Workflow Add-in Team  
**Current Sprint Theme:** Epic 2 — Innola Transaction Entry, Preflight Readiness, and Processing Setup

## 1) Current Objective

Stabilize transaction-driven workflow initialization by completing remaining Epic 2 handoff work so the user can:
- login with session-only credentials,
- select available Innola tasks,
- load a transaction and attachments into Case Folder,
- enforce transaction lifecycle rules and panel locking,
- keep live API integration while preserving mock mode.

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

## 4) Backlog

### Story backlog
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

## 6) Next Actions

1. Finalize and start 2-8-validate-dwg-readiness-when-present.
2. Implement 2-9-configure-processing-and-credential-profiles.
3. Implement 2-10-display-preflight-results-and-gate-extraction.
4. Run code review + acceptance checks before moving Epic 2 to done.
5. Update this file with outcome and risks before starting Epic 3.

## 8) Handoff Snapshot (for model transition)

- **Timestamp:** 2026-06-11  [handoff checkpoint]
- **Current status:** Epic 2 complete through 2.8A; next active backlog remains 2-8, 2-9, 2-10.
- **Active risk:** Build/package validation still depends on ArcGIS Pro SDK/MSBuild environment parity across models.

## 7) Key links

- Current status source: `_bmad-output/implementation-artifacts/sprint-status.yaml`
- Epic definitions: `_bmad-output/planning-artifacts/epics.md`
- Reference architecture: `_bmad-output/planning-artifacts/architecture.md`
- Tooling: `docs/toolchain.md`
