using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Workflow.RtExamination;

public sealed class InnolaRtExaminationService : IRtExaminationLoadService, IRtExaminationWritebackService
{
    private const string PlanTypeKey = "plan";
    private const string SpatialUnitTypeKey = "spatialunit";
    private const string NeighborsPropertyName = "neighbors";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly HttpClient httpClient;
    private readonly Func<InnolaSession?> sessionProvider;
    private readonly Func<RtExaminationSettings> settingsProvider;
    private readonly Func<InnolaTransactionSettings> transactionSettingsProvider;
    private readonly IInnolaTransactionLifecycleService? lifecycleService;
    private readonly ICompareMapIntegrationService? mapIntegrationService;

    public InnolaRtExaminationService(
        HttpClient httpClient,
        Func<InnolaSession?> sessionProvider,
        Func<RtExaminationSettings> settingsProvider,
        IInnolaTransactionLifecycleService? lifecycleService = null,
        ICompareMapIntegrationService? mapIntegrationService = null,
        Func<InnolaTransactionSettings>? transactionSettingsProvider = null)
    {
        this.httpClient = httpClient;
        this.sessionProvider = sessionProvider;
        this.settingsProvider = settingsProvider;
        this.lifecycleService = lifecycleService;
        this.mapIntegrationService = mapIntegrationService;
        this.transactionSettingsProvider = transactionSettingsProvider ?? InnolaTransactionSettings.Load;
    }

    public async Task<RtExaminationLoadResult> LoadAsync(
        SelectedInnolaTransaction transaction,
        string caseFolderPath,
        CancellationToken cancellationToken = default)
    {
        var session = sessionProvider();
        if (!IsAuthorized(session))
        {
            return RtExaminationLoadResult.Failed("RT Examination requires an active Innola login.");
        }

        if (string.IsNullOrWhiteSpace(transaction.TransactionId))
        {
            return RtExaminationLoadResult.Failed("RT Examination cannot load because the current transaction id is missing.");
        }

        try
        {
            var layout = CaseFolderLayout.FromRootDirectory(caseFolderPath);
            var settings = settingsProvider();
            var currentPlanFetch = await FetchPlansAsync(session!, transaction.TransactionId, transaction.TransactionNumber, cancellationToken).ConfigureAwait(false);
            var currentPlan = currentPlanFetch.Plans.FirstOrDefault();
            if (currentPlan is null)
            {
                WriteFailure(layout, transaction, "plan_missing", "Current RT transaction did not return a Plan object.");
                return RtExaminationLoadResult.Failed("RT Examination could not find a Plan linked to the current transaction.");
            }

            var planNumber = ReadString(currentPlan, "planNumber", "plan_number", "number");
            if (string.IsNullOrWhiteSpace(planNumber))
            {
                WriteFailure(layout, transaction, "plan_number_missing", "Current RT Plan is missing Plan.planNumber.");
                return RtExaminationLoadResult.Failed("RT Examination cannot load linked PE data because Plan.planNumber is missing.");
            }

            var originating = await FindOriginatingTransactionAsync(session!, planNumber, transaction.TransactionNumber, cancellationToken).ConfigureAwait(false);
            if (!originating.Success || string.IsNullOrWhiteSpace(originating.TransactionId))
            {
                WriteFailure(layout, transaction, originating.ErrorCategory ?? "originating_pe_unresolved", originating.Message);
                return RtExaminationLoadResult.Failed(originating.Message);
            }

            var originatingPlanFetch = await FetchPlansAsync(session!, originating.TransactionId, originating.TransactionNumber ?? planNumber, cancellationToken).ConfigureAwait(false);
            var originatingPlan = originatingPlanFetch.Plans.FirstOrDefault();
            var originatingPlanTrId = ReadString(originatingPlan, "trId", "transactionId", "transaction_id") ?? originating.TransactionId;
            var sources = await FetchLatestSourcesAsync(session!, originatingPlanTrId, transaction.TransactionId, cancellationToken).ConfigureAwait(false);
            var spatialUnits = await FetchLatestSpatialUnitsAsync(session!, planNumber, cancellationToken).ConfigureAwait(false);
            WriteJson(layout, "rt_examination_spatialunits_latest.json", spatialUnits.Select(item => item.DeepClone()).ToArray());

            var warnings = new List<string>();
            if (sources.Count == 0)
            {
                warnings.Add("No linked PE sources were returned by Innola.");
            }
            if (spatialUnits.Count == 0)
            {
                warnings.Add("No linked PE SpatialUnits were returned by Innola.");
            }
            var mapResult = await LoadWorkingReviewGeometryAsync(settings, planNumber, transaction, cancellationToken).ConfigureAwait(false);
            if (mapResult is null)
            {
                warnings.Add($"working_review geometry load is unavailable in this runtime; query key is {settings.WorkingReviewPeNumberField} = {planNumber}.");
            }
            else if (!mapResult.Success)
            {
                warnings.Add($"working_review geometry was not loaded: {mapResult.Message}");
            }
            else
            {
                warnings.Add($"working_review geometry loaded by {settings.WorkingReviewPeNumberField} = {planNumber}; review-only.");
            }
            var loadedMapGroups = string.IsNullOrWhiteSpace(mapResult?.GroupLayerName)
                ? Array.Empty<string>()
                : new[] { mapResult!.GroupLayerName! };

            var context = new RtExaminationContextDocument(
                "rt_examination_context_v1",
                DateTimeOffset.UtcNow,
                transaction.TransactionId,
                transaction.TransactionNumber,
                transaction.TaskId,
                ReadString(currentPlan, "id"),
                ReadString(currentPlan, "uid"),
                ReadString(currentPlan, "trId"),
                ReadString(currentPlan, "trNo"),
                planNumber,
                originating.TransactionId,
                originating.TransactionNumber ?? planNumber,
                sources.Count,
                spatialUnits.Count,
                planNumber,
                warnings);

            var partyRows = ReadPartyRows(currentPlan);
            var spatialAttributes = BuildSpatialUnitAttributes(spatialUnits);
            WriteJson(layout, "rt_examination_context.json", context);
            WriteJson(layout, "rt_examination_review.json", new RtExaminationReviewDocument(
                "rt_examination_review_v1",
                DateTimeOffset.UtcNow,
                transaction.TransactionNumber,
                partyRows,
                spatialAttributes.Select(item => new RtExaminationSpatialUnitAttribute(item.SpatialUnitId, item.FieldName, item.OriginalValue, item.ReviewedValue)).ToArray(),
                null,
                session!.User.Username));

            return RtExaminationLoadResult.Succeeded(
                $"RT Examination loaded PE {originating.TransactionNumber ?? planNumber}: {sources.Count} source(s), {spatialUnits.Count} spatial unit(s).",
                context,
                partyRows,
                spatialAttributes,
                sources.Select(SourceLabel)
                    .Concat(new[] { $"working_review: {settings.WorkingReviewPeNumberField} = {planNumber}" })
                    .ToArray(),
                loadedMapGroups);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException or InvalidOperationException or UriFormatException or TaskCanceledException)
        {
            TryWriteFailure(caseFolderPath, transaction, exception.GetType().Name, exception.Message);
            return RtExaminationLoadResult.Failed("RT Examination linked PE data could not be loaded. Try again.");
        }
    }

    public async Task CleanupAsync(IReadOnlyList<string> loadedMapGroups, CancellationToken cancellationToken = default)
    {
        if (mapIntegrationService is null || loadedMapGroups.Count == 0)
        {
            return;
        }

        foreach (var groupName in loadedMapGroups.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await mapIntegrationService.RemoveTransactionGeometryFromActiveMapAsync(groupName, cancellationToken).ConfigureAwait(false);
        }
    }


    private async Task<CompareMapIntegrationResult?> LoadWorkingReviewGeometryAsync(
        RtExaminationSettings settings,
        string planNumber,
        SelectedInnolaTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (mapIntegrationService is null)
        {
            return null;
        }

        var enterpriseSettings = transactionSettingsProvider().EnterpriseWorkingReview;
        if (!enterpriseSettings.Enabled)
        {
            return CompareMapIntegrationResult.MapUnavailable("Enterprise working_review map loading is disabled.");
        }

        if (!enterpriseSettings.HasRequiredTargets)
        {
            return CompareMapIntegrationResult.MapUnavailable("Enterprise working_review layer targets are incomplete.");
        }

        var scopeField = settings.WorkingReviewPeNumberField.Trim();
        if (!IsSafeFieldName(scopeField))
        {
            return CompareMapIntegrationResult.Failed($"RT working_review field '{scopeField}' is not safe for a definition query.");
        }

        var scopeValue = planNumber.Trim();
        var definitionQuery = CompareWorkingGeometryService.BuildDefinitionQuery(scopeField, scopeValue);
        var layers = new[]
        {
            new CompareWorkingLayerRequest(CompareWorkingLayerRole.Polygons, enterpriseSettings.Layers.Polygons!, definitionQuery, true),
            new CompareWorkingLayerRequest(CompareWorkingLayerRole.Lines, enterpriseSettings.Layers.Lines!, definitionQuery, true),
            new CompareWorkingLayerRequest(CompareWorkingLayerRole.Points, enterpriseSettings.Layers.Points!, definitionQuery, true)
        };
        var plan = new CompareWorkingGeometryLoadPlan(
            true,
            transaction.TransactionId,
            transaction.TransactionNumber,
            CompareWorkingGeometryService.ResolvePortalUrl(enterpriseSettings.ServiceRoot),
            scopeField,
            scopeValue,
            definitionQuery,
            layers,
            null);

        try
        {
            return await mapIntegrationService.AddTransactionGeometryToActiveMapAsync(plan, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return CompareMapIntegrationResult.Failed($"RT working_review geometry could not be loaded: {exception.Message}");
        }
    }

    private static bool IsSafeFieldName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (!char.IsLetter(trimmed[0]) && trimmed[0] != '_')
        {
            return false;
        }

        return trimmed.All(character => char.IsLetterOrDigit(character) || character == '_');
    }
    public async Task<RtExaminationSaveResult> SaveAsync(
        RtExaminationSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = sessionProvider();
        if (!IsAuthorized(session))
        {
            return RtExaminationSaveResult.Failed("RT Examination save requires an active Innola login.", "unauthorized");
        }

        try
        {
            var layout = CaseFolderLayout.FromRootDirectory(request.CaseFolderPath);
            var planFetch = await FetchPlansAsync(session!, request.Transaction.TransactionId, request.Transaction.TransactionNumber, cancellationToken).ConfigureAwait(false);
            if (planFetch.Plans.Count == 0)
            {
                WriteFailure(layout, request.Transaction, "plan_missing", "Current RT transaction did not return a Plan object during save.");
                return RtExaminationSaveResult.Failed("RT Examination save failed because no current Plan object was returned.", "plan_missing");
            }

            foreach (var plan in planFetch.Plans)
            {
                ApplyPartyRows(plan, request.PartyRows);
                if (!string.IsNullOrWhiteSpace(request.Observations))
                {
                    plan["rtObservations"] = request.Observations;
                }
            }

            var spatialUnits = LoadSpatialUnitArtifact(layout);
            var spatialResult = await BranchAndSaveSpatialUnitsAsync(session!, request, layout, spatialUnits, cancellationToken).ConfigureAwait(false);
            if (!spatialResult.Success)
            {
                return spatialResult;
            }

            WriteJson(layout, "rt_examination_api_request.json", new
            {
                schema_version = "rt_examination_api_request_v1",
                written_at_utc = DateTimeOffset.UtcNow,
                transaction_id = request.Transaction.TransactionId,
                transaction_number = request.Transaction.TransactionNumber,
                party_row_count = request.PartyRows.Count,
                spatial_unit_attribute_count = request.SpatialUnitAttributes.Count,
                complete_after_save = request.CompleteAfterSave
            });
            await SavePlansAsync(session!, request.Transaction.TransactionId, planFetch, cancellationToken).ConfigureAwait(false);

            if (request.CompleteAfterSave)
            {
                if (lifecycleService is null)
                {
                    return RtExaminationSaveResult.Failed("RT Examination completion service is not configured.", "lifecycle_unavailable");
                }

                var settings = settingsProvider();
                var lifecycle = await lifecycleService.CompleteAsync(
                    new InnolaTransactionLifecycleRequest(
                        session!,
                        request.Transaction,
                        request.CaseFolderPath,
                        "in_progress",
                        "RT Examination saved and completed.",
                        settings.DesiredTransitionName),
                    cancellationToken).ConfigureAwait(false);
                if (!lifecycle.Success)
                {
                    var lifecycleMessage = string.IsNullOrWhiteSpace(lifecycle.Message) ? "RT Examination transaction completion failed." : lifecycle.Message;
                    WriteFailure(layout, request.Transaction, lifecycle.ErrorCategory ?? "lifecycle_failed", lifecycleMessage);
                    return RtExaminationSaveResult.Failed(lifecycleMessage, lifecycle.ErrorCategory);
                }
            }

            WriteJson(layout, "rt_examination_api_response.json", new
            {
                schema_version = "rt_examination_api_response_v1",
                written_at_utc = DateTimeOffset.UtcNow,
                transaction_id = request.Transaction.TransactionId,
                transaction_number = request.Transaction.TransactionNumber,
                plan_count = planFetch.Plans.Count,
                spatial_unit_count = spatialUnits.Count,
                completed = request.CompleteAfterSave
            });
            WriteJson(layout, "rt_examination_review.json", new RtExaminationReviewDocument(
                "rt_examination_review_v1",
                DateTimeOffset.UtcNow,
                request.Transaction.TransactionNumber,
                request.PartyRows,
                request.SpatialUnitAttributes,
                request.Observations,
                session!.User.Username));
            return RtExaminationSaveResult.Succeeded(request.CompleteAfterSave
                ? "RT Examination data saved and task completed."
                : "RT Examination data saved.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException or InvalidOperationException or UriFormatException or TaskCanceledException)
        {
            TryWriteFailure(request.CaseFolderPath, request.Transaction, exception.GetType().Name, exception.Message);
            return RtExaminationSaveResult.Failed("RT Examination writeback failed. Try again.", exception.GetType().Name);
        }
    }

    private async Task<RtExaminationSaveResult> BranchAndSaveSpatialUnitsAsync(
        InnolaSession session,
        RtExaminationSaveRequest request,
        CaseFolderLayout layout,
        IReadOnlyList<JsonObject> spatialUnits,
        CancellationToken cancellationToken)
    {
        if (spatialUnits.Count == 0 || request.SpatialUnitAttributes.Count == 0)
        {
            return RtExaminationSaveResult.Succeeded("No SpatialUnit attribute updates required.");
        }

        var updatesByUnit = request.SpatialUnitAttributes
            .Where(item => !string.IsNullOrWhiteSpace(item.SpatialUnitId) && RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute(item.FieldName))
            .GroupBy(item => item.SpatialUnitId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        if (updatesByUnit.Count == 0)
        {
            return RtExaminationSaveResult.Succeeded("No SpatialUnit attribute updates required.");
        }

        var missingUid = spatialUnits.FirstOrDefault(unit => string.IsNullOrWhiteSpace(ReadString(unit, "uid")));
        if (missingUid is not null)
        {
            WriteFailure(layout, request.Transaction, "spatial_unit_uid_missing", "A linked SpatialUnit lacks uid and cannot be branched into the RT transaction.");
            return RtExaminationSaveResult.Failed("RT Examination save failed because a linked SpatialUnit lacks uid.", "spatial_unit_uid_missing");
        }

        foreach (var unit in spatialUnits)
        {
            var key = SpatialUnitKey(unit);
            if (!updatesByUnit.TryGetValue(key, out var updates))
            {
                continue;
            }

            foreach (var update in updates)
            {
                unit[update.FieldName] = update.ReviewedValue;
            }
        }

        var uids = spatialUnits.Select(unit => ReadString(unit, "uid")!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var uidQuery = Uri.EscapeDataString("[" + string.Join(",", uids.Select(uid => $"\"{uid}\"")) + "]");
        await SendJsonAsync(
            session,
            HttpMethod.Post,
            $"{InnolaSettings.V4RestPath}administrative/ladm-objects/new-version-by-uid?typeKeyId={SpatialUnitTypeKey}&uids={uidQuery}&transactionId={Uri.EscapeDataString(request.Transaction.TransactionId)}",
            null,
            request.Transaction.TransactionNumber,
            cancellationToken).ConfigureAwait(false);
        await SendJsonAsync(
            session,
            HttpMethod.Post,
            $"{InnolaSettings.V4RestPath}administrative/ladm-objects?typeKeyId={SpatialUnitTypeKey}&transactionId={Uri.EscapeDataString(request.Transaction.TransactionId)}",
            new JsonArray(spatialUnits.Select(unit => unit.DeepClone()).ToArray()).ToJsonString(),
            request.Transaction.TransactionNumber,
            cancellationToken).ConfigureAwait(false);
        return RtExaminationSaveResult.Succeeded("SpatialUnit attributes saved.");
    }

    private async Task<PlanFetchResult> FetchPlansAsync(InnolaSession session, string transactionId, string transactionNumber, CancellationToken cancellationToken)
    {
        foreach (var route in new[] { PlanApiRoute.DataObjects, PlanApiRoute.AdministrativeLadmObjects })
        {
            var body = await SendJsonAsync(session, HttpMethod.Get, BuildPlanPath(transactionId, route), null, transactionNumber, cancellationToken).ConfigureAwait(false);
            var plans = ResolveObjects(body)
                .Where(item => string.Equals(ReadString(item, "@c"), "Plan", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(ReadString(item, "planNumber", "plan_number")))
                .ToArray();
            if (plans.Length > 0)
            {
                return new PlanFetchResult(transactionId, route, plans);
            }
        }

        return new PlanFetchResult(transactionId, PlanApiRoute.DataObjects, Array.Empty<JsonObject>());
    }

    private async Task<OriginatingTransactionResult> FindOriginatingTransactionAsync(InnolaSession session, string planNumber, string currentTransactionNumber, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { searchKind = "transaction", transactionNo = planNumber });
        var body = await SendJsonAsync(session, HttpMethod.Post, $"{InnolaSettings.V4RestPath}portal/searches", payload, currentTransactionNumber, cancellationToken).ConfigureAwait(false);
        var matches = ResolveObjects(body)
            .Select(item => new OriginatingTransactionMatch(
                ReadString(item, "id", "transactionId", "transaction_id"),
                ReadString(item, "transactionNo", "transactionNumber", "number")))
            .Where(item => !string.IsNullOrWhiteSpace(item.TransactionId))
            .DistinctBy(item => item.TransactionId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return matches.Length switch
        {
            1 => OriginatingTransactionResult.Succeeded(matches[0].TransactionId!, matches[0].TransactionNumber ?? planNumber),
            0 => OriginatingTransactionResult.Failed($"RT Examination could not resolve originating PE transaction {planNumber}.", "originating_pe_missing"),
            _ => OriginatingTransactionResult.Failed($"RT Examination found multiple originating PE transactions for {planNumber}; choose cannot be guessed.", "originating_pe_ambiguous")
        };
    }

    private async Task<IReadOnlyList<JsonObject>> FetchLatestSourcesAsync(InnolaSession session, string planTransactionId, string currentRtTransactionId, CancellationToken cancellationToken)
    {
        var body = await SendJsonAsync(
            session,
            HttpMethod.Get,
            $"{InnolaSettings.V4RestPath}plan/sources/latest?planTransactionId={Uri.EscapeDataString(planTransactionId)}&transactionId={Uri.EscapeDataString(currentRtTransactionId)}",
            null,
            currentRtTransactionId,
            cancellationToken).ConfigureAwait(false);
        return ResolveObjects(body);
    }

    private async Task<IReadOnlyList<JsonObject>> FetchLatestSpatialUnitsAsync(InnolaSession session, string planNumber, CancellationToken cancellationToken)
    {
        var planNumbers = Uri.EscapeDataString($"[\"{planNumber}\"]");
        var body = await SendJsonAsync(
            session,
            HttpMethod.Get,
            $"{InnolaSettings.V4RestPath}plan/spatialunits/latest?planNumbers={planNumbers}",
            null,
            planNumber,
            cancellationToken).ConfigureAwait(false);
        return ResolveObjects(body).ToArray();
    }

    private async Task SavePlansAsync(InnolaSession session, string transactionId, PlanFetchResult planFetch, CancellationToken cancellationToken)
    {
        var payload = planFetch.Plans.Count == 1 ? planFetch.Plans[0].ToJsonString() : new JsonArray(planFetch.Plans.Select(plan => plan.DeepClone()).ToArray()).ToJsonString();
        await SendJsonAsync(session, SaveMethodFor(planFetch.Route), BuildPlanPath(transactionId, planFetch.Route), payload, transactionId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> SendJsonAsync(InnolaSession session, HttpMethod method, string relativePath, string? payloadJson, string transactionNumber, CancellationToken cancellationToken)
    {
        using var response = await InnolaApiResilience.SendAsync(
            httpClient,
            new InnolaApiOperation("rt examination", InnolaApiRetryMode.VerifyBeforeRetry, transactionNumber, MaxAttempts: 1),
            () =>
            {
                var request = new HttpRequestMessage(method, InnolaHttp.BuildUri(session.ServerUrl, relativePath));
                InnolaHttp.ApplyAuthHeaders(request, session.AccessToken);
                if (payloadJson is not null)
                {
                    request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                }

                return request;
            },
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"RT Examination {method.Method} {relativePath} failed: {response.StatusCode}");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<RtExaminationPartyRow> ReadPartyRows(JsonObject plan)
    {
        var source = plan[NeighborsPropertyName] as JsonArray ?? plan["neighbor"] as JsonArray ?? plan["neighbours"] as JsonArray;
        if (source is null)
        {
            return Array.Empty<RtExaminationPartyRow>();
        }

        return source.OfType<JsonObject>()
            .Select(item => new RtExaminationPartyRow(
                ResolveRole(item),
                ReadString(item, "name"),
                ReadString(item, "address"),
                ReadString(item, "volume"),
                ReadString(item, "folio"),
                ReadString(item, "lot", "lotNumber"),
                ReadString(item, "landValNumber", "landValNo"),
                ReadString(item, "examNumber", "examinationNumber")))
            .Where(item => RtExaminationPartyRow.IsAllowedRole(item.Role))
            .GroupBy(item => item.DeduplicationKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static void ApplyPartyRows(JsonObject plan, IReadOnlyList<RtExaminationPartyRow> partyRows)
    {
        var existing = (plan[NeighborsPropertyName] as JsonArray ?? plan["neighbor"] as JsonArray ?? new JsonArray())
            .OfType<JsonObject>()
            .Select(item => item.DeepClone().AsObject())
            .ToList();
        var byKey = existing
            .Select(item => new { Key = PartyKey(item), Item = item })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Item, StringComparer.Ordinal);

        foreach (var row in partyRows.Where(row => RtExaminationPartyRow.IsAllowedRole(row.Role)))
        {
            var key = row.DeduplicationKey;
            if (!byKey.TryGetValue(key, out var item))
            {
                item = new JsonObject { ["@c"] = "Neighbor" };
                existing.Add(item);
                byKey[key] = item;
            }

            item["role"] = RtExaminationPartyRow.NormalizeRole(row.Role);
            item["name"] = row.Name;
            item["address"] = row.Address;
            item["volume"] = row.Volume;
            item["folio"] = row.Folio;
            item["lot"] = row.Lot;
            item["landValNumber"] = row.LandValNumber;
            item["examNumber"] = row.ExamNumber;
            item["neighborType"] = RoleToNeighborType(row.Role);
        }

        plan.Remove("neighbor");
        plan.Remove("neighbours");
        plan[NeighborsPropertyName] = new JsonArray(existing.Select(item => item.DeepClone()).ToArray());
    }

    private static IReadOnlyList<RtExaminationSpatialUnitAttributeViewModel> BuildSpatialUnitAttributes(IReadOnlyList<JsonObject> spatialUnits)
    {
        return spatialUnits
            .SelectMany(unit => unit
                .Where(pair => RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute(pair.Key) && IsPrimitive(pair.Value))
                .Select(pair => new RtExaminationSpatialUnitAttributeViewModel(SpatialUnitKey(unit), pair.Key, pair.Value?.ToString())))
            .ToArray();
    }

    private static IReadOnlyList<JsonObject> LoadSpatialUnitArtifact(CaseFolderLayout layout)
    {
        var path = Path.Combine(layout.WorkingDirectory, "rt_examination_spatialunits_latest.json");
        return File.Exists(path) ? ResolveObjects(File.ReadAllText(path)) : Array.Empty<JsonObject>();
    }

    private static IReadOnlyList<JsonObject> ResolveObjects(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return Array.Empty<JsonObject>();
        }

        var node = JsonNode.Parse(body);
        return ResolveObjects(node);
    }

    private static IReadOnlyList<JsonObject> ResolveObjects(JsonNode? node)
    {
        var array = node switch
        {
            JsonArray direct => direct,
            JsonObject root when root["value"] is JsonArray value => value,
            JsonObject root when root["data"] is JsonArray data => data,
            JsonObject root when root["items"] is JsonArray items => items,
            JsonObject root when root["records"] is JsonArray records => records,
            JsonObject root when root["result"] is JsonArray result => result,
            JsonObject single => new JsonArray(single.DeepClone()),
            _ => null
        };
        return array?.OfType<JsonObject>().ToArray() ?? Array.Empty<JsonObject>();
    }

    private static string BuildPlanPath(string transactionId, PlanApiRoute route)
    {
        return route == PlanApiRoute.DataObjects
            ? $"{InnolaSettings.V4RestPath}data/objects?typeKeyId={PlanTypeKey}&transactionId={Uri.EscapeDataString(transactionId)}"
            : $"{InnolaSettings.V4RestPath}administrative/ladm-objects?typeKeyId={PlanTypeKey}&transactionId={Uri.EscapeDataString(transactionId)}";
    }

    private static HttpMethod SaveMethodFor(PlanApiRoute route) => route == PlanApiRoute.DataObjects ? HttpMethod.Put : HttpMethod.Post;

    private static bool IsAuthorized(InnolaSession? session)
    {
        return session is not null && !string.IsNullOrWhiteSpace(session.ServerUrl) && !string.IsNullOrWhiteSpace(session.AccessToken);
    }

    private static bool IsPrimitive(JsonNode? value)
    {
        return value is null || value is JsonValue;
    }

    private static string SpatialUnitKey(JsonObject unit)
    {
        return ReadString(unit, "uid", "id", "@id") ?? "spatial-unit";
    }

    private static string PartyKey(JsonObject item)
    {
        return new RtExaminationPartyRow(
            ResolveRole(item),
            ReadString(item, "name"),
            ReadString(item, "address"),
            ReadString(item, "volume"),
            ReadString(item, "folio"),
            ReadString(item, "lot", "lotNumber"),
            ReadString(item, "landValNumber", "landValNo"),
            ReadString(item, "examNumber", "examinationNumber")).DeduplicationKey;
    }

    private static string ResolveRole(JsonObject item)
    {
        var role = ReadString(item, "role", "group", "partyRole");
        if (RtExaminationPartyRow.IsAllowedRole(role))
        {
            return RtExaminationPartyRow.NormalizeRole(role);
        }

        var neighborType = ReadString(item, "neighborType");
        if (neighborType?.Contains("representative", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "Representative";
        }

        if (neighborType?.Contains("occup", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "Occupier";
        }

        if (neighborType?.Contains("owner", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "Owner";
        }

        return "Neighbor";
    }

    private static string RoleToNeighborType(string role)
    {
        return RtExaminationPartyRow.NormalizeRole(role) switch
        {
            "Owner" => "neighbor_type_owner",
            "Occupier" => "neighbor_type_occupier",
            "Representative" => "neighbor_type_representative",
            _ => "neighbor_type_neighbor"
        };
    }

    private static string SourceLabel(JsonObject source)
    {
        return ReadString(source, "name", "sourceName", "type", "sourceType", "id") ?? "Source";
    }

    private static string? ReadString(JsonObject? item, params string[] names)
    {
        if (item is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (item.TryGetPropertyValue(name, out var value) && value is not null)
            {
                if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
                {
                    return text;
                }

                if (value is JsonValue)
                {
                    return value.ToString();
                }
            }
        }

        return null;
    }

    private static void TryWriteFailure(string caseFolderPath, SelectedInnolaTransaction transaction, string category, string message)
    {
        try
        {
            WriteFailure(CaseFolderLayout.FromRootDirectory(caseFolderPath), transaction, category, message);
        }
        catch
        {
        }
    }

    private static void WriteFailure(CaseFolderLayout layout, SelectedInnolaTransaction transaction, string category, string message)
    {
        WriteJson(layout, "rt_examination_api_failure.json", new
        {
            schema_version = "rt_examination_api_failure_v1",
            written_at_utc = DateTimeOffset.UtcNow,
            transaction_id = transaction.TransactionId,
            transaction_number = transaction.TransactionNumber,
            task_id = transaction.TaskId,
            error_category = category,
            error_message = Redact(message)
        });
    }

    private static void WriteJson(CaseFolderLayout layout, string fileName, object payload)
    {
        Directory.CreateDirectory(layout.WorkingDirectory);
        File.WriteAllText(Path.Combine(layout.WorkingDirectory, fileName), JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static string Redact(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var result = value;
        foreach (var marker in new[] { "Access-Token", "Bearer", "password", "cookie", "INNOLAID" })
        {
            if (result.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                result = "Sensitive diagnostic redacted. Retry or inspect safe API request artifacts.";
                break;
            }
        }

        return result.Length > 1000 ? result[..1000] : result;
    }

    private sealed record PlanFetchResult(string LookupTransactionId, PlanApiRoute Route, IReadOnlyList<JsonObject> Plans);
    private sealed record OriginatingTransactionMatch(string? TransactionId, string? TransactionNumber);
    private sealed record OriginatingTransactionResult(bool Success, string Message, string? TransactionId, string? TransactionNumber, string? ErrorCategory)
    {
        public static OriginatingTransactionResult Succeeded(string transactionId, string transactionNumber) => new(true, "Originating PE transaction resolved.", transactionId, transactionNumber, null);
        public static OriginatingTransactionResult Failed(string message, string errorCategory) => new(false, message, null, null, errorCategory);
    }

    private enum PlanApiRoute
    {
        DataObjects,
        AdministrativeLadmObjects
    }
}