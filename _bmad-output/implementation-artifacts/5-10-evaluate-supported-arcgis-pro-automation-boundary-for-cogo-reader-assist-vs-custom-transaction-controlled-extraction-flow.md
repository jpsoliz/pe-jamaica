---
baseline_commit: handoff-2026-06-16
---

# Story 5.10: Evaluate Supported ArcGIS Pro Automation Boundary For COGO Reader Assist Vs Custom Transaction-Controlled Extraction Flow

Status: ready-for-review

## Story

As the Sidwell Co delivery and architecture team for the NLA Jamaica parcel examination add-in,  
I want to evaluate the supported automation boundary between ArcGIS Pro's COGO Reader assist workflow and our custom transaction-controlled extraction flow,  
so that we can decide whether COGO Reader should play any role in the Map Review experience without building core product behavior on unsupported or brittle UI automation.

## Acceptance Criteria

1. Given ArcGIS Pro includes the COGO Reader tool for parcel workflows, when this evaluation is completed, then the team has a clear statement of what is publicly supported through ArcGIS Pro SDK, geoprocessing, ArcPy, or documented command surfaces versus what is only available through manual UI interaction.
2. Given the app workflow is transaction-controlled, when the evaluation is documented, then it explicitly answers whether COGO Reader can be:
   - launched programmatically,
   - launched with a preselected file,
   - prevented from browsing to another file,
   - or observed/controlled after launch
   using supported/public ArcGIS Pro APIs.
3. Given unsupported UI-driving would create product risk, when the recommendation is made, then the story clearly distinguishes supported automation from undocumented/internal command hacks or brittle UI automation.
4. Given the current app already owns source-file routing from Innola transactions, when the evaluation compares alternatives, then it explains the pros/cons of:
   - using COGO Reader only as an optional operator-assist tool,
   - continuing with the custom transaction-controlled extraction flow as the primary path,
   - or combining both approaches in a bounded way.
5. Given Jamaica plan examination sources differ from generic deed-image scenarios, when the evaluation is completed, then it explicitly assesses whether COGO Reader is a good functional fit for:
   - computation sheets,
   - scanned plans/maps,
   - multi-parcel documents,
   - and manual correction workflows.
6. Given ArcGIS Pro Parcel Fabric APIs are already relevant to the app, when the story is completed, then it identifies what parcel-fabric and map-editing capabilities are fully automatable and should remain inside the supported integration boundary even if COGO Reader itself is not.
7. Given the product must remain maintainable across ArcGIS Pro upgrades, when the architecture recommendation is issued, then it names the preferred design boundary and explicitly states whether COGO Reader is:
   - not recommended,
   - optional/manual-assist only,
   - or suitable for deeper integration.
8. Given this evaluation is complete, then the outcome produces concrete next-step guidance for implementation stories, UX changes, toolbar decisions, and transaction-stage behavior.

## Tasks / Subtasks

- [x] Research the official automation boundary. (AC: 1-3, 6)
  - [x] Review official ArcGIS Pro help and SDK references for COGO Reader.
  - [x] Review Parcel Fabric SDK/API capabilities adjacent to COGO Reader workflows.
  - [x] Identify any documented command, pane, or SDK surface related to launching or controlling COGO Reader.
  - [x] Separate officially supported automation from inferred, undocumented, or internal-only behavior.

- [x] Evaluate COGO Reader against our product workflow. (AC: 2-5, 7)
  - [x] Determine whether the tool can be launched programmatically in a supported way.
  - [x] Determine whether a selected transaction file can be injected or preloaded in a supported way.
  - [x] Determine whether file-browse behavior can be disabled or constrained in a supported way.
  - [x] Assess whether COGO Reader fits Jamaica computation sheets and plan/map cases well enough to justify any role in the workflow.

- [x] Compare architecture options. (AC: 4-8)
  - [x] Option A: custom transaction-controlled extraction remains primary; COGO Reader excluded.
  - [x] Option B: custom transaction-controlled extraction remains primary; COGO Reader offered as optional manual-assist during Map Review.
  - [x] Option C: deeper COGO Reader integration if and only if supported APIs exist.
  - [x] Compare each option for maintainability, UX clarity, ArcGIS Pro dependency risk, and domain fit.

- [x] Define the supported integration boundary. (AC: 6-8)
  - [x] Document which ArcGIS Pro capabilities should stay inside the supported integration path:
    - Parcel Fabric creation/use
    - active record / record workflows
    - map loading and zoom
    - snapping and edit tools
    - line/point/parcel editing
  - [x] Document which responsibilities should remain in the Sidwell add-in and custom extraction stack.

- [x] Produce the recommendation and next steps. (AC: 7-8)
  - [x] State the recommended role of COGO Reader in this product.
  - [x] State resulting UX implications for Map Review and any toolbar stories.
  - [x] State follow-on stories or story adjustments required.

## Dev Notes

### Why This Story Exists

- The team wants to know whether ArcGIS Pro's COGO Reader can be productively integrated into the examination workflow.
- The core concern is not only functionality, but supportability: we should not build key workflow behavior on undocumented UI automation.
- This decision affects extraction architecture, Map Review UX, and Parcel Fabric editing strategy.

### Architectural Framing

This story is an **evaluation / architecture boundary decision**, not a commitment to implement COGO Reader integration.

The working assumption entering the story is:

- our add-in owns transaction selection, source routing, workflow state, and custom extraction behavior
- ArcGIS Pro owns map display, editing, snapping, Parcel Fabric workflows, and geometry refinement
- COGO Reader may be considered only if it can be integrated in a supported, maintainable way

### Key Questions To Answer

1. Is there a supported ArcGIS Pro SDK or command surface for launching COGO Reader?
2. Can a selected transaction source file be loaded into COGO Reader programmatically?
3. Can file selection be constrained so the transaction source remains authoritative?
4. If not, is COGO Reader still useful as a manual-assist option during Map Review?
5. Does the Jamaica source-document mix justify keeping our custom extraction path as the primary architecture?

### Likely Recommendation Bias

Unless official documentation proves otherwise, the most likely safe product boundary is:

- keep the custom transaction-controlled extraction flow as the primary architecture
- keep ArcGIS Pro editing / Parcel Fabric / map tools as the supported spatial review surface
- treat COGO Reader, at most, as an optional operator-assist feature rather than a required automated step

This story exists to validate or overturn that bias with evidence.

### Scope Boundaries

- Do not implement deep COGO Reader automation in this story.
- Do not rely on undocumented internal command IDs as the foundation of the recommendation.
- Do not replace the current extraction path based on assumption alone.
- Focus on the boundary between supported ArcGIS integration and custom app responsibility.

### Suggested Outputs

At minimum, this story should produce:

- a short technical evaluation note
- a decision statement
- follow-on implementation guidance

Possible conclusion examples:

- `COGO Reader is not recommended for integrated automation; keep custom extraction primary.`
- `COGO Reader may be offered as optional manual assist during Map Review, but not as a controlled transaction automation surface.`
- `A supported launch-only integration exists, but file-preload/lock is unsupported; use with caution and not as a gating workflow step.`

### References

- `https://doc.esri.com/en/arcgis-pro/latest/help/data/parcel-editing/extractcogofromdeeds.html`
- `https://pro.arcgis.com/en/pro-app/3.6/sdk/api-reference/conceptdocs/docs/ProConcepts-Parcel-Fabric.html`
- `_bmad-output/implementation-artifacts/5-6-add-spatial-review-stage-for-in-map-editing-and-manual-cogo.md`
- `_bmad-output/implementation-artifacts/5-8-implement-true-local-parcel-fabric-output-mode.md`
- `_bmad-output/implementation-artifacts/5-9-add-map-review-editing-toolbar-for-spatial-correction-workflows.md`

## Questions / Follow-up For Implementation

1. If COGO Reader is only viable as manual assist, should it appear as a button inside `Map Review Tools`?
2. If COGO Reader is not recommended, should the toolbar/story focus move fully to native ArcGIS edit tools plus Parcel Fabric workflows?
3. Should this evaluation also produce a UX note about when users should choose:
   - AI/custom extraction,
   - manual point correction,
   - native ArcGIS parcel editing,
   - or optional COGO Reader assist?

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Official ArcGIS Pro help reviewed for COGO Reader behavior
- Official ArcGIS Pro SDK Parcel Fabric concepts reviewed for supported automation boundary
- Repo research notes and related story documents reviewed for architecture alignment

### Completion Notes

- Added a dedicated evaluation note documenting the supported/public ArcGIS Pro automation boundary around COGO Reader versus Parcel Fabric and map-edit workflows.
- Confirmed that the public/documented surface strongly supports Parcel Fabric, map loading, records, and editing workflows, but does not expose the file-preload/lock controls needed to make COGO Reader a transaction-controlled workflow dependency.
- Recommended keeping custom transaction-controlled extraction as the primary architecture and treating COGO Reader, at most, as an optional manual-assist concept.

### File List

- `_bmad-output/implementation-artifacts/5-10-evaluate-supported-arcgis-pro-automation-boundary-for-cogo-reader-assist-vs-custom-transaction-controlled-extraction-flow.md`
- `_bmad-output/planning-artifacts/research/cogo-reader-automation-boundary-evaluation-2026-06-16.md`

### Change Log

- 2026-06-16: Completed the COGO Reader automation-boundary evaluation and documented the recommendation to keep COGO Reader outside the core controlled automation path.
