---
baseline_commit: handoff-2026-08-24
parent_story: 2-23-add-pla-plan-annexation-pdf-selection-extraction-and-review.md
---

# Story 2.23A: Add PLA Transaction Profile, Source Type, And Document Type Resolution

Status: done

## Story

As an SMD examiner working a PLA Plan Annexation transaction,
I want the add-in to recognize PLA as its own supported transaction profile with the correct required plan-annexation PDF source,
so that PLA cases enter the right workflow instead of being routed through PE or PXA behavior.

## Business Context

PLA is a new Innola transaction type for Plan Annexation. Its input document is not a PE computation sheet and must not be treated as a PXA survey-plan PDF by accident. The required source type for the attached plan annexation PDF is `st_plan_annexation_pdf`.

The final generated-output document/source types for PLA are explicit Innola PRO-stage source types. A PLA transaction may produce up to three output documents, and Finalize must attach them with these output source types in order:

1. `st_plan_annex_output`: the examiner-selected page extracted from the input `st_plan_annexation_pdf` document. If the user selected page 2, only page 2 is emitted as this output attachment.
2. `st_plan_annex_output2`: the generated geometry output produced from the reviewed bearings/distances in the selected plan-annexation PDF page.
3. `st_plan_annex_output3`: reserved; not yet defined and must not be generated or attached until a later requirement defines the document.

These are distinct from the required input source type `st_plan_annexation_pdf`.

## Acceptance Criteria

1. Given an Innola transaction has type/code/name `PLA` or `Plan Annexation`, when the transaction is loaded, then the add-in treats it as a supported transaction type and assigns it to a distinct PLA workflow profile.
2. Given supported transaction types are loaded from configuration, then PLA appears as another supported option alongside existing PE/PXA profiles without replacing them.
3. Given a PLA transaction is loaded, when source attachments are copied into the case folder, then the required plan annexation document/source type is `st_plan_annexation_pdf`.
4. Given the required `st_plan_annexation_pdf` attachment is missing, unreadable, or not a PDF, when Supporting Document Check or Structure Check runs, then the workflow reports a blocking finding and does not silently route through PE/PXA extraction.
5. Given Finalize preparation needs to save generated PLA output documents back to Innola, then the add-in uses PLA output source types `st_plan_annex_output`, `st_plan_annex_output2`, and `st_plan_annex_output3` in document order rather than any PE/PXA completed-package source type.
6. Given fewer than three generated PLA output documents exist, when Finalize preparation resolves output attachment types, then only the corresponding first N PLA output source types are used; if more than three output documents exist, the workflow reports a clear blocking diagnostic instead of reusing or inventing a source type.
7. Given existing PE, PXA, M-Geo, Compare, and title-plan image-placement workflows exist, when PLA routing/configuration is added, then those workflows keep their current routing, source types, stage gates, and artifacts.
8. Given automated tests run, then coverage proves PLA profile routing, required `st_plan_annexation_pdf` source behavior, explicit PLA output source type ordering/limits, missing/unreadable source blocking, and PE/PXA non-regression.

## Tasks / Subtasks

- [x] Add PLA transaction routing and source requirements. (AC: 1-4, 7)
  - [x] Add `PLA` / `Plan Annexation` to supported transaction type handling.
  - [x] Add a PLA workflow profile with required source type `st_plan_annexation_pdf`.
  - [x] Keep PLA separate from PE computation-sheet and PXA survey-plan PDF profile matching.
  - [x] Add/update Supporting Document Check and Structure Check rules for missing/unreadable PLA plan PDFs.

- [x] Add PLA document/source type resolution for generated output. (AC: 5-6)
  - [x] Resolve PLA generated-output attachment types as `st_plan_annex_output`, `st_plan_annex_output2`, and `st_plan_annex_output3`.
  - [x] Map output documents to the corresponding PLA output source types in order: selected source page, generated geometry, then reserved undefined output.
  - [x] Block Finalize preparation with a clear non-secret diagnostic if more than three generated PLA output documents must be attached.
  - [x] Ensure generated PLA output documents never use PE/PXA completed-package source types.

- [x] Add tests and fixture setup. (AC: 1-8)
  - [x] Add fixture copies or redacted equivalents for `1000-55.pdf` and `1150-100.pdf` under the repo fixture tree, if licensing/privacy allows.
  - [x] Test PLA transaction profile routing.
  - [x] Test required `st_plan_annexation_pdf` source type behavior.
  - [x] Test missing/unreadable/not-PDF blocking findings.
  - [x] Test explicit PLA output document source type ordering and the more-than-three blocking diagnostic.
  - [x] Test PE/PXA routing is unchanged.

### Review Findings

- [x] [Review][Patch] PLA output source-type contract must use explicit PRO-stage output types — resolved by replacing the generic resolver concept with ordered source types `st_plan_annex_output`, `st_plan_annex_output2`, and `st_plan_annex_output3`, plus a more-than-three blocking diagnostic.
- [x] [Review][Patch] PLA plan-annexation PDFs still produce an incomplete intake profile [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceInputProfileDetector.cs:38] — resolved by adding the `pla_plan_annexation` detected source input profile.
- [x] [Review][Patch] PLA transaction profile has no matching workflow rule/script plan [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRules.json:188] — resolved by adding `pla_plan_annexation_v1`.
- [x] [Review][Patch] PLA output resolver accepts generated-output source types even when configured as required/external input sources [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/PlaOutputDocumentSourceTypeResolver.cs:33] — resolved by requiring internal, optional, PDF-capable generated-output definitions.

## Dev Notes

Likely source areas to inspect and preserve:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/StructureRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ComputeAttachmentSourceTypeCatalog.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/*`

Preserve these constraints:

- Do not route PLA through PE computation-sheet extraction.
- Do not hardcode PLA as PXA.
- Do not bypass existing settings/catalog loading patterns.
- Do not log Innola tokens, passwords, raw authorization responses, or certificate material.

## References

- `_bmad-output/project-context.md`
- Parent story: `_bmad-output/implementation-artifacts/2-23-add-pla-plan-annexation-pdf-selection-extraction-and-review.md`
- Sample evidence: `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\ScannedImages\1000-55.pdf`
- Sample evidence: `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\ScannedImages\1150-100.pdf`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "pla"` initially failed as expected on missing `SourceRole.PlanAnnexationPdf` and missing `PlaOutputDocumentSourceTypeResolver`.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "plan annexation"` passed 5 tests.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "pla output document type resolver"` passed 3 tests.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "checked in config includes pla"` passed 1 test.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "manifest preflight"` passed 27 tests.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "innola"` passed 157 tests.
- Full regression command `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build` stopped at unrelated existing test `pxa review xaml uses tab scoped commands` / `PxaReviewExposesMemorandumRuleGroups`.
- Post-review patch: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with one existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- Post-review patch: `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "source input profile"` passed 12 tests.
- Post-review patch: `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "workflow rule resolver"` passed 8 tests.
- Post-review patch: `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "pla output document type resolver"` passed 3 tests.
- Post-review patch: `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "checked in config includes pla"` passed 1 test.
- Post-review patch: `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "manifest preflight pla"` passed 4 tests.
- Post-review patch: `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "innola live detail classifies pla"` passed 1 test.
- Broad `dotnet ... ParcelWorkflowAddIn.Tests.dll "pla"` filter passed the new PLA routing/preflight checks, then stopped at unrelated existing `SurveyPlanBoundarySolverTests.RebuildKeepsConflictingPrintedReferenceCoordinates`.

### Completion Notes List

- Added a first-class `plan_annexation_pdf` source role and safe-default PLA transaction profile (`pla_plan_annexation`) for `PLA` / `Plan Annexation`.
- Added checked-in `WorkflowSettings.json` support for PLA transaction types, `st_plan_annexation_pdf`, a PLA profile, and explicit PRO-stage output source types `st_plan_annex_output`, `st_plan_annex_output2`, and `st_plan_annex_output3`.
- Added PLA attachment classification by exact/configured source type and annexation wording while preserving generic PDF plan-map classification.
- Added a dedicated `pla_plan_annexation` detected input profile so PLA PDFs do not fall into incomplete PE/PXA intake checks.
- Added `pla_plan_annexation_v1` workflow rule/script plan routing for the PLA transaction profile.
- Added preflight coverage so PLA requires only the plan annexation PDF and blocks missing, missing-file/unreadable, or non-PDF PLA sources without requiring PE/PXA roles.
- Updated `PlaOutputDocumentSourceTypeResolver` to return the explicit ordered PLA output source types for one, two, or three generated documents, reject invalid required/external configuration, and block more than three outputs.
- Did not copy the external sample PDFs into repo fixtures in this routing/config slice; tests use synthetic case-folder sources and do not depend on the Dropbox evidence files.

### File List

- `_bmad-output/implementation-artifacts/2-23a-add-pla-transaction-profile-source-type-and-doc-type-resolution.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceInputProfile.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceInputProfileDetector.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceRole.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ComputeAttachmentSourceTypeCatalog.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ComputeTransactionTypeProfileDefinition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/PlaOutputDocumentSourceTypeResolver.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Intake/SourceInputProfileDetectorTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionDetailServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ManifestPreflightServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/WorkflowRules/WorkflowRuleResolverTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-08-24 | 1.2 | Patched review findings: added PLA detected profile, workflow rule, explicit ordered output resolver/types, and focused regression tests. | Codex |
| 2026-08-24 | 1.1 | Clarified PLA output document/source type contract as `st_plan_annex_output`, `st_plan_annex_output2`, and `st_plan_annex_output3`; replaced fetch/cache ambiguity with explicit ordered output type behavior. | JotaPe/Codex |
| 2026-08-24 | 1.0 | Implemented PLA transaction profile, source type mapping, output document type resolution, preflight blocking, and regression coverage. | Codex |
