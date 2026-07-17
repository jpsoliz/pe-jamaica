---
baseline_commit: handoff-2026-07-14
---

# Story 8.5: Persist Compare Decision And Unlock Commit Stage

Status: review

## Story

As a cadastral examiner,  
I want Compare finalization or blockage decisions to be recorded as transaction evidence,  
so that Commit is only available after ownership and neighbor-rights evidence has been reviewed and approved.

## Business Context

Compute currently has closeout/disposition artifacts and completion readiness checks. Compare needs a similar but separate decision contract. It must record what evidence was reviewed and which discrepancies were accepted, blocked, or sent back.

This story does not perform final authoritative layer promotion. It creates the Compare decision artifact and uses it to gate the next stage.

## Acceptance Criteria

1. Given Compare evidence has loaded, when the user selects Save Progress, then the add-in writes a draft Compare artifact with notes, evidence summaries, discrepancies, reviewer, and timestamp.
2. Given the user has no retained valuable evidence or no Decision Notes, when they view Finalize, then finalization is disabled.
3. Given the required Compare evidence passes, when the user selects Finalize, then the add-in writes a `compare_review_decision.json` artifact with decision `approved`.
4. Given the examiner blocks Compare, when they select Block Compare, then the add-in writes a decision artifact with decision `blocked`, blockers, notes, reviewer, and timestamp.
5. Given the examiner has retained at least one valuable evidence row and completed Decision Notes, when documents and geometry are ready, then Finalize is enabled.
6. Given a Compare decision is recorded, when the transaction is reopened, then the workspace can display the prior decision and evidence refs.
7. Given Compare is approved, when stage readiness is evaluated, then Commit becomes available only after the approved decision artifact is current for the transaction.
8. Given the decision artifact references evidence, when files are moved/restored from a resume package, then relative Case Folder refs continue to resolve where possible.
9. Given the transaction number/id in the artifact does not match the selected transaction, when readiness is evaluated, then Commit remains blocked.
10. Given automated tests run, then draft persistence, Finalize/block decisions, stale/mismatched artifact detection, and Commit readiness gating are covered.

## Tasks / Subtasks

- [x] Add Compare decision contract. (AC: 1-10)
  - [x] Define `CompareReviewDecisionDocument`.
  - [x] Include schema version, transaction id, transaction number, task id, reviewer, timestamps, decision, notes, evidence refs, discrepancy summary, and readiness status.
  - [x] Use relative Case Folder refs for local evidence artifacts.

- [x] Add persistence service. (AC: 1, 3-6, 8-10)
  - [x] Add `CompareReviewDecisionPersistenceService`.
  - [x] Add save/load for draft and final decision.
  - [x] Validate transaction identity on load.
  - [x] Keep JSON machine-readable and redacted.

- [x] Add Compare readiness service. (AC: 2, 5, 7, 9-10)
  - [x] Add a readiness checker for Commit availability.
  - [x] Require approved Compare decision for Commit.
  - [x] Block when decision is missing, blocked, stale, mismatched, or has missing evidence refs.

- [x] Wire workspace actions. (AC: 1-7)
  - [x] Save Progress writes draft.
  - [x] Finalize writes final approved decision.
  - [x] Block Compare writes final blocked decision.
  - [x] Finalize is gated by at least one retained valuable evidence row and non-empty Decision Notes.
  - [x] Return to Compute is removed from the Compare workspace action surface.
  - [x] Show prior decision state on reopen.

- [x] Add tests. (AC: 1-10)
  - [x] Draft save/load.
  - [x] Approved decision save/load.
  - [x] Blocked/returned decisions.
  - [x] Mismatched transaction blocks readiness.
  - [x] Missing/stale decision blocks Commit.

## Developer Notes

Relevant existing analogs:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Disposition/ComputeReviewDisposition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Disposition/ComputeReviewDispositionPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/DefaultTransactionCompletionReadinessService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`

Compare decision should be separate from Compute disposition. Do not overload `compute_review_disposition.json`.

Recommended artifact names:

- `working/compare_review_draft.json`
- `working/compare_review_decision.json`

Candidate decision values:

- `approved`
- `blocked`
- `returned_to_compute`
- `saved_progress`

## UX References

- Decision panel in `mockups/compare-workspace-evidence-reconciliation.html`
- EXPERIENCE state: `Compare approved`

## Testing Notes

Run:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj
```

## Dev Agent Record

### Implementation Plan

1. Add a Compare-specific decision document and persistence service separate from Compute disposition.
2. Extend Compare draft persistence with transaction/reviewer metadata while preserving existing draft behavior.
3. Wire Save Progress, Finalize, and Block Compare into draft/decision artifact writes.
4. Add a Commit readiness checker that requires an approved, current, matching Compare decision.
5. Add regression tests covering draft persistence, decision persistence, and readiness blocking.

### Debug Log

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 errors and one pre-existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "compare"` passed 31 tests.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build` passed 389 tests.
- Post-review patch: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 errors and one pre-existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- Post-review patch: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "compare"` passed 34 tests.
- Post-review patch: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build` passed 392 tests.

### Completion Notes

- Added `compare_review_decision.json` contract and persistence under the Case Folder `working` directory.
- Save Progress now records transaction/reviewer metadata in `compare_review_draft.json`.
- Finalize writes an approved commit-ready decision; Block writes a non-approving decision.
- Compare decisions include relative evidence refs, notes, reviewer identity, discrepancy summaries, transaction id/number, task id, and readiness status.
- Added `CompareCommitReadinessService` to block Commit when the decision is missing, mismatched, stale, not approved, not commit-ready, has unresolved blockers, or references missing evidence.
- Patched review findings by restoring prior decision evidence refs/discrepancy summaries and requiring at least one retained valuable evidence row plus non-empty Decision Notes before Finalize.

### File List

- `_bmad-output/implementation-artifacts/8-5-persist-compare-decision-and-unlock-commit-stage.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareReviewDecision.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareReviewDraftPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareReviewDecisionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-07-15 | 1.0 | Implemented Compare decision artifact persistence, workspace decision actions, Commit readiness gate, and regression coverage. | Codex |
| 2026-07-15 | 1.1 | Patched review findings for command refresh, prior decision restore, and evidence-review approval gating. | Codex |
| 2026-07-17 | 1.2 | Renamed approval action to Finalize, removed Return to Compute from Compare, and aligned Finalize gating to valuable evidence plus Decision Notes. | Codex |
