using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Workflow.Disposition;
using ParcelWorkflowAddIn.Workflow.Output;

namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaSpatialUnitService : IInnolaSpatialUnitService
{
    private const string SpatialUnitClass = "SpatialUnitExt";
    private const string SpatialUnitTypeKey = "spatialunit";
    private const string DefaultParishDisplayName = "St Andrew";
    private const string DefaultParishTypeKey = "parish_st_andrew";
    private static readonly JsonSerializerOptions TraceJsonOptions = new()
    {
        WriteIndented = true
    };
    private readonly HttpClient httpClient;
    private readonly OutputSummaryPersistenceService outputSummaryPersistenceService = new();

    public InnolaSpatialUnitService()
        : this(new HttpClient())
    {
    }

    public InnolaSpatialUnitService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<InnolaSpatialUnitSaveResult> CreateOrUpdateAsync(
        InnolaSession session,
        SelectedInnolaTransaction transaction,
        string caseFolderPath,
        ComputeReviewDispositionDocument disposition,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(session.ServerUrl) || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return InnolaSpatialUnitSaveResult.Failed("Could not create Spatial Unit because the Innola session is not authorized.", "unauthorized");
        }

        if (string.IsNullOrWhiteSpace(transaction.TransactionId))
        {
            return InnolaSpatialUnitSaveResult.Failed("Could not create Spatial Unit because the transaction id is unavailable.", "transaction_id_missing");
        }

        try
        {
            var layout = CaseFolderLayout.FromRootDirectory(caseFolderPath);
            var outputSummary = outputSummaryPersistenceService.Load(layout);
            var spatialUnitCount = ResolveSpatialUnitCount(outputSummary);
            WriteSpatialUnitApiRequest(layout, transaction, disposition, outputSummary, spatialUnitCount);

            var defaults = await CreateDefaultSpatialUnitsAsync(
                session,
                transaction.TransactionId,
                spatialUnitCount,
                cancellationToken).ConfigureAwait(false);
            if (defaults.Count == 0)
            {
                WriteSpatialUnitApiFailure(layout, transaction, "spatial_unit_defaults_empty", "Spatial Unit default creation did not return any objects.");
                return InnolaSpatialUnitSaveResult.Failed("Spatial Unit default creation did not return any objects.", "spatial_unit_defaults_empty");
            }

            var polygonAttributes = ResolveWorkingPolygonAttributes(layout, outputSummary);
            var populated = defaults
                .Select((item, index) => PopulateSpatialUnit(
                    item,
                    transaction,
                    layout,
                    disposition,
                    outputSummary,
                    index < polygonAttributes.Count ? polygonAttributes[index] : null))
                .ToArray();
            WriteSpatialUnitApiPayload(layout, transaction, polygonAttributes, populated);

            var saved = await SaveSpatialUnitsAsync(
                session,
                transaction.TransactionId,
                populated,
                cancellationToken).ConfigureAwait(false);
            var spatialUnitId = ResolveSpatialUnitId(saved) ?? ResolveSpatialUnitId(populated);
            var polygonReferences = ResolvePolygonReferences(saved, populated, polygonAttributes);
            WriteSpatialUnitApiResponse(layout, transaction, spatialUnitCount, defaults.Count, populated.Length, saved, polygonReferences);
            return InnolaSpatialUnitSaveResult.Succeeded(spatialUnitId, polygonReferences: polygonReferences);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            TryWriteSpatialUnitApiFailure(caseFolderPath, transaction, exception);
            Debug.WriteLine($"Innola Spatial Unit save failed. TransactionId={transaction.TransactionId}; Error={exception.GetType().Name}.");
            return InnolaSpatialUnitSaveResult.Failed("Could not create Spatial Unit. Try again.", exception.GetType().Name);
        }
    }

    private async Task<IReadOnlyList<JsonObject>> CreateDefaultSpatialUnitsAsync(
        InnolaSession session,
        string transactionId,
        int count,
        CancellationToken cancellationToken)
    {
        var requestBody = new JsonArray();
        for (var index = 0; index < count; index++)
        {
            requestBody.Add(new JsonObject
            {
                ["@c"] = SpatialUnitClass,
                ["id"] = null
            });
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            InnolaHttp.BuildUri(
                session.ServerUrl,
                $"{InnolaSettings.V4RestPath}administrative/ladm-objects/create/multi?transactionId={Uri.EscapeDataString(transactionId)}"));
        InnolaHttp.ApplyAuthHeaders(request, session.AccessToken);
        request.Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Spatial Unit default creation failed: {response.StatusCode}");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ResolveArray(JsonNode.Parse(body))
            .Select(node => node as JsonObject ?? new JsonObject())
            .ToArray();
    }

    private async Task<IReadOnlyList<JsonObject>> SaveSpatialUnitsAsync(
        InnolaSession session,
        string transactionId,
        IReadOnlyList<JsonObject> spatialUnits,
        CancellationToken cancellationToken)
    {
        var payload = new JsonArray(spatialUnits.Select(unit => unit.DeepClone()).ToArray());
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            InnolaHttp.BuildUri(
                session.ServerUrl,
                $"{InnolaSettings.V4RestPath}administrative/ladm-objects?typeKeyId={SpatialUnitTypeKey}&transactionId={Uri.EscapeDataString(transactionId)}"));
        InnolaHttp.ApplyAuthHeaders(request, session.AccessToken);
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Spatial Unit save failed: {response.StatusCode}");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var resolved = ResolveArray(JsonNode.Parse(body))
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Cast<JsonObject>()
            .ToArray();
        return resolved.Length == 0 ? spatialUnits : resolved;
    }

    private static JsonObject PopulateSpatialUnit(
        JsonObject source,
        SelectedInnolaTransaction transaction,
        CaseFolderLayout layout,
        ComputeReviewDispositionDocument disposition,
        OutputSummaryDocument? outputSummary,
        WorkingPolygonSpatialUnitAttributes? polygonAttributes)
    {
        var spatialUnit = source.DeepClone() as JsonObject ?? new JsonObject();
        spatialUnit["@c"] = ReadString(spatialUnit, "@c") ?? SpatialUnitClass;
        spatialUnit["type"] = "spatial_unit_type_land";
        spatialUnit["status"] = "reg_status_pending";
        spatialUnit["idMarkupType"] = "spatialunit";
        spatialUnit["transactionId"] = transaction.TransactionId;
        spatialUnit["transactionNumber"] = transaction.TransactionNumber;
        spatialUnit["taskId"] = transaction.TaskId;
        spatialUnit["reviewDecision"] = disposition.Decision;
        spatialUnit["reviewDecisionUtc"] = disposition.DecidedAtUtc;
        spatialUnit["reviewDecisionBy"] = disposition.OperatorId;
        spatialUnit["reviewComment"] = disposition.Comment;
        spatialUnit["enterprisePublishRef"] = ToCaseRelativePath(layout, disposition.EnterprisePublishRef);
        spatialUnit["outputSummaryRef"] = ToCaseRelativePath(layout, disposition.OutputSummaryRef);
        spatialUnit["publishRunId"] = disposition.PublishRunId;
        spatialUnit["workingPackageFileName"] = disposition.WorkingPackageFileName;
        spatialUnit["workingPackageSourceType"] = disposition.WorkingPackageSourceType;
        spatialUnit["workingPackageUploadStatus"] = disposition.WorkingPackageUploadStatus;
        spatialUnit["computeExaminationReportRef"] = ToCaseRelativePath(layout, disposition.ComputeExaminationReportRef);

        if (outputSummary is not null)
        {
            var parcelCount = ResolveSpatialUnitCount(outputSummary);
            spatialUnit["computeRunId"] = outputSummary.RunId;
            spatialUnit["parcelRecordName"] = outputSummary.Payload.ParcelRecordName;
            spatialUnit["parcelRecordId"] = outputSummary.Payload.ParcelRecordId;
            spatialUnit["parcelType"] = outputSummary.Payload.ParcelType;
            spatialUnit["parcelCount"] = parcelCount;
            spatialUnit["computedParcelCount"] = parcelCount;
            spatialUnit["computedPolygonCount"] = outputSummary.Payload.PolygonCount;
            spatialUnit["computedLineCount"] = outputSummary.Payload.LineCount;
            spatialUnit["computedPointCount"] = outputSummary.Payload.PointCount;
            spatialUnit["workingLayerReferences"] = new JsonArray(
                (outputSummary.Payload.EnterpriseWorkingPublish?.PublishedLayers ?? Array.Empty<EnterpriseWorkingPublishedLayer>())
                .Select(layer => new JsonObject
                {
                    ["layerRole"] = layer.LayerRole,
                    ["target"] = layer.Target,
                    ["recordCount"] = layer.RecordCount
                })
                .ToArray<JsonNode?>());

            if (string.IsNullOrWhiteSpace(ReadString(spatialUnit, "lot")) && !string.IsNullOrWhiteSpace(outputSummary.Payload.ParcelRecordName))
            {
                spatialUnit["lot"] = outputSummary.Payload.ParcelRecordName;
            }
        }

        ApplyWorkingPolygonSpatialUnitFields(spatialUnit, polygonAttributes);

        return spatialUnit;
    }

    private static void ApplyWorkingPolygonSpatialUnitFields(JsonObject spatialUnit, WorkingPolygonSpatialUnitAttributes? polygonAttributes)
    {
        if (polygonAttributes is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(polygonAttributes.Pid))
        {
            spatialUnit["PID"] = polygonAttributes.Pid;
            spatialUnit["lot"] = polygonAttributes.Pid;
            spatialUnit["lotNo"] = polygonAttributes.Pid;
            spatialUnit["lot No."] = polygonAttributes.Pid;
            spatialUnit["label"] = polygonAttributes.Pid;
        }

        spatialUnit["Parish"] = string.IsNullOrWhiteSpace(polygonAttributes.Parish)
            ? DefaultParishDisplayName
            : polygonAttributes.Parish;
        ApplyAddressParish(spatialUnit);

        if (polygonAttributes.Area.HasValue)
        {
            spatialUnit["area"] = polygonAttributes.Area.Value;
            spatialUnit["legalArea"] = polygonAttributes.Area.Value;
            spatialUnit["surveyArea"] = polygonAttributes.Area.Value;
            spatialUnit["gisArea"] = polygonAttributes.Area.Value;
            spatialUnit["legalAreaApproximate"] = false;
        }

        if (!string.IsNullOrWhiteSpace(polygonAttributes.Suid))
        {
            spatialUnit["SUID"] = polygonAttributes.Suid;
        }
    }

    private static IReadOnlyList<WorkingPolygonSpatialUnitAttributes> ResolveWorkingPolygonAttributes(
        CaseFolderLayout layout,
        OutputSummaryDocument? outputSummary)
    {
        var geoJsonPath = ResolveGeoJsonArtifactPath(layout, outputSummary);
        if (string.IsNullOrWhiteSpace(geoJsonPath) || !File.Exists(geoJsonPath))
        {
            return Array.Empty<WorkingPolygonSpatialUnitAttributes>();
        }

        var root = JsonNode.Parse(File.ReadAllText(geoJsonPath));
        if (root?["features"] is not JsonArray features)
        {
            return Array.Empty<WorkingPolygonSpatialUnitAttributes>();
        }

        var polygons = new List<WorkingPolygonSpatialUnitAttributes>();
        foreach (var feature in features.OfType<JsonObject>())
        {
            var geometryType = ReadString(feature["geometry"] as JsonObject, "type");
            if (!string.Equals(geometryType, "Polygon", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(geometryType, "MultiPolygon", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var properties = feature["properties"] as JsonObject;
            if (properties is null)
            {
                continue;
            }

            var pid = ReadString(properties, "parcel_name", "PID", "pid", "name", "parcel_id");
            var suid = ReadString(properties, "SUID", "suid");
            polygons.Add(new WorkingPolygonSpatialUnitAttributes(
                pid,
                ReadString(properties, "parish", "Parish") ?? DefaultParishDisplayName,
                ReadDouble(properties, "area_sq_m", "area"),
                suid));
        }

        return polygons;
    }

    private static string? ResolveGeoJsonArtifactPath(CaseFolderLayout layout, OutputSummaryDocument? outputSummary)
    {
        var artifactPath = outputSummary?.Payload.ArtifactPaths.FirstOrDefault(
            path => path.EndsWith(".geojson", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(artifactPath))
        {
            return null;
        }

        if (Path.IsPathFullyQualified(artifactPath))
        {
            return artifactPath;
        }

        return Path.GetFullPath(Path.Combine(layout.RootDirectory, artifactPath));
    }

    private static string? ToCaseRelativePath(CaseFolderLayout layout, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (!Path.IsPathFullyQualified(path))
        {
            return path.Replace('\\', '/');
        }

        var root = Path.GetFullPath(layout.RootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileName(path);
        }

        return Path.GetRelativePath(layout.RootDirectory, fullPath).Replace('\\', '/');
    }

    private static IReadOnlyList<JsonNode> ResolveArray(JsonNode? root)
    {
        if (root is JsonArray array)
        {
            return array.Where(node => node is not null).Cast<JsonNode>().ToArray();
        }

        if (root is JsonObject obj)
        {
            foreach (var name in new[] { "value", "items", "objects", "data", "result" })
            {
                if (obj[name] is JsonArray nestedArray)
                {
                    return nestedArray.Where(node => node is not null).Cast<JsonNode>().ToArray();
                }
            }

            return new[] { obj };
        }

        return Array.Empty<JsonNode>();
    }

    private static void WriteSpatialUnitApiRequest(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        ComputeReviewDispositionDocument disposition,
        OutputSummaryDocument? outputSummary,
        int requestedSpatialUnitCount)
    {
        var evidence = new JsonObject
        {
            ["schema_version"] = "spatial_unit_api_request_debug_v1",
            ["written_at_utc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["transaction_id"] = transaction.TransactionId,
            ["transaction_number"] = transaction.TransactionNumber,
            ["task_id"] = transaction.TaskId,
            ["decision"] = disposition.Decision,
            ["requested_spatial_unit_count"] = requestedSpatialUnitCount,
            ["output_summary_ref"] = ToCaseRelativePath(layout, disposition.OutputSummaryRef),
            ["output_polygon_count"] = outputSummary?.Payload.PolygonCount,
            ["output_built_parcel_count"] = outputSummary?.Payload.BuiltParcelCount,
            ["publish_run_id"] = disposition.PublishRunId
        };
        WriteWorkingEvidence(layout, "spatial_unit_api_request.json", evidence);
    }

    private static void WriteSpatialUnitApiPayload(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        IReadOnlyList<WorkingPolygonSpatialUnitAttributes> polygonAttributes,
        IReadOnlyList<JsonObject> populatedObjects)
    {
        var evidence = new JsonObject
        {
            ["schema_version"] = "spatial_unit_api_payload_debug_v1",
            ["written_at_utc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["transaction_id"] = transaction.TransactionId,
            ["transaction_number"] = transaction.TransactionNumber,
            ["task_id"] = transaction.TaskId,
            ["polygon_attribute_count"] = polygonAttributes.Count,
            ["payload_count"] = populatedObjects.Count,
            ["polygon_attributes"] = new JsonArray(polygonAttributes.Select(item => new JsonObject
            {
                ["pid"] = item.Pid,
                ["parish"] = item.Parish,
                ["area"] = item.Area,
                ["suid"] = item.Suid
            }).ToArray<JsonNode?>()),
            ["payload_preview"] = new JsonArray(populatedObjects.Select(item => item.DeepClone()).ToArray())
        };
        WriteWorkingEvidence(layout, "spatial_unit_api_payload.json", evidence);
    }

    private static void ApplyAddressParish(JsonObject spatialUnit)
    {
        var address = spatialUnit["address"] as JsonObject;
        if (address is null)
        {
            address = new JsonObject
            {
                ["@c"] = "AddressExt",
                ["country"] = "country_jm"
            };
            spatialUnit["address"] = address;
        }

        if (string.IsNullOrWhiteSpace(ReadString(address, "country")))
        {
            address["country"] = "country_jm";
        }

        address["parish"] = DefaultParishTypeKey;
        address["description"] = string.IsNullOrWhiteSpace(ReadString(address, "description"))
            ? DefaultParishDisplayName
            : ReadString(address, "description");
    }

    private static void WriteSpatialUnitApiResponse(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        int requestedSpatialUnitCount,
        int defaultObjectCount,
        int savedObjectCount,
        IReadOnlyList<JsonObject> savedObjects,
        IReadOnlyList<InnolaSpatialUnitPolygonReference> polygonReferences)
    {
        var evidence = new JsonObject
        {
            ["schema_version"] = "spatial_unit_api_response_debug_v1",
            ["written_at_utc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["transaction_id"] = transaction.TransactionId,
            ["transaction_number"] = transaction.TransactionNumber,
            ["task_id"] = transaction.TaskId,
            ["requested_spatial_unit_count"] = requestedSpatialUnitCount,
            ["default_object_count"] = defaultObjectCount,
            ["saved_object_count"] = savedObjectCount,
            ["returned_ids"] = new JsonArray(savedObjects.Select(obj => JsonValue.Create(ResolveSpatialUnitId(new[] { obj }))).ToArray<JsonNode?>()),
            ["returned_suids"] = new JsonArray(savedObjects.Select(obj => JsonValue.Create(
                ReadString(obj, "suid", "SUID", "spatialUnitSuid", "spatial_unit_suid")
                ?? ReadString(obj["spatialUnit"] as JsonObject, "suid", "SUID", "spatialUnitSuid", "spatial_unit_suid"))).ToArray<JsonNode?>()),
            ["polygon_references"] = new JsonArray(polygonReferences.Select(reference => new JsonObject
            {
                ["parcel_name"] = reference.ParcelName,
                ["spatial_unit_id"] = reference.SpatialUnitId,
                ["spatial_unit_suid"] = reference.SpatialUnitSuid
            }).ToArray<JsonNode?>())
        };
        WriteWorkingEvidence(layout, "spatial_unit_api_response.json", evidence);
    }

    private static void TryWriteSpatialUnitApiFailure(
        string caseFolderPath,
        SelectedInnolaTransaction transaction,
        Exception exception)
    {
        try
        {
            var layout = CaseFolderLayout.FromRootDirectory(caseFolderPath);
            WriteSpatialUnitApiFailure(layout, transaction, exception.GetType().Name, exception.Message);
        }
        catch (Exception traceException) when (traceException is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            Debug.WriteLine($"Could not write Spatial Unit API failure trace. Error={traceException.GetType().Name}.");
        }
    }

    private static void WriteSpatialUnitApiFailure(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        string errorCategory,
        string message)
    {
        var evidence = new JsonObject
        {
            ["schema_version"] = "spatial_unit_api_failure_debug_v1",
            ["written_at_utc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["transaction_id"] = transaction.TransactionId,
            ["transaction_number"] = transaction.TransactionNumber,
            ["task_id"] = transaction.TaskId,
            ["error_category"] = errorCategory,
            ["message"] = RedactForTrace(message)
        };
        WriteWorkingEvidence(layout, "spatial_unit_api_failure.json", evidence);
    }

    private static void WriteWorkingEvidence(CaseFolderLayout layout, string fileName, JsonObject evidence)
    {
        Directory.CreateDirectory(layout.WorkingDirectory);
        File.WriteAllText(Path.Combine(layout.WorkingDirectory, fileName), evidence.ToJsonString(TraceJsonOptions));
    }

    private static string RedactForTrace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var redacted = value
            .Replace("access_token", "access_token_redacted", StringComparison.OrdinalIgnoreCase)
            .Replace("password", "password_redacted", StringComparison.OrdinalIgnoreCase)
            .Replace("token", "token_redacted", StringComparison.OrdinalIgnoreCase);
        return redacted.Length > 1024 ? redacted[..1024] : redacted;
    }

    private static int ResolveSpatialUnitCount(OutputSummaryDocument? summary)
    {
        if (summary is null)
        {
            return 1;
        }

        var count = Math.Max(summary.Payload.PolygonCount, summary.Payload.BuiltParcelCount);
        return Math.Max(1, count);
    }

    private static string? ResolveSpatialUnitId(IEnumerable<JsonObject> objects)
    {
        foreach (var obj in objects)
        {
            var id = ReadString(obj, "id", "uid", "suid", "ladmId", "spatialUnitId")
                ?? ReadString(obj["link"] as JsonObject, "id", "uid")
                ?? ReadString(obj["spatialUnit"] as JsonObject, "id", "uid", "suid", "ladmId");
            if (!string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        return null;
    }

    private static IReadOnlyList<InnolaSpatialUnitPolygonReference> ResolvePolygonReferences(
        IReadOnlyList<JsonObject> savedObjects,
        IReadOnlyList<JsonObject> populatedObjects,
        IReadOnlyList<WorkingPolygonSpatialUnitAttributes> polygonAttributes)
    {
        var count = Math.Max(savedObjects.Count, Math.Max(populatedObjects.Count, polygonAttributes.Count));
        var references = new List<InnolaSpatialUnitPolygonReference>();
        for (var index = 0; index < count; index++)
        {
            var saved = index < savedObjects.Count ? savedObjects[index] : null;
            var populated = index < populatedObjects.Count ? populatedObjects[index] : null;
            var polygon = index < polygonAttributes.Count ? polygonAttributes[index] : null;
            var spatialUnitId = ResolveSpatialUnitId(new[] { saved, populated }.Where(item => item is not null).Cast<JsonObject>());
            var spatialUnitSuid = ReadString(saved, "suid", "SUID", "spatialUnitSuid", "spatial_unit_suid")
                ?? ReadString(saved?["spatialUnit"] as JsonObject, "suid", "SUID", "spatialUnitSuid", "spatial_unit_suid")
                ?? ReadString(populated, "suid", "SUID", "spatialUnitSuid", "spatial_unit_suid");
            var parcelName = polygon?.Pid
                ?? ReadString(populated, "PID", "pid", "parcelName", "parcel_name", "lot", "lotNo", "lot No.");

            if (string.IsNullOrWhiteSpace(spatialUnitId)
                && string.IsNullOrWhiteSpace(spatialUnitSuid)
                && string.IsNullOrWhiteSpace(parcelName))
            {
                continue;
            }

            references.Add(new InnolaSpatialUnitPolygonReference(parcelName, spatialUnitId, spatialUnitSuid));
        }

        return references;
    }

    private static string? ReadString(JsonObject? obj, params string[] names)
    {
        if (obj is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (obj[name] is null)
            {
                continue;
            }

            try
            {
                return obj[name]!.GetValue<string>();
            }
            catch (InvalidOperationException)
            {
                var value = obj[name]!.ToJsonString();
                return string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) ? null : value.Trim('"');
            }
        }

        return null;
    }

    private static double? ReadDouble(JsonObject? obj, params string[] names)
    {
        if (obj is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            var node = obj[name];
            if (node is null)
            {
                continue;
            }

            try
            {
                return node.GetValue<double>();
            }
            catch (InvalidOperationException)
            {
                var value = ReadString(obj, name);
                if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }
}

internal sealed record WorkingPolygonSpatialUnitAttributes(
    string? Pid,
    string? Parish,
    double? Area,
    string? Suid);
