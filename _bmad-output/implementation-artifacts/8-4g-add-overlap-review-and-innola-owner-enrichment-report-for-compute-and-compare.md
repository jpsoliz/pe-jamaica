---
baseline_commit: handoff-2026-08-17
---

# Story 8.4G: Add Overlap Review And Innola Owner Enrichment Report For Compute And Compare

Status: drafted

## Story

As a cadastral examiner reviewing Compute or Compare results in ArcGIS Pro,  
I want to run an overlap review against configured cadastral reference layers and enrich any overlap hits with Innola/LTF ownership data,  
so that I can produce a clear review report showing where the parcel overlaps, how much overlaps, and who or what is affected before I move the transaction forward.

## Business Context

The current workflow already loads reviewed parcel geometry into ArcGIS Pro and supports legal/fiscal/cadaster evidence search in Compare. However, there is not yet a button-driven review step that:

1. checks the currently loaded parcel/review geometry against configured map layers,
2. measures overlap area and overlap percentage,
3. captures identifying attributes from overlapping features,
4. queries Innola/LTF to retrieve owner or substantial property details for the overlap records, and
5. produces a structured report that ties map imagery to tabular evidence.

This story adds that examiner-facing overlap review capability for both Compute and Compare, with `no overlap found` treated as a valid review outcome rather than a failure.

## Recommendation Captured In This Story

The overlap review should be executed explicitly by the user through a command such as `Run Overlap Review`, not automatically on selection change.

The review uses the parcel/review layers already loaded in the active map. If the expected review geometry or configured comparison layers are missing from the map, the command must block with a clear message instead of running partial logic.

The workflow has three stages that should remain visible in the design and implementation:

1. **Spatial stage**  
   Detect overlaps and compute geometry metrics.
2. **Enrichment stage**  
   Read identifiers from the overlap records and query Innola/LTF for owner/property details.
3. **Report stage**  
   Produce a review artifact with imagery and tabular evidence.

## Delivery Alignment

This story is the coordinated parent story for the overlap-review capability and should stay aligned with the split delivery stories:

- **Story 8.4H** - button-driven overlap engine and persisted spatial review artifact
- **Story 8.4I** - overlap review surface and evidence/report model
- **Story 8.4J** - Innola/LTF owner enrichment for saved overlap-review results

The enrichment step is not background context in this story. It is an explicit required stage between spatial overlap detection and final reporting.

## Current State To Preserve

- Compute and Compare already load local and enterprise review geometry into ArcGIS Pro.
- Compare already contains legal/fiscal/enterprise query infrastructure and Innola-backed evidence lookups.
- The examiner already uses the map as the main review surface.
- Existing Compare search and owner lookup services should be reused instead of recreated.
- `No overlap` must remain a valid and reportable result.
- The workflow must not depend on hidden layers that are not already present in the active map.

## In Scope

- Button-driven overlap review for Compute and Compare
- Configured overlap checks against reference layers already loaded in the map
- Overlap metrics:
  - overlap area
  - overlap percentage
- Identifier extraction from overlap records
- Innola/LTF enrichment using overlap identifiers as an explicit second-stage process after spatial overlap detection
- Map snapshot generation when overlap exists
- Structured overlap review report model
- No-overlap review result generation without image capture

## Out Of Scope

- Automatic execution on selection change
- Full popup-based evidence review UI
- Editing overlap records inside the report surface
- Auto-fixing topology or parcel geometry in this story
- Final transaction approval/submit behavior beyond attaching or saving the report artifact

## Acceptance Criteria

1. Given a Compute or Compare review case is open and the required review geometry is already loaded in the active map, when the user clicks `Run Overlap Review`, then the add-in runs the overlap analysis.
2. Given no active map is available, when the user clicks `Run Overlap Review`, then the command is blocked with a clear examiner-facing message.
3. Given the review parcel/review geometry layer is not loaded in the active map, when the command runs, then it is blocked with a clear message stating that review geometry must be loaded first.
4. Given one or more configured overlap target layers are missing from the active map, when the command runs, then it is blocked with a clear message naming the missing layer roles or names.
5. Given the configured target layers are present, when the command runs, then the add-in checks overlap against each configured source independently, including legal, fiscal, cadastral, and roads layers when enabled.
6. Given an overlap exists between the review parcel and a configured source layer, when analysis completes, then the result records the overlap source, overlap geometry context, overlap area, and overlap percentage.
7. Given no overlap exists for a configured source layer, when analysis completes, then the result explicitly records `No overlap` for that layer instead of treating the layer as failed or skipped.
8. Given no overlap exists across all configured layers, when analysis completes, then the overall result is considered valid and reportable and the user sees a clear `No overlaps found` outcome.
9. Given one or more overlaps exist, when analysis completes, then the add-in captures the configured identifier fields available on each overlap record, including where present:
   - `PID`
   - `vol_folio`
   - `landval_no`
   - `r_number`
   - `pe_number`
   - `pd_number`
10. Given multiple identifiers are available on an overlap record, when Innola/LTF enrichment starts, then the add-in applies a defined identifier priority order rather than querying all identifiers blindly.
11. Given a PID is present, when enrichment starts, then PID is tried first unless configuration defines a different order.
12. Given PID is not present or does not yield a result, when enrichment continues, then fallback identifiers are tried in priority order:
    - `vol_folio`
    - `landval_no`
    - `r_number`
    - `pe_number`
    - `pd_number`
13. Given an overlap record contains a usable identifier, when the Innola/LTF query succeeds, then the overlap result is enriched with owner names and any configured substantial property information returned by the service.
14. Given an overlap record contains no usable identifiers, when enrichment runs, then the result remains spatially valid and is marked as `identifier unavailable`.
15. Given an Innola/LTF query returns no owner/property result, when enrichment completes, then the overlap record is marked as `no owner match found` without failing the spatial review.
16. Given an Innola/LTF query fails because of timeout, auth, unsupported route, or transient server failure, when enrichment completes, then the overlap record is marked with a non-secret retryable diagnostic.
17. Given overlaps exist, when the report is generated, then the add-in captures one or more map images showing the review parcel and the overlap evidence.
18. Given overlap images are captured, when the report is rendered, then each image is tied to the related tabular overlap rows through a stable overlap identifier or group identifier.
19. Given overlaps exist in more than one source layer or more than one feature, when the report is rendered, then the report shows multiple evidence rows rather than collapsing them into one summary line.
20. Given no overlaps exist, when the report is rendered, then it omits map snapshots and instead shows a clear `No overlays found across configured layers` outcome.
21. Given the report includes tabular evidence, when each overlap row is rendered, then it includes at minimum:
    - overlap source/layer
    - overlap id
    - parcel id or review geometry id
    - overlap area
    - overlap percentage
    - identifiers found
    - owner/property enrichment result
    - image reference when applicable
22. Given the overlap result includes roads-layer conflicts, when the report is rendered, then roads overlaps are shown as a separate source category rather than merged into parcel cadaster overlaps.
23. Given Compute uses the overlap report, when the examiner reaches the relevant review stage, then the overlap review can be rerun without corrupting existing local review artifacts.
24. Given Compare uses the overlap report, when the examiner reruns overlap review, then the report refreshes from current map geometry rather than stale cached geometry.
25. Given automated tests run, then overlap geometry detection, no-overlap behavior, identifier extraction, Innola fallback order, owner enrichment status handling, and report-model generation are covered.
26. Given spatial overlap detection completes and one or more candidate identifiers are present, when the next stage begins, then the workflow advances into owner/property enrichment before any final report is marked complete.
27. Given spatial overlap detection returns no overlap rows, when the workflow would otherwise enter enrichment, then enrichment is skipped and the final report remains valid with a clear `No overlaps found` result.

## UX Requirements

- Add a clear action button such as:
  - `Run Overlap Review`
- Place the button in both Compute and Compare where the geometry is already available for review.
- The button must feel examiner-driven, not automatic.
- If the command is blocked, the message must say what is missing:
  - active map
  - review geometry layer
  - configured overlap layer(s)
- The result surface should distinguish:
  - `No overlap`
  - `Overlap found`
  - `Identifier found`
  - `Owner lookup succeeded`
  - `Owner lookup failed`
  - `No owner match`
- Full report details should live in a dedicated overlap review/report surface or saved artifact, not inside a small popup.
- If the user drills into one overlap row, the map should be able to highlight or flash the corresponding overlap feature and review parcel.

## Technical Requirements

- Reuse existing Compare/Innola query services for owner/property enrichment wherever possible.
- Add a dedicated overlap-analysis service that operates on:
  - the current review parcel/review geometry
  - configured target layers already loaded in the active map
- Do not silently load missing reference layers in this story; missing layers should block execution.
- Add a normalized overlap evidence model that keeps spatial and enrichment results separate but linked.
- Persist the overlap-review artifact in a way that preserves stage boundaries so diagnostics can distinguish:
  - spatial overlap status
  - identifier extraction status
  - owner/property enrichment status
  - report readiness
- Support layer-role configuration for at least:
  - legal
  - fiscal
  - cadastral
  - roads
- Support stable identifier-field configuration per overlap source layer.
- Keep diagnostics sanitized:
  - no tokens
  - no passwords
  - no full auth headers
  - no secret response bodies
- Persist report data into case-scoped artifacts so Compute and Compare can be reopened without rerunning the overlap immediately unless the user chooses to rerun.

## Suggested Result Model

Use names that fit the codebase, but the implementation will likely need concepts similar to:

```csharp
public sealed record OverlapReviewRunResult(
    bool Success,
    bool HasAnyOverlap,
    string Message,
    IReadOnlyList<OverlapReviewLayerResult> LayerResults,
    IReadOnlyList<OverlapEvidenceRecord> EvidenceRecords,
    IReadOnlyList<string> SnapshotPaths);

public sealed record OverlapReviewLayerResult(
    string LayerRole,
    string LayerName,
    bool Checked,
    bool HasOverlap,
    string Message,
    int OverlapCount);

public sealed record OverlapEvidenceRecord(
    string OverlapId,
    string LayerRole,
    string LayerName,
    string ReviewParcelId,
    double OverlapAreaSquareMeters,
    double OverlapPercent,
    string? Pid,
    string? VolumeFolio,
    string? LandValNumber,
    string? RNumber,
    string? PeNumber,
    string? PdNumber,
    string? AppliedIdentifierKind,
    string? AppliedIdentifierValue,
    string EnrichmentStatus,
    string? OwnerDisplay,
    string? SourceReference,
    string? SnapshotPath,
    string? Diagnostic);
```

## Files Likely To Change

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/SpatialReview/*`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- new overlap review service/model/report files under:
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow`
- tests under:
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow`

## Settings / Configuration To Include

- overlap review enabled
- overlap target layers by role
- identifier field mappings by target layer
- Innola enrichment identifier priority order
- whether roads overlap participates
- snapshot capture enabled
- minimum overlap area threshold, if needed

## Testing Notes

Run:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare"
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "workflow"
```

Add tests for:

- active map missing blocks command
- missing review geometry blocks command
- missing configured overlap layer blocks command
- no overlap is reported as valid
- overlap area and percentage are computed
- identifier extraction from overlap rows
- identifier priority order for enrichment
- no identifier available does not fail spatial review
- no owner match is represented cleanly
- Innola failure is sanitized and retryable
- snapshot generation only occurs when overlap exists
- report rows link to overlap ids / image references

## Open Questions

1. Should the first implementation save the report locally only, or also attach it back to Innola when Compute/Compare reaches the relevant handoff stage?
2. Should owner enrichment return only owner names in v1, or also wider legal/fiscal record details when available?
3. For roads overlaps, do we need separate rules for road reserve encroachment severity in a follow-up story?

## Tasks / Subtasks

- [ ] Add overlap review command surface in Compute and Compare. (AC: 1-5, 23-24)
- [ ] Add map-state validation for active map, review geometry, and configured target layers. (AC: 2-4, 25)
- [ ] Implement overlap geometry analysis and per-layer result model. (AC: 5-9, 21-22, 25)
- [ ] Implement identifier extraction and Innola/LTF enrichment orchestration as a distinct post-spatial stage. (AC: 10-16, 25-27)
- [ ] Add map snapshot capture for overlap-positive runs only. (AC: 17-20, 25)
- [ ] Add structured overlap review report artifact model. (AC: 18-21, 23-25)
- [ ] Add settings surface for overlap layers, identifier mappings, and enrichment priority. (AC: Technical requirements, settings section)
- [ ] Add regression coverage for overlap outcomes, enrichment, and report output. (AC: 25)

## Notes For Implementation

- Treat `no overlap` as a complete review result, not an error.
- Keep the spatial stage and enrichment stage separable in code so reruns and diagnostics are easier to understand.
- Reuse Compare query services rather than creating a second owner lookup stack.
- Do not try to make the ArcGIS Pro popup the full report UI.
- The overlap review should work from the current map state because the user specifically wants the already-loaded layers to be the analysis surface.

## Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2026-08-17 | 0.1 | Created story for button-driven overlap review with Innola/LTF owner enrichment and evidence reporting for Compute and Compare. | Mary / Winston / Amelia / Codex |
| 2026-08-17 | 0.2 | Tightened the story so the owner/property enrichment stage is explicitly required between overlap detection and reporting, and aligned it to Stories 8.4H, 8.4I, and 8.4J. | Codex |
