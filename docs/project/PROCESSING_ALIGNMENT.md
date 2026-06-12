# PROCESSING_ALIGNMENT

**Last Updated:** 2026-06-12

## Purpose

Align the ArcGIS Pro add-in, transaction-driven intake, Python tooling, and future review UX around a reliable extraction-first workflow that supports both automation and manual correction before parcel geometry is generated.

## Core Recommendation

Treat extraction as **draft data capture**, not as final parcel generation.

The system should:

1. Load transaction files into the Case Folder.
2. Resolve the case path from transaction metadata and source roles.
3. Run source-specific extraction into a review artifact.
4. Allow user review, correction, and manual point entry.
5. Approve the reviewed draft data.
6. Generate parcel geometry and `.gdb` outputs only after approval.

## Why This Matters

Real source packages will vary in quality:

- PDF/image computation sheets may miss points or misread labels.
- Map documents may provide supporting OCR/reference context but not authoritative points.
- TXT/CSV inputs may be authoritative and should bypass OCR entirely.
- DWG files may provide parcel context, CAD-derived layers, or validation support but should not be assumed present for all cases.

Because extraction may be incomplete or partially wrong, the product must support a deliberate human correction stage before spatial parcel generation.

## Source Families

### 1. Document/Image Sources

Examples:

- `.pdf`
- `.tif`
- `.tiff`
- `.png`
- `.jpg`
- `.jpeg`

Expected behavior:

- Run OCR/local parsing/OpenAI-assisted extraction when enabled.
- Produce draft point rows and source evidence.
- Never assume perfect capture.

### 2. Structured Point Sources

Examples:

- `.txt`
- `.csv`

Expected behavior:

- Parse deterministically.
- Normalize point rows into the review dataset.
- Avoid OCR/OpenAI unless a future rule explicitly requires it.

### 3. DWG Sources

Examples:

- `.dwg`

Expected behavior:

- Inspect readiness and CAD sublayers.
- Provide context for later parcel build and validation.
- Stay independent from the primary point-capture workflow.

## Workflow Model

Recommended stage sequence:

1. Intake
2. Preflight
3. Draft Extraction
4. Review / Edit Points
5. Review Approved
6. Parcel Build
7. Validation
8. Output / Completion

## Architecture Alignment

### Keep `WorkflowRules.json` as the routing layer

Rules should determine:

- required source roles
- allowed file families
- processing path
- whether DWG context is expected
- whether OpenAI/local-only providers are allowed

### Add execution adapters by concern

Recommended adapter boundaries:

- `document_extraction_adapter`
- `points_normalization_adapter`
- `dwg_inspection_adapter`
- `parcel_build_adapter`

This is preferable to forcing all cases through one generic extraction adapter.

### Treat OpenAI as an optional provider

OpenAI should be selectable, not mandatory.

Recommended provider modes:

- `local`
- `openai`
- `hybrid`

TXT/CSV flows should normally remain local. PDF/image flows may use OpenAI when enabled by configuration and permitted by the selected profile.

## Role of `CreateParcelFromFile.py`

`CreateParcelFromFile.py` should be integrated as a backend execution tool, not as the whole workflow design.

Short-term recommendation:

- use it to generate draft review data first
- keep final `.gdb` build as a later approved step

If the script currently mixes extraction and parcel creation tightly, the add-in should call it in a bounded review mode first before exposing full parcel build behavior.

## Required Intermediate Artifact

Use a stable review artifact:

- `working/extraction_review_data.json`

This artifact should become the contract between automated extraction and human review.

It should contain:

- transaction metadata
- case/rule/profile metadata
- source file references
- extracted point rows
- confidence/status fields
- unresolved/missing indicators
- manual edits
- review approval metadata linkage

## Manual Review Requirements

Manual review is not a fallback; it is part of the intended workflow.

The UI must support:

- adding missing points
- correcting extracted values
- marking rows unresolved
- choosing authoritative source evidence when needed
- preventing parcel build until blockers are cleared or accepted according to rules

## Recommended Near-Term Delivery Path

### Story 2.12

Implement execution adapter support that:

- reads resolved `script_plan`
- runs draft extraction
- writes `working/extraction_review_data.json`

### Story 2.13

Implement review/edit workflow that:

- loads draft extraction data
- supports manual add/edit/correct actions
- produces approved review data

### Story 2.14

Implement parcel build from approved review data into final `.gdb` outputs.

## Guardrails

- Do not generate final geometry before review approval.
- Do not treat OCR/OpenAI output as authoritative without review.
- Do not persist API keys or secrets in manifests, logs, or generated INI files.
- Keep the Case Folder as the auditable system of record.
- Keep rule resolution, extraction, review, and parcel build as separate responsibilities.
