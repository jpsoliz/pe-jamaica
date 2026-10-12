---
story_key: 13-5d-add-read-only-copied-case-import
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-5-add-versioned-toolbox-workspace-persistence
depends_on: [13-5c-add-isolated-prototype-disk-revisions]
execution_gate: accepted-prototype-store-and-reviewed-import-mapping
baseline_branch: refactor/toolbox-case-workspace
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.5D - Read-Only Copied-Case Import

Status: backlog
Refinement: bounded candidate; C acceptance and reviewed normalization/identity mapping required. Tracked through parent, no child tracker key.

## Story

As a developer preparing isolated migration fixtures, I want a reviewed import plan over frozen copied-case bytes, so that original evidence and identity remain traceable without touching sources or promoting legacy approvals.

## Boundary And Proposed Contract

Use sanitized copies only, physically separated from operational roots and destination. Plan first, execute only that plan into a new isolated prototype store via C. No operational CaseFolderStore dispatch, package restore, GDB creation/adoption, Enterprise/Innola, approval, source repair or UI integration. E/B own later schema/GIS migration. Missing/corrupt source records remain diagnostics; don't recover from live services.

`PlanImport` accepts externally verified full case identity, explicit source root, explicit artifact selection with roles, import operation ID and reviewed mapper version. It does not search arbitrary folders for latest artifacts. Required plan fields: contract `toolbox_copied_import_v1`, schema 1, operation_id, source_case, target_case, mapper_id/version, frozen inventory (relative path, role, length, exact SHA-256), transformations (source JSON pointer, target canonical pointer, rule/version), identity bindings, exclusions/unsupported artifacts with reasons, legacy approval/task observations, diagnostics and plan_digest. Dates/actors are retained as evidence, not session permission. No credentials or raw authorization response admitted to sanitized fixtures.

Support two explicit modes, never fallback between them:

| Mode | Rule |
|---|---|
| `retained_toolbox_bundle` | Decode A with external matching case; retain complete identity map/history and exact bytes. Missing map/history is an error, not a request to reallocate. Prototype storage receipts may be new, domain state must not be reconstructed/reapproved |
| `new_legacy_import` | Explicitly reviewed legacy-to-canonical mapper over selected extraction/review JSON. Preserve original bytes as C assets plus unknown/raw/manual fields. Allocate new Toolbox stable identities once during plan preparation, record full map, and reuse on execute/retry. Do not match by row index/name alone or present allocated IDs as legacy accepted identities |

Initially support only declared PE/PXA JSON shapes evidenced by fixtures and mapper tests. Ambiguous competing review/extraction files, missing keyed entities, duplicate case-insensitive keys, conflicting transaction identities, or unsupported contracts block execution. An explicit examiner-approved key assignment can be recorded in the plan; do not silently invent one. Source labels and array order remain evidence, not identity. Geometry stored in JSON remains source evidence/unverified geometry, not materialized/approved FGDB data.

Legacy approval flags, old review hash, workflow stage, actor, task and dates are imported as historical observations. New import starts with no Toolbox human validation, findings dispositions or geometry acceptance minted from those flags. Preserve preexisting manual vs suggested values and absence/negative/uncertain states; mapping omissions block execution or require explicitly recorded unsupported status. Forwarded source defects never become valid geometry. Known Toolbox state uses hydration, not this downgrade-to-empty-review rule.

Source capture: hold validated read handles denying writes/deletes while freezing selected ordinary-file bytes, respecting C physical containment. Hash bytes as read; compare inventory/identity to the plan before execution. Fail on sharing denial, changed source, unsupported links, outside-root paths, missing assets or bounds. Do not mutate attributes to make a source readable. Frozen bytes, not later reopened source paths, are the only execute inputs. Source absence after successful freeze does not change bytes; never recapture silently. No GDB internal-file traversal/copy: record explicit unsupported GDB migration until E/B hosted adapters are accepted. External PDF/images may be copied as opaque sanitized C assets within bounds; no OCR rerun or remote fetch.

Execute validates plan digest, complete identity/map closure, mapper version, frozen inventory and separately supplied plan acceptance bound to that digest. C request includes encoded A bundle, frozen source assets, exact import plan/provenance sidecar and all transformations. Sidecar is an immutable revision asset, not an editable competing master; it does not require changing A v1. Same operation/plan reuses prepared bytes/IDs and returns C receipt; changed plan requires a distinct explicit import operation. No target overwrite, automatic merge or repair of a previously imported case.

### Durable Import Preparation

Planning remains read-only and returns owned bytes/map; a returned plan alone is not restart-safe. After explicit plan acceptance, `PrepareImport` freezes the complete C save request: operation_id, proposed_revision_id, null expected parent for new store, actor, prepared_at, exact bundle bytes, plan/provenance and source assets, and C request_digest. Allocate these storage/domain IDs and timestamps only once, before preparation is sealed. Never allocate during Execute or reopen.

Persist this preparation under a separately authorized isolated `import-preparations/<operation_id>/` root, physically distinct from source and the new target case. Reuse C containment/create-new/flush/read-back primitives, but do not create/publish the target store yet. Use an exclusive preparation handle, strict owner marker and immutable `preparation.json` (`toolbox_import_preparation_v1`, schema, case, plan_digest, complete pinned C request fields and inventory). Write frozen bytes first; publish a completion seal only after all hashes/lengths and A decode validate. Mutable plan acceptance attestation is supplied externally again at Execute, not saved as authority. No receipt/acceptance claim from the preparation seal.

Resume accepts only a verified sealed preparation; reconstructs the exact C request from it, never source recapture or replanning. Crash before seal leaves incomplete preparation with no executable result; retain it and return `incomplete_preparation`. Recovery may complete only from exact retained bytes plus pinned intent, otherwise explicitly abandon the preparation and use a new operation ID; never regenerate a map under the old ID. Crash during C publication resumes from sealed preparation and C journal. Once verified C publication occurred, its receipt remains the outcome even if preparation is later missing; retry resolves that receipt with supplied matching intent, not a new import. No automatic preparation deletion/GC; retention/count/byte limits inherit C, including aggregate frozen asset sizes. Source loss after a valid seal is permitted because owned frozen bytes survive.

Limits: inherit A/C byte/asset/work bounds; plan metadata at most 1 MiB, at most 256 selected artifacts and 100,000 mapping entries, bounded JSON depth 64. Stream inventory bytes; no unbounded recursive discovery. Fail instead of dropping source evidence or truncating field values.

## Acceptance Criteria

1. Given declared legacy single/multi-parcel fixtures, planning preserves exact source bytes, raw/unknown/manual values, source identity and explicit key transformations without writing to source or destination.
2. Given reviewed plan and accepted C store, preparation durably pins all bytes/map/storage IDs/actor/time before execution creates one new isolated revision; restart from its seal recovers the same C request and inspection state. Incomplete preparation fails explicitly; retry never mints identities or duplicates history.
3. Given retained A/B bundles, import preserves all history/IDs and stale proofs. Missing maps/archives/pending proposals or unsupported capture fail; never route them to new legacy import.
4. Given ambiguous files/keys/case, unsafe paths, source mutation, unsupported GDB/format, oversize inputs or incomplete mapping, planning/execution produces typed diagnostics and no published partial target.
5. Given legacy approval/task/geometry flags, new import preserves them as audit evidence only; fresh trusted policy, human review and permissions remain external and no source PE geometry becomes writable.
6. Given failures in source freeze, staging or C publication, protected-source hashes/bytes remain unchanged and C recovery outcomes remain honest. No target overwrite/merge or latest-file fallback.
7. Given verification, all 158 predecessor groups and C regressions remain passing; independently report supported mapper fixtures, import failures and full-suite limits. No production migration or FGDB proof claimed.

## Tasks / Subtasks

- [ ] Review exact supported artifact roles, legacy field mapping, identity assignment and plan-acceptance boundary; obtain C acceptance. (AC1-5)
- [ ] Add pure import plan/provenance records, strict limits and digest fixtures; implement mode-specific mapping with explicit unsupported results. (AC1,3-5)
- [ ] Add read-only contained source freeze, durable sealed preparation and plan-bound C execute seam without legacy call-site changes. (AC2,4,6)
- [ ] Add round-trip/manual/unknown/absent/ambiguous keys, retained-B history, source-mutation, incomplete-seal/source-loss and crash/retry fixtures preserving pinned actor/time/IDs. (AC1-6)
- [ ] Register/build/run focused/full lanes and record scoped acceptance evidence. (AC7)

## Dev Notes / File Map

NEW `Workflow/Toolbox/Persistence/Import/ToolboxCopiedCaseImport.cs`, `ToolboxImportPlan.cs`, `ToolboxLegacyImportMapper.cs` under the add-in; NEW `ParcelWorkflowAddIn.Tests/Workflow/ToolboxCopiedCaseImportTests.cs`. UPDATE only test registrations in `Program.cs`, after reading it fully. Reuse C filesystem/store and A canonical codec/restoration; no duplicate serializer or identity engine. Exact mapper matrix (source pointer, output, null/default, unknown-field policy, fixture) is an implementation deliverable reviewed before execute is enabled.

REFERENCE ONLY: `CaseFolders/CaseFolderLayout.cs`, `CaseFolderStore.cs`, `Contracts/ManifestDocument.cs`, `ManifestSerializer.cs`, `SourceFileCopyService.cs` and existing review contract/document readers. Legacy top-level `transaction_id` may contain the transaction number, while nested Innola metadata distinguishes ID/number; never copy that field blindly into Toolbox TransactionId. Environment and case ID must arrive externally. Both `manifest.json` and `_manifest.json` must be inventoried; disagreement blocks import, not resolved by priority.

Legacy absolute `copied_path` must never read outside the copied root, even when the original location still exists. No basename-only relocation, duplicate top-level discovery or session reopen/save side effects. Preserve original manifest bytes because typed legacy serialization drops unknown fields. `SourceFileCopyService` collision renaming/timestamps are not faithful provenance; `WorkflowSession` restores legacy workflow state and is not an import seam. Logical source metadata tokens and file-byte checksums remain distinct.

Never use `CaseResumePackageService` root replacement or the approved-JSON-driven output adapter to import/repair an FGDB. Keep linked PE snapshots read-only. Regression references: `CaseFolderStoreTests`, `SourceFileCopyServiceTests`, `CanonicalReviewContractTests`, `ToolboxWorkspaceStorageTests`, `ToolboxReconciliationStorageTests`; reuse their fixture patterns without changing existing assertions. Include relocated-copy/original-still-present, conflicting manifests, leading zeros, same-size changed bytes and ambiguous parcel grouping.

## References And Verification

- [C contract and test commands](13-5c-add-isolated-prototype-disk-revisions.md), [accepted A contract/evidence](13-5a-add-storage-contract-and-hydration-foundation.md), [ownership mapping](../../planning-artifacts/toolbox/toolbox-contract-ownership-and-identity-mapping.md).
- [Parent AC5/10/14/16](13-5-add-versioned-toolbox-workspace-persistence.md), [migration M3](../../planning-artifacts/toolbox/toolbox-parallel-migration-plan.md), [project rules](../../project-context.md).

Use existing net8.0-windows/x64 harness and C build/focused/full commands with separate `obj-toolbox-import/`. Preserve A's default-PointIds limitation, no hash normalization. Record actual supported formats and unsupported inputs; original source immutability is asserted byte-for-byte, not just via successful target decode.

## Dev Agent Record

Prepared 2026-10-11; not implemented, no import tests run. All tasks unchecked; execution/readiness gates remain visible.
