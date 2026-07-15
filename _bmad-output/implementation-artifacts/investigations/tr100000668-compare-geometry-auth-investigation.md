# Investigation: TR100000668 Compare Geometry Authentication Failure

## Hand-off Brief

1. **What happened.** Compare geometry for TR100000668 does not load because `compare_geometry_load` cannot authenticate to the configured ArcGIS portal.
2. **Where the case stands.** Source trace confirms the active ArcGIS Pro portal is rejected because ArcGIS Pro reports `https://jm-gis.innola-solutions.com` while settings configure `https://jm-gis.innola-solutions.com/portal`.
3. **What's needed next.** Patch portal URL matching to treat a same-host active root portal as compatible with a configured `/portal` path, while still rejecting sibling paths like `/portal2`.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | TR100000668 |
| Date opened | 2026-07-15 |
| Status | Active |
| System | ArcGIS Pro Compare workspace, Enterprise working review geometry |
| Evidence sources | User screenshot, `WorkflowSettings.json`, Compare map integration code, portal auth provider code, portal auth tests |

## Problem Statement

User reports TR100000668 Compare geometry is not loading. Screenshot shows:

`compare_geometry_load could not authenticate to https://jm-gis.innola-solutions.com/server/rest. Attempted sources: arcgis_pro_session, ARCGIS_PORTAL_TOKEN. arcgis_pro_session: Active ArcGIS Pro portal 'https://jm-gis.innola-solutions.com' does not match configured portal 'https://jm-gis.innola-solutions.com/server/rest'. environment: No portal token was found in ARCGIS_PORTAL_TOKEN.`

The screenshot text appears to reference the service root in the first sentence, but the current repo configuration has a portal URL at `/portal`.

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| User screenshot | Available | Shows failed Compare geometry authentication and active/configured portal mismatch. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json` | Available | Configures working review service root and portal URL. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/ArcGisCompareMapIntegrationService.cs` | Available | Calls portal auth before adding working layers. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Enterprise/PortalAuth/ArcGisProPortalAuthProvider.cs` | Available | Contains active/configured portal matching logic. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Enterprise/PortalAuthProviderTests.cs` | Available | Covers normal portal match and sibling portal rejection. |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --- | --- | --- | --- |
| 1 | Confirm configured portal URL used by Compare | High | Done | Current settings use `/portal`; screenshot may show older/alternate message text using service root. |
| 2 | Trace active/configured portal comparison | High | Done | Matcher rejects active root `/` when configured path is `/portal`. |
| 3 | Validate sibling portal safety after fix | High | Open | Existing `/portal2` test should keep passing; add root-vs-portal coverage. |
| 4 | Confirm token fallback | Medium | Done | Environment fallback is attempted after ArcGIS Pro session rejection. |

## Timeline of Events

| Time | Event | Source | Confidence |
| --- | --- | --- | --- |
| 2026-07-15 | User reports TR100000668 Compare geometry not loading | User screenshot | Confirmed |
| 2026-07-15 | Source trace identifies active/configured portal path mismatch | Repo inspection | Confirmed |

## Confirmed Findings

### Finding 1: Compare requires portal auth before loading working geometry

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/ArcGisCompareMapIntegrationService.cs:31`

**Detail:** `AddTransactionGeometryToActiveMapAsync` calls `TryAuthenticateAsync(plan, cancellationToken)` and returns a failed `CompareMapIntegrationResult` if authentication fails.

### Finding 2: Compare auth uses the plan portal URL and operation name `compare_geometry_load`

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/ArcGisCompareMapIntegrationService.cs:124`

**Detail:** `TryAuthenticateAsync` calls `portalAuthProvider.GetTokenAsync(new PortalAuthRequest(plan.PortalUrl, primaryLayer, "compare_geometry_load"), ...)`.

### Finding 3: The current repo config separates server root and portal URL

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json:14` and `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json:32`

**Detail:** `service_root` is `https://jm-gis.innola-solutions.com/server/rest`; `portal_url` is `https://jm-gis.innola-solutions.com/portal`.

### Finding 4: The portal auth provider rejects same-host active root when configured path is `/portal`

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Enterprise/PortalAuth/ArcGisProPortalAuthProvider.cs:336`

**Detail:** `PortalMatchesRequest` normalizes paths and only returns true when the configured path is `/`, paths are equal, or the active path starts with the configured path plus `/`. If ArcGIS Pro reports active path `/` and settings configure `/portal`, the method returns false.

### Finding 5: Environment token fallback was attempted and unavailable

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Enterprise/PortalAuth/CompositePortalAuthProvider.cs:21`; user screenshot.

**Detail:** The default composite provider tries ArcGIS Pro session first and environment token second. The screenshot says `ARCGIS_PORTAL_TOKEN` had no token, so the second source could not recover from the ArcGIS Pro portal mismatch.

## Deduced Conclusions

### Deduction 1: TR100000668 geometry is not loading because auth stops before layers are added

**Based on:** Findings 1, 2, and 5.

**Reasoning:** Compare map integration returns failure immediately when auth fails; no layer add or zoom code runs after that.

**Conclusion:** The issue is authentication/portal matching, not transaction filter, geometry availability, or map rendering.

### Deduction 2: The active portal is likely correct but represented differently by ArcGIS Pro

**Based on:** Findings 3 and 4 plus user screenshot.

**Reasoning:** Both URLs share host `jm-gis.innola-solutions.com`. The only mismatch is path root `/` versus `/portal`. ArcGIS Pro often reports the portal base differently than application configuration, while service/layer URLs sit under `/server/rest`.

**Conclusion:** The code should normalize this deployment shape instead of requiring exact `/portal` path equality.

## Hypothesized Paths

### Hypothesis 1: Portal URL matching needs root-vs-portal normalization

**Status:** Confirmed by source trace; runtime confirmation after patch still needed.

**Theory:** `PortalMatchesRequest` should consider active root `/` compatible with configured `/portal` when the host matches.

**Supporting indicators:** The user screenshot shows active root and configured same host; existing code rejects that combination.

**Would confirm:** Add a unit test where fake active portal is `https://portal.example` and requested portal is `https://portal.example/portal`; test currently fails, then passes after matcher patch.

**Would refute:** If runtime still fails after root-vs-portal normalization and ArcGIS Pro session does not return a token.

**Resolution:** Source trace confirms the mismatch mechanism.

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | --- | --- |
| Exact runtime `plan.PortalUrl` string in this build | Resolves screenshot inconsistency between `/server/rest` and current `/portal` config | Add temporary diagnostic or inspect deployed settings DLL/content. |
| Whether ArcGIS Pro session returns a token after path normalization | Confirms end-to-end fix | Patch matcher and retry TR100000668. |

## Source Code Trace

| Element | Detail |
| --- | --- |
| Error origin | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Enterprise/PortalAuth/ArcGisProPortalAuthProvider.cs:52` |
| Trigger | Compare workspace calls `ArcGisCompareMapIntegrationService.AddTransactionGeometryToActiveMapAsync` |
| Condition | Active ArcGIS Pro portal URI path `/` does not match configured portal path `/portal`; no `ARCGIS_PORTAL_TOKEN` fallback exists |
| Related files | `WorkflowSettings.json`, `ArcGisCompareMapIntegrationService.cs`, `CompositePortalAuthProvider.cs`, `PortalAuthProviderTests.cs` |

## Conclusion

**Confidence:** High

The failure is caused by portal authentication matching, not by missing working geometry. Compare stops before adding working layers because the ArcGIS Pro active portal URL is reported as the host root while the settings use the same host with `/portal`. The matcher currently rejects that safe same-host root-vs-portal combination.

## Recommended Next Steps

### Fix direction

Patch `ArcGisProPortalAuthProvider.PortalMatchesRequest` to treat active root `/` as compatible with configured `/portal` on the same host. Add a unit test for this case and keep the existing sibling `/portal2` rejection test.

### Diagnostic

After patching, retry TR100000668. If the message changes from portal mismatch to token unavailable, the path normalization is fixed and the next issue is ArcGIS Pro token retrieval/sign-in.

## Reproduction Plan

1. Configure portal URL as `https://jm-gis.innola-solutions.com/portal`.
2. Simulate ArcGIS Pro active portal URI as `https://jm-gis.innola-solutions.com`.
3. Request a token through `ArcGisProPortalAuthProvider`.
4. Expected after fix: provider accepts the active portal and attempts/returns token.
5. Expected before fix: provider returns `does not match configured portal`.

## Side Findings

- The screenshot first line says auth failed to `https://jm-gis.innola-solutions.com/server/rest`, while current repo settings configure `portal_url` as `https://jm-gis.innola-solutions.com/portal`. This may mean the deployed build or active settings differ from the checked-in file, or the UI message is surfacing service-root context around a portal auth failure.

## Patch Note: 2026-07-15

Patched `ArcGisProPortalAuthProvider.PortalMatchesRequest` so an active same-host root portal, such as `https://jm-gis.innola-solutions.com`, is accepted when the configured portal URL is the same host with `/portal`, such as `https://jm-gis.innola-solutions.com/portal`.

Added regression coverage in `PortalAuthProviderTests.ArcGisProProviderAcceptsActiveRootForConfiguredPortalPath`. Existing sibling-path protection remains covered by `ArcGisProProviderDoesNotMatchSiblingPortalPath`.

Verification:

- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln --no-restore` passed with the pre-existing nullable warning in `SurveyPlanBoundarySolverTests.cs:82`.
- `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj --no-build` passed: 396 tests.
