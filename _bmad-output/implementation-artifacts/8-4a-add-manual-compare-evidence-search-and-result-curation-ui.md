---
baseline_commit: handoff-2026-07-14
---

# Story 8.4A: Add Manual Compare Evidence Search And Result Curation UI

Status: review

## Story

As a cadastral examiner in Compare,  
I want to manually search Innola/cadaster evidence by PID, Volume/Folio, Land Val No., and Name with Parish filtering,  
so that I can collect owner, occupant, in-possession, and neighbor evidence even when the survey plan extraction does not provide enough query values.

## Business Context

Story 8.4 added service seams for legal and fiscal cadaster evidence, but the current Compare UI still uses extracted survey-plan values. The examiner needs direct control of the search criteria. Compare must support investigation-style evidence gathering: run a query, review all returned records, mark useful findings, classify the party role, and preserve the evidence trail for the final Compare decision.

Live Innola service endpoints are not yet confirmed, so this story should implement the UI, view-model state, query request models, mock/unsupported service seams, and persistence behavior without hard-coding final endpoint paths.

## Current State To Preserve

- Compare is a two-panel workspace: left source documents, right ownership evidence and decision.
- Working geometry is loaded into the active ArcGIS Pro map, not embedded in the form.
- The right-side geometry strip only exposes `Show active map` and `Refresh`.
- Existing query buttons call `QueryParcelIdAsync()` and `QueryVolumeFolioAsync()` using extracted survey-plan values from `CompareSurveyPlanEvidence`.
- Existing services are mockable and live adapters remain unsupported until endpoint contracts are known.
- Fiscal cadaster evidence is context, not legal ownership proof.

## Acceptance Criteria

1. Given the Compare workspace is open, when the Ownership Evidence panel renders, then it provides manual search modes for `PID`, `Volume/Folio`, `Land Val No.`, and `Name`.
2. Given the user selects `PID`, when they enter a PID and run search, then the value typed by the user is used for the query instead of only the extracted survey-plan parcel ID.
3. Given the user selects `Volume/Folio`, when they enter volume and folio values and run search, then both fields are required and used together for the query.
4. Given the user selects `Land Val No.`, when they enter a land valuation number and run search, then the value is captured in the query model and routed through a mockable service method.
5. Given the user selects `Name`, when they enter a person/company name, then the UI also provides a Parish field to limit the search before executing the query.
6. Given the user leaves a required field blank, when they attempt to search, then the UI shows a field-specific validation message without calling the service.
7. Given a query returns multiple records, when results render, then all records are displayed in a query-results list and none are silently discarded.
8. Given the user runs multiple searches, when new results arrive, then previous query history and selected evidence are preserved unless the user explicitly clears them.
9. Given a returned result is useful, when the user marks it as valuable, then it is copied or promoted into a retained evidence list used by Compare decision-making.
10. Given a result is marked valuable, when the examiner classifies it, then the evidence supports at minimum these role tags: `Owner`, `Occupant`, `In Possession`, `Neighbor`, and `Other`.
11. Given a retained evidence item exists, when the user saves Compare progress, then the evidence item, source query, role tag, source system, timestamp, and display summary are persisted in the Compare draft artifact.
12. Given the transaction is reopened, when the Compare draft is restored, then retained evidence and query history are restored without re-running the Innola services.
13. Given a service returns no records, when the query completes, then the no-record outcome appears in query history and remains reviewable without clearing existing valuable evidence.
14. Given a service fails due to missing endpoint contract, auth, timeout, invalid config, or unavailable endpoint, when the query completes, then a non-secret diagnostic is shown and existing evidence remains intact.
15. Given legal and fiscal sources both return people or parcel records, when evidence is displayed, then the UI clearly labels the source and does not present fiscal occupant/taxpayer evidence as legal ownership proof.
16. Given automated tests run, then manual input validation, query model construction, result retention, role tagging, draft persistence, and restore behavior are covered.

## UX Requirements

- Replace the current three simple query buttons with a compact search surface in the Ownership Evidence panel.
- Use a segmented control or tab-like selector for query mode:
  - `PID`
  - `Vol/Folio`
  - `Land Val No.`
  - `Name`
- Show only the fields relevant to the selected mode:
  - PID mode: `PID` textbox, optional `Parish` filter if supported by the final service.
  - Vol/Folio mode: `Volume` textbox, `Folio` textbox, optional `Parish` filter.
  - Land Val No. mode: `Land Val No.` textbox, optional `Parish` filter.
  - Name mode: `Name` textbox and `Parish` textbox or selector. Parish should be treated as required unless product confirms broad name search is acceptable.
- Provide one primary `Search` action in the active mode, plus a small `Clear fields` action.
- Keep the results visually separate from the retained evidence list:
  - `Query Results`: latest returned records for the active/manual query history.
  - `Valuable Evidence`: records the examiner explicitly marked for the decision.
- In each result row, include:
  - Person or organization display name.
  - Parcel/PID when present.
  - Volume/Folio when present.
  - Land Val No. when present.
  - Parish when present.
  - Source system/type.
  - Role/status from source when present.
  - Action to `Mark valuable`.
- In each retained evidence row, include:
  - Role tag selector: `Owner`, `Occupant`, `In Possession`, `Neighbor`, `Other`.
  - Source query summary, such as `PID: 100123`, `Vol/Folio: 123/45`, `Land Val No.: 98765`, or `Name: Brown, Parish: Clarendon`.
  - Source label: legal cadaster, fiscal cadaster, Innola service, or configured source name.
  - Option to remove from retained evidence.

## Technical Requirements

- Add view-model properties for manual search fields:
  - `SelectedEvidenceSearchMode`
  - `SearchPid`
  - `SearchVolume`
  - `SearchFolio`
  - `SearchLandValuationNumber`
  - `SearchName`
  - `SearchParish`
  - `SearchValidationMessage`
- Add commands:
  - `RunEvidenceSearchCommand`
  - `ClearEvidenceSearchFieldsCommand`
  - `MarkEvidenceResultValuableCommand`
  - `RemoveValuableEvidenceCommand`
- Extend the query model so it can represent:
  - PID query
  - Volume/Folio query
  - Land Val No. query
  - Name + Parish query
- Do not remove the existing extracted-value query behavior unless it is intentionally replaced by pre-populating the manual fields from survey-plan evidence.
- Prefer pre-populating PID and Volume/Folio fields from extracted survey-plan evidence when available, while still allowing the user to edit them before search.
- Keep services mockable. If live service contracts remain unknown, unsupported live adapters must return clear diagnostics but the UI must still function with mocks/tests.
- Ensure diagnostics redact tokens, passwords, bearer values, authorization headers, and raw unauthorized responses.
- Persist retained evidence and query history in `working/compare_review_draft.json` or a compatible Compare draft extension.
- Do not break existing approval gating: Compare approval should still require the configured evidence review and unresolved discrepancy rules.

## Suggested Model Shape

Use names that fit the existing codebase, but the implementation needs these concepts:

```csharp
public sealed record CompareEvidenceSearchRequest(
    string QueryKind,
    string? Pid,
    string? Volume,
    string? Folio,
    string? LandValuationNumber,
    string? Name,
    string? Parish);

public sealed record CompareEvidenceSearchResult(
    string SourceType,
    string SourceLabel,
    string QueryKey,
    string? DisplayName,
    string? PartyRole,
    string? ParcelId,
    string? Volume,
    string? Folio,
    string? LandValuationNumber,
    string? Parish,
    string Status,
    DateTimeOffset QueriedAt,
    string? Diagnostic);

public sealed record CompareValuableEvidence(
    string EvidenceId,
    string SourceType,
    string SourceLabel,
    string QueryKey,
    string DisplaySummary,
    string RoleTag,
    DateTimeOffset CapturedAt,
    string? Diagnostic);
```

## Files Likely To Change

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareEvidenceModels.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareReviewDraftPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareReviewDecision.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareCadasterQueryServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceViewModelTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareReviewDecisionTests.cs`

## Testing Notes

Run:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare"
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build
```

Add tests for:

- Empty PID blocks search and shows validation.
- Empty volume or empty folio blocks Volume/Folio search.
- Empty Land Val No. blocks Land Val No. search.
- Empty name or parish blocks Name search if parish is required.
- PID search uses the typed PID, not only extracted survey-plan PID.
- Volume/Folio search uses typed values.
- Land Val No. search creates the correct query kind/key.
- Name + Parish search creates the correct query kind/key.
- Multiple returned records all appear in query results.
- Marking a record valuable copies it to retained evidence with default role tag.
- Updating role tag persists to draft.
- Reopening Compare restores retained evidence without re-querying.
- Service unavailable preserves previous retained evidence.
- Fiscal evidence cannot be misclassified internally as legal ownership authority.

## Open Questions

- Confirm the final Innola webservice endpoints for PID, Volume/Folio, Land Val No., and Name + Parish searches.
- Confirm whether Parish is required for all query modes or only for Name search.
- Confirm exact allowed role taxonomy beyond `Owner`, `Occupant`, `In Possession`, `Neighbor`, and `Other`.
- Confirm whether retained evidence should be written only to the local Compare draft or also to an Innola transaction evidence endpoint.
- Confirm whether search results should include downloadable/source document references from Innola when available.

## Tasks / Subtasks

- [x] Add manual evidence search models and mockable service methods. (AC: 1-7, 13-15)
- [x] Add Compare view-model state, validation, query execution, query history, and valuable-evidence retention. (AC: 2-14, 16)
- [x] Add WPF search/curation UI to the Ownership Evidence panel. (AC: 1, 5, 7-10, 15)
- [x] Persist and restore query history and retained evidence in the Compare draft. (AC: 11-12)
- [x] Add regression coverage for manual queries, validation, curation, persistence, and restore. (AC: 16)

## Dev Agent Record

### Implementation Plan

1. Extend Compare evidence models and service seams for manual search modes without hard-coding live Innola endpoints.
2. Add view-model fields/commands for manual search, result history, valuable evidence, and role tagging.
3. Replace the old query-button row with compact search fields and separate query-results/valuable-evidence lists.
4. Persist manual query history and valuable evidence in the existing Compare draft artifact.
5. Cover the new behavior with compare-focused tests, then run the full harness.

### Debug Log

- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 errors and one pre-existing nullable warning during the first implementation pass.
- `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj --no-build -- "compare"` initially found that mock legal records kept their original query key during name searches.
- Patched `MockLegalCadasterQueryService` so returned records are stamped with the active query key.
- `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj --no-build -- "compare"` passed 45 compare-filtered tests.
- Final `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
- Final `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj --no-build` passed 404 tests.
- Review patch: `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 errors and one pre-existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- Review patch: `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj --no-build -- "compare"` passed 48 compare-filtered tests.
- Review patch: `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj --no-build` passed 407 tests.

### Completion Notes

- Added manual Compare evidence search modes for PID, Volume/Folio, Land Val No., and Name + Parish.
- Added visible input fields and validation messages in the Ownership Evidence panel.
- Extended legal cadaster query seams with Land Val No. and Name + Parish methods while keeping live adapters unsupported until Innola contracts are confirmed.
- Added query result history and a separate valuable evidence list with role tagging for Owner, Occupant, In Possession, Neighbor, and Other.
- Persisted and restored manual query history and valuable evidence through the Compare draft without re-querying services.
- Added regression coverage for typed manual PID search, required field validation, Land Val/Name query keys, valuable evidence role tagging, save, and restore.
- Patched review findings by restoring a visible fiscal-neighbor action in the Compare UI, preventing no-record/service-unavailable rows from being marked valuable, and replacing count-based valuable-evidence IDs with GUID-backed IDs.

### File List

- `_bmad-output/implementation-artifacts/8-4a-add-manual-compare-evidence-search-and-result-curation-ui.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareEvidenceModels.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareReviewDraftPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceViewModelTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-07-15 | 1.0 | Created follow-on Compare story for manual evidence search inputs, result display, valuable-evidence curation, role tagging, and draft persistence. | Mary / Sally / Codex |
| 2026-07-15 | 1.1 | Implemented manual search UI, typed query models, result history, valuable evidence role tagging, draft persistence, and regression coverage. | Amelia / Codex |
| 2026-07-15 | 1.2 | Patched code review findings for fiscal-neighbor UI access, valuable-evidence guards, unique evidence IDs, and regression coverage. | Amelia / Codex |
