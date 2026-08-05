# Story 10.1: Create User Training and Test Guide for Parcel Workflow Extension

Status: review

## Story

As an SMD training lead and cadastral examiner,
I want a Word-based user training and test guide for the Parcel Workflow Extension,
so that users can learn, execute, and validate the Compute and Compare workflows using assigned Innola transactions.

## Business Context

The Parcel Workflow Extension is used after a transaction is created and assigned in the Innola framework. The user logs into the ArcGIS Pro add-in, selects the assigned transaction, completes the Compute workflow, then completes the Compare workflow so the transaction can return to the Innola/e-title workflow for the next stage.

Training material must explain the end-to-end process in user language, not developer language. It must also support user acceptance testing with clear expected outcomes, screenshot placeholders, and troubleshooting guidance for common deployment and processing issues.

## Acceptance Criteria

1. A Microsoft Word document is created for the user training and test guide.
2. The guide explains the end-to-end workflow: Innola transaction assignment, add-in login, transaction selection, Compute execution, Compare execution, and return to the Innola workflow.
3. The guide lists prerequisites, including ArcGIS Pro, installed add-in, Innola credentials, assigned transaction, configured folders, map/layer access, JAD2001/EPSG:3448 expectations, Python/ArcPy readiness, and OpenAI key requirements where AI extraction is used.
4. The guide describes the Login and Transaction List panel, including refresh, filters, transaction selection, transaction info, enabled/disabled buttons, `SD`, `M-Geo`, and `CMP`.
5. The guide describes the Compute workflow stages: Supporting Document Check, Structure Check, Georeference Check, Dimension Check, Validate Points and Lines, Create Spatial Units, Final Review, Finalize, Suspend, and Cancel.
6. The guide describes the Points Validation Tool, including source PDF review, boundary segment review, point review, add/edit/delete behavior, delete confirmation, fixed reference coordinates, rebuild points, save, and validation complete.
7. The guide describes M-Geo, including when to use it, choosing document reference points, choosing map/control points, JAD2001 validation, creating a 70% transparent overlay, saving the overlay with the transaction output GDB, and cleanup behavior.
8. The guide describes Supporting Documents, including transaction-scoped launch, readable file types, PDF viewing, refresh behavior, and close/cleanup behavior.
9. The guide describes the Compare workflow, including launching Compare, loading compare layers, loading the transaction working polygon, Legal/Fiscal/Survey neighbor evidence, spatial search mode, Innola searches by name, PID, Volume/Folio, and LandVal, save, finalize, and cancel.
10. The guide clearly explains Save, Suspend, Finalize, and Cancel behavior for Compute and Compare, including report/output expectations, transaction movement, map cleanup, form cleanup, and completion confirmation messages.
11. The guide includes at least one complete test use case with placeholders for a training transaction number, screenshots, expected results, and pass/fail evidence.
12. The guide includes screenshot placeholders for every major form and step, with recommended filenames and capture notes.
13. The guide includes troubleshooting guidance for login failure, missing/invalid OpenAI key, configured Python not invoked, ArcPy unavailable or license not initialized, missing supporting documents, map/layer load issues, georeference blockers, point validation blockers, spatial unit creation failures, compare search failures, enterprise token/SSL issues, and ArcGIS Pro crash dump locations.
14. The guide includes a user acceptance test checklist with pass/fail/evidence columns.
15. The guide avoids internal implementation jargon in the main workflow and keeps technical details in a support appendix.
16. The guide is reviewed by the SMD/product team for workflow accuracy and by the implementation team for technical accuracy before release.

## Tasks / Subtasks

- [x] Create the Word guide structure and screenshot inventory.
- [x] Draft the overview, prerequisites, roles, and end-to-end workflow.
- [x] Document Login and Transaction List behavior.
- [x] Document the full Compute workflow and stage gates.
- [x] Document the Points Validation Tool, including manual correction and rebuild-point behavior.
- [x] Document Supporting Documents and M-Geo workflows.
- [x] Document the Compare workflow, including query/search and neighbor evidence review.
- [x] Add a training use case with expected results and screenshot placeholders.
- [x] Add troubleshooting and log-location appendix.
- [x] Add UAT checklist with pass/fail/evidence fields.
- [x] Produce the final `.docx` and verify package integrity; Word visual review remains a release-review step.

## Recommended Document Structure

1. Purpose and Audience
2. Workflow Overview
3. Before You Start
4. Logging In
5. Transaction List
6. Compute Workflow
7. Supporting Documents
8. M-Geo Review
9. Points Validation Tool
10. Create Spatial Units and Final Review
11. Compare Workflow
12. Completing, Suspending, or Cancelling Work
13. Training Use Case
14. User Acceptance Test Checklist
15. Troubleshooting and Support Logs
16. Appendix: Configuration and Installed Paths

## Screenshot Inventory

- `01-login.png`
- `02-transaction-list.png`
- `03-transaction-info.png`
- `04-compute-workflow-panel.png`
- `05-supporting-documents-window.png`
- `06-mgeo-window.png`
- `07-points-validation-boundary-segments.png`
- `08-points-validation-points.png`
- `09-create-spatial-units-map.png`
- `10-final-review.png`
- `11-compare-window.png`
- `12-compare-load-layers.png`
- `13-compare-search-results.png`
- `14-finalize-complete.png`

## Writer Notes

- Primary output should be a Word document, recommended path: `docs/training/Sidwell_Cadastre_Tools_User_Training_and_Test_Guide.docx`.
- A Markdown source draft may also be kept at `docs/training/parcel-workflow-extension-user-training-and-test-guide.md` if it helps review and versioning.
- Screenshots should be stored under `docs/training/screenshots/`.
- Do not include real passwords, bearer tokens, API keys, or private user credentials.
- Use configurable folder paths as examples, including:
  - `C:\Sidwell\ParcelWorkflow\ParcelWorkflowCases`
  - `C:\ProgramData\Sidwell\ParcelWorkflow\logs`
  - `C:\Users\<user>\Documents\SidwellCo\ParcelWorkflowCases`
- Use `JAD2001 / EPSG:3448` consistently in coordinate-system examples.
- Main instructions should be written for nontechnical users. Place log paths, Python/ArcPy checks, and installer diagnostics in troubleshooting appendices.

## Test Guidance

- Open the generated `.docx` in Microsoft Word.
- Verify screenshot placeholders are present and named consistently.
- Run the guide against one training Compute transaction and one training Compare transaction.
- Confirm no secrets are present in text, images, metadata, or examples.
- Confirm the UAT checklist can be completed by a user without developer assistance.

## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-08-05 | 0.1 | Initial story for user training and test guide. | Paige |
| 2026-08-05 | 0.2 | Implemented Markdown source, Word guide artifact, screenshot inventory, UAT checklist, and troubleshooting appendix. | Paige |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `python docs\training\build_training_guide_docx.py`
- `python -c "import zipfile; ... z.testzip() ..."` returned `docx_bad_member=None`.
- `python -c "import zipfile; ... word/document.xml ..."` confirmed core sections: Purpose, M-Geo Review, User Acceptance Checklist, Troubleshooting.
- `python ...\render_docx.py ...` could not render preview PNGs because the local environment is missing `pdf2image`.

### Completion Notes

- Created a Word-oriented training and test guide for the Parcel Workflow Extension covering Login, Transaction List, Compute, Supporting Documents, M-Geo, Points Validation, Create Spatial Units, Final Review, Compare, Save/Suspend/Finalize/Cancel behavior, troubleshooting, and UAT evidence capture.
- Added screenshot placeholders and a screenshot capture inventory. Real screenshots still need to be inserted during training material review.
- Kept a Markdown source draft beside the generated `.docx` so future guide updates can be versioned and regenerated.
- Generated `.docx` package integrity was validated. Final visual review in Microsoft Word is still required before release.

### File List

- `docs/training/parcel-workflow-extension-user-training-and-test-guide.md`
- `docs/training/Sidwell_Cadastre_Tools_User_Training_and_Test_Guide.docx`
- `docs/training/build_training_guide_docx.py`
- `docs/training/screenshots/README.md`
