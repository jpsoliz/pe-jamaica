# CURRENT_SPRINT

**Last Updated:** 2026-06-18  
**Sprint Owner:** Sidwell / ArcGIS Pro Parcel Workflow Add-in Team  
**Current Sprint Theme:** Epic 5 workflow alignment and Points Validation Tool handoff stabilization

## 1) Objective

Stabilize the examiner flow from transaction attachments through data extraction and Points Validation Tool, then return cleanly into `Create Spatial Units` for parcel-fabric/local spatial generation.

## 2) Done / Backlog

- **Done**
  - Compute workflow stage vocabulary was realigned around `Attachments -> Data Extraction -> Validate Points -> Create Spatial Units -> Final Review -> Finalize`.
  - `Points Validation Tool` is live, supports parcel-scoped point edits, save/discard behavior, embedded PDF viewing, and close-state messaging.
  - Story 5.16B save/return flow is implemented: dirty-only save, save/discard prompts, and continue handoff into `Create Spatial Units`.
- **Backlog**
  - Finish live UX verification of the new `Validation Complete` flow in ArcGIS Pro.
  - Keep the Parcel Workflow shell aligned so post-validation content does not duplicate the dedicated tool surface.
  - Continue downstream implementation for `Create Spatial Units`, Final Review, and final Innola closeout behavior.

## 3) Next Actions

1. Live-test `Validation Complete` in ArcGIS Pro: no-edit continue, edited-save-then-continue, and close-without-save paths.
2. Patch the Parcel Workflow Point Review shell so it no longer shows duplicated Jamaica/Points Validation embedded review content after the separate tool is used.
3. Continue the next story on downstream flow: `Create Spatial Units` entry, Final Review, and shell cleanup around the post-validation handoff.

## 4) Active Risk

- There is still a pre-existing direct add-in WPF/XAML build issue around `TransactionPanelDockpane.xaml.cs` / `InitializeComponent`, so packaging/live deployment can be noisier than the test harness.
- The workflow is now split between shell state and the separate validation window, so any mismatch in close/save/continue behavior can confuse examiners if not verified live in ArcGIS Pro.
