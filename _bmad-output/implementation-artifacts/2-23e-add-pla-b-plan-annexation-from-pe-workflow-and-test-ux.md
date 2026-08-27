---
baseline_commit: fe14dab
parent_story: 2-23-add-pla-plan-annexation-pdf-selection-extraction-and-review.md
depends_on: 2-23d-add-pla-visual-comparison-and-finalize-upload-flow.md
---

# Story 2.23E: Add PLA_B Recovery From PE Working Review And GDB

Status: review

## Story

As an SMD examiner evaluating a second Plan Annexation approach,
I want a separate PLA_B test workflow that loads only the PE-derived geometry evidence,
so that we can compare the alternate approach without reusing PLA_A extraction, survey-diagram, or finalize UX.

## Business Context

PLA_A remains the existing `pla_plan_annexation` workflow: plan-annexation PDF evidence selection, extraction/review, local-origin geometry, visual comparison, and later finalize behavior.

PLA_B is conceptually different. For initial testing it does not run PLA_A document selection, OCR/extraction, survey-diagram crop, or finalize/upload behavior. It only needs two manual/test values:

- `Current TR`: the current PLA transaction number used to name/group the current review context.
- `PE Number`: the related PE transaction number; values may include `PE-`, but PLA_B strips the prefix and uses only the numeric value.

For the initial PLA_B test, preparation must load/recover geometry evidence from:

- Enterprise `working_review`, searched by the stripped PE number using configured field `transaction_number`.
- The related PE transaction `survey_plan` ZIP package, extracting only `{pe_number}_parcel_output.gdb`.

The map/review content is separated into two groups: the current PLA transaction group containing the Enterprise working-review parcel evidence, and the PE group containing layers from the resolved PE output GDB. PLA_B preparation also downloads the current PLA transaction source files into the current TR case folder so the existing Supporting Documents PDF viewer can render them. The PE output GDB `mgeo_overlay_[trnumber]` raster dataset, plus equivalent `m-geo`/`m_geo`/`mgeo` layer names, must be added to the map at 70% transparency. The form also includes a separate control to launch the viewer for the current TR. Finalization will be handled by a later, separate form/story.

## Acceptance Criteria

1. Given PLA_A still uses `workflow_profile = pla_plan_annexation`, when PLA_B is added, then PLA_A routing and UX remain unchanged.
2. Given PLA_B uses `workflow_profile = pla_b_plan_annexation_from_pe`, then PLA_B is configured as a distinct recovery profile and does not require PLA_A plan-annexation PDF evidence.
3. Given a PE number such as `PE-100000630`, when PLA_B normalizes it, then it uses `100000630`.
4. Given a numeric PE number such as `100000630`, when PLA_B normalizes it, then it preserves `100000630`.
5. Given a blank or unsupported PE number, when PLA_B preparation runs, then it blocks with a clear non-secret validation message.
6. Given the stripped PE number is available, when Enterprise working-review lookup is planned, then it searches configured `working_review` geometry layers by `transaction_number = {pe_number}`.
7. Given the stripped PE number is available, when the related PE transaction is searched, then Innola lookup uses only the stripped numeric PE value.
8. Given the related PE transaction contains a `survey_plan` ZIP/GDB package, when package preparation runs, then the ZIP is downloaded under the case/work area and only `{pe_number}_parcel_output.gdb` is resolved.
9. Given the PE package is missing, corrupt, unsafe, or lacks the matching GDB, when package preparation runs, then PLA_B reports a retryable non-secret diagnostic and preserves local artifacts.
10. Given PLA_B map review preparation succeeds, then planned contents include one group for the current PLA transaction and one group for the related PE number.
11. Given the current PLA group is created, then it contains Enterprise `working_review` parcel evidence filtered by the stripped PE number.
12. Given the PE group is created, then it contains loadable standalone feature classes, feature-dataset feature classes, and root raster datasets from `{pe_number}_parcel_output.gdb`.
13. Given the examiner is logged in, when they open the Transaction List, then the `[PA]` test button is enabled.
14. Given the examiner clicks `[PA]`, then a separate PLA_B test form opens with `Current TR` and `PE Number` fields.
15. Given the examiner enters both values and clicks `Prepare`, then the form downloads the current TR source files and loads the PLA_B recovery groups without opening the PLA_A/Parcel Workflow pane.
16. Given the examiner clicks `Open Viewer`, then the current PLA transaction source files are downloaded into that TR case folder and the existing Supporting Documents/PDF viewer opens for that current TR without applying PLA_A/compute transaction-type validation.
17. Given the PE output GDB contains an `mgeo_overlay_[trnumber]` raster dataset or an `m-geo`, `m_geo`, or `mgeo` layer, when PLA_B adds it to the map, then it is displayed with 70% transparency.
18. Given initial PLA_B testing is active, then no survey-diagram crop, PLA_A extraction review, or PLA_B finalize/upload UX is required or shown.
19. Given a current TR has mixed Innola attachments, when one non-system source attachment cannot be downloaded or cannot be viewed, then PLA_B skips that attachment with a diagnostic and continues downloading the remaining source files; the operation fails only if no viewable source file is available in `[TR]/source`.

## Tasks / Subtasks

- [x] Keep PLA_B separate from PLA_A. (AC: 1-2, 18)
  - [x] Preserve `pla_plan_annexation` behavior for PLA_A.
  - [x] Configure `pla_b_plan_annexation_from_pe` as a recovery profile with no required PLA_A document source.
  - [x] Hide PLA_B test controls from the main Parcel Workflow pane.
  - [x] Remove PLA_B from the existing completion/finalize upload path for this story.

- [x] Add PE-number and recovery planning services. (AC: 3-12, 17)
  - [x] Normalize PE number by stripping optional `PE-` prefix.
  - [x] Plan Enterprise `working_review` lookup by configured `transaction_number` field.
  - [x] Plan related PE transaction lookup with stripped numeric PE value.
  - [x] Download and safely unzip related PE `survey_plan` package.
  - [x] Resolve only `{pe_number}_parcel_output.gdb`.
  - [x] Build deterministic current PLA and PE map group names.
  - [x] Enumerate PE output GDB feature classes inside feature datasets as well as standalone feature classes.
  - [x] Enumerate PE output GDB root raster datasets such as `mgeo_overlay_[trnumber]`.
  - [x] Apply 70% transparency when loading the PE output GDB `mgeo_overlay_[trnumber]`/m-geo layer.

- [x] Add Transaction List test UX. (AC: 13-16)
  - [x] Add `[PA]` button to the Transaction List toolbar.
  - [x] Enable `[PA]` only after Innola login and while the panel is not loading.
  - [x] Add a separate PLA_B test input form with Current TR and PE Number fields.
  - [x] Make Prepare validate, download current TR source files, and load the PLA_B recovery groups without starting the normal workflow.
  - [x] Add an Open Viewer action that loads current TR source files into the TR case folder and opens the existing Supporting Documents/PDF viewer.
  - [x] Ensure current TR source loading is source-only and does not reject non-PLA_A transaction types such as `First Registration`.
  - [x] Skip individual failed or non-viewable current TR attachments while preserving a concise diagnostic.

- [x] Add focused tests. (AC: 1-18)
  - [x] Cover PE normalization and invalid values.
  - [x] Cover recovery profile/rule configuration.
  - [x] Cover Enterprise working-review lookup planning.
  - [x] Cover related PE lookup and GDB package resolution.
  - [x] Cover Transaction List `[PA]` enablement and prepare behavior.
  - [x] Cover current TR viewer action source download without starting the PLA_A workflow pane.
  - [x] Cover current TR source download continuing after an individual attachment failure.
  - [x] Cover PE GDB feature-dataset scanning, raster dataset scanning, and `mgeo_overlay_[trnumber]`/m-geo 70% transparency behavior.

## Dev Notes

- PLA_B initial testing must not use PLA_A’s `PlaPlanEvidenceSelectionService`, extraction review flow, survey-diagram crop UX, or finalize/upload flow.
- `Current TR` is for naming/grouping the current PLA review context.
- Current TR source documents are still loaded into the normal TR case folder so the existing Supporting Documents/PDF viewer can render them.
- Current TR source download is PLA_B-specific and must not call the normal Parcel Workflow loader, because that path enforces PLA_A/compute transaction-type support.
- Current TR source download is best-effort across non-system viewable attachments; one bad attachment must not block the viewer if another source file can be downloaded.
- `PE Number` drives both Enterprise `working_review` lookup and related PE GDB discovery after stripping optional `PE-`.
- Enterprise working-review search field is `transaction_number`.
- Expected PE output GDB name is `{pe_number}_parcel_output.gdb`.
- The PE output GDB layer named `mgeo_overlay_[trnumber]` is a raster dataset in tested GDBs and must be discovered from root raster datasets and receive 70% transparency when added to the map; legacy/equivalent `m-geo`, `m_geo`, and `mgeo` names remain supported.
- Finalize will be implemented later as a separate form/story.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release /p:UseSharedCompilation=false`: passed with one pre-existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `.\src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Release\net8.0-windows\ParcelWorkflowAddIn.Tests.exe "PLA_B" "pla b"`: passed 25 focused PLA_B tests.
- `.\src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Release\net8.0-windows\ParcelWorkflowAddIn.Tests.exe`: partial pass, then stopped at an existing ArcGIS SDK assembly-load limitation for a spatial-overlap test outside ArcGIS Pro.
- `tools/package_addin.ps1 -Configuration Release`: passed; produced `ParcelWorkflowAddIn.esriAddInX` and bumped add-in patch version to `1.1.258`.

### Completion Notes List

- Corrected story scope after user clarification: PLA_B is recovery-only and not a variant of PLA_A document/extraction/finalize UX.
- Added `[PA]` button and separate PLA_B test form in Transaction List.
- Changed Prepare to validate, download/extract the PE package GDB, and load recovery groups without opening the normal Parcel Workflow pane.
- Added Open Viewer action on the PLA_B test form to download current TR source files and open the existing Supporting Documents/PDF viewer.
- Changed Prepare to also ensure current TR source files are downloaded and to fail if `[TR]/source` remains empty.
- Added PE output GDB `mgeo_overlay_[trnumber]`/m-geo 70% transparency handling.
- Added PE output GDB feature-dataset scanning so nested `mgeo_overlay_[trnumber]` layers are included in the PE group.
- Added PE output GDB raster dataset scanning so root `mgeo_overlay_[trnumber]` raster overlays are included in the PE group.
- Replaced PLA_B current-source loading with a source-only downloader so `First Registration` and other non-PLA_A transaction types are not blocked by PLA_A profile validation.
- Changed PLA_B current-source downloading to skip failed or non-viewable individual attachments and keep going when at least one usable source file can be downloaded.
- Removed PLA_B requirement for survey-diagram source documents in profile/rule configuration.
- Removed PLA_B from existing normal completion/finalize upload handling for this initial testing story.

### File List

- `_bmad-output/implementation-artifacts/2-23e-add-pla-b-plan-annexation-from-pe-workflow-and-test-ux.md`
- `_bmad-output/implementation-artifacts/investigations/pla-b-attachment-download-failure-investigation.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/PlaBWorkflowServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ComputeTransactionTypeProfileDefinition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/DefaultTransactionCompletionReadinessService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/PlaBTestInputWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/PlaBTestInputWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaBWorkflowServices.cs`

### Change Log

- 2026-08-27: Corrected PLA_B scope to recovery-only; removed PLA_A-style UX/finalize assumptions from story and code path.
- 2026-08-27: Patched PLA_B current-TR source download to tolerate individual attachment failures and preserve diagnostics for the test form.
- 2026-08-27: Expanded PE output GDB transparency handling to include `mgeo_overlay_[trnumber]`.
- 2026-08-27: Fixed PE output GDB enumeration to include feature classes inside feature datasets/output containers.
- 2026-08-27: Fixed PE output GDB enumeration to include root raster datasets such as `mgeo_overlay_[trnumber]`.
