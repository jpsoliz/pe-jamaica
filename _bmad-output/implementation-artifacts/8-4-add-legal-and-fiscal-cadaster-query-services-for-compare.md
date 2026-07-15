---
baseline_commit: handoff-2026-07-14
---

# Story 8.4: Add Legal And Fiscal Cadaster Query Services For Compare

Status: review

## Story

As a cadastral examiner in Compare,  
I want to query legal cadaster ownership and fiscal cadaster neighbor records from the selected transaction,  
so that I can validate survey plan ownership, volume/folio references, and boundary-neighbor rights against registered data.

## Business Context

Compare depends on external evidence sources. The survey plan may provide parcel ID, volume/folio, owner names, adjacent owners, and boundary notes. Legal cadaster is the authority for registered ownership. Fiscal cadaster is useful for neighbor/context validation but must not be treated as legal ownership proof.

This story creates query seams and normalized evidence models. It should be implemented so live service details can be configured without changing the Compare UI.

## Acceptance Criteria

1. Given Compare has survey plan parcel metadata, when the user queries by parcel ID, then the add-in requests legal cadaster owner/parcel records using a mockable service boundary.
2. Given Compare has volume/folio metadata, when the user queries by volume/folio, then the add-in requests legal cadaster records by volume and folio using a mockable service boundary.
3. Given transaction-scoped working polygons are loaded, when the user requests fiscal neighbors, then the add-in identifies neighboring fiscal cadaster parcels from configured fiscal cadaster layers/services.
4. Given legal cadaster query results are returned, when the evidence panel renders, then owner name, parcel ID, volume/folio, title/record identifiers, source timestamp, and confidence/status are shown.
5. Given fiscal cadaster query results are returned, when the evidence panel renders, then neighbor parcel IDs, spatial relationship, side/boundary if available, owner/taxpayer display if allowed, and source timestamp are shown separately from legal records.
6. Given a service returns no records, when the query completes, then the UI shows `No record returned` as a reviewable discrepancy rather than a generic failure.
7. Given a service fails due to auth, timeout, invalid config, or unavailable endpoint, when the query completes, then the UI shows a retryable non-secret diagnostic and preserves previously loaded evidence.
8. Given legal and fiscal records disagree, when comparison runs, then the discrepancy is recorded with evidence source labels and does not auto-approve Compare.
9. Given sensitive values are returned, when diagnostics/artifacts are written, then tokens, passwords, and raw unauthorized responses are redacted.
10. Given automated tests run, then query request construction, no-record behavior, service failure behavior, source labeling, and discrepancy detection are covered with mocks.

## Tasks / Subtasks

- [x] Define Compare evidence models. (AC: 1-10)
  - [x] Add normalized records for survey plan evidence, legal cadaster result, fiscal cadaster neighbor result, and discrepancy.
  - [x] Include evidence source, queried-at timestamp, query key, and non-secret diagnostics.
  - [x] Keep legal and fiscal source types distinct in the model.

- [x] Add legal cadaster query service. (AC: 1-2, 4, 6-10)
  - [x] Create an interface such as `ILegalCadasterQueryService`.
  - [x] Support parcel ID query.
  - [x] Support volume/folio query.
  - [x] Add mock implementation for development/tests.
  - [x] Add live adapter only if the endpoint contract is known; otherwise add a config-ready placeholder with clear unsupported diagnostics.

- [x] Add fiscal cadaster neighbor query service. (AC: 3, 5-10)
  - [x] Create an interface such as `IFiscalCadasterQueryService`.
  - [x] Support neighbor lookup from loaded working polygon geometry or extent.
  - [x] Support configured layer/service source.
  - [x] Add mock implementation for development/tests.

- [x] Add configuration support. (AC: 1-10)
  - [x] Add settings for legal cadaster source, fiscal cadaster source, query fields, timeout, and enabled flags.
  - [x] Validate config without requiring live service access in unit tests.
  - [x] Keep credentials out of `WorkflowSettings.json`.

- [x] Add comparison logic. (AC: 6-8)
  - [x] Compare plan owner against legal owner.
  - [x] Compare plan volume/folio against legal record.
  - [x] Compare boundary-adjacent labels against fiscal neighbor context.
  - [x] Produce discrepancy records for no match, mismatch, ambiguous match, service unavailable, and stale/unknown evidence.

- [x] Add tests. (AC: 1-10)
  - [x] Legal parcel ID query request/response mapping.
  - [x] Legal volume/folio query request/response mapping.
  - [x] Fiscal neighbor query mapping.
  - [x] No-record discrepancy.
  - [x] Legal/fiscal mismatch discrepancy.
  - [x] Redaction tests.

## Developer Notes

Likely source metadata already available from Compute review models:

- Parcel IDs / parcel names
- Volume/Folio rows
- Adjacent owners
- Boundary segment adjacent owner values

Relevant existing files:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Enterprise/PortalAuth`

UX rule: fiscal cadaster is context for neighbor review; legal cadaster is owner-rights authority. Do not combine them into one generic “owner match” flag.

## UX References

- Ownership evidence panel in `mockups/compare-workspace-evidence-reconciliation.html`
- EXPERIENCE Product-Specific UX Rules for Compare.

## Testing Notes

Run:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj
```

## Open Questions

- Confirm exact legal cadaster API/layer contract.
- Confirm exact fiscal cadaster API/layer contract.
- Confirm which owner/taxpayer fiscal fields are allowed to display in Compare.

## Dev Agent Record

### Implementation Plan

1. Add normalized Compare evidence/query/result models and source-labeled discrepancy support.
2. Add legal and fiscal cadaster service interfaces with mock implementations and unsupported live placeholders.
3. Wire query actions into the Compare workspace without making the geometry map editable.
4. Add configuration fields for future live cadaster services while keeping credentials out of settings.
5. Cover request mapping, no-record, mismatch, source labeling, failure, and redaction behavior in tests.

### Debug Log

- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare"` passed 24 compare-filtered tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build` passed 382 tests.
- Post-review patch: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 errors and one pre-existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- Post-review patch: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "compare"` passed 26 tests.
- Post-review patch: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build` passed 384 tests.

### Completion Notes

- Added legal cadaster query by parcel ID and by volume/folio through a mockable service boundary.
- Added fiscal neighbor query seams using the loaded Compare working geometry scope.
- Added survey plan evidence extraction from persisted review artifacts and Compare discrepancy creation for no records, mismatches, ambiguous/service-unavailable results, and fiscal neighbor context gaps.
- Added redaction for tokens, passwords, bearer/authorization values, and raw unauthorized diagnostics.
- Added disabled-by-default cadaster configuration placeholders in `WorkflowSettings.json`; live adapter contracts remain open until the legal/fiscal service endpoints are confirmed.
- Patched review findings by wiring Compare cadaster service factories into the production ShellState window launch and by splitting legal/fiscal command gates so legal evidence remains queryable when geometry/map load is unavailable.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareEvidenceComparisonService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareEvidenceModels.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareSurveyPlanEvidenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareCadasterQueryServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `_bmad-output/implementation-artifacts/8-4-add-legal-and-fiscal-cadaster-query-services-for-compare.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-07-15 | 1.0 | Implemented legal/fiscal Compare query seams, evidence extraction, discrepancy comparison, configuration, and test coverage. | Codex |
| 2026-07-15 | 1.1 | Patched review findings for production service wiring and independent legal evidence query gating. | Codex |
