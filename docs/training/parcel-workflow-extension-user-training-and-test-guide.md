# Sidwell Cadastre Tools: Parcel Workflow Extension User Training and Test Guide

Document status: Draft for SMD review  
Audience: Plan examiners, SMD supervisors, support staff, and UAT testers  
Applies to: Compute Survey Plan and Compare Survey Plan transactions  
Coordinate system: JAD2001 / EPSG:3448

## 1. Purpose

This guide explains how to use the Sidwell Cadastre Tools Parcel Workflow Extension inside ArcGIS Pro to complete plan examination work that begins in the Innola framework.

Use this document to:

- Train users on the end-to-end Compute and Compare workflow.
- Run a repeatable test using assigned Innola transactions.
- Capture pass/fail evidence during user acceptance testing.
- Troubleshoot common login, document, map, Python, ArcPy, georeference, and transaction-completion issues.

The main workflow is written for users. Technical checks and log paths are listed in the support appendix.

## 2. Workflow Overview

The transaction starts in Innola. After the transaction is assigned to an SMD user, the user completes the work in ArcGIS Pro using this extension.

High-level process:

1. The transaction is created and assigned in Innola.
2. The user opens ArcGIS Pro and logs into the Cadastre tools add-in.
3. The user refreshes the Transactions List and selects the assigned transaction.
4. For Compute Survey Plan work, the user completes all Compute stages.
5. The user reviews supporting documents and, when needed, runs M-Geo to overlay the survey plan on the map.
6. The user validates points and lines, creates spatial units, and completes final review.
7. For Compare Survey Plan work, the user launches Compare and reviews parcel evidence.
8. The user saves, suspends, finalizes, or cancels according to the state of the work.
9. When finalized, the transaction is moved forward in the Innola workflow.

[Screenshot placeholder: `00-workflow-overview.png` - overall process diagram from Innola to ArcGIS Pro and back to Innola.]

## 3. Before You Start

Confirm the following before training or testing:

- ArcGIS Pro is installed and licensed.
- The Sidwell Cadastre Tools add-in is installed and visible in ArcGIS Pro.
- The user has Innola credentials.
- The transaction is assigned to the logged-in user or the user's group.
- The computer has access to the configured Innola server.
- The working map can load the configured reference layers.
- JAD2001 / EPSG:3448 is used for all parcel geometry and map validation.
- The local case folder is configured and writable.
- The Python environment used by the tool can start.
- ArcPy is available when geoprocessing or output generation requires it.
- An OpenAI API key is configured when AI extraction is required.

Typical installed paths:

- Application root: `C:\Sidwell\ParcelWorkflow`
- Case folders on target computers: `C:\Sidwell\ParcelWorkflow\ParcelWorkflowCases`
- ProgramData logs: `C:\ProgramData\Sidwell\ParcelWorkflow\logs`
- User case folders on development machines: `C:\Users\<user>\Documents\SidwellCo\ParcelWorkflowCases`
- ArcGIS Pro cloned environment: `C:\Sidwell\ParcelWorkflow\envs\arcgispro-survey-ai`

## 4. Logging In

Open ArcGIS Pro and select the `Cadastre tools` ribbon tab.

1. Select `Login`.
2. Confirm the server address is shown.
3. Enter the Innola user name and password.
4. Select `Login`.
5. Confirm the transaction panel shows the logged-in user and connection information.

The server value comes from configuration and is read-only. Users should not edit it during normal work.

[Screenshot placeholder: `01-login.png` - login dialog with server, user, password, Login, and Cancel.]

Expected result:

- The login succeeds.
- The Transactions List panel shows the user name.
- The Refresh button is available.

If login fails:

- Confirm the user name and password.
- Confirm the server URL in settings.
- Confirm the computer can reach the Innola server.
- Check the add-in logs listed in Appendix A.

## 5. Transactions List

The Transactions List panel is the starting point for the workflow.

Main controls:

- Refresh: reloads available transactions from Innola.
- Filter: shows all available active tasks or tasks assigned to the logged-in user.
- Search: filters the visible list by transaction number or text.
- Sort: changes the list order.
- Transaction row: selects the transaction to work on.
- Transaction Info: shows key information about the selected transaction.
- SD: opens Supporting Documents for the loaded transaction.
- M-Geo: opens the map georeference review workflow for the loaded transaction.
- CMP: opens the Compare form for an active Compare task.

Completed transactions are hidden from the active work list.

Buttons that are not valid for the current transaction state are disabled. Disabled buttons should also appear visually inactive and include a tooltip explaining why the action is not available.

Transaction Info should show:

- Transaction
- Task
- Type
- Applicant
- Owner / Responsible
- Status

[Screenshot placeholder: `02-transaction-list.png` - transaction list with filters, transaction info, SD, M-Geo, and CMP.]

Expected result:

- A selected transaction shows its transaction number and task type.
- SD, M-Geo, and CMP are enabled only when valid for the loaded transaction.
- Cancel or Finalize clears the active transaction state and disables transaction-specific actions.

## 6. Compute Workflow

Use Compute for transactions that require survey plan processing, point and line validation, and spatial unit creation.

Typical Compute stages:

1. Supporting Document Check
2. Structure Check
3. Georeference Check
4. Dimension Check
5. Validate Points and Lines
6. Create Spatial Units
7. Final Review
8. Finalize

The workflow panel shows a status tile for each stage. A green tile means the stage completed. A warning or blocker means the user must review the detail before moving forward.

[Screenshot placeholder: `04-compute-workflow-panel.png` - Compute panel showing stage tiles.]

### Supporting Document Check

This stage copies and classifies transaction attachments into the local case folder.

Expected result:

- Required source files are copied.
- The supporting document status is `Completed`.
- Readable documents are available from the Supporting Documents window.

### Structure Check

This stage checks whether the source package contains enough files and structure to continue.

Expected result:

- The process runs without blockers.
- If a blocker appears, the message explains the missing file, invalid file, or Python/ArcPy issue.

### Georeference Check

This stage confirms that the plan has enough location information to build or validate geometry in JAD2001.

Expected result:

- The tool detects JAD2001 coordinates, parish, location text, or other georeference evidence.
- If JAD2001 coordinates are present in the document, they should be treated as reference coordinates.

### Dimension Check

This stage checks bearings, distances, area, and parcel closure information extracted from the plan.

Expected result:

- Bearings and distances are shown for review.
- Warnings can be reviewed by the examiner.
- Blockers must be corrected before moving forward.

## 7. Supporting Documents

Supporting Documents opens a transaction-scoped WPF window. It is used to view the copied source documents while the user works in Compute or Compare.

Use it to:

- Confirm the survey plan PDF.
- Review text or document attachments copied with the transaction.
- Keep the source material visible beside ArcGIS Pro.

Readable files:

- `.pdf`
- `.txt`
- `.doc`
- `.docx`
- `.dwg` when listed as a copied source file, with preview availability depending on installed viewers

Hidden files:

- `.zip`
- `.rar`
- Unsupported archive or packaging files

The window title includes the transaction number, for example `Supporting Documents [TR-100000854]`.

Only the refresh action should be visible. Open and reveal-folder actions are not part of the current training workflow.

[Screenshot placeholder: `05-supporting-documents-window.png` - document selector, refresh icon, and embedded PDF.]

Expected result:

- The document list shows readable files from the transaction case `source` folder.
- The selected PDF displays in the embedded viewer.
- When the transaction is cancelled, suspended, finalized, or closed, the document window clears or closes for that transaction.

## 8. M-Geo Review

M-Geo is used when the examiner needs to compare the scanned survey plan against the map. It creates a transparent image overlay from the source document.

Use M-Geo when:

- The parcel appears offset from imagery or reference layers.
- The plan contains two printed JAD2001 reference coordinates.
- The examiner wants to visually compare plan linework against the map.

Process:

1. Select a loaded transaction.
2. Select `M-Geo`.
3. Choose the source PDF.
4. Capture the PDF view.
5. Pick PDF point 1 and PDF point 2 on the document image.
6. Enter or confirm the matching JAD2001 map/control coordinates.
7. Select `Calculate fit`.
8. Review the distance, rotation, scale, and warning details.
9. Select `Create Overlay`.
10. Review the 70% transparent overlay in the active ArcGIS Pro map.

The overlay is transaction-specific. It is saved in the transaction output geodatabase when possible and can be reloaded later for the same transaction.

[Screenshot placeholder: `06-mgeo-window.png` - PDF view, reference fields, map/control fields, Calculate fit, Create Overlay.]
[Screenshot placeholder: `06a-mgeo-overlay-map.png` - 70% transparent survey plan overlay in ArcGIS Pro.]

Expected result:

- The overlay is aligned using JAD2001 / EPSG:3448 control points.
- The overlay is transparent enough to compare against imagery and cadastre layers.
- If the overlay already exists in the transaction output geodatabase, M-Geo loads the existing overlay instead of forcing the user to recreate it.
- Cancel, Suspend, or Finalize removes transaction-specific M-Geo overlay layers from the map.

Important note:

The two printed document coordinates are reference coordinates. If the bearings and distances reconstruct a parcel that differs from a printed coordinate, the tool should warn the user instead of silently changing the reference coordinate.

## 9. Points Validation Tool

The Points Validation Tool is used to review and correct the extracted parcel interpretation before spatial units are created.

Main areas:

- Source Verification: displays the source PDF.
- Boundary Segments: shows each reviewed segment with from point, to point, bearing, distance, and whether it is used for point generation.
- Points: shows printed, derived, and manually edited points.
- Parcel Interpretation: shows the parcel preview and validation details.

[Screenshot placeholder: `07-points-validation-boundary-segments.png` - boundary segment grid.]
[Screenshot placeholder: `08-points-validation-points.png` - points grid with add, edit, delete controls.]

### Boundary Segments

Review each segment against the PDF:

1. Confirm the from/to points.
2. Confirm bearing.
3. Confirm distance.
4. Confirm whether the segment should be used for point generation.
5. Edit rows when the PDF and extracted values differ.

### Points

Point types:

- Printed/reference points: coordinates printed on the survey plan.
- Derived points: calculated from reviewed bearings and distances.
- Manual points: added or edited by the examiner.

User actions:

- Add point: create a point that is missing from extraction.
- Edit point: correct a label, coordinate, status, or sequence value.
- Delete point: remove an incorrect point. The tool must ask for confirmation before deletion and refresh the list afterward.
- Rebuild points: rebuild derived points from reviewed boundary segments while preserving valid printed reference points.

### Validation Complete

Select `Validation Complete` only when:

- The boundary sequence is correct.
- The required point rows are present.
- No rows still need examiner review.
- Closure validation passes or only allowed warnings remain.

Expected result:

- The tool saves the reviewed points and segments.
- The Compute panel moves to the next stage.

If validation does not complete:

- Read the validation message.
- Check whether any point still needs review.
- Check whether duplicate sequence values exist.
- Check whether a printed reference coordinate conflicts with the bearing/distance reconstruction.
- Check whether segment labels, bearings, or distances were entered incorrectly.

## 10. Create Spatial Units and Final Review

After points and lines are validated, use Create Spatial Units to create the parcel geometry for review.

Expected map behavior:

- The working map is created or reused from configuration.
- The map coordinate system is JAD2001 / EPSG:3448.
- The parcel point layer uses black border and white fill.
- The parcel polygon layer uses configured transparency.
- The Load Layers button loads local file geodatabase outputs when available and zooms to the transaction parcel.

Final Review lets the examiner inspect the generated geometry in ArcGIS Pro before finalizing.

[Screenshot placeholder: `09-create-spatial-units-map.png` - map with parcel points, lines, and polygon.]
[Screenshot placeholder: `10-final-review.png` - final review stage with Load Layers and Mark Reviewed.]

Expected result:

- The generated parcel can be visually inspected.
- The examiner can compare the temporary geometry to reference layers.
- Finalize is available only when the workflow is ready.

## 11. Compare Workflow

Compare is used to review evidence around the parcel and confirm that the transaction can move forward.

Launch Compare:

1. Select or load an active Compare Survey Plan transaction.
2. Select `CMP` from the Transactions List when available.
3. Confirm only one Compare window opens for the transaction.

[Screenshot placeholder: `11-compare-window.png` - Compare window.]

### Load Compare Layers

Use `Load Compare Layers` to load the transaction review geometry and surrounding evidence.

Expected behavior:

- The transaction working polygon is loaded at 60% transparency.
- The active map stays focused on the review parcel.
- Legal Cadastre, Fiscal Cadastre, and Survey Cadastre evidence layers are queried using the configured spatial search mode.
- Neighbor evidence layers are styled as read-only context with dotted lines, 70% or greater transparency, and distinct colors.

Spatial search configuration:

- `intersects`: returns parcels that touch or intersect the review parcel.
- `buffer`: returns parcels within the configured buffer distance in JAD2001 meters.

[Screenshot placeholder: `12-compare-load-layers.png` - loaded review polygon and neighbor evidence layers.]

### Innola Searches

The Compare form supports searches for:

- Name
- PID
- Volume/Folio
- LandVal No.

Use the exact field requested by the form. For Volume/Folio, enter the volume and folio in their separate fields when available.

[Screenshot placeholder: `13-compare-search-results.png` - Compare search results grid.]

Expected result:

- Search results are shown when matching Innola records exist.
- Results can be saved with the Compare review.

## 12. Save, Suspend, Finalize, and Cancel

The action buttons have different meanings. Use the correct action for the state of the work.

| Action | Compute behavior | Compare behavior |
| --- | --- | --- |
| Save | Saves current status and creates the current report/output where applicable. Does not close the task. | Saves current search/evidence state and regenerates the report where applicable. Finalize becomes available after at least one valid save. |
| Suspend | Saves current status, uploads resumable status to the transaction, clears the form, and removes transaction map content. | Saves current status through the lifecycle bridge, uploads resumable status, closes Compare, and removes transaction map content. |
| Finalize | Saves final status, uploads the report/output to the transaction, moves the transaction forward, clears the form, and removes transaction map content. | Saves final status, uploads the PDF report to the transaction, moves the Compare task forward, shows a completion message, closes Compare, and removes transaction map content including M-Geo overlay. |
| Cancel | Cancels the current local operation without saving, clears the form, and removes transaction map content. | Asks for confirmation, closes the Compare operation without saving, and removes Compare and M-Geo transaction map content. |

[Screenshot placeholder: `14-finalize-complete.png` - completion dialog after successful Finalize.]

Expected result after successful Finalize:

- A message confirms the transaction was completed and moved to the next workflow stage.
- Cancel and Suspend are disabled.
- SD, M-Geo, and CMP are disabled for the completed transaction.
- The Transactions List refreshes and no stale transaction content remains.

## 13. Training Use Case

Use this section to run one complete training scenario.

Training transaction:

- Transaction number: `TR-____________`
- Transaction type: `Compute Survey Plan` or `Compare Survey Plan`
- Assigned user: `____________`
- Parish: `____________`
- Source document: `____________`

### Test Steps

| Step | Action | Expected result | Pass/Fail | Evidence |
| --- | --- | --- | --- | --- |
| 1 | Log into the add-in. | User name and connection details are shown. |  | Screenshot `01-login.png` |
| 2 | Refresh Transactions List. | Assigned active transaction is visible. |  | Screenshot `02-transaction-list.png` |
| 3 | Select the transaction. | Transaction Info is populated. |  | Screenshot `03-transaction-info.png` |
| 4 | Start Compute. | Compute panel opens and Supporting Documents opens. |  | Screenshot `04-compute-workflow-panel.png` |
| 5 | Review Supporting Documents. | PDF is visible and readable. |  | Screenshot `05-supporting-documents-window.png` |
| 6 | Run Structure Check. | Stage passes or shows clear actionable warnings. |  |  |
| 7 | Run Georeference Check. | JAD2001, parish, or location evidence is detected. |  |  |
| 8 | Run Dimension Check. | Bearings and distances are available for review. |  |  |
| 9 | Open Points Validation Tool. | Source PDF, segments, points, and preview are visible. |  | Screenshot `07-points-validation-boundary-segments.png` |
| 10 | Correct points/segments if needed. | Edits are saved and preview updates. |  | Screenshot `08-points-validation-points.png` |
| 11 | Select Validation Complete. | Stage completes or shows clear blocker. |  |  |
| 12 | Create Spatial Units. | Parcel geometry is created in the output workspace. |  | Screenshot `09-create-spatial-units-map.png` |
| 13 | Load Layers in Final Review. | Local output layers load and map zooms to parcel. |  | Screenshot `10-final-review.png` |
| 14 | Finalize Compute. | Transaction moves forward or shows clear login/output error. |  | Screenshot `14-finalize-complete.png` |
| 15 | Launch Compare, if assigned. | Compare form opens once. |  | Screenshot `11-compare-window.png` |
| 16 | Load Compare Layers. | Working polygon and neighbor evidence load. |  | Screenshot `12-compare-load-layers.png` |
| 17 | Run Innola searches. | Name, PID, Volume/Folio, or LandVal results are returned when matching records exist. |  | Screenshot `13-compare-search-results.png` |
| 18 | Save Compare. | Search and evidence state is saved and Finalize is enabled. |  |  |
| 19 | Finalize Compare. | Completion dialog appears and transaction moves to next stage. |  | Screenshot `14-finalize-complete.png` |

## 14. User Acceptance Checklist

| Area | Check | Pass/Fail | Evidence/Notes |
| --- | --- | --- | --- |
| Login | User can log in using Innola credentials. |  |  |
| Transaction List | Assigned active transactions appear after refresh. |  |  |
| Transaction List | Completed transactions are hidden from active list. |  |  |
| Transaction List | Disabled buttons are visually disabled and have clear tooltips. |  |  |
| Supporting Documents | SD opens the transaction document window. |  |  |
| Supporting Documents | PDF is visible from the copied case source folder. |  |  |
| M-Geo | M-Geo opens only when a transaction is loaded. |  |  |
| M-Geo | A 70% transparent overlay can be created or reloaded. |  |  |
| Compute | Each stage can be run in order. |  |  |
| Points Validation | Add, edit, delete, and rebuild point workflows behave as expected. |  |  |
| Points Validation | Delete asks for confirmation and refreshes the list. |  |  |
| Spatial Units | Generated geometry is in JAD2001 / EPSG:3448. |  |  |
| Final Review | Load Layers loads local output layers and zooms to parcel. |  |  |
| Compare | CMP opens one Compare form only. |  |  |
| Compare | Legal, Fiscal, and Survey neighbor evidence loads from configuration. |  |  |
| Compare | Name, PID, Volume/Folio, and LandVal searches work as expected. |  |  |
| Compare | Save persists searches and enables Finalize. |  |  |
| Finalize | Successful finalize shows a completion dialog and moves the task forward. |  |  |
| Cleanup | Cancel, Suspend, and Finalize remove transaction-specific map layers. |  |  |
| Installer | Logs and installation summary are created. |  |  |

## 15. Troubleshooting

### Login Failed

What to check:

- User name and password.
- Server URL in Settings.
- Network access to the Innola server.
- Whether the Innola session expired.

Expected recovery:

- Sign in again.
- Refresh the Transactions List.

### Transaction Does Not Start

What to check:

- The transaction is assigned to the user or user's group.
- The task is not already completed.
- The add-in is logged in.
- The case folder can be created or reopened.

### Supporting Documents Window Is Empty

What to check:

- Case folder exists.
- `source` folder contains readable files.
- PDF is not locked or deleted.
- The selected transaction number matches the folder.

Example folder:

- `C:\Sidwell\ParcelWorkflow\ParcelWorkflowCases\<transaction>\source`

### Configured Python Executable Could Not Be Invoked

What to check:

- Configured Python path exists.
- User has permission to run Python from that folder.
- The path does not point to a protected ArcGIS Pro environment when local security blocks execution.
- The cloned environment exists at the configured target path.

### ArcPy Is Not Available

What to check:

- ArcGIS Pro is installed.
- ArcGIS Pro is licensed.
- The Python environment matches the installed ArcGIS Pro version.
- ArcPy can be imported inside the active ArcGIS Pro session.

Support command:

`python -c "import arcpy; print(arcpy.GetInstallInfo())"`

### Product License Has Not Been Initialized

This can happen when ArcPy is started outside a licensed ArcGIS Pro session or from an elevated/service context that cannot access the named-user license.

What to do:

- Sign into ArcGIS Pro.
- Confirm the license page shows a valid license.
- Start processing from inside ArcGIS Pro.

### OpenAI API Key Missing

What to check:

- `OPENAI_API_KEY` is set for the user or machine.
- The key was entered during installation if required.
- The Python process can read the environment variable.

Support command:

`python -c "import os; print(bool(os.getenv('OPENAI_API_KEY')))"`

### Georeference Check Blocked

What to check:

- The document contains JAD2001 coordinates, parish, or clear location reference.
- Coordinates were extracted with correct Northing/Easting values.
- The map is using JAD2001 / EPSG:3448.

### Points Validation Blocked

What to check:

- Any row still needs review.
- Segment sequence is continuous.
- No duplicate point sequence values exist.
- Printed reference points are correct.
- Bearings and distances match the PDF.

### Spatial Unit Creation Failed

What to check:

- Validate Points and Lines is completed.
- Output geodatabase exists.
- Local output layers were created.
- Innola login/session is still valid if final upload is required.

### Compare Search Failed

What to check:

- Innola session is still valid.
- Search field is correct: Name, PID, Volume/Folio, or LandVal.
- For Volume/Folio, confirm volume and folio are entered separately where required.
- Confirm the record exists in the portal.

### Enterprise Token or SSL Error

What to check:

- ArcGIS Pro is signed into the correct portal.
- Portal token is available from the Pro session.
- SSL certificates are trusted by the target computer.
- Enterprise URLs in Settings are correct.

### ArcGIS Pro Crash

Crash dumps are usually stored under:

- `C:\Users\<user>\AppData\Local\ESRI\ErrorReports`

Capture the dump file path, transaction number, and the action being performed when the crash occurred.

## Appendix A. Log Locations

Common logs:

- `C:\ProgramData\Sidwell\ParcelWorkflow\logs`
- `C:\Sidwell\ParcelWorkflow\logs`
- `C:\Sidwell\ParcelWorkflow\ParcelWorkflowCases\<transaction>\process.log`
- `C:\Sidwell\ParcelWorkflow\ParcelWorkflowCases\<transaction>\output`
- `C:\Users\<user>\Documents\SidwellCo\ParcelWorkflowCases\<transaction>`

Installer summary files:

- `installation_summary.txt`
- `installation_summary.json`
- `setup_arcgispro37_environment_status.json`
- `setup_arcgispro37_environment_bat.log`
- `register_parcel_workflow_addin_bat.log`

## Appendix B. Configuration Notes

Coordinate system:

- All generated parcel geometry must use JAD2001 / EPSG:3448.
- If the ArcGIS Pro map shows Web Mercator because of a basemap, generated parcel layers still need to be stored and processed in JAD2001.

Map layers:

- Legal Cadastre
- Fiscal Cadastre
- Survey Cadastre
- Open Basemap Streets / OpenStreetMap
- Esri imagery
- World Topographic
- World Hillshade

Compare spatial search:

- Use `intersects` for touching/intersecting parcels only.
- Use `buffer` when surrounding parcels within a configured distance are required.

## Appendix C. Screenshot Checklist

| File name | Capture note | Captured |
| --- | --- | --- |
| `01-login.png` | Login dialog with read-only server value. |  |
| `02-transaction-list.png` | Transaction list, filters, and toolbar. |  |
| `03-transaction-info.png` | Transaction Info section. |  |
| `04-compute-workflow-panel.png` | Compute stage tiles. |  |
| `05-supporting-documents-window.png` | Supporting Documents window with PDF. |  |
| `06-mgeo-window.png` | M-Geo form with point fields. |  |
| `06a-mgeo-overlay-map.png` | Transparent georeferenced PDF overlay on map. |  |
| `07-points-validation-boundary-segments.png` | Boundary Segments tab. |  |
| `08-points-validation-points.png` | Points tab with action buttons. |  |
| `09-create-spatial-units-map.png` | Created parcel layers in ArcGIS Pro. |  |
| `10-final-review.png` | Final Review stage. |  |
| `11-compare-window.png` | Compare form. |  |
| `12-compare-load-layers.png` | Compare layers loaded with neighbor evidence. |  |
| `13-compare-search-results.png` | Search results from Innola. |  |
| `14-finalize-complete.png` | Completion confirmation dialog. |  |

## Appendix D. Training Signoff

| Reviewer | Role | Date | Result | Notes |
| --- | --- | --- | --- | --- |
|  | SMD product owner |  |  |  |
|  | Plan examiner |  |  |  |
|  | Implementation team |  |  |  |
|  | Support lead |  |  |  |
