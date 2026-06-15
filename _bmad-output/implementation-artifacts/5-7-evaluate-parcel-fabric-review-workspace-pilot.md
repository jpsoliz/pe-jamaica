---
baseline_commit: handoff-2026-06-14
---

# Story 5.7: Evaluate Parcel Fabric Review Workspace Pilot

Status: ready-for-dev

## Story

As an architecture and product team for NLA Jamaica cadastral examination workflows,  
I want to evaluate a Parcel Fabric-backed review workspace as a pilot option,  
so that we can determine whether parcel-aware topology and editing tools materially improve examiner productivity over the regular transaction `.gdb` review workspace without prematurely forcing Parcel Fabric as the default model.

## Acceptance Criteria

1. Given the current workflow already produces a transaction-local `.gdb`, when the pilot story is executed, then the team documents a side-by-side comparison between regular feature-class review and a Parcel Fabric-backed review workspace for plan examination use.
2. Given cadastral examiners may need topology-aware editing and manual COGO correction, when the pilot is assessed, then the evaluation explicitly considers snapping, COGO entry, visibility of parcel structure, topology help, and usability for poor-quality source plans.
3. Given lineage/history is out of scope for the current examination-stage app, when the pilot recommendation is made, then the evaluation distinguishes between Parcel Fabric as an editing accelerator versus Parcel Fabric as the system-of-record model.
4. Given this app is part of a larger cadastral ecosystem, when the pilot is evaluated, then the outcome explains how approved local geometry would later sync into enterprise regardless of whether the review workspace is plain `.gdb` or Parcel Fabric-backed.
5. Given a pilot implementation is feasible, when the story is developed, then it produces either:
   - a small prototype/spike showing how transaction outputs could be loaded into a Parcel Fabric review context, or
   - a documented reason why a paper architecture evaluation is the safer first step.
6. Given the pilot is complete, when the recommendation is delivered, then it clearly states one of:
   - keep regular `.gdb` as the default review workspace,
   - adopt Parcel Fabric as an optional review mode,
   - or promote Parcel Fabric to the default review workspace for examination transactions.
7. Given this story is complete, then the result includes concrete next-step implications for stories, configuration, licensing/permissions assumptions, and sync mapping expectations.

## Tasks / Subtasks

- [ ] Capture the evaluation criteria. (AC: 1-4, 6-7)
  - [ ] Define the examiner tasks to compare: visual review, point/line correction, manual COGO entry, topology checking, parcel completion, and handoff to sync.
  - [ ] Define comparison dimensions: implementation effort, examiner productivity, topology support, ArcGIS Pro tool leverage, operational complexity, and sync impact.

- [ ] Assess current regular `.gdb` review path. (AC: 1-4)
  - [ ] Document strengths of plain transaction feature classes for the current workflow.
  - [ ] Document limitations for cadastral review, especially around topology and parcel-oriented editing assistance.

- [ ] Assess Parcel Fabric review workspace feasibility. (AC: 1-5)
  - [ ] Review ArcGIS Pro Parcel Fabric requirements relevant to a local or pilot review workspace.
  - [ ] Identify required setup decisions: parcel types, records, topology/build behavior, local vs enterprise assumptions, and editing APIs/tooling.
  - [ ] If practical, implement a constrained spike/prototype path; otherwise document why a pure evaluation is preferred now.

- [ ] Produce a recommendation and decision note. (AC: 3-7)
  - [ ] State whether Parcel Fabric should remain future-facing, become optional, or become default for review.
  - [ ] State impact on the existing output contract and enterprise sync path.
  - [ ] State the next implementation stories implied by the recommendation.

## Dev Notes

### Why This Story Exists

- The workflow now reaches a point where human spatial review and manual COGO correction may be required after extraction and validation.
- Parcel Fabric may provide a materially better examiner editing experience, but it also brings heavier ArcGIS-specific structure and operational complexity.
- The team needs an informed decision, not a guess.

### Architectural Position

Current recommendation entering this story:

- keep the app’s stable output contract in a regular transaction `.gdb`
- evaluate Parcel Fabric as a review-workspace enhancement or pilot
- do not make Parcel Fabric the default system model until examiner benefit and sync impact are clearer

This story exists to validate or overturn that recommendation with evidence.

### Scope Boundaries

- This story may be a spike/evaluation rather than a production feature-complete implementation.
- It should not silently refactor the whole workflow into Parcel Fabric without an explicit recommendation outcome.
- It should stay anchored to NLA Jamaica examination use cases rather than generic cadastral theory.

### References

- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/research/technical-arcgis-pro-addin-parcel-workflow-research-2026-06-08.md`
- `_bmad-output/implementation-artifacts/4-4-generate-transaction-output-gdb-from-approved-review-data.md`
- `_bmad-output/implementation-artifacts/5-6-add-spatial-review-stage-for-in-map-editing-and-manual-cogo.md`
- `https://pro.arcgis.com/en/pro-app/3.6/sdk/api-reference/conceptdocs/docs/ProConcepts-Parcel-Fabric.html`

