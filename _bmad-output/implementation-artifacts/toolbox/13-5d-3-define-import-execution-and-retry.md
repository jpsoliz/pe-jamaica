---
story_key: 13-5d-3-define-import-execution-and-retry
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-5d-add-read-only-copied-case-import
depends_on: [13-5d-2-define-sealed-import-preparation]
execution_gate: explicit-runtime-authorization-and-d1-d2-c-acceptance
implementation_authorized: false
---

# 13.5D.3 - Import Execution And Retry

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through parent 13.5; no child tracker key.

## Story

As a migration-fixture developer, I want execution of only the accepted sealed import request, so that one isolated revision is created without changing source evidence or replaying legacy approval.

## Scope And Dependencies

Accepted D.1/D.2/C and explicit runtime authorization are entry gates. Owns new-target creation, frozen request execution, exact C receipt reconciliation and import result. Reuses D.1 mapping and D.2 preparation without allocation/replanning. [D parent](13-5d-add-read-only-copied-case-import.md) remains policy source; no E/B materialization, target adoption/merge, UI or remote actions.

## Acceptance Criteria

1. Given fresh external plan-digest acceptance, expected case/trusted policy and verified sealed D.2 bytes, validate full closure, mapper version and target store identity before any target write. Execute uses only preparation-owned bytes; source loss is permitted and never triggers recapture. New target is exclusively created with pinned store_id; an existing owned target is used only to resume this exact prepared operation, never adopted or overwritten as a fresh import.
2. Given full inventory, source assets and immutable plan/provenance sidecar are committed through C with the exact pinned request. Plan digest and C request_digest remain distinct. Retained Toolbox mode preserves original identity/history; new legacy mode keeps old flags/geometry as observations and creates no current acceptance. Linked PE remains read-only.
3. Given same operation after restart/successors, query C's verified evidence first and return exact matching receipt; changed plan/bytes/IDs/actor/time/store conflict. Historical missing tails require C explicit Recover. A missing preparation after publication requires caller-supplied matching frozen intent/digest sufficient for C verification; insufficient evidence returns unavailable/conflict, not fresh import. Partial initialized targets without valid ownership are retained and blocked.
4. Given cancellation/failure at target create, C stage/seal/pointer/receipt, use C's honest empty/pending/quarantined/committed outcomes. An exception cannot mean target was absent; no partial domain aggregate or falsely completed import is returned. Retry never remaps, creates duplicate domain audit or silently publishes with changed parent.
5. Given ambiguous formats/mapping, unsafe roots, tampered preparation, unsupported host/source data or unavailable trusted policy, fail before publication without source/operational mutation. A valid policy-inspection bundle remains inspectable, not writable. Target and preparation evidence are retained; no arbitrary deletion or source repair.
6. Given completed runtime work, report actual supported import modes/fixtures, immutable source hashes/bytes, receipt identity, cancellation/process restart and predecessor lanes. D closes only after D.1-D.3 implementation review and scoped acceptance; this is not FGDB migration or operational 11.2 completion.

## Tasks / Subtasks

- [ ] Confirm authorization, accepted D.1/D.2/C and exact frozen acceptance/policy inputs. (AC1-6)
- [ ] Implement plan/preparation validation and pinned new-target/resume routing. (AC1-2)
- [ ] Compose C save/retry/recovery receipts without replanning or allocation. (AC3-4)
- [ ] Add unsupported/tampered/policy/source-loss/partial-target and crash/retry fixtures. (AC3-5)
- [ ] Review results and obtain D.3 then D scoped acceptance. (AC6)

## Developer Context And File Map

NEW proposed `Import/ToolboxCopiedCaseImport.cs` and `Workflow/ToolboxCopiedCaseImportTests.cs`. Reuse accepted C store and D.1/D.2 types; only narrow reviewed integration amendments may touch them. Do not use WorkflowSession/CaseFolderStore dispatch, legacy copy collision naming, package restore or output adapters. Future harness registration/worker additions preserve substring-OR/fail-fast behavior after full read. No A format/domain hash/settings/DAML/remote changes.

## Test Requirements And Completion Evidence

Use D parent build/test lane; run new named groups independently of known full-suite stop. Cover retained B archives, new manual/unknown legacy state, retry after successor, missing historical tail/preparation, wrong store_id, target collision/incomplete initialization, source loss, policy inspection, changed frozen intent and late cancellation. Preserve 158 plus accepted C/D predecessor groups and default-PointIds limits. Completion needs actual evidence, review and scoped acceptance; no runtime tests run by this specification.

## References

- [D contract](13-5d-add-read-only-copied-case-import.md), [D.1](13-5d-1-define-source-freeze-mapping-and-import-plan.md), [D.2](13-5d-2-define-sealed-import-preparation.md), [C](13-5c-add-isolated-prototype-disk-revisions.md).
- [Persistence parent](13-5-add-versioned-toolbox-workspace-persistence.md), [project rules](../../project-context.md), [preparation review](13-5deb-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime authorization not granted; all tasks unchecked. No import target, implementation, runtime test, commit or deployment created. Fill model/logs/notes/files at authorized execution.
