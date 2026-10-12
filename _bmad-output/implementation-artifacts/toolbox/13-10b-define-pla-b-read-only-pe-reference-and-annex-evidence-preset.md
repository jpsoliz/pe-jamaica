---
story_key: 13-10b-define-pla-b-read-only-pe-reference-and-annex-evidence-preset
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-10-compose-plan-annexation-and-rt-toolbox-presets
depends_on: [13-5-add-versioned-toolbox-workspace-persistence, 13-6-register-reusable-toolbox-capabilities, 13-9-add-revision-consistent-toolbox-handoff]
execution_gate: explicit-runtime-read-host-sandbox-authorization-and-reviewed-reference-task-policy
implementation_authorized: false
---

# 13.10B - PLA_B Read-Only PE Reference And Annex Evidence Preset

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Existing 13.10 entry tracks this child, no new key.

## Story

As a PLA_B examiner, I want verified PE geometry/reference evidence and separate current-task annex artifacts, so that originating PE truth stays immutable while current evidence can be reviewed and attached.

## Scope And Dependencies

Accepted owner/capabilities/reference/handoff plus D08/D10 and actual task/read/crop/attachment mapping, explicit UI/host/read/sandbox scope required. Owns PLA_B current/source composition and standalone current-document crop/evidence commands; no A extraction/construction/profile dependency, PE geometry rebuild or PLA_A finalize UX. [Parent](13-10-compose-plan-annexation-and-rt-toolbox-presets.md) owns boundaries.

## Acceptance Criteria

1. Given current task and PeNumber, preserve raw value/leading zeros while applying reviewed PE- prefix normalization and resolving unique full source environment/transaction/case/revision. Multiple/missing/mismatched source blocks dependent acceptance; never choose first match or relabel source as current. Local task name/settings gate is discovery eligibility only, remote authority still13.9 actual instance proof.
2. Given PE local/package/Enterprise geometry, require owner-qualified named immutable source inventory/profile and read-back before accepted-reference claim. Legacy working_review scoped by number or numeric *_parcel_output.gdb is labeled evidence/partial until verified. No newest-GDB or rebuilt approved JSON fallback. Read-only geometry/source/files enforced by adapter/owner, not only map edit control.
3. Given source GDB, include standalone and feature-dataset feature classes and root raster inventory, with mgeo_overlay_* handled as raster and existing70% display transparency. Loading display is not source-write permission or complete schema/snapshot proof; unsupported/missing referenced raster remains scoped incomplete evidence.
4. Given shared standalone Supporting Documents crop for copied PDF/PNG/JPG/TIFF, export controlled source/page coordinates rather than WebView/screen screenshots, with user-selected DPI defaulting to 300. Save PNG first through accepted owner to working/pla_b/survey_diagram_selection.png, binding source hash/page/rectangle/transform/DPI/bytes/reviewer and exact revision; original/source PE unchanged. Retain immutable revision asset for prepared13.9 uploads so a later current-path crop cannot change frozen bytes. This is not PLA_A extraction/survey-diagram workflow or geometry construction. Crop/render, local Save and explicit attachment are separate commands; repeated attach uses13.9 exact receipt/recovery rather than overwrite/reupload.
5. Given current-task attachment/completion, require verified saved PNG and configured source/document type st_plan_annex_image, attached to Current TR only. Review exact content/required annex/package/inclusion and D08 reference policy; execute only13.9 ledger plan with fresh actual task/session and read-back. No currentgeometry publication from source PE, source Plan/PE child mutation, default transition or name-only authority. Missing source revision/mandatory evidence blocks dependent handoff without discarding partial review.
6. Given UI/cancellation/reopen, current task/reference provenance and scoped crop/document/geometry state remain distinct, parent original source hashes unchanged, stale source/current revision invalidates affected result and cleanup touches only owned current map groups after appropriate confirmation. Exact WPF/view/update/owner mappings require full read/review; no baseline case/registration replacement.
7. Given completion, pure reference/crop/task-gate tests and real read-only feature/raster/UI/owner plus authorized sandbox attachments/partial retry pass. Preserve predecessor lanes/limits and record exact reference/attachment policy/capability scope; obtain B acceptance without source PE or production mutation.

## Tasks / Subtasks

- [ ] Confirm authorization/dependencies and D08/D10 exact source/current/reference/task/effect policy. (AC1-7)
- [ ] Compose unique PE resolution and owner-qualified immutable geometry/reference read. (AC1-2)
- [ ] Verify complete feature/raster display handling without source writes. (AC3)
- [ ] Implement standalone current-case crop/save and13.9 receipt-qualified attachment commands. (AC4-5)
- [ ] Wire compact source/current UI/reopen/owned cleanup after exact update review. (AC6)
- [ ] Run separate pure/host/sandbox lanes and obtain B acceptance. (AC7)

## Developer Context And File Map

Future NEW Toolbox/Presets/PlaB coordinator/viewmodel/typed evidence commands and tests. Reuse PeNumberNormalizer, unique PE finder, map/read inventory and PlaBSupportingDocumentCropService after full call-path read; that crop service also uploads, so separate render/save/attach effects. Preserve existing baseline task/subworkflow gates without claiming verified server definition IDs. No PLA_A selection/extraction/finalize route, legacy source restore/write or currentgeometry clone-as-owner. Exact updates/targets/endpoints read/reviewed under authorization; F07 if installed.

## Test Requirements And Completion Evidence

Leading-zero PE prefix, multiple same-number sources, wrong environment/revision, missing accepted metadata, nested feature/root raster, mgeo_overlay raster display, original hashes unchanged, controlled source-coordinate crop versus screenshots, user DPI/default300, required PNG saved path before attach, missing/wrong st_plan_annex_image, Current TR versus PE target, later crop cannot change frozen upload, crop cancellation/stale source, ambiguous attachment, failed task after confirmed attach, task/subworkflow mismatch and no remote query/cleanup outside owned scope. Parent lanes preserve baseline158/known207-stop/default-PointIds; no runtime tests now.

## References

- [Parent](13-10-compose-plan-annexation-and-rt-toolbox-presets.md), [13.5](13-5-add-versioned-toolbox-workspace-persistence.md), [handoff](13-9-add-revision-consistent-toolbox-handoff.md), [S07](../../planning-artifacts/toolbox/toolbox-scenario-walkthroughs.md#s07-pla_b-related-pe-recovery).
- [Reference/task decisions](../../planning-artifacts/toolbox/toolbox-decision-register.md), [project rules](../../project-context.md), [review record](13-10-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime/read/host/sandbox authorization false; tasks unchecked. No PE load/crop/upload/task/UI code or effects/tests, source write, installation, commit or deployment. Record actual source/target proof later.
