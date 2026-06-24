using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Workflow.Output;

public sealed class JsonEnterpriseParcelFabricPublishService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly Func<InnolaTransactionSettings> getSettings;

    public JsonEnterpriseParcelFabricPublishService()
        : this(InnolaTransactionSettings.Load)
    {
    }

    internal JsonEnterpriseParcelFabricPublishService(Func<InnolaTransactionSettings> getSettings)
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
        if (!string.Equals(settings.ReviewWorkspaceMode, InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(EnterpriseWorkingLayerPublishResult.Skipped("Enterprise Parcel Fabric mode is not active for this case."));
        }

        var reviewSettings = settings.EnterpriseParcelFabricReview;
        if (!reviewSettings.Enabled)
        {
            return Task.FromResult(WriteFailureSummary(
                layout,
                manifest,
                outputSummary,
                operatorId,
                reviewSettings.TransactionScopeField,
                "Enterprise Parcel Fabric mode is configured, but enterprise parcel fabric review is disabled.",
                warnings: new[] { reviewSettings.Warning ?? "Enable enterprise_parcel_fabric_review to publish shared parcel-review geometry." },
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
                "Enterprise Parcel Fabric publish is missing required targets.",
                warnings: Array.Empty<string>(),
                errors: missingTargets));
        }

        var transactionScopeField = string.IsNullOrWhiteSpace(reviewSettings.TransactionScopeField)
            ? EnterpriseParcelFabricReviewSettings.Default.TransactionScopeField
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
                "Enterprise Parcel Fabric publish could not resolve the configured transaction scope value.",
                warnings: Array.Empty<string>(),
                errors: new[] { $"The scope field '{transactionScopeField}' does not map to a value from the current manifest." }));
        }

        var approvedReview = TryLoadApprovedReview(Path.Combine(layout.WorkingDirectory, "approved_review.json"));
        var reviewRows = approvedReview?.Rows ?? [];
        var publishTime = DateTimeOffset.UtcNow;
        var localOnlyArtifacts = BuildLocalOnlyArtifacts(layout, outputSummary);
        var publishedLayers = new List<EnterpriseWorkingPublishedLayer>();
        var warnings = new List<string>();
        warnings.AddRange(BuildReviewWarnings(approvedReview, outputSummary));
        if (!string.IsNullOrWhiteSpace(reviewSettings.Warning))
        {
            warnings.Add(reviewSettings.Warning);
        }

        try
        {
            var recordName = ResolveRecordName(reviewSettings.RecordNamePattern, manifest, transactionScopeValue);
            var reviewState = WorkflowState.SpatialReviewPending.ToContractValue();

            publishedLayers.Add(PublishRecordStore(
                reviewSettings.RecordsLayerUrl!,
                transactionScopeField,
                transactionScopeValue,
                BuildRecordPayload(recordName, reviewSettings, manifest, outputSummary, reviewState, operatorId, publishTime)));

            publishedLayers.Add(PublishRecordStore(
                reviewSettings.FabricLayerUrl!,
                transactionScopeField,
                transactionScopeValue,
                BuildFabricPayload(recordName, reviewSettings, manifest, outputSummary, reviewState, operatorId, publishTime, reviewRows)));

            if (!string.IsNullOrWhiteSpace(reviewSettings.ParcelLayerUrl))
            {
                publishedLayers.Add(PublishRecordStore(
                    reviewSettings.ParcelLayerUrl!,
                    transactionScopeField,
                    transactionScopeValue,
                    BuildParcelPayload(recordName, reviewSettings, manifest, outputSummary, reviewState, operatorId, publishTime, reviewRows)));
            }

            var summary = BuildSummary(
                status: "published",
                message: "Validated review geometry was published to the configured Enterprise Parcel Fabric working contract.",
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
                "Enterprise Parcel Fabric publish failed. Local output artifacts remain available.",
                warnings,
                new[] { exception.Message }));
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
        var summaryPath = Path.Combine(layout.OutputDirectory, "enterprise_parcel_fabric_publish.json");
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

    private static List<string> GetMissingRequiredTargets(EnterpriseParcelFabricReviewSettings settings)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(settings.FabricLayerUrl))
        {
            missing.Add("Fabric layer target is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.RecordsLayerUrl))
        {
            missing.Add("Records layer target is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.ParcelTypeName))
        {
            missing.Add("Parcel type name is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.RecordNamePattern))
        {
            missing.Add("Record name pattern is not configured.");
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
            warnings.Add("Approved review data was not found. Enterprise Parcel Fabric publish used output summary artifacts only.");
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

    private static EnterpriseWorkingPublishedLayer PublishRecordStore(
        string targetPath,
        string transactionScopeField,
        string transactionScopeValue,
        JsonObject payload)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? ".");
        var document = LoadTargetDocument(targetPath);
        var removedExisting = document.Records.RemoveAll(record =>
            record.Scope.TryGetPropertyValue(transactionScopeField, out var valueNode)
            && string.Equals(valueNode?.GetValue<string>(), transactionScopeValue, StringComparison.OrdinalIgnoreCase));

        document.Records.Add(new EnterpriseParcelFabricStoreRecord
        {
            SavedAt = DateTimeOffset.UtcNow.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            Scope = new JsonObject
            {
                [transactionScopeField] = transactionScopeValue
            },
            Payload = payload
        });

        File.WriteAllText(targetPath, JsonSerializer.Serialize(document, JsonOptions));
        var recordCount = ExtractPayloadCount(payload);
        var role = payload["layer_role"]?.GetValue<string>() ?? "parcel_fabric";
        return new EnterpriseWorkingPublishedLayer(role, targetPath, recordCount, removedExisting > 0);
    }

    private static EnterpriseParcelFabricStoreDocument LoadTargetDocument(string targetPath)
    {
        if (!File.Exists(targetPath))
        {
            return new EnterpriseParcelFabricStoreDocument();
        }

        return JsonSerializer.Deserialize<EnterpriseParcelFabricStoreDocument>(File.ReadAllText(targetPath), JsonOptions)
            ?? new EnterpriseParcelFabricStoreDocument();
    }

    private static int ExtractPayloadCount(JsonObject payload)
    {
        if (payload["records"] is JsonArray records)
        {
            return records.Count;
        }

        if (payload["parcels"] is JsonArray parcels)
        {
            return parcels.Count;
        }

        return 0;
    }

    private static JsonObject BuildRecordPayload(
        string recordName,
        EnterpriseParcelFabricReviewSettings settings,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary,
        string reviewState,
        string? operatorId,
        DateTimeOffset publishTime)
    {
        var transaction = manifest.Payload.InnolaTransaction;
        var records = new JsonArray
        {
            new JsonObject
            {
                ["record_name"] = recordName,
                ["parcel_type"] = settings.ParcelTypeName,
                ["transaction_id"] = transaction?.TransactionId ?? manifest.TransactionId,
                ["transaction_number"] = transaction?.TransactionNumber ?? manifest.TransactionId,
                ["review_state"] = reviewState,
                ["published_by"] = operatorId ?? string.Empty,
                ["published_at"] = publishTime.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                ["point_count"] = outputSummary.Payload.PointCount,
                ["line_count"] = outputSummary.Payload.LineCount,
                ["polygon_count"] = outputSummary.Payload.PolygonCount
            }
        };

        return new JsonObject
        {
            ["layer_role"] = "records",
            ["record_name"] = recordName,
            ["records"] = records
        };
    }

    private static JsonObject BuildFabricPayload(
        string recordName,
        EnterpriseParcelFabricReviewSettings settings,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary,
        string reviewState,
        string? operatorId,
        DateTimeOffset publishTime,
        IReadOnlyList<ExtractionReviewRow> rows)
    {
        var transaction = manifest.Payload.InnolaTransaction;
        var parcelGroups = GroupRowsByParcel(rows, outputSummary);
        var parcels = new JsonArray();
        foreach (var group in parcelGroups)
        {
            parcels.Add(new JsonObject
            {
                ["parcel_id"] = group.ParcelId,
                ["parcel_name"] = group.ParcelName,
                ["parcel_type"] = settings.ParcelTypeName,
                ["record_name"] = recordName,
                ["point_count"] = group.PointCount,
                ["line_count"] = group.LineCount,
                ["polygon_count"] = group.PolygonCount
            });
        }

        return new JsonObject
        {
            ["layer_role"] = "fabric",
            ["record_name"] = recordName,
            ["parcel_type"] = settings.ParcelTypeName,
            ["transaction_id"] = transaction?.TransactionId ?? manifest.TransactionId,
            ["transaction_number"] = transaction?.TransactionNumber ?? manifest.TransactionId,
            ["review_state"] = reviewState,
            ["published_by"] = operatorId ?? string.Empty,
            ["published_at"] = publishTime.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            ["build_behavior"] = settings.BuildBehavior,
            ["result_gdb_path"] = outputSummary.Payload.ResultGdbPath ?? string.Empty,
            ["parcels"] = parcels
        };
    }

    private static JsonObject BuildParcelPayload(
        string recordName,
        EnterpriseParcelFabricReviewSettings settings,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary,
        string reviewState,
        string? operatorId,
        DateTimeOffset publishTime,
        IReadOnlyList<ExtractionReviewRow> rows)
    {
        var transaction = manifest.Payload.InnolaTransaction;
        var parcelGroups = GroupRowsByParcel(rows, outputSummary);
        var records = new JsonArray();
        foreach (var group in parcelGroups)
        {
            var points = new JsonArray();
            foreach (var row in group.Rows)
            {
                points.Add(new JsonObject
                {
                    ["row_id"] = row.RowId,
                    ["point_id"] = row.PointIdentifier,
                    ["easting"] = row.Easting,
                    ["northing"] = row.Northing,
                    ["status_txt"] = row.ExtractionStatus,
                    ["length_txt"] = row.Length,
                    ["review_note"] = Truncate(row.ReviewNotes, 512),
                    ["source_txt"] = Truncate(row.SourceEvidence, 1024),
                    ["is_manual"] = row.IsManual,
                    ["is_edited"] = row.IsEdited
                });
            }

            records.Add(new JsonObject
            {
                ["parcel_id"] = group.ParcelId,
                ["parcel_name"] = group.ParcelName,
                ["record_name"] = recordName,
                ["parcel_type"] = settings.ParcelTypeName,
                ["points"] = points
            });
        }

        return new JsonObject
        {
            ["layer_role"] = "parcels",
            ["record_name"] = recordName,
            ["parcel_type"] = settings.ParcelTypeName,
            ["transaction_id"] = transaction?.TransactionId ?? manifest.TransactionId,
            ["transaction_number"] = transaction?.TransactionNumber ?? manifest.TransactionId,
            ["review_state"] = reviewState,
            ["published_by"] = operatorId ?? string.Empty,
            ["published_at"] = publishTime.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            ["result_gdb_path"] = outputSummary.Payload.ResultGdbPath ?? string.Empty,
            ["records"] = records
        };
    }

    private static IReadOnlyList<ParcelGroupSummary> GroupRowsByParcel(IReadOnlyList<ExtractionReviewRow> rows, OutputSummaryDocument outputSummary)
    {
        var usableRows = rows
            .Where(row => !row.Unresolved && !string.IsNullOrWhiteSpace(row.PointIdentifier))
            .ToArray();
        if (usableRows.Length == 0)
        {
            return
            [
                new ParcelGroupSummary(
                    outputSummary.Payload.ParcelType ?? "compute_review",
                    outputSummary.Payload.ParcelType ?? "compute_review",
                    Array.Empty<ExtractionReviewRow>(),
                    outputSummary.Payload.PointCount,
                    outputSummary.Payload.LineCount,
                    outputSummary.Payload.PolygonCount)
            ];
        }

        return usableRows
            .GroupBy(
                row => string.IsNullOrWhiteSpace(row.ParcelName) ? outputSummary.Payload.ParcelType ?? "compute_review" : row.ParcelName!,
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                new ParcelGroupSummary(
                    group.Key,
                    group.Key,
                    group.ToArray(),
                    group.Count(),
                    Math.Max(group.Count() - 1, 0),
                    1))
            .ToArray();
    }

    private static string ResolveRecordName(string pattern, ManifestDocument manifest, string transactionScopeValue)
    {
        var transaction = manifest.Payload.InnolaTransaction;
        var resolved = string.IsNullOrWhiteSpace(pattern)
            ? EnterpriseParcelFabricReviewSettings.Default.RecordNamePattern
            : pattern;
        return resolved
            .Replace("{transaction_number}", transaction?.TransactionNumber ?? transactionScopeValue, StringComparison.OrdinalIgnoreCase)
            .Replace("{transaction_id}", transaction?.TransactionId ?? manifest.TransactionId, StringComparison.OrdinalIgnoreCase)
            .Replace("{task_id}", transaction?.TaskId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
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

    private sealed record ParcelGroupSummary(
        string ParcelId,
        string ParcelName,
        IReadOnlyList<ExtractionReviewRow> Rows,
        int PointCount,
        int LineCount,
        int PolygonCount);

    private sealed class EnterpriseParcelFabricStoreDocument
    {
        public List<EnterpriseParcelFabricStoreRecord> Records { get; set; } = [];
    }

    private sealed class EnterpriseParcelFabricStoreRecord
    {
        public string SavedAt { get; set; } = string.Empty;

        public JsonObject Scope { get; set; } = [];

        public JsonObject Payload { get; set; } = [];
    }
}
