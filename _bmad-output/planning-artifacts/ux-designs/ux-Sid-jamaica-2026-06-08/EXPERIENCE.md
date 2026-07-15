---
name: "NLA Parcel Workflow Add-in"
status: final
sources:
  - _bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/prd.md
  - _bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/addendum.md
updated: 2026-07-14
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
| Jamaica Review Workspace | Open from Extraction Review | Large examiner workspace for source verification, parcel grouping review, OCR/AI result correction, and parcel interpretation before spatial editing |
| Compare Workspace | Open from transaction list when current task is Compare | Evidence reconciliation workspace for attached documents, transaction-scoped working geometry, legal owner records, fiscal neighbor records, and Compare decision |
| Validation | After review approval | Run rules, inspect severity findings, see score/percentage where available, decide whether to continue or route to Manual Process |
| Outputs | After validation path allows completion | Generate local GDB, GeoJSON, reports, logs, annotations where possible, and add outputs to ArcGIS Pro map |
| Commit / Sync Readiness | After Compare approval and output readiness | Show final-layer handoff metadata and confirm whether the transaction is ready to promote/commit into the authoritative layer |
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
| Jamaica review workspace shell | Jamaica Review Workspace | Preferred as a large floating workspace or dedicated review window. It contains four coordinated regions: source viewer, extracted review grid, parcel interpretation panel, and parcel preview inset. |
| Embedded source viewer | Jamaica Review Workspace | Supports PDF and image verification in-pane. TXT/CSV and unsupported formats use a compact metadata placeholder with `Open externally` and `Reveal in folder` fallback actions. |
| Parcel group switcher | Jamaica Review Workspace | Shows parcel tabs or parcel chips when one source contains multiple parcels. Switching parcels updates the review grid, interpretation panel, and preview together. |
| Parcel interpretation panel | Jamaica Review Workspace | Summarizes the active parcel, line sequence, closure/misclose, suspicious transitions, and unresolved row counts. It does not perform map edits. |
| Parcel preview inset | Jamaica Review Workspace | Displays a compact traverse/parcel sketch for the active parcel and the currently selected row. It is only an orientation aid, not the authoritative spatial editor. |
| Compare workspace shell | Compare Workspace | Preferred as a large floating workspace with three persistent regions: attached documents on the left, transaction-scoped map/geometry in the center, and ownership evidence plus decision controls on the right. |
| Compare source document pane | Compare Workspace | Reuses the embedded source viewer pattern from Compute. Document switching must not clear query results or decision notes. |
| Compare map panel | Compare Workspace | Loads working_review geometry filtered by the selected transaction scope, zooms to that extent, and presents geometry as read-only evidence. It must not expose COGO editing, add segment, point edit, or boundary correction actions. |
| Ownership evidence panel | Compare Workspace | Groups survey plan interpretation, legal cadaster results, fiscal cadaster neighbor results, open discrepancies, and the final Compare decision. Query actions are explicit and evidence returned from each source remains separately labeled. |
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
| Jamaica workspace loading | Jamaica Review Workspace | Viewer, parcel list, and review grid skeleton-load independently. The user can see which region is still loading without freezing the whole workspace. |
| Jamaica workspace unresolved | Jamaica Review Workspace | Unresolved and low-confidence rows remain prominent. `Approve review` stays disabled until blockers are cleared or explicitly routed onward by workflow policy. |
| Jamaica workspace approved | Jamaica Review Workspace | Review grid becomes read-only by default, parcel interpretation stays visible, and the handoff message points the user to `Map Review` for final spatial correction. |
| Unsupported source type | Jamaica Review Workspace | The workspace keeps the extracted table active but replaces the embedded viewer with a fallback panel that explains why external opening is required. |
| Review approved | Extraction Review | Lock approved data for validation unless user chooses Reopen Review. Record timestamp/operator when available. |
| Compare loading | Compare Workspace | Attached documents, working geometry, and legal/fiscal evidence load independently. Each region shows its own loading or failure state so the examiner can keep context. |
| Compare geometry unavailable | Compare Workspace | Show a blocker that names the missing working_review scope and disables Approve Compare. Document viewing and cadaster query fields remain available for investigation. |
| Compare discrepancy open | Compare Workspace | Keep Approve Compare disabled or guarded by policy. The right evidence panel lists unresolved owner, volume/folio, parcel ID, or neighbor mismatches. |
| Compare approved | Compare Workspace | Persist the Compare decision, reviewer, timestamp, queried evidence references, notes, and transaction scope. Commit becomes available only after this state is recorded. |
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

For the Jamaica review workspace specifically:

| Host shape | Behavior |
|---|---|
| Floating / dedicated review window | Preferred. Supports full three-column review with bottom preview and keeps source verification, extracted rows, and parcel interpretation visible together. Target minimum is roughly 1180px by 760px. |
| Wide docked pane | Acceptable fallback. Keeps the same regions but allows the source viewer to collapse to a narrower column and the parcel preview to become an inset. |
| Narrow docked pane | Not preferred for active review. The pane should offer a concise summary plus an `Open Review Workspace` action instead of forcing the full multi-region review UI into a cramped width. |

## Product-Specific UX Rules

- Output creation must never feel like the first moment of review. The review approval step is mandatory.
- Manual Process is not failure language. It is an official cadastral route for cases where automation is insufficient.
- Low extraction quality should push the user toward editing/correction, not hide the partial data.
- Sync readiness must not imply live CADINDEX update in v1.
- Plaintext credential configuration is a v1 constraint. The UX should not expose raw secrets in logs, reports, or ordinary status views.
- The Jamaica review workspace owns document verification, extracted-row correction, parcel grouping review, and review approval. It does not own parcel-fabric editing, snapping, or final map edits.
- When one source document contains multiple parcels, parcel switching must be explicit. The UI cannot assume the next row always belongs to the next segment of the current parcel.
- The Compare workspace owns evidence reconciliation only: plan evidence, working_review geometry context, legal ownership records, fiscal neighbor records, discrepancies, and the Compare decision.
- Compare geometry is read-only by default. If the examiner finds a geometry problem, the UX should route back to Compute or a named correction route rather than silently allowing map edits inside Compare.
- Legal cadaster and fiscal cadaster query results must remain visually distinct. A fiscal-neighbor match cannot be presented as legal owner confirmation.

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

### Flow 4 - Jamaica review workspace before map review

1. Nadia completes Processing Checks and opens the Jamaica review workspace from Review Extracted Points.
2. The workspace opens in a large floating window with the source PDF on the left, the extracted point/line table in the center, parcel tabs and interpretation on the right, and a parcel sketch preview below.
3. She switches from Parcel 1 to Parcel 3 because the source sheet contains multiple parcels and one sequence break looks suspicious.
4. The grid updates to Parcel 3 rows only, and the right-side interpretation panel flags one unresolved transition between two line groups.
5. Nadia corrects one point, adds one missing row, and verifies the bearing sequence against the source document.
6. **Climax:** She approves the review. The workspace locks the edited result set, shows a handoff message to `Map Review`, and closes back to the main workflow shell.
7. Failure: the source is a TXT-only attachment. The grid remains usable, but the viewer pane explains that embedded preview is unavailable and offers `Open externally` and `Reveal in folder`.

### Flow 5 - Output creation and sync readiness

1. Nadia has approved review data and acceptable validation status.
2. She selects Create Outputs.
3. The add-in writes result GDB, extracted point/line feature classes, GeoJSON, HTML/PDF/JSON reports, and logs.
4. The Output artifact list shows generated paths and counts. Nadia chooses Add to Map.
5. **Climax:** ArcGIS Pro map receives the generated layers, and the Sync Readiness panel shows the result GDB as the future CADINDEX sync package.
6. Failure: output GDB generation fails. The pane preserves prior artifacts, shows the failed step, and links to the process log.

### Flow 6 - Compare ownership evidence reconciliation

1. Nadia refreshes the transaction list and selects transaction `TR100000674`, whose current task is `Compare`.
2. The add-in assigns or starts the task for Nadia according to Innola ownership rules, then opens the Compare workspace.
3. The left pane loads attached transaction documents. The center pane loads `working_review` geometry filtered to the transaction number and zooms to the transaction extent.
4. Nadia selects the survey plan PDF and checks the plan owner, parcel ID, and volume/folio against the legal cadaster query results.
5. She runs fiscal cadaster neighbor lookup from the working polygon and reviews boundary neighbors against the plan labels.
6. **Climax:** One fiscal neighbor is unresolved, so Nadia records a note and blocks Compare until the boundary-rights discrepancy is resolved.
7. Success path: all owner and neighbor evidence aligns. Nadia selects `Approve Compare`, the add-in records the Compare decision artifact, and Commit becomes available.
8. Failure: no working_review polygon is returned for the transaction scope. The workspace disables `Approve Compare`, keeps documents visible, and shows the exact missing scope field/value.

## Mock Coverage

| Surface | Coverage |
|---|---|
| Dock Pane Shell | Mocked in `mockups/dock-pane-workflow.html` |
| Intake | Mocked in `mockups/dock-pane-workflow.html` |
| Preflight | Mocked in `mockups/dock-pane-workflow.html` |
| Extraction Review | Mocked in `mockups/dock-pane-workflow.html` and `mockups/dock-pane-review-before-output.html` |
| Jamaica Review Workspace - preferred floating layout | Mocked in `mockups/jamaica-cogo-review-workspace-floating.html` |
| Jamaica Review Workspace - docked fallback layout | Mocked in `mockups/jamaica-cogo-review-workspace-docked.html` |
| Compare Workspace - evidence reconciliation layout | Mocked in `mockups/compare-workspace-evidence-reconciliation.html` |
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
