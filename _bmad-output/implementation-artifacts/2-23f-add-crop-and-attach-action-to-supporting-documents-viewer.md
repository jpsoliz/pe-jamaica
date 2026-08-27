---
baseline_commit: fe14dab
parent_story: 2-23e-add-pla-b-plan-annexation-from-pe-workflow-and-test-ux.md
related_stories:
  - 9-2-add-supporting-documents-full-panel-viewer.md
  - 5-14-replace-embedded-pdf-browser-with-unified-rendered-document-viewer-for-pdf-and-raster-verification.md
  - 2-23e-add-pla-b-plan-annexation-from-pe-workflow-and-test-ux.md
---

# Story 2.23F: Add Crop And Attach Action To Supporting Documents Viewer

Status: review

## Story

As an SMD examiner reviewing PLA_B evidence,
I want to crop a selected region from a loaded PDF, PNG, JPG, or TIFF in the Supporting Documents viewer and attach it as a high-resolution PNG,
so that the survey diagram evidence can be captured and uploaded to the transaction without leaving the PLA_B review workflow.

## Business Context

Story 2.23E loads PLA_B recovery evidence using the current PLA transaction and related PE transaction. It deliberately does not reuse PLA_A extraction/review/finalize UX. The next needed PLA_B capability is a controlled way to capture the survey diagram image from any supported loaded Supporting Documents file and attach that generated PNG to the Current TR in Innola under a configured document/source type.

The standard Supporting Documents viewer already exists from Story 9.2 as a separate WPF `ProWindow` for transaction-scoped copied documents. Story 5.14 documents the preferred rendered-document direction: use controlled PDF/raster rendering for predictable zoom, page, and image behavior instead of relying on browser pixels. This story extends the Supporting Documents viewer with crop/attach behavior while preserving its current role as the standard transaction document surface.

## Acceptance Criteria

1. Given a transaction is loaded and the Supporting Documents window is open, when the selected document is a PDF, PNG, JPG/JPEG, TIFF, or TIF copied into the active case folder, then a `Crop` action is available in the viewer toolbar.
2. Given no document is selected, the selected document is missing, or the selected document type is unsupported for cropping, then the `Crop` action is disabled with a clear tooltip/status reason.
3. Given the examiner clicks `Crop`, then a crop mode or dedicated crop window opens for the selected document without disrupting the normal Supporting Documents viewer selection.
4. Given the selected document is a multi-page PDF or TIFF, then the crop surface lets the examiner choose the page/frame to crop and shows the current page/frame position.
5. Given the crop surface is open, when the examiner drags a rectangular region, then the selected region is visibly overlaid on the rendered page and can be adjusted or cleared before saving.
6. Given the examiner saves the crop, then the selected region is rendered from the source document to PNG using controlled document coordinates, not a screenshot of the visible viewer.
7. Given the crop is saved, then the PNG is written under the current case folder at the fixed path `working/pla_b/survey_diagram_selection.png`, overwriting the prior generated crop for that case.
8. Given the crop is saved, then metadata is written beside the PNG recording source file, source relative path, page/frame number, crop rectangle in source coordinates, crop rectangle in preview pixels when available, origin convention, requested DPI, output path, Current TR, PE number when available, created/updated timestamps, upload status, and configured source type.
9. Given the examiner chooses `Attach`, then the saved PNG is uploaded to the Current TR Innola transaction, not the PE transaction and not any PLA_A transaction context.
10. Given `st_plan_annex_image` is configured, then it is used as the default upload source/document type for this story; if the source type is missing or invalid, upload is blocked with a non-secret configuration diagnostic.
11. Given the upload succeeds, then local evidence records the uploaded status and the Supporting Documents/crop UI reports success without completing or finalizing the transaction.
12. Given the upload fails, then the PNG and metadata remain in the case folder, the failure category/message is preserved for retry, and the transaction is not marked complete.
13. Given the examiner reopens the same transaction case folder, then the last saved PLA_B crop evidence can be detected and surfaced as available for attach/retry.
14. Given the examiner changes transaction or closes/suspends/finalizes/cancels the active transaction, then the crop window closes, in-flight render/upload work is canceled where possible, unsaved selections are discarded, saved PNG evidence remains only in its original case folder, and crop state cannot leak into another transaction.
15. Given the selected crop would create an extremely large PNG, then the UI estimates or validates output dimensions/file size and warns the user before upload; the user may continue unless a hard technical renderer/upload limit is reached.
16. Given the crop is generated from a good-resolution color PDF or raster image, then the PNG preserves color and is generated using the selected DPI/output-resolution behavior.
17. Given the examiner needs higher detail, then DPI options include at least `200`, `300`, `400`, and `600`, with `300` as the default; for raster images, follow image-processing best practices and do not imply that higher DPI can recover detail not present in the source pixels.
18. Given the crop feature is implemented, then existing Supporting Documents behaviors from Story 9.2 continue to work: document selector, refresh, PDF viewing fallback, image/text preview, and transaction-scoped cleanup.
19. Given the Supporting Documents viewer is shared, then the `Crop` action is available globally for supported loaded documents, while attach still requires a resolvable Current TR and does not open or require PLA_A plan-annexation extraction, PLA_A review, or PLA_A finalize behavior.

## Tasks / Subtasks

- [x] Add crop entry point to Supporting Documents viewer. (AC: 1-3, 18)
  - [x] Update `SupportingDocumentsWindow.xaml` toolbar with a compact `Crop` button near Refresh.
  - [x] Add command and status/tooltip state to `SupportingDocumentsDockpaneViewModel.cs`.
  - [x] Enable crop only for selected copied PDF/PNG/JPG/JPEG/TIFF/TIF documents inside the active case folder.
  - [x] Preserve current document selector and refresh behavior.

- [x] Add crop selection UI. (AC: 3-5, 14, 17)
  - [x] Create a dedicated WPF crop window or explicit crop mode; prefer a dedicated window if WebView coordinate capture is unreliable.
  - [x] Render the selected page/frame into a WPF image surface with scroll/zoom support.
  - [x] Add visible rectangle selection with drag, adjust, clear, cancel, `Save PNG`, and `Attach` actions.
  - [x] Add DPI selector with `200`, `300`, `400`, `600`; default to `300`.
  - [x] Require `Save PNG` before `Attach`; `Attach` is disabled until a saved PNG exists for the active case.
  - [x] If the crop window closes before `Save PNG`, discard unsaved selection state; if a PNG has already been saved, keep it available for attach/retry.

- [x] Implement controlled crop rendering service. (AC: 4, 6-8, 15-17)
  - [x] Add a parallel generic crop/render pipeline near `Workflow/Review/RenderedReviewDocumentService.cs` for page/frame rendering and crop export; keep it separate from the existing PLA_A and preliminary PLA_B survey-selection renderer paths.
  - [x] Do not crop from screen pixels or WebView screenshots.
  - [x] Convert UI rectangle coordinates back to source page coordinates.
  - [x] Render the selected PDF/raster region to PNG at requested DPI while preserving color.
  - [x] Store PDF crop coordinates in PDF points, store raster crop coordinates in source pixels, record preview-pixel coordinates when available, and use top-left origin consistently in metadata.
  - [x] Validate bounds, empty selections, missing files, invalid page/frame, and excessive output dimensions/file size; warn for large outputs and block only when a hard renderer/upload limit is reached.

- [x] Persist PLA_B crop evidence. (AC: 7-8, 13-14)
  - [x] Implement a new metadata/persistence path for this Supporting Documents crop flow; do not make it depend on the existing PDF-only `PlaBSurveyDiagramSelectionService`.
  - [x] Save PNG to `working/pla_b/survey_diagram_selection.png` and JSON metadata beside it under `working/pla_b`.
  - [x] Store case-relative paths only; reject traversal or paths outside the active case folder.
  - [x] Restore existing crop metadata on reopen only when source and PNG still exist for the same Current TR.
  - [x] Record both local save status and upload status so a future finalize step can avoid duplicate generated-evidence upload.

- [x] Attach generated PNG to Innola. (AC: 9-12, 19)
  - [x] Reuse `IInnolaTransactionDetailService.UploadAttachmentAsync`; do not create a parallel Innola upload client.
  - [x] Add/verify the generated crop upload source/document type in configuration as `st_plan_annex_image`.
  - [x] Resolve source type from configuration, defaulting to `st_plan_annex_image`.
  - [x] Always upload to the Current TR represented by the active transaction/case context; never upload the crop image to the PE transaction.
  - [x] Upload as `image/png`.
  - [x] Persist uploaded/failed status for retry without completing the task.
  - [x] Keep final transaction completion/finalize as a later workflow step, not part of this story.

- [x] Add focused tests. (AC: 1-19)
  - [x] Supporting Documents crop command enablement for PDF/PNG/JPG/TIFF and disabled state for unsupported/missing documents.
  - [x] Crop request validation for invalid page, empty rectangle, out-of-bounds rectangle, and unsupported file type.
  - [x] Metadata persistence and safe case-relative path restoration.
  - [x] DPI selection/default behavior and output-size guard.
  - [x] Innola upload success/failure preserving local evidence and retry state.
  - [x] Regression tests proving standard Supporting Documents viewer behavior remains intact.

## Dev Notes

- This story extends the standard Supporting Documents WPF window from Story 9.2. Do not reintroduce the disabled ArcGIS dockpane host for Supporting Documents.
- Story 5.14 is the viewer-direction reference: crop from a controlled rendered page/raster service, not from browser pixels or screen capture.
- Crop output should be implemented as generated evidence for the Current TR, not as a normal source attachment downloaded from Innola and not as evidence for the PE transaction.
- Existing relevant files:
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentsWindow.xaml`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentsDockpaneViewModel.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/SupportingDocumentWorkspaceProjection.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/RenderedReviewDocumentService.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaBWorkflowServices.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/IInnolaTransactionDetailService.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `RenderedReviewDocumentService` currently renders raster/image sources but not PDF pages; the implementation must add a nearby generic crop renderer for PDF and raster sources using existing ArcGIS/Python/PDF tooling already available in the project.
- Add/verify the generated crop upload source/document type `st_plan_annex_image` in configuration; do not reuse `st_survey_diagram_png` for this story.
- The current Supporting Documents PDF path may still use WebView/browser projection depending on settings. That is acceptable for viewing, but crop export must use a deterministic render pipeline.
- DPI guidance:
  - Default: `300`.
  - Options: `200`, `300`, `400`, `600`.
  - Higher DPI is allowed, but the UI/service must warn for huge output dimensions and upload-unfriendly PNG size.
  - For PDF sources, selected DPI controls rasterization detail.
  - For raster sources, selected DPI/output-resolution handling must follow image best practices and preserve source quality without claiming to recover unavailable detail.
- Coordinate guidance:
  - PDF source crop coordinates are stored in PDF points.
  - Raster source crop coordinates are stored in source pixels.
  - Preview/UI rectangle coordinates are stored as pixels when available.
  - Metadata must declare top-left origin.
- Security/path rules:
  - Only crop copied case-folder documents.
  - Never use remote attachment URLs directly in the crop renderer.
  - Never persist Innola tokens or raw upload payloads in metadata/logs.
  - Store only non-secret diagnostics.
- UX guidance from Sally:
  - Keep normal document viewing as the default mode.
  - Make crop an explicit action/mode.
  - Show a preview/confirmation before upload.
  - Keep controls compact: document selector, Refresh, Crop, DPI selector in the crop surface, Save PNG, Attach, Close.
  - Closing the crop window discards unsaved selection state; saved PNG evidence remains available for attach/retry in the same Current TR case folder.

### Project Structure Notes

- New crop UI files should live beside existing WPF viewer files if they are Supporting Documents-specific, for example `SupportingDocumentCropWindow.xaml` and `.xaml.cs`.
- New pure crop/render/persistence services should live under `Workflow/Review` or `Workflow/Pla` depending on ownership:
  - Generic document crop/render logic: `Workflow/Review`.
  - PLA_B survey diagram evidence metadata/upload orchestration: `Workflow/Pla`.
- Add tests to the existing executable harness under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests`; do not introduce xUnit/NUnit.

### References

- [Story 9.2 Supporting Documents viewer](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/implementation-artifacts/9-2-add-supporting-documents-full-panel-viewer.md)
- [Story 5.14 rendered document viewer direction](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/implementation-artifacts/5-14-replace-embedded-pdf-browser-with-unified-rendered-document-viewer-for-pdf-and-raster-verification.md)
- [Story 2.23E PLA_B recovery workflow](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/implementation-artifacts/2-23e-add-pla-b-plan-annexation-from-pe-workflow-and-test-ux.md)
- [Project context](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/project-context.md)

## Testing Notes

- Test with PLA_B current TR `100000724` and PE `100000628` after source files are loaded by story 2.23E.
- Test with representative PDF/PNG/JPG/TIFF files. User-provided local example folder for manual/dev fixtures: `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\ScannedImages`.
- Test at least one good-resolution color PDF and verify output PNG dimensions increase with DPI.
- Test 600 DPI with a large selected region and verify the guard warns before unreasonable output/upload and only blocks on a hard technical limit.
- Test upload against the configured Innola route in mock/unit seams first, then manually in ArcGIS Pro.
- Test transaction switching: crop selection and saved evidence must not appear under the wrong TR.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /t:Rebuild /p:UseSharedCompilation=false` - passed; existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "supporting documents"` - passed, 11 tests.
- `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll` - partial run passed through the new Supporting Documents crop tests, then stopped at existing ArcGIS runtime boundary: `ArcGIS.Desktop.Mapping, Version=13.6.0.0` missing in `SpatialOverlapReviewPersistenceServiceTests.OverlapReviewServiceBlocksWhenNoTargetsAreConfigured`.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` - blocked by local `obj\Debug\net8.0-windows` access-denied errors while writing generated build files.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=D:\Code\BMad-Method\dev\pe-jamaica\.tmp\obj\ /p:BaseOutputPath=D:\Code\BMad-Method\dev\pe-jamaica\.tmp\bin\` - passed; existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\.tmp\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "supporting documents"` - passed, 11 tests.
- `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\.tmp\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "innola attachment upload"` - passed, 5 tests.

### Completion Notes List

- Story created from PLA_B crop/attach feasibility discussion and linked to Supporting Documents viewer lineage.
- Added global Supporting Documents crop eligibility, toolbar command, and status/tooltip behavior for copied case-folder PDF/PNG/JPG/JPEG/TIFF/TIF documents.
- Added dedicated `SupportingDocumentCropWindow` with page/frame navigation, default 300 DPI selector, rectangle drag overlay, clear/cancel, Save PNG, and Attach actions; it closes on active transaction/case changes and cancels preview work.
- Added generic controlled crop rendering/export service for raster sources and pypdfium2-backed PDF preview/export, with source-coordinate conversion, bounds checks, DPI validation, and output-size warning/hard-block behavior.
- Added separate PLA_B Supporting Documents crop evidence persistence/upload flow that writes `working/pla_b/survey_diagram_selection.png` plus metadata, restores only for the same Current TR, and uploads PNG to Current TR using configured `st_plan_annex_image`.
- Added `st_plan_annex_image` as an internal PNG generated-evidence source type in safe defaults and checked-in workflow settings.
- Added focused crop tests and Supporting Documents XAML smoke assertions for eligibility, invalid requests, metadata restore, DPI guard, upload success/failure, retry state, and existing viewer regression coverage.
- Added save/attach UX confirmations: after Save PNG the crop window reports `File was saved: {path}`, prompts before attaching the saved PNG to the Current TR number, and reports an attachment-complete message after successful upload.
- Patched the shared Innola attachment upload seam so transport exceptions return and persist safe diagnostic detail, including inner exception context, while redacting token/password/secret/raw structured diagnostics.
- Added configurable attachment upload auth mode (`innola_attachment_upload_auth_mode`) with default `access_token_then_bearer`; attachment upload now retries retryable access-token transport failures and 401/403 responses with `Authorization: Bearer`.
- Attachment upload results now carry route diagnostics, and PLA_B crop metadata persists upload route, binding mode, upload mode, auth mode used, task value, content type, and byte count.
- Confirmed live TR `100000724` metadata showed Bearer auth was attempted and still failed during multipart stream copy; exploratory alternate-route, PDF, and source-type fallbacks were reverted so this story remains strict to the approved generated PNG attachment type.
- Deep-dive validation confirmed Current TR binding and local `st_plan_annex_image` configuration are correct. The user's Swagger browser check prompted for a client certificate, while local store scans did not find the configured certificate through the add-in's manual lookup path.
- Patched the crop upload path to use the shared `ShellState.TransactionDetails` Innola client, patched default Innola detail/transaction services to use `InnolaHttpClientFactory`, and added automatic Windows client-certificate selection when the configured manual certificate is not found.

### File List

- `_bmad-output/implementation-artifacts/2-23f-add-crop-and-attach-action-to-supporting-documents-viewer.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ComputeAttachmentSourceTypeCatalog.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaResumePackageConventions.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentCropWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentCropWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentsDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentsWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaBSupportingDocumentCropService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaBWorkflowServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/DocumentCropRenderingService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/SupportingDocumentWorkspaceProjection.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/JamaicaReviewWorkspaceXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/SupportingDocumentCropTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-08-27 | 0.1 | Implemented Supporting Documents crop/attach flow for PLA_B generated image evidence. | Codex |
| 2026-08-27 | 0.2 | Added saved-file, attach-confirmation, and attach-complete crop window messages. | Codex |
| 2026-08-27 | 0.3 | Preserved safe transport diagnostics for failed Innola attachment uploads. | Codex |
| 2026-08-27 | 0.4 | Added configurable attachment upload auth fallback and persisted upload route diagnostics in PLA_B crop metadata. | Codex |
| 2026-08-27 | 0.5 | Added alternate scanning attach route fallback after primary upload route transport closure. | Codex |
| 2026-08-27 | 0.6 | Added PLA_B crop PDF fallback after PNG transport upload failure. | Codex |
| 2026-08-27 | 0.7 | Added fallback to original transaction source document type for crop PDF evidence. | Codex |
| 2026-08-27 | 0.8 | Reverted exploratory upload fallbacks; restored strict `st_plan_annex_image` PNG attach and routed crop upload through the shared certificate-aware Innola client with automatic certificate selection fallback. | Codex |
