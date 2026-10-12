---
story_key: 13-10-compose-plan-annexation-and-rt-toolbox-presets
track: toolbox
story_type: code
created: 2026-10-11
depends_on: [13-5-add-versioned-toolbox-workspace-persistence, 13-6-register-reusable-toolbox-capabilities, 13-9-add-revision-consistent-toolbox-handoff]
execution_gate: explicit-runtime-host-sandbox-authorization-and-reviewed-task-profile-reference-effect-contracts
implementation_authorized: false
baseline_branch: refactor/toolbox-case-workspace
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.10 - Compose Plan Annexation And RT Toolbox Presets

Status: backlog
Specification: preparation and static review complete under continuing documentation-only/no-coding instruction. Existing 13.10 backlog key retained; no child keys, runtime or task/service acceptance implied.

## Story

As an examiner, I want separate PLA_A, PLA_B and RT presets composed from shared capabilities and revision-consistent handoff, so that task-specific evidence and current-case actions never overwrite originating PE data or confuse Save with completion.

## Ordered Implementation Stories

Retain proposed architecture A/B/C identities. These are independent preset slices over shared accepted foundations; A -> B -> C is the documentation/delivery order, not a requirement that RT mutate or rebuild PLA/PE state.

| Story | Owns | Runtime dependency |
|---|---|---|
| [13.10A PLA_A Evidence And Construction Preset](13-10a-define-pla-a-evidence-and-construction-preset.md) | Selected plan/page, reviewed current-case candidates, explicit unreferenced geometry and annex outputs | Accepted capability/owner/Compute reuse, reviewed PLA permission/profile and explicit host/provider scope |
| [13.10B PLA_B Read-Only PE Reference And Annex Evidence Preset](13-10b-define-pla-b-read-only-pe-reference-and-annex-evidence-preset.md) | Verified PE reference/current-case evidence and distinct crop/attachment/task flow | Accepted owner/reference/handoff, D08/D10/task gate and explicit read/UI/sandbox scope |
| [13.10C RT Current-Plan Save And Branch Completion Preset](13-10c-define-rt-current-plan-save-and-branch-completion-preset.md) | Read-only PE context, current RT Plan neighbors, distinct Save/Save & Close plans | Accepted owner/handoff, actual Plan/child/branch mapping/read-back and explicit sandbox scope |

Local/pure preset preparation and sandbox remote-effect acceptance are separate. Reuse13.9 ledger/authority/receipt/recovery contracts for every attachment/Plan/check/task action; do not build a second remote ledger. F03-F06 apply to required owner/remote capabilities, F07 before installation. Named proof effects themselves need separately authorized bounded runners. No effects authorized by these specifications.

## Scope And Preserved Task Semantics

Presets select local work; actual task/environment/current/source Plan/TR/assignment are separately verified before remote effects. Keep local RemoteActionAccess denial and source-reference read-only. Current Toolbox GeometryAccess and review policy resolver support Compute-only editing/Compute-Compare policies: PLA_A needs a separately reviewed, explicitly permissioned profile/adapter before local geometry enabled, not pretending selected PLA is Compute or silently changing A schema/policy hashes. PLA_B/RT never receive source geometry permission.

PLA_A is a distinct plan-annexation source profile with st_plan_annexation_pdf. Examiner selects evidence page/area, defaulting only to explicitly chosen full-page representation with room for reviewed rectangle metadata. Reuse OCR/text/manual segment/solver mechanics; candidates remain unapproved until human review. No georeference is not by itself a reason to block a permitted PLA_A form-valid local-origin construction: label unreferenced/profile/units explicitly. Visual similarity is not survey-accurate georeferencing or cadastral promotion. Actual geometry publication requires compatible independently approved target profile, never arbitrary local-origin CRS conversion.

PLA_B uses distinct current task and originating PE context. Resolve PE- prefixed number using existing reviewed normalization with leading zeros/original value retained, then bind full source identity/revision. Legacy working_review/output recovery is evidence only until read-back proves a named immutable source snapshot. Keep feature-dataset/standalone classes and root rasters, including mgeo_overlay_* raster with existing 70% display transparency, separate from editable current evidence. No PLA_A re-extraction/solver/survey-diagram-crop/finalize UX for linked PE. Shared Supporting Documents crop uses copied PDF/PNG/JPG/TIFF source coordinates, never WebView/screen screenshots; user chooses DPI defaulting to 300. Save PNG first through owner to working/pla_b/survey_diagram_selection.png, retaining immutable revision payload, then separately attach only to Current TR with configured st_plan_annex_image through13.9.

RT is stage-driven: eligible In RT Examination work is not gated by main transaction type. Parent/main supporting documents feed review, while writes target only the uniquely verified current RT/main Plan. Partial parent attachment preload retains successes/warnings and fails only if no usable source document remains; do not weaken other transaction loaders. PE SpatialUnits/geometry and all SpatialUnit attributes are read-only; local summaries may show parcel_name/area_sqr/suid/created_utc without writeback.

Commands are distinct: Local Save stores a draft under accepted owner; RT Save explicitly writes current Plan neighbors only, preserves checkList and does not update PlanCheck/SpatialUnit or transition. RT Save & Close saves the same neighbors, updates/appends exactly one intended current-Plan check row using valid plan_check_type_* with passed=true, performs the examiner-selected verified transition, then refreshes/cleans/closes only after confirmed completion. That passed flag is RT process-check evidence, not valid geometry or a universal approved decision. New child templates come from Innola for current target, never fabricated local IDs or original linked-PE children.

The examiner chooses Yes, proceed next -> Review Completed RT Examination or No, prepare pre-check log sheet -> Prepare Plan Pre-Check Log Sheet. Labels are evidence/mapping inputs, not invented definition IDs; actual available transition and gateway aliases must match explicitly. No branch/default/first transition fallback. Partial Plan success plus failed/ambiguous transition retains confirmed evidence and UI state; no whole-command replay, duplicate PlanCheck or premature cleanup.

## Acceptance Criteria

1. Separate preset contracts trace exact source/current identity, scope, allowed effects, profile/policy/capability versions and owner/handoff inputs; unsupported binding/permission cannot be enabled by selection.
2. PLA_A preserves selected evidence/raw/manual history, reviewed candidate identity and explicit local-origin geometry/visual-match limitations. Geometry approval/handoff reflects actual profile, not a false georeference guarantee.
3. PLA_B/RT preserve read-only PE geometry/source snapshots and provenance while current-case evidence changes are separately owned. Missing/mismatched/partial source proof blocks dependent acceptance without fabricated replacement PE geometry.
4. Crop/render/local Save, server attachment, current RT neighbor write, PlanCheck and transition are separately gated outcomes, pinned to one owner revision through13.9. Saved permissions/task/boolean upload success cannot certify remote completion.
5. RT retains stage-driven eligibility/parent-document tolerance, no SpatialUnit writes, exact Save/Save & Close difference and chosen branch with one valid intended PlanCheck row, preserving unrelated existing children/fields.
6. Current Plan/source/remote version/concurrent operator conflicts fail closed. Child-create/upload/transition response loss remains ambiguous until actual13.9 F06 reconciliation; no retry that can duplicate confirmed steps or copy source PE children into current target.
7. WPF/pure/owner/host/conditional provider/live sandbox tests prove all three task flows and partial restart/authority/source-denial cases, preserving baseline158/applicable predecessors/known207-stop/default-PointIds. Independent GIS/task review and scoped13.10 acceptance required; no production/remote effects or operational11.2 closure by preparation.

## Tasks / Subtasks

- [ ] Confirm exact runtime/host/read/provider/sandbox scopes and reviewed profile/reference/task/effect/owner mappings. (AC1-7)
- [ ] Implement/review A current-case evidence/construction and local-origin policy adapter. (AC1-2)
- [ ] Implement/review B immutable PE/current annex evidence and ledger attachments. (AC3-4)
- [ ] Implement/review C current RT Plan/child/branch commands through13.9. (AC4-6)
- [ ] Report actual lanes/limits and obtain child then scoped13.10 acceptance. (AC7)

## Developer Context And File Map

Future NEW task composition/viewmodels under Workflow/Toolbox/Presets/ plus adapters and tests. Existing PLA selection/render/visual/crop/finalize, PE normalization/read/map and RT load/save/template/branch helpers are reuse candidates only after full call-path/side-effect review. Current wrappers mix file/network/map/lifecycle effects; do not invoke wholesale. Exact UPDATE set read/reviewed before wiring, preserve baseline source/settings/DAML/case registration; F07 for installation.

RT legacy load may choose latest SpatialUnits/fallback working_review/local PE artifacts and Save loops Plan objects. New Toolbox context requires explicit uniquely bound current Plan and immutable PE proof, not first/blanket Plan selection or fabricated accepted source. Strictly separate editable current neighbors from reference roles; unknown role cannot silently default Owner. Exact Plan payload preserving server shape/version/unknown fields and child semantics is a future actual service gate.

Future Program.cs named tests preserve filtering/fail-fast after full read. Use SDK3.6/net8.0-windows/x64 existing harness and parent capability commands with obj-toolbox-task-presets/. Actual host/provider/sandbox invocations/endpoints/credentials and schema/limits recorded only when authorized. No new GIS/rule/extraction engine, A v1 extension or legacy whole-task rewrite by default.

## Test Requirements And Completion Evidence

PLA multi-page/image-only/page selection, no georeference/local-origin, visual-only match; PE leading-zero/ambiguous/missing/wrong revision, nested/root dataset/raster load and no source modification; standalone crop versus attachment; RT any main type/correct stage, parent partial/no usable attachment, unknown party role, current Plan ambiguity, empty child arrays/templates, Save preserves checks/no transition, both Save & Close branches/one PlanCheck, neighbor success+transition timeout/restart, revoked assignment/no cleanup. No runtime/provider/UI/host/service tests performed now.

## References

- [S06-S08 scenarios](../../planning-artifacts/toolbox/toolbox-scenario-walkthroughs.md), [process](../../planning-artifacts/toolbox/toolbox-process-definition.md), [capability inventory](../../planning-artifacts/toolbox/toolbox-capability-inventory.md), [task/reference decisions](../../planning-artifacts/toolbox/toolbox-decision-register.md), [F03-F07](../../planning-artifacts/toolbox/toolbox-acceptance-strategy.md).
- [13.5](13-5-add-versioned-toolbox-workspace-persistence.md), [13.6](13-6-register-reusable-toolbox-capabilities.md), [Compute](13-7-compose-compute-toolbox-preset.md), [handoff](13-9-add-revision-consistent-toolbox-handoff.md), [project rules](../../project-context.md), [review record](13-10-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. No runtime/host/sandbox permission granted; tasks unchecked. No presets/UI/profile widening, source loads, geometry/crop/save/upload/Plan/check/task code/effects, tests, installation, commit or deployment. Exact mappings/profile/authority/UPDATE evidence remains future work.
