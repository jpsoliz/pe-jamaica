# PE/PXA Extraction Prompts And Rules

Last reviewed: 2026-09-02

## Purpose

This note records the current extraction routes, prompt source, and review-rule contract for PE and PXA compute workflows. It exists so future extraction changes are auditable before runtime behavior is changed.

## Current Routes

PE is computation-sheet driven.

- Transaction line: `PE`, `Plan Examination`, `Compute Survey Plan`, `Assign Computation Task`, `Computation Check`
- Primary source role: `computation_sheet`
- Supporting source role: `plan_map_reference`
- Main script route: `extract_points_from_computation_pdf`
- Adapter: `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- Text parser: `src/ProcessingTools/adapters/pdf_text_structured_extraction.py`
- Review artifact: `working/extraction_review_data.json`

PXA is survey-plan PDF driven.

- Transaction line: `PXA`, including `Plan Examination by Area`
- Primary source role: `survey_plan_pdf`
- Main script route: `extract_single_parcel_survey_plan_pdf`
- Adapter: `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- OCR/vision parser: `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`
- Review artifacts: `working/survey_plan_extraction_summary.json`, `working/extraction_review_data.json`, `working/extraction_route.json`

The configured routing table is `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRules.json`.

## Current PXA OpenAI Prompt

The live OCR/vision prompt is embedded in `_prompt(profile)` in:

`src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`

As of this review, the prompt text is:

```text
Extract structured cadastral survey plan data from this Jamaica survey plan image. Return only JSON with keys: document_type, coordinate_system, coordinate_system_confidence, north_arrow {detected, approximate_page_location, confidence, review_note}, scale_bar {detected, text, approximate_page_location, confidence, review_note}, survey_metadata {parish, property_name, document_area, survey_date, survey_method, grounds_of_objection, surveyor_decision_grounds, instrument, instrument_check_date, instrument_check_result, surveyed_by, plan_check_date, file_reference, volume_folio [{volume,folio,raw_text,confidence,source_page,source_zone,review_note}]}, surveyed_for_names, surveyed_property_names, notice_served_on, interested_parties, appeared_parties, parties, representatives, adjacent_owners, points [{point_id,northing,easting,confidence,source_page,source_zone,status,review_note}], derived_points [{point_id,northing,easting,confidence,source_page,source_zone,status,review_note}], segments [{from_point,to_point,bearing_txt,distance_txt,confidence,source_page,source_zone,status,review_note}], review_notes. Capture every visible boundary point and every visible boundary segment around the parcel. For coordinate_system, return only a coordinate reference system, datum, or grid label. Look directly near and above coordinate tables for labels such as JAD 2001, J.A.D. 2001, Jamaica Datum 2001, or Jamaica Grid. Do not put survey method text such as Theodolite Survey, Compass Standard, GPS, RTK, or Total Station in coordinate_system; put that text only in survey_metadata.survey_method. For survey_metadata.property_name, capture the visible value beside labels such as Property, Property Name, Estate, or Name of Property. Do not use owner, surveyed-for, parish, volume/folio, or adjoining-owner text as property_name. Also return the same value in surveyed_property_names when the property value appears in the memorandum. Use point labels only when the label is visibly attached to the boundary point, course table, or coordinate table entry for that exact point. Do not invent sequential labels from printed reference labels: if the plan has reference points A and B but an unlabeled boundary vertex follows A, do not call that vertex B unless B is visibly the same vertex. When an intermediate boundary vertex is unlabeled but is needed to keep the segment chain continuous, use a temporary generated label in the opposite style from the visible labels (lettered plans use 1, 2, 3; numbered plans use A, B, C), set status to review_required, and add review_note 'Generated temporary point label; confirm against visible plan labels.' If boundary labels are visible on the map, use those visible labels exactly and do not generate replacements. For bearings, preserve the complete quadrant bearing exactly when readable, including quadrant letters, degrees, minutes, seconds when present, and final direction, for example S84°56'E or N19°09'E. Do not return partial bearings such as S84 or N82; use null with a review note if the full bearing is unreadable. If a boundary point coordinate is not printed but can be calculated from printed anchored coordinates plus visible bearings and distances, include it in derived_points with status 'derived', confidence at or below 0.65, and a review_note explaining the derivation. Use null when uncertain. Do not invent values. For Volume/Folio, recognize these labels and abbreviations: Vol., Volume, Folio, Fol., Vol/Fol, Volume/Folio, Vol./Fol.. Return each detected pair as survey_metadata.volume_folio. Treat Occ. or Occ as Occupant in party or owner roles. For region-first MEMORANDUM table extraction, detect the memorandum region/table before reading labels and values. For memorandum fields return raw_value, normalized_value, source_page, source_zone, confidence, and semantic_state. Allowed semantic_state values are VALUE, NONE, N_A, NOT_STATED, NOT_FOUND, ILLEGIBLE, NO_ONE_APPEARED, UNKNOWN. Do not collapse blank cells, missing labels, illegible OCR, explicit None, explicit N/A, or No one appeared into the same null value. Parse area into numeric value and unit when readable. Keep memorandum instrument/check evidence distinct from GPS or remarks text. Preserve row boundaries for interested parties and appeared parties, and preserve surveyor certification name, title, and organization. Extraction profile: {profile}.
```

Runtime defaults:

- API endpoint: `https://api.openai.com/v1/chat/completions`
- Model default: `gpt-4.1-mini`, overridable by `OPENAI_MODEL`
- Extraction profile default: `balanced`, overridable by `OPENAI_EXTRACTION_PROFILE`
- Response mode: JSON object
- Temperature: `0`
- Max completion tokens: `4000`
- Secret input: configured add-in environment variable is copied into child process as `OPENAI_API_KEY`

## Memorandum Semantic Contract

Story 4.12 made PE/PXA memorandum extraction generic across matching survey-plan documents rather than tied to `DOC_PLAN_490449_s.pdf`.

Allowed semantic states:

- `VALUE`: a real captured value exists.
- `NONE`: the document explicitly says none.
- `N_A`: the document explicitly says N/A or not applicable.
- `NOT_STATED`: the expected label or cell exists but is blank.
- `NOT_FOUND`: the expected label or zone was not found.
- `ILLEGIBLE`: the zone exists but OCR/image evidence cannot be read confidently.
- `NO_ONE_APPEARED`: appeared-party evidence explicitly says no one appeared.
- `UNKNOWN`: extraction cannot confidently classify the state.

The C# review layer evaluates these states through:

`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/PxaMemorandumReviewRuleService.cs`

The service name and `pxa_memorandum_*` rule IDs remain for compatibility, but the rule scope is PE/PXA survey-plan memorandum behavior.

## Refactor Candidates

No urgent behavior refactor was identified during this review. The strongest improvement is auditability:

1. Move `_prompt(profile)` text into a versioned prompt file or JSON prompt catalog.
2. Add a small prompt loader so the adapter can record prompt version/profile in `extraction_review_data.json` and `survey_plan_extraction_summary.json`.
3. Keep the Python tests that assert required prompt clauses, but update them to load the external prompt asset.
4. Consider renaming `PxaMemorandumReviewRuleService` behind a compatibility wrapper, for example `SurveyPlanMemorandumReviewRuleService`, when a future story can absorb the churn.
5. Consider moving OpenAI request construction behind a provider interface if another vision provider or Responses API migration is planned.

## Verification Anchors

Relevant existing verification:

- `src/ProcessingTools/tests/test_survey_plan_ocr_vision_extraction.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ExtractionReviewPersistenceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/DocumentTypeCatalogLoaderTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/PreflightRuleCatalogLoaderTests.cs`
- Story-specific verification record: `_bmad-output/implementation-artifacts/4-12-improve-pe-pxa-memorandum-extraction-semantic-review-rules.md`

