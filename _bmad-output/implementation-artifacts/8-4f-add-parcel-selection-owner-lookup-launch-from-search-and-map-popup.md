---
baseline_commit: handoff-2026-07-14
---

# Story 8.4F: Add Parcel Selection Owner Lookup Launch From Search And Map Popup

Status: drafted

## Story

As a cadastral examiner reviewing parcels in ArcGIS Pro,  
I want to launch an Innola owner/property lookup from a selected parcel in the Search/Map experience,  
so that I can retrieve ownership evidence for the clicked parcel without manually retyping PID, Volume/Folio, Land Val No., or PE Number into the Compare search workflow.

## Business Context

The Compare stage already supports manual Innola/cadaster evidence searches by PID, Volume/Folio, Land Val No., and owner/name. However, examiners frequently begin from a selected parcel in the map or Search results, where parcel attributes are already visible in the ArcGIS Pro popup.

This story adds a selection-driven evidence-launch pattern that reuses the existing Compare evidence query services rather than duplicating them. The intent is to support quick examiner investigation from a clicked parcel while keeping ownership evidence review distinct from raw map attributes.

## Current State To Preserve

- Parcel Search already displays parcel features in the map and supports ArcGIS Pro popups.
- Compare already contains legal/evidence search infrastructure and Innola-backed query adapters for:
  - PID
  - Volume/Folio
  - Land Val No.
  - owner/name
- Compare results and valuable evidence review remain the authoritative UI for ownership evidence interpretation.
- The ArcGIS Pro popup currently shows raw feature attributes only.
- The map/search experience must remain responsive and must not trigger uncontrolled background Innola calls on every selection change.

## Recommendation Captured In This Story

The primary UX pattern is:

1. User selects or clicks a parcel.
2. Parcel popup shows parcel attributes and one lightweight action such as `Get Owners`.
3. The command opens or refreshes a dedicated `Parcel Owner Details` pane/window.
4. That pane runs the owner lookup using the best available identifier priority and shows the results in a structured review surface.

Do not make the popup itself the full evidence review UI.

## Acceptance Criteria

1. Given a parcel feature is selected in the Search/map workflow, when the user invokes `Get Owners`, then the add-in reads parcel identifiers from the selected feature and launches an owner/property lookup flow.
2. Given the selected parcel contains multiple possible identifiers, when lookup starts, then the add-in applies the configured identifier priority rather than forcing manual selection first.
3. Given the selected parcel contains a PID, when PID is present and valid, then PID is tried before fallback identifiers unless configuration defines a different priority.
4. Given PID is unavailable or blank, when Volume/Folio is present, then Volume/Folio is used as the next lookup option.
5. Given PID and Volume/Folio are unavailable, when Land Val No. is present, then Land Val No. is used as the next lookup option.
6. Given PE Number is configured as a supported identifier and prior identifiers are unavailable, when PE Number exists, then it is used as a fallback lookup input.
7. Given the popup launch path is used, when the lookup completes, then results are displayed in a dedicated owner-details surface rather than crowding the ArcGIS Pro popup with multi-record evidence details.
8. Given Innola returns one matching property/owner record, when the owner-details surface renders, then it clearly shows the matched identifier, source, owner display, parcel identifiers, and result status.
9. Given Innola returns multiple records, when the owner-details surface renders, then all results remain visible and reviewable with no silent auto-selection.
10. Given Innola returns no matching records, when the lookup completes, then the user sees a clear no-result message tied to the attempted identifier.
11. Given the Innola query fails due to auth, timeout, unsupported route, or unavailable endpoint, when the lookup completes, then a non-secret retryable diagnostic is shown.
12. Given the user wants to continue deeper evidence review, when the owner-details surface is open, then it provides an action to open or hand off into the existing Compare evidence workspace.
13. Given the user uses the Search pane repeatedly, when different parcels are clicked, then the owner lookup can be rerun without breaking the Search results layer, popup, or current selection behavior.
14. Given no parcel is selected, when the user invokes the command from ribbon/popup fallback, then the add-in shows a short validation message and does not call Innola.
15. Given automated tests run, then selection context extraction, identifier-priority routing, no-result/failure behavior, and owner-details rendering state are covered.

## UX Requirements

- Keep the ArcGIS Pro popup lightweight.
- Add one or both lightweight launch paths:
  - popup action/button: `Get Owners`
  - ribbon/button command: `Get Owners For Selected Parcel`
- Do not place a full results grid inside the ArcGIS Pro popup.
- The dedicated owner-details surface should show:
  - selected parcel context
    - PID
    - Volume/Folio
    - Land Val No.
    - PE Number
    - Parish
  - which identifier was used for the live lookup
  - result status
  - one or more owner/property rows
  - source label
  - `Refresh`
  - `Open Compare`
- If desired later, the popup may also show a compact status line such as:
  - `Innola owners: not queried`
  - `Innola owners: 1 record found`
  - `Innola owners: multiple matches found`
- The owner-details surface must clearly distinguish:
  - raw parcel attributes
  - Innola ownership evidence

## Technical Requirements

- Reuse the existing Compare legal cadaster / Innola search services instead of introducing a new owner-query implementation.
- Add a parcel-selection context reader that can extract from a selected parcel feature:
  - PID
  - Volume/Folio
  - Land Val No.
  - PE Number
  - Parish
- Add a dedicated orchestration layer that:
  - resolves selected parcel context
  - applies configured identifier priority
  - runs the corresponding Compare/Innola query service
  - normalizes the response into an owner-details view model
- Support at least one stable launch path independent of popup customization:
  - selected parcel + command button
- Popup integration may be implemented as:
  - a popup action
  - a context menu command
  - or another ArcGIS Pro command hook
  but must call the same backend service seam as the primary command.
- Do not auto-query on every selection change in this story.
- Redact tokens, passwords, headers, and raw unauthorized bodies from diagnostics.
- Preserve separation between:
  - Search/Map parcel context
  - Compare evidence review
- If the compare workspace is already open, support handing off the selected identifiers into Compare rather than forcing duplicate re-entry.

## Suggested Components

Use names that fit the codebase, but the implementation likely needs these concepts:

```csharp
public sealed record ParcelSelectionContext(
    string? ParcelId,
    string? Volume,
    string? Folio,
    string? LandValuationNumber,
    string? PeNumber,
    string? Parish,
    string SourceLayerName,
    string SourceObjectId);

public sealed record OwnerLookupAttempt(
    string IdentifierKind,
    string IdentifierValue,
    bool Success,
    string Message,
    string? Diagnostic = null);

public sealed record ParcelOwnerLookupResult(
    ParcelSelectionContext ParcelContext,
    string? AppliedIdentifierKind,
    string? AppliedIdentifierValue,
    IReadOnlyList<LegalCadasterRecord> Records,
    string Message,
    string? Diagnostic = null);
```

## Files Likely To Change

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearchDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearchDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareEvidenceModels.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ShellState.cs`
- new owner-details view/presenter files under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn`
- tests under:
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelSearch`

## Testing Notes

Run:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare"
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "parcelsearch"
```

Add tests for:

- no selection blocks lookup
- PID-first priority uses PID when available
- Volume/Folio fallback is used when PID is blank
- Land Val fallback is used when PID and Volume/Folio are blank
- PE Number fallback is used only when enabled/configured
- owner lookup diagnostics do not leak secrets
- popup/ribbon launch paths call the same orchestrator
- multiple returned rows render without truncation
- handoff to Compare preloads the selected identifier context

## Open Questions

- Confirm whether PE Number has a real Innola search route or should only seed Compare/manual evidence.
- Confirm whether the owner-details surface should be a dockpane, modal window, or embedded Compare-side panel.
- Confirm whether parcel-click lookup should support fiscal context rows in v1 or legal ownership only.
- Confirm whether popup integration should be implemented in the first pass or follow the stable selection-command path.

## Tasks / Subtasks

- [ ] Define parcel selection context model and extraction service. (AC: 1-6, 14-15)
- [ ] Add owner lookup orchestration using existing Compare/Innola query services. (AC: 1-11, 15)
- [ ] Add dedicated owner-details UI surface with refresh and Compare handoff actions. (AC: 7-13)
- [ ] Add stable selected-parcel launch command independent of popup customization. (AC: 1, 13-14)
- [ ] Add optional popup/context-menu launch hook that reuses the same backend command. (AC: 1, 7, 13)
- [ ] Add regression coverage for selection context, identifier priority, launch behavior, and diagnostics. (AC: 15)

## Notes For Implementation

- Start with the stable path: selected parcel + command.
- Treat popup/context-menu as a thin launcher over the same service seam.
- Keep the owner-details surface read-only; do not turn it into a second Compare editor.
- If no clicked/selected feature is available, do not fall back to ambiguous global search automatically.

