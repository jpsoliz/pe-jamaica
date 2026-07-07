using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Workflow.Disposition;
using ParcelWorkflowAddIn.Workflow.Output;
using ParcelWorkflowAddIn.Workflow.Reports;

namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaPlanCheckService : IInnolaPlanCheckService
{
    private const string PlanTypeKey = "plan";
    private const string RequestEvidenceFileName = "plan_check_api_request.json";
    private const string ResponseEvidenceFileName = "plan_check_api_response.json";
    private const string FailureEvidenceFileName = "plan_check_api_failure.json";

    private static readonly JsonSerializerOptions TraceJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly HttpClient httpClient;
    private readonly OutputSummaryPersistenceService outputSummaryPersistenceService = new();

    public InnolaPlanCheckService()
        : this(new HttpClient())
    {
    }

    public InnolaPlanCheckService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<InnolaPlanCheckWritebackResult> WriteAsync(
        InnolaSession session,
        SelectedInnolaTransaction transaction,
        string caseFolderPath,
        ComputeReviewDispositionDocument disposition,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(session.ServerUrl) || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return InnolaPlanCheckWritebackResult.Failed("Could not write Innola Plan Check values because the Innola session is not authorized.", "unauthorized");
        }

        if (string.IsNullOrWhiteSpace(transaction.TransactionId))
        {
            return InnolaPlanCheckWritebackResult.Failed("Could not write Innola Plan Check values because the transaction id is unavailable.", "transaction_id_missing");
        }

        try
        {
            var layout = CaseFolderLayout.FromRootDirectory(caseFolderPath);
            var evidence = LoadEvidence(layout, disposition);
            var plans = await FetchPlansAsync(session, transaction.TransactionId, cancellationToken).ConfigureAwait(false);
            if (plans.Count == 0)
            {
                WriteFailure(layout, transaction, "plan_missing", "Plan API returned no Plan objects.");
                return InnolaPlanCheckWritebackResult.Failed("Innola Plan Check writeback failed because no Plan object was returned.", "plan_missing");
            }

            var mutation = ApplyPlanCheckUpdates(plans, evidence);
            if (mutation.ChecklistRowCount == 0)
            {
                WriteFailure(layout, transaction, "checklist_missing", "Plan API returned no checkList rows that can be updated.");
                return InnolaPlanCheckWritebackResult.Failed("Innola Plan Check writeback failed because no Plan checklist rows were returned.", "checklist_missing");
            }

            if (mutation.Updates.Count == 0)
            {
                WriteFailure(layout, transaction, "checklist_no_supported_rows", "Plan API returned checklist rows, but none matched automated Plan Check mappings.");
                return InnolaPlanCheckWritebackResult.Failed("Innola Plan Check writeback failed because no supported Plan checklist rows were returned.", "checklist_no_supported_rows");
            }

            WriteRequestEvidence(layout, transaction, disposition, evidence.ReportPath, mutation);
            var saved = await SavePlansAsync(session, transaction.TransactionId, plans, cancellationToken).ConfigureAwait(false);
            WriteResponseEvidence(layout, transaction, mutation, saved.Count);
            return InnolaPlanCheckWritebackResult.Succeeded(updates: mutation.Updates);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or JsonException
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or TaskCanceledException
            or UriFormatException)
        {
            TryWriteFailure(caseFolderPath, transaction, exception);
            Debug.WriteLine($"Innola Plan Check writeback failed. TransactionId={transaction.TransactionId}; Error={exception.GetType().Name}.");
            return InnolaPlanCheckWritebackResult.Failed("Innola Plan Check writeback failed. Try again.", exception.GetType().Name);
        }
    }

    private async Task<IReadOnlyList<JsonObject>> FetchPlansAsync(InnolaSession session, string transactionId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildPlanUri(session, transactionId));
        InnolaHttp.ApplyAuthHeaders(request, session.AccessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Plan Check GET failed: {response.StatusCode}");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ResolveArray(JsonNode.Parse(body))
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Cast<JsonObject>()
            .ToArray();
    }

    private async Task<IReadOnlyList<JsonObject>> SavePlansAsync(
        InnolaSession session,
        string transactionId,
        IReadOnlyList<JsonObject> plans,
        CancellationToken cancellationToken)
    {
        var payload = new JsonArray(plans.Select(plan => plan.DeepClone()).ToArray());
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildPlanUri(session, transactionId));
        InnolaHttp.ApplyAuthHeaders(request, session.AccessToken);
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Plan Check POST failed: {response.StatusCode}");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ResolveArray(JsonNode.Parse(body))
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Cast<JsonObject>()
            .ToArray();
    }

    private static Uri BuildPlanUri(InnolaSession session, string transactionId)
    {
        return InnolaHttp.BuildUri(
            session.ServerUrl,
            $"{InnolaSettings.V4RestPath}administrative/ladm-objects?typeKeyId={PlanTypeKey}&transactionId={Uri.EscapeDataString(transactionId)}");
    }

    private PlanCheckEvidence LoadEvidence(CaseFolderLayout layout, ComputeReviewDispositionDocument disposition)
    {
        var reportPath = ResolveReportPath(layout, disposition);
        var report = File.Exists(reportPath) ? JsonNode.Parse(File.ReadAllText(reportPath)) as JsonObject : null;
        if (report is null)
        {
            throw new InvalidOperationException("Compute examination report is missing or invalid.");
        }

        return new PlanCheckEvidence(
            reportPath,
            report,
            TryLoadJson(Path.Combine(layout.OutputDirectory, "output_summary.json")),
            TryLoadJson(Path.Combine(layout.OutputDirectory, "enterprise_working_publish.json")),
            TryLoadJson(Path.Combine(layout.WorkingDirectory, "enterprise_working_disposition.json")),
            outputSummaryPersistenceService.Load(layout));
    }

    private static string ResolveReportPath(CaseFolderLayout layout, ComputeReviewDispositionDocument disposition)
    {
        if (!string.IsNullOrWhiteSpace(disposition.ComputeExaminationReportRef))
        {
            return Path.IsPathRooted(disposition.ComputeExaminationReportRef)
                ? disposition.ComputeExaminationReportRef
                : Path.Combine(layout.RootDirectory, disposition.ComputeExaminationReportRef.Replace('/', Path.DirectorySeparatorChar));
        }

        return Path.Combine(layout.ReportsDirectory, ComputeExaminationReportService.ReportFileName);
    }

    private static JsonObject? TryLoadJson(string path)
    {
        try
        {
            return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static PlanCheckMutationResult ApplyPlanCheckUpdates(
        IReadOnlyList<JsonObject> plans,
        PlanCheckEvidence evidence)
    {
        var updates = new List<InnolaPlanCheckUpdate>();
        var preservedUnsupported = new List<InnolaPlanCheckPreservedRow>();
        var checklistRowCount = 0;
        foreach (var plan in plans)
        {
            if (plan["checkList"] is not JsonArray checkList)
            {
                continue;
            }

            foreach (var node in checkList)
            {
                if (node is not JsonObject check)
                {
                    continue;
                }

                checklistRowCount++;
                var checkType = ReadString(check, "checkType");
                if (string.IsNullOrWhiteSpace(checkType))
                {
                    continue;
                }

                var decision = ResolveDecision(checkType, evidence);
                if (decision is null)
                {
                    if (IsKnownUnsupportedCheckType(checkType))
                    {
                        preservedUnsupported.Add(new InnolaPlanCheckPreservedRow(
                            checkType,
                            ReadNullableBool(check, "passed"),
                            ReadString(check, "description"),
                            "No automated Plan Check rule exists yet; row preserved as returned by Innola."));
                    }

                    continue;
                }

                var previousPassed = ReadNullableBool(check, "passed");
                var previousDescription = ReadString(check, "description");
                check["passed"] = decision.Passed;
                check["description"] = decision.Description;
                updates.Add(new InnolaPlanCheckUpdate(
                    checkType,
                    previousPassed,
                    decision.Passed,
                    previousDescription,
                    decision.Description));
            }
        }

        return new PlanCheckMutationResult(updates, preservedUnsupported, checklistRowCount);
    }

    private static PlanCheckDecision? ResolveDecision(string checkType, PlanCheckEvidence evidence)
    {
        return checkType switch
        {
            "plan_check_type_closure" => StageDecision(
                evidence,
                new[] { "dimension_check", "validate_points_and_lines" },
                "Closure accepted from Dimension Check and Validate Points and Lines.",
                "Closure could not be accepted because dimension or point validation blockers remain."),
            "plan_check_type_area" => AreaDecision(evidence),
            "plan_check_type_compplan_datasheet" => ComputationSheetDecision(evidence),
            "plan_check_type_plotting" => PlottingDecision(evidence),
            "plan_check_type_details" => StageDecision(
                evidence,
                new[] { "dimension_check" },
                "Bearing, distance, point-reference, and dimension checks are acceptable.",
                "Dimension details are not acceptable for closeout."),
            "plan_check_type_general" => GeneralDecision(evidence),
            "plan_check_type_notices" => null,
            "plan_check_type_adjoining" => null,
            _ => null
        };
    }

    private static bool IsKnownUnsupportedCheckType(string checkType)
    {
        return checkType.Equals("plan_check_type_notices", StringComparison.OrdinalIgnoreCase)
            || checkType.Equals("plan_check_type_adjoining", StringComparison.OrdinalIgnoreCase);
    }

    private static PlanCheckDecision StageDecision(
        PlanCheckEvidence evidence,
        IReadOnlyList<string> stageIds,
        string passDescription,
        string failDescription)
    {
        var stages = stageIds
            .Select(id => FindStage(evidence.Report, id))
            .Where(stage => stage is not null)
            .Cast<JsonObject>()
            .ToArray();
        if (stages.Length == 0)
        {
            return PlanCheckDecision.Fail(failDescription);
        }

        var failed = stages.Any(stage => !HasAcceptableStatus(stage) || HasBlockingStatusOrFinding(stage));
        return failed ? PlanCheckDecision.Fail(failDescription) : PlanCheckDecision.Pass(passDescription);
    }

    private static PlanCheckDecision AreaDecision(PlanCheckEvidence evidence)
    {
        var polygonCount = ReadNumber(evidence.OutputSummary, "payload", "polygon_count")
            ?? ReadNumber(evidence.OutputSummary, "payload", "built_parcel_count")
            ?? ReadNumber(evidence.OutputSummary, "polygon_count")
            ?? ReadNumber(evidence.OutputSummary, "built_parcel_count");
        if (polygonCount is null or <= 0)
        {
            return PlanCheckDecision.Fail("Area could not be accepted because no generated polygon evidence is available.");
        }

        return PlanCheckDecision.Pass($"Area accepted from {polygonCount:0} generated polygon(s).");
    }

    private static PlanCheckDecision ComputationSheetDecision(PlanCheckEvidence evidence)
    {
        var hasComputationSheetEvidence = ReportContains(evidence.Report, "computation")
            || ReportContains(evidence.Report, "datasheet")
            || ReportContains(evidence.Report, "data sheet")
            || ReportContains(evidence.Report, "primary");
        return hasComputationSheetEvidence
            ? PlanCheckDecision.Pass("Computation sheet/data sheet was accepted as the primary source.")
            : PlanCheckDecision.Fail("Computation sheet/data sheet primary-source evidence is missing.");
    }

    private static PlanCheckDecision PlottingDecision(PlanCheckEvidence evidence)
    {
        var createSpatialUnits = FindStage(evidence.Report, "create_spatial_units");
        var finalReview = FindStage(evidence.Report, "final_review");
        var enterprisePublish = FindStage(evidence.Report, "enterprise_working_publish");
        var enterpriseDisposition = FindStage(evidence.Report, "enterprise_disposition");
        var failed = new[] { createSpatialUnits, finalReview, enterprisePublish, enterpriseDisposition }
            .Any(stage => stage is null || !HasAcceptableStatus(stage) || HasBlockingStatusOrFinding(stage));
        return failed
            ? PlanCheckDecision.Fail("Plotting could not be accepted because spatial units, final review, or Enterprise disposition is incomplete.")
            : PlanCheckDecision.Pass("Plotting accepted from completed spatial units, final review, and Enterprise working-layer disposition.");
    }

    private static PlanCheckDecision GeneralDecision(PlanCheckEvidence evidence)
    {
        var decision = ReadString(evidence.Report, "closeout", "decision");
        var finalReview = FindStage(evidence.Report, "final_review");
        var passed = string.Equals(decision, "approved", StringComparison.OrdinalIgnoreCase)
            && finalReview is not null
            && HasAcceptableStatus(finalReview)
            && !HasBlockingStatusOrFinding(finalReview);
        return passed
            ? PlanCheckDecision.Pass("Compute Final Review approved; see compute examination report.")
            : PlanCheckDecision.Fail("Compute Final Review is not approved or closeout prerequisites are incomplete.");
    }

    private static JsonObject? FindStage(JsonObject? report, string stageId)
    {
        if (report?["stages"] is not JsonArray stages)
        {
            return null;
        }

        return stages
            .OfType<JsonObject>()
            .FirstOrDefault(stage => string.Equals(ReadString(stage, "stage_id"), stageId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasBlockingStatusOrFinding(JsonObject stage)
    {
        var status = ReadString(stage, "status");
        if (IsBlockingValue(status))
        {
            return true;
        }

        if (stage["findings"] is not JsonArray findings)
        {
            return false;
        }

        return findings.OfType<JsonObject>().Any(finding =>
            IsBlockingValue(ReadString(finding, "outcome"))
            || IsBlockingValue(ReadString(finding, "severity"))
            || IsBlockingValue(ReadString(finding, "workflow_effect")));
    }

    private static bool HasAcceptableStatus(JsonObject stage)
    {
        var status = ReadString(stage, "status");
        return status is not null
            && (status.Equals("passed", StringComparison.OrdinalIgnoreCase)
                || status.Equals("approved", StringComparison.OrdinalIgnoreCase)
                || status.Equals("accepted", StringComparison.OrdinalIgnoreCase)
                || status.Equals("succeeded", StringComparison.OrdinalIgnoreCase)
                || status.Equals("success", StringComparison.OrdinalIgnoreCase)
                || status.Equals("written", StringComparison.OrdinalIgnoreCase)
                || status.Equals("created", StringComparison.OrdinalIgnoreCase)
                || status.Equals("complete", StringComparison.OrdinalIgnoreCase)
                || status.Equals("completed", StringComparison.OrdinalIgnoreCase)
                || status.Equals("available", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ReportContains(JsonObject? report, string value)
    {
        return report?.ToJsonString().Contains(value, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsBlockingValue(string? value)
    {
        return value is not null
            && (value.Equals("failed", StringComparison.OrdinalIgnoreCase)
                || value.Equals("failure", StringComparison.OrdinalIgnoreCase)
                || value.Equals("blocked", StringComparison.OrdinalIgnoreCase)
                || value.Equals("blocker", StringComparison.OrdinalIgnoreCase)
                || value.Equals("error", StringComparison.OrdinalIgnoreCase)
                || value.Equals("fatal", StringComparison.OrdinalIgnoreCase));
    }

    private static decimal? ReadNumber(JsonObject? root, params string[] path)
    {
        JsonNode? current = root;
        foreach (var name in path)
        {
            current = current is JsonObject obj ? obj[name] : null;
            if (current is null)
            {
                return null;
            }
        }

        return current is JsonValue value && value.TryGetValue<decimal>(out var number)
            ? number
            : null;
    }

    private static string? ReadString(JsonObject? root, params string[] path)
    {
        JsonNode? current = root;
        foreach (var name in path)
        {
            current = current is JsonObject obj ? obj[name] : null;
            if (current is null)
            {
                return null;
            }
        }

        if (current is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return current?.ToJsonString();
    }

    private static bool? ReadNullableBool(JsonObject root, string propertyName)
    {
        var node = root[propertyName];
        if (node is null || node.GetValueKind() == JsonValueKind.Null)
        {
            return null;
        }

        return node.GetValueKind() == JsonValueKind.True
            ? true
            : node.GetValueKind() == JsonValueKind.False
                ? false
                : null;
    }

    private static IReadOnlyList<JsonNode> ResolveArray(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            return array.OfType<JsonNode>().ToArray();
        }

        if (node is JsonObject obj && obj["data"] is JsonArray data)
        {
            return data.OfType<JsonNode>().ToArray();
        }

        return Array.Empty<JsonNode>();
    }

    private static void WriteRequestEvidence(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        ComputeReviewDispositionDocument disposition,
        string reportPath,
        PlanCheckMutationResult mutation)
    {
        Directory.CreateDirectory(layout.WorkingDirectory);
        var evidence = new
        {
            schema_version = "plan_check_api_request_v1",
            written_at_utc = DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
            transaction_id = transaction.TransactionId,
            transaction_number = transaction.TransactionNumber,
            task_id = transaction.TaskId,
            operator_id = disposition.OperatorId,
            report_ref = SafeRelativePath(layout, reportPath),
            updated_check_types = mutation.Updates.Select(update => update.CheckType).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            preserved_unsupported_check_types = mutation.PreservedUnsupported.Select(row => row.CheckType).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            preserved_unsupported_rows = mutation.PreservedUnsupported,
            updates = mutation.Updates
        };
        File.WriteAllText(
            Path.Combine(layout.WorkingDirectory, RequestEvidenceFileName),
            JsonSerializer.Serialize(evidence, TraceJsonOptions));
    }

    private static void WriteResponseEvidence(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        PlanCheckMutationResult mutation,
        int savedPlanCount)
    {
        Directory.CreateDirectory(layout.WorkingDirectory);
        var evidence = new
        {
            schema_version = "plan_check_api_response_v1",
            written_at_utc = DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
            transaction_id = transaction.TransactionId,
            transaction_number = transaction.TransactionNumber,
            task_id = transaction.TaskId,
            status = "saved",
            saved_plan_count = savedPlanCount,
            updated_count = mutation.Updates.Count,
            updated_check_types = mutation.Updates.Select(update => update.CheckType).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            preserved_unsupported_check_types = mutation.PreservedUnsupported.Select(row => row.CheckType).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
        File.WriteAllText(
            Path.Combine(layout.WorkingDirectory, ResponseEvidenceFileName),
            JsonSerializer.Serialize(evidence, TraceJsonOptions));
    }

    private static void TryWriteFailure(string caseFolderPath, SelectedInnolaTransaction transaction, Exception exception)
    {
        try
        {
            var layout = CaseFolderLayout.FromRootDirectory(caseFolderPath);
            WriteFailure(layout, transaction, exception.GetType().Name, exception.Message);
        }
        catch (Exception writeException) when (writeException is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or InvalidOperationException)
        {
            // Diagnostics must not hide the original API failure.
        }
    }

    private static void WriteFailure(CaseFolderLayout layout, SelectedInnolaTransaction transaction, string errorCategory, string message)
    {
        Directory.CreateDirectory(layout.WorkingDirectory);
        var evidence = new
        {
            schema_version = "plan_check_api_failure_v1",
            written_at_utc = DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
            transaction_id = transaction.TransactionId,
            transaction_number = transaction.TransactionNumber,
            task_id = transaction.TaskId,
            error_category = errorCategory,
            error_message = SanitizeDiagnostic(message)
        };
        File.WriteAllText(
            Path.Combine(layout.WorkingDirectory, FailureEvidenceFileName),
            JsonSerializer.Serialize(evidence, TraceJsonOptions));
    }

    private static string SanitizeDiagnostic(string message)
    {
        return message.Contains("token", StringComparison.OrdinalIgnoreCase)
            || message.Contains("password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("{", StringComparison.Ordinal)
            || message.Contains("}", StringComparison.Ordinal)
                ? "Innola Plan Check writeback failed. Sensitive diagnostic was redacted."
                : message;
    }

    private static string SafeRelativePath(CaseFolderLayout layout, string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetRelativePath(layout.RootDirectory, path).Replace('\\', '/')
            : path.Replace('\\', '/');
    }

    private sealed record PlanCheckEvidence(
        string ReportPath,
        JsonObject Report,
        JsonObject? OutputSummary,
        JsonObject? EnterprisePublish,
        JsonObject? EnterpriseDisposition,
        OutputSummaryDocument? OutputSummaryDocument);

    private sealed record PlanCheckMutationResult(
        IReadOnlyList<InnolaPlanCheckUpdate> Updates,
        IReadOnlyList<InnolaPlanCheckPreservedRow> PreservedUnsupported,
        int ChecklistRowCount);

    private sealed record InnolaPlanCheckPreservedRow(
        string CheckType,
        bool? Passed,
        string? Description,
        string Reason);

    private sealed record PlanCheckDecision(bool Passed, string Description)
    {
        public static PlanCheckDecision Pass(string description)
        {
            return new PlanCheckDecision(true, description);
        }

        public static PlanCheckDecision Fail(string description)
        {
            return new PlanCheckDecision(false, description);
        }
    }
}
