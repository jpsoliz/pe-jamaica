---
name: "NLA Parcel Workflow Add-in"
status: final
sources:
  - _bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/prd.md
  - _bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/addendum.md
updated: 2026-06-08
---

# NLA Parcel Workflow Add-in - Experience Spine

`DESIGN.md` is the visual identity reference. This document defines how the ArcGIS Pro add-in works.

## Foundation

The primary surface is an ArcGIS Pro dock pane for NLA Cadastral Office technical staff and cadastral officials. The add-in runs inside ArcGIS Pro and coordinates source intake, preflight, extraction, human review, validation, local output creation, and future-ready CADINDEX sync metadata.

The UX inherits ArcGIS Pro desktop conventions: dockable pane behavior, compact Windows controls, WPF-style forms, table review, progress dialogs, map/layer integration, and geoprocessing status. The add-in should not behave like a standalone web app.

Visual tokens live in `DESIGN.md`. Behavioral references use the visual token names, such as `{colors.primary}`, `{colors.danger}`, `{components.review-table}`, and `{spacing.pane-width-target}`.

## Information Architecture

| Surface | Reached from | Purpose |
|---|---|---|
| Dock Pane Shell | ArcGIS Pro add-in button | Persistent workflow container, transaction status, step navigation, and current action |
| Intake | New case / reopen case | Create or reopen transaction, choose source scenario, copy source files into Case Folder |
| Preflight | After valid intake | Check required files, extensions, DWG readability, write access, ArcGIS/Python environment, coordinate/profile settings |
| Extraction Review | After preflight passes | Run extraction, review extracted points/lines/segments/metadata, edit values, add missing data, approve review data |
| Validation | After review approval | Run rules, inspect severity findings, see score/percentage where available, decide whether to continue or route to Manual Process |
| Outputs | After validation path allows completion | Generate local GDB, GeoJSON, reports, logs, annotations where possible, and add outputs to ArcGIS Pro map |
| Sync Readiness | After output creation | Show CADINDEX Sync Facade metadata and confirm no live Enterprise update is performed in v1 |
| Reports and Logs Viewer | Output links / status strip | Open HTML/PDF/JSON reports and processing logs from the Case Folder |
| Settings/Profile | Pane menu | Configure optional AI/OCR, local-only mode, coordinate/profile defaults, and plaintext v1 credential profile warning |

Composition references: `mockups/dock-pane-workflow.html`, `mockups/dock-pane-failed-extraction-manual-process.html`, and `mockups/dock-pane-review-before-output.html`. Spine wins on conflict.

## Voice and Tone

Microcopy should be direct, technical, and calm.

| Do | Don't |
|---|---|
| "Preflight blocked: DWG is unreadable." | "Something went wrong." |
| "3 low-confidence points need review." | "Please check your data." |
| "Route to Manual Process" | "Give up automation" |
| "Generated: result GDB, GeoJSON, HTML report." | "Success!" |
| "AI extraction disabled for this run." | "AI is not available." |

Use "case", "transaction", "source files", "review data", "validation", "output", and "Manual Process" consistently with the PRD glossary.

## Component Patterns

| Component | Use | Behavioral rules |
|---|---|---|
| Step navigator | Dock Pane Shell | Shows Intake, Preflight, Review, Validation, Outputs, Sync Readiness. Completed steps remain clickable. Future blocked steps are disabled until prerequisites pass. |
| Transaction header | Dock Pane Shell | Shows transaction ID, current step, last run status, and score/status when available. Reopen case action is available from a menu. |
| File picker row | Intake | Allows browse/reselect/remove. Shows required/optional state, accepted extensions, and copy-to-case-folder result. |
| Scenario selector | Intake | Two segmented choices: Scenario A and Scenario B. Changing scenario after files are selected prompts before clearing incompatible files. |
| Preflight checklist | Preflight | Groups blockers and warnings. Blocking rows must name the failed condition and a concrete correction. |
| Processing progress | Extraction/Validation/Outputs | Shows current step, elapsed time, cancellable state when technically available, and last written artifact. Does not freeze ArcGIS Pro UI. |
| Review table | Extraction Review | Supports inline edit, add point/data row, mark unresolved, evidence link, confidence indicator, and original-vs-edited value comparison. |
| Validation findings list | Validation | Groups by Critical, High, Warning, Info, Passed. Critical/high findings appear first. Each finding can open evidence/details. |
| Manual Process decision panel | Validation | Appears when zero usable extraction/validation occurs or when user chooses manual path. Requires explicit user confirmation and records decision. |
| Output artifact list | Outputs | Lists GDB, feature classes, GeoJSON, reports, and logs. Each artifact has open/reveal/add-to-map actions where applicable. |
| Sync Facade panel | Sync Readiness | Shows target CADINDEX reference and result GDB package. Clearly states v1 does not perform live CADINDEX update. |

## State Patterns

| State | Surface | Treatment |
|---|---|---|
| No case open | Dock Pane Shell | Show compact start state with New Transaction and Reopen Case actions. |
| Intake incomplete | Intake | Required file rows show missing state. Preflight button disabled. |
| Source copied | Intake | Each selected file row shows copied-to-case-folder confirmation and destination path. |
| Preflight blocked | Preflight | Blocker summary at top with `{colors.danger}` state. Downstream steps disabled. |
| Preflight warnings only | Preflight | Continue allowed. Warnings remain visible and are included in logs. |
| Extraction running | Extraction Review | Disable destructive changes. Show progress, current method, and artifact path. |
| Zero usable extraction | Extraction Review | Mark extraction failed, show Manual Process recommendation, allow user to add/correct data manually if workflow profile permits. |
| Nonzero incomplete extraction | Extraction Review | Continue to review/manual adjustment. Low-confidence and missing data rows remain flagged. |
| Review approved | Extraction Review | Lock approved data for validation unless user chooses Reopen Review. Record timestamp/operator when available. |
| Critical validation failure | Validation | Automated output completion disabled. User may explicitly route to Manual Process. |
| Output complete | Outputs | Show generated GDB, GeoJSON, HTML/PDF/JSON reports, logs, counts, and Add to Map option. |
| Sync facade ready | Sync Readiness | Show result GDB and CADINDEX future target metadata. No publish/sync button in v1. |

## Interaction Primitives

- **Primary action location:** sticky bottom action bar inside the dock pane. The main action changes by step: Run Preflight, Run Extraction, Approve Review, Run Validation, Create Outputs.
- **Secondary actions:** top-right pane menu and contextual row actions. Examples: Reopen Case, Reveal Folder, Open Report, Add to Map.
- **Tables:** keyboard navigation should support row focus, Enter to edit/open details, Escape to cancel edit, and Tab order through editable cells.
- **Dialogs:** use modal dialogs for destructive or irreversible choices only: changing scenario after selected files, reopening approved review data, and routing to Manual Process.
- **Evidence links:** evidence actions open the relevant report, source page reference, row detail, or map layer selection when available.
- **Progress/cancel:** long-running operations show progress. Cancellation is offered only when the underlying processing step can stop cleanly.
- **No hidden mandatory AI:** AI/OCR controls appear as profile settings and run options, not as a required path.

## Accessibility Floor

- Support full keyboard operation for the dock pane workflow.
- Maintain visible focus rings using `{colors.focus-ring}`.
- Color cannot be the only severity indicator. Use text labels and icons/shape markers alongside `{colors.success}`, `{colors.warning}`, and `{colors.danger}`.
- Tab order follows the visual workflow: header, step navigator, current step content, action bar, status/log strip.
- Error and blocker messages must state the failed condition and next action.
- Tables must expose row labels, column headers, edited states, missing states, and confidence states to assistive technology where WPF supports it.
- Tooltips provide full paths for truncated file/path text.

## Responsive & Platform

This is a desktop ArcGIS Pro add-in. It does not need phone or browser responsiveness in v1.

| Pane width | Behavior |
|---|---|
| `< 360px` | Minimum viable mode. Hide nonessential metadata columns; show details in row expansion. |
| `360-480px` | Target dock-pane behavior. Step navigator and current step stack vertically. |
| `> 480px` | Allow wider review tables and two-column field groups where ArcGIS Pro docking permits. |

The active ArcGIS Pro map is a companion surface, not a separate designed screen. The add-in should add/select/navigate map layers rather than recreating a map preview inside the pane.

## Product-Specific UX Rules

- Output creation must never feel like the first moment of review. The review approval step is mandatory.
- Manual Process is not failure language. It is an official cadastral route for cases where automation is insufficient.
- Low extraction quality should push the user toward editing/correction, not hide the partial data.
- Sync readiness must not imply live CADINDEX update in v1.
- Plaintext credential configuration is a v1 constraint. The UX should not expose raw secrets in logs, reports, or ordinary status views.

## Key Flows

### Flow 1 - New case intake and preflight

1. Nadia opens ArcGIS Pro and launches the Parcel Workflow dock pane.
2. She selects New Transaction. The pane shows transaction ID `TR-SMD-0000001`, source scenario options, coordinate/profile fields, and file rows.
3. She selects Scenario B and adds computation PDF, TXT/CSV points file, scanned plan PDF, and DWG reference.
4. The add-in copies each file into the Case Folder and shows copied destination paths.
5. Nadia runs Preflight.
6. **Climax:** Preflight returns one warning about an optional DWG sublayer but no blockers. The step navigator marks Intake and Preflight complete, and the Run Extraction action becomes available.
7. Failure: DWG unreadable. Preflight shows a blocker row, disables Run Extraction, and gives a correction path.

### Flow 2 - Extraction review and approval

1. Nadia runs extraction with OCR enabled and AI optional.
2. The pane shows progress, extraction method, and generated review data path.
3. Results appear in the Review table grouped by parcel/source section.
4. Nadia filters to low-confidence rows, edits two point values, and adds one missing point.
5. She opens evidence for a row to confirm the source page/row reference.
6. **Climax:** Nadia approves review data. The pane records the approval timestamp/operator and unlocks Validation.
7. Failure: zero usable extraction. The pane marks extraction failed, recommends Manual Process, and offers manual add/correction where the profile allows.

### Flow 3 - Validation and Manual Process decision

1. Nadia runs validation from approved review data.
2. Findings appear grouped by severity. Critical findings are first.
3. A critical closure or coordinate issue blocks automated output completion.
4. Nadia reviews evidence and decides automation is insufficient.
5. **Climax:** She selects Route to Manual Process, confirms the decision, and the pane records the Manual Process state with COGO/manual ArcGIS Pro guidance.
6. Failure: user tries Create Outputs while critical findings remain. The action is disabled and explains why.

### Flow 4 - Output creation and sync readiness

1. Nadia has approved review data and acceptable validation status.
2. She selects Create Outputs.
3. The add-in writes result GDB, extracted point/line feature classes, GeoJSON, HTML/PDF/JSON reports, and logs.
4. The Output artifact list shows generated paths and counts. Nadia chooses Add to Map.
5. **Climax:** ArcGIS Pro map receives the generated layers, and the Sync Readiness panel shows the result GDB as the future CADINDEX sync package.
6. Failure: output GDB generation fails. The pane preserves prior artifacts, shows the failed step, and links to the process log.

## Mock Coverage

| Surface | Coverage |
|---|---|
| Dock Pane Shell | Mocked in `mockups/dock-pane-workflow.html` |
| Intake | Mocked in `mockups/dock-pane-workflow.html` |
| Preflight | Mocked in `mockups/dock-pane-workflow.html` |
| Extraction Review | Mocked in `mockups/dock-pane-workflow.html` and `mockups/dock-pane-review-before-output.html` |
| Zero-result extraction failure | Mocked in `mockups/dock-pane-failed-extraction-manual-process.html` |
| Manual Process decision | Mocked in `mockups/dock-pane-failed-extraction-manual-process.html` |
| Review-before-geometry lock | Mocked in `mockups/dock-pane-review-before-output.html` |
| Validation | Mocked in `mockups/dock-pane-workflow.html`; blocked state mocked in `mockups/dock-pane-review-before-output.html` |
| Outputs | Mocked in `mockups/dock-pane-workflow.html` |
| Sync Readiness | Spine-only; behavior specified here |
| Settings/Profile | Spine-only; behavior specified here |

## Deferred UX Items

- Exact WPF controls, MVVM view models, and ArcGIS Pro SDK UI integration belong to architecture.
- Exact 0-10 solution score formula belongs to architecture/test planning.
- Exact Case 1-4 fixture names and baseline counts belong to test planning.
