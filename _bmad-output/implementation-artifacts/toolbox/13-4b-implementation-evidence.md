# 13.4B - Implementation Evidence

Latest 2026-10-11 correction result: all eight independent-review findings and follow-up issues fixed, ten regression groups added; clean build and 135 focused passes. Full run passed 207 tests before the existing Compare XAML assertion. Story is back in review, ready for scoped acceptance; [correction evidence](13-4b-review-fixes-2026-10-11.md) supersedes the unresolved-review status below.

2026-10-11 independent review outcome: changes requested, eight patch findings; story returned to in-progress. The historical implementation/test evidence below remains valid for those runs but is not acceptance. See [review report](13-4b-code-review-2026-10-11.md) for reproduced boundary/evidence invalidation, historical applicability, audit and impact gaps. No production fixes were applied by the review.

Date: 2026-10-09. Status: review, not product acceptance or production certification.

## Isolation And Ownership

Worktree: `temp/toolbox-worktree`, branch `refactor/toolbox-case-workspace`; operational `wipRC9` remains at baseline `5908f30617cdc49e09873635a30343da8aff9ad6`, with empty tracked diff. Existing uncommitted work preserved. Only isolated Toolbox models/services, new tests, registrations and Toolbox records changed.

D07 P01-P05 (`toolbox_d07_reconciliation_v1`), A scoped signoff and explicit B implementation authorization are entry evidence, not reissued approvals. Canonical JSON owner, policy loader/resolver, original bindings and A test bodies are reused unchanged. No UI/DAML/settings publication, extraction/solver replacement, FGDB, server action or deployment. SDK build artifacts remain local.

## Delivered Contract

- `Start` pins the initial case/revision/source hashes/dependency inventory and cannot reset an existing B model. Unknown adjacency evidence blocks readiness.
- `Inspect` validates the envelope and exhaustively returns carried, new, missing, ambiguous and value-conflicting candidates. Failed/partial/canceled/incomplete metadata cannot be hidden under batch success.
- `Seal` binds raw/resolved owned canonical snapshots, choices, lineage, applicability, boundaries and dependencies. New references use `@parcel:<key>`, `@row:<key>` or `@segment:<key>`; acceptance resolves them after fresh ID allocation. Check bindings reuse the policy resolver.
- `Accept` requires authorized local Compute and current revision/base fingerprint, digest, source identity and policy. Every old/incoming/output entity is dispositioned; values and memberships match explicit retain/replace choices. Global tabular changes have a separate audit choice. Empty/missing suggestions never erase manual work; failure returns no replacement snapshot.
- Physical boundaries retain independent source segments and declared point inputs, directed memberships, evidence/semantics and typed conflicts. Unknown consistency/semantics/points or unsupported cardinality blocks readiness. Removal and adjacency/no-shared-edge knowledge require explicit evidence. No detection, snapping or repair.
- Directed dependency hashes consume actual values. Deterministic old/new union impact includes excluded/retired former consumers and declared artifact IDs, with paths/reasons; no output executes. Unknown coverage is not zero impact.
- Split/merge results use fresh parcel IDs and explicit inclusion, retaining parent snapshots. Findings, human reviews and known manual/original-valued entities need auditable applicability links. Original finding scopes/classifications/history are never reassigned; unresolved applicability blocks readiness.
- Human source validation and finding dispositions bind actual inputs independently of caller revision strings. Shared point/segment/boundary edits stale every declared consumer. Unrelated parcels and geometry-only checks retain currency after unrelated tabular edits. Renewed source-defect disposition can forward normally without valid-geometry approval.
- Same operation/digest replay is a no-op in immutable in-memory history; changed payload or concurrent stale base rejects. Durable restart/retry is deferred to 13.5.

Adapters still supply trusted local authority, source hashes, verified geometry semantics and complete declared dependency/adjacency evidence. Fixtures do not certify real CRS/tolerances/curves/Z/M, automatic adjacency, host edits or production authorization.

## Acceptance Trace

| Criteria | Regression evidence |
|---|---|
| AC1,16 | Protected operational diff, narrow isolated changes, preserved A/canonical/policy/bindings; build/focused/full results below |
| AC2 | Envelope/base/case/status/hash failures and contradictory-success guard; base fingerprint includes canonical/history/identity/inclusion/register/operations |
| AC3-5 | Variable counts/rename/replay, manual conflicts/empty extraction, regenerated keys and explicit replacement, exhaustive inventory/digest/authority guards, unresolved new candidates |
| AC6-7 | Shared readiness, structural identity rejection, typed consistency/semantics/cardinality conflicts, adjacency/removal evidence |
| AC8-9 | Shared source-point change with unchanged boundary token; actual geometry consumers; old/new boundary reassignment with excluded A; tabular/global facts preserve geometry |
| AC10-11 | 1:2 and 1:5 splits, 3:1 merge; fresh IDs/archives; invalid ancestry/inclusion/applicability; original finding scope/history append-only guards |
| AC12 | Boundary resolution staleness; fresh checks cannot renew an old disposition; explicit renewed source-defect forwarding; fresh human validation after changed source; unchanged A cumulative/pending-LTD/Compare-tabular tests |
| AC13-14 | Local Compute/permission/authority/digest guards, unchanged read-only context tests, replay allocation assertions and stale concurrent rejection |
| AC15 | New unresolved parcel cannot be hidden by selection, shared/history conflicts block, unchanged A all-included/exclusion gates |
| A compatibility | B-only metadata excluded from A v2 aggregate/parcel hashes; unchanged default same-key `ReAdapt` rejection |

Cad Map cases: 1, 2, 3, 5, 10, 50 and 100 parcels, examples not a product cap/performance certification. Manual/raw/source/human histories retained; no inferred owner copying to every child.

## Verification

Executed from isolated worktree, outside sandbox for SDK/harness access:

```powershell
dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln /p:BaseIntermediateOutputPath=obj-toolbox-13-4b/ /p:UseSharedCompilation=false /m:1
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll "toolbox" "workspace planner" "settings workspace" "workflow execution settings" "review persistence" "review approval"
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll
```

- Final build: zero warnings/errors.
- Final focused: **125 passed**, preserved 105 plus 20 registered B groups.
- Final full retry: **146 passed before fail-fast**, then `UnauthorizedAccessException` at `ManifestSerializer.Write:28`, called from `CompareWorkspaceViewModelTests.SaveAndRestoreDraftPreservesNotesAndEvidence:94` through helpers 1508/1478. Later tests did not execute. Earlier retry failed at the same access point from `CaseFolderStoreTests.ReopenCaseFolderReportsUnknownWorkflowState:216`.
- Full suite did **not** pass. Existing intermittent Manifest and Compare XAML progress-indicator limitations remain in [deferred work](deferred-work.md) and [A evidence](13-4a-implementation-evidence.md). No legacy repair is claimed.
- Red evidence: missing explicit API and contradictory-success/failed-candidate regressions failed before their implementation. No claim every group individually ran red. Intermediate serialization/coverage issues were corrected; failed sandbox processes were closed before rebuilding.
- Local self-review only. Independent peer acceptance remains next; no formal multi-agent/GIS certification.

## Remaining Gates

B review/acceptance precedes 13.5; parent 13.4 remains in-progress. Durable revisions/history/restart, FGDB round trips, real GIS policy/provider normalization, host edits, production permissions, complete runtime bindings and Compare/handoff integration remain separate work. Original operational 11.2 is not retired.
