using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Workflow.Output;

public sealed class JsonEnterpriseWorkingLayerPublishService : IEnterpriseWorkingLayerPublishService
{
    private static readonly string[] SharedEnterpriseAttributes =
    [
        "transaction_number",
        "transaction_id",
        "task_id",
        "workflow_stage",
        "review_state",
        "case_status",
        "created_by",
        "created_utc",
        "last_saved_by",
        "last_saved_utc",
        "run_id",
        "review_decision",
        "review_decision_by",
        "review_decision_utc",
        "review_comment",
        "official_comparison_status",
        "official_reference_ids",
        "is_active",
        "edit_generation"
    ];

    private static readonly IReadOnlySet<string> EnterpriseDateAttributes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "created_utc",
            "last_saved_utc",
            "review_decision_utc"
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> EnterpriseAttributeAllowlists =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["points"] = BuildAllowlist("point_id", "parcel_group_id", "parcel_name", "point_role", "status_txt", "source_txt", "row_id"),
            ["lines"] = BuildAllowlist("line_id", "parcel_group_id", "parcel_name", "start_pt", "end_pt", "bearing_txt", "distance_txt", "length_txt", "line_type", "seg_index", "source_txt"),
            ["polygons"] = BuildAllowlist("parcel_group_id", "parcel_name", "parcel_type", "validation_status", "closure_status", "area_sq_m", "perimeter_m", "review_note", "SUID", "source_txt"),
            ["issues"] = BuildAllowlist("issue_type", "issue_text"),
            ["case_index"] = BuildAllowlist("case_id", "workflow_name", "assigned_user", "assigned_group", "output_summary_ref", "working_publish_ref", "recoverability_state", "spatial_unit_id", "spatial_unit_api_status")
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    private static readonly HttpClient HttpClient = new();

    private readonly Func<InnolaTransactionSettings> getSettings;

    public JsonEnterpriseWorkingLayerPublishService()
        : this(InnolaTransactionSettings.Load)
    {
    }

    internal JsonEnterpriseWorkingLayerPublishService(Func<InnolaTransactionSettings> getSettings)
    {
        this.getSettings = getSettings;
    }

    public async Task<EnterpriseWorkingLayerPublishResult> PublishAsync(
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
            return EnterpriseWorkingLayerPublishResult.Skipped("Enterprise working layers mode is not active for this case.");
        }

        var reviewSettings = settings.EnterpriseWorkingReview;
        if (!reviewSettings.Enabled)
        {
            return WriteFailureSummary(
                layout,
                manifest,
                outputSummary,
                operatorId,
                reviewSettings.TransactionScopeField,
                "Enterprise working layers mode is configured, but enterprise working review is disabled.",
                warnings: new[] { reviewSettings.Warning ?? "Enable enterprise_working_review to publish shared review geometry." },
                errors: Array.Empty<string>());
        }

        var missingTargets = GetMissingRequiredTargets(reviewSettings);
        if (missingTargets.Count > 0)
        {
            return WriteFailureSummary(
                layout,
                manifest,
                outputSummary,
                operatorId,
                reviewSettings.TransactionScopeField,
                "Enterprise working-layer publish is missing required layer targets.",
                warnings: Array.Empty<string>(),
                errors: missingTargets);
        }

        var transaction = manifest.Payload.InnolaTransaction;
        var transactionScopeField = string.IsNullOrWhiteSpace(reviewSettings.TransactionScopeField)
            ? "transaction_number"
            : reviewSettings.TransactionScopeField;
        var transactionScopeValue = ResolveScopeValue(manifest, transactionScopeField);
        if (string.IsNullOrWhiteSpace(transactionScopeValue))
        {
            return WriteFailureSummary(
                layout,
                manifest,
                outputSummary,
                operatorId,
                transactionScopeField,
                "Enterprise working-layer publish could not resolve the configured transaction scope value.",
                warnings: Array.Empty<string>(),
                errors: new[] { $"The scope field '{transactionScopeField}' does not map to a value from the current manifest." });
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

            publishedLayers.Add(await PublishLayerAsync(
                reviewSettings.Layers.Points!,
                "points",
                transactionScopeField,
                transactionScopeValue,
                BuildPointPayload(reviewRows, manifest, outputSummary, operatorId, publishTime),
                cancellationToken).ConfigureAwait(false));

            publishedLayers.Add(await PublishLayerAsync(
                reviewSettings.Layers.Lines!,
                "lines",
                transactionScopeField,
                transactionScopeValue,
                BuildLinePayload(reviewRows, manifest, outputSummary, operatorId, publishTime),
                cancellationToken).ConfigureAwait(false));

            publishedLayers.Add(await PublishLayerAsync(
                reviewSettings.Layers.Polygons!,
                "polygons",
                transactionScopeField,
                transactionScopeValue,
                BuildPolygonPayload(reviewRows, manifest, outputSummary, operatorId, publishTime),
                cancellationToken).ConfigureAwait(false));

            publishedLayers.Add(await PublishLayerAsync(
                reviewSettings.Layers.CaseIndex!,
                "case_index",
                transactionScopeField,
                transactionScopeValue,
                BuildCaseIndexPayload(layout, manifest, outputSummary, operatorId, publishTime),
                cancellationToken).ConfigureAwait(false));

            if (!string.IsNullOrWhiteSpace(reviewSettings.Layers.Issues))
            {
                publishedLayers.Add(await PublishLayerAsync(
                    reviewSettings.Layers.Issues!,
                    "issues",
                    transactionScopeField,
                    transactionScopeValue,
                    BuildIssuesPayload(manifest, outputSummary, operatorId, publishTime),
                    cancellationToken).ConfigureAwait(false));
            }

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
            return EnterpriseWorkingLayerPublishResult.Succeeded(summary.Message, summaryPath, summary);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or NotSupportedException or HttpRequestException or TaskCanceledException)
        {
            return WriteFailureSummary(
                layout,
                manifest,
                outputSummary,
                operatorId,
                transactionScopeField,
                "Enterprise working-layer publish failed. Local output artifacts remain available.",
                warnings: Array.Empty<string>(),
                errors: new[] { exception.Message });
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

        if (string.IsNullOrWhiteSpace(settings.Layers.CaseIndex))
        {
            missing.Add("Case index working table target is not configured.");
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

    private static async Task<EnterpriseWorkingPublishedLayer> PublishLayerAsync(
        string targetPath,
        string layerRole,
        string transactionScopeField,
        string transactionScopeValue,
        JsonObject payload,
        CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(targetPath, UriKind.Absolute, out var uri)
            && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return await PublishFeatureServiceLayerAsync(targetPath, layerRole, transactionScopeField, transactionScopeValue, payload, cancellationToken).ConfigureAwait(false);
        }

        return PublishJsonLayer(targetPath, layerRole, transactionScopeField, transactionScopeValue, payload);
    }

    private static EnterpriseWorkingPublishedLayer PublishJsonLayer(
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

    private static async Task<EnterpriseWorkingPublishedLayer> PublishFeatureServiceLayerAsync(
        string targetUrl,
        string layerRole,
        string transactionScopeField,
        string transactionScopeValue,
        JsonObject payload,
        CancellationToken cancellationToken)
    {
        var token = GetPortalToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("ArcGIS Enterprise publish requires ARCGIS_PORTAL_TOKEN. Generate a fresh portal token, set ARCGIS_PORTAL_TOKEN for the current user or ArcGIS Pro process, restart ArcGIS Pro if needed, then retry Finalize.");
        }

        var referer = GetArcGisReferer(targetUrl);
        var metadataForm = new Dictionary<string, string>
        {
            ["f"] = "json"
        };
        AddToken(metadataForm, token);
        var metadataResponse = await PostFormAsync(targetUrl.TrimEnd('/'), metadataForm, cancellationToken, referer).ConfigureAwait(false);
        EnsureArcGisSuccess(metadataResponse, "token validation", layerRole);
        ValidateLayerTargetRole(metadataResponse, targetUrl, layerRole);

        var where = $"{transactionScopeField} = '{EscapeSqlLiteral(transactionScopeValue)}'";
        var deleteForm = new Dictionary<string, string>
        {
            ["f"] = "json",
            ["where"] = where
        };
        AddToken(deleteForm, token);
        var deleteResponse = await PostFormAsync($"{targetUrl.TrimEnd('/')}/deleteFeatures", deleteForm, cancellationToken, referer).ConfigureAwait(false);
        EnsureArcGisSuccess(deleteResponse, "deleteFeatures", layerRole);

        var features = BuildArcGisFeatures(payload, layerRole);
        var addForm = new Dictionary<string, string>
        {
            ["f"] = "json",
            ["features"] = features.ToJsonString(JsonOptions)
        };
        AddToken(addForm, token);
        var addResponse = await PostFormAsync($"{targetUrl.TrimEnd('/')}/addFeatures", addForm, cancellationToken, referer).ConfigureAwait(false);
        EnsureArcGisSuccess(addResponse, "addFeatures", layerRole);

        return new EnterpriseWorkingPublishedLayer(layerRole, targetUrl, ExtractPayloadCount(payload), true);
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
        return SelectPortalToken(
            Environment.GetEnvironmentVariable("ARCGIS_PORTAL_TOKEN", EnvironmentVariableTarget.Process),
            Environment.GetEnvironmentVariable("ARCGIS_PORTAL_TOKEN", EnvironmentVariableTarget.User),
            Environment.GetEnvironmentVariable("ARCGIS_PORTAL_TOKEN", EnvironmentVariableTarget.Machine));
    }

    private static string? SelectPortalToken(string? processToken, string? userToken, string? machineToken)
    {
        return FirstNonBlank(userToken, processToken, machineToken);
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

    private static JsonArray BuildArcGisFeatures(JsonObject payload, string layerRole)
    {
        var features = new JsonArray();
        if (payload["records"] is not JsonArray records)
        {
            return features;
        }

        foreach (var record in records.OfType<JsonObject>())
        {
            var attributes = new JsonObject();
            JsonNode? geometry = null;
            foreach (var property in record)
            {
                if (string.Equals(property.Key, "geometry", StringComparison.OrdinalIgnoreCase))
                {
                    geometry = property.Value?.DeepClone();
                    continue;
                }

                var attributeName = NormalizeEnterpriseAttributeName(property.Key, layerRole);
                if (!IsAllowedEnterpriseAttribute(attributeName, layerRole))
                {
                    continue;
                }

                if (!attributes.ContainsKey(attributeName) || attributes[attributeName] is null)
                {
                    attributes[attributeName] = NormalizeEnterpriseAttributeValue(attributeName, property.Value);
                }
            }

            var feature = new JsonObject
            {
                ["attributes"] = attributes
            };
            if (geometry is not null)
            {
                feature["geometry"] = NormalizeArcGisGeometry(geometry);
            }
            else if (RequiresGeometry(layerRole))
            {
                throw new InvalidOperationException($"Cannot publish {layerRole}: one or more records do not contain geometry.");
            }

            features.Add(feature);
        }

        return features;
    }

    private static IReadOnlySet<string> BuildAllowlist(params string[] layerAttributes)
    {
        var fields = new HashSet<string>(SharedEnterpriseAttributes, StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in layerAttributes)
        {
            fields.Add(attribute);
        }

        return fields;
    }

    private static bool IsAllowedEnterpriseAttribute(string attributeName, string layerRole)
    {
        return EnterpriseAttributeAllowlists.TryGetValue(layerRole, out var allowlist)
            && allowlist.Contains(attributeName);
    }

    private static string NormalizeEnterpriseAttributeName(string attributeName, string layerRole)
    {
        if (string.Equals(layerRole, "points", StringComparison.OrdinalIgnoreCase))
        {
            return attributeName.ToLowerInvariant() switch
            {
                "point_identifier" => "point_id",
                "status" => "status_txt",
                "source_evidence" => "source_txt",
                "source_doc" => "source_txt",
                _ => attributeName
            };
        }

        if (string.Equals(layerRole, "lines", StringComparison.OrdinalIgnoreCase))
        {
            return attributeName.ToLowerInvariant() switch
            {
                "from_point_id" => "start_pt",
                "to_point_id" => "end_pt",
                "from_point" => "start_pt",
                "to_point" => "end_pt",
                "bearing" => "bearing_txt",
                "course" => "bearing_txt",
                "distance" => "distance_txt",
                "distance_m" => "distance_txt",
                "length" => "length_txt",
                "source_evidence" => "source_txt",
                "source_doc" => "source_txt",
                _ => attributeName
            };
        }

        if (string.Equals(layerRole, "polygons", StringComparison.OrdinalIgnoreCase))
        {
            return attributeName.ToLowerInvariant() switch
            {
                "status" => "validation_status",
                "source_evidence" => "source_txt",
                "source_doc" => "source_txt",
                _ => attributeName
            };
        }

        return attributeName;
    }

    private static JsonNode? NormalizeEnterpriseAttributeValue(string attributeName, JsonNode? value)
    {
        if (!EnterpriseDateAttributes.Contains(attributeName) || value is null)
        {
            return value?.DeepClone();
        }

        try
        {
            if (value is JsonValue jsonValue
                && jsonValue.TryGetValue<string>(out var text)
                && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return parsed.ToUnixTimeMilliseconds();
            }
        }
        catch (InvalidOperationException)
        {
            return value.DeepClone();
        }

        return value.DeepClone();
    }

    private static void EnsureArcGisSuccess(JsonObject response, string operation, string layerRole)
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

        if (response.TryGetPropertyValue("addResults", out var addResultsNode) && addResultsNode is JsonArray addResults)
        {
            var failedResults = addResults.OfType<JsonObject>().Where(result =>
                result.TryGetPropertyValue("success", out var successNode)
                && successNode?.GetValue<bool>() == false).ToArray();
            if (failedResults.Length > 0)
            {
                throw new InvalidOperationException(
                    $"{operation} failed for {layerRole}: one or more rows were rejected. {FormatArcGisEditFailures(failedResults)}");
            }
        }

        if (response.TryGetPropertyValue("deleteResults", out var deleteResultsNode) && deleteResultsNode is JsonArray deleteResults)
        {
            var failed = deleteResults.OfType<JsonObject>().FirstOrDefault(result =>
                result.TryGetPropertyValue("success", out var successNode)
                && successNode?.GetValue<bool>() == false);
            if (failed is not null)
            {
                throw new InvalidOperationException(
                    $"{operation} failed for {layerRole}: one or more existing rows could not be removed. {FormatArcGisEditFailures(new[] { failed })}");
            }
        }
    }

    private static void ValidateLayerTargetRole(JsonObject metadata, string targetUrl, string layerRole)
    {
        var actualName = ReadJsonString(metadata, "name") ?? string.Empty;
        var actualGeometryType = ReadJsonString(metadata, "geometryType") ?? string.Empty;
        var expectedName = ExpectedLayerName(layerRole);
        var expectedGeometryType = ExpectedGeometryType(layerRole);
        var isTable = string.Equals(layerRole, "case_index", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(expectedName)
            && !string.Equals(actualName, expectedName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Configured {layerRole} Enterprise target does not match the expected layer. URL '{targetUrl}' returned layer '{actualName}', expected '{expectedName}'. Re-run Enterprise Admin provisioning or update the Enterprise Working Review layer URLs in Settings.");
        }

        if (!isTable
            && !string.IsNullOrWhiteSpace(expectedGeometryType)
            && !string.Equals(actualGeometryType, expectedGeometryType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Configured {layerRole} Enterprise target has geometry '{actualGeometryType}', expected '{expectedGeometryType}'. URL: '{targetUrl}'. Re-run Enterprise Admin provisioning or update the Enterprise Working Review layer URLs in Settings.");
        }
    }

    private static string ExpectedLayerName(string layerRole)
    {
        return layerRole.ToLowerInvariant() switch
        {
            "points" => "working_points",
            "lines" => "working_lines",
            "polygons" => "working_polygons",
            "issues" => "working_issues",
            "case_index" => "working_case_index",
            _ => string.Empty
        };
    }

    private static string ExpectedGeometryType(string layerRole)
    {
        return layerRole.ToLowerInvariant() switch
        {
            "points" => "esriGeometryPoint",
            "lines" => "esriGeometryPolyline",
            "polygons" => "esriGeometryPolygon",
            "issues" => "esriGeometryPoint",
            _ => string.Empty
        };
    }

    private static string FormatArcGisEditFailures(IReadOnlyList<JsonObject> failedResults)
    {
        var details = failedResults
            .Take(5)
            .Select((result, index) =>
            {
                var objectId = ReadJsonString(result, "objectId") ?? ReadJsonString(result, "globalId") ?? $"row {index + 1}";
                var error = result["error"] as JsonObject;
                if (error is null)
                {
                    return $"{objectId}: rejected without error details";
                }

                var code = TryReadJsonInt(error, "code")?.ToString(CultureInfo.InvariantCulture);
                var description = ReadJsonString(error, "description") ?? ReadJsonString(error, "message") ?? "ArcGIS Enterprise returned an edit error.";
                return string.IsNullOrWhiteSpace(code)
                    ? $"{objectId}: {description}"
                    : $"{objectId}: {code} {description}";
            })
            .ToArray();

        var suffix = failedResults.Count > details.Length
            ? $" Additional rejected rows: {failedResults.Count - details.Length}."
            : string.Empty;
        return $"Rejected rows: {string.Join("; ", details)}.{suffix}";
    }

    private static void AddToken(IDictionary<string, string> form, string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            form["token"] = token;
        }
    }

    private static bool IsArcGisTokenError(JsonObject error, string message, IReadOnlyList<string> details)
    {
        var code = TryReadJsonInt(error, "code");
        return code is 498 or 499
            || message.Contains("token", StringComparison.OrdinalIgnoreCase)
            || details.Any(detail => detail.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    private static int? TryReadJsonInt(JsonObject raw, string propertyName)
    {
        if (!raw.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return null;
        }

        try
        {
            return node.GetValue<int>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ReadJsonStringArray(JsonObject raw, string propertyName)
    {
        if (!raw.TryGetPropertyValue(propertyName, out var node) || node is not JsonArray array)
        {
            return Array.Empty<string>();
        }

        return array
            .Select(item => item?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    private static string EscapeSqlLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
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
        OutputSummaryDocument outputSummary,
        string? operatorId,
        DateTimeOffset publishTime)
    {
        var records = ReadFeatureRows(outputSummary.Payload.PointFeatureClassPath, outputSummary, "points");
        if (records.Count > 0)
        {
            EnrichRecords(records, manifest, outputSummary, operatorId, publishTime);
            return BuildLayerEnvelope("points", manifest, outputSummary, records, operatorId, publishTime);
        }

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
            AddPointGeometry(node);
            records.Add(node);
        }

        EnrichRecords(records, manifest, outputSummary, operatorId, publishTime);
        return BuildLayerEnvelope("points", manifest, outputSummary, records, operatorId, publishTime);
    }

    private static JsonObject BuildLinePayload(
        IReadOnlyList<ExtractionReviewRow> rows,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary,
        string? operatorId,
        DateTimeOffset publishTime)
    {
        var records = ReadFeatureRows(outputSummary.Payload.LineFeatureClassPath, outputSummary, "lines");
        if (records.Count > 0)
        {
            foreach (var record in records.OfType<JsonObject>())
            {
                SetIfMissing(record, "bearing_txt", ReadJsonString(record, "bearing") ?? ReadJsonString(record, "course") ?? string.Empty);
                SetIfMissing(record, "distance_txt", ReadJsonString(record, "distance") ?? ReadJsonString(record, "distance_m") ?? string.Empty);
                SetIfMissing(record, "length_txt", ReadJsonString(record, "length") ?? ReadJsonString(record, "distance_txt") ?? string.Empty);
            }

            EnrichRecords(records, manifest, outputSummary, operatorId, publishTime);
            return BuildLayerEnvelope("lines", manifest, outputSummary, records, operatorId, publishTime);
        }

        foreach (var row in rows.Where(HasLineSignals))
        {
            var raw = row.RawRow;
            var node = new JsonObject
            {
                ["row_id"] = row.RowId,
                ["line_id"] = ReadRawString(raw, "segment_no") ?? row.RowId,
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

        EnrichRecords(records, manifest, outputSummary, operatorId, publishTime);
        return BuildLayerEnvelope("lines", manifest, outputSummary, records, operatorId, publishTime);
    }

    private static JsonObject BuildPolygonPayload(
        IReadOnlyList<ExtractionReviewRow> rows,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary,
        string? operatorId,
        DateTimeOffset publishTime)
    {
        var records = ReadFeatureRows(outputSummary.Payload.PolygonFeatureClassPath, outputSummary, "polygons");
        if (records.Count > 0)
        {
            EnrichRecords(records, manifest, outputSummary, operatorId, publishTime);
            return BuildLayerEnvelope("polygons", manifest, outputSummary, records, operatorId, publishTime);
        }

        records = new JsonArray
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

        EnrichRecords(records, manifest, outputSummary, operatorId, publishTime);
        return BuildLayerEnvelope("polygons", manifest, outputSummary, records, operatorId, publishTime);
    }

    private static JsonObject BuildCaseIndexPayload(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary,
        string? operatorId,
        DateTimeOffset publishTime)
    {
        var transaction = manifest.Payload.InnolaTransaction;
        var publishedUtc = publishTime.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var records = new JsonArray
        {
            new JsonObject
            {
                ["case_id"] = Path.GetFileName(layout.RootDirectory),
                ["transaction_number"] = transaction?.TransactionNumber ?? manifest.TransactionId,
                ["transaction_id"] = manifest.TransactionId,
                ["task_id"] = transaction?.TaskId ?? string.Empty,
                ["workflow_name"] = manifest.Payload.WorkflowProfile ?? transaction?.ProcessStep ?? "parcel_workflow",
                ["workflow_stage"] = WorkflowState.SpatialReviewPending.ToContractValue(),
                ["case_status"] = "review_pending",
                ["review_state"] = "published_to_working",
                ["assigned_user"] = transaction?.AssignedUser ?? transaction?.SelectedUser ?? string.Empty,
                ["assigned_group"] = transaction?.AssignedGroup ?? string.Empty,
                ["created_by"] = operatorId ?? string.Empty,
                ["created_utc"] = publishedUtc,
                ["last_saved_by"] = operatorId ?? string.Empty,
                ["last_saved_utc"] = publishedUtc,
                ["run_id"] = outputSummary.RunId,
                ["output_summary_ref"] = Path.Combine(layout.OutputDirectory, "output_summary.json"),
                ["working_publish_ref"] = Path.Combine(layout.OutputDirectory, "enterprise_working_publish.json"),
                ["recoverability_state"] = "current",
                ["is_active"] = 1,
                ["edit_generation"] = 1
            }
        };

        return BuildLayerEnvelope("case_index", manifest, outputSummary, records, operatorId, publishTime);
    }

    private static JsonObject BuildIssuesPayload(
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary,
        string? operatorId,
        DateTimeOffset publishTime)
    {
        var records = new JsonArray();
        foreach (var warning in outputSummary.Warnings)
        {
            records.Add(new JsonObject
            {
                ["issue_type"] = "warning",
                ["issue_text"] = Truncate(warning, 512)
            });
        }

        EnrichRecords(records, manifest, outputSummary, operatorId, publishTime);
        return BuildLayerEnvelope("issues", manifest, outputSummary, records, operatorId, publishTime);
    }

    private static JsonObject BuildLayerEnvelope(
        string layerRole,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary,
        JsonArray records,
        string? operatorId,
        DateTimeOffset publishTime)
    {
        var transaction = manifest.Payload.InnolaTransaction;
        var publishedUtc = publishTime.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
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
            ["created_by"] = operatorId ?? string.Empty,
            ["created_utc"] = publishedUtc,
            ["last_saved_by"] = operatorId ?? string.Empty,
            ["last_saved_utc"] = publishedUtc,
            ["run_id"] = outputSummary.RunId,
            ["result_gdb_path"] = outputSummary.Payload.ResultGdbPath ?? string.Empty,
            ["records"] = records
        };
    }

    private static JsonArray ReadFeatureRows(string? featureClassPath, OutputSummaryDocument outputSummary, string layerRole)
    {
        if (!string.IsNullOrWhiteSpace(featureClassPath) && File.Exists(featureClassPath))
        {
            var node = JsonNode.Parse(File.ReadAllText(featureClassPath));
            if (node is JsonArray array)
            {
                var clone = new JsonArray();
                foreach (var item in array)
                {
                    clone.Add(item?.DeepClone());
                }

                AddDerivedGeometry(clone, layerRole);
                if (RequiresGeometry(layerRole) && clone.OfType<JsonObject>().Any(record => !record.ContainsKey("geometry")))
                {
                    var fallbackGeoJsonPath = FindGeoJsonPath(outputSummary);
                    var geoJsonRows = string.IsNullOrWhiteSpace(fallbackGeoJsonPath)
                        ? []
                        : ReadGeoJsonRows(fallbackGeoJsonPath, layerRole);
                    if (geoJsonRows.Count > 0)
                    {
                        return geoJsonRows;
                    }
                }

                return clone;
            }
        }

        var geoJsonPath = FindGeoJsonPath(outputSummary);
        return string.IsNullOrWhiteSpace(geoJsonPath)
            ? []
            : ReadGeoJsonRows(geoJsonPath, layerRole);
    }

    private static string? FindGeoJsonPath(OutputSummaryDocument outputSummary)
    {
        return outputSummary.Payload.ArtifactPaths.FirstOrDefault(path =>
            !string.IsNullOrWhiteSpace(path)
            && File.Exists(path)
            && string.Equals(Path.GetFileName(path), "extracted_geometry.geojson", StringComparison.OrdinalIgnoreCase));
    }

    private static JsonArray ReadGeoJsonRows(string geoJsonPath, string layerRole)
    {
        var node = JsonNode.Parse(File.ReadAllText(geoJsonPath));
        if (node is not JsonObject root || root["features"] is not JsonArray features)
        {
            return [];
        }

        var rows = new JsonArray();
        foreach (var feature in features.OfType<JsonObject>())
        {
            if (feature["geometry"] is not JsonObject geometry || !GeoJsonTypeMatchesRole(ReadJsonString(geometry, "type"), layerRole))
            {
                continue;
            }

            var row = feature["properties"]?.DeepClone() as JsonObject ?? [];
            row["geometry"] = ConvertGeoJsonGeometry(geometry);
            rows.Add(row);
        }

        return rows;
    }

    private static void AddDerivedGeometry(JsonArray records, string layerRole)
    {
        if (!string.Equals(layerRole, "points", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var record in records.OfType<JsonObject>())
        {
            if (record.ContainsKey("geometry"))
            {
                continue;
            }

            AddPointGeometry(record);
        }
    }

    private static void AddPointGeometry(JsonObject record)
    {
        if (record.ContainsKey("geometry"))
        {
            return;
        }

        var xText = ReadJsonString(record, "x") ?? ReadJsonString(record, "easting");
        var yText = ReadJsonString(record, "y") ?? ReadJsonString(record, "northing");
        if (double.TryParse(xText, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            && double.TryParse(yText, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            record["geometry"] = new JsonObject
            {
                ["x"] = x,
                ["y"] = y
            };
        }
    }

    private static bool RequiresGeometry(string layerRole)
    {
        return string.Equals(layerRole, "points", StringComparison.OrdinalIgnoreCase)
            || string.Equals(layerRole, "lines", StringComparison.OrdinalIgnoreCase)
            || string.Equals(layerRole, "polygons", StringComparison.OrdinalIgnoreCase);
    }

    private static bool GeoJsonTypeMatchesRole(string? geometryType, string layerRole)
    {
        return layerRole.ToLowerInvariant() switch
        {
            "points" => string.Equals(geometryType, "Point", StringComparison.OrdinalIgnoreCase)
                || string.Equals(geometryType, "MultiPoint", StringComparison.OrdinalIgnoreCase),
            "lines" => string.Equals(geometryType, "LineString", StringComparison.OrdinalIgnoreCase)
                || string.Equals(geometryType, "MultiLineString", StringComparison.OrdinalIgnoreCase),
            "polygons" => string.Equals(geometryType, "Polygon", StringComparison.OrdinalIgnoreCase)
                || string.Equals(geometryType, "MultiPolygon", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static JsonNode? NormalizeArcGisGeometry(JsonNode geometry)
    {
        JsonNode? normalized;
        if (geometry is JsonObject geometryObject && geometryObject.TryGetPropertyValue("type", out var typeNode))
        {
            normalized = ConvertGeoJsonGeometry(geometryObject);
        }
        else
        {
            normalized = geometry.DeepClone();
        }

        if (normalized is JsonObject normalizedObject && !normalizedObject.ContainsKey("spatialReference"))
        {
            normalizedObject["spatialReference"] = new JsonObject
            {
                ["wkid"] = 3448,
                ["latestWkid"] = 3448
            };
        }

        return normalized;
    }

    private static JsonObject ConvertGeoJsonGeometry(JsonObject geometry)
    {
        var type = ReadJsonString(geometry, "type") ?? string.Empty;
        var coordinates = geometry["coordinates"];
        return type.ToLowerInvariant() switch
        {
            "point" => ConvertPointGeometry(coordinates),
            "multipoint" => new JsonObject { ["points"] = ConvertCoordinateArray(coordinates) },
            "linestring" => new JsonObject { ["paths"] = new JsonArray(ConvertCoordinateArray(coordinates)) },
            "multilinestring" => new JsonObject { ["paths"] = ConvertCoordinateArray(coordinates) },
            "polygon" => new JsonObject { ["rings"] = ConvertCoordinateArray(coordinates) },
            "multipolygon" => new JsonObject { ["rings"] = FlattenMultiPolygonRings(coordinates) },
            _ => []
        };
    }

    private static JsonObject ConvertPointGeometry(JsonNode? coordinates)
    {
        var point = coordinates is JsonArray values ? values : [];
        var geometry = new JsonObject
        {
            ["x"] = ReadCoordinate(point, 0),
            ["y"] = ReadCoordinate(point, 1)
        };
        if (point.Count > 2)
        {
            geometry["z"] = ReadCoordinate(point, 2);
        }

        return geometry;
    }

    private static JsonArray FlattenMultiPolygonRings(JsonNode? coordinates)
    {
        var rings = new JsonArray();
        if (coordinates is not JsonArray polygons)
        {
            return rings;
        }

        foreach (var polygon in polygons.OfType<JsonArray>())
        {
            foreach (var ring in polygon.OfType<JsonArray>())
            {
                rings.Add(ConvertCoordinateArray(ring));
            }
        }

        return rings;
    }

    private static JsonArray ConvertCoordinateArray(JsonNode? coordinates)
    {
        var converted = new JsonArray();
        if (coordinates is not JsonArray array)
        {
            return converted;
        }

        foreach (var item in array)
        {
            converted.Add(item is JsonArray child ? ConvertCoordinateArray(child) : item?.DeepClone());
        }

        return converted;
    }

    private static double ReadCoordinate(JsonArray coordinates, int index)
    {
        return coordinates.Count > index && coordinates[index] is not null
            ? coordinates[index]!.GetValue<double>()
            : 0d;
    }

    private static void EnrichRecords(
        JsonArray records,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary,
        string? operatorId,
        DateTimeOffset publishTime)
    {
        var transaction = manifest.Payload.InnolaTransaction;
        var publishedUtc = publishTime.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        foreach (var record in records.OfType<JsonObject>())
        {
            SetIfMissing(record, "transaction_number", transaction?.TransactionNumber ?? manifest.TransactionId);
            SetIfMissing(record, "transaction_id", manifest.TransactionId);
            SetIfMissing(record, "task_id", transaction?.TaskId ?? string.Empty);
            SetIfMissing(record, "workflow_stage", WorkflowState.SpatialReviewPending.ToContractValue());
            SetIfMissing(record, "review_state", "published_to_working");
            SetIfMissing(record, "case_status", "review_pending");
            SetIfMissing(record, "created_by", operatorId ?? string.Empty);
            SetIfMissing(record, "created_utc", publishedUtc);
            SetIfMissing(record, "last_saved_by", operatorId ?? string.Empty);
            SetIfMissing(record, "last_saved_utc", publishedUtc);
            SetIfMissing(record, "run_id", outputSummary.RunId);
            SetIfMissing(record, "is_active", 1);
            SetIfMissing(record, "edit_generation", 1);
        }
    }

    private static void SetIfMissing(JsonObject record, string propertyName, string value)
    {
        if (!record.ContainsKey(propertyName) || record[propertyName] is null)
        {
            record[propertyName] = value;
        }
    }

    private static void SetIfMissing(JsonObject record, string propertyName, int value)
    {
        if (!record.ContainsKey(propertyName) || record[propertyName] is null)
        {
            record[propertyName] = value;
        }
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

    private static string? ReadJsonString(JsonObject raw, string propertyName)
    {
        if (!raw.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return null;
        }

        try
        {
            return node.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            return node.ToJsonString();
        }
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
