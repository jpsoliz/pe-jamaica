---
baseline_commit: 99b349cd34935fbe185e43e10ef782878708bd56
---

# Story 9.2: Add Supporting Documents Full-Panel Viewer

Status: done

## Story

As a plan examiner,  
I want a full-panel Supporting Documents workspace for the loaded transaction,  
so that I can review all readable transaction attachments in a dedicated ArcGIS Pro pane beside Contents, Catalog, and Parcel Workflow.

## Business Context

Transaction attachments are already copied into the case folder and used by several workflow stages, but the user does not have one clear place to browse all supporting documents for the active transaction.

This story originally targeted a transaction-scoped ArcGIS Pro document viewer dockpane. Field testing showed the ArcGIS dockpane host can create a blank tab even when the view-model successfully restores the copied source files. The current accepted implementation is therefore a separate WPF `ProWindow` document viewer that uses the same transaction-scoped data and closes or refreshes with the workflow lifecycle.

## Acceptance Criteria

1. Given no transaction is loaded, when the Supporting Documents workspace is opened, then the workspace is disabled or immediately closed and no stale document from a previous transaction is visible.
2. Given a transaction is loaded, then the add-in opens a WPF window titled `Supporting Documents [TR-<transaction-number>]` for reviewing copied transaction documents. The ArcGIS dockpane registration is disabled until the blank dockpane rendering issue is resolved.
3. Given readable transaction attachments exist, then the top of the panel shows a combo box listing only supported readable files.
4. Given the attachment list contains `.zip` or `.rar` files, then those archive files are not shown in the document combo box.
5. Given the attachment list contains unsupported files, then those files are not shown unless they are explicitly supported by the viewer policy.
6. Given the attachment list contains `.pdf`, then the selected PDF opens in the WPF window's embedded PDF viewer when available and falls back to a document-specific error state when embedded viewing is unavailable.
7. Given the attachment list contains `.txt`, then the selected text file is rendered read-only in the panel with scrolling.
8. Given the attachment list contains `.doc`, `.docx`, or `.dwg`, then the file can appear in the combo box only if it was copied into the case folder, but the window may show a preview-unavailable state unless a supported embedded viewer exists.
9. Given no readable attachments exist for the loaded transaction, then the panel shows an empty state, disables the combo box, and keeps the active transaction number visible.
10. Given the user changes, cancels, suspends, finalizes, closes, or unloads the active transaction, then the supporting-document list, selected document, viewer state, and tab title are refreshed or cleared for the new state.
11. Given one selected document fails to render or is missing from disk, then the panel shows a document-specific error and keeps the remaining readable documents available.
12. Given a document is opened from this workspace, then the file path used by the viewer comes from the copied case-folder attachment, not a remote URL or uncopied source.

## UX Reference

Sally recommendation: make this a document workspace, not another nested card or tab inside the workflow stages. The current production-safe surface is a separate WPF window: selector at the top, refresh-only action, document taking the rest of the space.

```text
ArcGIS Pro + WPF supporting document window
┌──────────────────────────────────────────────────────────────────────────────┐
│ Supporting Documents [TR-1000099999]                                           │
├──────────────────────────────────────────────────────────────────────────────┤
│ Document  [ DOC_PLAN_492949_A.pdf                              v ] [Refresh]│
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│                              PDF / TXT VIEWER                                │
│                                                                              │
│  For PDF: embedded viewer, zoom/scroll/page controls when available.          │
│  For TXT: read-only text surface with vertical and horizontal scroll.         │
│  For DOC/DOCX/DWG fallback:                                                   │
│                                                                              │
│      Preview is not available for this file type.                             │
│      Open the source document or open the case folder copy.                   │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

### UX Notes

- The combo box belongs at the top and should keep the selected document visible even while the user scrolls the document content.
- Use readable file names first. Add role/type labels only as secondary text, for example `Survey plan - DOC_PLAN_492949_A.pdf`.
- Hide archives from this panel rather than showing them disabled.
- Do not promise embedded DWG viewing in this story. The safe first behavior is to list copied DWG files and show a clear preview-unavailable state.
- Do not show Open or Reveal actions in the current window; keep only the refresh icon.
- Include `.docx` with `.doc`; most modern Word attachments will use `.docx`.
- The empty state should be plain and operational: `No readable supporting documents are available for this transaction.`

## Tasks / Subtasks

- [x] Add a transaction-scoped Supporting Documents WPF window. (AC: 1, 2, 9, 10)
  - [x] Show `Supporting Documents [TR-<transaction-number>]` only when a transaction is loaded.
  - [x] Disable or close the workspace when no transaction is loaded.
  - [x] Clear selected document and viewer state on transaction unload/change.

- [x] Build the supporting-document list projection from copied case-folder attachments. (AC: 3, 4, 5, 8, 12)
  - [x] Include readable supported extensions: `.pdf`, `.txt`, `.doc`, `.docx`, and `.dwg`.
  - [x] Exclude archive extensions: `.zip`, `.rar`.
  - [x] Use copied case-folder paths from the manifest/source files/attachment provenance.
  - [x] Prefer display names that combine document role/type and file name when available.

- [x] Implement viewer modes. (AC: 6, 7, 8, 11)
  - [x] Reuse existing PDF viewer behavior where possible.
  - [x] Add read-only text rendering for `.txt`.
  - [x] Add preview-unavailable fallback for `.doc`, `.docx`, and `.dwg`.
  - [x] Remove Open source and Open in folder actions from the window.
  - [x] Surface missing-file and render-failure states without clearing the whole list.

- [x] Add full-panel layout and bindings. (AC: 2, 3, 6, 7, 8, 9)
  - [x] Add top document combo box.
  - [x] Add refresh-only control next to the combo box.
  - [x] Make the viewer consume the remaining dockpane area.
  - [x] Avoid nested cards inside the document workspace.

- [x] Add automated tests. (AC: 1-12)
  - [x] ViewModel/projection test: no transaction disables or closes workspace and clears stale documents.
  - [x] ViewModel/projection test: tab title includes the active transaction number.
  - [x] ViewModel/projection test: `.zip` and `.rar` attachments are excluded.
  - [x] ViewModel/projection test: supported copied files appear in the selector.
  - [x] ViewModel/projection test: selected PDF projects to PDF viewer state.
  - [x] ViewModel/projection test: selected TXT projects to text viewer state.
  - [x] ViewModel/projection test: DOC/DOCX/DWG use fallback state unless embedded support exists.
  - [x] ViewModel/projection test: transaction change clears prior selected document.
  - [x] XAML/binding smoke test: selector, refresh-only action, empty state, and viewer host are present.

### Review Findings

- [x] [Review][Patch] Supporting Documents open/reveal bypasses copied-path validation [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentsDockpaneViewModel.cs:234]
- [x] [Review][Patch] Supporting document option projection can throw on malformed copied paths [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/SupportingDocumentWorkspaceProjection.cs:18]
- [x] [Review][Patch] PDF WebView2 failures do not fall back to a document-specific error state [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentsDockpane.xaml.cs:100]
- [x] [Review][Patch] TXT preview wraps lines despite the horizontal-scroll UX requirement [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentsDockpane.xaml:105]
- [x] [Review][Patch] Supporting Documents pane does not refresh after documents are added to the already-loaded transaction [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs:1003]
- [x] [Review][Patch] Missing copied files make Open folder a no-op instead of helping inspect the case/source folder [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentsDockpaneViewModel.cs:239]
- [x] [Review][Patch] DOCX/DWG fallback test uses missing files and can pass for the wrong reason [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/SupportingDocumentsWorkspaceTests.cs:49]
- [x] [Review][Patch] Supporting Documents dockpane can open with a blank content surface because the view code-behind manually loads a second XAML control instead of using the dockpane's own `x:Class`/`InitializeComponent` binding path [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentsDockpane.xaml.cs:13]
- [x] [Review][Patch] Supporting Documents pane can still render blank when WebView2 is constructed eagerly in XAML; create the PDF viewer lazily and show restored/readable document counts so data-load issues are visible [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentsDockpane.xaml:63]
- [x] [Review][Patch] Supporting Documents WPF fallback can crash ArcGIS Pro because it manually constructs a class derived from ArcGIS `DockPane` after the DAML dockpane registration was disabled; use a plain `INotifyPropertyChanged` view-model and lazy PDF host instead [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentsDockpaneViewModel.cs:14]

## Developer Notes

- Existing source-viewer behavior can likely be reused from `Workflow/Review/ReviewSourceViewerStateProjector.cs`.
- Compare workspace already has PDF document selector behavior in `Compare/CompareWorkspaceViewModel.cs` and `CompareWorkspaceWindow.xaml`.
- Transaction attachment metadata and copied paths are available through `ManifestDocument.Payload.SourceFiles` and `ManifestDocument.Payload.AttachmentProvenance`.
- Existing attachment/source file handling lives near `CaseFolders/AttachmentSourceFileWriter.cs`, `Contracts/ManifestDocument.cs`, and `Innola/InnolaTransactionDetailService.cs`.
- Do not extract archives or render archive contents in this story.
- Treat embedded DWG viewing as a future enhancement unless ArcGIS Pro exposes a reliable viewer component already present in the add-in.

## Testing Notes

- Test with a loaded transaction that has mixed attachments: PDF, TXT, DOCX, DWG, ZIP, RAR, and an unsupported extension.
- Test that a transaction switch cannot leave the previous transaction's selected document visible.
- Test the no-transaction state from a fresh ArcGIS Pro launch and after Cancel/Suspend/Finalize cleanup.
- Test missing-file behavior by deleting one copied case-folder file while keeping other files present.

## Dev Agent Record

### Debug Log

- Implemented story 9-2 and validated with `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --configuration Release`.
- Patched all review findings and revalidated with `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -c Release` plus the full test harness.

### Completion Notes

- Added a dockpane-level `Supporting Documents [TR-...]` tab that is disabled when no active case is loaded.
- Moved Supporting Documents out of the Parcel Workflow pane and into a separate WPF window after the ArcGIS dockpane host rendered blank in field testing.
- Added copied-attachment projection for `.pdf`, `.txt`, `.doc`, `.docx`, and `.dwg`; archives and unsupported files are hidden.
- Added supporting-document viewer states: embedded PDF browser, read-only TXT surface, and preview-unavailable fallback with open/reveal actions.
- Added tests for workspace bindings, supported-file filtering, transaction label formatting, PDF viewer projection, and TXT/DOCX/DWG mode routing.
- Patched review findings: safe open/reveal actions, malformed copied-path handling, WebView2 render fallback, no-wrap TXT preview, add-documents refresh, missing copied-file reveal fallback, and stronger DOCX/DWG tests.
- Patched the blank-pane risk again by lazy-creating WebView2 only when a PDF is selected and by showing restored/readable document counts in the pane header.
- Removed the remaining ArcGIS `DockPane` inheritance and dockpane-manager lookup from the Supporting Documents WPF fallback so the separate window no longer manually constructs an ArcGIS-managed pane type.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentsDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentsDockpane.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentsDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ShowParcelWorkflowDockpaneButton.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/SupportingDocumentWorkspaceProjection.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/JamaicaReviewWorkspaceXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/SupportingDocumentsWorkspaceTests.cs`

### Change Log

- 2026-07-26: Implemented Supporting Documents full-panel viewer and tests.
- 2026-07-26: Corrected Supporting Documents placement to a separate WPF window and updated launch/cleanup wiring.
- 2026-07-26: Patched transaction launch so Supporting Documents activation is best-effort and cannot block Parcel Workflow from opening or continuing.
- 2026-07-26: Patched the Supporting Documents reload icon to re-read the active case folder so documents copied after pane activation appear in the picker/viewer.
- 2026-07-26: Patched code-review findings and marked story complete after Release build and 509-test validation.
- 2026-07-26: Hardened Supporting Documents against blank dockpane content by replacing eager WebView2 XAML construction with a lazy PDF host and visible document-count diagnostics.
- 2026-07-26: Pivoted Supporting Documents rendering back to a WPF `ProWindow` because the ArcGIS dockpane host still rendered blank despite successful data restoration; disabled dockpane DAML registration and removed Open/Reveal actions from the window.
- 2026-07-26: Patched crash risk in the WPF fallback by converting the Supporting Documents view-model to a plain `INotifyPropertyChanged` model and removing all supporting-documents `DockPaneManager` calls.
