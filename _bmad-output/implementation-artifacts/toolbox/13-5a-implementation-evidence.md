# 13.5A Implementation Evidence

Date: 2026-10-11
Status: accepted/done for isolated no-writer storage-contract, byte-codec and hydration scope; explicit JotaPe acceptance on 2026-10-11. No disk persistence or production cutover implied.
Branch: `refactor/toolbox-case-workspace`, checkpoint `0a6188f` plus preserved uncommitted accepted B corrections.

## Delivered Scope

Pure Capture/Encode/Decode APIs over explicit versioned DTOs. Deterministic UTF-8 wrapper bytes, exact canonical/policy payload bytes, SHA-256 transport checks and normalized content-addressed model references. Narrow model/register Restore factories preserve identities, inclusion/history, findings, human reviews, saved decisions, current checks, B reruns, dependencies, lineage, applicability, operation/audit records and proof strings. No action replay, new entity IDs, clocks, context construction or permission transfer.

32 record field manifests, a fixed type/recursive-nullability signature and 14 enum wire/numeric sets lock prototype v1. Signature: `3af2d0aca8232223cdefc4fd1cb5b634814ef3b5c8309031237098689e4e4300`. This is software schema admission, not an authenticity signature.

Limits cover bytes, depth, strings, models, graph edges, all collection entries including canonical unknown values, unique payload bytes before capture base64 expansion, repeated projection bytes, canonical quadratic-reader admission, historical index/edge/ancestor work and wrapper-byte processing. Dependency closure is indexed and does not call ActualInput/consumer fingerprint calculation. Immutable historical CorrectionId checks only validate retained reference identity; no current proof is rebound.

Policy loading uses the existing exact-byte loader only with externally supplied pins. Stored pins and checksums cannot authorize themselves. Missing policy bytes/trust yield explicit inspection diagnostics; mismatches fail without a partial aggregate.

## Verification

Final commands, from the isolated worktree:

```powershell
dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln /p:BaseIntermediateOutputPath=obj-toolbox-storage/ /p:UseSharedCompilation=false /m:1
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll "toolbox" "workspace planner" "settings workspace" "workflow execution settings" "review persistence" "review approval"
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll
```

| Lane | Actual result |
|---|---|
| Final build after schema lock | Passed, zero warnings/errors |
| Storage-only tests | 23 groups passed before final schema pin; same groups passed again in final focused lane with the pinned type/nullability/enum contract test |
| Final focused regressions | 158 passed: all 135 predecessor tests plus 23 new groups |
| Final full harness | 207 passed, then fail-fast at `CompareWorkspaceXamlTests.CompareWorkspaceExposesTaskLifecycleCommands:83`: missing indeterminate Finalize footer progress indicator. Later tests, including appended storage groups, did not execute in this lane |
| Whitespace | `git diff --check` passed; new-file checks recorded at close-out |

Red checks were observed: the explicit contract test failed before the DTO existed; the A round-trip test failed against a deliberately nonfunctional codec stub. Subsequent focused fixtures drove corrections; final counts above are after those fixes, not intermediate failures.

Full-suite green is not claimed. The Compare XAML assertion is the documented unchanged baseline limitation; historical manifest temporary replacement access denial remains separately documented, but did not cause the final run's failure. No unrelated operational fix attempted.

## Regression Coverage

The 23 named `toolbox storage ...` groups cover A counts 0/1/3/10; source/manual/unknown data; exclusions and audit; live pending checks and historical records; B readiness/fingerprint parity; source, geometry, tabular, links, inclusion and policy invalidation; shared archives; actual split -> merge -> regenerated-key history; candidate-only evidence; untrusted policy; corrupt/missing/foreign/duplicate/aliased/null/unknown fields; map/canonical TR invariants; unsupported A history/pending proposals; payload integrity and ownership; graph corruption; optional default/empty collections; deep 80-link history and aggregate/model/projection/work/byte/string/depth limits. Existing variable-count, shared-boundary, recorded-defect, LTD-pending and R1-R8 predecessor fixtures remain passing.

## Independent Review Corrections

Read-only independent review examined the codec, helpers, factories and tests. Confirmed findings and follow-ups were corrected with regression coverage:

- Capture aggregate bytes checked before retaining additional base64 payloads; no unnecessary policy copy.
- Prior lineage parent/evidence closure, uniqueness, casing and ordered child pairing.
- Accepted audit choice shapes, kind-specific identity/evidence closure, duplicate references and exactly one explicit global disposition.
- Applicability record/target closure through preserved findings, manual/correction identities, historical maps and retained candidates, without collapsing repeated history or refreshing old proofs.
- Explicit supported dictionary comparer profile; unsupported capture comparers fail.
- Map/inclusion/source aliases independently rejected before comparer conversion, with exact source inventory coverage.
- Indexed finding lookup, precharged archive union/traversal and bounded historical correction-ID byte work.
- Field names, types/nullability and enum wire/numeric values locked against silent v1 expansion.

Final narrow static review found no remaining findings. JotaPe explicitly accepted the isolated no-writer scope on 2026-10-11. Acceptance retains all verification limits below and does not close parent 13.5.

## Retained Limits

- Prototype only: no file store, journal, lease, paths, legacy import, FGDB schema/write/copy, package, UI integration, source dispatch, policy publication, remote action, installation, merge or commit.
- A-only history with absent original evidence is unsupported; no history is discarded or synthesized. Pending proposal capture is explicitly rejected.
- Comparer profile is intentionally limited: maps/inclusions/source hashes OrdinalIgnoreCase; binding/child-inclusion/geometry-proof maps ordinal/default string equality. Other comparers require a future contract, not silent conversion.
- Default boundary PointIds is encoded as null and restored as default, distinct from empty. The existing domain generic JSON hash throws for that default ImmutableArray in both original and restored snapshots; the codec retains it for inspection and does not normalize or certify it. Future domain evaluation must address this separately before using such state for approval.
- Transport hashes are not authenticity or geometry proof; operation records are in-memory replay evidence, not durable commit receipts. Fresh context/policy/geometry inputs and permissions remain external.
- Parent 13.5 production ownership/schema, writer, F01/F02/F03/F07 and other migration gates remain open. Original operational 11.2 remains open.

See [story and file list](13-5a-add-storage-contract-and-hydration-foundation.md), [parent](13-5-add-versioned-toolbox-workspace-persistence.md), [baseline deferred work](deferred-work.md).
