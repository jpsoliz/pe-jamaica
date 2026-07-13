---
baseline_commit: handoff-2026-07-07
---

# Story 7.13: Add Internal ArcGIS Portal Auth Provider For Enterprise Operations

Status: review

## Story

As a cadastral examiner or Enterprise administrator using the Parcel Workflow add-in,  
I want Enterprise publish, disposition, maintenance, and map provisioning to authenticate through an internal ArcGIS Portal authentication provider,  
so that users no longer need to manually generate and copy short-lived `ARCGIS_PORTAL_TOKEN` values for normal Enterprise operations.

## Business Context

Enterprise working-layer publishing, disposition writeback, Spatial Unit SUID writeback, admin provisioning, cleanup, and Innola map/view provisioning currently depend on a manually supplied `ARCGIS_PORTAL_TOKEN`. This has been useful for testing, but it creates a fragile production workflow:

- Tokens expire after one day or another administrator-selected duration.
- ArcGIS Pro must often be restarted so the process sees the new environment variable.
- Users can paste truncated tokens ending in `..`.
- Token source precedence has already required patches to prefer current user tokens over stale process tokens.
- Finalize can fail late even when the user is already signed into ArcGIS Pro.

The add-in should centralize ArcGIS Enterprise authentication behind one provider layer. Enterprise operation services should ask for a valid token and should not know whether the token came from the current ArcGIS Pro portal session, secure credentials, an ArcGIS Python profile, or the legacy environment variable fallback.

This story complements Stories 7.8, 7.9, 7.10, 7.11, and 7.12. It must preserve the existing `ARCGIS_PORTAL_TOKEN` path as a development fallback while making the ArcGIS Pro signed-in portal session the preferred add-in authentication source.

## Acceptance Criteria

1. Given the add-in is running inside ArcGIS Pro and the user is signed into the configured Enterprise portal, when an Enterprise operation needs a token, then the add-in obtains a usable token from the active ArcGIS Pro portal session without requiring `ARCGIS_PORTAL_TOKEN`.
2. Given ArcGIS Pro session auth is unavailable, when `ARCGIS_PORTAL_TOKEN` is set for the current user/process/machine, then Enterprise operations continue to use the environment token as a fallback.
3. Given multiple token sources are available, when a token is requested, then provider precedence is deterministic and documented: ArcGIS Pro session first, secure credential/profile source second if implemented, user/process environment fallback next, machine environment fallback last.
4. Given no valid token can be resolved, when Enterprise publish/disposition/provisioning runs, then the failure message identifies the attempted auth sources and the configured portal URL without exposing secrets.
5. Given a token is resolved, when the operation validates it against an ArcGIS FeatureServer or Portal endpoint, then invalid/expired token responses are translated into a clear retryable authentication diagnostic.
6. Given `JsonEnterpriseWorkingLayerPublishService` publishes working points, lines, polygons, issues, or case index, when it needs ArcGIS authentication, then it uses `IPortalAuthProvider` instead of directly reading environment variables.
7. Given `JsonEnterpriseWorkingDispositionService` records disposition, case index Spatial Unit references, or polygon SUID references, when it needs ArcGIS authentication, then it uses `IPortalAuthProvider` instead of directly reading environment variables.
8. Given Enterprise Parcel Fabric publish service uses ArcGIS REST operations that require auth, when it needs a token, then it also uses the shared provider or a compatible provider seam.
9. Given Enterprise Admin Settings validation/provision/cleanup runs from the add-in, when live provisioning is requested, then the add-in can pass a provider-resolved token into the admin script without exposing the token in UI text, settings, diagnostics, or logs.
10. Given Story 7.12 web map/view provisioning runs from PowerShell or terminal, when live mode is requested, then the existing `ARCGIS_PORTAL_TOKEN` path remains supported, and the story documents that terminal scripts cannot automatically use the in-process ArcGIS Pro session unless a supported ArcGIS Python profile/auth mode is configured.
11. Given the provider returns metadata about the token source, when operation diagnostics are written, then diagnostics include non-secret fields such as `auth_source`, `portal_url`, `operation`, and `validated_at_utc`, but never include the token.
12. Given a token is expired, malformed, truncated, or rejected by Enterprise, when the operation fails, then the error message does not ask users to restart ArcGIS Pro as the primary fix if ArcGIS Pro session auth is available; it should suggest signing into/refreshing the Portal session first.
13. Given environment-token fallback is used, when the operation succeeds, then existing behavior remains backward compatible for test environments and admin-only scripts.
14. Given automated tests run, then token provider precedence, missing token behavior, invalid token diagnostics, secret redaction, publish service integration, and disposition service integration are covered without requiring live Enterprise access.
15. Given the code review inspects Enterprise services, then no new direct reads of `ARCGIS_PORTAL_TOKEN` exist outside the provider layer and admin script boundaries.

## Tasks / Subtasks

- [x] Add Portal Auth provider contracts. (AC: 1-5, 11-15)
  - [x] Add `IPortalAuthProvider`, `PortalAuthRequest`, and `PortalAuthResult` under a focused namespace such as `Enterprise/PortalAuth` or `Workflow/Enterprise/Auth`.
  - [x] Include request fields for `PortalUrl`, `ServiceUrl`, `Operation`, and optional `LayerRole`.
  - [x] Include result fields for `Success`, `Token`, `Source`, `ExpiresAtUtc`, `ValidatedAtUtc`, `ErrorMessage`, and non-secret diagnostics.
  - [x] Ensure `PortalAuthResult.ToString()` or diagnostics helpers never expose tokens.

- [x] Implement ArcGIS Pro session provider. (AC: 1, 3-5, 12)
  - [x] Add `ArcGisProPortalAuthProvider`.
  - [x] Resolve the active/signed-in Portal from ArcGIS Pro APIs.
  - [x] Verify the active portal matches or is compatible with the configured `portal_url`/service root.
  - [x] Request a token from the active Portal session using the supported ArcGIS Pro SDK API for the project’s Pro version.
  - [x] Return a clear unavailable result when ArcGIS Pro is not signed in, the portal does not match, or the SDK cannot supply a token.

- [x] Implement environment fallback provider. (AC: 2-5, 11-15)
  - [x] Move current `ARCGIS_PORTAL_TOKEN` lookup into `EnvironmentPortalAuthProvider`.
  - [x] Preserve current precedence where user token is preferred over stale process token, then machine token, unless the provider chain supersedes it with ArcGIS Pro session auth.
  - [x] Detect obviously truncated tokens ending with `..` and return a specific diagnostic before making an Enterprise request.
  - [x] Keep environment-token fallback available for tests and scripts.

- [x] Add provider chain/composite. (AC: 1-5, 11-14)
  - [x] Add `CompositePortalAuthProvider` or equivalent.
  - [x] Try ArcGIS Pro session auth first.
  - [x] Reserve a seam for future secure credential/profile auth.
  - [x] Try environment fallback last.
  - [x] Aggregate attempted source diagnostics without secrets.

- [x] Refactor Enterprise working layer publish. (AC: 5-7, 11-15)
  - [x] Update `JsonEnterpriseWorkingLayerPublishService` to receive/use `IPortalAuthProvider`.
  - [x] Remove private duplicated `GetPortalToken`, `SelectPortalToken`, and `FirstNonBlank` methods from this service unless retained only as adapter calls inside the provider.
  - [x] Keep existing ArcGIS REST validation against the target FeatureServer layer.
  - [x] Preserve row-level ArcGIS add/delete error diagnostics.

- [x] Refactor Enterprise disposition writeback. (AC: 5, 7, 11-15)
  - [x] Update `JsonEnterpriseWorkingDispositionService` to receive/use `IPortalAuthProvider`.
  - [x] Apply the provider to disposition row updates, case index Spatial Unit reference writeback, and polygon SUID writeback.
  - [x] Remove direct environment variable reads from this service.
  - [x] Preserve required schema field validation and row rejection diagnostics.

- [x] Review Enterprise Parcel Fabric and admin pathways. (AC: 8-10, 15)
  - [x] Inspect `JsonEnterpriseParcelFabricPublishService` and any other Enterprise REST service for direct token handling.
  - [x] Route add-in initiated Enterprise REST operations through the provider where applicable.
  - [x] Keep Python/PowerShell admin scripts compatible with `ARCGIS_PORTAL_TOKEN`.
  - [x] Optionally add `--auth-mode token-env|profile` to admin scripts if feasible without broadening scope.

- [x] Update user-facing diagnostics. (AC: 4-5, 11-12)
  - [x] Replace messages that only say “set `ARCGIS_PORTAL_TOKEN` and restart ArcGIS Pro” with provider-aware guidance.
  - [x] Include attempted auth source names.
  - [x] Suggest signing into or refreshing ArcGIS Pro Portal session when ArcGIS Pro auth is configured/available.
  - [x] Keep secrets redacted from UI, logs, JSON artifacts, and test output.

- [x] Add tests. (AC: 1-15)
  - [x] Unit-test provider precedence.
  - [x] Unit-test missing, malformed, truncated, and expired/rejected token diagnostics.
  - [x] Unit-test publish service obtains token from provider and no longer reads environment variables directly.
  - [x] Unit-test disposition service obtains token from provider for all writeback methods.
  - [x] Unit-test diagnostics redaction.
  - [x] Update existing tests currently reflecting `SelectPortalToken` private methods.

## Developer Notes

### Current Token Handling Hotspots

Current hard-coded environment token reads were found in:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs`
  - `PublishFeatureServiceLayerAsync(...)`
  - private `GetPortalToken()`
  - private `SelectPortalToken(...)`
  - private `FirstNonBlank(...)`

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingDispositionService.cs`
  - `UpdateFeatureServiceLayerAsync(...)`
  - `UpdateFeatureServiceSpatialUnitReferenceAsync(...)`
  - `UpdateFeatureServicePolygonSpatialUnitReferencesAsync(...)`
  - private `GetPortalToken()`
  - private `SelectPortalToken(...)`
  - private `FirstNonBlank(...)`

Current Python/admin script token paths:

- `src/ProcessingTools/admin/provision_enterprise_working_layers.py`
- `src/ProcessingTools/admin/provision_innola_compute_review_web_map.py`
- `tools/provision_innola_compute_review_web_map.ps1`

These terminal scripts can keep `ARCGIS_PORTAL_TOKEN` initially because they run outside ArcGIS Pro. Do not pretend terminal scripts can automatically read the in-process ArcGIS Pro session unless a real ArcGIS Python profile or SDK-supported mechanism is implemented.

### Existing Tests To Update

Existing test names to revisit:

- `workflow session enterprise publish prefers user portal token over stale process token`
- `workflow session enterprise disposition prefers user portal token over stale process token`

These should move from private method reflection tests to provider tests.

Existing full harness currently passes 343 tests. Preserve this harness behavior.

### Recommended Provider Shape

Suggested contracts:

```csharp
public interface IPortalAuthProvider
{
    Task<PortalAuthResult> GetTokenAsync(
        PortalAuthRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PortalAuthRequest(
    string PortalUrl,
    string? ServiceUrl,
    string Operation,
    string? LayerRole = null);

public sealed record PortalAuthResult(
    bool Success,
    string? Token,
    string Source,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? ValidatedAtUtc,
    string? ErrorMessage,
    IReadOnlyList<string>? AttemptedSources = null);
```

Adjust shape to match repository conventions.

### ArcGIS Pro SDK Caution

Implementation must use the ArcGIS Pro SDK APIs available to this project’s ArcGIS Pro/SDK version. If direct active portal token retrieval is not available or is thread-constrained, implement the provider seam and environment fallback first, then return a clear `ArcGISProSessionUnavailable` result from the Pro provider until the correct SDK call is confirmed.

Do not block this story by hard-coding credentials or by storing Portal passwords in `WorkflowSettings.json`.

### Security Requirements

- Never write Portal token values to:
  - `WorkflowSettings.json`
  - case folder artifacts
  - diagnostics JSON
  - lifecycle audit
  - UI status text
  - test assertion failure messages
- Error text may include:
  - portal URL
  - target service URL path without token
  - auth source name
  - ArcGIS error code/message
- Error text must not include:
  - token
  - password
  - certificate material
  - full request form body if it contains token

### Backward Compatibility

The existing manual token flow remains useful for live admin testing. Keep it as a fallback, but it should no longer be the only way normal add-in Enterprise operations authenticate.

## Testing Notes

Run:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj
python -m unittest discover -s tests
```

For Python tests, run from:

```powershell
cd src\ProcessingTools
python -m unittest discover -s tests
```

## Dependencies

- Story 7.8 Enterprise Admin provisioning settings.
- Story 7.9 Enterprise working-layer Finalize closeout.
- Story 7.10 Enterprise visualization defaults.
- Story 7.12 Innola compute review web map provisioning.

## Out Of Scope

- Storing Portal usernames/passwords directly in `WorkflowSettings.json`.
- Replacing Innola API authentication.
- Changing Enterprise working layer schema.
- Rebuilding the 7.12 web map provisioning script beyond preserving its existing token-env terminal behavior.
- Implementing a full OAuth application registration flow unless the ArcGIS Pro SDK session token path is insufficient and product explicitly approves OAuth setup.

## Dev Agent Record

### Implementation Plan

- Add a central Portal auth provider contract and result model with non-secret diagnostics.
- Implement provider chain precedence: ArcGIS Pro session first, environment fallback last.
- Refactor Enterprise working-layer publish and disposition writeback services to request tokens from the provider seam.
- Replace old private environment-token reflection tests with provider and service-integration tests.

### Debug Log

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` - passed; existing nullable warning remains in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "portal auth"` - passed 8 tests.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` - passed 349 tests.
- `python -m unittest discover -s tests` from `src\ProcessingTools` - passed 80 tests.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "portal auth"` - passed 9 tests after review patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` - passed after review patch; existing nullable warning remains in `SurveyPlanBoundarySolverTests.cs`.

### Completion Notes

- Added `IPortalAuthProvider`, request/result contracts, ArcGIS Pro session provider, environment fallback provider, composite provider, and required-token diagnostic helper.
- ArcGIS Pro session provider uses guarded reflection so it can run inside ArcGIS Pro when the SDK session portal is present, while cleanly falling back outside Pro.
- Refactored Enterprise working-layer publish and disposition/SUID writeback paths to consume provider-resolved tokens instead of reading `ARCGIS_PORTAL_TOKEN` directly.
- Refactored Enterprise Admin Settings provisioning/cleanup launcher to resolve a token through the shared provider and pass it only to the child Python process environment.
- Tightened ArcGIS Pro active portal path matching so sibling portal paths do not match accidentally while host-root requests remain compatible.
- Preserved terminal/admin script behavior: PowerShell/Python provisioning still uses `ARCGIS_PORTAL_TOKEN` when running outside ArcGIS Pro.
- Inspected Enterprise Parcel Fabric publish path; it does not perform direct Portal-token FeatureServer REST calls in the current implementation.
- Updated tests to cover provider precedence, truncated token diagnostics, ArcGIS Pro session success/unavailable paths, secret redaction, and publish/disposition provider integration.

### File List

- `_bmad-output/implementation-artifacts/7-13-add-internal-arcgis-portal-auth-provider-for-enterprise-operations.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Enterprise/PortalAuth/IPortalAuthProvider.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Enterprise/PortalAuth/PortalAuthRequest.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Enterprise/PortalAuth/PortalAuthResult.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Enterprise/PortalAuth/ArcGisProPortalAuthProvider.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Enterprise/PortalAuth/EnvironmentPortalAuthProvider.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Enterprise/PortalAuth/CompositePortalAuthProvider.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Enterprise/PortalAuth/PortalAuthProviderExtensions.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingDispositionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Enterprise/PortalAuthProviderTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`

### Change Log

- 2026-07-12: Implemented internal Portal auth provider layer and refactored Enterprise publish/disposition token flows.
- 2026-07-12: Patched review findings for Enterprise Admin auth handoff and sibling portal path matching.

## Completion Notes

Story created from the July 2026 authentication discussion after repeated short-lived `ARCGIS_PORTAL_TOKEN` failures during Enterprise publish/finalize testing.

