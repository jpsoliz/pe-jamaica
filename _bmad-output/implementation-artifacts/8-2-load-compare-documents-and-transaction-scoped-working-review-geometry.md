---
baseline_commit: handoff-2026-07-14
---

# Story 8.2: Load Compare Documents And Transaction-Scoped Working Review Geometry

Status: review

## Story

As a cadastral examiner in Compare,  
I want the workspace to load the transaction attachments and the matching `working_review` geometry for the selected transaction,  
so that I can reconcile ownership evidence against the exact geometry produced by Compute.

## Business Context

Compute already downloads transaction attachments into a Case Folder and publishes approved geometry to Enterprise `working_review` layers. Compare should reuse those foundations instead of re-extracting geometry or relying on local-only output paths. The center map should be a real ArcGIS Pro map/layer surface, not a static image slot.

The working geometry must be filtered to the selected transaction scope. Current Enterprise working settings use `transaction_scope_field = transaction_number`; the user has sometimes said `transaction_id`, so the implementation must honor the configured field and not hard-code either one.

## Acceptance Criteria

1. Given a Compare transaction is loaded, when the workspace opens, then attached documents are available using the same local Case Folder attachment/source loading conventions as Compute.
2. Given `enterprise_working_review` is enabled and configured, when Compare loads geometry, then the add-in loads polygons, lines, and points from the configured working layers.
3. Given the configured transaction scope field is `transaction_number`, when transaction `TR100000674` or `100000674` is loaded, then the geometry query/filter uses the normalized transaction number value expected by the working layers.
4. Given the configured transaction scope field is changed to another supported field, when Compare loads geometry, then the query/filter uses that field instead of a hard-coded value.
5. Given matching working polygons exist, when they are added to the active map, then the map zooms to the transaction extent with reasonable padding and includes all parcels for the transaction.
6. Given working lines and points exist, when geometry loads, then they are grouped with the polygons and displayed read-only for Compare context.
7. Given no matching working polygon is returned, when Compare opens, then the workspace shows a blocking geometry-unavailable state and disables Compare approval.
8. Given ArcGIS Portal authentication is required for hosted layers, when geometry loads, then the operation uses the shared Enterprise/Portal auth provider and never asks the user to paste tokens into the Compare UI.
9. Given the active map is unavailable, when geometry load is requested, then the workspace shows a retryable status and keeps documents/evidence panels available.
10. Given automated tests run, then layer target resolution, scope-field query construction, no-geometry state, and map-load result handling are covered without requiring live Enterprise access.

## Tasks / Subtasks

- [x] Add Compare geometry loading service. (AC: 2-8)
  - [x] Create a focused service such as `ICompareWorkingGeometryService`.
  - [x] Read layer URLs and `TransactionScopeField` from `InnolaTransactionSettings.EnterpriseWorkingReview`.
  - [x] Build safe scope queries for points, lines, and polygons.
  - [x] Keep the service mockable for unit tests.

- [x] Add map integration for Compare. (AC: 5-7, 9)
  - [x] Add an ArcGIS Pro map integration service that creates/updates a transaction group layer.
  - [x] Apply definition queries or service-layer query filters for the transaction scope.
  - [x] Zoom to the polygon extent.
  - [x] Mark layers read-only or avoid exposing editing commands in Compare.

- [x] Reuse document loading. (AC: 1)
  - [x] Reuse `InnolaTransactionLoadService` / Case Folder source files for Compare.
  - [x] Do not duplicate attachment download logic.
  - [x] Ensure Compare can open an already-loaded/restored Case Folder.

- [x] Add workspace load state model. (AC: 1-10)
  - [x] Track document load, geometry load, no-geometry blocker, and map unavailable states separately.
  - [x] Keep document viewing available if geometry fails.
  - [x] Keep geometry context available if one cadaster query fails in later stories.

- [x] Add tests. (AC: 1-10)
  - [x] Unit-test query construction for `transaction_number`.
  - [x] Unit-test alternate scope field use.
  - [x] Unit-test no polygon result blocks approval.
  - [x] Unit-test active map unavailable status.
  - [x] Unit-test settings warnings when working layer targets are missing.

## Developer Notes

Relevant existing files:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputSummaryPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingStateRestoreService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Enterprise/PortalAuth/ArcGisProPortalAuthProvider.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`

Current configured working layers:

- `enterprise_working_review.layers.points`
- `enterprise_working_review.layers.lines`
- `enterprise_working_review.layers.polygons`
- `enterprise_working_review.layers.issues`
- `enterprise_working_review.layers.case_index`

Compare should consume the working service directly through a transaction filter. It should not depend on local `output_summary.json` being present, because Compare may be opened by another machine/user after Compute completed.

## UX References

- Compare mockup center panel: `mockups/compare-workspace-evidence-reconciliation.html`
- EXPERIENCE rules: Compare map panel is read-only and filtered to `working_review` transaction scope.

## Testing Notes

Run:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj
```

## Open Questions

- Confirm whether the Enterprise working layers store transaction scope as `100000674`, `TR100000674`, or both forms. The service should centralize normalization once confirmed.

## Dev Agent Record

### Implementation Plan

- Add a Compare geometry service that builds transaction-scoped working layer requests from `enterprise_working_review`.
- Add a thin ArcGIS Pro map integration service that groups polygons, lines, and points under a transaction Compare group and applies read-only scoped definition queries.
- Add a workspace load state that keeps document loading separate from geometry loading so documents remain available during geometry/map failures.
- Cover scope normalization, alternate scope fields, no-polygon blockers, map-unavailable retry state, and missing layer targets without live Enterprise dependencies.

### Debug Log

- First build failed because `CIMFeatureLayer.DefinitionExpression` is not available in this ArcGIS SDK version.
- Switched Compare map filtering to `FeatureLayer.SetDefinitionQuery(...)`.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with one pre-existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` passed: 366 tests.

### Completion Notes

- Implemented `ICompareWorkingGeometryService` with safe scope-field validation, transaction-number normalization from `TR100000674` to `100000674`, and escaped definition-query construction.
- Implemented `ICompareMapIntegrationService` / `ArcGisCompareMapIntegrationService` to load configured working polygons, lines, and points into a Compare group layer, apply definition queries, mark feature layers non-editable, and zoom to polygons.
- Added `CompareWorkspaceLoadService` and state records so Compare reuses `InnolaTransactionLoadService` for Case Folder documents while separately tracking geometry blockers and retryable active-map failures.
- Added unit coverage for layer request construction, alternate scope fields, no-polygon approval blocking, map-unavailable retry state, missing working layer targets, and query escaping.

### File List

- `_bmad-output/implementation-artifacts/8-2-load-compare-documents-and-transaction-scoped-working-review-geometry.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/ArcGisCompareMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkingGeometryService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkingGeometryServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

### Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-07-14 | 1.0 | Added Compare document/geometry load state, transaction-scoped working layer load plans, ArcGIS map integration, and regression coverage. | Amelia / Codex |
