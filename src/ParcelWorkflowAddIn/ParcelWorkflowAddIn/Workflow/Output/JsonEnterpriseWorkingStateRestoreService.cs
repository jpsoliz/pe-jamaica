using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow;

namespace ParcelWorkflowAddIn.Workflow.Output;

public sealed class JsonEnterpriseWorkingStateRestoreService : IEnterpriseWorkingStateRestoreService
{
    private const string SnapshotArtifactFileName = "enterprise_working_restore.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly Func<InnolaTransactionSettings> getSettings;

    public JsonEnterpriseWorkingStateRestoreService()
        : this(InnolaTransactionSettings.Load)
    {
    }

    internal JsonEnterpriseWorkingStateRestoreService(Func<InnolaTransactionSettings> getSettings)
    {
        this.getSettings = getSettings;
    }

    public EnterpriseWorkingStateRestoreResult Restore(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        WorkflowState resolvedState,
        OutputSummaryDocument? localOutputSummary)
    {
        var hasLocalOutputState = localOutputSummary is not null;
        if (!ShouldCheckEnterprise(resolvedState, hasLocalOutputState))
        {
            return EnterpriseWorkingStateRestoreResult.Skipped(
                hasLocalOutputState
                    ? EnterpriseWorkingStateRestoreResult.RestoreSourceLocalOnly
                    : EnterpriseWorkingStateRestoreResult.RestoreSourceNone,
                hasLocalOutputState ? "Case reopened from local saved state." : "Case reopened");
        }

        var settings = getSettings();
        if (!string.Equals(settings.ReviewWorkspaceMode, InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers, StringComparison.OrdinalIgnoreCase)
            || !settings.EnterpriseWorkingReview.Enabled
            || string.Equals(settings.EnterpriseWorkingReview.RestoreBehavior, EnterpriseWorkingReviewSettings.RestoreBehaviorLocalOnly, StringComparison.OrdinalIgnoreCase))
        {
            return EnterpriseWorkingStateRestoreResult.Skipped(
                hasLocalOutputState
                    ? EnterpriseWorkingStateRestoreResult.RestoreSourceLocalOnly
                    : EnterpriseWorkingStateRestoreResult.RestoreSourceNone,
                hasLocalOutputState ? "Case reopened from local saved state." : "Case reopened");
        }

        var scopeField = string.IsNullOrWhiteSpace(settings.EnterpriseWorkingReview.TransactionScopeField)
            ? "transaction_number"
            : settings.EnterpriseWorkingReview.TransactionScopeField;
        var scopeValue = ResolveScopeValue(manifest, scopeField);
        if (string.IsNullOrWhiteSpace(scopeValue))
        {
            return new EnterpriseWorkingStateRestoreResult(
                true,
                false,
                hasLocalOutputState
                    ? EnterpriseWorkingStateRestoreResult.RestoreSourceLocalOnly
                    : EnterpriseWorkingStateRestoreResult.RestoreSourceNone,
                hasLocalOutputState
                    ? "Case reopened from local saved state. Enterprise working state could not be matched to this transaction."
                    : "Case reopened. Enterprise working state could not be matched to this transaction.",
                null,
                Array.Empty<AvailableArtifact>(),
                new[]
                {
                    new RecoverabilityIssue(
                        "enterprise_restore_scope_unresolved",
                        "warning",
                        $"Enterprise working restore could not resolve '{scopeField}' for this transaction.",
                        layout.ManifestPath,
                        false)
                });
        }

        var issues = new List<RecoverabilityIssue>();
        var restoredLayers = new List<EnterpriseWorkingPublishedLayer>();
        var layerPayloads = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        var foundAnyEnterpriseState = false;

        var caseIndexRecord = LoadRecord(settings.EnterpriseWorkingReview.Layers.CaseIndex, "case_index", scopeField, scopeValue, issues);
        if (caseIndexRecord is not null)
        {
            foundAnyEnterpriseState = true;
        }

        foreach (var target in EnumerateGeometryTargets(settings.EnterpriseWorkingReview.Layers))
        {
            var record = LoadRecord(target.Path, target.Role, scopeField, scopeValue, issues);
            if (record is null)
            {
                issues.Add(new RecoverabilityIssue(
                    $"enterprise_restore_missing_{target.Role}",
                    "warning",
                    $"Enterprise working-state layer '{target.Role}' does not contain geometry for transaction {scopeValue}.",
                    target.Path,
                    false));
                continue;
            }

            foundAnyEnterpriseState = true;
            layerPayloads[target.Role] = record.Payload.DeepClone().AsObject();
            restoredLayers.Add(new EnterpriseWorkingPublishedLayer(
                target.Role,
                target.Path!,
                ExtractPayloadCount(record.Payload),
                false));
        }

        if (!foundAnyEnterpriseState)
        {
            var noEnterpriseStatus = hasLocalOutputState
                ? "Case reopened from local saved state. No enterprise working state was found for this transaction."
                : "Case reopened. No enterprise working state was found for this transaction.";
            return new EnterpriseWorkingStateRestoreResult(
                true,
                false,
                hasLocalOutputState
                    ? EnterpriseWorkingStateRestoreResult.RestoreSourceLocalOnly
                    : EnterpriseWorkingStateRestoreResult.RestoreSourceNone,
                noEnterpriseStatus,
                null,
                Array.Empty<AvailableArtifact>(),
                issues);
        }

        var snapshot = BuildSnapshot(
            manifest,
            resolvedState,
            scopeField,
            scopeValue,
            hasLocalOutputState,
            caseIndexRecord,
            restoredLayers,
            layerPayloads,
            issues);

        var snapshotPath = Path.Combine(layout.WorkingDirectory, SnapshotArtifactFileName);
        Directory.CreateDirectory(layout.WorkingDirectory);
        File.WriteAllText(snapshotPath, JsonSerializer.Serialize(snapshot, JsonOptions));
        var addedArtifacts = new[] { new AvailableArtifact(SnapshotArtifactFileName, snapshotPath) };

        var restoredOutputSummary = localOutputSummary;
        if (restoredOutputSummary is null)
        {
            restoredOutputSummary = BuildSyntheticOutputSummary(
                manifest,
                resolvedState,
                caseIndexRecord,
                restoredLayers,
                layerPayloads,
                snapshotPath);
        }
        else if (!restoredOutputSummary.Payload.ArtifactPaths.Contains(snapshotPath, StringComparer.OrdinalIgnoreCase))
        {
            restoredOutputSummary = restoredOutputSummary with
            {
                Payload = restoredOutputSummary.Payload with
                {
                    ArtifactPaths = restoredOutputSummary.Payload.ArtifactPaths
                        .Append(snapshotPath)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                }
            };
        }

        var restoreSource = hasLocalOutputState
            ? EnterpriseWorkingStateRestoreResult.RestoreSourceLocalAndEnterprise
            : EnterpriseWorkingStateRestoreResult.RestoreSourceEnterpriseOnly;
        var hasWarnings = issues.Count > 0;
        var statusMessage = restoreSource switch
        {
            EnterpriseWorkingStateRestoreResult.RestoreSourceLocalAndEnterprise when hasWarnings
                => "Case reopened. Some saved artifacts could not be restored - please review results. Local and enterprise working state were both detected.",
            EnterpriseWorkingStateRestoreResult.RestoreSourceLocalAndEnterprise
                => "Case reopened. Local and enterprise working state were restored.",
            EnterpriseWorkingStateRestoreResult.RestoreSourceEnterpriseOnly when hasWarnings
                => "Case reopened. Some saved artifacts could not be restored - please review results. Enterprise working state was restored for this transaction.",
            _ => "Case reopened from enterprise working state. Local output artifacts were not available on this machine."
        };

        return new EnterpriseWorkingStateRestoreResult(
            true,
            true,
            restoreSource,
            statusMessage,
            restoredOutputSummary,
            addedArtifacts,
            issues);
    }

    private static bool ShouldCheckEnterprise(WorkflowState resolvedState, bool hasLocalOutputState)
    {
        if (hasLocalOutputState)
        {
            return resolvedState is WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending or WorkflowState.SpatialReviewApproved;
        }

        return resolvedState is WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending or WorkflowState.SpatialReviewApproved;
    }

    private static IEnumerable<(string Role, string? Path)> EnumerateGeometryTargets(EnterpriseWorkingLayerTargets layers)
    {
        yield return ("points", layers.Points);
        yield return ("lines", layers.Lines);
        yield return ("polygons", layers.Polygons);
    }

    private static EnterpriseStoreRecord? LoadRecord(
        string? targetPath,
        string layerRole,
        string scopeField,
        string scopeValue,
        List<RecoverabilityIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return null;
        }

        if (!File.Exists(targetPath))
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<EnterpriseStoreDocument>(File.ReadAllText(targetPath), JsonOptions)
                ?? new EnterpriseStoreDocument();

            return document.Records.FirstOrDefault(record =>
                (string.IsNullOrWhiteSpace(record.LayerRole) || string.Equals(record.LayerRole, layerRole, StringComparison.OrdinalIgnoreCase))
                && record.Scope.TryGetPropertyValue(scopeField, out var scopeNode)
                && string.Equals(scopeNode?.GetValue<string>(), scopeValue, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException)
        {
            issues.Add(new RecoverabilityIssue(
                $"enterprise_restore_read_failed_{layerRole}",
                "warning",
                $"Enterprise working-state layer '{layerRole}' could not be read: {exception.Message}",
                targetPath,
                false));
            return null;
        }
    }

    private static EnterpriseWorkingRestoreSnapshot BuildSnapshot(
        ManifestDocument manifest,
        WorkflowState resolvedState,
        string scopeField,
        string scopeValue,
        bool localArtifactsPresent,
        EnterpriseStoreRecord? caseIndexRecord,
        IReadOnlyList<EnterpriseWorkingPublishedLayer> restoredLayers,
        IReadOnlyDictionary<string, JsonObject> layerPayloads,
        IReadOnlyList<RecoverabilityIssue> issues)
    {
        var layerRecords = new JsonArray();
        foreach (var layer in restoredLayers)
        {
            layerPayloads.TryGetValue(layer.LayerRole, out var payload);
            layerRecords.Add(new JsonObject
            {
                ["layer_role"] = layer.LayerRole,
                ["target"] = layer.Target,
                ["record_count"] = layer.RecordCount,
                ["payload"] = payload?.DeepClone()
            });
        }

        return new EnterpriseWorkingRestoreSnapshot(
            "1.0.0",
            manifest.TransactionId,
            manifest.Payload.InnolaTransaction?.TransactionNumber ?? manifest.TransactionId,
            DateTimeOffset.UtcNow.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            scopeField,
            scopeValue,
            resolvedState.ToContractValue(),
            localArtifactsPresent,
            caseIndexRecord?.Payload?.DeepClone().AsObject(),
            layerRecords,
            issues.Select(issue => issue.Message).ToArray());
    }

    private static OutputSummaryDocument BuildSyntheticOutputSummary(
        ManifestDocument manifest,
        WorkflowState resolvedState,
        EnterpriseStoreRecord? caseIndexRecord,
        IReadOnlyList<EnterpriseWorkingPublishedLayer> restoredLayers,
        IReadOnlyDictionary<string, JsonObject> layerPayloads,
        string snapshotPath)
    {
        var transaction = manifest.Payload.InnolaTransaction;
        var createdAt = DateTimeOffset.UtcNow.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var publishedAt = ReadPayloadString(caseIndexRecord?.Payload, "last_saved_utc") ?? createdAt;
        var publishedBy = ReadPayloadString(caseIndexRecord?.Payload, "saved_by")
            ?? transaction?.AssignedUser
            ?? transaction?.SelectedUser;
        var transactionScopeField = "transaction_number";
        var transactionScopeValue = transaction?.TransactionNumber ?? manifest.TransactionId;

        var publishSummary = new EnterpriseWorkingPublishSummary(
            "restored",
            "Enterprise working state was restored during reopen.",
            publishedAt,
            publishedBy,
            transactionScopeField,
            transactionScopeValue,
            ReadPayloadString(caseIndexRecord?.Payload, "workflow_name") ?? manifest.Payload.WorkflowProfile ?? "parcel_workflow",
            ReadPayloadString(caseIndexRecord?.Payload, "workflow_stage") ?? resolvedState.ToContractValue(),
            manifest.TransactionId,
            transaction?.TransactionNumber ?? manifest.TransactionId,
            transaction?.TaskId,
            transaction?.TaskName ?? transaction?.CaseType,
            transaction?.AssignedUser,
            transaction?.AssignedGroup,
            publishedAt,
            restoredLayers,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        return new OutputSummaryDocument(
            "1.0.0",
            manifest.TransactionId,
            $"restore-{Guid.NewGuid():N}",
            createdAt,
            publishedBy,
            string.Empty,
            new OutputSummaryPayload(
                "restored",
                InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers,
                null,
                new[] { snapshotPath },
                Array.Empty<string>(),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                0,
                0,
                ResolveCount(restoredLayers, "points", layerPayloads),
                ResolveCount(restoredLayers, "lines", layerPayloads),
                ResolveCount(restoredLayers, "polygons", layerPayloads),
                null,
                null,
                publishSummary),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static int ResolveCount(
        IReadOnlyList<EnterpriseWorkingPublishedLayer> restoredLayers,
        string role,
        IReadOnlyDictionary<string, JsonObject> layerPayloads)
    {
        if (restoredLayers.FirstOrDefault(layer => string.Equals(layer.LayerRole, role, StringComparison.OrdinalIgnoreCase)) is { } matchedLayer
            && matchedLayer.RecordCount > 0)
        {
            return matchedLayer.RecordCount;
        }

        return layerPayloads.TryGetValue(role, out var payload)
            ? ExtractPayloadCount(payload)
            : 0;
    }

    private static int ExtractPayloadCount(JsonObject payload)
    {
        return payload["records"] is JsonArray records ? records.Count : 0;
    }

    private static string? ReadPayloadString(JsonObject? payload, string propertyName)
    {
        return payload is not null && payload.TryGetPropertyValue(propertyName, out var node) && node is not null
            ? node.GetValue<string>()
            : null;
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

    private sealed class EnterpriseStoreDocument
    {
        public List<EnterpriseStoreRecord> Records { get; set; } = [];
    }

    private sealed class EnterpriseStoreRecord
    {
        public string LayerRole { get; set; } = string.Empty;

        public string SavedAt { get; set; } = string.Empty;

        public JsonObject Scope { get; set; } = [];

        public JsonObject Payload { get; set; } = [];
    }

    private sealed record EnterpriseWorkingRestoreSnapshot(
        string SchemaVersion,
        string TransactionId,
        string TransactionNumber,
        string RestoredAt,
        string ScopeField,
        string ScopeValue,
        string WorkflowState,
        bool LocalArtifactsPresent,
        JsonObject? CaseIndex,
        JsonArray Layers,
        IReadOnlyList<string> Warnings);
}
