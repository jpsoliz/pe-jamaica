# Toolbox Review Policy Settings Mapping

Date: 2026-10-08
Status: proposed technical design; approved classification C15 retained; no runtime integration
Implementation follow-up: strict local-test loader/resolver added only in the isolated worktree; [contract review and exact local-v1 contract](../../implementation-artifacts/toolbox/toolbox-policy-contract-review-and-implementation.md). Production activation and the 0.1.0 design seed remain unsupported.
Scope: isolated Toolbox Compute/Compare only; supports 13.4A and 13.6-13.9

## Boundary And Deliverables

The [approved matrix](toolbox-compute-compare-checklist-matrix.md#proposed-stage-classification) is the product baseline: 42 items, Compute 39 required/2 optional/1 not applicable, Compare 22 required/16 optional/4 not applicable. These defaults are conditional, not unconditional field requirements.

The [machine-readable seed](settings-design/ToolboxReviewPolicy.design.json) preserves every item, stage default and approved applicability note. It is a draft design artifact, not a deployable policy or a file read by the application. Empty bindings deliberately prevent activation. The classification is approved; this settings contract and its detailed bindings still need architecture/domain review.

Files stay under planning artifacts, mirrored into the isolated worktree. Do not add them to the application project, output-copy rules, DAML, current Settings tab, active settings resolver or legacy loaders. No live service calls, FGDB writes, Enterprise effects or installation changes are part of this design.

## Configuration Separation

| Concern | Proposed owner | What it controls | What it must not imply |
|---|---|---|---|
| Tool availability | Toolbox capability registry | Enabled tools and adapter versions | A disabled tool makes its review obligation optional or N/A |
| Review classification | Versioned ToolboxReviewPolicy.json | Required/optional/N/A per stage and approved applicability | Missing field means automatic failure/block |
| Evaluator parameters | Explicit isolated adapter configuration snapshot | Tolerances, layer aliases, supported profiles | Current global settings can silently change a pinned review |
| Outcome effects | Versioned item/subcheck effect bindings | Disposition, report-only, geometry-consumer prerequisites | Stage forwarding means valid geometry |
| Case review state | Case revision/evidence store | Presence, accuracy, findings, reasons, actor/history | Examiner changes global policy by changing a case outcome |
| Remote authority | Verified actual-task/authorization adapter | Permission for a named external action | Local task selection or settings grants server authority |

Future runtime layout: a dedicated Toolbox settings root selected explicitly by an isolated bootstrap; capability configuration and a policies directory containing immutable policy versions. No implicit search in the legacy active settings root. The case manifest pins policy ID/version/content hash and the sanitized evaluator binding/parameter snapshot. This layout is a design recommendation, not permission to deploy a second add-in.

## Seed Contract And Runtime Contract

| Field | Seed representation | Production obligation |
|---|---|---|
| schema_version | 0.1.0 | Explicit supported parser contract; reject incompatible versions |
| policy_id / policy_version | Stable ID / draft version | Immutable, unique approved version; content hash validated |
| activation_status | draft_not_activatable | Only separately validated/published policies can activate |
| classification_baseline | C15 approval provenance | Preserve exact approved baseline; technical publication is separate |
| supported_stages | Compute, Compare | Reject unsupported stages, never inherit Compute for RT/Annex |
| preset_overrides | Empty | Explicit versioned selectors and changes; no examiner downgrade |
| item_id / display_name | M01-M42 / labels | Stable item IDs, unique rows; labels never serve as identity |
| stage_defaults | required, optional, not_applicable | Exactly one default for each supported stage |
| approved_applicability_note | Approved matrix text | Traceability only; do not execute natural-language text |
| applicability_bindings | Empty | Typed scope/subcheck predicates and approved resolution rules |
| evidence_bindings | Empty | Explicit adapter/field/version references and supported scopes |
| effect_bindings | Empty | Explicit outcome/origin/disposition effects; no implicit defaults |
| binding_status | pending_design_resolution | Complete validated bindings required before publication |

The seed is intentionally not a production schema instance. Future production schema requires complete typed bindings, profile coverage, approval/publication provenance and integrity metadata. Empty arrays must not mean "all applicable" or "no effect". A downstream developer must not infer a runnable policy from the approved classification alone.

## Applicability And Resolution

1. Load the explicitly selected policy; validate schema, unique IDs, all 42 baseline items, references, stages and published version/hash. No silent default policy on parse, load or compatibility failure.
2. Resolve selected local stage independently of the actual Innola task. Check supported preset/document/geometry recipe scope; unknown scope remains unresolved, not N/A.
3. Apply at most one explicit preset override. Ambiguous matching overrides are configuration errors, not last-write-wins. Changes to approved defaults/conditions require a new approved policy version.
4. Expand items into scoped obligations: case, document/section, parcel, participant, boundary or identifier pair. Global evidence is stored once and linked; iterate all relevant included parcels and all participants/pairs, not the first displayed row.
5. Evaluate typed applicability using captured case facts. True produces the configured classification; false yields N/A with predicate/evidence/reason. Unknown yields unresolved applicability and prevents claiming complete coverage. Missing value, missing API field and service failure are not false applicability.
6. Attach current evaluator results and human dispositions. Store effective classification, policy/version/hash, selected stage, scope, condition evidence and input fingerprint per obligation.
7. Calculate review completeness, geometry suitability and remote readiness separately. Save partial progress even when publication, completion or handoff is unavailable.

Predicate vocabulary should be small and typed: all/any/not, equals, in, exists and numeric comparison over an allowlisted fact catalog. No scripts, arbitrary expressions or remote queries inside settings. Facts distinguish source role, document/memorandum applicability, geometry recipe, parcel membership, comparison purpose and counterpart capability. Presence/availability facts must not be used to erase a required comparison. Human applicability confirmation includes actor/time/evidence; unknown does not default false.

Subchecks are essential: M14 can require duplicate-edge assessment for one parcel while cross-parcel sharing is N/A. M10/M11 depend on point/segment recipe. M31/M37 may be N/A for an independent Compare evaluator while inherited findings remain mandatory under M41. Parent row labels cannot suppress required child checks.

## Outcome And Effect Mapping

Keep separate axes:
- Presence: present, missing_from_source, not_captured, uncertain.
- Accuracy: confirmed, corrected, discrepant, not_reviewed.
- Check outcome: passed, failed, not_evaluable, not_run.
- Applicability: applicable, not_applicable, unresolved.
- Origin: extraction_mismatch, source_defect, mixed, unknown.
- Disposition: unresolved, corrected_and_rechecked, recorded_source_defect, deferred_to_compare, pending_with_reason.

These are proposed logical enum names, not existing runtime fields. Do not collapse them into one status string.

| Situation | Stage treatment | Independent constraints |
|---|---|---|
| Required source value is human-confirmed absent | Record missing-from-source finding and coverage | Absence is not pass; source-defect forwarding requires evidence/reason |
| Correct source, wrong extraction | Correct captured value and recheck affected obligations | Unfixed extraction mismatch cannot use source-defect forwarding |
| Both source and extraction wrong | Correct extraction component, retain source defect | Preserve original evidence/history |
| Recorded underlying source defect | Normal next-stage forwarding after full applicable examination | Do not approve invalid geometry or invent required artifacts |
| Required LTD evidence incomplete/unavailable | Review available records; Compare pending with reason | No final approval/rejection or implicit server transition |
| Optional recheck not run | No duplicate-examination obligation solely for that action | Consumed results still require human review; inherited findings remain visible |
| Geometry-related tool/service failure | Preserve partial work and diagnose technical failure | Not a source-defect waiver, not proof of valid geometry |
| Wrong case, invalid references, stale evidence or unknown authority | Deny affected completion/mutation with scoped reason | Cannot be waived by a settings toggle or free-text justification |

Severity is presentation/prioritization; workflow effect governs the named action. "Required" governs review coverage. A report-only failure remains visible and does not block solely because it failed. A required human disposition may still need completion even when its automated evaluator is report-only. Disabling an evaluator leaves required coverage outstanding or invokes an explicitly supported manual alternative; it does not clear findings.

## Existing Configuration Reuse

| Items | Existing mechanics / settings | Isolated mapping approach |
|---|---|---|
| M01-M02 | detected_profile_presence/complete, required_source_roles, source_file_integrity | Split source-document absence from wrong identity/corrupt local package; never copy all legacy blocker effects unchanged |
| M03 | workflow_rule_resolution | Automated route prerequisite only; explicitly manual route differs |
| M04 | dwg_signature_check, dwg_readiness_probe, dwg_required_cad_layers | Apply to declared DWG use/profile, not every PDF case |
| M05 | SDK/workspace/Python rules | Scope prerequisites to selected tool; no Python gate on unrelated manual/legal action |
| M08, M12-M14 | Python validation profiles and C# geometry validators | Reuse adapters/results and pinned parameters; preflight counts are not full closure validation |
| M15-M16 | Georeference rules, parish evaluators and legal record fields | Distinct geometry and record checks; no guessed pass without evidence |
| M17 | Existing manual review/reconstruction route | Alternative Compute method; unavailable in Compare; not a second mandatory construction |
| M18-M30, M40 | Review JSON fields, participant projection, legal/title services | Field/source bindings with original evidence, human validation and Compare-local corrections |
| M31-M37 | pxa_memorandum_* rules | Preserve applicability and configured effects through explicit bindings; north-arrow/scale/proximity report-only does not become absence blocker |
| M38-M39 | Legal/fiscal/Enterprise/spatial services | Compare evidence bindings, actual supported counterpart fields and explicit legal/fiscal labels |
| M41-M42 | Separate findings/reports/drafts/disposition services | Add cumulative coverage and new stage outcomes; no automatic legacy status translation |

M06/M07/M09 remain explicit coverage/readability/parcel-scope obligations supported by review models and relevant validators. An existing adapter ID can serve several items; each scoped obligation retains its own provenance. One item can need multiple automated and human checks.

Sources: [StructureRules.json](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/StructureRules.json), [settings document](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs), [settings service](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs), [matrix source index](toolbox-compute-compare-checklist-matrix.md#evidence-and-verification).

Reuse pure evaluation and JSON parsing patterns. Do not invoke the existing settings Save operation for Toolbox policy editing: it writes active baseline settings/rules. Do not inherit legacy loaders' silent fallback behavior for review-policy activation. Tool-specific parameters may be imported only through explicit isolated copies with origin/hash; no credentials, tokens or endpoint secrets in case snapshots.

## Case Pinning And Policy Changes

Pin exact resolved policy and evaluator configuration when review starts; store an immutable sanitized snapshot with provenance in the future case evidence/package contract. The same review must reproduce after restart or transfer even if the global default changes.

A newly published policy applies to newly opened reviews unless an authorized explicit migration is performed. Migration records old/new policy/hash, actor/time/reason, diffs and affected obligations. Re-evaluate affected evidence and stale dependent decisions; retain old records. No automatic retroactive downgrade, deletion or approval. Reference source cases retain their own policy provenance.

Tabular corrections in Compare stale only dependent comparisons/findings. Geometry edits remain Compute-only and invalidate consumers that depend on the changed geometry. A settings change cannot grant geometry write access in Compare or authorize remote operations.

## Future Settings Surface

Toolbox-only "Review Policy" view: policy/version/publication state; item grid with Compute/Compare classification; applicability/subcheck details; evidence adapters and effects; validation diagnostics and a version diff. Examiner view is read-only policy plus per-case outcome/reason controls. Authorized policy administration edits drafts, validates and publishes a new version, not the live file in place.

Keep tool enabled toggles separate from classification selectors. N/A requires scoped applicability evidence; it is not a "skip required item" checkbox. Show unsupported presets explicitly. Final stage and remote action permissions come from code/verified authority, not editable policy options.

No UI or administrator-role enforcement is implemented here; actual policy publication authority remains a design/authorization gate.

## Verification And Delivery Order

Design checks now: seed parses as JSON; exactly 42 unique IDs; defaults and applicability notes match the approved matrix; mirrored documents identical; local links resolve; no tracked application/settings changes.

Future isolated implementation:
1. Pure policy records, strict parser/schema validator and deterministic applicability resolver. Tests first; no production hooks or UI.
2. Pin/snapshot policy with case revision contract (13.5); bind scoped obligations and evidence/dispositions (13.4A/13.6-13.8).
3. Explicit adapters for existing evaluators; fixtures prove baseline behavior unchanged and Toolbox effects correctly separated.
4. Add Toolbox-only settings view/publication workflow after authorization review. Remote handoff remains 13.9, with fresh server authority checks.

Required tests: bad JSON/version/duplicate IDs/missing stage/unbound rules; ambiguous presets; unknown applicability; single and multi-parcel subchecks; all owners and volume/folio pairs; PDF-only and manual cases; missing-from-source versus extraction mismatch; mixed errors; source-defect forwarding; report-only required review; missing LTD data pending; optional consumed-result review; Compare-local tabular correction; read-only geometry; policy migration/staleness; baseline configuration never read implicitly or written.

D04/D12 classification approval is resolved by C15. Remaining gates are exact profile/subcheck bindings, evaluator parameters/effects, permitted Compute-to-Compare tabular deferrals and publication authority. PLA_A/PLA_B/RT require their own approved catalogs, not inferred defaults. This design neither implements settings nor marks a story ready/done.
