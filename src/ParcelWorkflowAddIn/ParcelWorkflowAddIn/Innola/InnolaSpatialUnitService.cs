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

            var defaults = await CreateDefaultSpatialUnitsAsync(
                session,
                transaction.TransactionId,
                spatialUnitCount,
                cancellationToken).ConfigureAwait(false);
            if (defaults.Count == 0)
            {
                return InnolaSpatialUnitSaveResult.Failed("Spatial Unit default creation did not return any objects.", "spatial_unit_defaults_empty");
            }

            var populated = defaults
                .Select(item => PopulateSpatialUnit(item, transaction, disposition, outputSummary))
                .ToArray();

            var saved = await SaveSpatialUnitsAsync(
                session,
                transaction.TransactionId,
                populated,
                cancellationToken).ConfigureAwait(false);
            var spatialUnitId = ResolveSpatialUnitId(saved) ?? ResolveSpatialUnitId(populated);
            return InnolaSpatialUnitSaveResult.Succeeded(spatialUnitId);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
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
        ComputeReviewDispositionDocument disposition,
        OutputSummaryDocument? outputSummary)
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
        spatialUnit["enterprisePublishRef"] = disposition.EnterprisePublishRef;
        spatialUnit["outputSummaryRef"] = disposition.OutputSummaryRef;
        spatialUnit["publishRunId"] = disposition.PublishRunId;

        if (outputSummary is not null)
        {
            spatialUnit["computeRunId"] = outputSummary.RunId;
            spatialUnit["parcelRecordName"] = outputSummary.Payload.ParcelRecordName;
            spatialUnit["parcelRecordId"] = outputSummary.Payload.ParcelRecordId;
            spatialUnit["parcelType"] = outputSummary.Payload.ParcelType;
            spatialUnit["computedPolygonCount"] = outputSummary.Payload.PolygonCount;
            spatialUnit["computedLineCount"] = outputSummary.Payload.LineCount;
            spatialUnit["computedPointCount"] = outputSummary.Payload.PointCount;

            if (string.IsNullOrWhiteSpace(ReadString(spatialUnit, "lot")) && !string.IsNullOrWhiteSpace(outputSummary.Payload.ParcelRecordName))
            {
                spatialUnit["lot"] = outputSummary.Payload.ParcelRecordName;
            }
        }

        return spatialUnit;
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
}
