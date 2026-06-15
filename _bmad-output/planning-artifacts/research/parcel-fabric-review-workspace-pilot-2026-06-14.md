# Parcel Fabric Review Workspace Pilot Evaluation

Date: 2026-06-14
Scope: Story 5.7 - Evaluate Parcel Fabric Review Workspace Pilot For Cadastral Examiners

## Context

The Sidwell Jamaica ArcGIS Pro add-in already produces a transaction-local `.gdb` with:

- `parcel_points`
- `parcel_lines`
- `parcel_polygon`

That output supports the current `Normal` review path and can be loaded into the ArcGIS Pro map for spatial review.

The question for this pilot is whether cadastral examiners benefit enough from a parcel-oriented review workspace to justify a Parcel Fabric-inspired output mode before enterprise sync is implemented.

## Compared Options

### Option A: Normal

Current implemented mode.

Characteristics:

- local transaction `.gdb`
- plain feature classes
- lightweight and predictable
- easy to generate from approved review data
- works well for map display, labels, snapping, and standard editing

Strengths:

- simplest output contract
- lowest implementation and operational complexity
- easiest to troubleshoot in case folders
- aligns with the current local-first transaction workflow

Limitations:

- does not provide parcel-structured review context
- no parcel-oriented workspace grouping by default
- no topology-aware parcel model beyond what the examiner applies manually in ArcGIS Pro

### Option B: Parcel Fabric Pilot

Pilot mode implemented as an optional review workspace inside the same transaction `.gdb`.

Characteristics:

- preserves the standard output feature classes
- adds a `parcel_fabric_review` review dataset inside the transaction `.gdb`
- copies parcel points, lines, and polygons into that review dataset
- uses those review-layer paths as the preferred map-loading context

Strengths:

- gives examiners a more parcel-oriented review workspace
- keeps the output local to the transaction case
- preserves the existing `Normal` path as the safe fallback
- allows staged evaluation without changing enterprise sync assumptions

Limitations:

- this is a pilot review workspace, not a full authoritative enterprise Parcel Fabric
- native enterprise Parcel Fabric lifecycle, lineage, and authoritative parcel management remain out of scope
- ArcGIS Pro parcel editing benefits depend on project configuration, licensing, and how examiners use Pro tooling in practice

## Examiner Task Assessment

### Visual review

Recommendation:

- both modes support review
- Parcel Fabric pilot improves organization of review content, but not enough on its own to replace `Normal`

### Point and line correction

Recommendation:

- both modes can support standard ArcGIS Pro editing
- `Normal` remains simpler
- Parcel Fabric pilot becomes more valuable when the examiner consistently works parcel-by-parcel and wants a more review-oriented workspace structure

### Manual COGO entry for weak source documents

Recommendation:

- ArcGIS Pro tools remain the primary editing surface
- the pilot is useful if it encourages a parcel-centric review pattern
- this does not yet replace a future deeper parcel workflow or authoritative Parcel Fabric implementation

### Topology help

Recommendation:

- the pilot is promising as a structured review workspace
- the current implementation should still be treated as optional until the examiner team confirms a measurable workflow benefit

## Enterprise Sync Position

This pilot does not change the current enterprise architecture decision:

- local case folder and transaction `.gdb` remain the unit of work
- approved local geometry still needs a later sync story for enterprise transfer
- enterprise Parcel Fabric or authoritative cadastral publishing is still a downstream concern

## Recommendation

Recommended outcome:

- keep `Normal` as the default review workspace
- adopt `Parcel Fabric` as an optional pilot review mode

Reasoning:

- it preserves the stable local output contract
- it gives examiners a controlled way to try a parcel-oriented review workspace
- it avoids prematurely coupling the add-in to authoritative enterprise Parcel Fabric behavior

## Next Story Implications

1. Validate the examiner experience of the `Parcel Fabric` pilot against real plan-examination cases.
2. Add explicit map-review guidance and tool hints when Parcel Fabric pilot mode is selected.
3. Evaluate whether the compare-stage workflow should also support `Normal` and `Parcel Fabric` review modes.
4. Define the enterprise sync contract for outputs generated from the pilot review workspace.

## Final Decision For Story 5.7

Decision:

- keep regular `.gdb` as the default review workspace
- adopt Parcel Fabric as an optional review mode for evaluation

This pilot should remain opt-in until examiner feedback proves that the added parcel-oriented review context materially improves productivity and review quality.
