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
            var currentPlanFetch = await FetchPlansAsync(session!, transaction.TransactionId, transaction.TransactionNumber, cancellationToken, layout, "plan_current").ConfigureAwait(false);
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

            var originating = await FindOriginatingTransactionAsync(session!, planNumber, transaction.TransactionNumber, cancellationToken, layout).ConfigureAwait(false);
            if (!originating.Success || string.IsNullOrWhiteSpace(originating.TransactionId))
            {
                WriteFailure(layout, transaction, originating.ErrorCategory ?? "originating_pe_unresolved", originating.Message);
                return RtExaminationLoadResult.Failed(originating.Message);
            }

            var originatingPlanFetch = await FetchPlansAsync(session!, originating.TransactionId, originating.TransactionNumber ?? planNumber, cancellationToken, layout, "plan_originating").ConfigureAwait(false);
            var originatingPlan = originatingPlanFetch.Plans.FirstOrDefault();
            var originatingPlanTrId = ReadString(originatingPlan, "trId", "transactionId", "transaction_id") ?? originating.TransactionId;
            var sources = await FetchLatestSourcesAsync(session!, originatingPlanTrId, transaction.TransactionId, cancellationToken, layout).ConfigureAwait(false);
            var spatialUnits = await FetchLatestSpatialUnitsAsync(session!, planNumber, cancellationToken, layout).ConfigureAwait(false);
            WriteLinkedTransactionLog(
                layout,
                transaction,
                currentPlanFetch,
                currentPlan,
                planNumber,
                originating,
                originatingPlanFetch,
                originatingPlan,
                originatingPlanTrId,
                sources,
                spatialUnits,
                settings);

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
            if (spatialUnits.Count == 0 && mapResult?.WorkingPolygonRows?.Count > 0)
            {
                spatialUnits = BuildSpatialUnitsFromWorkingPolygonRows(mapResult.WorkingPolygonRows);
                warnings.Add($"Spatial Units populated from {mapResult.WorkingPolygonRows.Count} working_review polygon row(s).");
            }
            WriteJson(layout, "rt_examination_spatialunits_latest.json", spatialUnits.Select(item => item.DeepClone()).ToArray());
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
            var spatialSummaries = BuildSpatialUnitSummaries(spatialUnits);
            var planCheckRows = ReadPlanCheckRows(currentPlan);
            WriteJson(layout, "rt_examination_context.json", context);
            WriteJson(layout, "rt_examination_review.json", new RtExaminationReviewDocument(
                "rt_examination_review_v1",
                DateTimeOffset.UtcNow,
                transaction.TransactionNumber,
                partyRows,
                Array.Empty<RtExaminationSpatialUnitAttribute>(),
                planCheckRows,
                null,
                session!.User.Username));

            return RtExaminationLoadResult.Succeeded(
                $"RT Examination loaded PE {originating.TransactionNumber ?? planNumber}: {sources.Count} source(s), {spatialUnits.Count} spatial unit(s).",
                context,
                partyRows,
                spatialSummaries,
                planCheckRows,
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
            var currentPlanFetch = await FetchPlansAsync(session!, request.Transaction.TransactionId, request.Transaction.TransactionNumber, cancellationToken).ConfigureAwait(false);
            var currentPlan = currentPlanFetch.Plans.FirstOrDefault();
            var linkedPlanNumber = currentPlan is null ? null : ReadString(currentPlan, "planNumber", "plan_number", "number");
            if (currentPlan is null || string.IsNullOrWhiteSpace(linkedPlanNumber))
            {
                WriteFailure(layout, request.Transaction, "plan_number_missing", "Current RT Plan is missing Plan.planNumber; linked Plan cannot be resolved for update.");
                return RtExaminationSaveResult.Failed("RT Examination save failed because the linked Plan number is missing.", "plan_number_missing");
            }

            foreach (var plan in currentPlanFetch.Plans)
            {
                await ApplyPartyRowsAsync(session!, request.Transaction.TransactionId, plan, request.PartyRows, layout, cancellationToken).ConfigureAwait(false);
                if (request.CompleteAfterSave)
                {
                    ApplyApprovedPlanCheck(plan, request.Transaction.TransactionNumber);
                }
                else
                {
                    PreservePlanCheckRows(plan);
                }
                if (!string.IsNullOrWhiteSpace(request.Observations))
                {
                    plan["rtObservations"] = request.Observations;
                }
            }
            var spatialUnits = LoadSpatialUnitArtifact(layout);
            var planCheckRows = request.CompleteAfterSave
                ? new[] { BuildApprovedPlanCheckRow(request.Transaction.TransactionNumber) }
                : request.PlanCheckRows;

            WriteJson(layout, "rt_examination_api_request.json", new
            {
                schema_version = "rt_examination_api_request_v1",
                written_at_utc = DateTimeOffset.UtcNow,
                transaction_id = request.Transaction.TransactionId,
                transaction_number = request.Transaction.TransactionNumber,
                party_row_count = request.PartyRows.Count,
                spatial_unit_attribute_count = 0,
                plan_check_row_count = request.CompleteAfterSave ? 1 : 0,
                complete_after_save = request.CompleteAfterSave
            });
            await SavePlansAsync(session!, request.Transaction.TransactionId, currentPlanFetch, layout, cancellationToken).ConfigureAwait(false);

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
                plan_count = currentPlanFetch.Plans.Count,
                spatial_unit_count = spatialUnits.Count,
                completed = request.CompleteAfterSave
            });
            WriteJson(layout, "rt_examination_review.json", new RtExaminationReviewDocument(
                "rt_examination_review_v1",
                DateTimeOffset.UtcNow,
                request.Transaction.TransactionNumber,
                request.PartyRows,
                Array.Empty<RtExaminationSpatialUnitAttribute>(),
                planCheckRows,
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

    private async Task<PlanFetchResult> FetchPlansAsync(InnolaSession session, string transactionId, string transactionNumber, CancellationToken cancellationToken, CaseFolderLayout? evidenceLayout = null, string? evidenceName = null)
    {
        foreach (var route in new[] { PlanApiRoute.DataObjects, PlanApiRoute.AdministrativeLadmObjects })
        {
            var body = await SendJsonAsync(session, HttpMethod.Get, BuildPlanPath(transactionId, route), null, transactionNumber, cancellationToken, evidenceLayout, evidenceName ?? "plan").ConfigureAwait(false);
            var bodyNode = JsonNode.Parse(body);
            var plans = ResolveObjects(bodyNode)
                .Where(item => string.Equals(ReadString(item, "@c"), "Plan", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(ReadString(item, "planNumber", "plan_number")))
                .ToArray();
            if (plans.Length > 0)
            {
                return new PlanFetchResult(transactionId, route, plans, bodyNode is JsonArray);
            }
        }

        return new PlanFetchResult(transactionId, PlanApiRoute.DataObjects, Array.Empty<JsonObject>(), true);
    }

    private async Task<OriginatingTransactionResult> FindOriginatingTransactionAsync(InnolaSession session, string planNumber, string currentTransactionNumber, CancellationToken cancellationToken, CaseFolderLayout? evidenceLayout = null)
    {
        var payload = JsonSerializer.Serialize(new { @c = "SearchRequest", searchKind = "transaction", @params = new { transactionNo = planNumber }, start = 0, limit = 25 });
        var body = await SendJsonAsync(session, HttpMethod.Post, $"{InnolaSettings.V4RestPath}portal/searches", payload, currentTransactionNumber, cancellationToken, evidenceLayout, "originating_transaction_search").ConfigureAwait(false);
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

    private async Task<IReadOnlyList<JsonObject>> FetchLatestSourcesAsync(InnolaSession session, string planTransactionId, string currentRtTransactionId, CancellationToken cancellationToken, CaseFolderLayout? evidenceLayout = null)
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

    private async Task<IReadOnlyList<JsonObject>> FetchLatestSpatialUnitsAsync(InnolaSession session, string planNumber, CancellationToken cancellationToken, CaseFolderLayout? evidenceLayout = null)
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

    private async Task SavePlansAsync(InnolaSession session, string transactionId, PlanFetchResult planFetch, CaseFolderLayout layout, CancellationToken cancellationToken)
    {
        var payload = BuildPlanSavePayload(planFetch).ToJsonString();
        var method = SaveMethodFor(planFetch.Route);
        var path = BuildPlanPath(transactionId, planFetch.Route);
        WriteJson(layout, "rt_examination_api_plan_save_request.json", new JsonObject
        {
            ["method"] = method.Method,
            ["endpoint"] = path,
            ["payload"] = JsonNode.Parse(payload)
        });
        await SendJsonAsync(
            session,
            method,
            path,
            payload,
            transactionId,
            cancellationToken,
            layout,
            "plan_save").ConfigureAwait(false);
    }
    private async Task<string> SendJsonAsync(InnolaSession session, HttpMethod method, string relativePath, string? payloadJson, string transactionNumber, CancellationToken cancellationToken, CaseFolderLayout? evidenceLayout = null, string? evidenceName = null)
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
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        WriteApiResponseEvidence(evidenceLayout, evidenceName, method, relativePath, response.StatusCode, responseBody);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"RT Examination {method.Method} {relativePath} failed: {response.StatusCode}");
        }

        return responseBody;
    }

    private static IReadOnlyList<RtExaminationPartyRow> ReadPartyRows(JsonObject plan)
    {
        var source = ResolveChildArray(plan, NeighborsPropertyName, "neighbor", "neighbours");
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

    private static JsonNode BuildPlanSavePayload(PlanFetchResult planFetch)
    {
        JsonNode payload;
        if (!planFetch.SaveAsArray && planFetch.Plans.Count == 1)
        {
            payload = planFetch.Plans[0].DeepClone();
        }
        else
        {
            payload = new JsonArray(planFetch.Plans.Select(plan => plan.DeepClone()).ToArray());
        }

        NormalizeObjectAliases(payload);
        return payload;
    }

    private static void NormalizeObjectAliases(JsonNode? node)
    {
        var nextAlias = 1;
        Visit(node, ref nextAlias);
    }

    private static void Visit(JsonNode? node, ref int nextAlias)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj.ContainsKey("@id"))
                {
                    obj["@id"] = $"obj:{nextAlias++}";
                }

                foreach (var child in obj.Select(pair => pair.Value).ToArray())
                {
                    Visit(child, ref nextAlias);
                }

                break;
            case JsonArray array:
                foreach (var child in array.ToArray())
                {
                    Visit(child, ref nextAlias);
                }

                break;
        }
    }

    private static IReadOnlyList<RtExaminationPlanCheckRow> ReadPlanCheckRows(JsonObject plan)
    {
        var source = ResolveChildArray(plan, "checkList");
        if (source is null)
        {
            return Array.Empty<RtExaminationPlanCheckRow>();
        }

        return source.OfType<JsonObject>()
            .Select(item => new RtExaminationPlanCheckRow(
                ReadString(item, "checkType"),
                ReadBool(item, "passed", "acceptable"),
                ReadString(item, "description")))
            .Where(item => !string.IsNullOrWhiteSpace(item.CheckType))
            .ToArray();
    }

    private async Task ApplyPartyRowsAsync(
        InnolaSession session,
        string transactionId,
        JsonObject plan,
        IReadOnlyList<RtExaminationPartyRow> partyRows,
        CaseFolderLayout layout,
        CancellationToken cancellationToken)
    {
        var existing = (ResolveCurrentChildArray(plan, NeighborsPropertyName, "neighbor", "neighbours") ?? new JsonArray())
            .OfType<JsonObject>()
            .Select(item => item.DeepClone().AsObject())
            .ToList();

        // Use current-transaction Neighbor objects. The original Plan is only a display/source snapshot.
        var rows = partyRows.Where(row => RtExaminationPartyRow.IsAllowedRole(row.Role)).ToArray();
        while (existing.Count < rows.Length)
        {
            existing.Add(await CreateDefaultNeighborAsync(session, transactionId, layout, cancellationToken).ConfigureAwait(false));
        }

        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            var item = existing[index];
            item["neighborType"] = string.Equals(row.Role, "Occupier", StringComparison.OrdinalIgnoreCase)
                ? "neighbor_type_occupier"
                : "neighbor_type_owner";
            item["name"] = row.Name;
            item["address"] = row.Address;
            item["volume"] = row.Volume;
            item["folio"] = row.Folio;
            item["lot"] = row.Lot;
            item["landValNumber"] = row.LandValNumber;
            item["examNumber"] = row.ExamNumber;
        }

        plan.Remove("neighbor");
        plan.Remove("neighbours");
        plan[NeighborsPropertyName] = new JsonArray(existing.Select(item => item.DeepClone()).ToArray());
    }

    private async Task<JsonObject> CreateDefaultNeighborAsync(
        InnolaSession session,
        string transactionId,
        CaseFolderLayout layout,
        CancellationToken cancellationToken)
    {
        const string payloadJson = "{\"@c\":\"Neighbor\",\"id\":null}";
        WriteJson(layout, "rt_examination_api_neighbor_create_request.json", new JsonObject
        {
            ["method"] = HttpMethod.Post.Method,
            ["endpoint"] = $"{InnolaSettings.V4RestPath}data/objects/create",
            ["payload"] = JsonNode.Parse(payloadJson)
        });
        var body = await SendJsonAsync(
            session,
            HttpMethod.Post,
            $"{InnolaSettings.V4RestPath}data/objects/create",
            payloadJson,
            transactionId,
            cancellationToken,
            layout,
            "neighbor_create").ConfigureAwait(false);
        return ResolveNeighborTemplate(JsonNode.Parse(body));
    }

    private static JsonObject ResolveNeighborTemplate(JsonNode? node)
    {
        var template = ResolveObject(node);
        if (template is null || !string.Equals(ReadString(template, "@c"), "Neighbor", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("RT Examination Neighbor create-template response did not contain a Neighbor object.");
        }

        return template.DeepClone() as JsonObject
            ?? throw new InvalidOperationException("RT Examination Neighbor create-template response could not be cloned.");
    }

    private static JsonObject? ResolveObject(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            if (obj["data"] is JsonArray dataArray)
            {
                return dataArray.OfType<JsonObject>().FirstOrDefault();
            }

            if (obj["data"] is JsonObject dataObject)
            {
                return dataObject;
            }

            return obj;
        }

        if (node is JsonArray array)
        {
            return array.OfType<JsonObject>().FirstOrDefault();
        }

        return null;
    }

    private static void PreservePlanCheckRows(JsonObject plan)
    {
        var existing = ResolveChildArray(plan, "checkList") ?? new JsonArray();
        plan["checkList"] = new JsonArray(existing.Select(item => item?.DeepClone()).ToArray());
    }

    private static void ApplyApprovedPlanCheck(JsonObject plan, string transactionNumber)
    {
        var existing = (ResolveChildArray(plan, "checkList") ?? new JsonArray())
            .OfType<JsonObject>()
            .Select(item => item.DeepClone().AsObject())
            .ToList();
        var check = existing.FirstOrDefault(item =>
            string.Equals(ReadString(item, "checkType"), "approved", StringComparison.OrdinalIgnoreCase))
            ?? existing.FirstOrDefault();
        if (check is null)
        {
            check = new JsonObject { ["@c"] = "PlanCheck" };
            existing.Add(check);
        }

        var approved = BuildApprovedPlanCheckRow(transactionNumber);
        check["checkType"] = approved.CheckType;
        check["passed"] = approved.Acceptable;
        check["description"] = approved.Description;
        plan["checkList"] = new JsonArray(existing.Select(item => item.DeepClone()).ToArray());
    }

    private static RtExaminationPlanCheckRow BuildApprovedPlanCheckRow(string transactionNumber)
    {
        return new RtExaminationPlanCheckRow(
            "approved",
            true,
            $"Updated from ArcGIS Pro TR {transactionNumber}.");
    }

    private static IReadOnlyList<RtExaminationSpatialUnitSummary> BuildSpatialUnitSummaries(IReadOnlyList<JsonObject> spatialUnits)
    {
        return spatialUnits
            .Select(unit => new RtExaminationSpatialUnitSummary(
                ReadString(unit, "parcel_name", "parcelName", "name"),
                ReadString(unit, "area_sqr", "areaSqr", "area"),
                ReadString(unit, "suid", "suId", "uid", "id", "@id"),
                ReadString(unit, "created_utc", "createdUtc", "createdAt", "created")))
            .ToArray();
    }

    private static IReadOnlyList<JsonObject> BuildSpatialUnitsFromWorkingPolygonRows(IReadOnlyList<IReadOnlyDictionary<string, string?>> polygonRows)
    {
        return polygonRows
            .Select((row, index) =>
            {
                var unit = new JsonObject();
                foreach (var pair in row)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        continue;
                    }

                    unit[pair.Key] = pair.Value is null ? null : JsonValue.Create(pair.Value);
                }

                if (string.IsNullOrWhiteSpace(ReadString(unit, "uid", "id", "@id")))
                {
                    unit["id"] = $"working_review_polygon_{index + 1}";
                }

                return unit;
            })
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
            JsonObject root when root["value"] is JsonObject valueObject => new JsonArray(valueObject.DeepClone()),
            JsonObject root when root["data"] is JsonArray data => data,
            JsonObject root when root["data"] is JsonObject dataObject => new JsonArray(dataObject.DeepClone()),
            JsonObject root when root["items"] is JsonArray items => items,
            JsonObject root when root["items"] is JsonObject itemObject => new JsonArray(itemObject.DeepClone()),
            JsonObject root when root["records"] is JsonArray records => records,
            JsonObject root when root["result"] is JsonArray result => result,
            JsonObject root when root["result"] is JsonObject resultObject => new JsonArray(resultObject.DeepClone()),
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

    private static string SourceLabel(JsonObject source)
    {
        return ReadString(source, "name", "sourceName", "type", "sourceType", "id") ?? "Source";
    }

    private static JsonArray? ResolveChildArray(JsonObject plan, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (plan[propertyName] is JsonArray array && array.Count > 0)
            {
                return new JsonArray(array.Select(item => item?.DeepClone()).ToArray());
            }
        }

        var original = ResolveOriginalPlan(plan);
        if (original is not null)
        {
            foreach (var propertyName in propertyNames)
            {
                if (original[propertyName] is JsonArray array && array.Count > 0)
                {
                    return new JsonArray(array.Select(item => item?.DeepClone()).ToArray());
                }
            }
        }

        foreach (var propertyName in propertyNames)
        {
            if (plan[propertyName] is JsonArray array)
            {
                return new JsonArray(array.Select(item => item?.DeepClone()).ToArray());
            }
        }

        return null;
    }

    private static JsonArray? ResolveCurrentChildArray(JsonObject plan, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (plan[propertyName] is JsonArray array)
            {
                return new JsonArray(array.Select(item => item?.DeepClone()).ToArray());
            }
        }

        return null;
    }

    private static JsonObject? ResolveOriginalPlan(JsonObject plan)
    {
        var originalText = ReadString(plan, "original");
        if (string.IsNullOrWhiteSpace(originalText))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(originalText) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool? ReadBool(JsonObject? item, params string[] names)
    {
        if (item is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (item.TryGetPropertyValue(name, out var value) && value is JsonValue jsonValue)
            {
                if (jsonValue.TryGetValue<bool>(out var boolean))
                {
                    return boolean;
                }

                if (jsonValue.TryGetValue<string>(out var text) && bool.TryParse(text, out boolean))
                {
                    return boolean;
                }
            }
        }

        return null;
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

    private static void WriteLinkedTransactionLog(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        PlanFetchResult currentPlanFetch,
        JsonObject currentPlan,
        string planNumber,
        OriginatingTransactionResult originating,
        PlanFetchResult originatingPlanFetch,
        JsonObject? originatingPlan,
        string originatingPlanTransactionId,
        IReadOnlyList<JsonObject> sources,
        IReadOnlyList<JsonObject> spatialUnits,
        RtExaminationSettings settings)
    {
        WriteJson(layout, "rt_examination_linked_transaction_log.json", new
        {
            schema_version = "rt_examination_linked_transaction_log_v1",
            traced_at_utc = DateTimeOffset.UtcNow,
            selected_rt_transaction = new
            {
                transaction_id = transaction.TransactionId,
                transaction_number = transaction.TransactionNumber,
                task_id = transaction.TaskId,
                task_name = transaction.TaskName,
                process_step = transaction.ProcessStep,
                transaction_type = transaction.TransactionType
            },
            current_plan_lookup = new
            {
                endpoint = "/api/v4/rest/data/objects?typeKeyId=plan&transactionId={selected_rt_transaction.transaction_id}",
                fallback_endpoint = "/api/v4/rest/administrative/ladm-objects?typeKeyId=plan&transactionId={selected_rt_transaction.transaction_id}",
                route_used = currentPlanFetch.Route.ToString(),
                lookup_transaction_id = currentPlanFetch.LookupTransactionId,
                plan_number_field = "Plan.planNumber",
                resolved_plan_number = planNumber,
                plan_object_fields = currentPlan.DeepClone()
            },
            originating_pe_transaction = new
            {
                search_endpoint = "POST /api/v4/rest/portal/searches",
                search_value = planNumber,
                transaction_id = originating.TransactionId,
                transaction_number = originating.TransactionNumber,
                plan_transaction_id_used_for_sources = originatingPlanTransactionId,
                originating_plan_route_used = originatingPlanFetch.Route.ToString(),
                originating_plan_object_fields = originatingPlan?.DeepClone()
            },
            latest_sources = new
            {
                endpoint = "/api/v4/rest/plan/sources/latest",
                count = sources.Count,
                object_fields = sources.Select(item => item.DeepClone()).ToArray()
            },
            linked_spatial_units = new
            {
                endpoint = "/api/v4/rest/plan/spatialunits/latest",
                count = spatialUnits.Count,
                object_fields = spatialUnits.Select(item => item.DeepClone()).ToArray()
            },
            working_review = new
            {
                usage = "additional review evidence only",
                query_field = settings.WorkingReviewPeNumberField,
                query_value = planNumber
            }
        });
    }
    private static void WriteApiResponseEvidence(CaseFolderLayout? layout, string? evidenceName, HttpMethod method, string relativePath, System.Net.HttpStatusCode statusCode, string responseBody)
    {
        if (layout is null || string.IsNullOrWhiteSpace(evidenceName))
        {
            return;
        }

        try
        {
            var parsedBody = JsonNode.Parse(responseBody) ?? new JsonObject { ["raw"] = responseBody };
            WriteJson(layout, $"rt_examination_api_{evidenceName}.json", new JsonObject
            {
                ["schema_version"] = "rt_examination_api_response_evidence_v1",
                ["written_at_utc"] = DateTimeOffset.UtcNow.ToString("O"),
                ["method"] = method.Method,
                ["endpoint"] = relativePath,
                ["status_code"] = (int)statusCode,
                ["status"] = statusCode.ToString(),
                ["body"] = parsedBody.DeepClone()
            });
        }
        catch
        {
            // Evidence must never prevent the RT workflow from reporting the API result.
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

    private sealed record PlanFetchResult(string LookupTransactionId, PlanApiRoute Route, IReadOnlyList<JsonObject> Plans, bool SaveAsArray);
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












