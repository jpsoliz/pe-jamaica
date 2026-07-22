# Plan Examination Architecture Brief

Audience: Land Director  
Purpose: Explain the architecture and advantages of the Plan Examination solution at a high level.

## Executive Message

The solution is an ArcGIS Pro add-in that connects the Land examination workflow with Innola transactions. It does not replace Innola. Instead, it adds a controlled examination layer inside ArcGIS Pro, where staff can review survey documents, validate extracted parcel information, create or review spatial units, generate reports, and send results back to the transaction.

The central design principle is simple: each transaction gets a clear case record. Source documents, extracted information, examiner edits, validation results, map outputs, reports, and diagnostic logs are preserved in one Case Folder. This makes the process easier to resume, audit, troubleshoot, and improve.

## What The Architecture Does

1. Innola remains the transaction system

Innola provides the task queue, transaction identity, source documents, task status, and attachment destination. The add-in logs in to Innola, loads the examiner's eligible tasks, downloads the transaction documents, and later uploads saved or final outputs.

2. ArcGIS Pro becomes the examination workspace

The examiner works inside ArcGIS Pro, using custom dock panes and review tools built for the Plan Examination process. This keeps parcel review close to the map, reference layers, geometry editing tools, and spatial-unit review.

3. The add-in guides the process

The C# add-in controls the workflow stages, button availability, task ownership, save/suspend/finalize behavior, map cleanup, and user-facing messages. This reduces ambiguity and helps staff follow the correct sequence.

4. Processing tools handle specialized work

Python and ArcPy tools perform the processing-heavy work: source interpretation, point and line extraction, validation checks, geometry creation, geodatabase output, report generation, and map layer preparation. Existing specialist scripts can be wrapped instead of rewritten from scratch.

5. The Case Folder preserves the audit trail

Each transaction creates a local Case Folder with source files, working JSON artifacts, reports, logs, and output geodatabases. This is the durable record for recovery, review, and diagnostics.

## Why This Matters

- Better integration: staff can move from Innola task to ArcGIS Pro review and back to Innola without manual file hunting.
- Stronger governance: source documents, examiner decisions, validation outcomes, and reports are preserved.
- Less rework: the workflow can suspend and resume a transaction instead of losing context.
- Better quality control: custom validation gates prevent premature creation or finalization when required evidence is missing.
- More transparent decisions: final reports and compare reports can be attached back to Innola.
- Fit for local practice: custom Plan Examination steps can reflect Jamaica/NLA process rules rather than forcing a generic GIS workflow.
- Future-ready: the same boundaries can support later Enterprise services or broader system integration without discarding the current investment.

## Main Components

| Component | Plain-English Role | Value |
| --- | --- | --- |
| Innola integration | Gets tasks, documents, transaction status, and uploads reports | Keeps the official transaction connected |
| ArcGIS Pro add-in | Gives examiners guided tools inside ArcGIS Pro | Reduces training burden and process variation |
| Case Folder | Stores sources, decisions, reports, outputs, and logs | Enables audit, resume, and troubleshooting |
| Workflow rules | Chooses the right path for each transaction and source package | Supports custom examination requirements |
| Points Validation Tool | Lets examiners review and correct extracted points and lines | Keeps humans in control before geometry is created |
| Spatial Unit / Map Review | Shows generated parcel points, lines, polygons, and context layers | Makes geometry quality visible before finalization |
| Compare Review | Reconciles submitted geometry with Legal/Fiscal evidence | Supports evidence-based decisions |
| Reports and attachments | Generates PDF/JSON reports and uploads final evidence to Innola | Creates a clear transaction record |

## Director-Level Architecture View

Use the companion image:

`_bmad-output/presentation-artifacts/land-director-architecture-overview.svg`

Suggested speaking track:

"Innola remains the official transaction system. The ArcGIS Pro add-in gives examiners a guided workspace for the technical review. Every transaction creates a Case Folder that preserves the source documents, working decisions, validation results, map outputs, and reports. Custom processing and validation tools support the Plan Examination process, while final reports and status updates return to Innola. This gives NLA stronger control, auditability, and consistency without replacing the systems already in use."

## Key Advantages To Emphasize

- Integrated: links Innola tasks, ArcGIS Pro review, and transaction attachments.
- Controlled: buttons and stages guide examiners through the correct sequence.
- Auditable: every decision and artifact can be inspected later.
- Recoverable: work can be reopened after suspend, restart, or partial failure.
- Configurable: rules and stages can evolve as the process matures.
- Practical: keeps expert GIS work in ArcGIS Pro while connecting it to business workflow.

## Recommended Slide Structure

1. Current challenge

Plan Examination requires Innola transaction control, source document review, GIS editing, validation, reports, and final status updates. Without integration, staff must coordinate too many manual steps.

2. Solution concept

The add-in acts as a guided examination layer inside ArcGIS Pro, connected to Innola and supported by a durable Case Folder.

3. Architecture image

Show the diagram and explain the flow from Innola to ArcGIS Pro review, Case Folder audit record, processing tools, final package, and upload back to Innola.

4. Benefits

Emphasize auditability, quality control, reduced rework, staff consistency, and future expandability.

5. Ask / decision

Position the solution as a platform for standardizing and improving Plan Examination, not only as a single custom tool.
