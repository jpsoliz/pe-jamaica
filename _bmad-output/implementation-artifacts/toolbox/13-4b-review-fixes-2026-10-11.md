# 13.4B Review Fixes - 2026-10-11

Status: all eight review findings corrected and regression-tested; JotaPe accepted 13.4B's isolated in-memory scope on 2026-10-11. Parent 13.4 is done for M2. No production deployment, operational 11.2 closure, FGDB persistence or remote action is implied.

Branch/worktree: `refactor/toolbox-case-workspace`, `temp/toolbox-worktree`.
Correction baseline: `0a6188f467da81ccdbee639cd0c8954b1e727e85`.
Authorization: JotaPe requested fixing all eight findings and adding regression tests.

## Corrections And Coverage

| Finding | Result | Regression coverage |
|---|---|---|
| R1: boundary evidence | Source hashes feed traces; every physical boundary consumes its supporting traces; missing coverage blocks application/readiness | Secondary trace-only and hash-only changes invalidate both shared parcels; case-only evidence edges are insufficient; unrelated geometry stays current |
| R2: archived manual applicability | Same-case archived correction IDs are valid audit targets; later retirement requires renewed mappings; original records remain append-only | Split -> key regeneration -> repair restores readiness; subsequent merge validates retained archived mappings and rejects omissions; foreign archives reject |
| R3: evidence links | Scoped nongeometry hashes include relationships and actual linked trace/source inputs; dependency check inputs agree with readiness; every linked parcel decision consumes the evidence | Add/remove/reassign links; unchanged link IDs with changed title trace/hash; exact affected case-check inventory; unknown consumers block; multiple logical decisions give order-independent coverage |
| R4: case-wide applicability | Latest validated applicability is evaluated before expanding original empty scope | NotApplicable restores readiness; narrower Applicable excludes unrelated parcel currency; Unresolved remains blocked; original scope/history preserved |
| R5: dependency metadata | Version and node/binding descriptors participate in impact consistently with consumer hashes | Version-only and binding-only changes appear in affected checks/artifacts; geometry-role mismatches are rejected |
| R6: inclusion impact | Bound decision and case inputs consume inclusion/audit state; logical decisions need explicit parcel bindings | Exclusion/re-inclusion invalidates decisions/aggregates/artifacts; unrelated geometry stays unchanged; unbound logical decisions reject |
| R7: accepted audit | Returned snapshots append immutable accepted dispositions with operation/digest, parent/result revisions and UTC time | Replacement actor/reason/evidence survives without proposal; prior values survive in archives; replay does not duplicate/change audit; continued acceptance appends |
| R8: graph validation | Iterative topological validation visits shared descendants once and preserves cycle detection | 32 two-node layers and a 4,096-node chain complete; cycles in both graphs reject without wall-clock test thresholds |

Ten new executable-harness groups reside in:

- [Dependency regressions](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ToolboxReconciliationDependencyRegressionTests.cs).
- [History regressions](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ToolboxReconciliationHistoryRegressionTests.cs).

Existing test assertions remain intact. The existing reconciliation fixture class is partial so both new files reuse its private builders; its graph builder now explicitly declares supporting evidence edges and geometry-check roles. No new test framework or package was introduced.

## Contract Notes For Subsequent Work

- `ToolboxDependencyCheckBinding.GeometryCheck` declares the check's input class; a supplied review must agree with it. Every check node needs a known scope binding.
- `ToolboxDependencyNode.DecisionParcelId` binds logical decision IDs to an actual parcel. Existing parcel-ID reference conventions remain supported. Unbound decisions reject, and all decisions bound to a linked parcel require evidence coverage.
- `toolbox_dependencies_v2` binds node descriptors and consumed canonical inputs. `toolbox_reconciliation_base_v2` binds accepted audit history. B-only canonical/parcel-decision dependency hashes have explicit version labels.
- A's canonical v2 baseline and geometry-only canonical hashes remain unchanged. Scopes without relevant links retain the baseline canonical hash. Operational review writer/hash/approval code remains untouched.
- Accepted dispositions are immutable in-memory history, not durable restart storage. 13.5 must preserve them, archived applicability and declared bindings during its separately approved persistence work.

## Verification

Final commands from the isolated worktree:

```powershell
dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln /p:BaseIntermediateOutputPath=obj-toolbox-review-fixes/ /p:UseSharedCompilation=false /m:1
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll "toolbox" "workspace planner" "settings workspace" "workflow execution settings" "review persistence" "review approval"
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll
```

- Final build succeeded with zero warnings and zero errors.
- Final focused run passed **135 tests**: the prior 125 plus ten new review-fix groups. All eight findings have regression coverage.
- Final full run passed **207 tests before fail-fast**, then stopped at the existing `CompareWorkspaceXamlTests.CompareWorkspaceExposesTaskLifecycleCommands:83` assertion about the indeterminate Finalize footer progress indicator. Later tests did not execute; full-suite green is not claimed.
- The initial new-regression run exposed a fixture that refreshed human evidence while trying to isolate a finding. The test now retains the original register so only finding applicability differs.
- Broader verification exposed a real A/B empty-link hash regression; it was corrected by retaining the baseline for scopes without links. The unchanged A fingerprint regression now passes.
- A targeted linked-source regression failed with `Expected 'Blocked', got 'Ready'` before the corresponding source-bound hash fix, then passed. Original R1-R6 failures had already been reproduced in the review probes. The large DAG was not run against the known exponential implementation; no false red-run claim is made for it.
- One intermediate build hit file-copy locks from a failed harness process. That test session was stopped, and subsequent builds passed; no project/toolchain workaround was introduced.

## Independent Re-review

Blind Hunter, Edge Case Hunter and Acceptance Auditor examined the patch without conversation context. Follow-up findings were corrected and regression-tested: linked source changes behind stable link IDs, omitted affected-check IDs, unbound logical decisions, geometry-role mismatches and order-dependent multiple-decision coverage. Final narrow re-reviews reported no remaining findings in those areas; prior clean history/audit review remains applicable.

This is code-review closure for the requested corrections, not product/GIS acceptance or certification of deferred live integration. Existing full-suite limitations remain in [deferred work](deferred-work.md).

## Changed Files

Production model/services: `ToolboxBoundaryDependencies.cs`, `ToolboxExamination.cs`, `ToolboxParcelLineage.cs`, `ToolboxReconciliation.cs` under `Workflow/Toolbox/Model/`.

Tests: the two new regression files, `ToolboxReconciliationTests.cs` fixture/class declaration, and ten registrations in `Program.cs`. Operational source, canonical extraction owner, UI/DAML, settings, provider/solver code and external services were not modified.

## Suggested Review Order

1. [Dependency coverage and bindings](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxBoundaryDependencies.cs:72): enforce complete consumption and bounded graph validation.
2. [Consumed canonical inputs](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxExamination.cs:149): bind textual relationships and their actual source content.
3. [Historical applicability](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxParcelLineage.cs:24): preserve scope/history while applying audited mappings.
4. [Accepted audit snapshots](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxReconciliation.cs:24): retain decisions across replay and continued examination.
5. [Regression scenarios](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ToolboxReconciliationDependencyRegressionTests.cs:10): check the failure paths and isolation guarantees.

Next: prepare 13.5 persistence following the explicit acceptance/preparation request. No commit, merge or deployment was performed by the correction or acceptance tasks.
