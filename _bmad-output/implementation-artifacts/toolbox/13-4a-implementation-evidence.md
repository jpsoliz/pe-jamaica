# 13.4A - Isolated Implementation Evidence

Date: 2026-10-08
Status: accepted/done for 13.4A's isolated in-memory contract, 2026-10-08; no operational story closure or production activation.

Initial final-signoff attempt, 2026-10-08: withheld after three additional gaps were found despite the clean `obj-toolbox-signoff/` build and 101 focused passes. JotaPe then authorized all three fixes and regression tests in isolation. The three regression groups and additional evidence-alias group failed before their corresponding fixes and now pass; final build and 105 focused tests pass. Final patch review found no remaining blocker, so the requested scoped signoff is complete. D07 P01-P05 are approved separately as `toolbox_d07_reconciliation_v1`; B implementation remains unstarted.

## Delivery Order

1. Prepared [11.2 contract amendment and ownership map](11-2-toolbox-contract-amendment.md).
2. Implemented opt-in canonical projection; clean build and four canonical test groups passed before 13.4A implementation.
3. Implemented immutable parcel model and examination evaluator using the existing policy resolver and binding bundles.

## Delivered Behavior

- Stable case-scoped parcel/row/segment ID map; same-contract rename/reorder retains IDs. Missing/incomplete/foreign maps and reused IDs fail without a partial model.
- Explicit ordered memberships retain closing references and empty-geometry candidates. Variable Cad Map counts through 100 are exercised; 100 is not a product limit or performance certification.
- Selection is separate from inclusion and readiness. Auditable exclusions/reinclusions retain data and history; re-inclusion does not revive old acceptance.
- Canonical global facts, many-parcel participant/evidence links and linked-source environment/TR/revision identity remain owned, immutable and read-only.
- Resolver outputs drive required/optional/N/A obligations. Unknown applicability, absent inventories, unmapped bindings, missing/stale checks and invalid references block readiness.
- Reviews bind resolver, evidence/human validation, scoped canonical tabular inputs and declared geometry dependencies. Canonical owner/title/metadata changes stale old non-geometry checks even if caller scope tokens are reused. Geometry-only check inputs and unrelated participant inputs remain unchanged. Case/document checks conservatively consume the known global tabular collections; fine-grained provider bindings remain integration work.
- Per-parcel dispositions and case snapshots carry case, stage, policy, actor/time, semantic fingerprints, counts, reasons and immutable audit history. Both bind geometry-policy verification; revocation makes prior accepted parcel dispositions stale as well as blocking the case.
- Source defects can be recorded by an explicitly authorized local examiner for the normal next stage. Uncorrected extraction mismatch, mixed extraction errors and unknown origin cannot bypass correction. Forwarding is separate from valid geometry and never authorizes server actions.
- Compute findings survive Compare; appended history cannot erase previous entries. Active defects are distinct from historical resolved defects. Resolution requires a current, applicable, passing recheck linked to the finding, with matching stage/rule/policy/input and local disposition authority; a geometry defect also requires a geometry recheck. Resolution does not waive unknown origin or uncorrected extraction. Reopening restores the active defect. Rule ID/version and policy version are distinct; evidence document/page/zone references are checked.
- Human presence and accuracy are separate. Confirmed missing-from-source is representable; not-extracted/uncertain/unreviewed do not become source absence. Compare tabular review retains originals/history without geometry or linked-source edits.
- Required unavailable LTD comparisons retain completed results and a pending reason; no final approval/rejection or server transition is fabricated.

## Verification

Latest three-gap correction build (earlier review passes used `obj-toolbox-review/` and `obj-toolbox-signoff/`):

```powershell
dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln /p:BaseIntermediateOutputPath=obj-toolbox-currency/ /p:UseSharedCompilation=false /m:1
```

Result: success, zero warnings, zero errors, including strengthened human-review/empty-evidence/blank-actor assertions and final case-insensitive finding/forwarding/resolution linkage assertions. Earlier review builds and a fresh `-t:Rebuild` also passed; earlier initial-implementation builds encountered stale WPF generated files and a locked reference DLL. No project/toolchain changes were made.

Final focused regression command:

```powershell
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll "toolbox" "workspace planner" "settings workspace" "workflow execution settings" "review persistence" "review approval"
```

Result: **105 tests passed** outside the sandbox: 20 canonical/model/review-fix groups, 64 prior Toolbox/settings/workspace tests, and 21 existing review/approval tests. The earlier 97/101/104-test passes did not cover all later-discovered branches; they are historical evidence, not proof those gaps were absent. The latest three regression groups plus the evidence-alias group failed before their corresponding fixes and subsequently passed. Final resolution/forwarding alias assertions also pass.

Full-suite attempts did not complete: `ManifestSerializer.Write` / `FileSystem.MoveFile` raised `UnauthorizedAccessException` in legacy temporary-folder tests, including a Compare view-model test outside the sandbox. Independent `"compare workspace xaml"` reproduced the known missing indeterminate Finalize footer progress-indicator assertion (two preceding tests passed). Do not report the full suite as passing or later tests as executed.

After the fixes, the full harness was retried outside the sandbox and again stopped at the manifest replacement access error in `CompareWorkspaceViewModelTests.SaveRefreshesFinalizeCommandWhenReviewIsReady`. The earlier independent XAML failure was not rerun in this fix pass. No full-suite success is claimed.

Latest correction-pass full retry (outside sandbox) again stopped at `ManifestSerializer.Write:28` / `FileSystem.MoveFile` with `UnauthorizedAccessException`, this time in `CompareWorkspaceViewModelTests.SuspendTaskSavesDraftAndRequestsCloseOnSuccess:311` (fixture helper at line 1508). Earlier Compare tests, including SaveRefreshesFinalizeCommandWhenReviewIsReady, passed in this attempt. The manifest replacement failure remains intermittent baseline behavior; later tests were not executed. Independent XAML was not rerun in this correction pass. No full-suite success is claimed.

Subsequent full retry after evidence-alias hashing progressed beyond manifest tests and reproduced the known `CompareWorkspaceXamlTests.CompareWorkspaceExposesTaskLifecycleCommands:83` assertion about an indeterminate Finalize footer progress indicator. The last full retry on the final finding/forwarding/resolution-link patch stopped earlier at the intermittent manifest access failure in `SaveRefreshesFinalizeCommandWhenReviewIsReady:167`. Both baseline limitations remain; no tests after either fail-fast stop are claimed as executed. No full-suite success is claimed.

Operational `git diff --stat` is empty. Existing review model, persistence/hash/approval code, UI, DAML, settings and operational sprint tracker were not changed. No test fixture activation, FGDB, Enterprise/Innola call or deployment occurred.

## Files

- `Workflow/Review/Canonical/CanonicalReviewContract.cs`
- `Workflow/Toolbox/Model/ToolboxParcelModel.cs`
- `Workflow/Toolbox/Model/ToolboxExamination.cs`
- `ParcelWorkflowAddIn.Tests/Workflow/CanonicalReviewContractTests.cs`
- `ParcelWorkflowAddIn.Tests/Workflow/ToolboxParcelModelTests.cs`
- `ParcelWorkflowAddIn.Tests/Program.cs`: 20 canonical/model registrations, including eight regression groups across both reviews and evidence-alias follow-up; existing registrations retained.
- Existing `ToolboxPolicyTests` / `ToolboxBindingFixtureTests`: expose existing fixture helpers internally for reuse only.

## Review Boundaries And Next Work

This is an isolated in-memory contract, not a live examiner screen or extraction dispatcher. Canonical key/evidence normalization adapters for individual providers are not connected. Missing provenance/bindings cannot be silently manufactured to complete a real case; all three pilot bundles remain incomplete.

Authority, geometry-policy verification and check results are explicit caller-supplied local inputs. They are not proof of current Innola authority or real surveyed topology. Detailed production exclusion/effect/role policies remain open. Unverified geometry-policy inputs default to blocked. Production integration must establish those trusted inputs, implement check adapters and retain the deny-by-default remote boundary.

Non-geometry review records must now supply `CanonicalInputFingerprint`, computed from the current model and resolved scope. Missing fingerprints block readiness, including previously constructed in-memory results. Known global collections are survey metadata, document sections, parties, representatives, adjacent owners, volume/folio, participants, identifier pairs and global facts. Provider-specific extension fields are preserved but require explicit future binding before consumption; the hash is not a generic proof of every possible provider field.

Finding event statuses are validated as `recorded`, `retained`, `reopened`, `resolved` or `corrected_and_rechecked`. Only a validated `resolved` event plus a current passing recheck removes an active defect's effect. Original classification/evidence/history remains available in the cumulative register. Failed, pending, not-evaluable, unlinked, stale or unauthorized rechecks cannot approve resolution.

## Review Fixes

JotaPe requested fixing the four findings while keeping 13.4A in review and not starting 13.4B.

| Finding | Correction | Regression evidence |
|---|---|---|
| Canonical tabular values omitted from check fingerprints | Scoped canonical-input fingerprint on reviews and current case/parcel decisions; not dependent on caller bumping a token | Owner, title and metadata edits with unchanged tokens block old checks; fresh checks recover; other-owner and geometry-only inputs remain unchanged; unbound old results block |
| Revoked geometry-policy verification left parcel acceptance current | Verification state included in each parcel fingerprint; currency also compares readiness state | Three accepted parcels all become stale when verification is revoked; case/geometry approval blocks |
| Resolved source findings stayed active forever | Validated lifecycle plus linked current passing rechecks; history and classification preserved | Resolution restores clean readiness/geometry only with valid recheck; reopening reactivates; stale/unauthorized/nonpassing proof and unknown/mixed/uncorrected origins do not bypass gates; participant-scoped tabular resolution works in Compare |
| Malformed participant/title parcel links crashed scope resolution | Canonical loading validates inventory objects, array/string link types, uniqueness and target identities atomically | Null/string/object links, null/numeric/blank/foreign/duplicate entries and malformed inventories reject before adaptation for both participant and title collections; valid missing/empty/global links still load |

The four earlier fixes remain covered by the final 105-test selection. The three additional corrections and alias follow-up are described below. 13.4B implementation has not started.

## Final Signoff Findings

| Severity | Finding (now fixed) | Regression evidence |
|---|---|---|
| High | Case fingerprint omitted recomputed consumed evidence; currency ignored readiness/geometry state | `TraceOnlyChangesStaleAggregateAndPreserveHistory`: trace-data-only edit changes fingerprint and stales old approval with caller tokens unchanged; unchanged inputs stay current; status/geometry changes cannot match; decision history retained |
| High | Excluded-scope resolution bypassed validation of stored passing proof | `ExcludedFindingResolutionValidatesEveryConsumedInput`: fresh excluded resolution passes; changed trace, geometry, canonical values or human revision, absent fingerprints, empty evidence, blank actor and failed result block resolution; history/manual review values retained |
| Medium | Parcel canonical hash omitted applicable unlinked global participant/title facts | `GlobalFactsInvalidateParcelTabularChecksButNotGeometry`: both collections tested in Compare; global edits stale all consuming parcel checks, explicit local links affect only that parcel hash, explicit empty links affect no parcel hash, geometry unchanged and fresh tabular checks recover |

Blind review and edge-case review identified these paths; the acceptance auditor reproduced all three against the pre-fix DLL. That invalidated the preliminary clean result and justified withholding signoff. The authorized correction pass added the three regressions first, reproduced all three failures, then fixed only the isolated evaluator/tests/registrations. No legacy model/writer/hash/UI code changed.

Implementation: aggregate hashes recomputed consumed evidence plus resulting status/geometry validity, and `IsCurrent` checks both validity fields. A shared `ReviewInputIssues` validator is used by normal checks and resolution independently of inclusion. Parcel tabular hashing includes global facts with absent `parcel_ids`; explicit empty arrays remain unlinked. Canonical-input and aggregate hash formats now carry v2 markers; previous v1 in-memory proof is not silently current. No persistence migration is implied.

Evidence-alias follow-up: validation already accepted case-insensitive IDs, but trace hashing and resolution linkage had exact comparisons. Evidence hashing now resolves to canonical trace ID before case-insensitive deduplication/order; trace and latest-human lookups use the same identity comparer. Scoped finding registration and forwarding/resolution evidence linkage are consistent. The evidence hash also has a v2 marker. Alias casing cannot hide source changes or falsely block a fresh recheck; manual-review events remain intact. Regression: `MixedCaseEvidenceAliasesBindCanonicalTraceAndLatestHumanReview`, plus fresh excluded alias proof and source-defect forwarding assertions.

## Scoped Acceptance

2026-10-08: final patch reviewers report no remaining blocker within the authorized currency/resolution corrections, including the reproduced alias paths. Clean final build and 105 focused passes support completing JotaPe's requested 13.4A signoff. Canonical ownership amendment is accepted as an isolated adaptation; original operational 11.2 stays open. Parent 13.4 remains in-progress and B remains unimplemented. No claim of complete production authority, connected provider adapters, real topology, FGDB persistence/recovery, UI/live-TR workflow or deployment is made.

All commands ran from the isolated worktree; final build/focused/full outcomes are above. Full-suite, live-TR, FGDB and topology certification remain explicitly unclaimed.

13.4B owns rerun matching/shared-edge reconciliation/lineage. 13.5 owns durable maps, audited FGDB revisions and recovery. 13.8/13.9 own actual Compare/handoff integration. Original extraction JSON is still the persistence owner; the canonical layer only returns defensive JSON snapshots. Parent 13.4 and operational 11.2 remain open.
