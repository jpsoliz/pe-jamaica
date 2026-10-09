# Compute / Compare Review Checklist Matrix

Date: 2026-10-08
Status: source-based inventory with approved Compute/Compare classification v1; approved by JotaPe on 2026-10-08; runtime implementation pending
Track: Toolbox; supports 13.4A, 13.6-13.9
Baseline inspected: operational wipRC9, 5908f30; isolated 13.3 context retained
Verification: source/configuration/test-registration inspection only; no runtime tests or live LTD requests

## Reading This Matrix

Compute creates and human-reviews geometry and printed evidence. Compare verifies the evidence against LTD/Innola and external cadastral context, can correct current-case tabular values, and owns the final examination decision. Compare geometry remains read-only.

Reuse = observed implementation mechanics. Adapt = reuse with changed Toolbox scope/state/audit. Gap = target behavior not established by inspected code. Codes S01-S24 refer to linked sources below; source presence and registered tests are not runtime certification.

These are checklist item identities for planning, not new rule IDs, runtime configuration or mandatory flags. Evaluate applicability per case/document/parcel/profile. Do not require every memorandum field in a document with no memorandum or every CAD layer in a PDF-only case. D04/D12 still define the versioned applicability/required catalog; C11-C14 process choices are confirmed.

## Inventory Summary

- StructureRules.json contains 32 rules: supporting documents 3, structure 5, system prerequisites 5, georeference 4, dimension 2 and memorandum 13.
- Additional validation rules/profiles live in rules.yaml and C#/Python evaluators: closure, construction readiness, orientation/bearing consistency, parish, plan/computation-sheet consistency and printed text height.
- Compute review surfaces: General Info, Volume/Folio, Memorandum, Validation Findings, Participants, Boundary Segments and Points. Some visibility is PE/PXA/profile-specific, not universal.
- Shared review JSON already stores metadata, parties/representatives, adjacent owners, volume/folio, memorandum, points and segments with review/source fields.
- Compare already loads computed participants, searches legal records and title documents, gathers fiscal/Enterprise/spatial evidence, saves drafts and generates reports. This is not yet a per-field/per-parcel Toolbox checklist or cumulative finding register.

## A. Intake And Technical Usability

| ID / Item | Compute Examination | Compare Examination | Existing Evidence | Toolbox Treatment |
|---|---|---|---|---|
| M01 Current TR and source package | Verify identity, profile, attachments and copied sources | Confirm same case and inherited source/review inventory | S01 supporting_document rules; S02 manifest preflight | Adapt case/revision provenance; wrong identity is not a source-defect waiver |
| M02 Source completeness and readable files | Check expected source roles, supported types, readable case-relative paths | Review source pages needed for comparison; record missing evidence | S01 required_source_roles/source_file_integrity; S02 | Reuse checks; distinguish missing-source defect from unreadable/corrupt local artifact |
| M03 Workflow/script plan currency | Resolve configured extraction route and plan | Consume reviewed evidence, do not rerun extraction implicitly | S01 workflow_rule_resolution; S02 | Adapt reusable capability eligibility; transaction type does not dictate parcel count |
| M04 Optional DWG signature/layers | If applicable, inspect signature/readiness/points/lines/annotation | Use as labeled supporting reference | S01 dwg_signature_check/dwg_readiness_probe/dwg_required_cad_layers; S02 | Reuse configured layer aliases; no universal CAD-layer requirement |
| M05 Runtime/workspace prerequisites | SDK lane, access, Python health/package probes | Validate enabled comparison service prerequisites | S01 system rules; S02 | Reuse; tool/config failures are not defects in the survey plan |
| M06 Extraction coverage and source trace | Review draft output across applicable pages and parcels; correct mismatches | Keep access to source and reviewed evidence; classify new discrepancies | S03 routes; S04 model/persistence; S05 viewer | Adapt coverage and human signoff; extraction success/confidence is not approval |
| M07 Legibility and printed text size | Inspect readability and available printed-height evidence | Explain limits affecting comparison rather than invent missing values | S07 document.printed_text_height; S03 | Reuse evaluator when evidence exists; partial/manual review where unavailable, no assumed size measurement |
| M08 Embedded plan vs computation sheet | When both exist, review coordinate/distance/bearing/area consistency | Retain unresolved source contradictions in cumulative review | S07 pxa.embedded_compute_sheet_detected/pxa.plan_compute_sheet_consistency | Reuse evaluator; forward evidenced source defects, not unfixed extraction discrepancies |

## B. Geometry And Geographic Evidence

| ID / Item | Compute Examination | Compare Examination | Existing Evidence | Toolbox Treatment |
|---|---|---|---|---|
| M09 Parcel coverage and identity | Review every detected parcel/group and its inclusion disposition | Keep parcel scope for evidence/comparisons; no first-parcel-only conclusion | S04 ParcelGroupId/ParcelName; S05 filtering | Adapt stable maps/all-included examination; 11.2 and 13.4 foundations pending |
| M10 Point IDs, coordinates and sequence | Confirm presence, numeric coordinates, parcel assignment and order | Read geometry and note discrepancies; edits return to Compute | S06 ParcelScopedReviewValidationService; S04 | Reuse validation; preserve manual/source values and reviewer order |
| M11 Boundary segments and dimensions | Review from/to, bearing, distance, sequence and inclusion | Inspect read-only dimension evidence where needed | S05 grids; S06 solver/manual services; S07 | Reuse manual/solver inputs; membership identity and revision audit are adaptations |
| M12 Chain, closure and area coherence | Check connectivity, missing references, tolerance and source area | Retain source/geometry inconsistency findings; never silently repair | S06 closure/readiness; S07 profiles; S02 dimension summary | Reuse full validators; preflight presence/count checks alone are not closure validation |
| M13 Ring orientation and bearing-coordinate consistency | Evaluate whole ring and source-versus-derived bearings | Inspect outcomes; changed geometry invalidates comparisons | S06 orientation; S07 orientation profiles | Reuse; output normalization cannot change saved reviewer order |
| M14 Shared/duplicate edges and adjoining boundaries | Review diagnostic conflicts across parcels | Add boundary/adjacency discrepancies from references | S07 duplicate/shared-edge diagnostics; S12 spatial review | Adapt; current diagnostics do not prove stable physical-edge identity or complete edit-impact propagation (13.4B) |
| M15 Georeference, coordinate system and bounds | Review georeference source, Easting/Northing, JAD2001 evidence and Jamaica bounds | Inspect placement against references without editing geometry | S01 georeference rules; S02; S08 overlay | Reuse; configured validators/evidence limits remain explicit |
| M16 Parish/location consistency | Review printed parish and available spatial point/polygon boundary checks | Compare parish/location in system records as applicable | S07 parish rules; S09 legal record/search fields | Reuse evidence; metadata presence is not proof of spatial containment |
| M17 Manual COGO / geometry recovery | Use manual points/segments and reviewed boundary reconstruction when extraction is inadequate | Geometry corrections use Compute; continue tabular work in Compare | S06; S13 manual-mode entry | Reuse existing manual route; validate full bad-source/zero-extraction recovery separately. Not a claim that a new full ArcGIS COGO editor exists |

## C. Printed Tabular Values And LTD Comparison

| ID / Item | Compute Examination | Compare Examination | Existing Evidence | Toolbox Treatment |
|---|---|---|---|---|
| M18 Owner names and party roles | Capture/review printed names; distinguish owner/occupant/representative | Compare against legal/LTD party evidence; record discrepancies | S04 parties; S05 Participants; S09 legal/party fields; S10 | Reuse field/query logic; add field/parcel-scoped comparison trace |
| M19 Neighbors/adjacent owners | Review names, roles and related boundary/lot evidence | Compare against LTD and labeled fiscal/Enterprise neighbor context | S04 AdjacentOwners/RelatedSegmentFrom/To; S09; S10; S12 | Adapt links. Fiscal owner/taxpayer labels are not legal ownership proof |
| M20 Volume / folio | Review every printed pair and its participant/parcel association | Query matching records and optional title images for each relevant pair | S04 VolumeFolios; S05; S09; S11 | Reuse pair/title services; simple summary selects first pair, not a complete multi-pair checklist |
| M21 Lot/parcel/PID identifiers | Review printed lot and group labels, preserving their meaning | Compare parcel identifiers/system references without equating lot label with authoritative PID | S04 participant LotNumber/ParcelName; S09 ParcelId; S10 | Adapt explicit identifier mappings; no label-based identity merge |
| M22 Participant addresses | Review printed address; manual correction when needed | Compare available system party address evidence | S04/S05; S09 LegalCadasterPartyRecord.FullAddress | Reuse capture/search evidence; no inspected automatic per-address matcher |
| M23 Land valuation number | Review printed value, absent-source state or correction | Query available legal/fiscal evidence by configured source; verify association | S04 participant fields; S09 query modes/records | Reuse; source-specific query support and field maps matter |
| M24 Examination/file references | Review ExaminationNumber and file_reference evidence | Correlate with available records/docs; record unsupported lookup | S04; S03 file_reference; S09 displays | Reuse capture; no dedicated Exam No./file-reference matching engine established |
| M25 Property/estate name | Review property_name/surveyed_property_name and diagram evidence | Compare names where system record evidence actually provides them | S03; S14 memorandum rules; S09 | Reuse capture/manual review; do not assume a distinct LTD property-name endpoint |
| M26 Printed area and units | Review numeric value/unit and dimension coherence | Compare available legal/parcel context where mapped; distinguish source area from calculated area | S03/S04; S14 area; S07; S12 | Reuse geometry/source checks; generic legal record has no universal area field in inspected contract |
| M27 Survey date/method | Review survey_date/survey_method; do not confuse method with CRS | Retain as context; compare only if corresponding system evidence exists | S03/S04; S05 General Info | Reuse capture; not an automatic mandatory LTD comparison |
| M28 Surveyor/certification | Review surveyed_by, title, organization and certification evidence | Consider source certification findings in final review; system comparison only with actual evidence | S03/S04; S14 | Reuse memorandum review; no surveyor-registration verifier established |
| M29 Equipment/instrument evidence | Review instrument, instrument_check_date/result, GPS instrument/serial values | Carry evidence/findings; compare only against available corresponding records | S03 fields; S14 instrument group; S05 | Reuse capture/rules; equipment presence is not an LTD-verification pass |
| M30 Plan-check date and other printed metadata | Review plan_check_date and supported metadata; preserve unknown fields | Manual tabular capture/correction and evidence linkage where needed | S03; S04 RootMetadata/ReviewStatus/ReviewNotes | Adapt general field coverage; no guessed required items or new extraction schema |

## D. Memorandum And Map Evidence

| ID / Item | Compute Examination | Compare Examination | Existing Evidence | Toolbox Treatment |
|---|---|---|---|---|
| M31 Memorandum applicability | Confirm detected text/source evidence and applicability | Carry applicability/limitations; revisit source if needed | S14 detection/evaluator; S04; S05 Memorandum | Reuse detection; no memorandum must not automatically imply incomplete extraction or rule failure |
| M32 Survey made at instance of / surveyed-for parties | Review recorded names/roles | Correlate with participants where applicable, not assume requestor equals owner | S14 surveyed_for; S04 MemorandumParties | Reuse; explicit role/evidence associations |
| M33 Notices served and attendance | Review notice_served_on, appeared parties/representatives and explicit no-one-appeared | Carry findings; verify related participants when appropriate | S14; S04 memorandum semantic states | Reuse; negative attendance is valid evidence, not an empty field |
| M34 Objections and surveyor decision grounds | Review text, explicit None/N/A and source explanation | Retain/add discrepancies; final rejection includes cumulative source concerns | S03 grounds_of_objection/surveyor_decision_grounds; S14 | Reuse evidence/dispositions; do not infer legal resolution from extracted text |
| M35 North arrow | Confirm source presence/evidence; no inferred geometry rotation | Reference only; assess unresolved source defect if applicable | S03 north_arrow; S04; S14 report_only | Reuse presence rule, not a guarantee of directional correctness |
| M36 Scale bar / printed scale | Review presence and visible scale text | Retain source/reference evidence; note comparison limits | S03 scale_bar/text detection; S14 report_only | Reuse; scale presence alone is not georeferencing or measurement accuracy |
| M37 Property name near diagram | Human review of proximity/presence evidence | Carry source finding, not universal LTD field match | S14 property_name_near_parcel_diagram | Reuse report-only rule; distinguish from property identity matching |

## E. Comparison Evidence And Stage Outcomes

| ID / Item | Compute Examination | Compare Examination | Existing Evidence | Toolbox Treatment |
|---|---|---|---|---|
| M38 Legal/LTD query evidence | Prepare reviewed search values and sources | Search PID, volume/folio, LandVal or names; review returned records/parties, ambiguity and no-record diagnostics | S09 query contracts/services; S10; S11 | Reuse queries; no-record/ambiguity/unavailable are different states, not inferred pass/rejection |
| M39 Spatial overlap/fiscal/Enterprise context | Retain local geometry/source facts and relevant existing spatial evidence | Review overlays/neighbors/overlap evidence with explicit legal/fiscal/source labels | S12; S09; S16 working geometry | Reuse services; geometry stays read-only; Enterprise checkpoint is not final cadastre authority |
| M40 Human presence and accuracy confirmation | Review extracted/manual values against source; evidence for missing-from-source | Correct/capture current-case tabular evidence locally and review against LTD | S04/S05 editable Present, ReviewStatus, ReviewNotes; S15 Compare participant display | Adapt typed presence/accuracy/provenance. A dedicated universal accuracy field/checkbox is not established in inspected source |
| M41 Cumulative findings and full review coverage | Correct extraction errors; record source defects across every included parcel/applicable document section | Preserve Compute findings, add Compare findings, resolve with history and retain limitations | S17 finding dispositions; S18 separate Compute/Compare reports; S19 drafts/decisions | Gap cumulative stable finding register, item coverage and stage-forwarding versus geometry-validity separation |
| M42 Stage disposition and final decision | Forward recorded source defects normally after applicable examination; do not declare geometry valid | Review available data; pending-with-reason for incomplete required LTD comparison; approve/reject at completed Compare | S20 legacy Compute disposition; S19 legacy Compare approved/blocked/returned/saved-progress | Adapt new stage states. Do not map blocked to rejected or saved-progress to completed pending automatically |

## Proposed Stage Classification

Reviewed: 2026-10-08
Status: product classification v1 approved by JotaPe on 2026-10-08, including the documented applicability conditions; no runtime configuration changed. The section heading is retained for existing links.

R = Required examination/disposition when applicable. O = Optional independent action/verification. N/A = Not applicable to that stage's responsibility. Conditions below resolve each item to R/O/N/A per case; conditional R is still one of the three categories, not a fourth status.

Required means the examiner must assess and record the applicable outcome, not that a source value must exist, every automated check must pass, or the same computation must be repeated in Compare. Confirmed missing-from-source and recorded source defects remain valid dispositions under C11-C12. Unknown applicability requires a recorded decision, not silent N/A.

Unavailable required evidence is not N/A: review what is available and preserve outstanding work with reason. Under C14 incomplete required LTD comparison remains pending, not approved/rejected. Optional tool execution does not make human review optional for values/results actually consumed. An optional action can become required only by an explicit versioned case/profile requirement; explain that promotion.

Scope: these classifications govern examination activities, not mandatory source-field law, exception authority, Enterprise publication, packaging or remote transition permissions. Source completeness/integrity and stage examination have distinct outcomes. This review does not resolve D05/D10 or 11.2/identity/persistence dependencies.

| ID / Item | Compute | Compare | Applicability / Rationale |
|---|---|---|---|
| M01 Current TR and source package | R | R | Case/TR identity and source/revision scope are always checked. |
| M02 Source completeness and readable files | R | R | Assess completeness/readability; missing source is recorded, not silently N/A. Integrity failures remain technical blockers. |
| M03 Workflow/script plan currency | R | N/A | Compute route/plan currency is required for automated extraction; N/A for an explicitly manual-only route. Compare does not select extraction scripts. |
| M04 Optional DWG signature/layers | R | O | Compute required when DWG is supplied for use or required by profile; otherwise N/A. Compare optional DWG reference, N/A if irrelevant. |
| M05 Runtime/workspace prerequisites | R | R | Check prerequisites for the chosen tools only. Python/CAD checks are N/A to comparison-only tools that do not use them. |
| M06 Extraction coverage and source trace | R | R | Compute checks capture/manual coverage; Compare checks provenance and outstanding capture. No mandatory rerun of extraction. |
| M07 Legibility and printed text size | R | R | Assess legibility/limitations in both stages. Numeric text-height measurement is conditional on applicable policy/evidence; inability is recorded, not a guessed pass. |
| M08 Embedded plan vs computation sheet | R | O | Compute required if plan and computation-sheet dimensional evidence both apply; otherwise N/A. Compare optional independent recheck; inherited defects always reviewed under M41. |
| M09 Parcel coverage and identity | R | R | Every detected parcel/inclusion disposition must be accounted for; no active-parcel filter bypass. |
| M10 Point IDs, coordinates and sequence | R | O | Compute required for point-based geometry; explicit alternate segment recipe may make point-only checks N/A. Compare optional independent read-only recheck. |
| M11 Boundary segments and dimensions | R | O | Compute required for a boundary/dimension construction recipe; representation-specific subchecks may be N/A. Compare optional dimension recheck. |
| M12 Chain, closure and area coherence | R | O | Compute required construction/closure/area assessment; open-boundary policy determines applicable closure test. Compare may recheck, but carries known defects via M41. |
| M13 Ring orientation and bearing-coordinate consistency | R | O | Compute required orientation/consistency assessment where ring/source bearings apply; unavailable bearings are recorded, not passed. Compare optional recheck. |
| M14 Shared/duplicate edges and adjoining boundaries | R | O | Compute required duplicate/shared-boundary assessment when boundary data/adjacency applies; single-parcel cross-parcel subcheck N/A. Compare optional additional boundary analysis; known conflicts reviewed via M41. |
| M15 Georeference, coordinate system and bounds | R | R | Compute assesses georeference/CRS/bounds; Compare confirms placement/evidence suitability where comparison needs it, without rerunning/editing Compute geometry. Source-defect limitations remain visible. |
| M16 Parish/location consistency | R | R | Assess parish/location evidence and applicable source/system/spatial consistency. Missing printed parish is a finding, not an automatic pass. |
| M17 Manual COGO / geometry recovery | O | N/A | Manual COGO is an alternative method, not an obligatory second construction. Use it when needed and feasible; lack of source dimensions is recorded, not invented. Geometry edits never in Compare. |
| M18 Owner names and party roles | R | R | Assess source presence/accuracy and all relevant owner/party roles; compare available LTD evidence. No name means recorded absence, not omitted review. |
| M19 Neighbors/adjacent owners | R | R | Assess neighbors/adjacent roles and parcel links; distinguish legal ownership from fiscal/taxpayer context. |
| M20 Volume / folio | R | R | Review presence and all available volume/folio pairs. Compare relevant pairs or documented alternative keys; missing pair is not an invented query/pass. |
| M21 Lot/parcel/PID identifiers | R | R | Review lot/parcel identifiers and explicit system-key mapping; never substitute a lot label for verified PID. |
| M22 Participant addresses | R | R | Assess printed addresses; compare when address is part of applicable participant verification. N/A only for a genuinely non-applicable comparison, not unavailable required evidence. |
| M23 Land valuation number | R | R | Assess printed LandVal presence; compare when relevant to identity/reference matching. Field/source-specific comparison may be N/A with reason if not applicable. |
| M24 Examination/file references | R | O | Compute reviews examination/file reference presence. Compare correlation is optional unless the case policy requires that reference to establish identity. |
| M25 Property/estate name | R | R | Assess property-name evidence; compare when applicable system/source counterpart is part of identity review. No universal property-name endpoint is assumed. |
| M26 Printed area and units | R | O | Compute requires source-area/unit/coherence review as applicable. Compare independent area comparison is optional unless a case-specific required check consumes it. |
| M27 Survey date/method | R | O | Compute reviews printed survey date/method presence/accuracy. Compare independent date/method matching optional; no counterpart means N/A for matching only. |
| M28 Surveyor/certification | R | O | Compute reviews certification/surveyor evidence as applicable. Compare may independently verify against actual records; inherited certification defects remain required M41 review. |
| M29 Equipment/instrument evidence | R | O | Compute assesses equipment/instrument/check evidence and applicable memorandum fields. Compare equipment matching optional only with a real counterpart; otherwise N/A, while findings are retained. |
| M30 Plan-check date and other printed metadata | R | O | Compute inventories/reviews supported printed metadata, conditional on document profile. Compare extra metadata matching optional unless explicitly required; manual correction remains available. |
| M31 Memorandum applicability | R | N/A | Compute must assess memorandum applicability. Compare need not rerun detection; it consumes applicability/history under M06/M41. |
| M32 Survey made at instance of / surveyed-for parties | R | R | Compute required if memorandum/survey-request section applies; otherwise N/A. Compare required for applicable participant/role relationships, not an assumption requestor equals owner. |
| M33 Notices served and attendance | R | O | Compute required when notice/attendance evidence is applicable; otherwise N/A. Compare optional independent verification, while recorded defects/roles remain covered by M18/M41. |
| M34 Objections and surveyor decision grounds | R | R | Required when objection/decision-ground evidence or inherited related findings applies; otherwise N/A. Explicit None/N/A remains valid reviewed evidence. |
| M35 North arrow | R | O | Compute assesses north-arrow presence/source evidence, not automatic rotation or guaranteed direction accuracy. Compare independent visual check optional. |
| M36 Scale bar / printed scale | R | O | Compute assesses scale/scale-bar presence/text. Compare independent scale check optional unless needed for a declared required measurement; no inferred georeference. |
| M37 Property name near diagram | R | N/A | Compute required when relevant property-name/diagram rule applies; otherwise N/A. Compare does not repeat proximity rule; inherits findings under M41. |
| M38 Legal/LTD query evidence | N/A | R | Actual legal/LTD retrieval/comparison belongs to Compare; Compute prepares keys under M18-M25. Unavailable/incomplete required comparison stays pending. |
| M39 Spatial overlap/fiscal/Enterprise context | O | R | Compute optional external overlap/context preparation. Compare spatial/neighbor context required when applicable geometry-based comparison is declared; irrelevant checks N/A, required-but-unavailable evidence pending/limitation recorded. |
| M40 Human presence and accuracy confirmation | R | R | Human presence/accuracy review is required for consumed values; manual tabular correction allowed in Compare, not geometry editing. |
| M41 Cumulative findings and full review coverage | R | R | Full applicable examination coverage and cumulative finding history required; no stop at first source defect or clearing earlier-stage findings. |
| M42 Stage disposition and final decision | R | R | Compute records examination/forwarding disposition; Compare records completed approved/rejected or pending-with-reason. Geometry validation is separate. |

### Preventing Duplicate Work

- Required in both stages usually means different work: Compute validates printed/captured truth; Compare compares that reviewed truth with system evidence.
- Compare need not repeat geometry solving, closure, orientation, memorandum detection or diagram-name proximity. Independent rechecks are optional; reviewing inherited findings/limitations is always required under M41.
- O/N/A for a direct system match (equipment, survey method, source-only notes) never hides its source evidence or inherited defect. Complete-document review remains M06/M40/M41.
- Equipment/north-arrow/scale presence review can be required while absence is a recorded source finding; existing report_only effects are not silently upgraded to hard blockers.
- All included parcels need examination disposition. A genuinely not-applicable item records its reason; a pending/unperformed required item cannot be disguised as N/A.
- For unavailable geometry-related evidence, distinguish a recorded source-defect-related inability from a tool/service failure. Neither establishes valid geometry; incomplete required LTD review stays pending.
- When Compute tabular work is explicitly assigned for completion in Compare, preserve per-item outstanding state/owner/next-stage requirement; never mark it reviewed or missing-from-source just to forward. The approved stage policy must identify permissible deferrals.

### Classification Review Result

Settings design: [isolated policy mapping](toolbox-review-policy-settings-design.md) and [non-activatable JSON seed](settings-design/ToolboxReviewPolicy.design.json). These preserve C15 and do not change application configuration.

JotaPe approved classification v1 for all 42 inventory IDs and its documented applicability conditions on 2026-10-08. This resolves the classification-approval portion of D04/D12, not the remaining detailed preset catalog, effect/authorization mappings or runtime implementation. Apply the approved address/LandVal/property-name conditions explicitly; absent API fields alone do not establish N/A. Future optional-item promotions require an explicit versioned requirement.

Settings integration is not implemented. The intended Toolbox policy should represent stage classification, applicability and policy version separately from enabled tools and workflow effects. Existing StructureRules.json remains unchanged; required review must not be translated automatically into an absence/failure blocker. Store the policy version used for each case review so later settings changes do not silently alter prior decisions.


## Existing Memorandum Rule Detail

All 13 are configured in StructureRules.json and implemented by PxaMemorandumReviewRuleService. Applicability depends on detection/profile and enabled settings; default effects shown are current configuration, not new compulsory policy.

| Existing Rule ID Suffix (prefix pxa_memorandum_) | Item | Default Effect |
|---|---|---|
| detected | Memorandum text detected | info |
| surveyed_for_names_present | Survey made at the instance of | requires_disposition |
| surveyed_property_name_present | Surveyed property name | requires_disposition |
| property_name_near_diagram | Property name near parcel diagram | report_only |
| document_area_present | Area value/unit | requires_disposition |
| objections_captured | Grounds of objections | requires_disposition |
| surveyor_certification_present | Surveyor certification | requires_disposition |
| instrument_group_complete | Instrument + check date + check result | requires_disposition |
| parish_present | Parish evidence | requires_disposition |
| north_arrow_present | North arrow evidence | report_only |
| scale_bar_present | Scale-bar evidence/text | report_only |
| notice_served_on_present | Notices served on | requires_disposition |
| appearance_parties_present | Appeared parties or explicit negative attendance | requires_disposition |

Current memorandum reviewer choices include Accepted, Corrected, Skipped and Not available; evaluators expose passed/needs_review/failed/not_available/not_applicable/disabled. These are not already the structured Toolbox origin/presence/accuracy states. Preserve observed outcomes while adapting disposition/audit; do not translate Skipped into examined or Not available into human-confirmed missing-from-source without evidence.

## Existing Field Groups To Reuse

- Point rows: RowId, ParcelGroupId, ParcelName, TraverseId, SequenceInGroup, boundary break, point identifier, Easting/Northing/Length, unresolved state, source evidence, original/manual/review provenance.
- Boundary segments: SegmentId, group/name, sequence, from/to, bearing/distance/length, boundary inclusion, original and reviewed values, source page/zone/evidence, adjacent owner.
- Named parties and adjacent owners: name, role, lot, address, LandVal, Exam No., volume/folio, source page/zone, review status/notes; adjacent owners also reference related segment endpoints.
- Metadata: key/label/value/raw text/confidence/page/zone, Present, review status/notes, semantic state/unit, surveyor title/organization and original values.
- OCR/vision normalized metadata: parish, property_name, document_area, survey_date, survey_method, grounds_of_objection, surveyor_decision_grounds, instrument, instrument_check_date, instrument_check_result, gps_instrument_number, gps_serial_number, surveyed_by, plan_check_date, file_reference, volume_folio. Memorandum normalization adds its own structured parties/fields; do not duplicate source-trace types.
- Compare computed-participant projection retains parties, representatives and adjacent owners (not just one owner). Its selected row can drive title-image search. The separate simple survey-plan summary uses first owner/volume-folio and a name-list neighbor projection; that simple path is not comprehensive matching.
- Compare legal/party evidence exposes names/roles, PID, volume/folio, LandVal, parish, property type, tenure, registration date and, separately, PRID/address/tax/status. Availability depends on actual response/source mapping.

## Proposed Compute Completeness Checklist

- [ ] Same current case and safe/readable source inventory; missing or unusable source evidence documented separately from tool failure.
- [ ] Applicable document pages/sections and every detected parcel inventoried; inclusion/exclusion/unresolved dispositions explicit.
- [ ] Structure/georeference/dimension checks evaluated or source-related inability recorded with reason; skipped/disabled does not automatically mean examined.
- [ ] Geometry extraction mismatches corrected/rechecked, or manual reviewed geometry route used; source-plan defects preserved instead of stopping at the first defect.
- [ ] Printed field groups M18-M37 reviewed for applicable items: source presence and value accuracy human-confirmed, or explicitly not reviewed/uncertain for outstanding work.
- [ ] Tabular work remaining for Compare is enumerated; do not treat extraction omission as source absence. Applicable stage-required items cannot remain silently pending; classification v1 is approved, detailed preset/deferral mapping remains to be implemented.
- [ ] Every included parcel has current examination disposition; genuine source defects can forward normally with evidence/explanation. Stage readiness is distinct from valid-geometry approval.
- [ ] Cumulative findings and review limitations accompany the exact handoff inputs; local save/package/remote transition are separate outcomes.

## Proposed Compare Completeness Checklist

- [ ] Inherit source/review evidence, stable parcel/participant links, Compute findings and explicit outstanding tabular work.
- [ ] Capture/correct current-case tabular values directly in Compare; preserve original evidence and human review audit. Geometry/source-PE edits remain forbidden.
- [ ] For each applicable participant/identifier, retain relevant LTD query/record/evidence and human comparison outcome; automatic equality alone is not a whole-document pass.
- [ ] Keep legal ownership evidence distinct from fiscal/taxpayer/Enterprise/spatial context; source-specific unsupported queries are not fabricated.
- [ ] Review all available evidence and retain completed items; classify unavailable service, incomplete evidence, ambiguous/no-record results and source absence separately.
- [ ] Required incomplete comparison stays pending with a scoped recorded reason; this permits saving partial work, not a fabricated final decision or server completion.
- [ ] Preserve earlier findings, append new ones and retain related/resolved history. Complete applicable whole-document/parcel scope rather than stop at the first defect.
- [ ] At completed Compare, record explicit approved/rejected examination outcome and the cumulative evidence/reasons; do not approve invalid geometry or infer rejected from a generic blocked state.

## Finding And Check Record Reuse

Target logical fields adapt 11.2 and 13.4A rather than introduce another canonical extraction schema:

| Record | Required Semantics | Existing Foundation / Gap |
|---|---|---|
| Check coverage | checklist ID/version, stage, document/parcel/field scope, applicability, pending/evaluated/not-evaluable outcome and reason | Preflight/rule IDs/results reusable; full item coverage map Gap |
| Value review | raw/extracted and reviewed values; presence and accuracy separately; manual/corrected capture; source; reviewer/time; input fingerprint | Existing review fields reusable; typed common presence/accuracy/audit Adapt |
| Comparison | current reviewed input, actual LTD evidence/ref/retrieval status, human result, limitations, pending reason | Search/draft evidence reusable; per-field/parcel completion and invalidation Adapt |
| Finding | stable ID, error origin, scope, detection stage/actor/time, source versus extracted/system evidence, unresolved outcome, explanation, resolution history | Current rule IDs and discrepancy text reusable; cumulative stable case register Gap |
| Stage disposition | all-included examination coverage; clean/forwarded-with-recorded-defects/pending/final Compare outcome, separate geometry validation | Legacy dispositions reusable mechanically; semantics Adapt, not direct enum substitution |

## Reuse Plan And Gaps

| Priority | Work | Existing Owner To Reuse | Planned Toolbox Owner |
|---|---|---|---|
| 1 | Canonical groups/source trace, legacy compatibility | 11.2 review contract; do not duplicate or retire it silently | 11.2 dependency, 13.4A adapter |
| 2 | Stable memberships, human value semantics, coverage and finding/disposition model | Review document/views, existing rule results/dispositions | 13.4A in-memory foundation |
| 3 | Rerun/manual preservation and shared-edge impacts | Manual services/solver/readiness evaluators | 13.4B; no stable topology guarantee from current diagnostics |
| 4 | Save/reopen evidence, findings/history and revisions | Current review/draft persistence mechanics | 13.5 single-writer migration |
| 5 | Reusable rule/tool invocation and Compute/manual preset | Existing preflight, extraction, solver, rule catalogs | 13.6/13.7; no extraction engine rewrite |
| 6 | Multi-parcel/participant comparison, local tabular corrections, partial LTD state | Existing queries, evidence comparison, participant/title and spatial services | 13.8; no Compare geometry edits |
| 7 | Same-revision cumulative reports, final decision and normal transition | Existing reports/package/lifecycle services | 13.9; fresh authority and truthful receipts |

Specific inventory caveats:
- Current General Info exposes Present as an editable text column, ReviewStatus and Notes; inspected model has no universal separate Accuracy property. The installed-build behavior described by the user should be reconciled during implementation; do not remove working confirmation controls or assume the inspected checkout matches every deployed build.
- Compute grids allow editing existing metadata/participant/volume-folio values; CanUserAddRows=false is present on relevant grids. Standalone missing-item creation and Compare-local tabular editing are adaptations, not claimed complete simply because fields are editable in Compute.
- Compare computed participants are displayed as immutable projected records/ListView columns; changing search inputs or retaining valuable evidence is not editing saved source-plan values.
- Simple automatic legal comparison checks owner, parcel ID and volume/folio; fiscal comparison checks adjacent labels. It is not a matcher for every table row/field. Both-blank values can compare equal in that helper; Toolbox must require presence/coverage evidence before interpreting a match as examined.
- Preflight dimension readiness can use candidate counts/area from extraction summary. Full bearing/closure/construction checks are separate validators; don't label preflight success a dimensional-accuracy pass.
- Existing readiness diagnostics identify duplicate/cross-parcel edge issues without the target stable shared-edge lineage/dependency model.
- Existing Compute Pending/Approved/Rejected/Postponed and Compare approved/blocked/returned_to_compute/saved_progress are baseline semantics. User-confirmed final rejection at end of Compare, defect-forwarding and pending partial review require explicit Toolbox adapters, not operational enum/rule edits.
- Source-plan defect forwarding does not fabricate missing geometry or publish invalid shapes. If a downstream action requires geometry that cannot be constructed, record inability/evidence; do not treat the action as succeeded. Detailed checkpoint/package policy remains D05/D10.
- LTD no-record/ambiguity can be examination evidence or outstanding work depending on whether required comparison is actually complete; incomplete required evidence stays pending, not automatically a rejection.

## Evidence And Verification

S01: [Structure rule catalog](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/StructureRules.json) (32 parsed rules and groups).
S02: [Manifest preflight](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs), georeference/dimension checks; [processing prerequisites](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessingEnvironmentPreflightService.cs).
S03: [Survey-plan OCR/vision adapter](../../../src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py), normalized metadata and prompt; [text extraction](../../../src/ProcessingTools/adapters/pdf_text_structured_extraction.py).
S04: [Review document](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs); [review persistence](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs); [metadata/participant view models](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewMetadataViewModels.cs).
S05: [Compute review XAML](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml), General Info/Participants/Points/Boundary/Memorandum; [workspace view model](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs), filtering/rebuild.
S06: [Parcel-scoped validation](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedReviewValidationService.cs); [boundary solver](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/SurveyPlanBoundarySolver.cs); [manual points](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedManualPointService.cs); [manual segments](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ManualBoundarySegmentService.cs).
S07: [Validation configuration](../../../src/ProcessingTools/rules/rules.yaml); [validation adapter](../../../src/ProcessingTools/adapters/validation_adapter.py), evaluators/output findings. Stale configuration comments are not implementation-status proof.
S08: [Map georeference overlay](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceOverlayService.cs).
S09: [Compare evidence models](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareEvidenceModels.cs); [cadaster query services](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs).
S10: [Survey-plan summary](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareSurveyPlanEvidenceService.cs), first owner/pair; [evidence comparison](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareEvidenceComparisonService.cs), legal/fiscal comparison.
S11: [Participant title search/download](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareComputedParticipantTitleService.cs), current-title queries and case-folder download. Title-image lookup is not automatic Innola attachment.
S12: [Enterprise comparison evidence](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareEnterpriseCadasterEvidenceService.cs); [spatial overlap](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/SpatialReview/ArcGisSpatialOverlapReviewService.cs); [owner enrichment](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/SpatialReview/SpatialOverlapOwnerEnrichmentService.cs).
S13: [Dockpane](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs), UseManualCogoFallbackAsync; [workflow session](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs), UseManualCogoReviewAsync.
S14: [Memorandum rules](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/PxaMemorandumReviewRuleService.cs), DefaultRules/Evaluate and grouped evidence.
S15: [Compare view model](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs), ProjectComputedParticipants/CompareComputedParticipantItem/search/apply/save; [Compare XAML](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml), read-only participant columns.
S16: [Compare working geometry](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkingGeometryService.cs).
S17: [Validation finding dispositions](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Validation/ValidationFindingDispositionPersistenceService.cs).
S18: [Compute report](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeExaminationReportService.cs); [Compare report](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareReviewReportService.cs).
S19: [Compare draft](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareReviewDraftPersistenceService.cs); [Compare decision/readiness](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareReviewDecision.cs).
S20: [Compute disposition](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Disposition/ComputeReviewDisposition.cs).
S21: [C# test registrations](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs), existing metadata/memorandum, parcel validation, solver/manual, participant/title, Compare decisions and report tests.
S22: [Python validation tests](../../../src/ProcessingTools/tests/test_validation_adapter.py); [survey-plan extraction tests](../../../src/ProcessingTools/tests/test_survey_plan_ocr_vision_extraction.py).
S23: [Historical Compute stage design](../../../docs/project/COMPUTE_STAGE_STEPS_AND_RULES.md), draft alignment document, not runtime proof.
S24: [Confirmed decisions C11-C14](toolbox-decision-register.md); [13.4A](../../implementation-artifacts/toolbox/13-4a-add-stable-parcel-memberships-and-acceptance.md); [process](toolbox-process-definition.md).

Document checks completed: 42 unique matrix IDs, 32 parsed catalog rules and all 13 memorandum rule/effect mappings verified; source links resolve in both checkouts and four preparation documents match across mirrors. Operational source/tracker/project-context diff remains unchanged. No runtime C#/Python tests, build or live service requests were performed.

## Review Still Needed

1. Confirm this item inventory is complete for current examiners; add actual source fields/rules if anything is missing, rather than invent new mandatory checks.
2. Classification v1 is approved for all 42 items and documented conditions. Next define the versioned settings/preset mapping and explicit tabular-deferral inputs; no runtime configuration is changed by this approval.
3. Define the final report/rejection presentation: unresolved findings as the rejection reasons, with resolved findings and review limitations retained in history. This presentation choice is proposed, not yet a recorded product decision.
4. Keep 11.2 ownership, local identity policy, shared-edge and persistence feasibility gates open; producing the matrix does not make 13.4A ready or change operational story status.
