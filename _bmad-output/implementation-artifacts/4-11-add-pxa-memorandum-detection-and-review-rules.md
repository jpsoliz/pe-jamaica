---
baseline_commit: handoff-2026-08-20
---

# Story 4.11: Add PXA Memorandum Detection And Review Rules

Status: review

## Story

As a PXA compute examiner,  
I want memorandum-specific document facts extracted, grouped, rule-checked, and shown in the PXA review experience,  
so that I can validate legal/document evidence with a human-in-the-loop workflow before approving points, lines, spatial units, and the final report.

## Acceptance Criteria

1. Given a PXA transaction source document is processed, when OCR/embedded text or vision extraction finds text containing `MEMORANDUM`, then the extraction artifact classifies the source as a memorandum-capable PXA document and records the detection evidence.
2. Given a PXA source document does not contain `MEMORANDUM`, when memorandum rules evaluate, then memorandum-specific rules are `not_applicable`, not failed.
3. Given a memorandum is detected, when extraction runs, then the artifact captures the party at whose instance the survey was made / surveyed for as one or more structured names with source page, source zone, confidence, and review status.
4. Given a memorandum is detected, when extraction runs, then the artifact captures the surveyed property name as one or more structured values with source page, source zone, confidence, and review status.
5. Given a surveyed property name is captured, when visual/source-zone evidence is available, then the system records whether the property name is printed near the parcel diagram; low-confidence or missing visual evidence produces a reviewable rule finding.
6. Given a memorandum is detected, when extraction runs, then instrument evidence is captured as a grouped review section containing instrument name/type, instrument check date, instrument check result, and GPS instrument number / serial number when present.
7. Given instrument name/type and instrument check date are both available, when memorandum validation runs, then the date is validated as related to the instrument check group rather than treated as an unrelated metadata field.
8. Given a memorandum is detected, when extraction runs, then parish is captured or reused from existing survey metadata with source page, source zone, confidence, and review status.
9. Given a memorandum is detected, when extraction runs, then notice-served-on parties are captured as one or more names with source page, source zone, confidence, and review status.
10. Given a memorandum is detected, when extraction runs, then parties who appeared personally and parties who appeared by representative are captured as structured rows, preserving whether the appearance was personal or representative.
11. Given a memorandum is detected, when extraction runs, then north arrow and scale bar presence are captured as map evidence fields with present/missing/uncertain status, source page or approximate location, confidence, and review status.
12. Given the PXA review workspace opens, when memorandum data exists or memorandum rules are applicable, then the reviewer sees a compact Memorandum review area grouped as:
    - Memorandum Detection
    - Property / Survey Request
    - Instrument Check
    - Location / Map Evidence
    - Notice / Attendance
13. Given the PXA review workspace opens, when rule results are available, then each group displays a concise status count such as `2 passed / 1 needs review / 1 not available`.
14. Given a rule row is shown to the reviewer, then the row uses reviewer-friendly statuses: `Passed`, `Needs Review`, `Failed`, `Not Available`, and `Not Applicable`, while preserving persisted machine outcomes.
15. Given an extracted memorandum value is low confidence, missing source-zone evidence, or requires examiner judgment, when shown in the review workspace, then it appears as `Needs Review` and allows the user to accept, correct, or add review notes.
16. Given a required memorandum field is absent from a detected memorandum, when validation runs, then the rule result is `Not Available` or `Failed` according to the configured rule severity/workflow effect, not `Passed`.
17. Given a memorandum rule has `workflow_effect=requires_disposition`, when the reviewer tries to complete validation with unresolved memorandum findings, then the reviewer must accept, override, correct, or add a disposition note before continuing.
18. Given a memorandum rule has `workflow_effect=report_only`, when it fails or needs review, then the finding remains visible in the UI/report but does not block progression.
19. Given memorandum findings are persisted, when the Compute examination report is generated, then it uses the same persisted outcomes and includes reviewed memorandum values, source zones, reviewer status, and unresolved findings where report-visible.
20. Given automated tests run, then coverage proves memorandum detection, non-memorandum not-applicable behavior, metadata extraction normalization, review persistence, status mapping, disposition gating, and report inclusion.

## Tasks / Subtasks

- [x] Extend PXA memorandum extraction/classification. (AC: 1-11, 20)
  - [x] Detect `MEMORANDUM` from embedded text and OCR/vision output for PXA survey-plan documents.
  - [x] Persist a document classification field such as `document_sections.memorandum.detected` or equivalent in `extraction_review_data.json`.
  - [x] Capture detection evidence: matched text, page, source zone, confidence, and extraction provider.
  - [x] Extend `survey_plan_ocr_vision_extraction.py` normalization to include memorandum-specific fields without breaking existing PXA survey metadata.
  - [x] Preserve existing fields: `survey_metadata`, `parties`, `representatives`, `adjacent_owners`, `volume_folio`, `north_arrow`, `segments`, and `rows`.

- [x] Add memorandum metadata schema fields. (AC: 3-11, 15, 19)
  - [x] Add `surveyed_for_names` or equivalent party rows distinct from generic owners/parties.
  - [x] Add `surveyed_property_names`.
  - [x] Add `property_name_near_parcel_diagram` with status/evidence rather than only a string value.
  - [x] Add grouped instrument fields: `instrument_name`, `instrument_check_date`, `instrument_check_result`, `gps_instrument_number`, `gps_serial_number`.
  - [x] Add `notice_served_on` name rows.
  - [x] Add `appeared_parties` rows with appearance mode: `personal`, `representative`, or `unknown`.
  - [x] Add `scale_bar` map evidence beside existing `north_arrow`.
  - [x] Ensure every new field supports value/present status, confidence, source page, source zone, review status, and review notes where applicable.

- [x] Add configurable memorandum rules. (AC: 2, 12-18, 20)
  - [x] Add evaluator support for memorandum detection and memorandum field presence.
  - [x] Add evaluator support for property-name-near-diagram visual/source-zone evidence.
  - [x] Add evaluator support for grouped instrument completeness/consistency.
  - [x] Add evaluator support for notice/appearance party list presence.
  - [x] Add evaluator support for map evidence presence: north arrow and scale bar.
  - [x] Scope rules to PXA transaction profiles and memorandum-detected documents.
  - [x] Return `not_applicable` when `MEMORANDUM` is not detected.
  - [x] Return `not_available` when a required field is absent from a detected memorandum.
  - [x] Use `needs_review` when the extracted value exists but confidence/source-zone/reviewer status is unresolved.
  - [x] Persist `workflow_effect` separately from outcome so report-only findings do not block workflow.

- [x] Update the PXA review UX. (AC: 12-18)
  - [x] Add a Memorandum tab or Memorandum section in the existing PXA review workspace.
  - [x] Do not place memorandum validation fields inside the point or segment grids.
  - [x] Show group summaries for Memorandum Detection, Property / Survey Request, Instrument Check, Location / Map Evidence, and Notice / Attendance.
  - [x] Show rule row statuses using reviewer labels: `Passed`, `Needs Review`, `Failed`, `Not Available`, `Not Applicable`.
  - [x] Allow reviewer correction and disposition notes for low-confidence, missing, or contradictory memorandum fields.
  - [x] Keep unresolved memorandum counts visible while the reviewer works in Points and Segments.
  - [x] Warn or block on Validation Complete based on rule `workflow_effect`.

- [x] Persist review edits and report outcomes. (AC: 15-19)
  - [x] Extend `ExtractionReviewPersistenceService` load/save to preserve new memorandum fields and review statuses.
  - [x] Ensure review hash changes when memorandum values, statuses, or notes change.
  - [x] Ensure `ComputeExaminationReportService` includes memorandum group outcomes and reviewed values when report-visible.
  - [x] Ensure report output distinguishes `Needs Review`, `Not Available`, and `Not Applicable` from `Passed`.

- [x] Add focused automated coverage. (AC: 1-20)
  - [x] Python extraction tests for memorandum detection and normalization.
  - [x] C# persistence tests for memorandum fields and review hash changes.
  - [x] Rule evaluation tests for detected, missing, low-confidence, and not-applicable states.
  - [x] UX/view-model tests for group counts and status labels.
  - [x] Gate tests for `requires_disposition` versus `report_only`.
  - [x] Report tests proving persisted memorandum findings are included.

## Dev Notes

### Why This Is A New Story

This should not be folded into Story 4.10. Story 4.10 created the configurable staged rule infrastructure. This story is a business-rule consumer of that infrastructure for PXA memorandum documents.

The implementation should add memorandum-specific extraction, review, rules, and reporting while reusing:

- the staged rule catalog from Story 4.10
- the PXA survey-plan extraction/review model from Stories 2.18, 2.19, and 2.20
- the existing PXA review workspace and report output patterns

### Current Code Reality

- `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`
  - Normalizes PXA survey-plan OCR/vision output.
  - Already emits `survey_metadata`, `parties`, `representatives`, `adjacent_owners`, `north_arrow`, `segments`, and `rows`.
  - Existing metadata includes fields such as `parish`, `document_area`, `survey_date`, `instrument`, `surveyed_by`, `plan_check_date`, `file_reference`, and `volume_folio`.
  - It does not yet represent the complete memorandum-specific schema requested here.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
  - Review document already has `SurveyMetadataFields`, `Parties`, `Representatives`, `AdjacentOwners`, `VolumeFolios`, `Segments`, and point rows.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
  - Loads/saves existing PXA metadata and party rows.
  - Must be extended carefully to preserve existing review artifacts and unknown fields.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
  - Already exposes PXA metadata and named-party review concepts.
  - New Memorandum grouping should use existing observable/review patterns rather than a disconnected UI model.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeExaminationReportService.cs`
  - Already reports PXA metadata/participants.
  - Must consume persisted memorandum outcomes, not recompute display-only state.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleDefinition.cs`
  - Story 4.10 added staged rule metadata: `stage_id`, `workflow_effect`, `evaluator_key`, `report_visible`.
  - New memorandum rules should use supported evaluator keys and staged rule records.

### Recommended UX Design

The best UX is a dedicated **Memorandum** tab or section in the PXA review workspace.

Do not mix these fields into the point or segment grids. They are document-evidence rules, not geometry rows.

Recommended PXA tabs/sections:

```text
Metadata | Memorandum | Points | Segments | Validation
```

If adding a new tab is too large for this story, use a dedicated Memorandum panel above the existing PXA metadata area, but keep it visually distinct from Points and Segments.

### Recommended Memorandum Groups

#### Memorandum Detection

- Memorandum text detected
- detection source: embedded text, OCR, vision, or manual
- page/zone/confidence

#### Property / Survey Request

- party at whose instance the survey was made / surveyed for
- surveyed property name
- property name appears near parcel diagram

#### Instrument Check

- instrument name/type
- instrument check date
- instrument check result
- GPS instrument number
- GPS serial number

#### Location / Map Evidence

- parish
- north arrow
- scale bar

#### Notice / Attendance

- notices served on
- appeared personally
- appeared by representative

### Status Vocabulary

Persisted machine outcomes should remain explicit and stable. Reviewer labels should be friendlier.

Recommended mapping:

| Machine outcome | Reviewer label | Meaning |
|---|---|---|
| `passed` | Passed | Rule has enough accepted evidence. |
| `needs_review` | Needs Review | Evidence exists but requires human confirmation, correction, or note. |
| `failed` | Failed | Required evidence is contradictory or invalid. |
| `not_available` | Not Available | Required evidence is missing from a detected memorandum. |
| `not_applicable` | Not Applicable | Rule does not apply, usually because memorandum was not detected. |
| `disabled` | Disabled | Rule is disabled in configuration. |
| `skipped` | Skipped | Rule was skipped for a known workflow reason. |

The user specifically asked whether rules should say Passed, Failed, or Not Available to Review. The UX recommendation is:

- Use `Passed` when accepted.
- Use `Needs Review` when human validation is required.
- Use `Failed` only when the system can determine invalid/contradictory evidence.
- Use `Not Available` when the expected evidence is missing but the memorandum exists.
- Use `Not Applicable` when the memorandum condition is false.

### Human-In-The-Loop Flow

The reviewer should see memorandum rules before or when the point/line form is launched.

Recommended behavior:

1. Data Extraction creates draft memorandum values and draft rule findings.
2. PXA review workspace opens.
3. Memorandum group summaries are visible before point/line approval.
4. Reviewer corrects values, accepts extracted values, or adds notes.
5. While reviewing Points and Segments, unresolved memorandum counts remain visible as a compact side/top summary.
6. Validation Complete checks workflow effects:
   - `blocker`: cannot continue.
   - `requires_disposition`: reviewer must correct, accept, override, or add note.
   - `report_only`: shown in report, does not block.
   - `info`: audit only.

### Suggested Rule IDs

Use final names consistent with local rule naming patterns, but these are recommended:

```text
pxa_memorandum_detected
pxa_memorandum_surveyed_for_names_present
pxa_memorandum_surveyed_property_name_present
pxa_memorandum_property_name_near_diagram
pxa_memorandum_instrument_group_complete
pxa_memorandum_instrument_check_date_present
pxa_memorandum_instrument_check_result_present
pxa_memorandum_gps_serial_recorded_when_present
pxa_memorandum_parish_present
pxa_memorandum_notice_served_on_present
pxa_memorandum_appearance_parties_present
pxa_memorandum_north_arrow_present
pxa_memorandum_scale_bar_present
```

### Recommended Stage Assignment

- `data_extraction`
  - memorandum detection
  - raw value extraction
- `validate_points_and_lines`
  - human confirmation/disposition of memorandum fields
  - unresolved memorandum checks while reviewing points/segments
- `final_review`
  - report completeness and unresolved finding summary

If the current implementation can only attach these to existing stage summaries initially, prefer `validate_points_and_lines` for reviewer-facing checks and keep extraction-only detection metadata in the extraction artifact.

### Preservation Rules

- Do not make memorandum rules apply to non-PXA transactions.
- Do not fail memorandum rules when `MEMORANDUM` is not detected; use `not_applicable`.
- Do not fabricate parties, representatives, instrument numbers, or property names from weak OCR guesses.
- Do not treat low-confidence extracted text as passed without review.
- Do not block point/line editing just because a memorandum value is unresolved; gate only at Validation Complete or Final Review according to `workflow_effect`.
- Do not mix legal/document metadata editing into geometry grids.
- Do not create spatial output or Enterprise writes from memorandum review.
- Do not log raw OCR prompts, provider secrets, API keys, tokens, or unbounded raw document text.

### Mary Review Addendum: Example-Driven Rule Refinements

The reviewed memorandum examples show at least two source layouts:

- **Table memorandum layout**: `MEMORANDUM` appears as a table heading, with labeled rows/columns for parish, area, surveyed-for party, property name, survey dates, objections, surveyor decision, instrument, instrument check date/result, notices served, and appeared parties.
- **Narrative/map memorandum layout**: `MEMORANDUM` appears below or near the parcel diagram/scale bar, followed by narrative text such as the portion surveyed, registered ownership, survey date, instruction source, notices served, absence/presence at survey, and surveyor block.

The rule/evidence model should treat the following printed labels as strong anchors, with fuzzy/OCR-tolerant matching for case, punctuation, line breaks, and common OCR substitutions:

```text
MEMORANDUM
The name of the party at whose instance the survey was made
SURVEYED FOR
The name of the property surveyed or of the property of which the Land surveyed forms part
NAME OF PROPERTY
The dates between which the survey was made
DATE OF SURVEY
Make and No. of Instrument
INSTRUMENT
Date of last instr. check & result
DATE OF INSTRUMENT CHECK
GPS INSTRUMENT
DATE OF GPS INSTRUMENT CHECK
RESULT OF INSTRUMENTS CHECK
The names of the parties interested in the survey who were served with notices
NOTICES WERE SERVED ON
The names of those who appeared either personally or by their representatives
THOSE WHO APPEARED
No one appeared
There were no objections
PARISH
AREA
Scale
SCALE
North arrow
```

Recommended rule refinements:

| Rule area | Refinement |
|---|---|
| Memorandum detection | Pass when `MEMORANDUM` is found in either a table heading or narrative/map memorandum section. Capture section type: `table`, `narrative`, or `unknown`. |
| Scale bar | Detect numeric/graphic scale bars near the memorandum or parcel diagram, including `SCALE One Millimetre = 0.5 Metre or 1:500`, `Scale 1:1000; 1 cm = 10.00 metres`, and tick-mark scale bars. |
| North arrow | Detect north arrow as a graphic/map symbol, not only the words `north arrow`. Because examples may show a triangle/surveyor logo near the surveyor block that is not necessarily a north arrow, uncertain symbol-only matches should be `Needs Review`. |
| Surveyed-for party | Use label anchors `The name of the party at whose instance the survey was made` and `SURVEYED FOR`. Capture one or more names; preserve multi-name rows such as `Edward George Gayle, Dwayne Gayle, & Jennifer Gayle`. |
| Surveyed property name | Use label anchors `The name of the property surveyed...` and `NAME OF PROPERTY`. Capture property names such as `Part of SYMS RUN`, `Part of BLUE MOUNTAIN`, `EMMA VILLE situate at SMITHVILLE`, and `Part of RETREAT`. |
| Property name near diagram | This should be a visual proximity rule separate from property-name extraction. It may pass when the property name is printed as the plan title near the parcel diagram even if the memorandum table repeats it below. |
| Instrument group | Keep instrument fields grouped even when labels differ: `Make and No. of Instrument`, `INSTRUMENT`, `GPS INSTRUMENT`, `Date of last instr. check & result`, `DATE OF INSTRUMENT CHECK`, `RESULT OF INSTRUMENTS CHECK`. |
| Instrument check date/result | Examples combine date and result in one cell, such as `04/10/2024 - Satisfactory` or `Jan. 20, 2025 (Satisfactory)`. Extract both date and result from the same evidence span and keep the source zone shared. |
| GPS instrument optionality | GPS instrument number/serial should be optional unless the label exists. If `GPS INSTRUMENT` is present but value is missing, use `Needs Review` or `Not Available` based on configured severity. |
| Notice-served-on parties | Use table/list extraction after `notices were served on` anchors. Preserve organization names such as `The C.E.O of St. Ann Municipal Corporation`, `The Commissioner of Lands`, and agency names, not only personal names. |
| Appeared parties | Treat `No one appeared`, `No one`, and equivalent negative statements as a valid explicit value, not as missing evidence. Outcome should be `Passed` or `Needs Review`, not `Not Available`, once the examiner accepts it. |
| Objections | Examples consistently include objections (`None`, `There were no objections`). This is memorandum-relevant evidence and should be considered as an additional future rule: `pxa_memorandum_objections_recorded`. |
| Area | Examples include area inside the memorandum/table. Area may already exist in General Info, but the memorandum rules should record whether area was found in the memorandum section and avoid duplicate inconsistent values silently passing. |

Suggested additional/future rule IDs:

```text
pxa_memorandum_section_type_identified
pxa_memorandum_scale_bar_graphic_or_text_present
pxa_memorandum_appeared_negative_statement_recorded
pxa_memorandum_objections_recorded
pxa_memorandum_area_consistent_with_general_metadata
pxa_memorandum_instrument_check_result_parsed_from_combined_cell
pxa_memorandum_gps_instrument_recorded_when_label_present
pxa_memorandum_notice_party_organization_names_preserved
```

UX refinement:

- In the Memorandum tab, show extracted values under their business label, not only rule rows. The reviewer should see the field/value/evidence next to the rule result for quick validation.
- For appeared parties, display one of:
  - `No one appeared`
  - `Appeared personally: [names]`
  - `Appeared by representative: [names / representatives]`
- For instrument evidence, show one grouped panel: instrument, serial/number, instrument check date, result, GPS instrument, GPS serial, GPS check date, GPS result.

### References

- `docs/project/COMPUTE_STAGE_STEPS_AND_RULES.md`
- `_bmad-output/implementation-artifacts/4-10-add-configurable-compute-rule-catalog-by-stage.md`
- `_bmad-output/implementation-artifacts/2-18-add-single-parcel-survey-plan-pdf-metadata-and-geometry-extraction.md`
- `_bmad-output/implementation-artifacts/2-19-implement-pxa-survey-plan-segment-review-and-deterministic-boundary-solver.md`
- `_bmad-output/implementation-artifacts/2-20-add-pxa-survey-plan-metadata-review-model-and-ux.md`
- `_bmad-output/project-context.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex (Amelia)

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` - passed; one existing nullable warning observed earlier in `SurveyPlanBoundarySolverTests.cs`, final build passed after test update.
- `python -m unittest tests\test_survey_plan_ocr_vision_extraction.py` from `src\ProcessingTools` - passed, 5 tests.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "memorandum" "compute examination report generation uses persisted stage findings"` - passed, 4 tests.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "rule catalog" "settings workspace" "preflight rules"` - passed, 13 tests.
- Full C# harness still stops at existing `ManifestPreflightServiceTests.ManifestPreflightPassesValidScenarioA` with `Scenario A should pass computation role check`; this failure is outside the 4.11 memorandum path and was not introduced by the targeted memorandum changes.
- `python -m unittest tests\test_survey_plan_ocr_vision_extraction.py` from `src\ProcessingTools` - passed, 6 tests including table-layout/no-appearance memorandum extraction.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=...\.tmp-build\obj\ParcelWorkflowAddIn\ /p:BaseOutputPath=...\.tmp-build\bin\ParcelWorkflowAddIn\` - passed with clean temporary MSBuild paths because the default add-in `obj` folder denied writes.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:UseSharedCompilation=false /p:BuildProjectReferences=false /p:BaseIntermediateOutputPath=...\.tmp-build\obj\ParcelWorkflowAddIn.Tests\ /p:BaseOutputPath=...\.tmp-build\bin\ParcelWorkflowAddIn.Tests\` - passed after supplying the already-built add-in DLL to the temporary project-reference path; one existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet .tmp-build\bin\ParcelWorkflowAddIn.Tests\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll memorandum` - passed, 4 tests.
- `dotnet .tmp-build\bin\ParcelWorkflowAddIn.Tests\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "compute examination report generation uses persisted stage findings"` - passed, 1 test.
- `python -m unittest tests\test_survey_plan_ocr_vision_extraction.py` from `src\ProcessingTools` - passed, 7 tests including visible `MEMORANDUM` plus `SCALE : 1cm To 10m R.F 1/1000`.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=...\.tmp-build\obj\ParcelWorkflowAddIn\ /p:BaseOutputPath=...\.tmp-build\bin\ParcelWorkflowAddIn\` - passed.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:UseSharedCompilation=false /p:BuildProjectReferences=false /p:BaseIntermediateOutputPath=...\.tmp-build\obj\ParcelWorkflowAddIn.Tests\ /p:BaseOutputPath=...\.tmp-build\bin\ParcelWorkflowAddIn.Tests\` - passed; one existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet .tmp-build\bin\ParcelWorkflowAddIn.Tests\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll memorandum` - passed, 5 tests.
- `dotnet .tmp-build\bin\ParcelWorkflowAddIn.Tests\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "compute examination report generation uses persisted stage findings"` - passed, 1 test.
- `python -m unittest tests\test_survey_plan_ocr_vision_extraction.py` from `src\ProcessingTools` - passed, 7 tests; visible scale text is now preserved as value/raw_text.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=...\.tmp-build\obj\ParcelWorkflowAddIn\ /p:BaseOutputPath=...\.tmp-build\bin\ParcelWorkflowAddIn\` - passed.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:UseSharedCompilation=false /p:BuildProjectReferences=false /p:BaseIntermediateOutputPath=...\.tmp-build\obj\ParcelWorkflowAddIn.Tests\ /p:BaseOutputPath=...\.tmp-build\bin\ParcelWorkflowAddIn.Tests\` - passed; one existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet .tmp-build\bin\ParcelWorkflowAddIn.Tests\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll memorandum` - passed, 6 tests including root-level OCR text with no memorandum section.
- `dotnet .tmp-build\bin\ParcelWorkflowAddIn.Tests\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "compute examination report generation uses persisted stage findings"` - passed, 1 test.
- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=.tmp/obj/ /p:BaseOutputPath=.tmp/bin/` - passed; one existing nullable warning remains in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/.tmp/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll "pxa review" "compute examination report generation uses persisted stage findings"` - passed, 4 tests.
- `dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/.tmp/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll memorandum` - passed, 8 tests.
- `tools/package_addin.ps1 -Configuration Release` - passed; add-in package generated and registered as version `1.1.282`.

### Completion Notes List

- Added memorandum detection and normalized extraction fields for surveyed-for names, surveyed property names, property-near-diagram evidence, instrument check fields, GPS identifiers, notice-served-on parties, appeared parties, and scale-bar map evidence.
- Added catalog-driven PXA memorandum rule evaluation with reviewer labels, not-applicable handling for non-memorandum documents, report-only versus requires-disposition workflow effects, and disabled/report-visible support from Settings rule definitions.
- Added a dedicated Memorandum tab in the PXA review workspace, wired group summaries/rule rows through the parent review projection, and included memorandum disposition blockers in Validation Complete gating.
- Extended persistence/review hashing and Compute examination report JSON/PDF output so reviewed memorandum values and rule outcomes are auditable.
- Updated project context to record the new durable expectation that PXA memorandum rules remain catalog-driven.
- Mary review addendum added example-driven refinements for table/narrative memorandum layouts, label anchors, negative appeared-party statements, instrument combined-cell parsing, and future rules.
- Patched the addendum feedback into implementation: memorandum section layout is classified as table/narrative/unknown, scale-bar text can satisfy the scale-bar presence evidence, combined instrument-check cells split into date/result, and explicit `No one appeared` text is treated as positive attendance evidence with `appearance_mode: none`.
- Added `EvidenceValue` to memorandum rule results so the Memorandum tab, persistence hash, and saved rule JSON carry the reviewed value alongside pass/fail/not-available status.
- Adjusted visible-text detection from the screenshot example: OCR/embedded text containing `MEMORANDUM` now overrides a stale false flag, `SCALE : 1cm To 10m R.F 1/1000` is recognized as scale-bar evidence, and presence fields preserve visible text instead of collapsing to only `Present`.
- Embedded-text survey plan extraction now writes `document_sections.memorandum` and `scale_bar` so the Memorandum tab rules can evaluate when the text layer already contains the memorandum header.
- Expanded recovery for real artifacts: review loading now infers memorandum detection from root-level OCR/text fields (`document_text`, `raw_text`, `ocr_text`, `source_text`, `text_content`) and from memorandum-sourced survey metadata, so rules do not remain Not Applicable when visible evidence exists outside `document_sections`.
- Preserved visible scale text in both OCR/vision and embedded-text extraction outputs so the Memorandum tab Value column can show the actual scale label.
- Review-fix patch clarified the Memorandum tab's editable status as `Reviewer Disposition`, highlights unresolved `Needs Review`/`Failed`/`Not Available` rows in red, returns rows to normal text color after the reviewer selects `Accepted`, `Corrected`, `Override`, or `Disposition`, and wraps long Value/Finding text.
- Compute examination report output now includes all memorandum rule results and exposes each memorandum finding's evidence value and message in both the report JSON and the PDF Memorandum Findings table.

### File List

- `_bmad-output/implementation-artifacts/4-11-add-pxa-memorandum-detection-and-review-rules.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/project-context.md`
- `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`
- `src/ProcessingTools/tests/test_survey_plan_ocr_vision_extraction.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleDefinition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/StructureRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewMetadataViewModels.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/PxaMemorandumReviewRuleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeExaminationReportService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/PreflightRuleCatalogLoaderTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Settings/SettingsWorkspaceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ComputeExaminationReportServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ExtractionReviewPersistenceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/JamaicaReviewWorkspaceXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-08-20 | 0.1 | Created story for PXA memorandum detection, grouped review UX, configurable memorandum rules, human-in-the-loop validation, and reportable outcomes. | Mary / Sally / Codex |
| 2026-08-20 | 1.0 | Implemented memorandum extraction schema, configurable rule catalog entries/evaluator, review UX, persistence, report output, and automated coverage. | Amelia / Codex |
| 2026-08-20 | 1.1 | Added Mary example-review refinements for memorandum table/narrative layouts, OCR anchors, negative appeared-party handling, instrument parsing, and future rule candidates. | Mary / Codex |
| 2026-08-21 | 1.2 | Patched review feedback into 4.11: table layout detection, combined instrument-check parsing, scale-bar text evidence, explicit no-appearance evidence, and on-screen rule evidence values. | Amelia / Codex |
| 2026-08-21 | 1.3 | Fixed visible `MEMORANDUM` and `SCALE : 1cm To 10m R.F 1/1000` evidence so memorandum rules no longer remain Not Applicable when the text is present. | Amelia / Codex |
| 2026-08-21 | 1.4 | Added loader recovery for root-level OCR text and memorandum-sourced metadata, and preserved visible scale text in extracted values. | Mary / Amelia / Codex |
| 2026-08-28 | 1.5 | Patched Memorandum review UX/status readability and compute report Memorandum Findings value/message output while keeping sprint status in review pending code review. | JotaPe / Amelia / Codex |
