---
baseline_commit: handoff-2026-07-14
---

# Story 8.6: Wire Commit Stage Readiness To Compare Approval And Authoritative Promotion

Status: ready-for-dev

## Story

As a cadastral examiner or authorized publisher,  
I want the Commit stage to become available only after Compare approval and then promote approved working geometry into the final authoritative layer,  
so that Enterprise authoritative data is updated only after both geometry and ownership evidence checks are complete.

## Business Context

The new three-stage workflow ends with Commit. Compute validates geometry and creates/publishes working review data. Compare approves ownership and neighbor-rights evidence. Commit is the controlled final publish/promotion action.

Existing Epic 7 work includes Enterprise working-layer publishing and a story for promoting working-review geometry to a sync-ready authoritative package. This story connects that promotion to the new Compare approval gate and the transaction stage model.

## Acceptance Criteria

1. Given a transaction is in Commit stage, when it is loaded, then the shell recognizes Commit as a valid stage distinct from Compute and Compare.
2. Given Compare approval is missing, blocked, returned, stale, or mismatched, when Commit is launched, then Commit is blocked with a clear readiness message.
3. Given Compare is approved and current, when Commit is launched, then the add-in can load the transaction-scoped working geometry and related evidence refs for final promotion.
4. Given final authoritative layer settings are configured, when Commit runs, then the add-in promotes or packages the approved working geometry according to the configured authoritative promotion path.
5. Given final authoritative layer settings are not configured, when Commit runs, then the add-in blocks with non-secret configuration diagnostics and does not mark the transaction complete.
6. Given promotion succeeds, when commit evidence is written, then the artifact records transaction id/number, Compare decision ref, working layer refs, promoted feature counts, target layer/package refs, reviewer/publisher, timestamp, and non-secret diagnostics.
7. Given promotion fails, when errors occur, then partial failure is recorded, secrets are redacted, and the transaction remains uncompleted.
8. Given Commit succeeds, when Innola lifecycle completion is called, then completion happens only after commit evidence is successful and current.
9. Given Commit succeeds, when the transaction list refreshes, then the locally completed transaction is removed or marked according to existing completion behavior.
10. Given automated tests run, then Commit stage gating, Compare readiness checks, missing target config, success evidence, failure evidence, and lifecycle completion gating are covered.

## Tasks / Subtasks

- [ ] Add Commit stage config/routing. (AC: 1-2)
  - [ ] Add `commit_workflow_stages` to settings.
  - [ ] Extend the stage router from Story 8.1 to return `Commit`.
  - [ ] Add a Commit launch seam.

- [ ] Integrate Compare readiness. (AC: 2-3)
  - [ ] Use the Compare readiness service from Story 8.5.
  - [ ] Block Commit when Compare decision is not approved/current.
  - [ ] Surface a clear readiness diagnostic in the UI.

- [ ] Connect authoritative promotion. (AC: 4-7)
  - [ ] Reuse or complete Story 7.4 promotion package/service where applicable.
  - [ ] Load transaction-scoped working geometry from Enterprise working layers.
  - [ ] Promote/package only the selected transaction scope.
  - [ ] Write commit evidence artifact.

- [ ] Gate Innola completion. (AC: 8-9)
  - [ ] Extend completion readiness so Commit-stage transactions require successful commit evidence.
  - [ ] Preserve Compute-stage completion behavior while transitioning to the new three-stage model.
  - [ ] Ensure completion happens after authoritative promotion, not before.

- [ ] Add tests. (AC: 1-10)
  - [ ] Settings parser tests for Commit stages.
  - [ ] Commit route tests.
  - [ ] Missing Compare approval blocks Commit.
  - [ ] Approved Compare unlocks Commit.
  - [ ] Missing target config blocks promotion.
  - [ ] Promotion success/failure artifact tests.
  - [ ] Lifecycle completion gating tests.

## Developer Notes

Relevant existing stories:

- `_bmad-output/implementation-artifacts/7-4-promote-working-review-geometry-to-sync-ready-authoritative-package.md`
- `_bmad-output/implementation-artifacts/7-2-publish-approved-review-geometry-to-enterprise-working-layers.md`
- `_bmad-output/implementation-artifacts/7-9-record-compute-final-review-disposition-and-closeout-enterprise-working-layer.md`
- `_bmad-output/implementation-artifacts/8-5-persist-compare-decision-and-unlock-commit-stage.md`

Relevant existing files:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/DefaultTransactionCompletionReadinessService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingDispositionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs`

Do not let Commit become a second Compare review. Commit should be operational: readiness check, promotion/package, evidence write, lifecycle completion.

## UX References

- EXPERIENCE: `Commit / Sync Readiness`
- Compare approval state in `mockups/compare-workspace-evidence-reconciliation.html`

## Testing Notes

Run:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj
```

## Open Questions

- Confirm the final authoritative target layer/package contract before implementation starts.
- Confirm whether Commit is an examiner action, supervisor action, or automated transition after Compare approval.
