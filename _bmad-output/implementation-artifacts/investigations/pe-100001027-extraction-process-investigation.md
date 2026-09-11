# PE 100001027 Extraction Process Investigation

## Hand-Off Brief

PE transaction 100001027 is a Scenario A computation-sheet workflow using `document (5).pdf` as `computation_sheet` and `document (4).pdf` as `plan_map_reference`. The extraction route reaches the scanned/OCR fallback, but the case artifact records `ocr_vision_unavailable` and zero extracted rows. Current dev installation is wired to repo paths and the Documents case root, while production installer wiring uses `C:\Sidwell\ParcelWorkflow`.

## Case Info

- Date: 2026-09-10
- Case: PE transaction 100001027
- Scope: files used and defined extraction process for PE computation workflow
- Status: Active

## Problem Statement

User reports that Square/Lot 1 for PE produced no extraction even though the scanned computation sheet contains parcel construction points.

## Evidence Inventory

- Confirmed: `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001027\manifest.json` records `case_type = PE`, task `Compute Survey Plan`, source `document (5).pdf` as `computation_sheet`, source type `st_surveysheet`, and `document (4).pdf` as `plan_map_reference`, source type `st_surveyplan`.
- Confirmed: same manifest records detected profile `scenario_a` and transaction type profile `pe_computation_review`.
- Confirmed: same manifest script plan first step is `extract_points_from_computation_pdf` with input role `computation_sheet`; second step is `ocr_plan_map_pdf` with input role `plan_map_reference`.
- Confirmed: `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001027\working\extraction_route.json` records `fallback_reason = no_usable_text_layer`, `text_layer_probe_status = ocr_vision_unavailable`, and `parsed_row_count = 0`.
- Confirmed: `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001027\working\extraction_decision_gate.json` records `LastQualityStatus = weak` and notes zero review rows / no usable coordinate rows.
- Confirmed: current dev installed add-in settings point Python to `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe`, adapters to `D:\Code\BMad-Method\dev\pe-jamaica\src\ProcessingTools\adapters`, and leave `case_folder_output_root` blank.
- Confirmed: blank `case_folder_output_root` defaults to `Documents\SidwellCo\ParcelWorkflowCases`.
- Confirmed: production installer staging targets `C:\Sidwell\ParcelWorkflow`, `C:\Sidwell\ParcelWorkflow\ProcessingTools`, and `C:\Sidwell\ParcelWorkflow\ParcelWorkflowCases`.

## Source Code Trace

- PE profile definition: `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`.
- Source role mapping: `st_surveysheet -> computation_sheet`, `st_surveyplan -> plan_map_reference`.
- Workflow rule: `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRules.json`, `scenario_a_two_pdf_v1`.
- Runtime settings loader: `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/WorkflowExecutionSettings.cs`.
- Case folder settings loader: `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`.
- Extraction adapter: `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`.
- Text-first parser: `src/ProcessingTools/adapters/pdf_text_structured_extraction.py`.
- Scanned OCR parser: `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`.
- Production package copy: `deployment/target-computer-tools/package/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`.

## Defined PE Procedure

1. Load PE Innola transaction.
2. Download/copy source attachments to the case folder.
3. Map `st_surveysheet` to `computation_sheet`.
4. Map `st_surveyplan` to `plan_map_reference`.
5. Detect Scenario A because both roles are present.
6. Resolve workflow rule `scenario_a_two_pdf_v1`.
7. Run `extract_points_from_computation_pdf`.
8. Attempt embedded text extraction with `pdf_text_structured_extraction.py`.
9. If embedded text succeeds, write review rows directly.
10. If embedded text returns `no_usable_text_layer`, route to scanned OCR/vision.
11. Run `survey_plan_ocr_vision_extraction.py` with profile `survey_table_vision_v1`.
12. Write `working/extraction_review_data.json`.
13. Decision gate checks row count / coordinate rows.
14. If rows are usable, Validate Points and Lines can load parcels.
15. If rows are zero, decision gate marks extraction weak and recommends manual/reprocess.

## Findings

- Confirmed: The current failure is after PE route resolution, not before it. Source files and Scenario A detection are correct.
- Confirmed: The computation source is a scanned PDF with no embedded text layer; text-first extraction cannot extract it.
- Confirmed: The OCR/vision fallback was invoked for 100001027 but returned unavailable, leaving zero rows.
- Deduced: Validate Points and Lines cannot load parcels because `working/extraction_review_data.json` contains `rows: []` and `segments: []`.
- Deduced: In the dev environment, deleting `C:\Sidwell\ParcelWorkflow\ParcelWorkflowCases\100001027` would not reset this case because the active dev case root is `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases`.

## Open Questions

- Why did the OCR provider return `ocr_vision_unavailable` in the ArcGIS Pro process? Direct command-line retry under sandbox showed a socket permission error; validating the external OpenAI call requires explicit user authorization to send the PDF externally.
- Did ArcGIS Pro reload add-in version 1.1.490 after rebuild, or was it still holding an older add-in assembly?

## Next Diagnostics

- After restarting ArcGIS Pro and deleting the dev case folder, rerun transaction 100001027 and inspect `working/extraction_route.json`.
- If it still says `ocr_vision_unavailable`, inspect the fallback reason from `working/computation_ocr_extraction_summary.json`.
- If the fallback reason is network/socket/OpenAI, fix environment/network/API key rather than extraction code.

## Follow-up: 2026-09-10

### Current Rerun Evidence

- Confirmed: current case folder only contains `preflight_summary.json`, `structure_check_summary.json`, and `workflow_lifecycle_audit.json` under `working`.
- Confirmed: current case folder does not contain `georeference_check_summary.json`, `dimension_check_summary.json`, `extraction_route.json`, or `extraction_review_data.json`.
- Confirmed: current manifest records `workflow_state = preflight_blocked`.
- Confirmed: `preflight_summary.json` records `payload.status = blocked` while `payload.blockers = []`.
- Confirmed: `WorkflowSession.WriteLegacyPreflightSummaryIfPossible` writes combined status `blocked` when Structure, Georeference, and Dimension have not all passed, even if the current blockers list is empty.
- Confirmed: `WorkflowSession.CanRunExtractionReviewState` allows extraction only from `PreflightPassed`, `ExtractionFailed`, `ReviewPending`, or `ReviewManualPending` for this PE workflow.
- Confirmed: both current PE source PDFs are image-only: `document (5).pdf` has two pages and zero extracted text; `document (4).pdf` has one page and zero extracted text.
- Confirmed: `ReviewSourceSelectionResolver` defaults source viewing to computation sheet first, then coordinate text, then plan/map reference, but a user-selected source can keep the plan PDF visible.

### Updated Findings

- Confirmed: the latest "no results" state is not an OCR failure yet; extraction has not run in this fresh case because the workflow is still blocked at early checks.
- Confirmed: the visible survey plan on screen is not itself proof extraction ran. It is a source document/viewer surface.
- Deduced: for PE/Scenario A, `document (5).pdf` is the primary extraction input. `document (4).pdf` is the plan/map reference and does not produce parcel rows by itself.
- Deduced: to reach extraction, the operator must run all early checks: Structure Check, Georeference Check, and Dimension Check. Only then should Validate Points and Lines run `extract_points_from_computation_pdf`.

### Current Root Cause For No Results

The current case has no results because no extraction artifacts exist. The workflow is still `preflight_blocked` due to missing Georeference/Dimension pass state, not because Square/Lot 1 was extracted incorrectly.
