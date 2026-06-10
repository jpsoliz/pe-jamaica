---
workflowType: correct-course
project_name: Sid-jamaica
date: 2026-06-10
status: approved
change_scope: moderate
trigger: Innola-authenticated transaction workflow required before parcel processing
---

# Sprint Change Proposal - Innola Transaction Workflow

## 1. Issue Summary

During ArcGIS Pro add-in testing after Epic 1 and Story 2.1, the working scaffold proved that local Case Folder creation, intake, reopen/resume, source files, and preflight contracts can work. The product direction now needs a stronger operational entry point: users should not normally create a parcel workflow case manually. They should log into the Innola framework, see only transactions available to their user or group for the current process step, select one transaction, download its attachment metadata/files, and then run the parcel workflow inside the Case Folder created from that transaction.

This is a new stakeholder requirement and a correction to the current backlog. The current PRD, UX, architecture, and epics assume manual case creation as the primary intake path. That conflicts with the required Innola-controlled workflow and transaction ownership rules.

Evidence:

- Target Innola endpoint for initial configuration: `https://eltrs.innola-solutions.com/`.
- Passwords may be retained only in ArcGIS Pro session memory, not persisted.
- Transaction list must include tasks assigned to the logged-in user or their group and only those available for the current process step.
- Transaction attachments must come from Innola metadata that includes file id, file name, extension/type, role/category where available, size/hash where available, and a download endpoint or tokenized retrieval id.
- The user who starts/claims a transaction is the only user who can complete it.
- A new transaction can load only when the current process is cancelled or the full Case Folder has been saved.
- Completion should happen through an explicit Complete action after data is synced or confirmed ready for Enterprise.

## 2. Impact Analysis

### Epic Impact

Epic 1 remains useful but should be reframed. Its completed local Case Folder capabilities become the local workspace primitive used after a transaction is selected from Innola, rather than the normal user-facing start point.

Epic 2 must be expanded before continuing. Story 2.1 is still valid because manifest preflight operates on the Case Folder once transaction attachments are loaded. However, the current backlog should insert Innola login, transaction list, attachment download, active-transaction control, and completion-gate stories before ArcGIS/Python environment and DWG-specific preflight stories.

Epics 3-6 remain directionally valid, but their user identity, transaction ownership, audit trail, and final completion assumptions must reference the Innola transaction/session context.

### Story Impact

Completed stories do not need rollback:

- `1.1` add-in scaffold remains valid.
- `1.2` Case Folder creation remains valid as an internal/service-driven operation.
- `1.3` source file copy remains valid, but source files should normally come from Innola attachment downloads rather than only manual browse.
- `1.4` profile detection remains valid after attachment download.
- `1.5` reopen/resume remains valid for saved Case Folders.
- `1.6` open/reveal source files remains valid.
- `2.1` manifest preflight remains valid.

New or shifted Epic 2 stories are needed:

1. `2.2` Add Sidwell shell, Innola login, and session gating.
2. `2.3` Display available Innola transactions for logged-in user/group.
3. `2.4` Load Innola transaction details and attachments into the Case Folder.
4. `2.5` Control active transaction lifecycle, save/cancel, claim/ownership, and Complete gate.
5. `2.6` Validate ArcGIS Pro and Python processing environment. Former `2.2`.
6. `2.7` Validate DWG readiness when present. Former `2.3`.
7. `2.8` Configure processing and credential profiles. Former `2.4`, narrowed so local configuration is user-visible and general/server configuration is hidden.
8. `2.9` Display preflight results and gate extraction. Former `2.5`.

### Artifact Conflicts

PRD conflicts:

- FR1 currently says the user creates a new submission Case Folder by entering or confirming a transaction identifier. This should become "selects an Innola transaction, then the add-in creates or reopens the Case Folder from transaction metadata."
- FR21/NFR3 currently allow v1 plaintext local credential configuration. This must be narrowed for Innola login: password may live in memory during the ArcGIS Pro session only; tokens must not be logged or written to Case Folder artifacts.
- MVP scope needs explicit Innola authentication, available transaction list, attachment loading, ownership/claim, and completion gate requirements.

Architecture conflicts:

- Add an `InnolaIntegration` boundary for auth/session, task list, transaction detail, attachment metadata/download, claim/start, save state, and complete.
- Add a session state layer separate from the parcel workflow state machine. Recommended shell states: `logged_out`, `authenticating`, `logged_in`, `transaction_loading`, `transaction_loaded`, `transaction_in_progress`, `transaction_complete_pending`, `completed`.
- Case Folder remains the local system of record, but `manifest.json` must include Innola transaction id/no, task id, assigned user/group, claimed-by/started-by, attachment metadata, source download references, and completion status.
- Local settings must distinguish user-visible local preferences from hidden/server-managed Innola configuration.

UX conflicts:

- Current dock pane mockups start from "New Transaction" and manual file rows. The first operational screen now needs a Sidwell shell with Login, Transaction Panel, Configuration, Parcel Workflow, and About.
- Transaction Panel and Parcel Workflow must be disabled until login succeeds.
- Parcel Workflow must remain disabled until a transaction is selected, loaded, attachment metadata is valid, and the Case Folder is ready.
- The transaction list should follow the Innola task panel pattern: compact toolbar, refresh/open controls, filter, search, sorting, and rows showing transaction number, task name, assignee/requestor/group, and received timestamp.
- Loading a new transaction needs an interruption guard: save full Case Folder, cancel current process, or stay on current transaction.

### Technical Impact

The existing add-in can absorb this change. It needs new service interfaces and UI gating but not a new platform.

Recommended API shape for the add-in:

- `POST /auth/login` returns access token, refresh/ping metadata, user identity, groups, and roles.
- `GET /tasks/available?process_step=parcel_workflow` returns only user/group-eligible transactions.
- `GET /transactions/{transaction_id}` returns transaction/task metadata, case type, ownership/claim status, and attachment manifest.
- `GET /transactions/{transaction_id}/attachments` returns attachment metadata, including stable id, name, extension, mime type, role/category, size, checksum if available, created/updated timestamps, and download capability.
- `GET /transactions/{transaction_id}/attachments/{attachment_id}/content` downloads the file into the Case Folder `source` area.
- `POST /transactions/{transaction_id}/claim` starts/claims the task for the current user.
- `POST /transactions/{transaction_id}/save-state` optionally records remote progress metadata while the full local Case Folder remains the recoverable state.
- `POST /transactions/{transaction_id}/complete` completes only after parcel workflow sync/readiness criteria pass and only for the user who claimed/started the transaction.

## 3. Recommended Approach

Recommended path: Direct Adjustment.

This is a moderate scope correction. It requires backlog reorganization, PRD/architecture/UX amendments, and new Epic 2 stories, but it does not invalidate completed implementation. Rolling back would lose useful Case Folder, manifest, intake, and preflight work without simplifying the new Innola requirement.

Effort estimate: Medium.

Risk level: Medium.

Primary risks:

- Innola API contracts are not yet confirmed from Swagger in this environment.
- Transaction ownership/claim semantics must match server behavior.
- Attachment role mapping may be incomplete unless Innola metadata provides source roles or enough type/category hints.
- Session-only credentials must be implemented carefully to avoid accidental persistence or logging.

Mitigation:

- Implement Innola integration behind interfaces and mockable service contracts.
- Start with configuration endpoint `https://eltrs.innola-solutions.com/`.
- Define minimal attachment metadata contract in the story, then adapt to actual API response.
- Keep Case Folder as local recovery truth and use Innola only for session/task/attachment/completion coordination.

## 4. Detailed Change Proposals

### PRD Changes

FR1 current:

> Create a new submission Case Folder from the ArcGIS Pro add-in by entering or confirming transaction identifier, source profile, coordinate/profile settings, and output location.

FR1 proposed:

> Create or reopen a submission Case Folder from an Innola transaction selected in the ArcGIS Pro add-in after user login. The transaction supplies the transaction identifier, case type/profile metadata, and attachment references; the add-in stores downloaded source files and workflow artifacts in the local Case Folder.

Add functional requirements:

- The add-in must authenticate against the configured Innola environment and retain credentials only for the current ArcGIS Pro session.
- The add-in must show only transactions available to the logged-in user or their group for the relevant workflow step.
- The add-in must load transaction metadata and attachment metadata before enabling Parcel Workflow.
- The add-in must prevent switching transactions unless the current process is cancelled or the full Case Folder is saved.
- The add-in must expose a Complete action only after required sync/readiness criteria pass and only for the user who started/claimed the transaction.

### Epic/Story Changes

Epic 1 title proposed:

Old: Case Intake & Transaction Workspace

New: Local Case Folder & Transaction Workspace Foundation

Epic 2 title proposed:

Old: Preflight Readiness & Processing Setup

New: Innola Transaction Entry, Preflight Readiness & Processing Setup

Insert Epic 2 stories before environment/DWG preflight:

- `2.2` Add Sidwell shell, Innola login, and session gating.
- `2.3` Display available Innola transactions.
- `2.4` Load transaction details and attachments into Case Folder.
- `2.5` Control active transaction lifecycle and completion gate.

Shift current remaining Epic 2 backlog:

- `2.2` becomes `2.6`
- `2.3` becomes `2.7`
- `2.4` becomes `2.8`
- `2.5` becomes `2.9`

### Architecture Changes

Add components:

- `InnolaIntegration/Auth`: login, token/session state, refresh/ping, logout.
- `InnolaIntegration/Transactions`: available task list, transaction details, ownership/claim, complete.
- `InnolaIntegration/Attachments`: metadata retrieval and streamed download into Case Folder.
- `Shell/SidwellRibbonState`: command enable/disable state based on login and active transaction.

Update contracts:

- `manifest.json` must include Innola metadata and attachment provenance.
- Audit trail must record login identity, transaction load, attachment download, claim/start, save/cancel, and complete events without logging tokens or passwords.

### UX Changes

Update the ArcGIS Pro ribbon to use the Sidwell Co group and command set:

- Login
- Transaction Panel
- Configuration
- Parcel Workflow
- About

Logged-out state:

- Login and About enabled.
- Configuration enabled only for safe local preferences.
- Transaction Panel and Parcel Workflow disabled.

Logged-in state:

- Transaction Panel enabled.
- Parcel Workflow disabled until a transaction is selected and loaded.

Transaction loaded state:

- Parcel Workflow enabled.
- Transaction Panel must guard loading a different transaction with Save Case Folder, Cancel Current Process, or Stay.

## 5. Implementation Handoff

Scope classification: Moderate.

Handoff recipients:

- Analyst/Product Owner: approve the changed workflow and update PRD/epics/sprint status.
- Architect: update architecture with Innola integration boundaries, session state, API contracts, and security rules.
- UX Designer: update mockups for login, transaction panel, gated shell, and transaction-loaded parcel workflow state.
- Developer: implement new Epic 2 stories after approval.

Recommended immediate sequence after approval:

1. Update `epics.md` and `sprint-status.yaml` with the new Epic 2 story sequence.
2. Create Story `2.2` for Sidwell shell, login, and command gating.
3. Implement Story `2.2` by reusing patterns from the previous Innola add-in while avoiding persistent password storage.
4. Create and implement Story `2.3` for transaction listing using a mockable Innola API client if live API details are unavailable.
5. Create and implement Story `2.4` for transaction details and attachment download into Case Folder.

Success criteria:

- Logged-out users cannot open Transaction Panel or Parcel Workflow.
- Login succeeds against configured Innola environment or fails with a clear non-secret error.
- Transaction list shows only user/group-eligible available tasks.
- Selecting a transaction creates/reopens a Case Folder with transaction metadata and downloaded attachment source files.
- Parcel Workflow is enabled only after transaction load validation passes.
- Switching active transaction is blocked unless the current process is saved or cancelled.
- Complete is available only after the configured downstream sync/readiness gate and only for the user who started/claimed the transaction.

## 6. Checklist Status

- [x] 1.1 Triggering story identified: testing after Epic 1 and Story 2.1 revealed manual case creation is not the desired operational entry point.
- [x] 1.2 Core problem defined: new stakeholder requirement and original intake-flow mismatch.
- [x] 1.3 Supporting evidence gathered from user-provided endpoint, session password rule, transaction assignment rule, attachment metadata needs, save/cancel rule, and ownership rule.
- [x] 2.1 Current epic assessed: Epic 2 requires inserted stories before continuing.
- [x] 2.2 Epic changes identified: Epic 1 reframed; Epic 2 expanded and renamed.
- [x] 2.3 Remaining epics reviewed: Epics 3-6 remain valid with Innola context additions.
- [x] 2.4 No epic is obsolete; no new top-level epic required.
- [x] 2.5 Story order should change inside Epic 2.
- [x] 3.1 PRD conflicts identified.
- [x] 3.2 Architecture conflicts identified.
- [x] 3.3 UX conflicts identified.
- [x] 3.4 Secondary artifacts identified: sprint status and future story files.
- [x] 4.1 Direct Adjustment evaluated as viable.
- [x] 4.2 Rollback evaluated as not recommended.
- [x] 4.3 MVP review evaluated as not required; MVP expands at entry/integration layer but core workflow remains.
- [x] 4.4 Recommended path selected: Direct Adjustment.
- [x] 5.1 Issue summary created.
- [x] 5.2 Epic and artifact impacts documented.
- [x] 5.3 Recommended path documented.
- [x] 5.4 MVP impact and action plan documented.
- [x] 5.5 Handoff plan documented.
- [x] 6.1 Checklist reviewed.
- [x] 6.2 Proposal reviewed for consistency.
- [x] 6.3 User approval received on 2026-06-10.
- [x] 6.4 Sprint status updated on 2026-06-10.
- [x] 6.5 Final handoff routed to story creation and development for revised Epic 2.

## 7. Approval and Handoff Log

Approved by user on 2026-06-10.

Artifacts updated:

- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-10.md`

Next command:

`create story 2.2`
