---
story_key: 13-5d-1-define-source-freeze-mapping-and-import-plan
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-5d-add-read-only-copied-case-import
depends_on: [13-5c-add-isolated-prototype-disk-revisions]
execution_gate: explicit-runtime-authorization-accepted-c-and-reviewed-mapper
implementation_authorized: false
---

# 13.5D.1 - Source Freeze, Mapping And Import Plan

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through parent 13.5; no child tracker key.

## Story

As a migration-fixture developer, I want an explicit plan over owned copied-case bytes, so that identity and evidence can be reviewed before any destination is created.

## Scope And Dependencies

The [D parent](13-5d-add-read-only-copied-case-import.md) owns import policy. This slice owns contained source capture, strict plan records/digest and mode-specific mapping; D.2 owns durability, D.3 owns C execution. Runtime requires accepted C containment/protocol and a reviewed fixture-backed mapper. No destination/preparation writes, OCR, remote fetch, FGDB traversal, source repair or operational dispatch. Documentation may precede C acceptance.

## Acceptance Criteria

1. Given an explicit sanitized copied root and artifact role selection, validated handles deny write/delete while selected bytes are frozen. Require physical separation from operational, preparation and target roots; reject sharing denial, unsafe links, outside-root references, changed bytes and unsupported storage. Planning writes no source or target data or attributes; tests compare exact protected bytes before/after.
2. Given both manifest.json and _manifest.json, inventory both and diagnose disagreement; never choose by priority. Resolve full environment/case/transaction ID/number from separately verified context and reviewed mapping. Preserve leading zeros; a legacy top-level transaction_id may be the transaction number and is never copied blindly. Stale copied_path never follows the original root, even if it still exists.
3. Given retained_toolbox_bundle mode, unchanged A decode preserves the complete map/archives/history/raw bytes and stale proof, with matching source/target full case tuple. Missing known Toolbox identity/history is an error, never fallback to new import. Given new_legacy_import, allocate identities once from reviewed canonical keys, retain the complete map, and create no human validation/dispositions/geometry acceptance from legacy flags.
4. Given declared PE/PXA fixture shapes, an exact reviewed matrix specifies source JSON pointer, target owner/pointer, conversion/version, identity/comparer rule, null/absent/manual/unknown policy and supported fixture. Unsupported/ambiguous keys, parcel grouping or omitted meaningful fields block executable planning; explicit reviewer key assignment is recorded, never inferred from row index/name/coordinate. Geometry is unverified source evidence.
5. Given a valid plan, strict records bind mode, mapper/version, source/target identities, operation ID, frozen role/path/length/digest inventory, transformations, identity bindings, exclusions and historical approval/task observations. Pin independent golden bytes/digest; exact original bytes and unknown values stay owned. External acceptance is bound to that digest and is not a saved permission.
6. Given plan/selection limits, charge A/C bounds, 1 MiB for the entire serialized plan including transformations/maps/observations/diagnostics, 256 selected artifacts, 100,000 mapping entries and JSON depth 64 before allocation. The effective limit is the first exhausted budget; maximum entry count does not promise that all entries fit in 1 MiB. Reserve C asset capacity for mandatory provenance/plan sidecars. Preserve separate path/role provenance for identical source bytes, but supply a unique digest-keyed C asset inventory; identical bytes may share one asset reference and changed bytes under the same digest conflict. Diagnose rather than truncate/drop evidence. Inventory both required manifests within the selection budget; no recursive newest-file discovery.
7. Given completed runtime work, named tests cover relocated copies with originals still present, conflicting manifests/identity, same-size modified bytes, absent/manual/unknown values, ungrouped single versus ambiguous multi-parcel input, retained B history and duplicate aliases. Report actual supported mapper shapes, build/focused/full results and source immutability separately; obtain D.1 scoped acceptance.

## Tasks / Subtasks

- [ ] Confirm authorization, C acceptance and exact supported-role/mapper review. (AC1-7)
- [ ] Define strict plan/provenance records and golden digest fixtures. (AC5-6)
- [ ] Implement contained read-only freeze and explicit identity/manifest inventory. (AC1-2)
- [ ] Implement reviewed retained/new mode mapping with complete identity and unsupported diagnostics. (AC3-4)
- [ ] Add negative/round-trip/source-immutability tests and record scoped acceptance. (AC7)

## Developer Context And File Map

NEW proposed `Workflow/Toolbox/Persistence/Import/ToolboxImportPlan.cs`, `ToolboxLegacyImportMapper.cs` and source-freeze helper if needed; NEW `ParcelWorkflowAddIn.Tests/Workflow/ToolboxImportPlanTests.cs`. Reuse accepted C handle/limits infrastructure, A codec, CanonicalReviewContract and Toolbox identity engine. Reference CaseFolderLayout/ManifestDocument/readers only; typed manifest reserialization loses unknown fields. Do not call WorkflowSession, CaseResumePackageService or collision-renaming SourceFileCopyService to plan imports.

UPDATE only Program.cs future test registrations after full read; preserve substring-OR, fail-fast and no-argument behavior. No changes to A manifests, source adapters, CaseFolderStore, ManifestSerializer, settings/DAML, approval/domain hashes or remote services. Work in refactor/toolbox-case-workspace with existing dirty work preserved.

## Test Requirements And Completion Evidence

Use parent D/C commands with obj-toolbox-import/ and explicit import filters. Preserve baseline 158 focused groups plus accepted C groups; full-harness 207-stop/Compare XAML and default-PointIds limitations remain historical. Completion needs reviewed mapper matrix, plan golden fixtures, supported/unsupported shape list, actual tests and scoped acceptance. No code/tests or exact mapper implementation created by preparation.

## References

- [D source contract](13-5d-add-read-only-copied-case-import.md), [C contract](13-5c-add-isolated-prototype-disk-revisions.md), [accepted A evidence](13-5a-implementation-evidence.md).
- [Ownership/identity rules](../../planning-artifacts/toolbox/toolbox-contract-ownership-and-identity-mapping.md), [project rules](../../project-context.md).
- [D.2 sealed preparation](13-5d-2-define-sealed-import-preparation.md), [D.3 execution](13-5d-3-define-import-execution-and-retry.md), [preparation review](13-5deb-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime authorization not granted; tasks unchecked. No capture fixture, implementation, build/runtime tests, commit or deployment performed. Populate model/logs/notes/changed files only during authorized execution.
