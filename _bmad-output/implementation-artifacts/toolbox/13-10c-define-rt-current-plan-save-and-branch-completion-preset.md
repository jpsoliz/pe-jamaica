---
story_key: 13-10c-define-rt-current-plan-save-and-branch-completion-preset
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-10-compose-plan-annexation-and-rt-toolbox-presets
depends_on: [13-5-add-versioned-toolbox-workspace-persistence, 13-6-register-reusable-toolbox-capabilities, 13-9-add-revision-consistent-toolbox-handoff]
execution_gate: explicit-runtime-read-sandbox-authorization-and-reviewed-current-plan-child-branch-contract
implementation_authorized: false
---

# 13.10C - RT Current-Plan Save And Branch Completion Preset

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Existing 13.10 entry tracks this child, no new key.

## Story

As an RT examiner, I want immutable PE context, current Plan neighbor review and distinct Save/Save & Close actions, so that current-task edits and branch completion are verified without modifying SpatialUnits or source PE.

## Scope And Dependencies

Requires accepted owner/capabilities/13.9 ledger/authority/reconciliation plus reviewed actual RT Plan/child/branch/outcome mappings and explicit UI/read/host/sandbox scope. Does not depend on A/B preset implementation. Owns stage-driven RT composition, compact neighbor/reference/check review and effect planning through13.9, not a duplicate remote orchestration engine. [Parent](13-10-compose-plan-annexation-and-rt-toolbox-presets.md) owns task semantics.

## Acceptance Criteria

1. Given In RT Examination stage, local eligibility applies regardless of main transaction type, but remote actions need current verified actual instance/assignment/session/mapping. Resolve current/main RT Plan uniquely by explicit server identity/version and originating PE separately; ambiguous/missing Plan blocks writes, no FirstOrDefault or blanket loop over fetched Plans. Unknown party role fails instead of defaulting Owner; supported Owner/Occupier review preserves typed source/raw evidence.
2. Given parent/main attachment preload, retain successful usable copies and individual failure warnings, fail only if none usable, without weakening non-RT loader behavior. PE SpatialUnits/working_review/local references retain explicit source identity/revision and read-only inspection/approval labels; no latest-artifact fallback grants accepted source proof. Display parcel_name/area_sqr/suid/created_utc and no editable/saved SpatialUnit attributes or geometry/boundary fields.
3. Given Local Save, preserve draft/evidence under accepted owner only, no server effect. Given explicit RT Save,13.9 executes only pinned current Plan neighbor update under fresh authority/version protection and verified read-back. Preserve checkList/unrelated existing Plan children/fields; no PlanCheck change, SpatialUnit save, transition or cleanup/close. Observations remain local audit unless a separately reviewed current Plan field effect is explicitly authorized; source PE untouched.
4. Given Save & Close, examiner must explicitly choose branch before effects. Execute same neighbor save, then update/append exactly one intended current RT PlanCheck using valid plan_check_type_* and passed=true, preserving unrelated check rows, then exact selected allowed transition. These logical steps may share a reviewed server request if actual API combines neighbor/check writes, but ledger/read-back must distinguish both; no assumed cross-step transaction or duplicated neighbor step.
5. Given empty current child arrays, obtain Innola-generated Neighbor/PlanCheck templates for intended current Plan through verified create endpoint/shape, preserving server fields and returned IDs. Do not invent child IDs, reuse original linked-PE snapshots or literal approved checkType. Template/create effects and response loss are13.9 ledger steps requiring actual F06 reconciliation before retry. Multiple candidates/unknown child shape/version/conflict block write.
6. Given branch choice, retain label/gateway aliases and target stage in frozen plan: Yes, proceed next -> Review Completed RT Examination; No, prepare pre-check log sheet -> Prepare Plan Pre-Check Log Sheet. Resolve currently returned actual transition exactly; no absent choice/default/first fallback. passed=true PlanCheck is a process checkpoint for both branches, not an approved examination/valid-geometry claim. No UI label is a fabricated definition ID.
7. Given neighbor/check success then failed/ambiguous transition, retain exact confirmed request/child/check/content IDs and local dirty/review context, never resend confirmed steps or append a duplicate check. Fresh task/session/version revalidation and actual remote read-back determine recovery. Refresh/map cleanup/content clear/form close happen only after verified selected transition; task-owned cleanup never touches source PE/other map groups. Local cleanup failure after confirmed transition is separately recoverable and must not resend remote effects.
8. Given completed implementation, pure patch/command/branch tests and actual authorized RT Plan/child/read-back/host UI/partial restart scenarios prove the effect matrix, no source/SpatialUnit writes and both branch outcomes. Preserve predecessors/limits and baseline RT8.8 behavior in operational path. Obtain C then parent scoped acceptance; no production release or live effects by preparation.

## Tasks / Subtasks

- [ ] Confirm explicit authorization/dependencies and exact current Plan/version/child/branch/read-back policy. (AC1-8)
- [ ] Compose stage/source/current Plan loading and parent-document tolerance without accepted-source fallback. (AC1-2)
- [ ] Implement typed Local Save/RT Save/Save & Close effect plans through13.9. (AC3-4)
- [ ] Review/implement current-child template mapping and explicit branch resolution with idempotency evidence. (AC5-6)
- [ ] Implement partial recovery and separately tracked local refresh/cleanup/close. (AC7)
- [ ] Run actual pure/host/sandbox effect matrix, review and obtain C/13.10 acceptance. (AC8)

## Developer Context And File Map

Future NEW Toolbox/Presets/Rt coordinator/patch/effect plan/viewmodel and tests. Reference RtExaminationModels/InnolaRtExaminationService after full source/tests read: current helpers fetch latest/fallback source and SaveAsync loops current Plans, modifies neighbors/checks/observations and may complete in one method. New adapter binds unique current Plan, declared patch/preserved fields and13.9 durable steps, not wholesale SaveAsync wrapper. Unknown-role default and old mutable source snapshots are not identity/authority proof.

Actual code uses Innola objects/create with Neighbor/PlanCheck template markers, existing plan_check_type_general-compatible behavior and two completion labels/aliases; exact endpoint/response/field/version protections must be verified on authorized sandbox, not assumed from source. Preserve valid keys without new server enums/fields, and never copy original PE children. Exact WPF/service/harness UPDATE targets enumerated/read/reviewed before coding, settings/DAML/operational dispatch unchanged by default; F07 if installed.

## Test Requirements And Completion Evidence

Any main type/correct stage, wrong task/fullidentity, multiple current Plans, unknown role, partial/no usable parent documents, readonly SpatialUnit/source refs, empty Neighbor/PlanCheck template plus unknown response, Plan version conflict, Save byte-equivalent checks/unrelated fields/no transition, both selected branches, exactly one intended check, create/check/transition timeout, confirmed neighbor retry, revoked assignment, cleanup failure after confirmed completion and no whole-command replay. Parent lanes preserve baseline158/known207-stop/default-PointIds. Actual service tests/field/version evidence separate from mocks; no tests/effects now.

## References

- [Parent](13-10-compose-plan-annexation-and-rt-toolbox-presets.md), [13.9 ledger](13-9c-define-handoff-ledger-attachments-and-task-transition.md), [S08](../../planning-artifacts/toolbox/toolbox-scenario-walkthroughs.md#s08-rt-save-versus-save--close), [task/reference decisions](../../planning-artifacts/toolbox/toolbox-decision-register.md).
- [Project RT rules](../../project-context.md), [review record](13-10-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime/UI/read/host/sandbox authorization false; all tasks unchecked. No RT load/save/child/PlanCheck/transition/cleanup/UI code/effects/tests, registration, installation, commit or deployment. Actual mapping/receipt/version evidence remains future gate.
