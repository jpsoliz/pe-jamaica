---
baseline_commit: NO_VCS
---

# Story 2.4: Load Transaction Details and Attachments into Case Folder

Status: done

## Story

As a cadastral technical staff user,
I want to load a selected Innola transaction and its attachments into the local Case Folder,
so that the parcel workflow starts from authoritative transaction metadata and stable local source files.

## Acceptance Criteria

1. Given the user is logged in and has selected an available transaction in the Transaction Panel, when the user loads the transaction, then the add-in retrieves transaction metadata, case type/profile metadata, ownership/claim status, and attachment metadata through a mockable Innola service boundary.
2. Given attachment metadata is returned, then each attachment record includes stable id, file name, extension or MIME type, source role/category where available, size/checksum where available, and a download reference or equivalent service identifier.
3. Given a transaction is loaded successfully, then the add-in creates or reopens the local Case Folder using a safe stable folder identifier derived from the Innola transaction number/id.
4. Given attachments are loadable, then the add-in downloads or copies them into the Case Folder `source` area without allowing path traversal, unsupported extensions, duplicate overwrite, or writes outside the Case Folder.
5. Given the Case Folder manifest is updated, then `manifest.json` records Innola transaction metadata, task id, selected/logged-in user and group assignment, ownership/claim status, attachment provenance, copied source file paths, and load timestamp using lowercase snake_case fields.
6. Given attachment load completes, then source profile detection runs and the detected profile/status is reflected in the workflow state.
7. Given transaction detail retrieval, attachment metadata retrieval, attachment download, Case Folder creation/reopen, or manifest update fails, then the UI shows a clear retryable non-secret error, does not enable Parcel Workflow, and preserves any previously valid Case Folder state.
8. Given transaction load validation passes, then Parcel Workflow becomes enabled for the loaded transaction; selecting a transaction alone remains insufficient.
9. Given Story 2.4 is complete, then claim/start/save/cancel/complete lifecycle behavior is still not implemented here and remains reserved for Story 2.5.

## Tasks / Subtasks

- [x] Add transaction detail and attachment contracts. (AC: 1, 2, 5, 7)
  - [x] Add service interfaces under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/`, such as `IInnolaTransactionDetailService` or a cohesive transaction load service.
  - [x] Add canonical models for transaction detail, case/profile metadata, ownership/claim status, attachment metadata, attachment content/download result, and transaction load result.
  - [x] Include fields needed by the Case Folder manifest: task id, transaction id, transaction number, task name, process step, case type/profile metadata, selected/assigned user, assigned group, ownership status, attachment stable id/name/type/role/size/checksum/download reference, load status, and loaded timestamp.
  - [x] Keep all API response-shape assumptions inside the Innola adapter; UI and workflow code must depend on add-in-owned models only.
  - [x] Do not log or expose access tokens, passwords, raw request bodies, raw response bodies, or signed download URLs.
- [x] Add mock/dry-run transaction detail and attachment provider. (AC: 1, 2, 4, 6, 7, 8)
  - [x] Extend mock mode from Story 2.3 so a sample selected transaction returns realistic metadata and attachments.
  - [x] Provide sample attachment roles covering computation/plan, points TXT/CSV, scanned plan PDF/image, and DWG where practical.
  - [x] Generate or copy small local test fixture files for mock attachment content so ArcGIS Pro dry-run works without network.
  - [x] Include at least one negative fixture in tests for unsupported extension, missing content, wrong-step transaction, or failed download.
  - [x] Mock mode must still require logged-in session and selected transaction state.
- [x] Create the transaction load orchestration service. (AC: 1, 3, 4, 5, 6, 7, 8)
  - [x] Add a service such as `InnolaTransactionLoadService` that coordinates selected transaction validation, detail retrieval, attachment metadata/content retrieval, Case Folder create/reopen, manifest update, source file copy/write, profile detection, and shell gating.
  - [x] Keep orchestration testable without ArcGIS Pro by injecting the session manager, transaction detail service, Case Folder store, source writer/copy service, profile detector, output-root provider, and clock.
  - [x] Ensure the load service refuses work when there is no logged-in session, no selected transaction, or selected transaction does not match the detail response.
  - [x] Ensure load failure leaves `IsTransactionLoaded` false and does not enable Parcel Workflow.
  - [x] Ensure load success sets the transaction-loaded gate only after all required metadata/attachments/manifest/profile steps pass.
- [x] Handle Case Folder naming and create/reopen behavior for Innola transaction identifiers. (AC: 3, 5, 7)
  - [x] Address the current mismatch: `CaseFolderStore` only accepts `TR-SMD-0000001`, while Innola transaction numbers may look like `TR100000004`.
  - [x] Either safely extend validation to support Innola transaction numbers such as `TR100000004`, or add a deterministic safe folder id mapping while preserving the original Innola transaction number in the manifest.
  - [x] Reopen an existing Case Folder for the same transaction when appropriate instead of failing solely because the folder already exists.
  - [x] Do not overwrite an existing Case Folder for a different transaction or mismatched task id.
  - [x] Ensure all generated paths stay inside the configured output root and Case Folder `source` directory.
- [x] Extend manifest contracts with Innola transaction and attachment provenance. (AC: 5, 6, 7)
  - [x] Extend `ManifestDocument`/`ManifestPayload` with lowercase snake_case JSON fields for Innola transaction metadata and attachment provenance.
  - [x] Preserve backward compatibility when reopening existing Epic 1/2.1 Case Folders that do not have Innola metadata.
  - [x] Record both original Innola metadata and local copied source paths.
  - [x] Record attachment provenance without storing tokens, passwords, signed URLs, or raw service payloads.
  - [x] Record detected profile after attachment load and invalidate stale preflight results when source files change.
- [x] Update Transaction Panel and shell behavior for Load. (AC: 1, 7, 8, 9)
  - [x] Change the Transaction Panel `Load` action from session-only selection handoff to actual transaction load.
  - [x] Show loading, success, empty/missing attachment, and retryable error states in the Transaction Panel.
  - [x] Provide or inject a Case Folder output root source. Use a safe local setting or a user prompt; tests should inject this value without UI.
  - [x] On success, show the loaded transaction and Case Folder path and enable Parcel Workflow.
  - [x] On failure, keep Parcel Workflow disabled and present a non-secret error.
  - [x] Do not implement active transaction switching guard, save/cancel prompt, claim/start, or complete button in this story; those belong to Story 2.5.
- [x] Update Parcel Workflow dock pane state after transaction load. (AC: 6, 8)
  - [x] Ensure Parcel Workflow opens to the loaded Case Folder and displays transaction id, current step, source files, detected profile, and intake issues.
  - [x] Preserve existing manual Case Folder behaviors where still needed for tests and recovery.
  - [x] Ensure Parcel Workflow remains disabled before load and becomes enabled only after load validation passes.
- [x] Add focused tests. (AC: 1, 2, 3, 4, 5, 6, 7, 8, 9)
  - [x] Test successful mock transaction load creates or reopens Case Folder, writes attachments into `source`, updates manifest with Innola metadata/provenance, refreshes detected profile, and enables Parcel Workflow.
  - [x] Test selected transaction without logged-in session fails without API calls and keeps Parcel Workflow disabled.
  - [x] Test no selected transaction fails without API calls and keeps Parcel Workflow disabled.
  - [x] Test detail response mismatch with selected transaction fails safely.
  - [x] Test unsupported attachment extension is rejected or blocks load with a clear non-secret error.
  - [x] Test attachment file names cannot traverse outside Case Folder `source`.
  - [x] Test duplicate attachment file names do not overwrite existing files.
  - [x] Test existing Case Folder for same transaction reopens and preserves prior valid artifacts where appropriate.
  - [x] Test existing Case Folder mismatch blocks load.
  - [x] Test manifest serialization remains backward-compatible for manifests without Innola metadata.
  - [x] Test password/token/download reference values do not appear in manifest, status text, logs, reports, or test-written Case Folder files.
  - [x] Test Story 2.5 lifecycle actions remain absent or disabled.
- [x] Validate and package. (AC: 1, 2, 3, 4, 5, 6, 7, 8, 9)
  - [x] Run `tools\validate_contracts.ps1`.
  - [x] Run `tools\run_python_tests.ps1`.
  - [x] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`.
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`.
  - [x] Run `tools\package_addin.ps1`.
  - [x] Manual ArcGIS Pro 3.6 smoke test: automated package generation completed; manual ArcGIS Pro smoke remains user-run in ArcGIS Pro 3.6.

## Dev Notes

### Current Project State

- Story 2.3 is done after code review. It added:
  - `IInnolaTransactionService`, `InnolaTransactionService`, `MockInnolaTransactionService`, transaction row/query/result/status models, and selected transaction state.
  - A compact Transaction Panel with refresh, filter, search, sort, selection, and a `Load` button.
  - Local mock settings in `Settings/WorkflowSettings.json`: `innola_transaction_mode: "mock"` and `innola_process_step: "parcel_workflow"`.
  - Project file packaging for `Settings\WorkflowSettings.json`.
- Current Story 2.3 behavior:
  - Login enables the Transaction Panel.
  - Selecting/loading a row records `SelectedInnolaTransaction`.
  - `IsTransactionLoaded` remains false and Parcel Workflow remains disabled.
  - No Case Folder is created and no attachments are downloaded yet.
- Story 2.4 is the first story allowed to turn a selected transaction into a loaded local Case Folder and enable Parcel Workflow.
- The add-in remains on the ArcGIS Pro 3.6 compatibility lane: `net8.0-windows`, ArcGIS Pro SDK 3.6, Visual Studio 2022, and `Config.daml` `desktopVersion="3.6"`.

### Existing Files To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
  - Current `LoadSelectedTransaction()` only sets selected session state. Replace or extend this path to call transaction load orchestration.
  - Preserve loading-state notifications added in Story 2.3 review.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
  - Add status/error/progress details for transaction load and Case Folder path.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
  - Current `SelectedTransaction` and `IsTransactionLoaded` are separate.
  - Add an explicit method for successful transaction load, e.g. `MarkTransactionLoaded(...)`, rather than setting `IsTransactionLoaded` during selection.
  - Keep clearing selected/loaded state on logout/session expiry.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionService.cs`
  - Existing service lists available tasks through likely `application/getmytasks`.
  - Do not overload this list service with detail/attachment responsibilities unless names remain clear and testable.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/MockInnolaTransactionService.cs`
  - Extend mock support or add a second mock service for transaction details/attachments.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
  - Existing methods create/reopen Case Folders, add source files, refresh profile, and run preflight.
  - Add a transaction-load entry point only if it keeps workflow orchestration clean; otherwise keep Innola load orchestration outside and call existing workflow methods.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
  - Currently validates transaction id with `^TR-SMD-[0-9]{7}$`.
  - This must be updated or wrapped because Story 2.3 mock rows use Innola transaction numbers like `TR100000004`.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileCopyService.cs`
  - Existing copy service handles local paths and duplicate names safely.
  - Downloaded attachments may need a sibling writer that accepts stream/bytes while preserving the same supported-extension and path-safety behavior.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs`
  - Current payload only includes `workflow_state`, `source_files`, and `detected_profile`.
  - Extend with optional Innola transaction metadata and attachment provenance while keeping old manifests readable.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
  - May add a non-secret default output root or mock attachment fixture setting if needed. Do not add credentials or tokens.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/`
  - Continue the explicit console test runner pattern in `Program.cs`.

### Recommended New Components

Use names that fit the existing codebase; exact names can vary if the boundaries remain clear:

```text
Innola/
  IInnolaTransactionDetailService.cs
  InnolaTransactionDetailService.cs
  MockInnolaTransactionDetailService.cs
  InnolaTransactionDetail.cs
  InnolaAttachmentMetadata.cs
  InnolaAttachmentContentResult.cs
  InnolaTransactionLoadResult.cs
  InnolaTransactionLoadService.cs

CaseFolders/
  AttachmentSourceFileWriter.cs
  TransactionFolderId.cs
```

Potential service split:

- `IInnolaTransactionDetailService`: fetch transaction detail and attachment metadata/content.
- `InnolaTransactionLoadService`: app-owned orchestration that validates session/selection, invokes detail/content service, creates/reopens Case Folder, writes manifest/source files/profile, and updates shell gates.

### Innola API Boundary For This Story

Live Swagger was not verified from this environment, so use tolerant adapters and mockable boundaries.

Known starting points from prior Innola add-in analysis:

```text
Base server: https://eltrs.innola-solutions.com/
Likely REST prefix: /api/rest/
Authenticated header: Access-Token
Task list: application/getmytasks
Possible task/transaction routes from prior code:
  workflow/{taskId}/claim
  workflow/{taskId}/complete
  transaction source files / source file download APIs
```

Story 2.4 should implement detail and attachment retrieval through an adapter shaped like:

```text
GetTransactionDetailsAsync(session, selected_transaction)
GetAttachmentContentAsync(session, transaction_detail, attachment)
```

Suggested canonical detail model:

```text
transaction_id
transaction_number
task_id
task_name
process_step
case_type
profile_hint
assigned_user
assigned_group
owner_user
claim_status
attachments[]
```

Suggested attachment metadata:

```text
attachment_id
file_name
extension
mime_type
source_role
category
size
checksum
download_reference
is_required
```

Do not store `download_reference` in the manifest if it is a signed URL, token, or otherwise secret. Store a stable non-secret attachment id/reference instead.

### Case Folder And Manifest Guidance

Current `ManifestDocument` structure:

```json
{
  "schema_version": "1.0.0",
  "transaction_id": "TR-SMD-0000001",
  "run_id": "...",
  "created_at": "...",
  "created_by": "...",
  "source_manifest_hash": null,
  "payload": {
    "workflow_state": "intake",
    "source_files": [],
    "detected_profile": null
  },
  "warnings": [],
  "errors": []
}
```

Recommended optional payload additions:

```json
{
  "innola_transaction": {
    "transaction_id": "100000004",
    "transaction_number": "TR100000004",
    "task_id": "task-100000004",
    "task_name": "Computation Check",
    "process_step": "parcel_workflow",
    "case_type": "parcel_workflow",
    "profile_hint": "scenario_b",
    "assigned_user": "tester",
    "assigned_group": "survey",
    "owner_user": null,
    "claim_status": "available",
    "loaded_at": "2026-06-10T..."
  },
  "attachment_provenance": [
    {
      "attachment_id": "att-computation",
      "file_name": "computation.pdf",
      "source_role": "computation",
      "category": "computation",
      "file_type": ".pdf",
      "file_size": 12345,
      "checksum": "sha256:...",
      "copied_path": "...",
      "downloaded_at": "2026-06-10T..."
    }
  ]
}
```

Keep source files in `payload.source_files` because existing intake/preflight/profile code reads that list.

### Transaction Identifier Guidance

Do not ignore this mismatch:

- Current Case Folder validator accepts `TR-SMD-0000001`.
- Story 2.3 mock/live transaction numbers may be `TR100000004`.

Acceptable approaches:

1. Extend Case Folder validation to support both patterns:
   - `TR-SMD-[0-9]{7}`
   - `TR[0-9]{9}` or a similarly conservative Innola transaction-number pattern.
2. Create a deterministic safe local folder id such as `TR-INNOLA-100000004` and store the original transaction number/id in manifest metadata.

Whichever approach is chosen, tests must prove path traversal is impossible and old `TR-SMD` cases still work.

### UI Behavior Requirements

- Transaction Panel `Load` should:
  - Require login and selected row.
  - Show loading status while detail and attachments are retrieved.
  - Disable duplicate load/refresh actions during load.
  - Show loaded status and Case Folder path on success.
  - Show retryable non-secret status on failure.
- Parcel Workflow command should:
  - Remain disabled after selection only.
  - Become enabled after successful transaction load validation.
  - Open to the loaded Case Folder state and show source files/profile.
- Configuration/output root:
  - Use a non-secret local output-root setting or a prompt/provider abstraction.
  - Tests must inject an output root; do not require interactive UI in automated tests.

### Security Requirements

- Passwords and access tokens remain session-only and in memory.
- Attachment download references are treated as potentially secret.
- Do not write passwords, tokens, signed URLs, raw request bodies, raw response bodies, or full exception dumps to:
  - `manifest.json`
  - source action audit
  - preflight summaries
  - logs/reports
  - status text
  - test artifacts
- Error messages should use non-secret categories such as unauthorized, timeout, invalid response, missing attachment, unsupported file type, or write failure.

### Scope Boundaries

- Do not claim/start a transaction in Innola.
- Do not implement save/cancel/switch-transaction guard.
- Do not implement Complete.
- Do not perform preflight, extraction, validation, output generation, CADINDEX, or ArcGIS Enterprise writeback.
- Do not add production credential persistence.
- Do not rely on live network in automated tests.

### Testing Guidance

- Keep tests in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests` and register them in `Program.cs`.
- Prefer pure service tests over WPF visual tests.
- Use fake/mocked Innola detail and attachment providers.
- Use `TempDirectory` for Case Folder load tests.
- Scan generated Case Folder files for fake secret values.
- Preserve the existing tests for Story 1, Story 2.1, Story 2.2, and Story 2.3.

### References

- [_bmad-output/planning-artifacts/epics.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/epics.md): Story 2.4 acceptance criteria.
- [_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-10.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-10.md): approved Innola transaction workflow correction.
- [_bmad-output/implementation-artifacts/2-3-display-available-innola-transactions.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/implementation-artifacts/2-3-display-available-innola-transactions.md): completed transaction list, selected transaction state, mock mode, and review findings.
- [_bmad-output/implementation-artifacts/2-2-add-sidwell-shell-innola-login-and-session-gating.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/implementation-artifacts/2-2-add-sidwell-shell-innola-login-and-session-gating.md): login/session shell and command gating.
- [_bmad-output/planning-artifacts/architecture.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/architecture.md): Case Folder, manifest, C# add-in, WPF/MVVM, and security boundaries.
- [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs](D:/Code/BMad-Method/dev/pe-jamaica/src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs): current Transaction Panel state and Load command.
- [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs](D:/Code/BMad-Method/dev/pe-jamaica/src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs): selected transaction and transaction-loaded gate.
- [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs](D:/Code/BMad-Method/dev/pe-jamaica/src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs): Case Folder create/reopen and transaction id validation.
- [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileCopyService.cs](D:/Code/BMad-Method/dev/pe-jamaica/src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileCopyService.cs): safe source file copy behavior to mirror for downloaded attachments.
- [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs](D:/Code/BMad-Method/dev/pe-jamaica/src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs): manifest contract to extend.
- [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs](D:/Code/BMad-Method/dev/pe-jamaica/src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs): workflow state, source profile refresh, and preflight invalidation.
- `D:\Code\Innola_Code\arcgis-pro-nscrp-develop\src\Services\InnolaApiService.cs`: prior transaction source file API patterns.
- `D:\Code\Innola_Code\arcgis-pro-nscrp-develop\src\Windows\TaskList\SourceFilesWindowViewModel.cs`: prior source file download pattern.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `tools\validate_contracts.ps1` - passed.
- `tools\run_python_tests.ps1` - passed, 2 tests.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` - passed, 86 tests.
- Review fix rerun: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` - passed, 89 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` - passed, 0 warnings, 0 errors.
- Review fix rerun: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` - passed, 0 warnings, 0 errors.
- `tools\package_addin.ps1` - passed; package produced at `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.esriAddInX`.

### Completion Notes List

- Added add-in-owned Innola transaction detail, attachment metadata/content, and load result models behind mockable service boundaries.
- Implemented mock transaction detail/attachment provider for dry-run with computation, plan, points CSV, and DWG attachments.
- Implemented transaction load orchestration that creates/reopens a Case Folder, writes attachments safely into `source`, records Innola metadata/provenance, refreshes source profile, and enables Parcel Workflow only after validation passes.
- Extended manifest payload with optional `innola_transaction` and `attachment_provenance` fields while preserving old manifest compatibility.
- Extended Case Folder ID validation to support Innola transaction numbers like `TR100000004`.
- Updated Transaction Panel load behavior and Parcel Workflow dock pane sync to open the loaded Case Folder.
- Fixed review findings: failed new loads now preserve any previously loaded Case Folder, partial attachment files are cleaned up on later load failure, and expected adapter exceptions return sanitized retryable errors.
- Live Innola detail/download adapter remains a conservative placeholder until exact production detail/download endpoints are mapped; mock mode is fully functional for dry-run.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/AttachmentSourceFileWriter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/IInnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaAttachmentContentResult.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaAttachmentMetadata.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetail.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailResult.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadResult.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/MockInnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/CaseFolders/CaseFolderStoreTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLoadServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-10 | 0.1 | Initial story context for loading Innola transaction details and attachments into Case Folder. | Mary |
| 2026-06-10 | 1.0 | Implemented transaction load orchestration, manifest provenance, mock attachments, panel load behavior, Case Folder reopen, tests, validation, and packaging. | Codex |
| 2026-06-10 | 1.1 | Addressed code review findings for failed-load preservation, partial attachment cleanup, adapter exception handling, and regression coverage. | Codex |
