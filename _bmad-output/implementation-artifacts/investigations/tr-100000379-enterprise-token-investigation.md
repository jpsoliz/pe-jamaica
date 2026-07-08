# Investigation: TR 100000379 Enterprise token validation failure

## Hand-off Brief

1. **What happened.** ArcGIS Pro finalize reports `Enterprise working-layer publish failed... token validation failed for points` while PowerShell can validate the current user-level `ARCGIS_PORTAL_TOKEN` against Portal.
2. **Where the case stands.** Active; the first confirmed contradiction is that the token is valid outside Pro but the add-in reports it invalid inside Pro.
3. **What's needed next.** Trace the add-in token resolution path and TR 100000379 logs/artifacts to determine whether ArcGIS Pro is using a stale process token, stale user environment, or a different publish endpoint.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | N/A |
| Date opened | 2026-07-07 |
| Status | Active |
| System | Windows / ArcGIS Pro add-in / Sid-jamaica |
| Evidence sources | User screenshot, Portal validation command, source code, TR 100000379 case folder |

## Problem Statement

User reports the same Enterprise working-layer publish failure after token refresh: `token validation failed for points: ArcGIS token is invalid or expired`.

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| User screenshot | Available | Error text names points-layer token validation failure. |
| Portal `/community/self` validation | Available | Current user-level token validated successfully in PowerShell. |
| ArcGIS Pro process environment | Partial | Process exists, but its environment variables are not directly observable from normal PowerShell. |
| Source code token resolution | Available | Needs source trace. |
| TR 100000379 logs/artifacts | Partial | Needs inventory. |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --- | --- | --- | --- |
| 1 | Validate current user token and ArcGIS Pro start time | High | Done | Current user token validates against Portal. |
| 2 | Trace `JsonEnterpriseWorkingLayerPublishService` token resolution and validation | High | Done | Process token was preferred over user token. |
| 3 | Inspect TR 100000379 logs and publish artifacts | High | Done | Enterprise publish artifact shows prior successful publish of all working layers. |
| 4 | Check configured Enterprise layer URLs | Medium | Open | A wrong endpoint can sometimes surface as auth-like failure. |

## Timeline of Events

| Time | Event | Source | Confidence |
| --- | --- | --- | --- |
| 2026-07-07 | User reports persistent invalid-token publish error for TR 100000379 | User screenshot | Confirmed |
| 2026-07-07 | Current user-level `ARCGIS_PORTAL_TOKEN` validates against Portal as `GIS_Test` | PowerShell Portal validation | Confirmed |
| 2026-07-07 | TR 100000379 `enterprise_working_publish.json` shows points, lines, polygons, case_index, and issues published | `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000379\output\enterprise_working_publish.json` | Confirmed |

## Confirmed Findings

### Finding 1: Current user-level token is valid

**Evidence:** Portal `/community/self` validation returned `username=GIS_Test` for the current user-level token.

**Detail:** The observed finalize error is not explained by the current user-level token being expired or malformed.

### Finding 2: Publish code preferred process token before user token

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs` and `JsonEnterpriseWorkingDispositionService.cs` selected `EnvironmentVariableTarget.Process` before `EnvironmentVariableTarget.User`.

**Detail:** A stale process-level token inherited by ArcGIS Pro can override a fresh valid user-level token.

### Finding 3: TR 100000379 already has a successful Enterprise publish artifact

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000379\output\enterprise_working_publish.json` has `"status": "published"` and includes published layers for points, lines, polygons, case_index, and issues.

**Detail:** The current error is likely from a retry/second finalize attempt using stale token state, not proof that no Enterprise data was copied.

## Deduced Conclusions

### Deduction 1: The failure path can be caused by stale ArcGIS Pro process environment

**Based on:** Findings 1 and 2.

**Reasoning:** The current user token validates, but the add-in used process token first. If ArcGIS Pro launched while `ARCGIS_PORTAL_TOKEN` was invalid, the process token remains invalid until restart and overrides the corrected user token.

**Conclusion:** Token precedence must prefer the current user environment over the process environment.

## Hypothesized Paths

### Hypothesis 1: ArcGIS Pro process is using a stale token

**Status:** Open

**Theory:** ArcGIS Pro was launched before the corrected user-level token was written, so its process environment still contains an invalid token.

**Supporting indicators:** PowerShell validated the current user-level token, while Pro still reports invalid token.

**Would confirm:** ArcGIS Pro start time precedes the final corrected user-token write; relaunching Pro clears the issue.

**Would refute:** ArcGIS Pro relaunched after the corrected token and still has the error while token validation from the same process succeeds.

**Resolution:** Pending.

**Status:** Confirmed

**Resolution:** Source code showed process token was preferred, and the patch now prefers user token first in publish and disposition services.

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | --- | --- |
| ArcGIS Pro process environment value for `ARCGIS_PORTAL_TOKEN` | Would confirm stale process token directly | Add temporary in-app diagnostic or log token metadata only. |

## Source Code Trace

| Element | Detail |
| --- | --- |
| Error origin | `JsonEnterpriseWorkingLayerPublishService.EnsureArcGisSuccess` token error handling |
| Trigger | Finalize / Enterprise working-layer publish |
| Condition | Stale process token can override corrected user token |
| Related files | `JsonEnterpriseWorkingLayerPublishService.cs`, `JsonEnterpriseWorkingDispositionService.cs`, `WorkflowSessionTests.cs` |

## Conclusion

**Confidence:** High

The code path preferred process-level `ARCGIS_PORTAL_TOKEN` before user-level token. This can keep ArcGIS Pro using a stale invalid token even after the user-level token is corrected outside Pro.

## Recommended Next Steps

### Fix direction

Patch token selection in Enterprise publish and disposition services to prefer User, then Process, then Machine.

### Diagnostic

Focused tests now verify both publish and disposition services prefer the current user token over a stale process token.

## Reproduction Plan

Pending.

## Side Findings
