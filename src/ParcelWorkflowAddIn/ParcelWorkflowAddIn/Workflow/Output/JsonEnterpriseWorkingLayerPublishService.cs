using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Workflow.Output;

public sealed class JsonEnterpriseWorkingLayerPublishService : IEnterpriseWorkingLayerPublishService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly Func<InnolaTransactionSettings> getSettings;

    public JsonEnterpriseWorkingLayerPublishService()
        : this(InnolaTransactionSettings.Load)
    {
    }

    internal JsonEnterpriseWorkingLayerPublishService(Func<InnolaTransactionSettings> getSettings)
    {
        this.getSettings = getSettings;
    }

    public Task<EnterpriseWorkingLayerPublishResult> PublishAsync(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary,
        string? operatorId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = getSettings();
        if (!string.Equals(settings.ReviewWorkspaceMode, InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(EnterpriseWorkingLayerPublishResult.Skipped("Enterprise working layers mode is not active for this case."));
        }

        var reviewSettings = settings.EnterpriseWorkingReview;
        if (!reviewSettings.Enabled)
        {
            return Task.FromResult(WriteFailureSummary(
                layout,
                manifest,
                outputSummary,
                operatorId,
                reviewSettings.TransactionScopeField,
                "Enterprise working layers mode is configured, but enterprise working review is disabled.",
                warnings: new[] { reviewSettings.Warning ?? "Enable enterprise_working_review to publish shared review geometry." },
                errors: Array.Empty<string>()));
        }

        var missingTargets = GetMissingRequiredTargets(reviewSettings);
        if (missingTargets.Count > 0)
        {
            return Task.FromResult(WriteFailureSummary(
                layout,
                manifest,
                outputSummary,
                operatorId,
                reviewSettings.TransactionScopeField,
                "Enterprise working-layer publish is missing required layer targets.",
                warnings: Array.Empty<string>(),
                errors: missingTargets));
        }

        var transaction = manifest.Payload.InnolaTransaction;
        var transactionScopeField = string.IsNullOrWhiteSpace(reviewSettings.TransactionScopeField)
            ? "transaction_number"
            : reviewSettings.TransactionScopeField;
        var transactionScopeValue = ResolveScopeValue(manifest, transactionScopeField);
        if (string.IsNullOrWhiteSpace(transactionScopeValue))
        {
            return Task.FromResult(WriteFailureSummary(
                layout,
                manifest,
                outputSummary,
                operatorId,
                transactionScopeField,
                "Enterprise working-layer publish could not resolve the configured transaction scope value.",
                warnings: Array.Empty<string>(),
                errors: new[] { $"The scope field '{transactionScopeField}' does not map to a value from the current manifest." }));
        }

        var approvedReview = TryLoadApprovedReview(Path.Combine(layout.WorkingDirectory, "approved_review.json"));
        var reviewRows = approvedReview?.Rows ?? [];
        var publishTime = DateTimeOffset.UtcNow;
        var localOnlyArtifacts = BuildLocalOnlyArtifacts(layout, outputSummary);
        var publishedLayers = new List<EnterpriseWorkingPublishedLayer>();

        try
        {
            var warnings = new List<string>();
            warnings.AddRange(BuildReviewWarnings(approvedReview, outputSummary));

            publishedLayers.Add(PublishLayer(
                reviewSettings.Layers.Points!,
                "points",
                transactionScopeField,
                transactionScopeValue,
                BuildPointPayload(reviewRows, manifest, outputSummary)));

            publishedLayers.Add(PublishLayer(
                reviewSettings.Layers.Lines!,
                "lines",
                transactionScopeField,
                transactionScopeValue,
                BuildLinePayload(reviewRows, manifest, outputSummary)));

            publishedLayers.Add(PublishLayer(
                reviewSettings.Layers.Polygons!,
                "polygons",
                transactionScopeField,
                transactionScopeValue,
                BuildPolygonPayload(reviewRows, manifest, outputSummary)));

            var summary = BuildSummary(
                status: "published",
                message: "Approved review geometry was published to enterprise working-layer stores.",
                manifest,
                outputSummary,
                operatorId,
                transactionScopeField,
                transactionScopeValue,
                publishTime,
                publishedLayers,
                localOnlyArtifacts,
                warnings,
                Array.Empty<string>());

            var summaryPath = WriteSummary(layout, summary);
            return Task.FromResult(EnterpriseWorkingLayerPublishResult.Succeeded(summary.Message, summaryPath, summary));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or NotSupportedException)
        {
            return Task.FromResult(WriteFailureSummary(
                layout,
                manifest,
                outputSummary,
                operatorId,
                transactionScopeField,
                "Enterprise working-layer publish failed. Local output artifacts remain available.",
                warnings: Array.Empty<string>(),
                errors: new[] { exception.Message }));
        }
    }

    private static EnterpriseWorkingLayerPublishResult WriteFailureSummary(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary,
        string? operatorId,
        string transactionScopeField,
        string message,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> errors)
    {
        var transactionScopeValue = ResolveScopeValue(manifest, transactionScopeField);
        var summary = BuildSummary(
            status: "failed",
            message,
            manifest,
            outputSummary,
            operatorId,
            transactionScopeField,
            transactionScopeValue ?? string.Empty,
            DateTimeOffset.UtcNow,
            [],
            BuildLocalOnlyArtifacts(layout, outputSummary),
            warnings,
            errors);
        var summaryPath = WriteSummary(layout, summary);
        return EnterpriseWorkingLayerPublishResult.Failed(message, summaryPath, summary);
    }

    private static EnterpriseWorkingPublishSummary BuildSummary(
        string status,
        string message,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary,
        string? operatorId,
        string transactionScopeField,
        string transactionScopeValue,
        DateTimeOffset publishTime,
        IReadOnlyList<EnterpriseWorkingPublishedLayer> publishedLayers,
        IReadOnlyList<string> localOnlyArtifacts,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> errors)
    {
        var transaction = manifest.Payload.InnolaTransaction;
        return new EnterpriseWorkingPublishSummary(
            status,
            message,
            publishTime.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            operatorId,
            transactionScopeField,
            transactionScopeValue,
            manifest.Payload.WorkflowProfile ?? transaction?.ProcessStep ?? "parcel_workflow",
            WorkflowState.SpatialReviewPending.ToContractValue(),
            manifest.TransactionId,
            transaction?.TransactionNumber ?? manifest.TransactionId,
            transaction?.TaskId,
            transaction?.TaskName ?? transaction?.CaseType,
            transaction?.AssignedUser,
            transaction?.AssignedGroup,
            publishTime.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            publishedLayers,
            localOnlyArtifacts,
            warnings,
            errors);
    }

    private static string WriteSummary(CaseFolderLayout layout, EnterpriseWorkingPublishSummary summary)
    {
        Directory.CreateDirectory(layout.OutputDirectory);
        var summaryPath = Path.Combine(layout.OutputDirectory, "enterprise_working_publish.json");
        File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, JsonOptions));
        return summaryPath;
    }

    private static IReadOnlyList<string> BuildLocalOnlyArtifacts(CaseFolderLayout layout, OutputSummaryDocument outputSummary)
    {
        var paths = new List<string>();
            var approvedReviewPath = Path.Combine(layout.WorkingDirectory, "approved_review.json");
            if (File.Exists(approvedReviewPath))
            {
                paths.Add(approvedReviewPath);
            }

        foreach (var path in outputSummary.Payload.ArtifactPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }

        if (!string.IsNullOrWhiteSpace(outputSummary.Payload.ResultGdbPath))
        {
            paths.Add(outputSummary.Payload.ResultGdbPath);
        }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static List<string> GetMissingRequiredTargets(EnterpriseWorkingReviewSettings settings)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(settings.Layers.Points))
        {
            missing.Add("Points working layer target is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.Layers.Lines))
        {
            missing.Add("Lines working layer target is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.Layers.Polygons))
        {
            missing.Add("Polygons working layer target is not configured.");
        }

        return missing;
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

    private static ExtractionReviewDocument? TryLoadApprovedReview(string approvedReviewPath)
    {
        if (!File.Exists(approvedReviewPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ExtractionReviewDocument>(File.ReadAllText(approvedReviewPath), JsonOptions);
    }

    private static IReadOnlyList<string> BuildReviewWarnings(ExtractionReviewDocument? approvedReview, OutputSummaryDocument outputSummary)
    {
        var warnings = new List<string>();
        if (approvedReview is null)
        {
            warnings.Add("Approved review data was not found. Working-layer publish used output summary artifacts only.");
        }

        if (outputSummary.Payload.PointCount == 0)
        {
            warnings.Add("Output summary reported zero point features.");
        }

        if (outputSummary.Payload.LineCount == 0)
        {
            warnings.Add("Output summary reported zero line features.");
        }

        if (outputSummary.Payload.PolygonCount == 0)
        {
            warnings.Add("Output summary reported zero polygon features.");
        }

        return warnings;
    }

    private static EnterpriseWorkingPublishedLayer PublishLayer(
        string targetPath,
        string layerRole,
        string transactionScopeField,
        string transactionScopeValue,
        JsonObject payload)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? ".");
        var document = LoadTargetDocument(targetPath);
        var removedExisting = document.Records.RemoveAll(record =>
            record.Scope.TryGetPropertyValue(transactionScopeField, out var valueNode)
            && string.Equals(valueNode?.GetValue<string>(), transactionScopeValue, StringComparison.OrdinalIgnoreCase));

        document.Records.Add(new EnterpriseWorkingLayerStoreRecord
        {
            Scope = new JsonObject
            {
                [transactionScopeField] = transactionScopeValue
            },
            LayerRole = layerRole,
            Payload = payload,
            SavedAt = DateTimeOffset.UtcNow.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
        });

        File.WriteAllText(targetPath, JsonSerializer.Serialize(document, JsonOptions));
        var recordCount = ExtractPayloadCount(payload);
        return new EnterpriseWorkingPublishedLayer(layerRole, targetPath, recordCount, removedExisting > 0);
    }

    private static EnterpriseWorkingLayerStoreDocument LoadTargetDocument(string targetPath)
    {
        if (!File.Exists(targetPath))
        {
            return new EnterpriseWorkingLayerStoreDocument();
        }

        return JsonSerializer.Deserialize<EnterpriseWorkingLayerStoreDocument>(File.ReadAllText(targetPath), JsonOptions)
            ?? new EnterpriseWorkingLayerStoreDocument();
    }

    private static int ExtractPayloadCount(JsonObject payload)
    {
        return payload["records"] is JsonArray records ? records.Count : 0;
    }

    private static JsonObject BuildPointPayload(
        IReadOnlyList<ExtractionReviewRow> rows,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary)
    {
        var records = new JsonArray();
        foreach (var row in rows)
        {
            var node = new JsonObject
            {
                ["row_id"] = row.RowId,
                ["point_id"] = row.PointIdentifier,
                ["easting"] = row.Easting,
                ["northing"] = row.Northing,
                ["status_txt"] = row.ExtractionStatus,
                ["length_txt"] = row.Length,
                ["source_txt"] = Truncate(row.SourceEvidence, 1024),
                ["review_note"] = Truncate(row.ReviewNotes, 512),
                ["is_manual"] = row.IsManual,
                ["is_edited"] = row.IsEdited,
                ["unresolved"] = row.Unresolved,
                ["row_provenance"] = row.RowProvenance
            };
            records.Add(node);
        }

        return BuildLayerEnvelope("points", manifest, outputSummary, records);
    }

    private static JsonObject BuildLinePayload(
        IReadOnlyList<ExtractionReviewRow> rows,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary)
    {
        var records = new JsonArray();
        foreach (var row in rows.Where(HasLineSignals))
        {
            var raw = row.RawRow;
            var node = new JsonObject
            {
                ["row_id"] = row.RowId,
                ["segment_id"] = ReadRawString(raw, "segment_no") ?? row.RowId,
                ["start_pt"] = ReadRawString(raw, "from_point") ?? string.Empty,
                ["end_pt"] = ReadRawString(raw, "to_point") ?? string.Empty,
                ["length_txt"] = row.Length,
                ["bearing_txt"] = ReadRawString(raw, "bearing") ?? ReadRawString(raw, "course") ?? string.Empty,
                ["seg_index"] = ReadRawString(raw, "segment_no") ?? string.Empty,
                ["source_txt"] = Truncate(row.SourceEvidence, 1024)
            };
            records.Add(node);
        }

        if (records.Count == 0 && !string.IsNullOrWhiteSpace(outputSummary.Payload.LineFeatureClassPath))
        {
            records.Add(new JsonObject
            {
                ["source_feature_class_path"] = outputSummary.Payload.LineFeatureClassPath,
                ["line_count"] = outputSummary.Payload.LineCount
            });
        }

        return BuildLayerEnvelope("lines", manifest, outputSummary, records);
    }

    private static JsonObject BuildPolygonPayload(
        IReadOnlyList<ExtractionReviewRow> rows,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary)
    {
        var records = new JsonArray
        {
            new JsonObject
            {
                ["parcel_name"] = ReadTransactionType(manifest),
                ["parcel_number"] = manifest.Payload.InnolaTransaction?.TransactionNumber ?? manifest.TransactionId,
                ["parcel_type"] = manifest.Payload.InnolaTransaction?.CaseType ?? string.Empty,
                ["source_txt"] = Truncate(string.Join("; ", rows.Select(row => row.SourceEvidence).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase)), 1024),
                ["polygon_count"] = outputSummary.Payload.PolygonCount,
                ["source_feature_class_path"] = outputSummary.Payload.PolygonFeatureClassPath ?? string.Empty
            }
        };

        return BuildLayerEnvelope("polygons", manifest, outputSummary, records);
    }

    private static JsonObject BuildLayerEnvelope(
        string layerRole,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary,
        JsonArray records)
    {
        var transaction = manifest.Payload.InnolaTransaction;
        return new JsonObject
        {
            ["layer_role"] = layerRole,
            ["transaction_id"] = manifest.TransactionId,
            ["transaction_number"] = transaction?.TransactionNumber ?? manifest.TransactionId,
            ["task_id"] = transaction?.TaskId ?? string.Empty,
            ["workflow_name"] = manifest.Payload.WorkflowProfile ?? transaction?.ProcessStep ?? "parcel_workflow",
            ["workflow_stage"] = WorkflowState.SpatialReviewPending.ToContractValue(),
            ["transaction_type"] = ReadTransactionType(manifest),
            ["assigned_user"] = transaction?.AssignedUser ?? string.Empty,
            ["assigned_group"] = transaction?.AssignedGroup ?? string.Empty,
            ["last_saved_utc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            ["result_gdb_path"] = outputSummary.Payload.ResultGdbPath ?? string.Empty,
            ["records"] = records
        };
    }

    private static string ReadTransactionType(ManifestDocument manifest)
    {
        var transaction = manifest.Payload.InnolaTransaction;
        return transaction?.TaskName
            ?? transaction?.CaseType
            ?? manifest.Payload.WorkflowProfile
            ?? "Parcel Workflow";
    }

    private static bool HasLineSignals(ExtractionReviewRow row)
    {
        return ReadRawString(row.RawRow, "segment_no") is not null
            || ReadRawString(row.RawRow, "segment_type") is not null
            || ReadRawString(row.RawRow, "from_point") is not null
            || ReadRawString(row.RawRow, "to_point") is not null;
    }

    private static string? ReadRawString(JsonObject raw, string propertyName)
    {
        return raw.TryGetPropertyValue(propertyName, out var node)
            ? node?.GetValue<string>()
            : null;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
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
