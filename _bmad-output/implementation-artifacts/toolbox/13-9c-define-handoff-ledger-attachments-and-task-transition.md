---
story_key: 13-9c-define-handoff-ledger-attachments-and-task-transition
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-9-add-revision-consistent-toolbox-handoff
depends_on: [13-9a-define-revision-snapshot-package-and-restore-v2]
execution_gate: explicit-sandbox-authorization-reviewed-task-plan-policy-actual-f04-f06-and-required-b-receipts
implementation_authorized: false
---

# 13.9C - Handoff Ledger, Attachments And Task Transition

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through existing13.9 entry, no child key. B acceptance/receipt is conditional on reviewed task policy requiring projection, not silently optional.

## Story

As an authorized sandbox examiner, I want a durable ordered handoff plan with verified attachments/task outcomes, so that restart preserves confirmed work and uncertain effects are reconciled rather than duplicated.

## Scope And Dependencies

Requires accepted A/owner/Compute/Compare, reviewed D02/D05/D10/task outcome policy, actual F04 mapping/F06 upload/task read-back capability and explicit named sandbox effects. B receipt required whenever policy mandates projection. Owns versioned frozen plan/durable ledger, attachments and verified lifecycle orchestration; task-specific PLA/RT adapters remain 13.10. No production/live effect by preparation, no global RemoteActionAccess enablement.

## Acceptance Criteria

1. Given handoff plan, freeze full case/snapshot/decision/policy/target/mapping, exact package/report/annex payloads, original actor/task observations and mandatory ordered actions. Distinguish draft/suspend, Compute forwarding and completed Approved/Rejected branches. Required pending Compare cannot become final completion; geometry publication remains independently gated. Plan/source/payload changes require new operation/review, not changed bytes behind old step receipts.
2. Given durable ledger, strict versioned bounded plan/step records track pending/running/confirmed/failed/ambiguous/reconciled with exact request digest/action/target/time/result/object identity/read-back evidence. Persist each effect intent/running marker through one accepted local owner before network call; serialize competing local resume attempts. Mutable progress/fresh auth never changes frozen intent digest or grants authority. A v1 remains unchanged; admit separate owner-held ledger contract before execution.
3. Given fresh task action, verify actual environment/TR/instance/operator/assignment/session, evidenced mapping/version and currently returned allowed transition/branch immediately before each effect. taskId=transactionId fallback, selected preset/name/first default transition cannot authorize. Persist only redacted provenance, never tokens. Later task/assignment/permission divergence blocks new effects; historical confirmed receipt remains audit.
4. Given required action order, use A exact bytes and required verified B receipt, upload only allowed source-type/document targets and validate actual returned remote identity/type/content/read-back. Missing package/report/task-specific artifact or current examination/authority blocks before effect. Existing upload success bool/local path alone cannot confirm content. Confirmed identical steps are not resent.
5. Given restart with running/no response, timeout/cancellation or ledger flush failure after remote success, treat effect ambiguous until F06 read-back/dedup proves exact outcome or absence. If actual service cannot locate/deduplicate object by sufficiently discriminating identity/content/request, keep blocked recovery and require explicit reviewed disposition; never guess absence from name alone or resend automatically. Read-before-write alone cannot ensure safe concurrent dispatch; action contract must prove enforceable remote idempotency/exclusion or block retries/concurrent writers.
6. Given actual transition, require all mandatory steps confirmed for the same frozen intent, freshly allowed branch and current task authority, then persist/query actual next-state evidence. Lost response is ambiguous, not failed/not-sent; reconcile before later transition. Report plan completion only when all required effect/result identities verify; local Save/suspend package or final decision is not server completion.
7. Given partial failure, preserve successful projection/attachments/decision and source snapshots; failed/ambiguous plan resumes only missing proven-safe actions with same IDs/bytes. No whole-plan replay, stale expected-target rebase, duplicate domain audit or compensation deleting newer/unrelated remote objects. New local inputs/decision cannot reuse old plan as current; prior steps remain immutable history.
8. Given completion, F04/F06 actual isolated-service task mapping, confirmed upload identity/read-back and ambiguous transition recovery plus pure crash/ordering/conflict fixtures pass. Separately report supported/blocked target capabilities, exact branch mapping and real sandbox effects; mocks cannot substitute. Preserve predecessor lanes/limits and obtain C then scoped13.9 acceptance, no production/operational11.2 closure.

## Tasks / Subtasks

- [ ] Confirm explicit code/sandbox scopes, accepted A/owner/decisions and required B/policy/F04/F06 gates. (AC1-8)
- [ ] Define strict frozen plan/owner-held ledger/step/authority/receipt mappings and reviewed bounds. (AC1-2)
- [ ] Prove actual task/transition mapping and upload/task reconciliation capabilities before adapter enablement. (AC3-5)
- [ ] Implement exact ordered attachment/transition orchestration with honest durable progress. (AC4-6)
- [ ] Implement restart/read-back/explicit recovery and concurrent/stale-plan denial. (AC5,7)
- [ ] Run actual F04/F06 sandbox plus pure tests, review and obtain C/parent acceptance. (AC8)

## Developer Context And File Map

Future NEW Toolbox/Handoff/ToolboxHandoffPlan.cs, ToolboxHandoffLedger.cs, task authorization/attachment/lifecycle adapters and tests. Reuse existing Innola session/transport/upload/lifecycle mechanics only after full target/source/test read and exact API/mapping evidence; legacy default transitions and boolean upload returns are insufficient. Existing Compare Finalize/PLA/RT orchestration may mix report/upload/cleanup/transition, so bind individually reviewed effects rather than whole method. No baseline settings/DAML/task configuration or production URLs by default; F07 for installation, exact sandbox targets required.

## Test Requirements And Completion Evidence

Use parent lanes and separately authorized F04/F06 targets. Test durable running before call, crash after server success before ledger flush, identical versus changed plan, confirmed upload plus later failure, ambiguous same filename/content, two resumers/writers, stale actual task/assignment, fallback ID, missing B mandatory receipt, rejection/source-defect branch without invalid geometry publish, pending Compare denial, timeout transition read-back and no duplicated confirmed effect. B is independent protocol; C consumes its receipt and cannot force re-publication after attachment failure. No runtime/live test performed now.

## References

- [Parent](13-9-add-revision-consistent-toolbox-handoff.md), [A](13-9a-define-revision-snapshot-package-and-restore-v2.md), [B](13-9b-define-enterprise-projection-and-verified-receipts.md), [ledger outline](../../planning-artifacts/toolbox/toolbox-contract-catalog.md#handoff-plan-and-ledger-v1).
- [F04/F06](../../planning-artifacts/toolbox/toolbox-acceptance-strategy.md), [task/action decisions](../../planning-artifacts/toolbox/toolbox-decision-register.md), [13.8 decision](13-8-compose-read-only-compare-and-correction-flow.md), [review record](13-9-story-preparation-review-2026-10-11.md), [project rules](../../project-context.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime/sandbox authorization false; all tasks unchecked. No ledger/upload/transition code or service requests, task mapping experiment, runtime tests, installation, commit or deployment. Actual mapping/bounds/receipt/dedup evidence remains future gate.
