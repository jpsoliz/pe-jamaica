---
baseline_commit: NO_VCS
---

# Story 2.2: Add Sidwell Shell, Innola Login, and Session Gating

Status: done

## Story

As a cadastral technical staff user,
I want the add-in to provide a Sidwell Co shell with Innola login and command gating,
so that only authenticated users can access assigned transactions and parcel workflow actions.

## Acceptance Criteria

1. Given ArcGIS Pro starts with no active Innola session, when the add-in ribbon and dock panes load, then Login and About are enabled, Transaction Panel and Parcel Workflow are disabled, Configuration exposes only safe local preferences to general users, and no Innola password or access token is loaded from disk.
2. Given the user opens Login, when valid Innola credentials are submitted to the configured environment `https://eltrs.innola-solutions.com/`, then the add-in stores password/token data only in memory for the current ArcGIS Pro session, the logged-in user identity and group context are available to the add-in, Transaction Panel becomes enabled, Parcel Workflow remains disabled until a transaction is selected and loaded, and failed login returns a clear non-secret error.
3. Given the user logs out or the session expires, when the session state changes to logged out, then Transaction Panel and Parcel Workflow are disabled, in-memory token/password state is cleared, and logs, reports, manifests, and status views do not contain secrets.

## Tasks / Subtasks

- [x] Add Innola session and login service contracts. (AC: 1, 2, 3)
  - [x] Create an `Innola/` or `Integration/Innola/` folder under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/`.
  - [x] Add session models such as `InnolaSession`, `InnolaUserContext`, and `InnolaLoginResult` with access token, username, display/full name, groups/roles, server URL, expiration/status where available, and non-secret error text.
  - [x] Add an injectable `IInnolaAuthService` with `LoginAsync(...)`, `Logout()`, session-state access, and optional ping/refresh hook.
  - [x] Add a default server setting using `https://eltrs.innola-solutions.com/`.
  - [x] Keep all password and token values in memory only; do not add them to `WorkflowSettings.json`, `manifest.json`, logs, reports, or Case Folder artifacts.
- [x] Implement session state and command gating. (AC: 1, 2, 3)
  - [x] Add an `InnolaSessionManager` or equivalent service that exposes `IsLoggedIn`, `CurrentUser`, `StatusText`, and state-change notifications.
  - [x] Create command gate properties for `CanOpenTransactionPanel`, `CanOpenParcelWorkflow`, and `CanOpenConfiguration`.
  - [x] Ensure logged-out default state enables only Login, About, and safe local Configuration.
  - [x] Ensure login success enables Transaction Panel but keeps Parcel Workflow disabled until a transaction-loaded state exists in later stories.
  - [x] Ensure logout/session-expired clears token/password state and disables Transaction Panel and Parcel Workflow.
- [x] Update DAML/ribbon shell for Sidwell Co commands. (AC: 1)
  - [x] Extend `Config.daml` from the single Parcel Workflow button to the approved shell: Login, Transaction Panel, Configuration, Parcel Workflow, About.
  - [x] Keep the existing `Sidwell Co` tab/group naming; do not reintroduce the old `Innola` group caption.
  - [x] Add button classes for each command or a shared pattern where appropriate.
  - [x] Use ArcGIS Pro DAML conditions or button `OnUpdate`/`Enabled` logic where practical, backed by the central session manager.
  - [x] Keep ArcGIS Pro 3.6 compatibility and the nested dock pane `<content className="...">` pattern.
- [x] Add Login UI. (AC: 2, 3)
  - [x] Add a WPF `ProWindow` login dialog with server, username, password, login/cancel, and clear status/error text.
  - [x] Default server to `https://eltrs.innola-solutions.com/`; allow changing it for dev/test without persisting passwords.
  - [x] Do not include a persistent "save password" option. Session-only password use is allowed only for current-session reauth/ping if the service needs it.
  - [x] Avoid showing raw exception text that can include URL credentials, tokens, request payloads, or stack traces.
  - [x] After successful login, activate or enable the Transaction Panel entry point; do not auto-create a Case Folder and do not enable Parcel Workflow yet.
- [x] Add placeholder Transaction Panel and safe Configuration entry points. (AC: 1, 2)
  - [x] Add a Transaction Panel dock pane or placeholder surface that shows logged-out/empty state and can be enabled after login; full task list behavior belongs to Story 2.3.
  - [x] Add a minimal Configuration command/window or placeholder for safe local preferences only; do not expose hidden/server-managed Innola configuration to general users.
  - [x] Preserve the existing Parcel Workflow dock pane and its Case Folder/preflight behavior, but gate opening or interaction behind the new shell state.
- [x] Add focused tests. (AC: 1, 2, 3)
  - [x] Add unit tests for session default logged-out state, successful login state, failed login state, logout/session-expired clearing secrets, and command gate booleans.
  - [x] Add tests that prove credentials/tokens are not written to `WorkflowSettings.json`, `manifest.json`, `preflight_summary.json`, source action audit, or any Case Folder file touched by this story.
  - [x] Add tests that Parcel Workflow gate remains false after login when no transaction is loaded.
  - [x] Add tests around button command/view-model gating with fake auth service; do not require live Innola network access in automated tests.
- [x] Validate and package. (AC: 1, 2, 3)
  - [x] Run `tools\validate_contracts.ps1`.
  - [x] Run `tools\run_python_tests.ps1`.
  - [x] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`.
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`.
  - [x] Run `tools\package_addin.ps1`.
  - [x] Smoke-test in ArcGIS Pro 3.6: package/register validation passed; visual confirmation in ArcGIS Pro remains the manual review step.

## Dev Notes

### Current Project State

- Story 2.1 is done. It added manifest preflight contracts, `ManifestPreflightService`, preflight workflow states, dock pane preflight UI, and regression fixes for stable preflight hashes, intake invalidation, and corrupt manifest blockers.
- Epic 1 is done and remains the local Case Folder foundation. This story must not break manual Case Folder creation/reopen behavior because later transaction loading will reuse those services.
- The add-in is on the ArcGIS Pro 3.6 compatibility lane: `net8.0-windows`, ArcGIS Pro SDK 3.6, Visual Studio 2022, and DAML `desktopVersion="3.6"`.
- Packaging path is `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.esriAddInX`.

### Existing Files To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`
  - Current state: one `Sidwell Co` tab/group with only `ParcelWorkflow_ShowDockpaneButton`, plus `ParcelWorkflow_Dockpane` using nested `<content className="ParcelWorkflowDockpane" />`.
  - Change: add Login, Transaction Panel, Configuration, Parcel Workflow, and About commands. Preserve the nested dock pane content pattern that fixed the blank pane in ArcGIS Pro 3.6.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ShowParcelWorkflowDockpaneButton.cs`
  - Current state: activates `ParcelWorkflowDockpaneViewModel.DockPaneId` unconditionally.
  - Change: gate this command through the session/transaction-loaded state. In Story 2.2, it should remain disabled or show a non-secret "Load a transaction before opening Parcel Workflow" status because transaction-loaded is not implemented yet.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
  - Current state: owns Case Folder creation/reopen/source/preflight commands directly through `WorkflowSession`.
  - Change: integrate with shell/session state without moving Case Folder business rules into UI code. Preserve existing `WorkflowSession` behavior and tests.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
  - Current state: compact technical pane with manual transaction id/output folder, source files, intake, artifacts, and preflight.
  - Change: may show logged-out or transaction-required state, but full UX refactor belongs to later stories. Keep the UI compact and ArcGIS Pro-adjacent.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Module1.cs`
  - Current state: ArcGIS Pro module singleton.
  - Change: can host application-wide session manager/service access if needed, but do not turn it into a large service locator unless the pattern remains simple and testable.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/RelayCommand.cs`
  - Current state: simple reusable command with optional `CanExecute` and `RaiseCanExecuteChanged`.
  - Change: reuse for new ViewModel/window commands.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
  - Current state: explicit lightweight console test runner.
  - Change: register new session/gating tests here.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
  - Current state: stores non-secret toolchain settings.
  - Change: may add a non-secret default Innola server/environment field if useful. Must not store password or token.

### Previous Innola Add-in Reuse Guidance

Source project: `D:\Code\Innola_Code\arcgis-pro-nscrp-develop`.

Reuse the patterns, not the implementation wholesale:

- `src\Config.daml`: shows ArcGIS Pro ribbon command registration for Login, Tasks Panel, Settings, About, and dock pane content. Adapt to `Sidwell Co` naming and ArcGIS Pro 3.6 DAML already used here.
- `src\LoginWindow.xaml.cs`: uses `ArcGIS.Desktop.Framework.Controls.ProWindow`, disables the login button during async login, calls `InnolaApiService.LoginAsync`, starts a ping background task, unlocks widgets, activates the task list pane, and publishes load-tasks.
- `src\Services\LoginDataService.cs`: keeps the access response in static memory with locking. Good pattern for session-only storage.
- `src\Services\InnolaApiService.cs`: has endpoints/patterns for `authenticate`, `currentUserDetails`, `application/getmytasks`, `ping`, `workflow/{taskId}/claim`, and `workflow/{taskId}/complete`.
- `src\Services\ApiService.cs`: adds the token as `Access-Token` and retries after `Unauthorized` by relogging in.
- `src\TaskListDockpaneViewModel.cs`: gates `IsEnabled` from `LoginDataService.GetAccess().AccessToken`, refreshes task rows, assigns unassigned tasks, and activates task-related panels.
- `src\TaskListDockpane.xaml`: compact WPF toolbar/filter/search/sort layout for the future transaction panel.

Do not copy these parts without changing them:

- Do not persist encrypted/saved passwords. The previous project supports saved password; this project allows password only during the ArcGIS Pro session.
- Do not let API services show MessageBoxes internally. Return typed success/failure results and let UI commands decide how to display non-secret errors.
- Do not use the old `Innola` ribbon group/caption. This add-in must be under `Sidwell Co`.
- Do not enable Spatial Units, export map, or unrelated old project controls in this story.

### Innola API Boundary For This Story

Live Swagger was not reachable from this environment during prior analysis, so implement behind a mockable boundary and keep endpoint paths configurable. The previous project indicates these likely paths:

- Base server: `https://eltrs.innola-solutions.com/`
- REST prefix: `/api/rest/`
- Login: `authenticate`
- Current user: `currentUserDetails`
- Ping: `ping`

Minimum login request/response contract for Story 2.2:

```text
request: login, password
response: access token, username/login, display/full name if available, groups/roles if available, server, expiry/status if available
header for authenticated requests: Access-Token
```

If the actual API response differs, isolate the mapping inside `InnolaAuthService` and keep the rest of the add-in dependent on `InnolaSession` only.

### Session And Gating Semantics

Recommended shell states:

```text
logged_out
authenticating
logged_in
session_expired
```

Story 2.2 does not introduce `transaction_loaded`; it should expose a false gate or placeholder for it so Story 2.4 can enable Parcel Workflow after a selected transaction is loaded and validated.

Command behavior:

- Login: enabled when logged out/session expired; may also support "switch user" later, but not required here.
- Transaction Panel: disabled until `logged_in`.
- Parcel Workflow: disabled until both `logged_in` and transaction loaded. In Story 2.2, transaction loaded is always false unless an explicit placeholder is added for tests.
- Configuration: enabled only for safe local preferences; hidden/server-managed Innola settings are not user-editable.
- About: always enabled.

### Security Requirements

- Password and token data must be session-only, in memory, and cleared on logout/session expiry.
- Do not write secrets to `WorkflowSettings.json`, `manifest.json`, `preflight_summary.json`, source action audit, reports, logs, or status text.
- Do not include raw request payloads, tokens, passwords, or full exception dumps in user-facing errors.
- If implementing retry-after-unauthorized, do not persist the password for future ArcGIS Pro sessions. Holding it in memory during the same session is allowed because the user approved session-only password retention.
- Tests should search all files written by this story for the test password/token values.

### Architecture Requirements

- C# owns add-in shell, command gating, WPF/MVVM UI, and ArcGIS Pro integration.
- Keep processing/Case Folder state in `WorkflowSession`; keep Innola session state separate so login/logout does not corrupt existing Case Folder artifacts.
- Long-running or network login calls must be async and must not freeze ArcGIS Pro UI.
- WPF UI updates occur on the UI thread.
- Use file-based JSON contracts and lowercase snake_case only when this story writes project artifacts. This story should not create processing artifacts.
- Do not call Python, ArcPy, extraction, validation, output generation, CADINDEX, or ArcGIS Enterprise writeback.

### UX Requirements

- The first-viewport signal in ArcGIS Pro should be the `Sidwell Co` add-in shell, not the old Innola branding.
- Use compact WPF/ArcGIS Pro-adjacent layout: Segoe UI, restrained neutral surfaces, tight spacing, no landing-page/marketing composition, no nested cards.
- Use direct microcopy:
  - `Not logged in.`
  - `Logged in as {name}.`
  - `Load a transaction before opening Parcel Workflow.`
  - `Login failed. Check user name, password, and server.`
- Keep disabled states visible and understandable through tooltip/status text where possible.
- Do not add visible instructional paragraphs explaining how the feature works; use labels, status, and tooltips.

### Testing Guidance

- Continue the existing lightweight C# console test runner pattern in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests`.
- Use fake auth services for login success/failure/session expiry tests.
- Automated tests must not require network access or real Innola credentials.
- Test command gating through pure services/view-model properties where possible. DAML visual smoke testing remains manual in ArcGIS Pro.
- Keep existing 56 tests passing.

Recommended tests:

- default session is logged out and has no token/password
- successful login stores token/password in memory and exposes user context
- failed login does not create a session and exposes non-secret error
- logout clears token/password/current user
- session expiry clears gates
- Transaction Panel gate is false before login and true after login
- Parcel Workflow gate is false before login and remains false after login with no loaded transaction
- safe Configuration and About gates are true while logged out
- no test password/token appears in `WorkflowSettings.json` or any Case Folder file created during the test

### Scope Boundaries

- Do not implement transaction list retrieval or task rows; Story 2.3 owns that.
- Do not implement transaction detail/attachment download into Case Folder; Story 2.4 owns that.
- Do not implement active transaction save/cancel/complete lifecycle; Story 2.5 owns that.
- Do not implement ArcGIS/Python environment checks; Story 2.6 owns that.
- Do not inspect DWG readiness; Story 2.7 owns that.
- Do not build full processing/profile configuration; Story 2.8 owns that.
- Do not perform live CADINDEX/Enterprise updates.

### References

- [_bmad-output/planning-artifacts/epics.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/epics.md): FR24-FR28, UX-DR19-UX-DR22, Epic 2 goal, and Story 2.2 acceptance criteria.
- [_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-10.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-10.md): approved course correction and handoff plan.
- [_bmad-output/planning-artifacts/architecture.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/architecture.md): ArcGIS Pro SDK add-in, DAML, WPF/MVVM, command gating, security, and Case Folder boundaries.
- [_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md): compact ArcGIS Pro-adjacent visual design tokens.
- [_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md): dock pane behavior, credential secrecy, and compact desktop UX.
- [_bmad-output/implementation-artifacts/2-1-run-manifest-preflight.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/implementation-artifacts/2-1-run-manifest-preflight.md): previous story learnings, files touched, and validation commands.
- [docs/toolchain.md](D:/Code/BMad-Method/dev/pe-jamaica/docs/toolchain.md): ArcGIS Pro 3.6 lane and packaging command.
- `D:\Code\Innola_Code\arcgis-pro-nscrp-develop\src\Config.daml`: previous Innola ribbon/dock pane pattern.
- `D:\Code\Innola_Code\arcgis-pro-nscrp-develop\src\LoginWindow.xaml.cs`: previous login `ProWindow` pattern.
- `D:\Code\Innola_Code\arcgis-pro-nscrp-develop\src\Services\LoginDataService.cs`: in-memory access pattern.
- `D:\Code\Innola_Code\arcgis-pro-nscrp-develop\src\Services\InnolaApiService.cs`: prior auth/user/ping/task endpoint pattern.
- `D:\Code\Innola_Code\arcgis-pro-nscrp-develop\src\TaskListDockpaneViewModel.cs`: prior panel gating and post-login task refresh pattern.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Red phase: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` failed because `ParcelWorkflowAddIn.Innola` session contracts did not exist.
- Green phase: C# test runner passed 61 tests after adding the Innola session layer.
- Regression expansion: C# test runner passed 62 tests after adding secret leak coverage.
- `tools\validate_contracts.ps1` passed.
- `tools\run_python_tests.ps1` passed: 2 Python tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` passed with 0 warnings and 0 errors.
- `tools\package_addin.ps1` passed and produced `ParcelWorkflowAddIn.esriAddInX`.
- Review patch validation: C# test runner passed 64 tests after user/group context mapping and synchronization-context regression coverage.
- Review patch validation: `tools\validate_contracts.ps1`, `tools\run_python_tests.ps1`, `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`, and `tools\package_addin.ps1` passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added session-only Innola auth contracts, HTTP auth adapter, session manager, user/session models, and shell state.
- Added command gates so Transaction Panel requires login and Parcel Workflow remains disabled until a future transaction-loaded state.
- Expanded the Sidwell Co ribbon group to Login, Transaction Panel, Configuration, Parcel Workflow, and About.
- Added Login, Transaction Panel placeholder, Configuration, and About WPF surfaces.
- Added automated tests for login state, logout/expiry, command gates, and secret non-persistence.
- Applied code review patches so login response user/group/role fields are mapped into `InnolaUserContext`, and session change notifications after async login preserve the caller synchronization context for WPF safety.

### File List

- `_bmad-output/implementation-artifacts/2-2-add-sidwell-shell-innola-login-and-session-gating.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/AboutWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/AboutWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/IInnolaAuthService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaAuthService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaLoginResult.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionStatus.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaUserContext.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/LoginWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/LoginWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ShowAboutWindowButton.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ShowConfigurationWindowButton.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ShowLoginWindowButton.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ShowParcelWorkflowDockpaneButton.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ShowTransactionPanelButton.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaSessionManagerTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaAuthServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Senior Developer Review (AI)

### Review Outcome

Approved after patch.

### Findings

- [x] [Review][Patch] Login success did not hydrate user group context from API response [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaAuthService.cs]. AC2 requires logged-in user identity and group context to be available; the original adapter created an `InnolaUserContext` with empty groups/roles even when the response could include them. Patched `InnolaAuthService` to map username, display/full name, groups, and roles from root or nested `value` response payloads and added regression coverage.
- [x] [Review][Patch] Session change notifications after async login could fire off the WPF caller context [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs]. The manager used `ConfigureAwait(false)` before mutating state and raising `SessionChanged`; WPF subscribers can receive property notifications from a thread-pool continuation. Patched session manager awaits to preserve caller context and added a synchronization-context regression test.

### Residual Risk

- Live Innola endpoint behavior is still unverified in this environment; auth parsing is deliberately tolerant and Story 2.3 should confirm real task/user payloads against the server.

### Change Log

- 2026-06-10: Story 2.2 created and marked ready-for-dev.
- 2026-06-10: Implemented Story 2.2 Sidwell shell, Innola login/session state, command gating, placeholder panels, tests, validation, and package generation; moved story to review.
- 2026-06-10: Applied code review patches for user/group context mapping and WPF-safe session notifications; moved story to done.
