# TR100000872 Spatial Unit / Plan Check Investigation

Date: 2026-07-24

## Finding

TR100000872 completed local compute review output generation, but enterprise closeout failed while creating the Innola Spatial Unit default record.

Evidence:

- `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000872\working\spatial_unit_api_failure.json`
- Error category: `HttpRequestException`
- Error message: `Spatial Unit default creation failed: Unauthorized`
- Requested Spatial Unit count: `1`
- Output polygon count: `1`

The Plan Check detail was not written because `InnolaTransactionLifecycleCoordinator` returns immediately when Spatial Unit creation fails. Plan Check writeback is executed only after Spatial Unit creation and save succeed.

## Fix Applied

The Spatial Unit and Plan Check Innola services now retry with cookie-only authentication when the first request is rejected with HTTP 401 or 403 and an `INNOLAID` session cookie is available. This follows the existing Compare query behavior.

Changed files:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaSpatialUnitServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaPlanCheckServiceTests.cs`

## Validation

Full add-in test suite passed after the change:

- `PASS 503 tests`

Fresh package generated:

- Version: `0.1.16`
- Local add-in package: `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Release\net8.0-windows\ParcelWorkflowAddIn.esriAddInX`
- Deployment package: `deployment\target-computer-tools\package\ParcelWorkflowAddIn.esriAddInX`

