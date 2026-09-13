using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Workflow.RtExamination;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Tests.Innola;

internal static class RtExaminationTests
{
    public static void SettingsDefaultsExposeRtExaminationStage()
    {
        var settings = InnolaTransactionSettings.Default.RtExamination;

        TestAssert.True(settings.Enabled, "RT Examination should be enabled by default.");
        TestAssert.Equal("In RT Examination", settings.StageName, "Default RT stage mismatch.");
        TestAssert.Equal("RT Examination", settings.SubworkflowName, "Default RT subworkflow mismatch.");
        TestAssert.Equal("transaction_number", settings.WorkingReviewPeNumberField, "Default working_review transaction field mismatch.");
    }

    public static void StageRouterRecognizesRtExaminationIndependentlyOfTransactionType()
    {
        var route = ParcelWorkflowStageRouter.Resolve(
            "In RT Examination",
            new[] { "Compute Survey Plan" },
            new[] { "Compare Survey Plan" },
            RtExaminationSettings.Default);

        TestAssert.Equal(ParcelWorkflowStageRoute.RtExamination, route, "In RT Examination should route to the RT workspace.");
    }

    public static void PartyRolesAreConstrainedForRtReview()
    {
        var roles = RtExaminationPartyRow.AllowedRoles;

        TestAssert.True(roles.Contains("Owner"), "Owner role missing.");
        TestAssert.True(roles.Contains("Occupier"), "Occupier role missing.");
        TestAssert.False(RtExaminationPartyRow.IsAllowedRole("Applicant"), "Unexpected Applicant role should not be allowed.");
    }

    public static void SpatialUnitEditableFieldsExcludeGeometry()
    {
        TestAssert.True(RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute("landValNumber"), "landValNumber should be editable.");
        TestAssert.True(RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute("examNumber"), "examNumber should be editable.");
        TestAssert.False(RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute("geometry"), "geometry must not be editable.");
        TestAssert.False(RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute("coordinates"), "coordinates must not be editable.");
        TestAssert.False(RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute("bfsMinus"), "boundary fields must not be editable.");
    }


    public static void WindowXamlExposesReviewTabsEditableColumnsAndActions()
    {
        var source = File.ReadAllText(FindSourceFile("RtExaminationWindow.xaml"));

        foreach (var expected in new[]
        {
            "Header=\"Context\"",
            "Header=\"Neighbors / Parties\"",
            "Header=\"Spatial Units\"",
            "Header=\"Plan Check\"",
            "Header=\"Sources / Map Evidence\"",
            "LoadLinkedPeDataCommand",
            "SaveCommand",
            "ItemsSource=\"{Binding CompletionDecisions}\"",
            "SelectedItem=\"{Binding SelectedCompletionDecision, Mode=TwoWay}\"",
            "DisplayMemberPath=\"Label\"",
            "Text=\"Save\"",
            "Text=\"Decision to:\"",
            "Text=\"Next Stage\"",
            "BooleanToVisibilityConverter",
            "ProgressBar",
            "IsIndeterminate=\"True\"",
            "Visibility=\"{Binding IsBusy, Converter={StaticResource BoolToVisibility}}\"",
            "Images/RTIcons/LoadSpatialData.png",
            "Images/RTIcons/Save.png",
            "Images/RTIcons/SaveClose.png",
            "Images/RTIcons/Cancel.png",
            "Header=\"Role\"",
            "Header=\"Address\"",
            "Header=\"LandVal No.\"",
            "Header=\"Exam No.\"",
            "Header=\"Check Type\"",
            "Header=\"Acceptable\"",
            "Header=\"Description\"",
            "Header=\"parcel_name\"",
            "Header=\"area_sqr\"",
            "Header=\"suid\"",
            "Header=\"created_utc\"",
            "ItemsSource=\"{Binding PlanCheckRows}\"",
            "ItemsSource=\"{Binding SpatialUnits}\""
        })
        {
            TestAssert.True(source.Contains(expected, StringComparison.Ordinal), $"RT Examination window is missing expected surface: {expected}.");
        }

        TestAssert.True(
            source.Contains("ComboBox ItemsSource=\"{Binding AllowedRoles}\"", StringComparison.Ordinal),
            "RT role editing should bind the combo list from each editable party row.");
    }

    public static void WindowChromeCloseRoutesThroughCancelCommand()
    {
        var source = File.ReadAllText(FindSourceFile("RtExaminationWindow.xaml.cs"));

        foreach (var expected in new[]
        {
            "Closing += OnClosing;",
            "e.Cancel = true;",
            "viewModel.CancelCommand.CanExecute(null)",
            "viewModel.CancelCommand.Execute(null)",
            "allowClose = true;"
        })
        {
            TestAssert.True(source.Contains(expected, StringComparison.Ordinal), $"RT Examination window close synchronization is missing: {expected}.");
        }

        TestAssert.False(source.Contains("closeRequestedFromChrome", StringComparison.Ordinal), "RT chrome close should not use a sticky guard that suppresses later cancel prompts.");
    }

    public static async Task LoadPopulatesSpatialUnitsFromLinkedPeCaseFolderWhenApiAndMapReturnNone()
    {
        using var tempRoot = new TempDirectory();
        var linkedPeLayout = CaseFolderLayout.For(tempRoot.Path, "100000623");
        Directory.CreateDirectory(linkedPeLayout.WorkingDirectory);
        File.WriteAllText(
            Path.Combine(linkedPeLayout.WorkingDirectory, "spatial_unit_api_response.json"),
            """
            {
              "written_at_utc": "2026-09-04T01:00:01.1549790Z",
              "polygon_references": [
                {
                  "parcel_name": "parcel-001",
                  "spatial_unit_id": "01a069ee-011c-70cb-9d59-c5b21dbc057a",
                  "spatial_unit_suid": "S100284154"
                }
              ]
            }
            """);
        var linkedGdbPath = Path.Combine(linkedPeLayout.OutputDirectory, "100000623_parcel_output.gdb");
        Directory.CreateDirectory(linkedGdbPath);
        var linkedGdbJson = JsonSerializer.Serialize(linkedGdbPath);
        var pointPathJson = JsonSerializer.Serialize(Path.Combine(linkedGdbPath, "parcel_points"));
        var linePathJson = JsonSerializer.Serialize(Path.Combine(linkedGdbPath, "parcel_lines"));
        var polygonPathJson = JsonSerializer.Serialize(Path.Combine(linkedGdbPath, "parcel_polygons"));
        File.WriteAllText(
            Path.Combine(linkedPeLayout.OutputDirectory, "output_summary.json"),
            $$"""
            {
              "schema_version": "1.0.0",
              "transaction_id": "100000623",
              "run_id": "run-test",
              "created_at": "2026-09-04T01:00:00Z",
              "created_by": "test",
              "source_manifest_hash": "",
              "payload": {
                "status": "completed",
                "review_workspace_mode": "standard",
                "result_gdb_path": {{linkedGdbJson}},
                "artifact_paths": [],
                "map_layer_paths": [
                  {{pointPathJson}},
                  {{linePathJson}},
                  {{polygonPathJson}}
                ],
                "point_feature_class_path": {{pointPathJson}},
                "line_feature_class_path": {{linePathJson}},
                "polygon_feature_class_path": {{polygonPathJson}},
                "built_parcel_count": 1,
                "built_line_count": 12,
                "built_point_count": 12,
                "point_count": 12,
                "line_count": 12,
                "polygon_count": 1
              },
              "warnings": [],
              "errors": []
            }
            """);

        var currentPlan = """
            [
              {
                "@c": "Plan",
                "id": "current-plan",
                "uid": "current-plan-uid",
                "planNumber": "100000623",
                "trId": "tx-current-rt",
                "trNo": "100001033",
                "neighbors": [],
                "checkList": []
              }
            ]
            """;
        var originatingSearch = """
            [
              { "id": "tx-pe-100000623", "transactionNo": "100000623" }
            ]
            """;
        var originatingPlan = """
            [
              {
                "@c": "Plan",
                "id": "originating-plan",
                "uid": "originating-plan-uid",
                "planNumber": "100000623",
                "trId": "tx-pe-100000623",
                "trNo": "100000623"
              }
            ]
            """;
        var handler = new CapturingHttpMessageHandler(
            currentPlan,
            originatingSearch,
            originatingPlan,
            "[]",
            "[]");
        using var httpClient = new HttpClient(handler);
        var map = new FixedMapIntegrationService(CompareMapIntegrationResult.MapUnavailable("No active Enterprise portal."));
        var transactionSettings = InnolaTransactionSettings.Default with
        {
            CaseFolderOutputRoot = tempRoot.Path,
            EnterpriseWorkingReview = new EnterpriseWorkingReviewSettings(
                true,
                "https://enterprise.example/server/rest",
                "sidwell_working_review",
                EnterpriseWorkingReviewSettings.PublishBehaviorReplaceTransactionScope,
                EnterpriseWorkingReviewSettings.PublishTimingOnComplete,
                EnterpriseWorkingReviewSettings.RestoreBehaviorPreferLocalThenEnterprise,
                true,
                "transaction_number",
                new EnterpriseWorkingLayerTargets(
                    "https://enterprise.example/FeatureServer/1",
                    "https://enterprise.example/FeatureServer/2",
                    "https://enterprise.example/FeatureServer/3",
                    null,
                    "https://enterprise.example/FeatureServer/4"),
                null)
        };
        var service = new InnolaRtExaminationService(
            httpClient,
            () => CreateSession(),
            () => RtExaminationSettings.Default,
            mapIntegrationService: map,
            transactionSettingsProvider: () => transactionSettings);
        var rtCaseFolderPath = Path.Combine(tempRoot.Path, "100001033");
        Directory.CreateDirectory(Path.Combine(rtCaseFolderPath, "working"));

        var result = await service.LoadAsync(
            new SelectedInnolaTransaction(
                "task-rt-100001033",
                "tx-current-rt",
                "100001033",
                "In RT Examination",
                "parcel_workflow",
                DateTimeOffset.UtcNow,
                TransactionType: "First Registration"),
            rtCaseFolderPath);

        TestAssert.True(result.Success, $"RT load should succeed from linked PE local spatial unit artifact. Message={result.Message}");
        TestAssert.Equal(1, result.SpatialUnits.Count, "RT Spatial Units should be populated from the linked PE case folder when API and map return none.");
        TestAssert.Equal("parcel-001", result.SpatialUnits[0].ParcelName, "Local PE spatial unit parcel name should populate the RT grid.");
        TestAssert.Equal("S100284154", result.SpatialUnits[0].Suid, "Local PE spatial unit SUID should populate the RT grid.");
        TestAssert.Equal(3, map.LastPlan?.LocalFallbackLayerPaths?.Count ?? 0, "RT map load should carry linked PE local output layer paths as a fallback.");
        TestAssert.True(
            map.LastPlan!.LocalFallbackLayerPaths!.Any(path => path.EndsWith("parcel_polygons", StringComparison.OrdinalIgnoreCase)),
            "RT local map fallback should include the linked PE polygon feature class.");
        TestAssert.True(result.Context!.Warnings.Any(warning => warning.Contains("linked PE case folder 100000623", StringComparison.OrdinalIgnoreCase)), "RT context should explain the local PE fallback.");
    }

    private static string FindSourceFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "ParcelWorkflowAddIn",
                "ParcelWorkflowAddIn",
                fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {fileName} from the test output directory.");
    }

    public static void DuplicatePartyRowsUseDeterministicRtKey()
    {
        var first = new RtExaminationPartyRow("Owner", "A Brown", "1 King St", "1158", "604", "7", "LV-1", "EX-1");
        var duplicate = new RtExaminationPartyRow("owner", " A Brown ", "1 King St", "1158", "604", "7", "LV-1", "EX-1");
        var different = first with { Folio = "605" };

        TestAssert.Equal(first.DeduplicationKey, duplicate.DeduplicationKey, "Equivalent RT party rows should have the same dedupe key.");
        TestAssert.True(!string.Equals(first.DeduplicationKey, different.DeduplicationKey, StringComparison.Ordinal), "Different RT party rows should not collapse.");
    }

    public static async Task SaveUpdatesCurrentAdministrativePlanRowsInPlace()
    {
        var originalPlan = """
            {
              "neighbors": [
                { "id": "neighbor-1", "neighborType": "owner", "name": "Old Owner", "address": "Old Address", "volume": "1", "folio": "2", "lot": "3", "landValNumber": "4", "examNumber": "5" },
                { "id": "neighbor-2", "neighborType": "occupier", "name": "Old Occupier", "address": "Old Address 2" }
              ],
              "checkList": [
                { "id": "check-1", "checkType": "plan_check_type_area", "passed": false, "description": "Original area note" },
                { "id": "check-2", "checkType": "plan_check_type_title", "passed": true, "description": "Keep title note" }
              ]
            }
            """;
        var currentPlan = $$"""
            [
              {
                "@c": "Plan",
                "id": "current-plan",
                "uid": "current-plan-uid",
                "planNumber": "100000749",
                "trId": "tx-current-rt",
                "trNo": "100000854",
                "neighbors": [],
                "checkList": [],
                "original": {{JsonSerializer.Serialize(originalPlan)}}
              }
            ]
            """;
        var handler = new CapturingHttpMessageHandler(
            "[]",
            currentPlan,
            """
            { "@c": "Neighbor", "id": "created-neighbor-1", "neighborType": "neighbor_type_owner", "allowRead": true, "allowWrite": true }
            """,
            """
            { "@c": "Neighbor", "id": "created-neighbor-2", "neighborType": "neighbor_type_owner", "allowRead": true, "allowWrite": true }
            """,
            """{ "status": "ok" }""");
        using var httpClient = new HttpClient(handler);
        var service = new InnolaRtExaminationService(
            httpClient,
            () => CreateSession(),
            () => RtExaminationSettings.Default,
            transactionSettingsProvider: () => InnolaTransactionSettings.Default);
        var caseFolderPath = Path.Combine(Path.GetTempPath(), "rt-exam-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(caseFolderPath, "working"));

        var result = await service.SaveAsync(new RtExaminationSaveRequest(
            CreateTransaction(),
            caseFolderPath,
            new[]
            {
                new RtExaminationPartyRow("Occupier", "Henry Natheson", "Sherwood Content", "1", "", "", "", ""),
                new RtExaminationPartyRow("Owner", "Adolphus Boland", "Sherwood Content", "", "2", "", "4", "")
            },
            Array.Empty<RtExaminationSpatialUnitAttribute>(),
            new[] { new RtExaminationPlanCheckRow("plan_check_type_area", true, "Area accepted.") },
            null,
            false));

        TestAssert.True(result.Success, "RT save should succeed against the current administrative Plan route.");
        TestAssert.Equal(5, handler.Requests.Count, "RT save should fetch data, fetch administrative, create current Neighbor rows, then save administrative.");
        TestAssert.True(handler.Requests[2].Uri.PathAndQuery.Contains("/api/v4/rest/data/objects/create", StringComparison.Ordinal), "RT save should create current-transaction Neighbor child rows when the current Plan has none.");
        TestAssert.True(handler.Requests[3].Uri.PathAndQuery.Contains("/api/v4/rest/data/objects/create", StringComparison.Ordinal), "RT save should create one current Neighbor child row for each missing reviewed row.");
        using (var createDocument = JsonDocument.Parse(handler.Requests[2].Body!))
        {
            TestAssert.Equal("Neighbor", createDocument.RootElement.GetProperty("@c").GetString(), "RT Neighbor create-template body should request a Neighbor object.");
            TestAssert.Equal(JsonValueKind.Null, createDocument.RootElement.GetProperty("id").ValueKind, "RT Neighbor create-template body should include id:null for Innola.");
        }
        TestAssert.Equal(HttpMethod.Post.Method, handler.Requests[4].Method.Method, "Administrative Plan save must use POST.");
        TestAssert.True(handler.Requests[4].Uri.PathAndQuery.Contains("/api/v4/rest/administrative/ladm-objects?typeKeyId=plan", StringComparison.Ordinal), "RT save should use the administrative Plan endpoint.");
        TestAssert.True(handler.Requests[4].Uri.PathAndQuery.Contains("transactionId=tx-current-rt", StringComparison.Ordinal), "RT save should target the current RT transaction id.");

        using var document = JsonDocument.Parse(handler.Requests[4].Body!);
        TestAssert.Equal(JsonValueKind.Array, document.RootElement.ValueKind, "Administrative Plan save must preserve the array shape returned by the GET route.");
        var root = document.RootElement[0];
        var neighbors = root.GetProperty("neighbors");
        TestAssert.Equal(2, neighbors.GetArrayLength(), "RT neighbor writeback must update existing rows without adding or removing rows.");
        TestAssert.Equal("created-neighbor-1", neighbors[0].GetProperty("id").GetString(), "New current-transaction Neighbor identity should come from Innola create-template.");
        AssertUniqueObjectAliases(root);
        TestAssert.Equal("neighbor_type_occupier", neighbors[0].GetProperty("neighborType").GetString(), "Edited neighbor role should update in place.");
        TestAssert.Equal("Henry Natheson", neighbors[0].GetProperty("name").GetString(), "Edited neighbor name should update in place.");
        TestAssert.Equal("created-neighbor-2", neighbors[1].GetProperty("id").GetString(), "Second new current-transaction Neighbor identity should come from Innola create-template.");
        TestAssert.Equal("Adolphus Boland", neighbors[1].GetProperty("name").GetString(), "Second edited neighbor should update in place.");

        var checkList = root.GetProperty("checkList");
        TestAssert.Equal(2, checkList.GetArrayLength(), "RT Save must preserve existing Plan Check rows without add/remove.");
        TestAssert.False(checkList[0].GetProperty("passed").GetBoolean(), "RT Save must not update Plan Check acceptable values.");
        TestAssert.Equal("Original area note", checkList[0].GetProperty("description").GetString(), "RT Save must not update Plan Check descriptions.");
        TestAssert.Equal("Keep title note", checkList[1].GetProperty("description").GetString(), "Unedited Plan Check rows should be preserved.");
    }

    public static async Task SaveAndCloseApprovesPlanCheckAndCompletesWorkflow()
    {
        var currentPlan = """
            [
              {
                "@c": "Plan",
                "id": "current-plan",
                "uid": "current-plan-uid",
                "planNumber": "100000749",
                "trId": "tx-current-rt",
                "trNo": "100000854",
                "neighbors": [
                  { "id": "neighbor-1", "neighborType": "owner", "name": "Old Owner" }
                ],
                "checkList": [
                  { "@c": "PlanCheck", "id": "check-1", "checkType": "plan_check_type_general", "passed": false, "description": "Original note" }
                ]
              }
            ]
            """;
        var handler = new CapturingHttpMessageHandler(
            "[]",
            currentPlan,
            """{ "status": "ok" }""");
        var lifecycle = new CapturingLifecycleService();
        using var httpClient = new HttpClient(handler);
        var service = new InnolaRtExaminationService(
            httpClient,
            () => CreateSession(),
            () => RtExaminationSettings.Default,
            lifecycle,
            transactionSettingsProvider: () => InnolaTransactionSettings.Default);
        var caseFolderPath = Path.Combine(Path.GetTempPath(), "rt-exam-close-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(caseFolderPath, "working"));

        var result = await service.SaveAsync(new RtExaminationSaveRequest(
            CreateTransaction(),
            caseFolderPath,
            new[] { new RtExaminationPartyRow("Owner", "Mary Owner", "", "", "", "", "", "") },
            Array.Empty<RtExaminationSpatialUnitAttribute>(),
            Array.Empty<RtExaminationPlanCheckRow>(),
            null,
            true,
            RtExaminationCompletionDecision.DefaultOptions[0]));

        TestAssert.True(result.Success, "Save & Close should save and complete.");
        TestAssert.True(lifecycle.Completed, "Save & Close must move the Innola workflow to the next step.");
        TestAssert.Equal("Review Completed RT Examination", lifecycle.LastRequest?.DesiredTransitionName, "Save & Close should use the selected RT transition target.");
        using var document = JsonDocument.Parse(handler.Requests[2].Body!);
        TestAssert.Equal(JsonValueKind.Array, document.RootElement.ValueKind, "Administrative Plan Save & Close must preserve the array shape returned by the GET route.");
        var check = document.RootElement[0].GetProperty("checkList")[0];
        TestAssert.Equal("plan_check_type_general", check.GetProperty("checkType").GetString(), "Save & Close should preserve Innola's Plan Check type key.");
        TestAssert.True(check.GetProperty("passed").GetBoolean(), "Save & Close should set Plan Check acceptable/passed to true.");
        TestAssert.Equal("Updated from ArcGIS Pro TR 100000854.", check.GetProperty("description").GetString(), "Save & Close should write the ArcGIS Pro transaction description.");
    }

    public static async Task SaveAndCloseCreatesCurrentPlanCheckWhenCurrentListIsEmpty()
    {
        var originalPlan = """
            {
              "checkList": [
                { "@c": "PlanCheck", "id": "original-check", "checkType": "plan_check_type_area", "passed": false, "description": "Original PE note" }
              ]
            }
            """;
        var currentPlan = $$"""
            [
              {
                "@c": "Plan",
                "id": "current-plan",
                "uid": "current-plan-uid",
                "planNumber": "100000749",
                "trId": "tx-current-rt",
                "trNo": "100000854",
                "neighbors": [],
                "checkList": [],
                "original": {{JsonSerializer.Serialize(originalPlan)}}
              }
            ]
            """;
        var handler = new CapturingHttpMessageHandler(
            "[]",
            currentPlan,
            """
            { "@c": "PlanCheck", "id": "created-plan-check", "allowRead": true, "allowWrite": true }
            """,
            """{ "status": "ok" }""");
        var lifecycle = new CapturingLifecycleService();
        using var httpClient = new HttpClient(handler);
        var service = new InnolaRtExaminationService(
            httpClient,
            () => CreateSession(),
            () => RtExaminationSettings.Default,
            lifecycle,
            transactionSettingsProvider: () => InnolaTransactionSettings.Default);
        var caseFolderPath = Path.Combine(Path.GetTempPath(), "rt-exam-close-empty-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(caseFolderPath, "working"));

        var result = await service.SaveAsync(new RtExaminationSaveRequest(
            CreateTransaction(),
            caseFolderPath,
            Array.Empty<RtExaminationPartyRow>(),
            Array.Empty<RtExaminationSpatialUnitAttribute>(),
            Array.Empty<RtExaminationPlanCheckRow>(),
            null,
            true,
            RtExaminationCompletionDecision.DefaultOptions[1]));

        TestAssert.True(result.Success, $"Save & Close should create a current PlanCheck row when checkList is empty. Error: {result.Message}");
        TestAssert.True(lifecycle.Completed, "Save & Close must complete after successful PlanCheck save.");
        TestAssert.Equal("Prepare Plan Pre-Check Log Sheet", lifecycle.LastRequest?.DesiredTransitionName, "Save & Close should support the pre-check log sheet branch.");
        TestAssert.Equal(4, handler.Requests.Count, "RT Save & Close should fetch data, fetch administrative, create PlanCheck, then save administrative.");
        TestAssert.True(handler.Requests[2].Uri.PathAndQuery.Contains("/api/v4/rest/data/objects/create", StringComparison.Ordinal), "RT Save & Close should create a PlanCheck child row through Innola.");
        using (var createDocument = JsonDocument.Parse(handler.Requests[2].Body!))
        {
            TestAssert.Equal("PlanCheck", createDocument.RootElement.GetProperty("@c").GetString(), "RT PlanCheck create-template body should request a PlanCheck object.");
            TestAssert.Equal(JsonValueKind.Null, createDocument.RootElement.GetProperty("id").ValueKind, "RT PlanCheck create-template body should include id:null for Innola.");
        }

        using var document = JsonDocument.Parse(handler.Requests[3].Body!);
        var checkList = document.RootElement[0].GetProperty("checkList");
        TestAssert.Equal(1, checkList.GetArrayLength(), "Save & Close should append one current PlanCheck row, not copy original snapshot rows.");
        var check = checkList[0];
        TestAssert.Equal("created-plan-check", check.GetProperty("id").GetString(), "Current PlanCheck identity should come from Innola create-template.");
        TestAssert.Equal("plan_check_type_general", check.GetProperty("checkType").GetString(), "Save & Close should set a valid Innola Plan Check type key when creating a current row.");
        TestAssert.True(check.GetProperty("passed").GetBoolean(), "Save & Close should set Plan Check acceptable/passed to true.");
        TestAssert.Equal("Updated from ArcGIS Pro TR 100000854.", check.GetProperty("description").GetString(), "Save & Close should write the ArcGIS Pro transaction description.");
        AssertUniqueObjectAliases(document.RootElement[0]);
    }

    private static void AssertUniqueObjectAliases(JsonElement root)
    {
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        CollectObjectAliases(root, aliases);
    }

    private static void CollectObjectAliases(JsonElement element, HashSet<string> aliases)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("@id", out var alias) && alias.ValueKind == JsonValueKind.String)
            {
                TestAssert.True(aliases.Add(alias.GetString()!), $"RT Plan save payload should not contain duplicate @id alias '{alias.GetString()}'.");
            }

            foreach (var property in element.EnumerateObject())
            {
                CollectObjectAliases(property.Value, aliases);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectObjectAliases(item, aliases);
            }
        }
    }

    public static async Task SaveKeepsLinkedRtTransactionOpenUntilComplete()
    {
        var transaction = new SelectedInnolaTransaction(
            "task-rt-100000854",
            "tx-rt-100000854",
            "100000854",
            "In RT Examination",
            "parcel_workflow",
            DateTimeOffset.UtcNow,
            TransactionType: "First Registration");
        var loadService = new CapturingRtLoadService();
        var writeback = new CapturingRtWritebackService();
        var refreshed = false;
        var closeObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new RtExaminationViewModel(
            transaction,
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            loadService,
            writeback,
            _ => true,
            _ => { },
            () => refreshed = true);
        viewModel.RequestClose += (_, _) => closeObserved.TrySetResult();

        await viewModel.LoadAsync();
        TestAssert.True(!viewModel.CanSave, "Loaded RT Examination should not enable Save before changes.");
        TestAssert.True(!viewModel.CanComplete, "Loaded RT Examination should require an RT result before Save & Close.");
        viewModel.SelectedCompletionDecision = RtExaminationCompletionDecision.DefaultOptions[0];
        TestAssert.True(viewModel.CanComplete, "Choosing an RT result should enable Save & Close.");
        viewModel.Observations = "Updated review";
        TestAssert.True(viewModel.CanSave, "Changed RT Examination should enable Save.");

        viewModel.SaveCommand.Execute(null);
        await Task.Delay(100);

        TestAssert.True(!closeObserved.Task.IsCompleted, "Save must keep the RT workspace open until the user chooses Complete.");
        TestAssert.True(!viewModel.CanSave, "Successful Save should disable Save until another change.");
        TestAssert.Equal("Close", viewModel.CloseActionText, "Successful Save should change Cancel to Close.");
        TestAssert.True(writeback.Request is not null, "Save should call RT writeback service.");
        TestAssert.True(!writeback.Request!.CompleteAfterSave, "Save must not complete the RT transaction.");
        TestAssert.Equal("tx-rt-100000854", writeback.Request.Transaction.TransactionId, "Save should write back to the linked/current RT transaction object.");
        TestAssert.True(!loadService.CleanupCalled, "Save must not clean up RT map layers.");
        TestAssert.True(!refreshed, "Save must not refresh Innola transactions before completion.");

        viewModel.CompleteCommand.Execute(null);
        var completed = await Task.WhenAny(closeObserved.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        TestAssert.True(ReferenceEquals(completed, closeObserved.Task), "Complete should close the RT workspace.");
        TestAssert.True(writeback.Request!.CompleteAfterSave, "Complete should be the final RT action.");
        TestAssert.Equal("Review Completed RT Examination", writeback.Request.CompletionDecision?.TransitionTargetStage, "Complete should pass the selected RT branch to writeback.");
        TestAssert.True(loadService.CleanupCalled, "Successful completion should clean up RT map layers.");
        TestAssert.True(refreshed, "Successful completion should refresh Innola transactions.");
    }

    private sealed class CapturingRtLoadService : IRtExaminationLoadService
    {
        public bool CleanupCalled { get; private set; }

        public Task<RtExaminationLoadResult> LoadAsync(SelectedInnolaTransaction transaction, string caseFolderPath, CancellationToken cancellationToken = default)
        {
            var context = new RtExaminationContextDocument(
                "rt_examination_context_v1",
                DateTimeOffset.UtcNow,
                transaction.TransactionId,
                transaction.TransactionNumber,
                transaction.TaskId,
                "plan-current",
                "plan-current-uid",
                transaction.TransactionId,
                transaction.TransactionNumber,
                "100000854",
                "tx-pe-100000854",
                "100000854",
                1,
                1,
                "100000854",
                Array.Empty<string>());
            return Task.FromResult(RtExaminationLoadResult.Succeeded(
                "Loaded linked transaction data.",
                context,
                Array.Empty<RtExaminationPartyRow>(),
                new[] { new RtExaminationSpatialUnitSummary("Parcel 1", "123.45", "SU-1", "2026-09-07T00:00:00Z") },
                new[] { new RtExaminationPlanCheckRow("plan_check_type_area", true, "Area accepted.") },
                new[] { "working_review: transaction_number = 100000854" },
                new[] { "RT 100000854 - PE 100000854" }));
        }

        public Task CleanupAsync(IReadOnlyList<string> loadedMapGroups, CancellationToken cancellationToken = default)
        {
            CleanupCalled = loadedMapGroups.Count == 1 && loadedMapGroups[0] == "RT 100000854 - PE 100000854";
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingRtWritebackService : IRtExaminationWritebackService
    {
        public RtExaminationSaveRequest? Request { get; private set; }

        public Task<RtExaminationSaveResult> SaveAsync(RtExaminationSaveRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(RtExaminationSaveResult.Succeeded("RT Examination data saved and task completed."));
        }
    }

    private sealed class FixedMapIntegrationService : ICompareMapIntegrationService
    {
        private readonly CompareMapIntegrationResult result;

        public FixedMapIntegrationService(CompareMapIntegrationResult result)
        {
            this.result = result;
        }

        public CompareWorkingGeometryLoadPlan? LastPlan { get; private set; }

        public Task<CompareMapIntegrationResult> AddTransactionGeometryToActiveMapAsync(
            CompareWorkingGeometryLoadPlan plan,
            CancellationToken cancellationToken = default)
        {
            LastPlan = plan;
            return Task.FromResult(result);
        }

        public Task<CompareMapCleanupResult> RemoveTransactionGeometryFromActiveMapAsync(string groupLayerName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CompareMapCleanupResult.Skipped("Test cleanup skipped."));
        }
    }

    private sealed class CapturingLifecycleService : IInnolaTransactionLifecycleService
    {
        public bool Completed { get; private set; }
        public InnolaTransactionLifecycleRequest? LastRequest { get; private set; }

        public Task<InnolaTransactionLifecycleResult> ClaimAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InnolaTransactionLifecycleResult.Succeeded("claimed", request.Session.Username, request.Session.User.DisplayName));
        }

        public Task<InnolaTransactionLifecycleResult> SaveProgressAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InnolaTransactionLifecycleResult.Succeeded("saved", request.Session.Username, request.Session.User.DisplayName));
        }

        public Task<InnolaTransactionLifecycleResult> CompleteAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            Completed = true;
            LastRequest = request;
            return Task.FromResult(InnolaTransactionLifecycleResult.Succeeded("completed", request.Session.Username, request.Session.User.DisplayName));
        }
    }

    private static InnolaSession CreateSession()
    {
        return new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            InnolaSettings.DefaultServerUrl,
            "rt.examiner",
            null,
            "access-token",
            new InnolaUserContext("rt.examiner", "RT Examiner", Array.Empty<string>(), Array.Empty<string>()),
            DateTimeOffset.UtcNow.AddHours(1));
    }

    private static SelectedInnolaTransaction CreateTransaction()
    {
        return new SelectedInnolaTransaction(
            "task-rt-100000854",
            "tx-current-rt",
            "100000854",
            "In RT Examination",
            "parcel_workflow",
            DateTimeOffset.UtcNow,
            TransactionType: "First Registration");
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<string> responses;

        public CapturingHttpMessageHandler(params string[] responses)
        {
            this.responses = new Queue<string>(responses);
        }

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body));

            var response = responses.Count > 0 ? responses.Dequeue() : "{}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? Body);
}




