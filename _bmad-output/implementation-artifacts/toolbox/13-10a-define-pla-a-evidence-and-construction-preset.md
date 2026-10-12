---
story_key: 13-10a-define-pla-a-evidence-and-construction-preset
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-10-compose-plan-annexation-and-rt-toolbox-presets
depends_on: [13-5-add-versioned-toolbox-workspace-persistence, 13-6-register-reusable-toolbox-capabilities, 13-7-compose-compute-toolbox-preset, 13-9-add-revision-consistent-toolbox-handoff]
execution_gate: explicit-runtime-host-provider-scope-and-reviewed-pla-permission-profile-owner
implementation_authorized: false
---

# 13.10A - PLA_A Evidence And Construction Preset

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Existing 13.10 entry tracks this child, no new key.

## Story

As a plan-annexation examiner, I want selected-page evidence, reviewed geometry and annex outputs with explicit unreferenced status, so that local-origin construction is not mistaken for verified cadastral georeferencing.

## Scope And Dependencies

Requires accepted owners/capabilities/Compute reuse and affected13.9 artifacts/ledger, reviewed PLA_A source/profile/permission/output policy and explicit runtime/UI/host/provider/remote scope as applicable. Owns current-case PLA_A composition and compact evidence/review/visual match workflow. Does not depend on B/C preset code. [Parent](13-10-compose-plan-annexation-and-rt-toolbox-presets.md) owns task boundaries; no PE/PXA route substitution or source PE mutation.

## Acceptance Criteria

1. Given distinct PLA input st_plan_annexation_pdf, inventory full current case/source hash and examiner-selected page/area. Preserve image-only/multi-page input, original bytes/page coordinates and owned generated evidence. Full-page selection is explicit, not default first page; use selected PDF when practical with labeled PNG fallback. Stale selection/crop/source invalidates downstream candidate/artifact bindings.
2. Given PLA_A local geometry action, require separately reviewed versioned preset/profile/authority adapter before enabling it. Current Toolbox Compute-only GeometryAccess and Compute/Compare policy resolver stay unchanged until an explicitly reviewed scoped extension is implemented/tested; do not impersonate Compute or silently widen A schema/policy/hash contracts. Unknown profile/effect/authority blocks action, not a guessed legal permission.
3. Given automatic/assisted/manual preparation, reuse13.6/13.7 service seams with owned draft output and accepted stable map/reconciliation. Extraction/solver cannot approve candidates or replace manual/history silently. Optional provider requires explicit reviewed effect authorization; absent provider permits declared manual/local choices but never claimed automatic success.
4. Given reviewed dimensions without usable geographic anchor, allow profile-permitted form-valid local-origin geometry and record local frame/units/unreferenced reason. Do not block solely because georeference is missing or assign universal CRS. Dimension/coverage/identity/permission checks still apply. Visual similarity/overlay is source-form evidence, not survey accuracy/authoritative promotion. Unsupported publication profile blocks geometry publish independently of valid local work.
5. Given annex outputs, policy-bound required documents/source types/counts, selected evidence and review/finding state come from one verified owner revision. Existing two-document legacy output behavior is evidence, not universal v2 task policy. Render/local Save and external attach/transition are distinct13.9 actions with exact bytes and receipts; response loss cannot trigger whole-finalize resend.
6. Given UI/state/reopen, compact source-page/candidate/tabular/geometry/visual review shows provenance, pending/stale status and dirty work. Map/edit/cancellation use accepted owner and SDK-safe seams; no remote task change by preset selection. Exact durable selection/annex/profile fields need reviewed storage representation, not an A v1 expansion.
7. Given completion, pure/model/UI plus actual selected-page/local-origin/owner/host and conditional provider/sandbox handoff fixtures pass, preserving original sources and predecessor lanes/limits. Record exact accepted profile/extensions/enabled outputs; obtain A scoped acceptance, no georeference/production guarantee.

## Tasks / Subtasks

- [ ] Confirm authorization, accepted dependencies and exact PLA permission/profile/source/output mappings. (AC1-7)
- [ ] Review/implement scoped PLA profile/access adapter with required regression protection. (AC2)
- [ ] Compose selected evidence/candidate/manual/solver/reconciliation workflow. (AC1,3)
- [ ] Implement permitted local-origin and visual-only comparison semantics. (AC4)
- [ ] Wire compact review/owner save and13.9 exact annex artifact handoff. (AC5-6)
- [ ] Run separate lanes, review and obtain A acceptance. (AC7)

## Developer Context And File Map

Future NEW Toolbox/Presets/PlaA composition/profile/viewmodel and tests. Reuse PlaPlanEvidenceSelectionService/renderers, extraction/manual/solver and PlaVisualComparisonService after full source/read/effect review. PlaFinalizeService performs uploads; never use it as a local render/approval command. Context/policy/registry changes needed for PLA permission must be enumerated/read and reviewed as explicit extension, with locked A/Compute regression evidence. Exact WPF/DAML UPDATE targets only after isolation review; F07 if installed, no operational settings/source dispatch rewrite by default.

## Test Requirements And Completion Evidence

Parent preset lanes plus page-not-first, invalid extent, image-only/PDF fallback, source hash change, manual override, no anchor/local-origin, unreferenced target incompatibility, visual match not accuracy, currentpermission denial, unknown profile, incomplete annex bytes and partial attachment retry. Baseline158/known207-stop/default-PointIds retained. Actual profile/permission extension and UI/host tests are future evidence, no tests run now.

## References

- [Parent](13-10-compose-plan-annexation-and-rt-toolbox-presets.md), [Compute](13-7-compose-compute-toolbox-preset.md), [13.6](13-6-register-reusable-toolbox-capabilities.md), [handoff](13-9-add-revision-consistent-toolbox-handoff.md).
- [S06](../../planning-artifacts/toolbox/toolbox-scenario-walkthroughs.md#s06-pla_a-selected-evidence-and-unreferenced-geometry), [project rules](../../project-context.md), [review record](13-10-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime authorization false; tasks unchecked. No profile widening, page render/candidate/geometry/annex/UI/code/effects/tests, installation, commit or deployment. Record actual profile/access and runtime evidence later.
