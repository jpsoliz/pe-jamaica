# DECISIONS

## Purpose

Use this as the permanent, append-only decision log for product, architecture, and implementation choices that matter across model/model handoffs.

## Decision Template

- **ID:** DEC-YYYY-NNN
- **Date:** YYYY-MM-DD
- **Owner:** Team / model / reviewer
- **Status:** Accepted | Superseded | Rejected
- **Context:** short problem statement
- **Decision:** concise chosen approach
- **Rationale:** why this choice was made
- **Alternatives considered:** alternatives + rejection reason
- **Impacts:** architecture, test, docs, UX, ops
- **Revisions:** links to follow-up PR/stories if changed later

## Accepted Decisions

- **DEC-2026-001** — 2026-06-08 — **Architecture lane and integration model**
  - **Owner:** Team + Amelia + Mary
  - **Status:** Accepted
  - **Decision:** Use ArcGIS Pro Module Add-in + Dockpane for v1 (C# orchestration), keep Python/ArcPy as processing layer.
  - **Rationale:** Product must run inside ArcGIS Pro and needs map/layer operations, long-running safe UI state, and contract-based processing.
  - **Impacts:** `src/ParcelWorkflowAddIn/`, `src/ProcessingTools/`, `src/Contracts/`.

- **DEC-2026-002** — 2026-06-08 — **Case Folder as system of record**
  - **Owner:** Product/Architecture
  - **Status:** Accepted
  - **Decision:** Treat local transaction Case Folder as primary durable state; avoid hidden ArcGIS-only state for recovery/audit.
  - **Rationale:** Recoverability and auditability across crashes, restarts, and partial failures.
  - **Impacts:** `src/ParcelWorkflowAddIn/CaseFolders/`, `manifest.json`, artifact naming conventions.

- **DEC-2026-003** — 2026-06-08 — **Review gating and review hash**
  - **Owner:** Team
  - **Status:** Accepted
  - **Decision:** Enforce explicit review approval and validation/output gates; approve must be tied to review hash/version and invalidate when review changes.
  - **Impacts:** Workflow state machine and validation adapter invocation logic.

- **DEC-2026-004** — 2026-06-08 — **Cadindex sync scope in v1**
  - **Owner:** Product/Architecture
  - **Status:** Accepted
  - **Decision:** Keep CADINDEX/Enterprise sync as no-op facade/readiness indicator only in v1.
  - **Rationale:** Avoid dependency explosion and keep release scope controlled while preserving evolution path.
  - **Impacts:** `Sync` boundary, reports, and readiness states.

- **DEC-2026-005** — 2026-06-11 — **Command ordering and UI discoverability**
  - **Owner:** Product/Developer
  - **Status:** Accepted
  - **Decision:** Set ribbon order to Login → Transaction Panel → Parcel Workflow → Configuration → About.
  - **Rationale:** Matches operator expected flow and reduces accidental mis-clicking.
  - **Impacts:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`.

- **DEC-2026-006** — 2026-06-11 — **Keep model in mock mode + live mode**
  - **Owner:** Team
  - **Status:** Accepted
  - **Decision:** Preserve mock mode for offline/dev while enabling live Innola API path; switchable by settings/runtime.
  - **Rationale:** De-risks validation and allows deterministic testing.
  - **Impacts:** `src/ParcelWorkflowAddIn/Innola/*` services and session bootstrap.

- **DEC-2026-009** — 2026-06-11 — **Use explicit token auth plus client certificate for live Innola desktop calls**
  - **Owner:** Team + current model
  - **Status:** Accepted
  - **Decision:** ArcGIS Pro desktop live API calls use explicit access-token/bearer authentication and attach the configured Windows client certificate for environments that require certificate authentication.
  - **Rationale:** Browser-only session/cookie authentication is unreliable for the desktop add-in; production Innola access requires certificate selection before authenticator login.
  - **Impacts:** `WorkflowSettings.json`, `InnolaAuthService`, `InnolaHttpClientFactory`, live transaction/task services.

- **DEC-2026-010** — 2026-06-11 — **Treat Innola task list as eligible queue, not global transaction search**
  - **Owner:** Team + current model
  - **Status:** Accepted
  - **Decision:** The add-in should use the Innola available-task queue for the authenticated user role/group and should not assume it can retrieve all transactions globally.
  - **Rationale:** Swagger describes `/api/v4/rest/workflow/my-tasks` as tasks assigned to or claimable by the current user through role/group permissions.
  - **Impacts:** Transaction panel expectations, live diagnostics, future admin/debug queue design.

- **DEC-2026-011** — 2026-06-12 — **Resolve workflow rules from JSON and persist script plans in Case Folder manifest**
  - **Owner:** Team + current model
  - **Status:** Accepted
  - **Decision:** Workflow script selection is driven by `Settings/WorkflowRules.json`; the add-in resolves a rule from transaction type/process step/source roles and persists a versioned `script_plan` in the Case Folder manifest.
  - **Rationale:** Keeps future transaction/script routing configurable and audit-friendly while preserving the boundary that Story 2.11 plans scripts but does not execute extraction.
  - **Impacts:** `WorkflowRules/*`, `WorkflowRules.json`, `ManifestDocument`, `InnolaTransactionLoadService`, `WorkflowSession.RefreshInputProfile`, preflight gating.

## Pending / follow-up decisions

- **DEC-2026-007** — 2026-06-xx — **Credential storage strategy beyond v1**
  - **Owner:** Security/Architecture
  - **Status:** Accepted (v1 constraint noted)
  - **Decision:** v1 may keep plaintext local credential profile for local config; production hardening to DPAPI/managed vault to be planned.
  - **Impacts:** hardening work in a later release train.

- **DEC-2026-008** — 2026-06-11 — **No new decision in this handoff window**
  - **Owner:** Current model / transition steward
  - **Status:** Superseded
  - **Revision 2026-06-11:** Superseded by DEC-2026-009 and DEC-2026-010 after live Innola certificate and queue-scope findings.
  - **Decision:** No architectural or scope decisions were added before handoff; maintain current constraints and continue stories 2-8/2-9/2-10.
  - **Rationale:** Session focused on process continuity, onboarding standardization, and handoff readiness.
  - **Impacts:** `docs/project-management/*.md`, `docs/project/ONBOARDING.md`.

## Deferred / Open

- Exact 0-10 scoring formula pending fixture calibration.
- Final 3.7-exclusive API usages must be gated during final upgrade decisions.
- No new process risks identified beyond existing build/toolchain environment dependence.
