# COGO Reader Automation Boundary Evaluation

Date: 2026-06-16
Scope: Story 5.10 - Evaluate Supported ArcGIS Pro Automation Boundary For COGO Reader Assist Vs Custom Transaction-Controlled Extraction Flow

## Context

The Sidwell Jamaica ArcGIS Pro add-in already owns:

- transaction selection from Innola
- source-file routing into case folders
- extraction and review artifacts
- workflow state and stage gating
- output generation and map-review setup

The question for this evaluation is whether ArcGIS Pro's COGO Reader should become part of that controlled workflow, or whether it should remain outside the supported integration boundary.

## Sources Reviewed

Primary official references:

- ArcGIS Pro help: `Extract COGO from deed images`  
  `https://doc.esri.com/en/arcgis-pro/latest/help/data/parcel-editing/extractcogofromdeeds.html`
- ArcGIS Pro SDK Parcel Fabric concepts  
  `https://pro.arcgis.com/en/pro-app/3.6/sdk/api-reference/conceptdocs/docs/ProConcepts-Parcel-Fabric.html`

Supporting local repo context:

- `_bmad-output/planning-artifacts/research/technical-arcgis-pro-addin-parcel-workflow-research-2026-06-08.md`
- `_bmad-output/planning-artifacts/research/parcel-fabric-review-workspace-pilot-2026-06-14.md`
- `_bmad-output/implementation-artifacts/5-6-add-spatial-review-stage-for-in-map-editing-and-manual-cogo.md`
- `_bmad-output/implementation-artifacts/5-9-add-map-review-editing-toolbar-for-spatial-correction-workflows.md`

## What The Official Surface Clearly Supports

### COGO Reader as a user-facing tool

The help page describes COGO Reader as a Parcel Record Workflows tool where the user opens the deed image and works through extraction inside the ArcGIS Pro UI.

Observed supported characteristics from the help content:

- it is a desktop user tool
- it is oriented around parcel/deed image workflows
- it supports common source image types such as PDF, TIFF, PNG, and JPG
- it assumes user interaction with the source document inside the tool workflow

### Parcel Fabric and map-review automation

The ArcGIS Pro SDK Parcel Fabric concept documentation shows a strong supported automation boundary around:

- parcel-fabric-aware map content
- records / active-record workflows
- parcel lines and parcel seeds
- parcel build operations
- layer loading and map context
- edit workflows that remain inside ArcGIS Pro's supported SDK/editing model

This means the spatial review and parcel editing side of the product has a solid supported ArcGIS Pro surface even if COGO Reader itself does not.

## What Was Not Found In The Public Supported Surface

I did not find public ArcGIS Pro SDK documentation showing a supported API to:

- launch COGO Reader programmatically against a selected transaction file
- preload COGO Reader with a specific file path from the add-in
- disable or constrain the file picker so the transaction source remains authoritative
- observe or drive COGO Reader state after launch through documented SDK APIs

I also did not find a public/documented command or pane contract that would make COGO Reader safe to treat as a transaction-controlled workflow dependency.

That does not prove such behavior is technically impossible through internal command IDs or UI-driving. It does mean the behavior is not something we should treat as a supported product contract.

## Compared Options

### Option A: Custom transaction-controlled extraction only

Characteristics:

- the add-in remains authoritative for source routing and stage control
- extraction stays in our custom pipeline
- ArcGIS Pro remains the spatial review and editing surface

Strengths:

- strongest control over transaction behavior
- easiest to keep deterministic and auditable
- lowest risk across ArcGIS Pro upgrades
- best fit for multi-source Jamaica cases such as computation sheets, plans, maps, text/CSV, and DWG

Limitations:

- all extraction quality improvements remain our responsibility
- we do not benefit from COGO Reader's deed-image assist UX

### Option B: Custom extraction primary, COGO Reader optional manual assist

Characteristics:

- the add-in remains authoritative
- COGO Reader is treated as an examiner assist tool only
- no product-critical dependency is placed on unsupported automation

Strengths:

- preserves safe architecture
- gives examiners an additional manual recovery tool for some image cases
- fits naturally inside Map Review as an optional human-assist step

Limitations:

- weak transaction control once the user is inside COGO Reader
- cannot reliably guarantee the user opened the intended transaction file through supported APIs
- may confuse workflow ownership unless clearly labeled as optional assist

### Option C: Deep COGO Reader integration

Characteristics:

- the product would try to treat COGO Reader as a controlled step in the transaction workflow

Strengths:

- only attractive if fully supported APIs exist

Limitations:

- no supported/public automation boundary was found for the key controls we need
- highest maintenance risk
- most likely to break on ArcGIS Pro updates
- weakest fit for a product that must keep transaction source routing authoritative

## Functional Fit For NLA Jamaica Plan Examination

### Computation sheets

Fit: low to medium

Reasoning:

- our workflow needs table/point extraction from computation documents
- those documents are not always the same as deed-image-centered parcel reading scenarios
- custom extraction remains a better fit for controlled parsing and review

### Scanned plans and maps

Fit: medium as optional assist, low as primary architecture

Reasoning:

- COGO Reader may help in some visually readable image cases
- plan/map quality, layout, and multi-parcel complexity reduce confidence as a primary automated path

### Multi-parcel documents

Fit: low for controlled automation

Reasoning:

- the product needs transaction-aware parcel grouping, review, and later output generation
- custom extraction plus explicit point review is a better fit for parcel sequencing and manual correction

### Manual correction workflows

Fit: medium as an operator assist

Reasoning:

- COGO Reader may still help an examiner interpret a difficult document
- ArcGIS Pro native map editing, snapping, COGO-capable tools, and Parcel Fabric review remain the stronger supported review surface

## Recommended Supported Integration Boundary

### Inside the supported product boundary

Keep these inside the add-in + public ArcGIS Pro automation contract:

- transaction-controlled source selection
- case-folder and artifact management
- custom extraction and review-data generation
- review-state persistence
- output creation
- map loading and zoom
- Parcel Fabric or standard spatial review workspaces
- active record / record workflows where applicable
- map-based editing, snapping, and parcel review tooling

### Outside the required product boundary

Do not make these required automated steps:

- forcing COGO Reader to open on a selected transaction file
- disabling the COGO Reader browse/load behavior
- treating undocumented UI automation as product infrastructure

## Recommendation

Recommended outcome:

- keep the custom transaction-controlled extraction flow as the primary architecture
- keep ArcGIS Pro map review, Parcel Fabric, and native editing tools as the supported spatial review surface
- allow COGO Reader only as an optional manual-assist concept, not as a required controlled workflow stage

Recommended classification for Story 5.10:

- `COGO Reader: optional/manual-assist only`

## UX And Toolbar Implications

1. Map Review tooling should focus first on native ArcGIS Pro editing, snapping, parcel review, and review-layer loading.
2. If a future toolbar includes COGO Reader, it should be labeled as optional assist and not as a transaction-controlled command.
3. Workflow copy should make clear that the authoritative source routing, review state, and approval path remain in the add-in.

## Next Story Implications

1. Continue strengthening the `Map Review` stage and map-review toolbar around supported ArcGIS Pro editing capabilities.
2. If desired, create a small evaluation spike for an optional `Open COGO Reader Assist` command only if a documented launch surface is found later.
3. Keep document extraction investment focused on the custom transaction pipeline, especially for:
   - computation-sheet parsing
   - multi-parcel segmentation
   - reviewable AI-assisted extraction
   - manual correction handoff

## Final Decision For Story 5.10

Decision:

- do not adopt COGO Reader as a core automated workflow dependency
- keep custom transaction-controlled extraction as the primary architecture
- permit COGO Reader only as a possible optional manual-assist concept in later UX work

This keeps the product inside a stable, supported ArcGIS Pro automation boundary while still leaving room for examiner-assist tools where they help.
