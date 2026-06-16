---
baseline_commit: handoff-2026-06-15
---

# Story 7.4: Promote Working Review Geometry To Sync-Ready Authoritative Package

Status: drafted

## Story

As a cadastral examiner,  
I want the reviewed working geometry prepared for downstream authoritative sync,  
so that approved transaction outputs can move from collaborative review state into a controlled promotion path.

## Acceptance Criteria

1. Given a transaction has passed review, validation, and required readiness checks, when the user prepares the case for final downstream promotion, then the add-in records a sync-ready package or payload reference based on the latest approved working geometry.
2. Given the workflow reaches promotion readiness, when status is shown to the user, then the add-in clearly distinguishes working-review completion from authoritative promotion completion.
3. Given the promotion package is created, when audit metadata is written, then it records the source working-layer features, operator, timestamp, and readiness outcome.
4. Given downstream promotion cannot yet complete, when the workflow is paused or blocked, then the reviewed working geometry remains intact and recoverable.
5. Given future authoritative targets may vary, when this story is implemented, then the promotion path remains compatible with later Enterprise sync or authoritative Parcel Fabric targets.

## Tasks / Subtasks

- [ ] Define the sync-ready promotion contract. (AC: 1-3, 5)
  - [ ] Specify the payload/package contents derived from the latest approved working geometry.
  - [ ] Record promotion readiness separately from final authoritative completion.

- [ ] Implement readiness packaging and status transitions. (AC: 1-4)
  - [ ] Build the sync-ready reference/package from current working-state geometry.
  - [ ] Preserve working geometry when promotion is deferred or blocked.

- [ ] Extend audit and user messaging. (AC: 2-4)
  - [ ] Add explicit UI language for “ready for downstream promotion” versus “completed downstream sync”.
  - [ ] Record promotion-source metadata and outcomes in audit artifacts.

## Dev Notes

### Architectural Direction

- Promotion is a controlled boundary, not an implicit side effect of review completion.
- This story should not force a specific downstream authoritative implementation yet.

### References

- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/implementation-artifacts/4-4-generate-transaction-output-gdb-from-approved-review-data.md`
- `_bmad-output/implementation-artifacts/5-8-implement-true-local-parcel-fabric-output-mode.md`
