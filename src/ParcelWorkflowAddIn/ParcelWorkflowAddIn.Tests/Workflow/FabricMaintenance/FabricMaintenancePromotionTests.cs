using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.FabricMaintenance;

namespace ParcelWorkflowAddIn.Tests.Workflow.FabricMaintenance;

internal static class FabricMaintenancePromotionTests
{
    public static void RoutingGateRequiresConfiguredSubworkflowAndStage()
    {
        var settings = ConfiguredSettings();
        var eligible = Row("TR100001349", "First Registration", "In Parcel Fabric Update", "Parcel Fabric Maintenance");
        var stageOnlyListRow = Row("TR100001349", "First Registration", "In Parcel Fabric Update", null);
        var wrongStage = Row("TR100001349", "First Registration", "Compare", "Parcel Fabric Maintenance");
        var wrongSubworkflow = Row("TR100001349", "First Registration", "In Parcel Fabric Update", "Plan Annexation");

        var result = FabricMaintenancePromotionGate.Evaluate(eligible, settings);

        TestAssert.True(result.IsEligible, result.Reason ?? "Configured Fabric Maintenance row should be eligible.");
        TestAssert.True(FabricMaintenancePromotionGate.Evaluate(stageOnlyListRow, settings).IsEligible, "Exact Fabric stage should be eligible when Innola list omits subworkflow metadata.");
        TestAssert.False(FabricMaintenancePromotionGate.Evaluate(wrongStage, settings).IsEligible, "Wrong stage should be ineligible.");
        TestAssert.False(FabricMaintenancePromotionGate.Evaluate(wrongSubworkflow, settings).IsEligible, "Wrong subworkflow should be ineligible.");
    }

    public static void SettingsLoadFabricMaintenancePromotionBlock()
    {
        using var tempRoot = new TempDirectory();
        var settingsPath = Path.Combine(tempRoot.Path, "WorkflowSettings.json");
        File.WriteAllText(settingsPath, """
            {
              "fabric_maintenance_promotion": {
                "enabled": true,
                "subworkflow_name": "Parcel Fabric Maintenance",
                "stage_name": "In Parcel Fabric Update",
                "spatial_unit_examination_field": "examinationNumber"
              },
              "enterprise_working_review": {
                "enabled": true,
                "transaction_scope_field": "transaction_number",
                "layers": {
                  "points": "https://example.test/working/1",
                  "lines": "https://example.test/working/2",
                  "polygons": "https://example.test/working/3",
                  "case_index": "https://example.test/working/4"
                }
              },
              "compare_enterprise_cadaster": {
                "enabled": true,
                "legal": {
                  "enabled": true,
                  "source_name": "Legal Cadastre",
                  "display_name": "Legal",
                  "layer_url": "https://example.test/legal/15",
                  "sublayer_name": "Legal_Parcel",
                  "pid_field": "PID"
                },
                "fiscal": {
                  "enabled": true,
                  "source_name": "Fiscal Cadastre",
                  "display_name": "Cadastral",
                  "layer_url": "https://example.test/fiscal/1",
                  "sublayer_name": "Parcel",
                  "pid_field": "PID"
                }
              }
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsPath);

        TestAssert.True(settings.FabricMaintenancePromotion.Enabled, "Fabric Maintenance promotion should load as enabled.");
        TestAssert.Equal("Parcel Fabric Maintenance", settings.FabricMaintenancePromotion.SubworkflowName, "Fabric subworkflow setting mismatch.");
        TestAssert.Equal("In Parcel Fabric Update", settings.FabricMaintenancePromotion.StageName, "Fabric stage setting mismatch.");
        TestAssert.Equal("examinationNumber", settings.FabricMaintenancePromotion.SpatialUnitExaminationField, "Fabric PE field setting mismatch.");
        TestAssert.Equal("transaction_number", settings.FabricMaintenancePromotion.WorkingReview.TransactionScopeField, "Fabric working review scope setting mismatch.");
        TestAssert.Equal("Cadastral", settings.FabricMaintenancePromotion.FinalCadastre.Fiscal.DisplayName, "Fabric fiscal label setting mismatch.");
    }

    public static void TargetSelectionRejectsNullAndBothTargets()
    {
        TestAssert.False(FabricMaintenancePromotionTargetSelection.FromFlags(false, false).IsValid, "No target should be rejected.");
        TestAssert.False(FabricMaintenancePromotionTargetSelection.FromFlags(true, true).IsValid, "Both targets should be rejected.");

        var legal = FabricMaintenancePromotionTargetSelection.FromFlags(true, false);
        var fiscal = FabricMaintenancePromotionTargetSelection.FromFlags(false, true);

        TestAssert.True(legal.IsValid, legal.Message);
        TestAssert.Equal(FabricMaintenanceTarget.Legal, legal.Target, "Legal target mismatch.");
        TestAssert.True(fiscal.IsValid, fiscal.Message);
        TestAssert.Equal(FabricMaintenanceTarget.Fiscal, fiscal.Target, "Fiscal target mismatch.");
        TestAssert.Equal("Cadastral", fiscal.DisplayLabel, "Fiscal user label should be Cadastral.");
    }

    public static void ContextResolutionBlocksWithoutActiveTransactionOrPeNumber()
    {
        var settings = ConfiguredSettings();
        var resolver = new FabricMaintenancePromotionContextResolver(settings);
        var noActive = resolver.Resolve(null, "100000814");
        var noPe = resolver.Resolve(Row("TR100001349", "First Registration", "In Parcel Fabric Update", "Parcel Fabric Maintenance"), " ");

        TestAssert.False(noActive.IsReady, "Missing active transaction should block Fabric Maintenance.");
        TestAssert.True(noActive.Message.Contains("active Innola transaction", StringComparison.OrdinalIgnoreCase), "Missing active transaction message should be clear.");
        TestAssert.False(noPe.IsReady, "Missing PE/examination number should block Fabric Maintenance.");
        TestAssert.True(noPe.Message.Contains("PE/examination number", StringComparison.OrdinalIgnoreCase), "Missing PE message should be clear.");
    }

    public static void WorkingReviewPlanUsesParcelInReviewScopeOnly()
    {
        var settings = ConfiguredSettings();
        var plan = FabricMaintenanceWorkingReviewPlanner.BuildPlan(
            settings,
            currentTransactionNumber: "TR100001349",
            peNumber: "100000814");

        TestAssert.True(plan.IsValid, plan.Message);
        TestAssert.Equal("transaction_number", plan.ScopeField, "Working review scope field mismatch.");
        TestAssert.Equal("100000814", plan.ScopeValue, "Working review must use Parcel in Review, not the current transaction.");
        TestAssert.False(plan.LayerRequests.Any(request => request.Where.Contains("TR100001349", StringComparison.Ordinal)), "Working review queries must not be scoped by the current Innola transaction.");
        TestAssert.True(plan.LayerRequests.Any(request => request.Role == "points"), "Points request missing.");
        TestAssert.True(plan.LayerRequests.Any(request => request.Role == "lines"), "Lines request missing.");
        TestAssert.True(plan.LayerRequests.Any(request => request.Role == "polygons"), "Polygons request missing.");
        TestAssert.True(plan.LayerRequests.Any(request => request.Role == "case_index"), "Case index request missing.");
    }

    public static void ReviewLoadPlanUsesSpatialQueryTransparencyAndNeighborMode()
    {
        var settings = ConfiguredSettings() with
        {
            FinalCadastre = ConfiguredSettings().FinalCadastre with
            {
                SpatialSearchMode = CompareEnterpriseCadasterSettings.SpatialSearchModeBuffer,
                BufferDistanceMeters = 30
            }
        };

        var plan = FabricMaintenanceReviewLoadPlanner.BuildPlan(
            settings,
            "100000859",
            "100000814",
            FabricMaintenanceTarget.Fiscal);

        TestAssert.True(plan.IsValid, plan.Message);
        TestAssert.Equal("100000814", plan.WorkingReviewPlan.ScopeValue, "Load Parcel must query working_review by Parcel in Review.");
        TestAssert.Equal("Cadastral", plan.TargetLabel, "Fiscal target must display as Cadastral.");
        TestAssert.Equal("Surrounding parcels within 30 m", plan.SpatialRelationMode, "Neighbor search mode should be reused from Compare settings.");
        TestAssert.Equal(60, plan.WorkingTransparencyPercent, "Working review transparency mismatch.");
        TestAssert.Equal(70, plan.FinalTargetTransparencyPercent, "Final target transparency mismatch.");
        TestAssert.Equal("1=1", plan.FinalTargetPlan.SpatialCandidateWhere, "Fiscal spatial candidate query must not hide visible overlaps behind attribute/status filters.");
        TestAssert.True(plan.FinalTargetPlan.EvidenceWhere.Contains("parcel_status", StringComparison.OrdinalIgnoreCase), "Fiscal active parcel status must remain visible as attribute evidence.");
    }

    public static void FinalTargetQueryPlanUsesCanonicalPidRules()
    {
        var settings = ConfiguredSettings();
        var legalPlan = FabricMaintenanceFinalTargetQueryPlanner.BuildPlan(
            settings,
            FabricMaintenanceTarget.Legal,
            new FabricMaintenanceCandidateSearchKeys("PID-42", "LOT-1", "100000814", null));
        var fiscalPlan = FabricMaintenanceFinalTargetQueryPlanner.BuildPlan(
            settings,
            FabricMaintenanceTarget.Fiscal,
            new FabricMaintenanceCandidateSearchKeys("PID-42", null, "100000814", null));

        TestAssert.True(legalPlan.IsValid, legalPlan.Message);
        TestAssert.Equal("Legal", legalPlan.TargetLabel, "Legal label mismatch.");
        TestAssert.True(legalPlan.EvidenceWhere.Contains("PID", StringComparison.OrdinalIgnoreCase), "Legal evidence query should use PID.");
        TestAssert.Equal("1=1", legalPlan.SpatialCandidateWhere, "Legal spatial candidates should be found from overlap first.");
        TestAssert.True(legalPlan.Source.SublayerName?.Contains("Legal_Parcel", StringComparison.OrdinalIgnoreCase) == true, "Legal query should target Legal_Parcel.");
        TestAssert.True(fiscalPlan.IsValid, fiscalPlan.Message);
        TestAssert.Equal("Cadastral", fiscalPlan.TargetLabel, "Fiscal user label mismatch.");
        TestAssert.Equal("Lv_number", fiscalPlan.CanonicalPidField, "Fiscal candidate identity should use the configured parcel key.");
        TestAssert.True(fiscalPlan.EvidenceWhere.Contains("parcel_status", StringComparison.OrdinalIgnoreCase), "Fiscal evidence query should retain active parcel_status.");
        TestAssert.Equal("1=1", fiscalPlan.SpatialCandidateWhere, "Fiscal spatial candidates should be found from overlap first.");
    }

    public static void ReviewChecksGateApprovalAndRequireNotesForBlockingFindings()
    {
        var review = FabricMaintenanceReviewState.Create(
            "TR100001349",
            "100000814",
            FabricMaintenanceTarget.Legal,
            workingFeatureCounts: new FabricMaintenanceFeatureCounts(1, 2, 1, 1),
            candidateCount: 1);
        review.CheckResults.Add(new FabricMaintenanceCheckResult("geometry_validity", FabricMaintenanceCheckSeverity.Pass, "Geometry valid."));
        review.CheckResults.Add(new FabricMaintenanceCheckResult("missing_required_attributes", FabricMaintenanceCheckSeverity.Blocking, "PID is missing."));
        review.SelectDecision(FabricMaintenancePromotionDecision.KeepExistingDiscardWorking);

        var blocked = FabricMaintenanceFinalWriteReadinessService.Evaluate(review);
        review.DecisionNotes = "Existing final parcel is correct; discard working draft.";
        var ready = FabricMaintenanceFinalWriteReadinessService.Evaluate(review);

        TestAssert.False(blocked.IsReady, "Blocking check without notes should block final write approval.");
        TestAssert.True(blocked.Message.Contains("decision notes", StringComparison.OrdinalIgnoreCase), "Readiness message should require notes.");
        TestAssert.True(ready.IsReady, ready.Message);
    }

    public static void NotImplementedDecisionOptionsRemainVisibleButBlocked()
    {
        var review = FabricMaintenanceReviewState.Create(
            "TR100001349",
            "100000814",
            FabricMaintenanceTarget.Fiscal,
            new FabricMaintenanceFeatureCounts(0, 0, 1, 1),
            candidateCount: 1);

        var replace = review.SelectDecision(FabricMaintenancePromotionDecision.ReplaceExisting);
        var merge = review.SelectDecision(FabricMaintenancePromotionDecision.MergeUpdateAttributesOnly);

        TestAssert.Equal("To be implemented", replace.Message, "Replace option popup text mismatch.");
        TestAssert.False(replace.IsExecutable, "Replace must not be executable in this story.");
        TestAssert.Equal("To be implemented", merge.Message, "Merge option popup text mismatch.");
        TestAssert.False(FabricMaintenanceFinalWriteReadinessService.Evaluate(review).IsReady, "Merge must keep final write blocked.");
    }

    public static void WorkspaceXamlExposesReviewAndFinalWriteScreens()
    {
        var xaml = File.ReadAllText(FindSourceFile("FabricMaintenancePromotionWindow.xaml"));
        var viewModel = File.ReadAllText(FindSourceFile("FabricMaintenancePromotionViewModel.cs"));
        var services = File.ReadAllText(FindSourceFile("FabricMaintenancePromotionServices.cs"));

        TestAssert.True(xaml.Contains("Title=\"Fabric Maintenance\"", StringComparison.Ordinal), "Fabric Maintenance window title missing.");
        TestAssert.True(xaml.Contains("Header=\"Review And Decide\"", StringComparison.Ordinal), "Review And Decide tab missing.");
        TestAssert.True(xaml.Contains("Header=\"Final Layer Write\"", StringComparison.Ordinal), "Final Layer Write tab missing.");
        TestAssert.True(xaml.Contains("Text=\"Parcel in Review:\"", StringComparison.Ordinal), "Parcel in Review label missing.");
        TestAssert.True(xaml.Contains("<RadioButton", StringComparison.Ordinal), "Final target radio buttons missing.");
        TestAssert.True(xaml.Contains("Content=\"Legal\"", StringComparison.Ordinal), "Legal target option missing.");
        TestAssert.True(xaml.Contains("Content=\"Cadastral\"", StringComparison.Ordinal), "Cadastral target option missing.");
        TestAssert.True(xaml.Contains("Content=\"Load Parcel\"", StringComparison.Ordinal), "Load Parcel action missing.");
        TestAssert.True(xaml.Contains("Content=\"Cancel\"", StringComparison.Ordinal), "Cancel action missing.");
        TestAssert.False(xaml.Contains("Content=\"Refresh Review\"", StringComparison.Ordinal), "Generic Refresh Review action should not be visible in this patch.");
        TestAssert.True(xaml.Contains("ItemsSource=\"{Binding ReviewResults}\"", StringComparison.Ordinal), "Review results grid missing.");
        TestAssert.True(xaml.Contains("ItemsSource=\"{Binding FinalCandidates}\"", StringComparison.Ordinal), "Final candidate grid missing.");
        TestAssert.True(xaml.Contains("SelectedItem=\"{Binding SelectedFinalCandidate, Mode=TwoWay}\"", StringComparison.Ordinal), "Final candidate selection binding missing.");
        TestAssert.True(xaml.Contains("Text=\"Attribute Review\"", StringComparison.Ordinal), "Attribute Review evidence area missing.");
        TestAssert.True(xaml.Contains("Selected decision:", StringComparison.Ordinal), "Selected decision feedback should be visible.");
        TestAssert.True(xaml.Contains("Content=\"Replace Existing\"", StringComparison.Ordinal), "Replace future option missing.");
        TestAssert.True(xaml.Contains("Content=\"Merge Attributes Only\"", StringComparison.Ordinal), "Merge future option missing.");
        TestAssert.True(xaml.Contains("Content=\"Approve For Final Write\"", StringComparison.Ordinal), "Approve action missing.");
        TestAssert.True(xaml.Contains("Content=\"Confirm Final Write\"", StringComparison.Ordinal), "Final write confirmation action missing.");
        TestAssert.True(xaml.Contains("Text=\"{Binding ParcelInReview, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", StringComparison.Ordinal), "Parcel in Review should be editable from the workspace.");
        TestAssert.True(xaml.Contains("IsEnabled=\"{Binding IsParcelInReviewEditable}\"", StringComparison.Ordinal), "Parcel in Review editing should be controlled by the view model.");
        TestAssert.True(viewModel.Contains("showMessage(result.Message)", StringComparison.Ordinal), "View model should show future-option messages.");
        TestAssert.True(services.Contains("\"To be implemented\"", StringComparison.Ordinal), "Future decision options should show exact not-implemented popup text.");
        TestAssert.True(services.Contains("ArcGisFabricMaintenanceReviewLoadService", StringComparison.Ordinal), "ArcGIS review load seam missing.");
    }

    public static void MissingPeNumberKeepsWorkspaceEditable()
    {
        var viewModel = new FabricMaintenancePromotionViewModel(
            "100000859",
            string.Empty,
            ConfiguredSettings(),
            "SpatialUnit examinationNumber is missing for this transaction. Enter the PE number manually.");

        TestAssert.True(viewModel.IsPeNumberEditable, "Missing PE number should enable manual PE editing.");
        TestAssert.True(viewModel.StatusText.Contains("Enter the PE number manually", StringComparison.Ordinal), "Missing PE status should explain manual entry.");

        viewModel.ParcelInReview = "100000814";

        TestAssert.Equal("100000814", viewModel.ParcelInReview, "Manual Parcel in Review value should be accepted.");
        TestAssert.True(viewModel.ConfirmationSummary.Contains("Parcel in Review 100000814", StringComparison.Ordinal), "Manual Parcel in Review value should flow into confirmation summary.");
    }

    public static async Task LoadParcelPopulatesCompactResultsAndEvidence()
    {
        var loadService = new CapturingReviewLoadService();
        var viewModel = new FabricMaintenancePromotionViewModel(
            "100000859",
            "100000814",
            ConfiguredSettings(),
            reviewLoadService: loadService);

        viewModel.IsCadastralTargetSelected = true;
        viewModel.LoadParcelCommand.Execute(null);
        await loadService.Completed.Task;

        TestAssert.Equal("100000814", loadService.CapturedPlan?.ParcelInReview, "Load Parcel should pass Parcel in Review to the loader.");
        TestAssert.Equal(FabricMaintenanceTarget.Fiscal, loadService.CapturedPlan?.Target, "Load Parcel should use the selected radio target.");
        TestAssert.Equal(2, viewModel.ReviewResults.Count, "Compact results grid should include working and final rows.");
        TestAssert.True(viewModel.ReviewResults.Any(row => row.Source == "Working Review" && row.Count == 1), "Working parcel count row missing.");
        TestAssert.True(viewModel.ReviewResults.Any(row => row.Source == "Cadastral" && row.Count == 2), "Final candidate count row missing.");
        TestAssert.Equal(2, viewModel.FinalCandidates.Count, "Final overlap candidates should be visible.");
        TestAssert.True(viewModel.FinalCandidates.Any(candidate => candidate.Pid == "PID-1" && candidate.OverlapPercent == 96.5), "Final overlap candidate details missing.");
        TestAssert.True(viewModel.ReviewChecks.Any(check => check.Code == "overlap_conflict"), "Topology evidence should include overlap/conflict check.");
        TestAssert.True(viewModel.AttributeChecks.Any(check => check.Code == "parcel_identifier"), "Attribute evidence should include parcel identifier check.");

        viewModel.SelectedFinalCandidate = viewModel.FinalCandidates[0];

        TestAssert.Equal("PID-1", viewModel.SelectedCandidateId, "Selected final candidate should update readiness candidate identity.");
    }

    public static async Task LoadParcelDisablesAfterSuccessAndReenablesWhenContextChanges()
    {
        var loadService = new CapturingReviewLoadService();
        var viewModel = new FabricMaintenancePromotionViewModel(
            "100000859",
            "100000814",
            ConfiguredSettings(),
            reviewLoadService: loadService);

        viewModel.IsLegalTargetSelected = true;
        viewModel.LoadParcelCommand.Execute(null);
        await loadService.Completed.Task;

        TestAssert.True(viewModel.IsReviewLoaded, "Successful Load Parcel should mark review context as loaded.");
        TestAssert.False(viewModel.LoadParcelCommand.CanExecute(null), "Load Parcel should be disabled after successful load.");

        viewModel.ParcelInReview = "100000815";

        TestAssert.False(viewModel.IsReviewLoaded, "Changing Parcel in Review should invalidate loaded review context.");
        TestAssert.True(viewModel.LoadParcelCommand.CanExecute(null), "Load Parcel should re-enable after context changes.");
    }

    public static async Task CancelCleansReviewContextAndRequestsWindowClose()
    {
        var loadService = new CapturingReviewLoadService();
        var viewModel = new FabricMaintenancePromotionViewModel(
            "100000859",
            "100000814",
            ConfiguredSettings(),
            reviewLoadService: loadService);
        var closeRequested = false;
        viewModel.RequestClose += (_, _) => closeRequested = true;

        viewModel.CancelCommand.Execute(null);
        await loadService.CleanupCompleted.Task;

        TestAssert.Equal("100000859", loadService.CleanupTransactionNumber, "Cancel cleanup should be scoped to the current transaction.");
        TestAssert.True(closeRequested, "Cancel should request the Fabric Maintenance window to close after cleanup.");
    }

    public static async Task LoadParcelExceptionStaysInWorkspaceStatus()
    {
        var loadService = new ThrowingReviewLoadService();
        var messages = new List<string>();
        var viewModel = new FabricMaintenancePromotionViewModel(
            "100000859",
            "100000814",
            ConfiguredSettings(),
            showMessage: messages.Add,
            reviewLoadService: loadService);

        viewModel.IsLegalTargetSelected = true;
        viewModel.LoadParcelCommand.Execute(null);
        await loadService.Completed.Task;
        await Task.Delay(25);

        TestAssert.True(viewModel.StatusText.Contains("could not be loaded", StringComparison.OrdinalIgnoreCase), "Load Parcel exceptions should be shown in status.");
        TestAssert.True(messages.Any(message => message.Contains("simulated geodatabase failure", StringComparison.OrdinalIgnoreCase)), "Load Parcel exceptions should be surfaced without crashing.");
        TestAssert.True(viewModel.LoadParcelCommand.CanExecute(null), "Failed Load Parcel should remain retryable.");
    }

    public static void PersistenceRoundTripsDraftDecisionTopologyAndSummaryArtifacts()
    {
        using var tempRoot = new TempDirectory();
        var layout = CaseFolderLayout.FromRootDirectory(tempRoot.Path);
        var service = new FabricMaintenancePromotionArtifactService();
        var review = FabricMaintenanceReviewState.Create(
            "TR100001349",
            "100000814",
            FabricMaintenanceTarget.Fiscal,
            new FabricMaintenanceFeatureCounts(2, 3, 1, 1),
            candidateCount: 0);
        review.CheckResults.Add(new FabricMaintenanceCheckResult("geometry_validity", FabricMaintenanceCheckSeverity.Pass, "Geometry valid."));
        review.SelectDecision(FabricMaintenancePromotionDecision.SendBackForReview);
        review.DecisionNotes = "Boundary needs review before final promotion.";

        var paths = service.SaveAll(layout, review, "jp.examiner", "uploaded");
        var restored = service.LoadDraft(layout);
        var summaryText = File.ReadAllText(paths.SummaryPath);

        TestAssert.True(File.Exists(paths.DraftPath), "Draft artifact missing.");
        TestAssert.True(File.Exists(paths.TopologyPath), "Topology artifact missing.");
        TestAssert.True(File.Exists(paths.DecisionPath), "Decision artifact missing.");
        TestAssert.True(File.Exists(paths.SummaryPath), "Summary artifact missing.");
        TestAssert.Equal("TR100001349", restored?.CurrentTransactionNumber, "Draft transaction mismatch.");
        TestAssert.True(summaryText.Contains("\"attachment_status\": \"uploaded\"", StringComparison.Ordinal), "Summary should include attachment status.");
    }

    public static void TerminalActionsUpdateWorkingReviewAndRequireSummaryAttachmentBeforeCompletion()
    {
        using var tempRoot = new TempDirectory();
        var layout = CaseFolderLayout.FromRootDirectory(tempRoot.Path);
        var terminal = new FabricMaintenancePromotionFinalActionService(new FabricMaintenancePromotionArtifactService());
        var review = FabricMaintenanceReviewState.Create(
            "TR100001349",
            "100000814",
            FabricMaintenanceTarget.Fiscal,
            new FabricMaintenanceFeatureCounts(1, 1, 1, 1),
            candidateCount: 0);
        review.SelectDecision(FabricMaintenancePromotionDecision.SendBackForReview);
        review.DecisionNotes = "Topology needs correction.";

        var result = terminal.Execute(layout, review, "jp.examiner", summaryAttachmentSucceeded: true);
        var readiness = FabricMaintenanceCompletionReadinessService.Evaluate(result);
        var failedAttachment = terminal.Execute(layout, review, "jp.examiner", summaryAttachmentSucceeded: false);

        TestAssert.True(result.Success, result.Message);
        TestAssert.Equal("returned_for_review", result.WorkingReviewStatus, "Send back status mismatch.");
        TestAssert.True(readiness.IsReady, readiness.Message);
        TestAssert.False(FabricMaintenanceCompletionReadinessService.Evaluate(failedAttachment).IsReady, "Missing summary attachment must block Innola completion.");
    }

    public static async Task SummaryAttachmentServiceUploadsJsonArtifact()
    {
        using var tempRoot = new TempDirectory();
        var summaryPath = Path.Combine(tempRoot.Path, "final_cadastre_promotion_summary.json");
        await File.WriteAllTextAsync(summaryPath, "{}");
        var detailService = new CapturingTransactionDetailService();
        var attachmentService = new FabricMaintenanceSummaryAttachmentService(
            () => new InnolaSession(
                InnolaSessionStatus.LoggedIn,
                "https://example.test/",
                "jp.examiner",
                null,
                "token",
                new InnolaUserContext("jp.examiner", "JP Examiner", Array.Empty<string>(), Array.Empty<string>()),
                null),
            detailService);

        var result = await attachmentService.UploadAsync(
            new SelectedInnolaTransaction(
                "task-1",
                "tx-1",
                "TR100001349",
                "In Parcel Fabric Update",
                "parcel_workflow",
                DateTimeOffset.UtcNow),
            summaryPath);

        TestAssert.True(result.Success, result.Message);
        TestAssert.Equal("st_fabric_promotion_summary", detailService.SourceType, "Fabric summary upload source type mismatch.");
        TestAssert.Equal("application/json", detailService.ContentType, "Fabric summary upload content type mismatch.");
        TestAssert.Equal("final_cadastre_promotion_summary.json", detailService.FileName, "Fabric summary upload filename mismatch.");
        TestAssert.True(detailService.ContentLength > 0, "Fabric summary upload should include file content.");
    }

    private static InnolaTransactionRow Row(string number, string type, string task, string? subworkflow)
    {
        return new InnolaTransactionRow(
            "task-1",
            "tx-1",
            number,
            task,
            "parcel_workflow",
            InnolaTransactionStatus.Available,
            type,
            "Tester",
            null,
            null,
            DateTimeOffset.UtcNow,
            true,
            true,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            type,
            subworkflow,
            new[] { type, subworkflow }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToArray());
    }

    private static FabricMaintenancePromotionSettings ConfiguredSettings()
    {
        return FabricMaintenancePromotionSettings.Default with
        {
            WorkingReview = EnterpriseWorkingReviewSettings.Default with
            {
                Enabled = true,
                Layers = new EnterpriseWorkingLayerTargets(
                    "https://example.test/working/1",
                    "https://example.test/working/2",
                    "https://example.test/working/3",
                    "https://example.test/working/0",
                    "https://example.test/working/4")
            },
            FinalCadastre = new CompareEnterpriseCadasterSettings(
                true,
                0.05,
                100,
                50,
                new CompareEnterpriseCadasterSourceSettings(
                    true,
                    "Legal Cadastre",
                    "https://example.test/legal/15",
                    "PID",
                    "PID",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "parish",
                    "suid",
                    "objectid",
                    "globalid",
                    null)
                {
                    DisplayName = "Legal",
                    SublayerName = "Legal_Parcel",
                    LotNumberField = "lot_number",
                    PeNumberField = "pe_number"
                },
                new CompareEnterpriseCadasterSourceSettings(
                    true,
                    "Fiscal Cadastre",
                    "https://example.test/fiscal/1",
                    "Lv_number",
                    "Lv_number",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "parish",
                    "suid",
                    "objectid",
                    "globalid",
                    null)
                {
                    DisplayName = "Cadastral",
                    SublayerName = "Parcel",
                    LotNumberField = "lot_number",
                    PeNumberField = "pe_number"
                },
                CompareEnterpriseCadasterSourceSettings.Disabled("Survey"),
                null)
        };
    }

    private static string FindSourceFile(string fileName)
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var repoCandidate = Path.Combine(
                directory,
                "src",
                "ParcelWorkflowAddIn",
                "ParcelWorkflowAddIn",
                fileName);
            if (File.Exists(repoCandidate))
            {
                return repoCandidate;
            }

            var projectRoot = Path.Combine(directory, "src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn");
            if (Directory.Exists(projectRoot))
            {
                var recursiveCandidate = Directory.EnumerateFiles(projectRoot, fileName, SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(recursiveCandidate))
                {
                    return recursiveCandidate;
                }
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new FileNotFoundException($"Could not locate {fileName}.");
    }

    private sealed class CapturingReviewLoadService : IFabricMaintenanceReviewLoadService
    {
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource CleanupCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public FabricMaintenanceReviewLoadPlan? CapturedPlan { get; private set; }

        public string? CleanupTransactionNumber { get; private set; }

        public Task<FabricMaintenanceReviewLoadResult> LoadAsync(
            FabricMaintenanceReviewLoadPlan plan,
            CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            CapturedPlan = plan;
            var result = new FabricMaintenanceReviewLoadResult(
                true,
                "Loaded from fake review loader.",
                new FabricMaintenanceFeatureCounts(0, 0, 1, 1),
                2,
                new[]
                {
                    new FabricMaintenanceReviewResultRow("Working Review", "transaction_number = 100000814", 1, plan.SpatialRelationMode, "Loaded into map."),
                    new FabricMaintenanceReviewResultRow("Cadastral", "Spatial query from working parcel geometry", 2, plan.SpatialRelationMode, "Loaded into map.")
                },
                new[]
                {
                    new FabricMaintenanceFinalCandidate("Cadastral", "1", "gid-1", "parcel-1", "PID-1", "overlaps", 965, 96.5, "Spatial overlap candidate"),
                    new FabricMaintenanceFinalCandidate("Cadastral", "2", "gid-2", "parcel-2", "PID-2", "touches", 10, 1.0, "Spatial overlap candidate")
                },
                FabricMaintenanceReviewEvidenceCatalog.TopologyChecks(1, 2),
                FabricMaintenanceReviewEvidenceCatalog.AttributeChecks());
            Completed.TrySetResult();
            return Task.FromResult(result);
        }

        public Task<FabricMaintenanceReviewCleanupResult> CleanupAsync(
            string currentTransactionNumber,
            CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            CleanupTransactionNumber = currentTransactionNumber;
            CleanupCompleted.TrySetResult();
            return Task.FromResult(new FabricMaintenanceReviewCleanupResult(true, "Cleaned up."));
        }
    }

    private sealed class ThrowingReviewLoadService : IFabricMaintenanceReviewLoadService
    {
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<FabricMaintenanceReviewLoadResult> LoadAsync(
            FabricMaintenanceReviewLoadPlan plan,
            CancellationToken cancellationToken = default)
        {
            _ = plan;
            _ = cancellationToken;
            Completed.TrySetResult();
            throw new InvalidOperationException("simulated geodatabase failure");
        }

        public Task<FabricMaintenanceReviewCleanupResult> CleanupAsync(
            string currentTransactionNumber,
            CancellationToken cancellationToken = default)
        {
            _ = currentTransactionNumber;
            _ = cancellationToken;
            return Task.FromResult(new FabricMaintenanceReviewCleanupResult(true, "Cleaned up."));
        }
    }

    private sealed class CapturingTransactionDetailService : IInnolaTransactionDetailService
    {
        public string? FileName { get; private set; }

        public string? ContentType { get; private set; }

        public string? SourceType { get; private set; }

        public int ContentLength { get; private set; }

        public Task<InnolaTransactionDetailResult> GetTransactionDetailAsync(
            InnolaSession session,
            SelectedInnolaTransaction selectedTransaction,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<InnolaAttachmentContentResult> GetAttachmentContentAsync(
            InnolaSession session,
            InnolaTransactionDetail detail,
            InnolaAttachmentMetadata attachment,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<InnolaAttachmentUploadResult> UploadAttachmentAsync(
            InnolaSession session,
            SelectedInnolaTransaction selectedTransaction,
            string fileName,
            string contentType,
            byte[] content,
            string sourceType,
            CancellationToken cancellationToken = default)
        {
            FileName = fileName;
            ContentType = contentType;
            SourceType = sourceType;
            ContentLength = content.Length;
            return Task.FromResult(InnolaAttachmentUploadResult.Succeeded());
        }
    }
}
