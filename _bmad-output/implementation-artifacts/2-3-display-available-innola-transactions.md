---
baseline_commit: NO_VCS
---

# Story 2.3: Display Available Innola Transactions

Status: done

## Story

As a cadastral technical staff user,
I want to see the Innola transactions available to me or my group for the current parcel workflow step,
so that I can choose the correct assigned task before creating or reopening the local Case Folder.

## Acceptance Criteria

1. Given the user is not logged into Innola, when the Transaction Panel is opened or refreshed, then the panel shows a clean logged-out state, disables refresh/search/filter/sort/load actions, and does not call the Innola transaction API.
2. Given the user is logged into Innola, when the Transaction Panel opens or the user selects Refresh, then the add-in requests only transactions available to the logged-in user or one of their groups for the configured parcel workflow step.
3. Given available transactions are returned, when the list renders, then each row shows transaction number, task name, responsible party or group, received or assigned timestamp, status where available, and enough hidden identifiers for later story handoff without exposing tokens.
4. Given the list is visible, when the user filters, searches, sorts, refreshes, or selects a row, then the panel responds without freezing ArcGIS Pro and preserves a clear selected transaction state.
5. Given unavailable, completed, wrong-step, or locked transactions are returned by the server or mock service, when the panel displays available work, then those transactions are excluded or clearly not loadable according to the service result.
6. Given the Innola API call fails, times out, or returns an unexpected payload, when the panel refresh completes, then the UI shows a retryable non-secret error and does not expose access tokens, passwords, raw request payloads, or stack traces.
7. Given live Innola access is unavailable, when the add-in runs in the configured mock or dry-run transaction mode, then a logged-in user can test the enabled Transaction Panel, sample rows, selection, and downstream gating state without real network calls.
8. Given a transaction is selected in Story 2.3, then the add-in records the selected transaction only in session state for later handoff; it does not create a Case Folder, download attachments, claim/complete the task, or enable Parcel Workflow until later stories implement those gates.

## Tasks / Subtasks

- [x] Add transaction list UX mockup and login polish guidance. (AC: 1, 3, 4, 6, 7)
  - [x] Add `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/mockups/transaction-panel.html`.
  - [x] Follow the existing Sidwell design tokens: compact ArcGIS Pro-adjacent pane, Segoe UI, neutral surfaces, tight spacing, clear separators, no nested cards, no old `Innola` group branding.
  - [x] Mock the key states: logged out, loading, empty, error/retry, populated list, and selected transaction.
  - [x] Include controls matching the prior Innola task panel pattern: compact toolbar, refresh, optional open/load action, filter combo, search box, sorting controls, and dense transaction rows.
  - [x] Enhance the login UI only where it supports this story's dry run and polish: server field, login status, logged-in identity, and non-secret errors. Do not add persistent saved-password behavior.
- [x] Add Innola transaction API contracts behind a mockable boundary. (AC: 2, 3, 5, 6, 7)
  - [x] Create an `IInnolaTransactionService` or equivalent under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/`.
  - [x] Add request/result models such as `InnolaTransactionQuery`, `InnolaTransactionListResult`, `InnolaTransactionRow`, and `InnolaTransactionStatus`.
  - [x] Include stable fields needed now and by later stories: task id, transaction id, transaction number, task name, process step, status, assignee, group, responsible party, received/assigned timestamp, claim/lock/loadability flags, and optional browser URL.
  - [x] Query by configured process step, initially `parcel_workflow`, and by the current `InnolaSession` user/group context.
  - [x] Use the session access token only inside the HTTP adapter. Never expose it through rows, errors, logs, mockup data, or UI bindings.
  - [x] Keep endpoint paths configurable or isolated. Prefer mapping the previous add-in's `application/getmytasks` behavior behind the interface while allowing the future `/tasks/available?process_step=parcel_workflow` shape to be swapped in later.
- [x] Implement a dry-run/mock transaction provider. (AC: 1, 3, 4, 5, 7, 8)
  - [x] Add mock rows that resemble the user-provided task list, for example `TR100000004 - Computation Check`, `TR100000005 - Prepare Rejection Letter`, and `TR100000009 - QC of Registration Cases`.
  - [x] Ensure mock mode still requires a logged-in session before transactions appear.
  - [x] Include at least one wrong-step/completed/unavailable sample in service tests to prove filtering/loadability behavior.
  - [x] Configure mock mode through a non-secret local setting or test-only injection. Do not require live Innola credentials for automated tests.
- [x] Replace the placeholder Transaction Panel with the usable list UI. (AC: 1, 3, 4, 5, 6, 7, 8)
  - [x] Extend `TransactionPanelDockpaneViewModel` with observable row collection, selected row, loading state, logged-out state, empty state, error state, filter/search/sort settings, status text, and commands.
  - [x] Replace `TransactionPanelDockpane.xaml` placeholder content with the compact toolbar/filter/search/sort/list layout from the mockup.
  - [x] Disable refresh/filter/search/sort/load controls while logged out or while refresh is running.
  - [x] Refresh automatically on login or panel activation only if it does not trigger unwanted live calls in tests; otherwise provide an explicit Refresh command and status.
  - [x] Sort by transaction number, task name, timestamp, and status where data is available.
  - [x] Search transaction number, task name, responsible party, assignee, and group.
  - [x] Selecting a row updates session-level selected transaction state for Story 2.4, but does not open Parcel Workflow or create/reopen a Case Folder.
- [x] Preserve and extend shell gating state. (AC: 1, 4, 7, 8)
  - [x] Add a selected-transaction session model or shell-state property that is independent from loaded Case Folder state.
  - [x] Keep `Transaction Panel` enabled only after login, as implemented in Story 2.2.
  - [x] Keep `Parcel Workflow` disabled after transaction selection until Story 2.4 validates transaction details and source attachment intake.
  - [x] Clear selected transaction state on logout/session expiry.
- [x] Add focused tests. (AC: 1, 2, 3, 4, 5, 6, 7, 8)
  - [x] Test logged-out panel state: no API call, disabled commands, non-secret `Not logged in.` status.
  - [x] Test logged-in refresh calls the transaction service with the configured process step and current user/group context.
  - [x] Test mapping from prior Innola task payload fields such as `transaction_id`, `transaction_no`, task name, assignee/group, and timestamp into the add-in row model.
  - [x] Test search, filter, sort, selected-row, loading, empty, and retryable-error behavior in the ViewModel.
  - [x] Test mock/dry-run mode returns sample rows after login and excludes or marks unavailable rows as not loadable.
  - [x] Test error redaction: token, password, raw request body, and stack trace text do not appear in status/errors.
  - [x] Test selecting a transaction does not create a Case Folder, does not download attachments, does not claim/complete a task, and does not enable Parcel Workflow.
- [x] Validate and package. (AC: 1, 2, 3, 4, 5, 6, 7, 8)
  - [x] Run `tools\validate_contracts.ps1`.
  - [x] Run `tools\run_python_tests.ps1`.
  - [x] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`.
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`.
  - [x] Run `tools\package_addin.ps1`.
  - [x] Manual ArcGIS Pro 3.6 smoke test: automated package/register validation passed; visual confirmation in ArcGIS Pro remains the manual review step.

## Dev Notes

### Current Project State

- Story 2.2 is done. It added the Sidwell Co ribbon shell, session-only Innola login, command gating, safe Configuration and About windows, a Transaction Panel placeholder, and C# tests for login/session behavior.
- The current Transaction Panel is intentionally minimal. `TransactionPanelDockpaneViewModel` only reports `Transactions are ready to load in the next story.` after login. This story replaces that placeholder with the real task list surface.
- The current shell rules are:
  - Logged out: Login, Configuration, and About available; Transaction Panel and Parcel Workflow disabled.
  - Logged in: Transaction Panel enabled; Parcel Workflow disabled because no transaction has been loaded.
  - Logout/session expiry: token/password/user context cleared and gates reset.
- The add-in remains on the ArcGIS Pro 3.6 compatibility lane: `net8.0-windows`, ArcGIS Pro SDK 3.6, Visual Studio 2022, and `Config.daml` `desktopVersion="3.6"`.
- Packaging output remains `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.esriAddInX`.

### Existing Files To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs`
  - Replace placeholder status-only behavior with MVVM state and commands for transaction listing.
  - Subscribe to `ShellState.Session.SessionChanged` and clear rows/selection on logout.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
  - Replace placeholder text with the compact task-list UI.
  - Keep the pane usable in narrow ArcGIS Pro dock widths.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
  - Reuse current session access and UI-thread notification behavior.
  - Extend only if selected transaction needs to live near session state; keep transaction-loaded Case Folder state separate.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
  - Centralize transaction panel and parcel workflow gate decisions here if new selected-transaction state affects gates.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaAuthService.cs`
  - Do not mix transaction-list API logic into auth service. Auth remains login/current-user/session.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/LoginWindow.xaml` and `.cs`
  - Optional polish only. Keep passwords session-only and never persisted.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
  - May add non-secret local transaction list settings such as process step or mock/dry-run mode. Do not store tokens or passwords.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
  - Register new transaction service and panel ViewModel tests.
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/mockups/`
  - Add a transaction-list mockup because current UX mockups only cover the parcel workflow pane, extraction/review, and manual-process flows.

### Previous Innola Add-in Reuse Guidance

Source project: `D:\Code\Innola_Code\arcgis-pro-nscrp-develop`.

Reuse these patterns:

- `src\TaskListDockpane.xaml`
  - Compact toolbar, filter/search/sort controls, dense rows, and task list ergonomics.
- `src\TaskListDockpaneViewModel.cs`
  - `Rows` collection, refresh/load behavior, selected task state, filtering/sorting, and login-based enablement.
- `src\Services\InnolaApiService.cs`
  - Likely task endpoint pattern: `application/getmytasks`.
  - Related patterns: `workflow/{taskId}/claim`, `workflow/{taskId}/complete`, and source file APIs belong to later stories.
- `src\Services\ApiService.cs`
  - Uses the `Access-Token` header. Reuse the idea, but avoid MessageBoxes inside services.
- `src\Models\ApplicationTasks\ApplicationTask.cs`
  - Useful fields include transaction id, transaction number, task name, browser URL, and task identity.

Do not copy these behaviors:

- Do not persist saved passwords or auto-load saved credentials.
- Do not show MessageBoxes from services.
- Do not use the old `Innola` group/caption in the ribbon or panel.
- Do not implement old project controls such as Spatial Units, Export Map, or unrelated task actions in this story.

### Innola API Boundary For This Story

The live Swagger endpoint was not verified from this environment. The implementation must therefore isolate all response-shape assumptions in one adapter and keep the panel dependent on add-in-owned models.

Known starting configuration:

```text
Base server: https://eltrs.innola-solutions.com/
Likely REST prefix from previous add-in: /api/rest/
Likely task-list path from previous add-in: application/getmytasks
Authenticated header from previous add-in: Access-Token
Configured process step for this story: parcel_workflow
```

Preferred service shape:

```text
InnolaTransactionQuery
  server_url
  access_token
  user_name
  groups[]
  process_step
  filter
  search
  sort_field
  sort_direction

InnolaTransactionRow
  task_id
  transaction_id
  transaction_no
  task_name
  process_step
  status
  responsible_party
  assigned_user
  assigned_group
  received_at
  is_available
  is_loadable
  unavailable_reason
  browser_url
```

The HTTP adapter may post a prior-style filter request to `application/getmytasks` and then map the result to this canonical model. If the server later exposes `/tasks/available?process_step=parcel_workflow`, only the adapter should change.

### UI Behavior Requirements

- Logged-out state:
  - Show `Not logged in.`
  - Disable refresh, filter, search, sort, and load/select actions.
  - Do not call the transaction service.
- Loading state:
  - Show a compact progress status.
  - Disable duplicate refresh.
  - Keep the previous selection only if it remains valid after refresh.
- Empty state:
  - Show a calm message such as `No available transactions for this step.`
  - Do not imply an error.
- Error state:
  - Show `Could not refresh transactions. Try again.`
  - Include a non-secret reason category where useful, such as unauthorized, timeout, unavailable, or invalid response.
- Populated state:
  - Show dense rows with transaction number as the primary link-like identifier, task name as the bold secondary scan target, and party/group/timestamp details beneath or in compact columns.
  - Do not show hidden API IDs unless needed in debug/test text outside production UI.
- Selection state:
  - Make the selected transaction visually obvious.
  - Expose `Selected transaction: {transaction_no}` status.
  - Do not enable Parcel Workflow until Story 2.4 loads details and attachments into a Case Folder.

### Dry-Run Testing Guidance

The user needs a way to simulate the process before live Innola contract details are complete. Recommended approach:

- Add a mock transaction provider that is injected into the ViewModel tests.
- Optionally add a local non-secret setting such as:

```json
{
  "innola_transaction_mode": "mock",
  "innola_process_step": "parcel_workflow"
}
```

- If a user-facing toggle is added, keep it in safe Configuration and label it as a local test mode. If that is too much UI for this story, support the mode only through settings and tests.
- Mock mode must still respect login/session gating so enable/disable behavior is realistic.

### Security Requirements

- Passwords and access tokens remain session-only and in memory.
- The transaction service must not write access tokens, passwords, request payloads, or raw response bodies to logs, status text, mockups, Case Folder files, or test artifacts.
- Error objects should expose a redacted user message and optional category, not raw exceptions.
- Tests should assert that known fake secret values do not appear in Transaction Panel status/error strings or any file written by the story.

### Architecture Requirements

- C# owns the ArcGIS Pro UI, session state, Innola transaction service boundary, and shell gating.
- Keep Innola auth, transaction list, transaction detail/attachment, and lifecycle/complete concerns separate:
  - Auth/session: Story 2.2.
  - Available transaction list and selection: Story 2.3.
  - Transaction details and attachments to Case Folder: Story 2.4.
  - Claim/save/cancel/complete lifecycle: Story 2.5.
- Do not call Python, ArcPy, extraction, validation, output generation, CADINDEX, or ArcGIS Enterprise writeback in this story.
- Keep WPF UI updates on the UI thread. Preserve the Story 2.2 synchronization-context fix.
- Network calls must be async and cancellable or timeout-bounded enough that ArcGIS Pro does not appear frozen.

### Scope Boundaries

- Do not create or reopen a Case Folder from selected transaction in this story.
- Do not download transaction details or attachments into the Case Folder in this story.
- Do not claim, save, cancel, complete, or reassign Innola tasks in this story.
- Do not enable Parcel Workflow based only on selection.
- Do not implement preflight/environment/DWG checks in this story.
- Do not add live CADINDEX or ArcGIS Enterprise update behavior.

### Testing Guidance

- Continue using the existing lightweight C# console test runner in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests`.
- Automated tests must not require network access or real Innola credentials.
- Use fake session and fake transaction services for ViewModel tests.
- Keep live endpoint behavior behind adapter tests that can use canned JSON payloads.
- Avoid brittle WPF visual tests; test bindable ViewModel state and command behavior.
- Manual ArcGIS Pro 3.6 smoke testing should confirm the pane is no longer blank, controls are enabled only after login/mock login, and selection does not prematurely open Parcel Workflow.

### References

- [_bmad-output/planning-artifacts/epics.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/epics.md): Epic 2 and Story 2.3 acceptance criteria.
- [_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-10.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-10.md): approved Innola transaction workflow correction.
- [_bmad-output/implementation-artifacts/2-2-add-sidwell-shell-innola-login-and-session-gating.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/implementation-artifacts/2-2-add-sidwell-shell-innola-login-and-session-gating.md): completed login/session shell story and current gating semantics.
- [_bmad-output/planning-artifacts/architecture.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/architecture.md): C# add-in, WPF/MVVM, Case Folder boundary, security, and logging rules.
- [_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md): visual tokens and compact ArcGIS Pro-adjacent style.
- [_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md): dock pane behavior, state patterns, and UX rules.
- [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs](D:/Code/BMad-Method/dev/pe-jamaica/src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs): placeholder ViewModel to replace.
- [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml](D:/Code/BMad-Method/dev/pe-jamaica/src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml): placeholder UI to replace.
- `D:\Code\Innola_Code\arcgis-pro-nscrp-develop\src\TaskListDockpane.xaml`: previous Innola task list layout pattern.
- `D:\Code\Innola_Code\arcgis-pro-nscrp-develop\src\TaskListDockpaneViewModel.cs`: previous Innola task list ViewModel behavior.
- `D:\Code\Innola_Code\arcgis-pro-nscrp-develop\src\Services\InnolaApiService.cs`: likely `application/getmytasks` API usage.
- `D:\Code\Innola_Code\arcgis-pro-nscrp-develop\src\Services\ApiService.cs`: `Access-Token` header pattern.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- C# test runner initially required escalation because the .NET SDK needed access to the local Windows SDK cache under `C:\Users\js91482\AppData\Local\Microsoft SDKs`.
- First escalated C# run failed on `InnolaTransactionSettings.cs` missing `System.IO`; patched and reran.
- C# test runner passed 73 tests after adding transaction service, mock provider, transaction panel state, and gating tests.
- Code review patch validation: C# test runner passed 74 tests after loading-state gate notification coverage and output settings packaging fix.
- `tools\validate_contracts.ps1` passed.
- `tools\run_python_tests.ps1` passed: 2 Python tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` passed with 0 warnings and 0 errors.
- `tools\package_addin.ps1` passed and produced `ParcelWorkflowAddIn.esriAddInX`.
- Code review patch validation: `tools\validate_contracts.ps1`, `tools\run_python_tests.ps1`, `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`, and `tools\package_addin.ps1` passed.

### Completion Notes List

- Added a dedicated transaction-list mockup aligned to Sidwell compact desktop styling and the prior Innola task-list interaction pattern.
- Added mockable Innola transaction contracts, canonical row/query/result models, prior payload mapping, availability filtering, secret-redacted failures, and an HTTP adapter for the likely `application/getmytasks` endpoint.
- Added a dry-run mock transaction provider with sample rows matching the user-provided task list and local non-secret settings for `innola_transaction_mode` and `innola_process_step`.
- Replaced the Transaction Panel placeholder with a compact refresh/filter/search/sort/list/load UI backed by testable panel state.
- Added selected transaction session state while keeping `IsTransactionLoaded` false so Parcel Workflow remains disabled until Story 2.4 loads details and attachments into a Case Folder.
- Polished the login dialog title/header without adding persistent password behavior.
- Added focused transaction service and transaction panel tests covering logged-out no-call behavior, query shape, mapping/filtering, search/sort/selection, dry-run mode, logout clearing, and error redaction.

### File List

- `_bmad-output/implementation-artifacts/2-3-display-available-innola-transactions.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/mockups/transaction-panel.html`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/LoginWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/RelayCommand.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/IInnolaTransactionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionFiltering.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionListResult.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionQuery.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionRow.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionStatus.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/MockInnolaTransactionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/SelectedInnolaTransaction.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`

## Senior Developer Review (AI)

### Review Outcome

Approved after patch.

### Findings

- [x] [Review][Patch] Loading-state changes did not notify list-control gate bindings [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs:69]. AC4 requires filter/search/sort/refresh/load controls to respond while the panel refreshes. The original `IsLoading` setter only notified `IsLoading` and command states, so ComboBox/TextBox bindings to `CanUseListControls` could remain enabled during an active refresh. Patched `IsLoading` to raise `CanRefresh`, `CanUseListControls`, `CanLoadSelectedTransaction`, and `IsEmpty`; added `LoadingRefreshDisablesListControls` regression coverage.
- [x] [Review][Patch] Transaction settings were not copied to the add-in output [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj:16]. AC7 requires configurable mock/dry-run behavior for ArcGIS Pro testing, but `WorkflowSettings.json` was not marked as output content, so runtime configuration would fall back to defaults instead of reflecting the packaged settings. Patched the project file to copy `Settings\WorkflowSettings.json` to output and verified it exists in `bin\Debug\net8.0-windows\Settings\WorkflowSettings.json`.

### Residual Risk

- Live Innola task endpoint behavior remains unverified in this environment; Story 2.3 isolates that risk behind `IInnolaTransactionService` and tolerant payload mapping, but real endpoint smoke testing is still needed when credentials/network are available.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-10 | 0.1 | Initial story context for displaying available Innola transactions, dry-run support, and transaction-panel UX. | Mary |
| 2026-06-10 | 1.0 | Implemented transaction list UI, mock/live service boundary, dry-run mode, selected transaction state, and tests. | Codex |
| 2026-06-10 | 1.1 | Applied code review patches for loading-state UI gating and transaction settings output packaging; moved story to done. | Codex |
