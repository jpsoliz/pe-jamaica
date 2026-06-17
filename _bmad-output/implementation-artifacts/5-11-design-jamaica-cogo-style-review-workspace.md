---
baseline_commit: handoff-2026-06-16
---

# Story 5.11: Design Jamaica COGO-Style Review Workspace

Status: ready-for-dev

## Story

As a cadastral examiner using the Sidwell Co ArcGIS Pro add-in,  
I want a Jamaica-specific review workspace inspired by COGO Reader,  
so that I can inspect source documents, review OCR/AI extraction results, and understand parcel/line interpretation in one controlled transaction workflow before spatial editing begins.

## Story Requirements

### User Story Statement

The current workflow already produces review data and later creates map-review outputs, but the examiner experience is split across several stages and generic ArcGIS Pro surfaces. The product needs a dedicated Jamaica-specific review workspace that keeps the transaction source document, extracted result set, parcel grouping, and parcel/line preview together in a way that matches plan-examination work rather than generic deed-reading workflows.

### Acceptance Criteria

1. Given the product needs a Jamaica-specific alternative to Esri's built-in COGO Reader, when the UX story is completed, then it defines a custom review workspace pattern rather than relying on unsupported automation inside the built-in COGO Reader.
2. Given the review workspace must support computation sheets, scanned plans/maps, TIFF/PNG/JPG files, and future related source types, when the design is documented, then it defines a left-panel source viewer that can host PDF/image verification and clearly defines fallback behavior for unsupported file types such as TXT/CSV.
3. Given OCR/AI/custom extraction results are the examiner's primary working dataset, when the design is documented, then it defines a center review surface that shows extracted rows in a compact editable table with only the fields needed for this workflow, including point identifier, easting, northing, status, parcel grouping, line/bearing/distance values where relevant, and unresolved/manual state markers.
4. Given the Jamaica workflow may include multiple parcels in one source document, when the design is documented, then it explicitly defines how parcel tabs, parcel grouping, parcel switching, boundary-break handling, and "current parcel in focus" behavior should appear in the review workspace.
5. Given the examiner needs a fast mental model of the parcel under review, when the design is documented, then it includes a right-side parcel/line interpretation panel and a bottom or inset point/traverse preview that visually represents the currently selected parcel/group.
6. Given users may need to review and correct extracted results before spatial editing, when the design is documented, then it explicitly defines the primary actions available in the workspace, including reload, save review, approve review, add point, open externally, reveal source, and parcel navigation.
7. Given final spatial correction must still happen in ArcGIS Pro map tools, when the design is documented, then it clearly distinguishes review-workspace responsibilities from later map-review responsibilities.
8. Given the review workspace will eventually work with Parcel Fabric-backed outputs, when the design is documented, then it explains how the review workspace hands off approved parcel groups, points, and lines into Parcel Fabric-based spatial review without forcing Parcel Fabric editing inside the review surface itself.
9. Given the workspace must fit ArcGIS Pro constraints, when the design is documented, then it states whether the preferred container is a larger dock pane, floating pane, or dedicated window and why, including expected minimum workable dimensions.
10. Given accessibility and dense desktop use both matter, when the design is documented, then it defines keyboard flow, focus movement, scrolling behavior, truncation/tooltip behavior, and how the source viewer and results grid avoid clipping critical actions.
11. Given the user has already identified that the built-in COGO Reader has restrictions that do not fit this product, when the design story is complete, then it includes a concise comparison between:
   - built-in COGO Reader,
   - current extraction review pane,
   - and the proposed Jamaica COGO-style review workspace.
12. Given this story is complete, then it produces concrete UX outputs for implementation: workspace regions, primary actions, stage transitions, workspace states, and status/microcopy guidance.

## Tasks / Subtasks

- [ ] Define the purpose and boundary of the Jamaica review workspace. (AC: 1, 7-8, 11)
  - [ ] State what the workspace owns: source verification, extraction review, parcel grouping review, and review approval.
  - [ ] State what it does not own: final spatial editing, snapping, parcel-fabric editing, and completion/sync behaviors.
  - [ ] Compare the proposed workspace against the built-in COGO Reader and current extraction-review implementation.

- [ ] Design the workspace layout. (AC: 2-5, 9, 12)
  - [ ] Define left, center, right, and bottom/inset regions.
  - [ ] Define the preferred ArcGIS Pro host container: expanded dock pane, floating pane, or dedicated window.
  - [ ] Define expected behavior for narrow vs larger workspace sizes, including minimum workable dimensions.

- [ ] Define the source viewer UX. (AC: 2, 10, 12)
  - [ ] Specify behavior for PDF, TIFF, PNG, JPG, and text-based sources.
  - [ ] Specify page navigation, zoom/pan, source switching, source-type labeling, and scroll behavior.
  - [ ] Define fallback actions like `Open externally` and `Reveal in folder`.
  - [ ] Define when embedded rendering should yield to external opening.

- [ ] Define the extraction review table UX. (AC: 3-6, 10, 12)
  - [ ] Specify the columns shown in the primary table.
  - [ ] Specify parcel-group tabs, row grouping, and current parcel context.
  - [ ] Specify row editing states, unresolved markers, and manual point entry behavior.
  - [ ] Specify save/approve behavior and what becomes read-only after approval.

- [ ] Define the parcel interpretation preview UX. (AC: 4-5, 8, 12)
  - [ ] Specify the right-side parcel/line panel.
  - [ ] Specify the point/traverse preview behavior.
  - [ ] Define how the user understands parcel sequencing, closure, and suspicious transitions.

- [ ] Define workspace actions, states, and stage handoff messaging. (AC: 6-8, 10, 12)
  - [ ] Define the primary action set for this workspace.
  - [ ] Define key workspace states such as loading, ready, unresolved, approved, and handoff-ready.
  - [ ] Specify the transition from review workspace to map review.
  - [ ] Define user-facing copy for save, approve, unresolved blockers, and handoff to spatial review.
  - [ ] Define how the UI communicates that Parcel Fabric/map review happens next, not here.

- [ ] Produce design artifacts in repo language. (AC: 9-12)
  - [ ] Update or add UX planning artifacts that fit the existing `DESIGN.md` / `EXPERIENCE.md` structure.
  - [ ] Include one annotated layout reference or mockup direction for the implementation spike.
  - [ ] Record open UX risks and decisions that the dev spike must validate.

## Dev Notes

### Why This Story Exists

- The built-in ArcGIS Pro COGO Reader can be launched, but it does not appear to offer the transaction-controlled file loading and workflow control this product requires.
- The current extraction review workspace is serviceable but not designed as a full examiner review surface.
- The product now needs a Jamaica-specific review experience that fits document-driven cadastral examination and multi-parcel source handling.

### Design Direction

The recommended direction is a **custom review workspace inspired by COGO Reader**, not a clone of the built-in tool and not a dependency on unsupported Esri UI automation.

The design should assume:

- source files come from Innola transaction-controlled routing
- extraction results come from our own OCR/AI/document-type-aware pipeline
- parcel grouping must be explicit and reviewable
- final geometry correction still happens later in ArcGIS Pro map review

### Recommended Workspace Regions

Suggested first-pass workspace structure:

1. `Left`: source document viewer
2. `Center`: extracted rows / editable review table
3. `Right`: parcel interpretation / parcel-line summary
4. `Bottom or inset`: compact parcel/point preview

### Recommended Container Bias

The design should strongly consider a **large floating workspace** or **dedicated review window** rather than a narrow standard dock pane. The current ArcGIS Pro pane width is likely too constrained for a true three-region review experience.

### UX Bias To Preserve

- This is a **review workspace**, not a second map-editing environment.
- The source document and extracted results must feel tightly coupled.
- Parcel switching should be explicit and calm, not hidden inside row data.
- Dense data is acceptable, but clipped actions or unreadable viewer behavior are not.
- The user should always understand:
  - which source file is being reviewed,
  - which parcel is active,
  - whether edits are saved,
  - whether approval is still blocked,
  - and what happens next after approval.

### Related Existing Stories

- Story 2.12 and 2.16 already push document-type-aware extraction and review artifacts.
- Story 2.14A already improved extraction review around source verification.
- Story 5.6, 5.8, and 5.9 already establish Map Review, Parcel Fabric review output, and review-toolbar context.

This story should build on those decisions rather than replacing them.

### Suggested Outputs

At minimum this story should produce:

- a UX specification note
- annotated workspace layout
- primary interaction model
- workspace states and action model
- stage handoff guidance into `Map Review`
- explicit guidance on the preferred ArcGIS Pro host container

## References

- `_bmad-output/implementation-artifacts/2-12-execute-draft-extraction-and-review-artifact-generation.md`
- `_bmad-output/implementation-artifacts/2-14a-redesign-extraction-review-workspace-around-source-document-verification.md`
- `_bmad-output/implementation-artifacts/2-16-apply-document-type-catalog-v2-to-multi-source-extraction-pipelines.md`
- `_bmad-output/implementation-artifacts/5-6-add-spatial-review-stage-for-in-map-editing-and-manual-cogo.md`
- `_bmad-output/implementation-artifacts/5-8-implement-true-local-parcel-fabric-output-mode.md`
- `_bmad-output/implementation-artifacts/5-9-add-map-review-editing-toolbar-for-spatial-correction-workflows.md`
- `_bmad-output/implementation-artifacts/5-10-evaluate-supported-arcgis-pro-automation-boundary-for-cogo-reader-assist-vs-custom-transaction-controlled-extraction-flow.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md`
- `https://doc.esri.com/en/arcgis-pro/latest/help/data/parcel-editing/extractcogofromdeeds.html`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

TBD

### Completion Notes List

TBD

### File List

TBD

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-16 | 0.1 | Initial UX story for a Jamaica-specific COGO-style review workspace. | Codex |
| 2026-06-16 | 0.2 | Refined the UX story with clearer action/state requirements, accessibility and host-container constraints, and explicit design outputs for later implementation. | Codex |
