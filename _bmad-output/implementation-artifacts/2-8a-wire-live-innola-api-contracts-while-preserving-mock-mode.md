---
baseline_commit: 15286ba8353f30a0da5cc94671a2a228fecbf004
---

# Story 2.8A: Wire Live Innola API Contracts While Preserving Mock Mode

Status: done

## Story

As a cadastral technical staff user,
I want the Sidwell Co add-in to use the verified live Innola workstation APIs while retaining mock mode,
so that I can test the transaction workflow against the real dev server without losing deterministic dry-run behavior.

## Acceptance Criteria

1. Given `innola_transaction_mode` is `mock`, when the user logs in, refreshes tasks, starts/stops/completes mock transactions, and loads mock attachments, then the existing mock behavior remains network-free and all current mock tests continue to pass.
2. Given `innola_transaction_mode` is `live`, when the user logs in, then the add-in posts the documented `LoginObject` shape to `POST /api/rest/authenticate`, accepts the documented `auth-token` response, keeps the password/token in memory only, and initializes the user context through `GET /api/rest/currentUserDetails` when available.
3. Given the user is logged in in live mode, when the Transaction Panel refreshes, then the add-in calls `GET /api/v4/rest/workflow/my-tasks`, maps the returned `CamundaTask`/`WorkflowTask` payload to `InnolaTransactionRow`, and continues applying the configured `innola_process_step`/local availability filters without sending the old legacy `application/getmytasks` request body.
4. Given a live task row is selected, when transaction detail is loaded, then the add-in calls `GET /api/v4/rest/workflow/tasks/{taskId}` and maps task/application/transaction metadata into the existing `InnolaTransactionDetail` contract without replacing the Case Folder loader.
5. Given live task metadata exposes downloadable source identifiers, when attachments are loaded, then the add-in downloads content through `GET /api/v4/rest/source/download` using `sourceId`, `sourceUid`, or `bodyId` and writes files through the existing attachment writer; when identifiers are absent, the load fails with a clear non-secret "attachment metadata unavailable" message rather than guessing another endpoint.
6. Given a live transaction is started, stopped/saved, or completed, when lifecycle actions run, then the add-in uses the documented workflow endpoints: `POST /api/v4/rest/workflow/tasks/{taskId}/claim` or `/start` for start ownership, `POST /api/v4/rest/workflow/tasks/{taskId}/unclaim` only when the user intentionally releases a task, `GET /api/v4/rest/workflow/tasks/{taskId}/transitions` to resolve completion transitions, and `POST /api/v4/rest/workflow/tasks/{taskId}/complete?transition=...` for completion.
7. Given a live lifecycle endpoint returns HTTP 200 with `{ "success": false, "message": "..." }`, 401, 403, 422, null task, or a transport failure, then the previous valid transaction state is preserved and the UI shows a retryable redacted error.
8. Given live mode is enabled, then login, task refresh, detail load, attachment download, claim/start, stop/save/release, and complete are covered by focused unit tests using fake `HttpMessageHandler` fixtures; no automated test depends on the live Innola network.
9. Given the story is complete, then no extraction, validation, DWG readiness, output generation, CADINDEX sync, ArcGIS Enterprise writeback, or real document upload implementation is added.

## Tasks / Subtasks

- [x] Preserve and formalize adapter mode selection. (AC: 1)
  - [x] Keep `InnolaTransactionSettings.Mode` as the switch between mock and live services.
  - [x] Preserve current `MockInnolaAuthService`, `MockInnolaTransactionService`, `MockInnolaTransactionDetailService`, and `MockInnolaTransactionLifecycleService` behavior.
  - [x] Add tests proving mock mode remains network-free after live services are implemented.
- [x] Update live authentication to the verified contract. (AC: 2, 7, 8)
  - [x] Change `InnolaAuthService` request body to include `createSession`, `login`, `module`, `password`, and `version`; use settings/defaults for module/version.
  - [x] Parse `auth-token` in addition to existing token field names.
  - [x] Use the same session `HttpClient`/handler path for `GET /api/rest/currentUserDetails` after login where possible.
  - [x] Map current-user fields such as `userName`, `fullName`, `groups`, and roles into `InnolaUserContext`.
  - [x] Keep passwords and tokens out of settings, manifests, status text, logs, and test failure messages.
- [x] Replace the legacy live task-list call. (AC: 3, 7, 8)
  - [x] Change `InnolaTransactionService` from `POST /api/rest/application/getmytasks` to `GET /api/v4/rest/workflow/my-tasks`.
  - [x] Remove the legacy user/group/filter request body from the live call; apply search/sort/filter locally after mapping if needed.
  - [x] Map top-level task fields: `id`, `name`, `assignee`, `role`, `createTime`, `definitionKey`, `taskKey`, `processKey`, `processName`, `transactionId`, `transactionCode`, and `transition`.
  - [x] Map nested `transaction.transactionNo`, `transaction.transactionType`, `transaction.status`, and `transaction.createDatetime`; prefer nested transaction number over application number when present.
  - [x] Keep `InnolaTransactionFiltering.FilterAvailableRows` as the final local gate.
- [x] Implement live transaction detail mapping. (AC: 4, 5, 7, 8)
  - [x] Replace the `adapter_not_configured` body of `InnolaTransactionDetailService` with an HTTP implementation.
  - [x] Map task/application/transaction metadata into `InnolaTransactionDetail` without changing the existing Case Folder load service contract.
  - [x] Extract `CaseType` and `ProfileHint` from stable fields if present, such as transaction type/code or configured process metadata; otherwise leave them null and let existing profile detection continue from source files.
  - [x] Treat missing/null task responses as not found and preserve previous UI state.
- [x] Implement source download only for proven identifiers. (AC: 5, 7, 8)
  - [x] If task/detail payload includes source/file body identifiers, map them to `InnolaAttachmentMetadata.ServiceReference` with an explicit reference scheme such as `source-id:...`, `source-uid:...`, or `body-id:...`.
  - [x] Implement `GetAttachmentContentAsync` through `GET /api/v4/rest/source/download`.
  - [x] Set `attachment=false` unless a download filename is required by the response behavior.
  - [x] Do not invent a source-list endpoint. If the payload does not expose source identifiers, return a clear redacted failure and document the needed Innola contract.
- [x] Implement live lifecycle endpoints behind the existing lifecycle interface. (AC: 6, 7, 8)
  - [x] Replace `InnolaTransactionLifecycleService` not-configured responses with HTTP calls.
  - [x] Start/claim behavior must match current UI semantics: active transaction only after server ownership succeeds.
  - [x] Prefer `/start` when opening a task because Swagger says it assigns the task and returns task/application context; support `/claim` where the current coordinator expects success/failure ownership semantics.
  - [x] Save/Stop must preserve the local full Case Folder. Do not call `/complete`; call `/unclaim` only if the intended UI action is to release the task for other operators.
  - [x] Complete must resolve a transition from `/transitions`; use configured/default transition if present, otherwise fail safely with "completion transition unavailable."
- [x] Add live contract tests and preserve existing validation. (AC: 1-9)
  - [x] Update/add fake `HttpMessageHandler` tests for auth request, `auth-token` parsing, current user mapping, task list endpoint/mapping, task detail mapping, source download, lifecycle success/failure, and redaction.
  - [x] Register new tests in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`.
  - [x] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`.
  - [x] Run `tools\validate_contracts.ps1`.
  - [x] Run `tools\run_python_tests.ps1`.
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`.
  - [x] Run `tools\package_addin.ps1`.

## Dev Notes

### Current State From Prior Stories

- Story 2.2 added Sidwell Co shell, session-only login, mock/live service selection, command gating, and Configuration/About windows.
- Story 2.3 added the Transaction Panel, mock task rows, search/filter/sort, and a partial `InnolaTransactionService` live adapter.
- Story 2.4 added `InnolaTransactionLoadService`, `IInnolaTransactionDetailService`, attachment writing into the Case Folder, and profile detection from source files.
- Story 2.5 added `IInnolaTransactionLifecycleService`, lifecycle coordinator, manifest `innola_lifecycle`, and `working\workflow_lifecycle_audit.json`.
- Story 2.7 tightened execution state: selection does not enable Parcel Workflow, start locks the panel and enables Parcel Workflow, Stop/Save unlocks and disables Parcel Workflow, completion clears active state, and mock flow is confirmed working in ArcGIS Pro.
- Current live gaps are intentional stubs or partial adapters: auth posts too small a body and does not parse `auth-token`; transaction list calls the old `POST /api/rest/application/getmytasks`; detail and lifecycle services return `adapter_not_configured`/`lifecycle_endpoint_not_configured`.

### Verified Innola API Contracts

Use `D:\Code\SwaggerDocs\specific-api.txt` as the contract source captured from `https://eltrs-dev.innola-solutions.com/rest-api/#/`.

- `POST /api/rest/authenticate`
  - Request body example:
    ```json
    {
      "createSession": true,
      "login": "jdoe",
      "module": "default",
      "password": "password",
      "version": "1"
    }
    ```
  - Success may return `{ "auth-token": "..." }` or `{}` for session-only login.
- `GET /api/rest/currentUserDetails`
  - Returns the active `AppUser`, or `null` with HTTP 200 when no session is active.
- `GET /api/v4/rest/workflow/my-tasks`
  - Returns tasks assigned to the current user or candidate groups matching current user roles.
  - Response example includes `CamundaTask` fields plus nested `application` and `transaction`.
- `GET /api/v4/rest/workflow/tasks/{taskId}`
  - Returns a workflow task with loaded application object, or `null` when the task no longer exists.
- `POST /api/v4/rest/workflow/tasks/{taskId}/claim`
  - Always uses HTTP 200 for business outcome; body has `success` and optional `message`.
- `POST /api/v4/rest/workflow/tasks/{taskId}/start`
  - Starts/assigns the task and returns `StartTaskResponse` with `task`, `readOnly`, `officeOpened`, and FIFO fields.
- `POST /api/v4/rest/workflow/tasks/{taskId}/unclaim`
  - Releases the task to the shared queue. Use carefully; this is not the same as local "save full Case Folder and keep working later" unless product confirms release is desired.
- `GET /api/v4/rest/workflow/tasks/{taskId}/transitions`
  - Returns transition objects with `id`, `transitionId`, `name`, `isDefault`, and destination metadata.
- `POST /api/v4/rest/workflow/tasks/{taskId}/complete?transition=...`
  - Completes using the transition identifier.
- `GET /api/v4/rest/source/download`
  - Supports `bodyId`, `sourceId`, or `sourceUid`, plus optional `attachment`, `documentPart`, `documentName`, and `pageNumber`.

### Existing Files To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSettings.cs`
  - Currently has `DefaultServerUrl`, `RestPath`, and `AuthenticationPath`.
  - Consider adding v4 path helpers/constants rather than hardcoding strings in multiple services.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
  - Currently loads `innola_transaction_mode`, `innola_process_step`, and `case_folder_output_root`.
  - Extend only if needed for `innola_auth_module`, `innola_auth_version`, or completion transition configuration.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
  - Currently defaults to `innola_transaction_mode: "mock"` and `innola_server_url: "https://eltrs.innola-solutions.com/"`.
  - Preserve mock as default unless the user explicitly switches to live.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaAuthService.cs`
  - Update request/response mapping and current user lookup here.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionService.cs`
  - Replace the legacy endpoint and enhance `MapRows` for Swagger task payloads.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
  - Replace not-configured stubs with live detail/download logic.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleService.cs`
  - Replace not-configured stubs with live workflow lifecycle logic.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
  - Keep mode-based service factory pattern intact.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`
  - Do not rewrite this service. It already owns Case Folder creation and attachment copy/write behavior.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/*.cs`
  - Extend existing test style and fake HTTP handlers; keep automated tests offline.

### Mapping Requirements

- Task id: use top-level `id`.
- Transaction id: use top-level `transactionId` or nested `transaction.id`.
- Transaction number: prefer nested `transaction.transactionNo`; fall back to `application.applicationNo` only if no transaction number exists.
- Task name: use top-level `name`.
- Process step: map from `taskKey`, `definitionKey`, `processKey`, or configured process metadata; keep configured `innola_process_step` filtering. If the live task step taxonomy is not `parcel_workflow`, the story may need settings to map live task keys to local `parcel_workflow`.
- Responsible party: use applicant/requestor fields only when present. Do not fabricate names.
- Assigned user: top-level `assignee`.
- Assigned group: top-level `role` or candidate group field if present.
- Received timestamp: top-level `createTime`; fall back to nested transaction/application create timestamps.
- Status: map completed/closed statuses to completed, started/processing/in progress to in progress, otherwise available if the task is returned by `my-tasks`.

### Security And Reliability Requirements

- Never persist Innola passwords, `auth-token`, access tokens, cookies, authorization headers, raw request bodies, raw responses, or stack traces.
- Do not write secrets to `manifest.json`, lifecycle audit, reports, logs, or visible status text.
- Preserve prior valid state on all live failures. In particular, failed start must not enable Parcel Workflow or lock the panel as active.
- Keep `HttpClient` usage testable through injected `HttpClient`/`HttpMessageHandler`.
- Distinguish transport failure from business failure but keep user-facing text concise and redacted.
- Automated tests must not call `https://eltrs-dev.innola-solutions.com` or production.

### Open Contract Questions To Carry Forward

- Confirm whether production workstation login should be session-cookie first, `auth-token` header first, or both. For this story, support `auth-token` parsing and preserve cookies naturally through the injected `HttpClient`.
- Confirm official `module` and `version` values. Use safe defaults from Swagger (`default`, `1`) or settings until Innola confirms.
- Confirm the live task key(s) that represent the Sidwell parcel workflow step. The add-in currently filters by `parcel_workflow`.
- Confirm where attachment/source identifiers appear in the task/application/transaction payload. Do not implement a guessed discovery endpoint.
- Confirm whether Stop/Save should release the server task with `/unclaim` or keep ownership while only saving the local Case Folder. Current product guidance says the task can be completed only by the user who started it, so avoid releasing ownership unless explicitly configured.

### Scope Boundaries

- Do not redesign the Transaction Panel UI in this story.
- Do not add live document upload or "add file to transaction" support.
- Do not implement DWG readiness, extraction, validation, output generation, CADINDEX sync, or ArcGIS Enterprise writes.
- Do not change Case Folder JSON naming conventions; keep lowercase snake_case artifacts.
- Do not change ArcGIS Pro compatibility: ArcGIS Pro SDK 3.6 lane, `desktopVersion="3.6"`, `net8.0-windows`.

### Testing Notes

- Keep the current no-framework console test harness pattern.
- Add fake HTTP payloads based on `specific-api.txt`.
- Update the existing endpoint assertion that currently expects `/api/rest/application/getmytasks`.
- Add regression tests for redaction on all live adapters.
- Manual ArcGIS Pro smoke after implementation:
  - `mock` mode still works exactly as today.
  - `live` mode logs into dev/test, populates Transaction Panel, starts a transaction, locks the panel, enables Parcel Workflow, and fails gracefully if attachments are unavailable.

### References

- `D:\Code\SwaggerDocs\specific-api.txt`: verified Innola Swagger excerpts for auth, current user, tasks, lifecycle, transitions, complete, and source download.
- `_bmad-output/planning-artifacts/epics.md`: Epic 2 FR24-FR28 and UX-DR19-UX-DR22.
- `_bmad-output/planning-artifacts/architecture.md`: C# owns workflow state and command gates; Case Folder remains system of record; no live CADINDEX/Enterprise writeback in v1.
- `_bmad-output/implementation-artifacts/2-4-load-transaction-details-and-attachments-into-case-folder.md`: existing transaction detail/attachment-to-Case-Folder flow.
- `_bmad-output/implementation-artifacts/2-5-control-active-transaction-lifecycle-and-completion-gate.md`: existing lifecycle coordinator and completion gate.
- `_bmad-output/implementation-artifacts/2-7-enforce-transaction-execution-state-and-panel-locking.md`: current panel locking and active transaction behavior.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaAuthService.cs`: live auth adapter to update.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionService.cs`: live task-list adapter to update.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`: live detail/download adapter to implement.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleService.cs`: live lifecycle adapter to implement.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` - passed, 118 tests.
- `tools\validate_contracts.ps1` - passed.
- `tools\run_python_tests.ps1` - passed, 2 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` - passed.
- `tools\package_addin.ps1` - passed; produced `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.esriAddInX`.

### Completion Notes List

- Added a shared Innola HTTP helper for normalized server URLs, v4 endpoint construction, token/cookie-compatible auth headers, and redacted retry messages.
- Updated live authentication to post the documented `LoginObject`, parse `auth-token`, and hydrate `InnolaUserContext` from `currentUserDetails` when available.
- Replaced the legacy task-list adapter call with `GET /api/v4/rest/workflow/my-tasks` while preserving tolerant row mapping and local process-step filtering.
- Implemented live task detail mapping through `GET /api/v4/rest/workflow/tasks/{taskId}` and source downloads through `GET /api/v4/rest/source/download` only when the payload exposes source/body identifiers.
- Implemented live lifecycle start through `/start`, local save/in-progress semantics for Stop/Save, transition lookup through `/transitions`, and completion through `/complete?transition=...`.
- Added offline fake-HTTP tests for auth, task list, detail/download, lifecycle start/complete/business failure, plus preserved existing mock flow coverage.

### File List

- `_bmad-output/implementation-artifacts/2-8a-wire-live-innola-api-contracts-while-preserving-mock-mode.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/epics.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaAuthService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaHttp.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaAuthServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionDetailServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLifecycleServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-11 | 0.1 | Initial story context for live Innola API contract wiring while preserving mock mode. | Mary |
| 2026-06-11 | 1.0 | Implemented live Innola auth, task list, detail/download, and lifecycle adapters with offline contract tests while preserving mock mode. | Amelia |
