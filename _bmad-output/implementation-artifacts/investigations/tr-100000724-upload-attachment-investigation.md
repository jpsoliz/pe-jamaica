# Investigation: TR 100000724 Crop Attachment Upload Failure

## Hand-off Brief

1. **What happened.** User reported the Supporting Documents crop attach flow showed `Could not upload attachment.` while working with Current TR `100000724`.
2. **Where the case stands.** Active; local case metadata confirms the crop was saved and upload failed with `HttpRequestException`. Chrome can reach Swagger only after prompting for a client certificate, while local certificate store scans did not find the configured certificate through the add-in's manual lookup path.
3. **What's needed next.** Install the cert-aware add-in patch, restart ArcGIS Pro, retry Attach, and inspect crop metadata for the actual route/auth/certificate-adjacent transport result.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | N/A |
| Date opened | 2026-08-27 |
| Status | Active |
| System | ArcGIS Pro add-in, local repo investigation |
| Evidence sources | User report, source code, story 2-23f, project context, local Case Folder metadata |

## Problem Statement

User reported: `i got the message: "Could not upload attachment. " please review the repo 100000724 please`

Initial scope: Current TR `100000724`, Supporting Documents crop/attach upload added in story 2-23f.

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| User-reported message | Available | Exact text: `Could not upload attachment.` |
| Source code | Available | Upload seam and crop caller are in repo. |
| Local crop metadata for TR `100000724` | Available | `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000724\working\pla_b\survey_diagram_selection.json` records failed upload. |
| Local crop PNG for TR `100000724` | Available | `survey_diagram_selection.png` exists in the same folder, 968,067 bytes. |
| Live Innola HTTP response/status | Missing | Metadata category is `HttpRequestException`, so no HTTP status/body was persisted. Debug output only logs exception type today. |
| Innola HTTPS reachability | Partial | `Test-NetConnection` to configured hosts failed from PowerShell while DNS resolved; Chrome showed the Innola Swagger client-certificate prompt. |
| Browser certificate prompt | Available | Chrome incognito prompted for `Jamaica eTitles Project Team`, issuer `IS Digital ID CA - JM`, serial `45AF05AA02`. |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --- | --- | --- | --- |
| 1 | Trace exact upload failure branches | High | Done | Failure category maps to `UploadAttachmentAsync` catch branch. |
| 2 | Inspect upload route/binding/source-type config | High | Done | `st_plan_annex_image` plus attachment upload settings confirmed. |
| 3 | Locate TR `100000724` case folder and crop metadata | High | Done | PNG exists and metadata records `HttpRequestException`. |
| 4 | Check if source registration after upload is a separate possible failure | Medium | Done | This failure did not reach registration; upload itself threw before response handling completed. |
| 5 | Preserve safe transport diagnostic detail | High | Done | Patched shared upload seam to return safe exception/inner exception diagnostics and added regression coverage. |
| 6 | Restore Innola network/certificate reachability | High | Open | Chrome indicates the endpoint requires a client certificate. |
| 7 | Route crop attachment through the same cert-aware Innola client as other transaction flows | High | Done | Patched crop service/default services to use `InnolaHttpClientFactory`; factory falls back to automatic client-certificate selection if manual lookup misses. |

## Timeline of Events

| Time | Event | Source | Confidence |
| --- | --- | --- | --- |
| 2026-08-27 | User observed upload failure message for TR `100000724`. | User report | Confirmed |
| 2026-08-27T15:55:56Z | Crop evidence was created for TR `100000724`, PE `100000628`. | Case metadata | Confirmed |
| 2026-08-27T18:01:27Z | Upload failed and metadata was updated with category `HttpRequestException`. | Case metadata | Confirmed |
| 2026-08-27 | `Test-NetConnection eltrs-test.innola-solutions.com -Port 443` failed; DNS resolved to `5.9.84.210`. | Local network check | Confirmed |
| 2026-08-27 | `Test-NetConnection eltrs-dev.innola-solutions.com -Port 443` failed; DNS resolved to `5.9.84.210`. | Local network check | Confirmed |
| 2026-08-27 | `Test-NetConnection eltrs.innola-solutions.com -Port 443` failed; DNS resolved to `213.160.156.140`. | Local network check | Confirmed |
| 2026-08-27T18:53:45Z | Retried upload after Bearer auth fallback patch; metadata recorded `upload_auth_mode = bearer` but still failed with remote host forcibly closing the multipart stream. | Case metadata | Confirmed |
| 2026-08-27T19:03:45Z | Retried upload after alternate route patch; metadata recorded `upload_route = scanning/source/attach`, `upload_mode = attach_only`, `upload_task_value = 054aed4c-9c02-11f1-b826-ca0ed72590b6`, and still failed with remote host closing multipart stream. | Case metadata | Confirmed |
| 2026-08-27 | User opened Swagger in Chrome incognito and received a client-certificate prompt for `Jamaica eTitles Project Team`. | User screenshot | Confirmed |
| 2026-08-27 | Local `CurrentUser\My` and `LocalMachine\My` scans did not find the configured `Jamaica eTitles Project Team` certificate/thumbprint visible to the add-in's manual lookup path. | Local certificate scan | Confirmed |
| 2026-08-27 | Crop attach path was patched to reuse `ShellState.TransactionDetails`; default Innola services were patched to create certificate-aware clients; factory now tries automatic client-certificate selection when manual lookup misses. | Source code | Confirmed |

## Confirmed Findings

### Finding 1: The failure is in the shared Innola attachment upload seam

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs:237`

**Detail:** The `UploadAttachmentAsync` catch branch maps `HttpRequestException`, `TaskCanceledException`, `InvalidOperationException`, and `UriFormatException` to `Could not upload attachment. Try again.`

### Finding 2: The crop PNG and metadata exist for TR 100000724

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000724\working\pla_b\survey_diagram_selection.json`

**Detail:** Metadata records `current_transaction_number` = `100000724`, `current_transaction_id` = `01a014f0-e60b-7208-b94b-4511fd9aeedd`, `current_task_id` = `054aed4c-9c02-11f1-b826-ca0ed72590b6`, `configured_source_type` = `st_plan_annex_image`, `local_save_status` = `saved`, `upload_status` = `failed`, `error_category` = `HttpRequestException`, and `message` = `Could not upload attachment. Try again.`

### Finding 3: Source type and upload configuration are present

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json:512`

**Detail:** Upload route is `source/sources/attach`, binding mode is `query_only`, upload mode is `attach_then_register_source`, and client certificate settings are enabled.

### Finding 4: The current code discards the actionable transport diagnostic

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs:239`

**Detail:** The catch branch logs only `exception.GetType().Name` and returns generic `Could not upload attachment. Try again.` with error category `HttpRequestException`.

**Resolution:** Patched on 2026-08-27 so the catch branch returns `Could not upload attachment. {safeDiagnostic}` where `safeDiagnostic` includes exception and inner exception context with token/password/secret/raw structured diagnostics redacted.

### Finding 5: Innola HTTPS is not reachable from this machine during investigation

**Evidence:** Local command `Test-NetConnection eltrs-test.innola-solutions.com -Port 443`

**Detail:** DNS resolution succeeded, but TCP connection to port 443 failed for test/dev (`5.9.84.210`) and production (`213.160.156.140`) Innola hosts.

### Finding 6: Bearer auth did run but did not resolve the crop upload failure

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000724\working\pla_b\survey_diagram_selection.json`

**Detail:** Latest metadata records `upload_route = source/sources/attach`, `upload_binding_mode = query_only`, `upload_mode = attach_then_register_source`, `upload_auth_mode = bearer`, `upload_task_value = 01a014f0-e60b-7208-b94b-4511fd9aeedd`, `upload_content_type = image/png`, and `upload_byte_count = 815145`. The persisted message is `HttpRequestException: Error while copying content to a stream` with inner connection-reset diagnostics.

### Finding 7: The fallback route also ran and failed; the crop path is unique as PNG upload

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000724\working\pla_b\survey_diagram_selection.json`

**Detail:** Latest metadata records `upload_route = scanning/source/attach`, `upload_binding_mode = query_and_form`, `upload_mode = attach_only`, `upload_auth_mode = bearer`, `upload_task_value = 054aed4c-9c02-11f1-b826-ca0ed72590b6`, `upload_content_type = image/png`, and `upload_byte_count = 1143505`. The message confirms `Fallback route also failed` with the same transport reset.

### Finding 8: Current TR binding and local source type configuration are valid

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000724\manifest.json`; `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`

**Detail:** Manifest records Current TR `100000724`, transaction GUID `01a014f0-e60b-7208-b94b-4511fd9aeedd`, task id `054aed4c-9c02-11f1-b826-ca0ed72590b6`, and source `pe489541.pdf` as `st_survey_diagram`. Settings include `st_plan_annex_image` as an internal generated PNG source type.

### Finding 9: Known successful generated attachments in the add-in are PDF, not PNG

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000628\working\compute_report_attachment.json`; `ComputeReportAttachmentService.cs`; `CompareReportAttachmentService.cs`

**Detail:** PE `100000628` successfully attached `st_compute_report` as a 26,445 byte PDF. Compute and Compare report attachment services both upload `application/pdf`. The PLA_B crop evidence path is the only observed generated `image/png` upload path.

### Finding 10: Swagger confirms Innola test requires a client certificate

**Evidence:** User screenshot of `https://eltrs-test.innola-solutions.com/rest-api/`

**Detail:** Chrome prompted to select a certificate for `eltrs-test.innola-solutions.com:443`; the visible certificate was `Jamaica eTitles Project Team`, issuer `IS Digital ID CA - JM`, serial `45AF05AA02`.

### Finding 11: Crop attach had a cert-client gap

**Evidence:** `PlaBSupportingDocumentCropService.cs`; `InnolaTransactionDetailService.cs`; `InnolaHttpClientFactory.cs`

**Detail:** The crop service default constructor created its own `InnolaTransactionDetailService`, and that detail-service default used a plain `HttpClient`. This could bypass the shared `ShellState.TransactionDetails` client that is configured for Innola client certificates.

**Resolution:** Patched crop attach to reuse `ShellState.TransactionDetails`, patched default Innola services to create clients through `InnolaHttpClientFactory`, and made the factory use `ClientCertificateOption.Automatic` when manual configured certificate lookup fails.

## Deduced Conclusions

### Deduction 1: This was not caused by missing crop output or missing `st_plan_annex_image`

**Based on:** Findings 2 and 3

**Reasoning:** The metadata records a saved PNG path and configured source type. The upload moved far enough to invoke the shared upload service and fail with `HttpRequestException`.

**Conclusion:** The active failure is in the HTTP transport/request path, not crop rendering, case-folder persistence, or source-type lookup.

### Deduction 2: Innola did not return a persisted HTTP status for this failure

**Based on:** Findings 1 and 4

**Reasoning:** Non-success HTTP responses are handled before the catch branch and would return a status-derived category such as `session_expired` or a status name. Metadata instead recorded `HttpRequestException`.

**Conclusion:** The request failed before normal response handling completed, or `InnolaApiResilience` wrapped a retryable connection exception into `HttpRequestException`.

### Deduction 3: Adding `st_plan_annex_image` on the server does not address the current observed failure

**Based on:** Findings 2, 3, and 5

**Reasoning:** Missing server document dictionary/source type would normally produce a server response after the request reaches Innola. The current metadata and network checks point to HTTPS transport failure before a normal response is received.

**Conclusion:** Keep the dictionary entry, but the next blocker is network reachability or transport setup, not the dictionary itself.

## Hypothesized Paths

### Hypothesis 1: Live Innola rejected the upload request

**Status:** Refuted

**Theory:** The request reached Innola but received a non-success HTTP response.

**Supporting indicators:** The message text is produced by the upload service on failed upload.

**Would confirm:** Debug line with upload HTTP status/body or crop metadata `error_category` showing an HTTP category.

**Would refute:** Metadata or logs showing `unauthorized`, missing session, or local exception before HTTP response.

**Resolution:** Refuted by metadata category `HttpRequestException`; non-success HTTP statuses are categorized separately in `UploadAttachmentAsync`.

### Hypothesis 2: Session was unavailable or expired

**Status:** Refuted

**Theory:** The add-in attempted upload with missing/expired Innola session/token.

**Supporting indicators:** `UploadAttachmentAsync` returns a generic upload failure before HTTP upload when session/token is unavailable.

**Would confirm:** Metadata `error_category` = `unauthorized` or corresponding debug/session diagnostic.

**Would refute:** Metadata category other than `unauthorized`.

**Resolution:** Refuted by metadata category `HttpRequestException`; the crop service would have recorded `session_unavailable` for a missing local session and the upload service would return `unauthorized` for missing server/token.

### Hypothesis 3: TLS/client-certificate/network transport failed during the multipart upload

**Status:** Confirmed

**Theory:** The request did not complete due to TLS/client-certificate handshake, proxy/DNS, connection reset, timeout, or another HTTP transport exception.

**Supporting indicators:** Metadata records `HttpRequestException`; client certificate settings are active; the code currently discards the exception message that would distinguish these cases.

**Would confirm:** Persisted or captured exception message/inner exception naming SSL/TLS, DNS, timeout, connection refused/reset, or certificate failure.

**Would refute:** Captured diagnostic showing upload endpoint returned a normal HTTP response code or registration failed after upload.

**Resolution:** Confirmed at the transport/reachability level. The Chrome Swagger certificate prompt and crop-service client wiring point to client-certificate selection as the next subcause to validate.

### Hypothesis 4: The crop upload was not sending the required client certificate

**Status:** Likely

**Theory:** Other add-in flows used a shared cert-aware client, but crop upload used a default detail service with a plain `HttpClient`, so Innola closed the stream during or after the TLS/multipart request.

**Supporting indicators:** Chrome prompts for a client certificate; manual store lookup did not find the configured cert; crop service default bypassed `ShellState.TransactionDetails`.

**Would confirm:** Retry after the cert-aware patch succeeds, or metadata changes from connection reset to a clean HTTP status/application validation response.

**Would refute:** Retry after the patch still fails with the same connection reset while certificate diagnostics show a selected certificate was sent.

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | --- | --- |
| Underlying `HttpRequestException.Message` and inner exception | Needed to identify the exact transport cause. | Patch diagnostics or capture debugger output with full exception detail. |
| Reason TCP 443 cannot reach Innola hosts | Needed to fix the actual environment blocker. | Check VPN, firewall, proxy, endpoint allowlist, and whether Innola test/dev/prod hosts are up from another network. |

## Source Code Trace

| Element | Detail |
| --- | --- |
| Error origin | `InnolaTransactionDetailService.UploadAttachmentAsync` catch branch |
| Trigger | `PlaBSupportingDocumentCropService.AttachSavedCropAsync` after user confirms Attach |
| Condition | Metadata records `HttpRequestException`; no HTTP status/body was persisted |
| Related files | `PlaBSupportingDocumentCropService.cs`, `WorkflowSettings.json`, `InnolaTransactionDetailService.cs` |

## Conclusion

**Confidence:** High

The crop output, Current TR binding, and `st_plan_annex_image` configuration are valid. The strongest remaining blocker is transport setup, specifically client-certificate selection for the Innola test endpoint. The crop upload path has been patched to use the same certificate-aware Innola client pattern as the rest of the add-in.

## Recommended Next Steps

### Fix direction

Install the cert-aware add-in patch and restart ArcGIS Pro before retrying. Failed upload retries should now persist safe transport diagnostics in crop metadata.

Follow-up patch added `innola_attachment_upload_auth_mode` with default `access_token_then_bearer`. Attachment upload now retries retryable access-token transport failures and 401/403 responses with Bearer auth, and crop metadata persists the actual route, binding mode, upload mode, auth mode used, task value, content type, and byte count.

Exploratory alternate-route, PDF, and source-type fallback attempts were reverted. The intended story behavior is strict: upload `survey_diagram_selection.png` as `image/png` to Current TR `100000724` using source/document type `st_plan_annex_image`.

Final follow-up patch changed crop upload to reuse `ShellState.TransactionDetails`, changed default Innola transaction/detail services to use `InnolaHttpClientFactory`, and set the factory to try Windows automatic client-certificate selection if the configured manual certificate is not found.

### Diagnostic

Retry Attach after network reachability is restored. If it still fails, inspect `working/pla_b/survey_diagram_selection.json`; the `message` field should now include a safe exception summary instead of only the generic upload message.

## Reproduction Plan

1. Open TR `100000724`.
2. Reopen the Supporting Documents crop window for the saved crop evidence.
3. Click `Attach`.
4. Expected current behavior: metadata remains `upload_status = failed` and `error_category = HttpRequestException` if the same transport failure repeats.
5. Expected after diagnostic/auth/certificate patch: metadata records the primary upload route, auth attempt, task value, content type, byte count, and either success or a safer transport diagnostic. If it still fails with a connection reset, validate the configured certificate thumbprint/store against the certificate Chrome is prompting for.

## Side Findings

- The current request path uses `source/sources/attach` and, because upload mode is `attach_then_register_source`, sends the transaction GUID as the `taskId` query value for this route.
- New metadata fields to inspect after retry: `upload_route`, `upload_binding_mode`, `upload_mode`, `upload_auth_mode`, `upload_task_value`, `upload_content_type`, and `upload_byte_count`.
