---
baseline_commit: handoff-2026-07-08
---

# Story 2.18A: Add Transaction-Type Workflow Profiles For PE And PXA Source Requirements

Status: review

## Story

As a solution administrator,  
I want compute workflow source requirements and extraction behavior organized by transaction type profile,  
so that existing PE transactions and new PXA transactions can use the same workflow shell while enforcing different required documents, optional documents, extraction profiles, and stage rules through configuration.

## Business Context

The current implementation supports compute-style transactions with a global set of attachment source types. Today the active PE flow expects two mandatory business documents and two optional supporting sources:

- mandatory: computation/survey sheet
- mandatory: plan/map reference
- optional: structured survey points
- optional: AutoCAD/DWG source

The new PXA transaction type aligns with Story 2.18 and is expected to be mostly single parcel. Its primary business source is a scanned survey plan PDF, where points, bearings/distances, parish, area, survey date, instrument, owners, representatives, surveyor, and adjacent owners may all come from the plan itself.

The existing global `compute_attachment_source_types.required` flag is not expressive enough for both PE and PXA. Required/optional source roles must become transaction-type/profile specific while preserving the existing PE behavior.

## Acceptance Criteria

1. Given `WorkflowSettings.json` is loaded, when transaction type profiles are configured, then the system supports a profile model that maps transaction type code/name aliases to a workflow profile, required source roles, optional source roles, primary extraction role, and document profile.

2. Given an existing PE transaction is loaded, when profile resolution runs, then it resolves to a PE profile that preserves current behavior:
   - required `computation_sheet`
   - required `plan_map_reference`
   - optional `coordinate_text_source`
   - optional `dwg_source`
   - primary extraction role `computation_sheet`.

3. Given a PXA transaction is loaded, when profile resolution runs, then it resolves to a PXA profile that supports Story 2.18:
   - required scanned survey plan PDF source role
   - optional `coordinate_text_source`
   - optional `dwg_source`
   - primary extraction role set to the scanned survey plan source
   - document profile `scanned_single_parcel_survey_plan_pdf`.

4. Given no profile matches a supported transaction type, when the workflow attempts to load the transaction, then the workflow blocks with a clear unsupported/missing-profile message rather than falling back to incorrect PE source requirements.

5. Given profile-specific source requirements are configured, when `Supporting Document Check` runs, then it uses the resolved transaction profile's required/optional source roles instead of the global `required` flag from `compute_attachment_source_types`.

6. Given profile-specific source requirements are configured, when attachment classification runs, then source type definitions still map Innola source types to workflow roles, but role requirement status is resolved from the transaction profile.

7. Given workflow rules are resolved, when `WorkflowRules.json` is evaluated, then rules can match by transaction type/profile and select the correct script plan for PE versus PXA.

8. Given Structure Rules are evaluated, when Structure, Georeference, or Dimension Check runs, then rule applicability can include transaction profile/document profile so PE-specific computation-sheet rules do not falsely block PXA and PXA-specific survey-plan rules do not affect PE.

9. Given Document Type Catalog V2 classifies a source, when the resolved transaction profile specifies a document profile, then extraction routing can prefer the profile-compatible document type and extractor without hardcoding PXA in the workflow shell.

10. Given existing settings do not yet contain transaction type profiles, when settings are loaded, then safe defaults preserve current PE behavior and emit a non-blocking configuration warning that profile defaults are being used.

11. Given settings are saved through the Settings workspace, when transaction profile fields are round-tripped, then PE and PXA profile configuration remains stable and does not reorder or drop unknown future profile fields unnecessarily.

12. Given automated tests run, then coverage proves PE still uses the existing two-required-document flow and PXA uses the new single survey-plan-primary flow.

## Tasks / Subtasks

- [x] Define the transaction-type profile settings contract. (AC: 1-4, 10-11)
  - [x] Add a settings section such as `compute_transaction_type_profiles`.
  - [x] Include profile id/code, transaction type aliases, workflow profile, required source roles, optional source roles, primary extraction role, document profile, and enabled flag.
  - [x] Preserve backward-compatible safe defaults for current PE behavior.
  - [x] Add PXA default/profile example aligned to Story 2.18.

- [x] Decouple source type classification from requirement status. (AC: 5-6, 10)
  - [x] Keep `compute_attachment_source_types` as the source-type-to-workflow-role registry.
  - [x] Stop using the global `required` flag as the only source of truth for required business roles when a transaction profile is resolved.
  - [x] Treat `required` as legacy/default metadata or profile bootstrap only.
  - [x] Ensure internal-only sources such as resume ZIP remain excluded from business completeness.

- [x] Add profile resolution service/model. (AC: 1-6, 10-12)
  - [x] Resolve by transaction type code/name/text from Innola.
  - [x] Support aliases such as `PE`, `Plan Examination`, `Compute Survey Plan`, and `Assign Computation Task` for PE where applicable.
  - [x] Support alias/code `PXA` for the new transaction family.
  - [x] Persist the resolved profile id/code into case/manifest/routing artifacts for later stages.
  - [x] Fail clearly when no enabled profile matches.

- [x] Update Supporting Document Check. (AC: 2-6, 12)
  - [x] Use profile-required roles to decide copied/missing/optional status.
  - [x] Show operator-facing messages that identify the active transaction profile.
  - [x] Preserve PE required roles exactly.
  - [x] Make PXA require only the configured scanned survey plan primary source unless optional roles are present.

- [x] Update WorkflowRules and extraction script-plan routing. (AC: 7, 9, 12)
  - [x] Add or enable matching by workflow profile/document profile in addition to transaction type names.
  - [x] Preserve existing PE `scenario_a_two_pdf` and `scenario_b_points_dwg_plan` behavior.
  - [x] Add a PXA workflow rule placeholder that routes to Story 2.18 extractor/script names without implementing the extractor in this story.
  - [x] Ensure "No workflow rule matches" diagnostics include transaction profile, source roles, and document profile.

- [x] Update Structure/Georeference/Dimension rule applicability. (AC: 8, 12)
  - [x] Allow rules to target transaction profile and/or document profile.
  - [x] Prevent PE computation-sheet rules from blocking PXA when PXA intentionally lacks a computation sheet.
  - [x] Add placeholder PXA rule grouping for scanned survey-plan PDF structure/georeference/dimension readiness.

- [x] Expose/round-trip configuration in Settings workspace. (AC: 1, 10-11)
  - [x] Add editable or JSON-backed transaction profile configuration area.
  - [x] Validate that at least one enabled profile exists for each supported transaction type/code.
  - [x] Warn on duplicate aliases, missing primary extraction role, or required roles not present in source type registry.

- [x] Add automated coverage. (AC: 1-12)
  - [x] PE profile resolution by code/name aliases.
  - [x] PXA profile resolution by code/name aliases.
  - [x] PE supporting document check still requires computation sheet and plan/map reference.
  - [x] PXA supporting document check requires scanned survey plan primary source and does not require computation sheet.
  - [x] Optional coordinate text and DWG are recognized but not required for both profiles.
  - [x] WorkflowRules resolver selects PE versus PXA profiles correctly.
  - [x] Missing/unknown profile blocks with clear diagnostics.
  - [x] Settings round-trip preserves profile JSON.

## Dev Notes

### Current Configuration Reality

Current `WorkflowSettings.json` has:

```json
"supported_transaction_types": [
  "Plan Examination",
  "Cadastral Plan Examination",
  "Compute Survey Plan"
],
"compute_attachment_source_types": [
  {
    "source_type": "st_surveyplan",
    "workflow_role": "plan_map_reference",
    "required": true
  },
  {
    "source_type": "st_surveysheet",
    "workflow_role": "computation_sheet",
    "required": true
  },
  {
    "source_type": "st_survey_points",
    "workflow_role": "coordinate_text_source",
    "required": false
  },
  {
    "source_type": "st_autocad_file",
    "workflow_role": "dwg_source",
    "required": false
  }
]
```

This is enough for PE but too global for PXA. The same source type registry can stay, but source-role requirement status must move to profile-level configuration.

### Recommended Profile Shape

Candidate settings shape:

```json
"compute_transaction_type_profiles": [
  {
    "profile_id": "pe_computation_review",
    "enabled": true,
    "transaction_type_codes": ["PE"],
    "transaction_type_names": [
      "Plan Examination",
      "Cadastral Plan Examination",
      "Compute Survey Plan",
      "Assign Computation Task",
      "Computation Check"
    ],
    "workflow_profile": "pe_computation_sheet_review",
    "required_source_roles": [
      "computation_sheet",
      "plan_map_reference"
    ],
    "optional_source_roles": [
      "coordinate_text_source",
      "dwg_source"
    ],
    "primary_extraction_role": "computation_sheet",
    "document_profile": "computation_sheet_multi_or_single_parcel"
  },
  {
    "profile_id": "pxa_single_parcel_survey_plan",
    "enabled": true,
    "transaction_type_codes": ["PXA"],
    "transaction_type_names": ["PXA"],
    "workflow_profile": "pxa_single_parcel_survey_plan",
    "required_source_roles": [
      "survey_plan_pdf"
    ],
    "optional_source_roles": [
      "coordinate_text_source",
      "dwg_source"
    ],
    "primary_extraction_role": "survey_plan_pdf",
    "document_profile": "scanned_single_parcel_survey_plan_pdf"
  }
]
```

The implementation may choose to reuse `plan_map_reference` for PXA in the first patch, but the cleaner product model is a distinct `survey_plan_pdf` workflow role because for PXA the plan is not only a reference; it is the primary extraction source.

### Source Type Registry Direction

For PXA, add or map an Innola source type for the survey plan primary source. Options:

1. Preferred: add a new workflow role `survey_plan_pdf` and map the relevant Innola source type to it.
2. Transitional: reuse `st_surveyplan -> plan_map_reference`, but the profile marks it as primary extraction role. This is lower effort but semantically weaker.

Mary recommendation: use `survey_plan_pdf`.

Winston recommendation: keep source role vocabulary stable and avoid making one role mean "reference" in PE but "primary source" in PXA.

Amelia implementation note: adding the role touches `SourceRole.cs`, source-type defaults, settings validation, and tests, but will reduce special cases later.

### PE Versus PXA Behavior

PE:

- `computation_sheet` is primary.
- `plan_map_reference` is required reference.
- `coordinate_text_source` optional.
- `dwg_source` optional.
- Existing extraction profile: computation sheet/table.

PXA:

- `survey_plan_pdf` is primary.
- usually one parcel.
- `coordinate_text_source` optional.
- `dwg_source` optional.
- extraction profile: scanned single-parcel survey plan PDF from Story 2.18.

### Scope Boundaries

This story does not implement the PXA OCR/vision extractor. Story 2.18 owns that.

This story does not change Finalize, Enterprise publishing, Innola Spatial Unit creation, or map web views.

This story should not fork the workflow shell. PE and PXA still share the same visible stages:

```text
Supporting Document Check
Structure Check
Georeference Check
Dimension Check
Validate Points and Lines
Create Spatial Units
Final Review
Finalize
```

### Suggested Files To Review

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ComputeAttachmentSourceTypeCatalog.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceRole.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/WorkflowRules/WorkflowRuleResolver.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/WorkflowRules/WorkflowRuleDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/StructureRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/`

## Dependencies

- Precedes Story 2.18 implementation.
- Complements Story 5.16F source-type classification.
- Complements Story 4.7/4.8/4.9 stage rule grouping.

## References

- `_bmad-output/implementation-artifacts/2-18-add-single-parcel-survey-plan-pdf-metadata-and-geometry-extraction.md`
- `_bmad-output/implementation-artifacts/5-16f-configure-supporting-document-source-types-and-attachment-role-rules-for-compute-intake.md`
- `_bmad-output/implementation-artifacts/4-7-rename-preflight-rules-to-structure-rules-and-add-configurable-dwg-cad-layer-validation.md`
- `_bmad-output/implementation-artifacts/4-8-split-structure-check-and-dimension-check-into-separate-actions-and-result-summaries.md`
- `_bmad-output/implementation-artifacts/4-9-add-georeference-check-stage-and-reportable-stage-findings-model.md`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-07-08 | 0.1 | Initial story for configurable transaction-type workflow profiles separating PE and PXA source requirements. | Codex |
| 2026-07-08 | 1.0 | Implemented transaction-type workflow profile settings, PE/PXA source requirements, profile-aware rule routing, settings UI round-trip, and automated coverage. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln`
- `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj`

### Completion Notes

- Added configurable `compute_transaction_type_profiles` with safe PE/PXA defaults and Settings workspace JSON round-trip.
- Added `survey_plan_pdf` source role/profile detection and PXA survey-plan source type support.
- Profile resolution now runs during transaction load, persists profile metadata into manifest payloads, and blocks unmatched supported transaction types with explicit diagnostics.
- Supporting Document, WorkflowRules, and Structure/Georeference/Dimension rule applicability now support transaction/document profile targeting.
- PXA workflow-rule placeholder routes to the Story 2.18 extractor name without implementing the extractor in this story.

### File List

- `_bmad-output/implementation-artifacts/2-18a-add-transaction-type-workflow-profiles-for-pe-and-pxa-source-requirements.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Intake/SourceInputProfileDetectorTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Settings/SettingsWorkspaceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/WorkflowRules/WorkflowRuleResolverTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ComputeAttachmentSourceTypeCatalog.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ComputeTransactionTypeProfileDefinition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceInputProfile.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceInputProfileDetector.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceRole.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleDefinition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/StructureRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/WorkflowRules/WorkflowRuleDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/WorkflowRules/WorkflowRuleResolutionContext.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/WorkflowRules/WorkflowRuleResolver.cs`
