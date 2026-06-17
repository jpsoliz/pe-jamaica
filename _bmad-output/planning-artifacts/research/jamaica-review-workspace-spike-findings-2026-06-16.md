# Jamaica Review Workspace Spike Findings

Date: 2026-06-16
Story: `5-13-build-dev-spike-for-jamaica-cogo-style-review-workspace-shell`

## Summary

The spike successfully proves a dedicated floating ArcGIS Pro review window can host the Jamaica review-workspace shell without replacing the existing dock-pane workflow.

## What Worked

- A large floating `ProWindow` is practical for the Jamaica review workspace shell.
- The spike can bind to live case artifacts by reusing the existing `ParcelWorkflowDockpaneViewModel` review state:
  - copied source files
  - `extraction_review_data.json`
  - current selected review row
  - review save / approve actions
- Existing embedded source-viewer behavior can be reused in the spike window:
  - PDF through the browser host
  - TIFF / PNG / JPG through image rendering
  - unsupported types through explicit fallback messaging
- Parcel grouping can be projected directly from current review rows using `ParcelGroupId`.
- A lightweight parcel preview can be generated from current review rows without requiring final spatial outputs.

## What Remains Provisional

- Parcel interpretation logic is still a spike projection, not a production cadastral interpretation engine.
- Parcel preview is a lightweight generated sketch. It is not authoritative geometry and does not replace Map Review.
- Unsupported-source fallback is policy-correct, but real-world usability for TXT/CSV-heavy cases still needs live examiner testing.
- The current spike reuses the existing review row model. A future production build may still want a dedicated workspace projection contract.

## ArcGIS Pro Host Recommendation

- Keep the existing dock pane as the transaction workflow shell.
- Use a large floating review window as the preferred host for the Jamaica COGO-style review experience.
- Treat narrow docked layouts as summary/launch surfaces, not the primary active review environment.

## Promotion Recommendation

Proceed to production implementation, with these guardrails:

1. Keep the floating workspace pattern.
2. Preserve live artifact reuse where possible.
3. Replace provisional parcel interpretation logic with a durable production service.
4. Validate the window live inside ArcGIS Pro with real examiner cases before locking the final layout and controls.

## Manual Validation Still Needed

- Open the spike window from the Extraction Review stage in ArcGIS Pro.
- Confirm PDF hosting behavior and scroll usability.
- Confirm parcel switching and row selection remain readable at real ArcGIS Pro window sizes.
- Confirm save / approve actions behave correctly when invoked from the spike window.
