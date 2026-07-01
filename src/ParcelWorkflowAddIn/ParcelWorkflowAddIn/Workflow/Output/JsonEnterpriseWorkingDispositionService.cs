using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Disposition;

namespace ParcelWorkflowAddIn.Workflow.Output;

public sealed class JsonEnterpriseWorkingDispositionService : IEnterpriseWorkingDispositionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly Func<InnolaTransactionSettings> getSettings;

    public JsonEnterpriseWorkingDispositionService()
        : this(InnolaTransactionSettings.Load)
    {
    }

    internal JsonEnterpriseWorkingDispositionService(Func<InnolaTransactionSettings> getSettings)
    {
        this.getSettings = getSettings;
    }

    public Task<EnterpriseWorkingDispositionResult> RecordDispositionAsync(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        ComputeReviewDispositionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = getSettings();
        var reviewSettings = settings.EnterpriseWorkingReview;
        var scopeField = string.IsNullOrWhiteSpace(reviewSettings.TransactionScopeField)
            ? "transaction_number"
            : reviewSettings.TransactionScopeField;
        var scopeValue = ResolveScopeValue(manifest, scopeField);
        if (string.IsNullOrWhiteSpace(scopeValue))
        {
            return Task.FromResult(EnterpriseWorkingDispositionResult.Failed(
                "Enterprise disposition writeback could not resolve the transaction scope.",
                new[] { $"The scope field '{scopeField}' does not map to a value from the current manifest." }));
        }

        var targets = new (string Role, string? Path)[]
        {
            ("points", reviewSettings.Layers.Points),
            ("lines", reviewSettings.Layers.Lines),
            ("polygons", reviewSettings.Layers.Polygons),
            ("case_index", reviewSettings.Layers.CaseIndex)
        };
        var errors = new List<string>();
        var updatedRoles = new List<string>();
        var decidedAtUtc = DateTimeOffset.UtcNow.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        foreach (var target in targets)
        {
            if (string.IsNullOrWhiteSpace(target.Path))
            {
                errors.Add($"{target.Role} target is not configured.");
                continue;
            }

            try
            {
                if (Uri.TryCreate(target.Path, UriKind.Absolute, out var uri)
                    && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add($"{target.Role} FeatureServer disposition writeback is not implemented in this build.");
                    continue;
                }

                UpdateJsonStore(target.Path, target.Role, scopeField, scopeValue, request, decidedAtUtc);
                updatedRoles.Add(target.Role);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or NotSupportedException)
            {
                errors.Add($"{target.Role} disposition writeback failed: {exception.Message}");
            }
        }

        if (errors.Count > 0)
        {
            return Task.FromResult(EnterpriseWorkingDispositionResult.Failed(
                "Enterprise disposition writeback failed.",
                errors));
        }

        Directory.CreateDirectory(layout.WorkingDirectory);
        var evidencePath = Path.Combine(layout.WorkingDirectory, "enterprise_working_disposition.json");
        var evidence = new JsonObject
        {
            ["schema_version"] = "1.0.0",
            ["transaction_id"] = manifest.TransactionId,
            ["transaction_number"] = manifest.Payload.InnolaTransaction?.TransactionNumber ?? manifest.TransactionId,
            ["decision"] = request.Decision.ToContractValue(),
            ["comment"] = request.Comment ?? string.Empty,
            ["operator_id"] = request.OperatorId ?? string.Empty,
            ["decided_at_utc"] = decidedAtUtc,
            ["scope_field"] = scopeField,
            ["scope_value"] = scopeValue,
            ["updated_roles"] = new JsonArray(updatedRoles.Select(role => JsonValue.Create(role)).ToArray())
        };
        File.WriteAllText(evidencePath, evidence.ToJsonString(JsonOptions));
        return Task.FromResult(EnterpriseWorkingDispositionResult.Succeeded(
            "Enterprise working-layer disposition was recorded.",
            evidencePath));
    }

    private static void UpdateJsonStore(
        string targetPath,
        string layerRole,
        string scopeField,
        string scopeValue,
        ComputeReviewDispositionRequest request,
        string decidedAtUtc)
    {
        var document = LoadTargetDocument(targetPath);
        var matched = document.Records.Where(record =>
            (string.IsNullOrWhiteSpace(record.LayerRole) || string.Equals(record.LayerRole, layerRole, StringComparison.OrdinalIgnoreCase))
            && record.Scope.TryGetPropertyValue(scopeField, out var valueNode)
            && string.Equals(valueNode?.GetValue<string>(), scopeValue, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matched.Length == 0)
        {
            throw new InvalidOperationException($"No transaction-scoped rows were found for {scopeField}={scopeValue}.");
        }

        foreach (var storeRecord in matched)
        {
            if (storeRecord.Payload["records"] is JsonArray records)
            {
                foreach (var record in records.OfType<JsonObject>())
                {
                    ApplyDisposition(record, request, decidedAtUtc);
                }
            }
            else
            {
                ApplyDisposition(storeRecord.Payload, request, decidedAtUtc);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? ".");
        File.WriteAllText(targetPath, JsonSerializer.Serialize(document, JsonOptions));
    }

    private static void ApplyDisposition(JsonObject record, ComputeReviewDispositionRequest request, string decidedAtUtc)
    {
        var caseStatus = request.Decision == ComputeReviewDecision.Postponed ? "review_postponed" : "review_closed";
        record["review_decision"] = request.Decision.ToContractValue();
        record["review_decision_by"] = request.OperatorId ?? string.Empty;
        record["review_decision_utc"] = decidedAtUtc;
        record["review_comment"] = request.Comment ?? string.Empty;
        record["official_comparison_status"] = string.Empty;
        record["official_reference_ids"] = string.Empty;
        record["review_state"] = "final_review_decided";
        record["case_status"] = caseStatus;
    }

    private static EnterpriseWorkingLayerStoreDocument LoadTargetDocument(string targetPath)
    {
        if (!File.Exists(targetPath))
        {
            throw new FileNotFoundException("Enterprise working target was not found.", targetPath);
        }

        return JsonSerializer.Deserialize<EnterpriseWorkingLayerStoreDocument>(File.ReadAllText(targetPath), JsonOptions)
            ?? new EnterpriseWorkingLayerStoreDocument();
    }

    private static string? ResolveScopeValue(ManifestDocument manifest, string scopeField)
    {
        var transaction = manifest.Payload.InnolaTransaction;
        return scopeField.Trim().ToLowerInvariant() switch
        {
            "transaction_id" => transaction?.TransactionId ?? manifest.TransactionId,
            "task_id" => transaction?.TaskId,
            "case_id" => transaction?.TransactionNumber ?? manifest.TransactionId,
            _ => transaction?.TransactionNumber ?? manifest.TransactionId
        };
    }

    private sealed class EnterpriseWorkingLayerStoreDocument
    {
        public List<EnterpriseWorkingLayerStoreRecord> Records { get; set; } = [];
    }

    private sealed class EnterpriseWorkingLayerStoreRecord
    {
        public string LayerRole { get; set; } = string.Empty;

        public string SavedAt { get; set; } = string.Empty;

        public JsonObject Scope { get; set; } = [];

        public JsonObject Payload { get; set; } = [];
    }
}
