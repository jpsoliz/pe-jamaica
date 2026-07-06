using System.Globalization;
using System.IO;
using System.Net.Http;
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
    private static readonly HttpClient HttpClient = new();
    private static readonly string[] RequiredDispositionFields =
    [
        "review_decision",
        "review_decision_by",
        "review_decision_utc",
        "review_comment",
        "official_comparison_status",
        "official_reference_ids",
        "review_state",
        "case_status"
    ];
    private static readonly string[] RequiredSpatialUnitReferenceFields =
    [
        "spatial_unit_id",
        "spatial_unit_api_status"
    ];

    private readonly Func<InnolaTransactionSettings> getSettings;

    public JsonEnterpriseWorkingDispositionService()
        : this(InnolaTransactionSettings.Load)
    {
    }

    internal JsonEnterpriseWorkingDispositionService(Func<InnolaTransactionSettings> getSettings)
    {
        this.getSettings = getSettings;
    }

    public async Task<EnterpriseWorkingDispositionResult> RecordDispositionAsync(
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
            return EnterpriseWorkingDispositionResult.Failed(
                "Enterprise disposition writeback could not resolve the transaction scope.",
                new[] { $"The scope field '{scopeField}' does not map to a value from the current manifest." });
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
                    await UpdateFeatureServiceLayerAsync(
                        target.Path,
                        target.Role,
                        scopeField,
                        scopeValue,
                        request,
                        decidedAtUtc,
                        cancellationToken).ConfigureAwait(false);
                    updatedRoles.Add(target.Role);
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
            return EnterpriseWorkingDispositionResult.Failed(
                $"Enterprise disposition writeback failed. {errors[0]}",
                errors);
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
        return EnterpriseWorkingDispositionResult.Succeeded(
            "Enterprise working-layer disposition was recorded.",
            evidencePath);
    }

    public async Task<EnterpriseWorkingDispositionResult> RecordSpatialUnitReferenceAsync(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        string? spatialUnitId,
        string spatialUnitApiStatus,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(spatialUnitId))
        {
            return EnterpriseWorkingDispositionResult.Succeeded(
                "Spatial Unit id was not returned; Enterprise case index reference writeback was skipped.",
                string.Empty);
        }

        var settings = getSettings();
        if (!string.Equals(settings.ReviewWorkspaceMode, InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers, StringComparison.OrdinalIgnoreCase)
            || !settings.EnterpriseWorkingReview.Enabled)
        {
            return EnterpriseWorkingDispositionResult.Succeeded(
                "Enterprise working layers mode is not active; Spatial Unit case index reference writeback was skipped.",
                string.Empty);
        }

        var reviewSettings = settings.EnterpriseWorkingReview;
        var caseIndexTarget = reviewSettings.Layers.CaseIndex;
        if (string.IsNullOrWhiteSpace(caseIndexTarget))
        {
            return EnterpriseWorkingDispositionResult.Failed(
                "Enterprise Spatial Unit reference writeback failed.",
                new[] { "Case index working table target is not configured." });
        }

        var scopeField = string.IsNullOrWhiteSpace(reviewSettings.TransactionScopeField)
            ? "transaction_number"
            : reviewSettings.TransactionScopeField;
        var scopeValue = ResolveScopeValue(manifest, scopeField);
        if (string.IsNullOrWhiteSpace(scopeValue))
        {
            return EnterpriseWorkingDispositionResult.Failed(
                "Enterprise Spatial Unit reference writeback failed.",
                new[] { $"The scope field '{scopeField}' does not map to a value from the current manifest." });
        }

        try
        {
            if (Uri.TryCreate(caseIndexTarget, UriKind.Absolute, out var uri)
                && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                await UpdateFeatureServiceSpatialUnitReferenceAsync(
                    caseIndexTarget,
                    scopeField,
                    scopeValue,
                    spatialUnitId,
                    spatialUnitApiStatus,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                UpdateJsonSpatialUnitReference(
                    caseIndexTarget,
                    scopeField,
                    scopeValue,
                    spatialUnitId,
                    spatialUnitApiStatus);
            }

            Directory.CreateDirectory(layout.WorkingDirectory);
            var evidencePath = Path.Combine(layout.WorkingDirectory, "enterprise_working_spatial_unit_reference.json");
            var evidence = new JsonObject
            {
                ["schema_version"] = "enterprise_working_spatial_unit_reference_v1",
                ["transaction_id"] = manifest.TransactionId,
                ["transaction_number"] = manifest.Payload.InnolaTransaction?.TransactionNumber ?? manifest.TransactionId,
                ["scope_field"] = scopeField,
                ["scope_value"] = scopeValue,
                ["spatial_unit_id"] = spatialUnitId,
                ["spatial_unit_api_status"] = spatialUnitApiStatus,
                ["written_at_utc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
            };
            File.WriteAllText(evidencePath, evidence.ToJsonString(JsonOptions));
            return EnterpriseWorkingDispositionResult.Succeeded(
                "Enterprise case index Spatial Unit reference was recorded.",
                evidencePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or NotSupportedException or HttpRequestException or TaskCanceledException)
        {
            return EnterpriseWorkingDispositionResult.Failed(
                "Enterprise Spatial Unit reference writeback failed.",
                new[] { exception.Message });
        }
    }

    private static async Task UpdateFeatureServiceLayerAsync(
        string targetUrl,
        string layerRole,
        string scopeField,
        string scopeValue,
        ComputeReviewDispositionRequest request,
        string decidedAtUtc,
        CancellationToken cancellationToken)
    {
        var token = GetPortalToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("ArcGIS Enterprise disposition writeback requires ARCGIS_PORTAL_TOKEN. Generate a fresh portal token, set ARCGIS_PORTAL_TOKEN for the ArcGIS Pro process, restart ArcGIS Pro if needed, then retry Finalize.");
        }

        var referer = GetArcGisReferer(targetUrl);
        var metadataForm = new Dictionary<string, string>
        {
            ["f"] = "json"
        };
        AddToken(metadataForm, token);
        var metadataResponse = await PostFormAsync(targetUrl.TrimEnd('/'), metadataForm, cancellationToken, referer).ConfigureAwait(false);
        EnsureArcGisDispositionSuccess(metadataResponse, "metadata", layerRole);
        var objectIdField = ReadJsonString(metadataResponse, "objectIdField")
            ?? ReadJsonString(metadataResponse, "objectIdFieldName")
            ?? "OBJECTID";
        EnsureRequiredFields(metadataResponse, layerRole);

        var where = $"{scopeField} = '{EscapeSqlLiteral(scopeValue)}'";
        var queryForm = new Dictionary<string, string>
        {
            ["f"] = "json",
            ["where"] = where,
            ["outFields"] = objectIdField,
            ["returnGeometry"] = "false"
        };
        AddToken(queryForm, token);
        var queryResponse = await PostFormAsync($"{targetUrl.TrimEnd('/')}/query", queryForm, cancellationToken, referer).ConfigureAwait(false);
        EnsureArcGisDispositionSuccess(queryResponse, "query", layerRole);

        if (queryResponse["features"] is not JsonArray features || features.Count == 0)
        {
            throw new InvalidOperationException($"{layerRole} disposition writeback found no rows for {scopeField}={scopeValue}.");
        }

        var updates = new JsonArray();
        foreach (var feature in features.OfType<JsonObject>())
        {
            if (feature["attributes"] is not JsonObject attributes
                || !attributes.TryGetPropertyValue(objectIdField, out var objectId)
                || objectId is null)
            {
                throw new InvalidOperationException($"{layerRole} disposition writeback query did not return {objectIdField}.");
            }

            var updateAttributes = new JsonObject
            {
                [objectIdField] = objectId.DeepClone()
            };
            ApplyDisposition(updateAttributes, request, decidedAtUtc, useArcGisDateValue: true);
            updates.Add(new JsonObject
            {
                ["attributes"] = updateAttributes
            });
        }

        var updateForm = new Dictionary<string, string>
        {
            ["f"] = "json",
            ["features"] = updates.ToJsonString(JsonOptions),
            ["rollbackOnFailure"] = "true"
        };
        AddToken(updateForm, token);
        var updateResponse = await PostFormAsync($"{targetUrl.TrimEnd('/')}/updateFeatures", updateForm, cancellationToken, referer).ConfigureAwait(false);
        EnsureArcGisDispositionSuccess(updateResponse, "updateFeatures", layerRole);
    }

    private static async Task UpdateFeatureServiceSpatialUnitReferenceAsync(
        string targetUrl,
        string scopeField,
        string scopeValue,
        string spatialUnitId,
        string spatialUnitApiStatus,
        CancellationToken cancellationToken)
    {
        var token = GetPortalToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("ArcGIS Enterprise Spatial Unit reference writeback requires ARCGIS_PORTAL_TOKEN. Generate a fresh portal token, set ARCGIS_PORTAL_TOKEN for the ArcGIS Pro process, restart ArcGIS Pro if needed, then retry Finalize.");
        }

        var referer = GetArcGisReferer(targetUrl);
        var metadataForm = new Dictionary<string, string>
        {
            ["f"] = "json"
        };
        AddToken(metadataForm, token);
        var metadataResponse = await PostFormAsync(targetUrl.TrimEnd('/'), metadataForm, cancellationToken, referer).ConfigureAwait(false);
        EnsureArcGisDispositionSuccess(metadataResponse, "metadata", "case_index");
        var objectIdField = ReadJsonString(metadataResponse, "objectIdField")
            ?? ReadJsonString(metadataResponse, "objectIdFieldName")
            ?? "OBJECTID";
        EnsureRequiredFields(metadataResponse, "case_index", RequiredSpatialUnitReferenceFields);

        var where = $"{scopeField} = '{EscapeSqlLiteral(scopeValue)}'";
        var queryForm = new Dictionary<string, string>
        {
            ["f"] = "json",
            ["where"] = where,
            ["outFields"] = objectIdField,
            ["returnGeometry"] = "false"
        };
        AddToken(queryForm, token);
        var queryResponse = await PostFormAsync($"{targetUrl.TrimEnd('/')}/query", queryForm, cancellationToken, referer).ConfigureAwait(false);
        EnsureArcGisDispositionSuccess(queryResponse, "query", "case_index");

        if (queryResponse["features"] is not JsonArray features || features.Count == 0)
        {
            throw new InvalidOperationException($"case_index Spatial Unit reference writeback found no rows for {scopeField}={scopeValue}.");
        }

        var updates = new JsonArray();
        foreach (var feature in features.OfType<JsonObject>())
        {
            if (feature["attributes"] is not JsonObject attributes
                || !attributes.TryGetPropertyValue(objectIdField, out var objectId)
                || objectId is null)
            {
                throw new InvalidOperationException($"case_index Spatial Unit reference query did not return {objectIdField}.");
            }

            updates.Add(new JsonObject
            {
                ["attributes"] = new JsonObject
                {
                    [objectIdField] = objectId.DeepClone(),
                    ["spatial_unit_id"] = spatialUnitId,
                    ["spatial_unit_api_status"] = spatialUnitApiStatus
                }
            });
        }

        var updateForm = new Dictionary<string, string>
        {
            ["f"] = "json",
            ["features"] = updates.ToJsonString(JsonOptions),
            ["rollbackOnFailure"] = "true"
        };
        AddToken(updateForm, token);
        var updateResponse = await PostFormAsync($"{targetUrl.TrimEnd('/')}/updateFeatures", updateForm, cancellationToken, referer).ConfigureAwait(false);
        EnsureArcGisDispositionSuccess(updateResponse, "updateFeatures", "case_index");
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

    private static void UpdateJsonSpatialUnitReference(
        string targetPath,
        string scopeField,
        string scopeValue,
        string spatialUnitId,
        string spatialUnitApiStatus)
    {
        var document = LoadTargetDocument(targetPath);
        var matched = document.Records.Where(record =>
            (string.IsNullOrWhiteSpace(record.LayerRole) || string.Equals(record.LayerRole, "case_index", StringComparison.OrdinalIgnoreCase))
            && record.Scope.TryGetPropertyValue(scopeField, out var valueNode)
            && string.Equals(valueNode?.GetValue<string>(), scopeValue, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matched.Length == 0)
        {
            throw new InvalidOperationException($"No case_index row was found for {scopeField}={scopeValue}.");
        }

        foreach (var storeRecord in matched)
        {
            if (storeRecord.Payload["records"] is JsonArray records)
            {
                foreach (var record in records.OfType<JsonObject>())
                {
                    ApplySpatialUnitReference(record, spatialUnitId, spatialUnitApiStatus);
                }
            }
            else
            {
                ApplySpatialUnitReference(storeRecord.Payload, spatialUnitId, spatialUnitApiStatus);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? ".");
        File.WriteAllText(targetPath, JsonSerializer.Serialize(document, JsonOptions));
    }

    private static void ApplyDisposition(
        JsonObject record,
        ComputeReviewDispositionRequest request,
        string decidedAtUtc,
        bool useArcGisDateValue = false)
    {
        var caseStatus = request.Decision == ComputeReviewDecision.Postponed ? "review_postponed" : "review_closed";
        record["review_decision"] = request.Decision.ToContractValue();
        record["review_decision_by"] = request.OperatorId ?? string.Empty;
        record["review_decision_utc"] = useArcGisDateValue
            ? ToArcGisDateValue(decidedAtUtc)
            : decidedAtUtc;
        record["review_comment"] = request.Comment ?? string.Empty;
        record["official_comparison_status"] = string.Empty;
        record["official_reference_ids"] = string.Empty;
        record["review_state"] = "final_review_decided";
        record["case_status"] = caseStatus;
    }

    private static JsonNode ToArcGisDateValue(string decidedAtUtc)
    {
        return DateTimeOffset.TryParse(
            decidedAtUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? JsonValue.Create(parsed.ToUnixTimeMilliseconds())!
            : JsonValue.Create(decidedAtUtc)!;
    }

    private static void ApplySpatialUnitReference(JsonObject record, string spatialUnitId, string spatialUnitApiStatus)
    {
        record["spatial_unit_id"] = spatialUnitId;
        record["spatial_unit_api_status"] = spatialUnitApiStatus;
    }

    private static void EnsureRequiredFields(JsonObject metadataResponse, string layerRole)
    {
        EnsureRequiredFields(metadataResponse, layerRole, RequiredDispositionFields);
    }

    private static void EnsureRequiredFields(JsonObject metadataResponse, string layerRole, IReadOnlyList<string> requiredFields)
    {
        if (metadataResponse["fields"] is not JsonArray fields)
        {
            throw new InvalidOperationException($"{layerRole} disposition writeback could not validate Enterprise schema fields.");
        }

        var available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields.OfType<JsonObject>())
        {
            var name = ReadJsonString(field, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                available.Add(name);
            }
        }

        var missing = requiredFields.Where(field => !available.Contains(field)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"{layerRole} disposition writeback schema is missing required field(s): {string.Join(", ", missing)}.");
        }
    }

    private static Uri? GetArcGisReferer(string targetUrl)
    {
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return new Uri(uri.GetLeftPart(UriPartial.Authority));
    }

    private static string? GetPortalToken()
    {
        return FirstNonBlank(
            Environment.GetEnvironmentVariable("ARCGIS_PORTAL_TOKEN", EnvironmentVariableTarget.Process),
            Environment.GetEnvironmentVariable("ARCGIS_PORTAL_TOKEN", EnvironmentVariableTarget.User),
            Environment.GetEnvironmentVariable("ARCGIS_PORTAL_TOKEN", EnvironmentVariableTarget.Machine));
    }

    private static string? FirstNonBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static async Task<JsonObject> PostFormAsync(string url, IReadOnlyDictionary<string, string> form, CancellationToken cancellationToken, Uri? referer = null)
    {
        using var content = new FormUrlEncodedContent(form);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        if (referer is not null)
        {
            request.Headers.Referrer = referer;
        }

        using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonNode.Parse(text)?.AsObject() ?? [];
    }

    private static void EnsureArcGisDispositionSuccess(JsonObject response, string operation, string layerRole)
    {
        if (response.TryGetPropertyValue("error", out var errorNode) && errorNode is JsonObject error)
        {
            var message = ReadJsonString(error, "message") ?? "ArcGIS Enterprise returned an error.";
            var details = ReadJsonStringArray(error, "details");
            if (IsArcGisTokenError(error, message, details))
            {
                throw new InvalidOperationException($"{operation} failed for {layerRole}: ArcGIS token is invalid or expired. Generate a fresh portal token, set ARCGIS_PORTAL_TOKEN for the ArcGIS Pro process, restart ArcGIS Pro if needed, then retry Finalize.");
            }

            var detailSuffix = details.Count == 0
                ? string.Empty
                : $" Details: {string.Join("; ", details)}";
            throw new InvalidOperationException($"{operation} failed for {layerRole}: {message}{detailSuffix}");
        }

        if (response.TryGetPropertyValue("updateResults", out var updateResultsNode) && updateResultsNode is JsonArray updateResults)
        {
            var failed = updateResults.OfType<JsonObject>().FirstOrDefault(result =>
                result.TryGetPropertyValue("success", out var successNode)
                && successNode?.GetValue<bool>() == false);
            if (failed is not null)
            {
                throw new InvalidOperationException($"{operation} failed for {layerRole}: one or more rows were rejected.");
            }
        }
    }

    private static void AddToken(IDictionary<string, string> form, string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            form["token"] = token;
        }
    }

    private static bool IsArcGisTokenError(JsonObject error, string message, IReadOnlyList<string> details) =>
        ReadJsonNumber(error, "code") is 498 or 499
        || message.Contains("token", StringComparison.OrdinalIgnoreCase)
        || details.Any(detail => detail.Contains("token", StringComparison.OrdinalIgnoreCase));

    private static string EscapeSqlLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static int? ReadJsonNumber(JsonObject obj, string propertyName)
    {
        if (obj.TryGetPropertyValue(propertyName, out var node)
            && node is JsonValue value
            && value.TryGetValue<int>(out var number))
        {
            return number;
        }

        return null;
    }

    private static string? ReadJsonString(JsonObject obj, string propertyName)
    {
        if (obj.TryGetPropertyValue(propertyName, out var node)
            && node is JsonValue value
            && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return null;
    }

    private static IReadOnlyList<string> ReadJsonStringArray(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is not JsonArray array)
        {
            return Array.Empty<string>();
        }

        return array
            .Select(item => item?.GetValue<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();
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
