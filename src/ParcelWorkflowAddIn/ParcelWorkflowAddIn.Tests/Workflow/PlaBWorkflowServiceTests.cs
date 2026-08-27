using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Tests;
using ParcelWorkflowAddIn.Workflow.Pla;
using System.IO.Compression;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class PlaBWorkflowServiceTests
{
    public static void PeNumberNormalizerStripsPrefixAndPreservesNumeric()
    {
        var prefixed = PlaBPeNumberNormalizer.Normalize("PE-100000861");
        var numeric = PlaBPeNumberNormalizer.Normalize("100000631");

        TestAssert.True(prefixed.Success, "Prefixed PE number should normalize.");
        TestAssert.Equal("100000861", prefixed.PeNumber, "PE prefix should be stripped.");
        TestAssert.True(numeric.Success, "Numeric PE number should normalize.");
        TestAssert.Equal("100000631", numeric.PeNumber, "Numeric PE number should be preserved.");
    }

    public static void PeNumberNormalizerRejectsBlankAndUnsupportedValues()
    {
        var blank = PlaBPeNumberNormalizer.Normalize(" ");
        var invalid = PlaBPeNumberNormalizer.Normalize("PX-abc");

        TestAssert.False(blank.Success, "Blank PE number should fail.");
        TestAssert.Equal("pe_number_missing", blank.ErrorCode, "Blank error code mismatch.");
        TestAssert.False(invalid.Success, "Unsupported PE number should fail.");
        TestAssert.Equal("pe_number_invalid", invalid.ErrorCode, "Invalid error code mismatch.");
    }

    public static void InputResolverRequiresPeNumberOnly()
    {
        var missingPe = PlaBWorkflowInputResolver.Resolve(CreateDetail(customFields: null, sourceType: PlaBWorkflowConstants.SurveyDiagramSourceType));
        var resolved = PlaBWorkflowInputResolver.Resolve(CreateDetail(new Dictionary<string, string> { ["PeNumber"] = "PE-100000861" }, null));

        TestAssert.False(missingPe.Success, "Missing PeNumber should block PLA_B preparation.");
        TestAssert.Equal("pe_number_missing", missingPe.ErrorCode, "Missing PeNumber blocker mismatch.");
        TestAssert.True(resolved.Success, "Complete PLA_B inputs should resolve.");
        TestAssert.Equal("100000861", resolved.PeNumber, "Resolved PE number mismatch.");
        TestAssert.Equal(null, resolved.SurveyDiagramAttachment, "PLA_B recovery should not require current transaction survey diagram input.");
    }

    public static void WorkflowSettingsContainPlaBProfileAndSurveyDiagramSourceType()
    {
        var settings = InnolaTransactionSettings.Load();

        TestAssert.True(
            settings.ComputeTransactionTypeProfiles.Any(profile => profile.WorkflowProfile == PlaBWorkflowConstants.WorkflowProfile),
            "WorkflowSettings should define the PLA_B workflow profile.");
        TestAssert.True(
            settings.ComputeAttachmentSourceTypes.Any(source => source.SourceType == PlaBWorkflowConstants.SurveyDiagramSourceType && source.SupportsExtension(".pdf")),
            "WorkflowSettings may keep st_survey_diagram registered for future use, but PLA_B recovery should not require it.");
        var plaBProfile = settings.ComputeTransactionTypeProfiles.First(profile => profile.WorkflowProfile == PlaBWorkflowConstants.WorkflowProfile);
        TestAssert.Equal(0, plaBProfile.RequiredSourceRoles.Count, "PLA_B recovery profile should not require PLA_A-style document sources.");
        TestAssert.Equal("pla_b_recovery", plaBProfile.DocumentProfile, "PLA_B recovery profile should use a neutral recovery document profile.");
    }

    public static void WorkflowRulesContainPlaBRuleWithoutChangingPlaA()
    {
        var rulesJson = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "Settings", "WorkflowRules.json"));

        TestAssert.True(rulesJson.Contains("\"workflow_profile\": \"pla_b_plan_annexation_from_pe\"", StringComparison.Ordinal), "WorkflowRules should include PLA_B.");
        TestAssert.True(rulesJson.Contains("\"workflow_profile\": \"pla_plan_annexation\"", StringComparison.Ordinal), "WorkflowRules should still include PLA_A.");
        TestAssert.True(rulesJson.Contains("\"document_profiles\": [\r\n        \"pla_b_recovery\"", StringComparison.Ordinal)
            || rulesJson.Contains("\"document_profiles\": [\n        \"pla_b_recovery\"", StringComparison.Ordinal), "PLA_B rule should use the neutral recovery profile.");
        TestAssert.False(rulesJson.Contains("\"script\": \"prepare_pla_b_from_pe\"", StringComparison.Ordinal), "PLA_B initial testing should not use a PLA_A-style extraction script plan.");
    }

    public static void PackageResolverExtractsMatchingPeOutputGdbOnly()
    {
        using var tempRoot = new TempDirectory();
        var layout = CaseFolderLayout.For(tempRoot.Path, "100001999");
        Directory.CreateDirectory(layout.WorkingDirectory);
        var zipPath = Path.Combine(tempRoot.Path, "survey_plan.zip");
        CreateZipWithGdb(zipPath, "100000631_parcel_output.gdb", "other_output.gdb");

        var result = PlaBPackageService.ExtractAndResolveOutputGdb(layout, zipPath, "100000631");

        TestAssert.True(result.Success, "Package resolver should find the matching PE output GDB.");
        TestAssert.True(result.GdbPath?.EndsWith(@"100000631_parcel_output.gdb", StringComparison.OrdinalIgnoreCase) == true, "Resolved GDB name mismatch.");
        TestAssert.True(result.GdbPath!.StartsWith(layout.RootDirectory, StringComparison.OrdinalIgnoreCase), "Resolved GDB should stay inside the case folder.");
    }

    public static void SourceInspectionShowsPeMGeoLoadsWithSeventyPercentTransparency()
    {
        var source = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "Workflow", "Pla", "PlaBWorkflowServices.cs"));

        TestAssert.True(source.Contains("IsMGeoLayerPath(layerPath)", StringComparison.Ordinal), "PLA_B map loader should identify m-geo layers from the PE output GDB.");
        TestAssert.True(source.Contains("layer.SetTransparency(70)", StringComparison.Ordinal), "PLA_B m-geo layer should be loaded with 70 percent transparency.");
        TestAssert.True(source.Contains("name.Equals(\"m-geo\"", StringComparison.Ordinal)
            && source.Contains("name.Equals(\"m_geo\"", StringComparison.Ordinal)
            && source.Contains("name.Equals(\"mgeo\"", StringComparison.Ordinal)
            && source.Contains("name.StartsWith(\"mgeo_overlay_\"", StringComparison.Ordinal), "PLA_B m-geo matching should allow common GDB layer names and output overlays.");
        TestAssert.True(source.Contains("GetDefinitions<RasterDatasetDefinition>", StringComparison.Ordinal), "PLA_B should scan root PE output GDB raster datasets such as mgeo_overlay.");
        TestAssert.True(source.Contains("GetDefinitions<FeatureDatasetDefinition>", StringComparison.Ordinal)
            && source.Contains("OpenDataset<FeatureDataset>", StringComparison.Ordinal), "PLA_B should scan feature classes inside PE output GDB feature datasets.");
    }

    public static async Task RelatedPeFinderSearchesWithStrippedNumericPeNumber()
    {
        var service = new RecordingTransactionService(new[]
        {
            CreateRow("100000631")
        });
        var finder = new PlaBRelatedPeTransactionFinder(service, () => "parcel_workflow");

        var result = await finder.FindAsync(CreateSession(), "PE-100000631");

        TestAssert.True(result.Success, "Related PE finder should resolve the exact PE row.");
        TestAssert.Equal("100000631", service.LastQuery?.Search, "Related PE finder must search with stripped numeric PE number.");
        TestAssert.Equal("100000631", result.Transaction?.TransactionNumber, "Related PE transaction mismatch.");
    }

    public static async Task RelatedPeFinderReportsNoAndMultipleMatches()
    {
        var noMatchFinder = new PlaBRelatedPeTransactionFinder(new RecordingTransactionService(Array.Empty<InnolaTransactionRow>()));
        var multiFinder = new PlaBRelatedPeTransactionFinder(new RecordingTransactionService(new[] { CreateRow("100000631"), CreateRow("100000631") }));

        var missing = await noMatchFinder.FindAsync(CreateSession(), "100000631");
        var multiple = await multiFinder.FindAsync(CreateSession(), "100000631");

        TestAssert.False(missing.Success, "No related PE match should fail.");
        TestAssert.Equal("pe_transaction_missing", missing.ErrorCode, "No-match error code mismatch.");
        TestAssert.False(multiple.Success, "Multiple related PE matches should fail.");
        TestAssert.Equal("pe_transaction_multiple", multiple.ErrorCode, "Multiple-match error code mismatch.");
    }

    public static async Task PackageDownloaderStoresSurveyPlanZipInsideCaseFolder()
    {
        using var tempRoot = new TempDirectory();
        var layout = CaseFolderLayout.For(tempRoot.Path, "100001999");
        Directory.CreateDirectory(layout.WorkingDirectory);
        var detail = CreatePeDetailWithPackage("survey_plan.zip");
        var downloader = new PlaBPePackageDownloader(new StubDetailService(new byte[] { 80, 75, 3, 4 }));

        var result = await downloader.DownloadAsync(CreateSession(), detail, layout);

        TestAssert.True(result.Success, "Related PE package download should succeed.");
        TestAssert.True(File.Exists(result.PackagePath), "Downloaded package should be written.");
        TestAssert.True(result.PackagePath!.StartsWith(layout.RootDirectory, StringComparison.OrdinalIgnoreCase), "Downloaded package should stay inside the case folder.");
    }

    public static async Task CurrentSourceDownloaderSkipsFailedAttachmentsAndKeepsUsableFiles()
    {
        using var tempRoot = new TempDirectory();
        var detail = CreateCurrentDetailWithSources(
            new InnolaAttachmentMetadata(
                "bad-1",
                "unavailable.pdf",
                ".pdf",
                "application/pdf",
                SourceRole.PlanMapReference,
                "st_survey_diagram",
                10,
                null,
                "body-id:bad",
                true,
                "st_survey_diagram"),
            new InnolaAttachmentMetadata(
                "good-1",
                "survey-plan.pdf",
                ".pdf",
                "application/pdf",
                SourceRole.PlanMapReference,
                "st_survey_diagram",
                10,
                null,
                "body-id:good",
                true,
                "st_survey_diagram"));
        var detailService = new PlaBCurrentSourceDetailService(
            detail,
            attachment => attachment.AttachmentId == "bad-1"
                ? InnolaAttachmentContentResult.Failure("Could not load attachment. Try again.", "unauthorized")
                : InnolaAttachmentContentResult.Succeeded(new byte[] { 1, 2, 3 }));
        var service = new PlaBCurrentTransactionSourceDownloadService(
            detailService,
            new CaseFolderStore(() => FixedNow(), () => "run-pla-b-source"),
            new AttachmentSourceFileWriter(() => FixedNow()),
            () => FixedNow());

        var result = await service.DownloadAsync(
            CreateSession(),
            CreateTransaction(),
            tempRoot.Path,
            "tester");

        TestAssert.True(result.Success, result.Message);
        TestAssert.Equal(1, result.SourceFileCount, "PLA_B should keep usable source files when another attachment fails.");
        TestAssert.Equal(1, result.Warnings.Count, "PLA_B should preserve a skipped-attachment diagnostic.");
        TestAssert.True(result.Warnings[0].Contains("unavailable.pdf", StringComparison.Ordinal), "PLA_B warning should name the failed attachment.");
        TestAssert.True(File.Exists(Path.Combine(result.Layout!.SourceDirectory, "survey-plan.pdf")), "PLA_B should write the usable source attachment.");
    }

    public static void EnterpriseWorkingLayerLookupPlannerUsesConfiguredTransactionNumberField()
    {
        var settings = InnolaTransactionSettings.Default with
        {
            EnterpriseWorkingReview = EnterpriseWorkingReviewSettings.Default with
            {
                Enabled = true,
                TransactionScopeField = "transaction_number",
                Layers = new EnterpriseWorkingLayerTargets("points-target", "lines-target", "polygons-target", null, null)
            }
        };

        var result = PlaBEnterpriseWorkingLayerLookupPlanner.Build(settings, "PE-100000631");

        TestAssert.True(result.Success, "Enterprise working-layer lookup plan should succeed with configured layer targets.");
        TestAssert.Equal("transaction_number", result.ScopeField, "Enterprise working-layer lookup should use configured scope field.");
        TestAssert.Equal("100000631", result.ScopeValue, "Enterprise working-layer lookup should use stripped PE number.");
        TestAssert.True(result.LayerTargets.Contains("polygons-target"), "Enterprise working-layer lookup should include configured polygon target.");
    }

    public static void PackageResolverReportsMissingAndCorruptPackages()
    {
        using var tempRoot = new TempDirectory();
        var layout = CaseFolderLayout.For(tempRoot.Path, "100001999");
        Directory.CreateDirectory(layout.WorkingDirectory);
        var zipPath = Path.Combine(tempRoot.Path, "survey_plan.zip");
        CreateZipWithGdb(zipPath, "999999999_parcel_output.gdb");
        var corruptPath = Path.Combine(tempRoot.Path, "corrupt.zip");
        File.WriteAllText(corruptPath, "not a zip");

        var missing = PlaBPackageService.ExtractAndResolveOutputGdb(layout, zipPath, "100000631");
        var corrupt = PlaBPackageService.ExtractAndResolveOutputGdb(layout, corruptPath, "100000631");

        TestAssert.False(missing.Success, "Missing matching GDB should fail.");
        TestAssert.Equal("matching_gdb_missing", missing.ErrorCode, "Missing GDB error code mismatch.");
        TestAssert.False(corrupt.Success, "Corrupt zip should fail.");
        TestAssert.Equal("package_corrupt", corrupt.ErrorCode, "Corrupt zip error code mismatch.");
    }

    public static void PackageResolverRejectsUnsafeArchivePaths()
    {
        using var tempRoot = new TempDirectory();
        var layout = CaseFolderLayout.For(tempRoot.Path, "100001999");
        Directory.CreateDirectory(layout.WorkingDirectory);
        var zipPath = Path.Combine(tempRoot.Path, "unsafe.zip");
        CreateUnsafeZip(zipPath);

        var result = PlaBPackageService.ExtractAndResolveOutputGdb(layout, zipPath, "100000631");

        TestAssert.False(result.Success, "Unsafe archive paths should fail before GDB resolution.");
        TestAssert.Equal("package_extract_failed", result.ErrorCode, "Unsafe archive error code mismatch.");
        TestAssert.False(File.Exists(Path.Combine(tempRoot.Path, "escape.txt")), "Unsafe archive entry should not be written outside the package folder.");
    }

    public static void MapPlannerBuildsCurrentAndPeGroups()
    {
        var result = PlaBMapReviewPlanner.Build("100001999", "100000631", @"C:\case\working\pla_b\pe_package\100000631_parcel_output.gdb");

        TestAssert.True(result.Success, "PLA_B map plan should succeed with required values.");
        TestAssert.Equal("PLA 100001999 - Current Transaction", result.CurrentTransactionGroupName, "Current transaction group name mismatch.");
        TestAssert.Equal("PE 100000631 - Approved PE Output", result.PeTransactionGroupName, "PE group name mismatch.");
        TestAssert.Equal(2, result.Groups.Count, "PLA_B map plan should contain current and PE groups.");
    }

    public static async Task SurveyDiagramSelectionPersistsPngAndMetadata()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateCaseWithSurveyDiagram(tempRoot.Path, out var source);
        var service = new PlaBSurveyDiagramSelectionService(new StubPlaBRenderer(new byte[] { 137, 80, 78, 71 }), () => FixedNow());

        var result = await service.SaveSelectionAsync(
            layout,
            "100001999",
            "100000631",
            new PlaBSurveyDiagramSelectionRequest(source, 2, new PlaBPdfSelectionRegion(10, 20, 300, 200)));
        var reopened = PlaBSurveyDiagramSelectionService.LoadSelection(layout);

        TestAssert.True(result.Success, "PLA_B survey diagram selection should save.");
        TestAssert.True(File.Exists(Path.Combine(layout.RootDirectory, "working", "pla_b", "survey_diagram_selection.png")), "PNG evidence should exist.");
        TestAssert.Equal("100001999", reopened?.TransactionNumber, "Selection transaction number mismatch.");
        TestAssert.Equal("100000631", reopened?.PeNumber, "Selection PE number mismatch.");
        TestAssert.Equal("st_survey_diagram", reopened?.SourceType, "Selection source type mismatch.");
        TestAssert.Equal(2, reopened?.SelectedPageNumber, "Selection page mismatch.");
        TestAssert.Equal("working/pla_b/survey_diagram_selection.png", reopened?.PngRelativePath, "Selection PNG path mismatch.");
    }

    public static void TestEmulationInputUsesSamePeNormalizer()
    {
        var input = new PlaBTestEmulationInputViewModel
        {
            CurrentTransactionNumber = "100001999",
            PeNumber = "PE-100000631"
        };

        TestAssert.True(input.CanPrepare, "Complete test values should enable preparation.");
        TestAssert.Equal("100000631", input.NormalizedPeNumber, "Test UX should use the same PE normalizer as production.");
    }

    public static void DockpaneDoesNotExposePlaBWorkflowTestEmulation()
    {
        var xaml = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "ParcelWorkflowDockpane.xaml"));
        var viewModel = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "ParcelWorkflowDockpaneViewModel.cs"));

        TestAssert.True(xaml.Contains("ShowPlaBTestEmulation", StringComparison.Ordinal), "Dormant dockpane markup may remain hidden while PLA_B testing moves to Transaction List.");
        TestAssert.True(viewModel.Contains("IsPlaBWorkflow", StringComparison.Ordinal), "View model should expose PLA_B profile detection.");
        TestAssert.True(viewModel.Contains("SourceInputProfile.PlaBPlanAnnexationFromPe", StringComparison.Ordinal), "View model should use the PLA_B profile constant.");
        TestAssert.True(viewModel.Contains("ShowPlaBTestEmulation => false", StringComparison.Ordinal), "PLA_B initial testing should not expose PLA_A-style workflow-pane UX.");
    }

    public static async Task FinalizeUploadsSurveyDiagramPngEvidence()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreatePlaBCaseWithSelection(tempRoot.Path);
        var uploader = new RecordingPlaBGeneratedEvidenceUploader();
        var service = new PlaBFinalizeService(uploader, getUtcNow: () => FixedNow());

        var readiness = service.CheckReadiness(layout);
        var result = await service.UploadGeneratedOutputsAsync(layout, CreateTransaction(), "tester");
        var evidence = service.LoadEvidence(layout);

        TestAssert.True(readiness.IsReady, "PLA_B finalize should be ready when survey diagram PNG evidence exists.");
        TestAssert.True(result.Success, "PLA_B finalize should upload PNG evidence.");
        TestAssert.Equal(1, uploader.Uploads.Count, "PLA_B finalize should upload only configured generated evidence.");
        TestAssert.Equal(PlaBWorkflowConstants.SurveyDiagramPngOutputSourceType, uploader.Uploads[0].SourceType, "PLA_B PNG upload source type mismatch.");
        TestAssert.Equal(PlaBFinalizeService.PngContentType, uploader.Uploads[0].ContentType, "PLA_B PNG content type mismatch.");
        TestAssert.Equal(PlaFinalizeService.UploadedStatus, evidence?.UploadStatus, "PLA_B finalize evidence should persist uploaded status.");
    }

    public static void FinalizeReadinessRequiresSurveyDiagramPngEvidence()
    {
        using var tempRoot = new TempDirectory();
        var layout = CaseFolderLayout.For(tempRoot.Path, "100001999");
        Directory.CreateDirectory(layout.WorkingDirectory);
        WritePlaBManifest(layout);
        var service = new PlaBFinalizeService(new RecordingPlaBGeneratedEvidenceUploader(), getUtcNow: () => FixedNow());

        var readiness = service.CheckReadiness(layout);

        TestAssert.False(readiness.IsReady, "PLA_B finalize should block without saved survey diagram PNG evidence.");
        TestAssert.Equal("pla_b_survey_diagram_selection_missing", readiness.Reason, "PLA_B missing evidence blocker mismatch.");
    }

    public static async Task FinalizeFailurePersistsRetryEvidence()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreatePlaBCaseWithSelection(tempRoot.Path);
        var uploader = new RecordingPlaBGeneratedEvidenceUploader(fail: true);
        var service = new PlaBFinalizeService(uploader, getUtcNow: () => FixedNow());

        var result = await service.UploadGeneratedOutputsAsync(layout, CreateTransaction(), "tester");
        var evidence = service.LoadEvidence(layout);

        TestAssert.False(result.Success, "PLA_B finalize should stop on upload failure.");
        TestAssert.Equal(PlaFinalizeService.FailedStatus, evidence?.UploadStatus, "PLA_B failure evidence should persist for retry.");
        TestAssert.Equal("upload_failed", evidence?.ErrorCategory, "PLA_B failure category should be retryable.");
    }

    public static void SourceInspectionShowsInnolaDetailMapsPeNumber()
    {
        var source = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "Innola", "InnolaTransactionDetailService.cs"));
        var rules = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "Settings", "WorkflowRules.json"));

        TestAssert.True(source.Contains("ExtractCustomFields", StringComparison.Ordinal), "Innola detail service should map custom fields.");
        TestAssert.True(source.Contains("\"PeNumber\"", StringComparison.Ordinal), "Innola detail service should read PeNumber.");
        TestAssert.True(rules.Contains("\"script_plan\": []", StringComparison.Ordinal), "PLA_B initial testing should not run PLA_A-style document extraction.");
    }

    private static InnolaTransactionDetail CreateDetail(
        IReadOnlyDictionary<string, string>? customFields,
        string? sourceType)
    {
        var attachment = new InnolaAttachmentMetadata(
            "attachment-1",
            "survey-diagram.pdf",
            ".pdf",
            "application/pdf",
            sourceType == PlaBWorkflowConstants.SurveyDiagramSourceType ? PlaBWorkflowConstants.SurveyDiagramSourceRole : SourceRole.PlanMapReference,
            sourceType,
            100,
            null,
            "body-id:1",
            true,
            sourceType);

        return new InnolaTransactionDetail(
            "tx-1",
            "100001999",
            "task-1",
            "Plan Annexation",
            "parcel_workflow",
            "PLA",
            PlaBWorkflowConstants.WorkflowProfile,
            null,
            null,
            null,
            null,
            new[] { attachment },
            null,
            customFields);
    }

    private static InnolaTransactionDetail CreatePeDetailWithPackage(string fileName)
    {
        var attachment = new InnolaAttachmentMetadata(
            "package-1",
            fileName,
            ".zip",
            "application/zip",
            SourceRole.WorkflowResumePackage,
            "survey_plan",
            10,
            null,
            "body-id:zip",
            true,
            "st_survey_zip");

        return new InnolaTransactionDetail(
            "tx-pe",
            "100000631",
            "task-pe",
            "PE",
            "parcel_workflow",
            "PE",
            "pe_computation_sheet_review",
            null,
            null,
            null,
            null,
            new[] { attachment });
    }

    private static InnolaTransactionDetail CreateCurrentDetailWithSources(params InnolaAttachmentMetadata[] attachments)
    {
        return new InnolaTransactionDetail(
            "tx-current",
            "100001999",
            "task-current",
            "First Registration",
            "parcel_workflow",
            "PLA",
            PlaBWorkflowConstants.WorkflowProfile,
            null,
            null,
            null,
            null,
            attachments);
    }

    private static InnolaSession CreateSession()
    {
        return new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            "https://example.test/",
            "tester",
            null,
            "token",
            new InnolaUserContext("tester", "Tester", Array.Empty<string>(), Array.Empty<string>()),
            FixedNow().AddHours(1));
    }

    private static InnolaTransactionRow CreateRow(string transactionNumber)
    {
        return new InnolaTransactionRow(
            $"task-{transactionNumber}",
            $"tx-{transactionNumber}",
            transactionNumber,
            "Plan Examination",
            "parcel_workflow",
            InnolaTransactionStatus.Available,
            "PE",
            null,
            null,
            null,
            FixedNow(),
            true,
            true,
            null,
            null);
    }

    private static CaseFolderLayout CreateCaseWithSurveyDiagram(string outputRoot, out SourceFileCopyResult source)
    {
        var store = new CaseFolderStore(() => FixedNow(), () => "run-pla-b");
        var created = store.CreateCase(outputRoot, "100001999", "tester");
        var layout = created.Layout!;
        var sourcePath = Path.Combine(layout.SourceDirectory, "survey-diagram.pdf");
        File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });
        source = new SourceFileCopyResult(
            @"C:\incoming\survey-diagram.pdf",
            sourcePath,
            "survey-diagram.pdf",
            ".pdf",
            4,
            PlaBWorkflowConstants.SurveyDiagramSourceRole,
            "copied",
            "Copied.",
            true,
            PlaBWorkflowConstants.SurveyDiagramSourceType);
        return layout;
    }

    private static CaseFolderLayout CreatePlaBCaseWithSelection(string outputRoot)
    {
        var layout = CreateCaseWithSurveyDiagram(outputRoot, out _);
        WritePlaBManifest(layout);
        var workingDirectory = PlaBSurveyDiagramSelectionService.GetWorkingDirectory(layout);
        Directory.CreateDirectory(workingDirectory);
        File.WriteAllBytes(PlaBSurveyDiagramSelectionService.GetPngPath(layout), new byte[] { 137, 80, 78, 71 });
        File.WriteAllText(
            PlaBSurveyDiagramSelectionService.GetMetadataPath(layout),
            """
            {
              "schema_version": "1.0.0",
              "transaction_number": "100001999",
              "pe_number": "100000631",
              "source_type": "st_survey_diagram",
              "source_relative_path": "source/survey-diagram.pdf",
              "selected_page_number": 1,
              "selection_region": { "x": 10, "y": 20, "width": 300, "height": 200 },
              "png_path": "working/pla_b/survey_diagram_selection.png",
              "page_width_points": 612,
              "page_height_points": 792,
              "created_at_utc": "2026-08-27T12:00:00Z",
              "updated_at_utc": "2026-08-27T12:00:00Z"
            }
            """);
        return layout;
    }

    private static void WritePlaBManifest(CaseFolderLayout layout)
    {
        ManifestSerializer.Write(
            layout.ManifestPath,
            new ManifestDocument(
                "1.0.0",
                "100001999",
                "run-pla-b",
                FixedNow().UtcDateTime.ToString("O"),
                "tester",
                null,
                new ManifestPayload(
                    "spatial_review_approved",
                    Array.Empty<ManifestSourceFile>(),
                    null,
                    null,
                    null,
                    null,
                    PlaBWorkflowConstants.WorkflowProfile),
                Array.Empty<string>(),
                Array.Empty<string>()));
    }

    private static SelectedInnolaTransaction CreateTransaction()
    {
        return new SelectedInnolaTransaction(
            "task-1",
            "tx-1",
            "100001999",
            "PLA_B",
            "Plan Annexation From PE",
            FixedNow(),
            TransactionType: "PLA_B");
    }

    private static void CreateZipWithGdb(string zipPath, params string[] gdbNames)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var gdbName in gdbNames)
        {
            var entry = archive.CreateEntry($"{gdbName}/a00000001.gdbtable");
            using var stream = entry.Open();
            stream.WriteByte(1);
        }
    }

    private static void CreateUnsafeZip(string zipPath)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("../escape.txt");
        using var stream = entry.Open();
        stream.WriteByte(1);
    }

    private static DateTimeOffset FixedNow() => new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    private sealed class StubPlaBRenderer : IPlaBSurveyDiagramSelectionRenderer
    {
        private readonly byte[] content;

        public StubPlaBRenderer(byte[] content)
        {
            this.content = content;
        }

        public Task<PlaBSurveyDiagramSelectionRenderResult> RenderAsync(
            PlaBSurveyDiagramSelectionRenderRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(PlaBSurveyDiagramSelectionRenderResult.Png(content, 612, 792));
        }
    }

    private sealed class RecordingTransactionService : IInnolaTransactionService
    {
        private readonly IReadOnlyList<InnolaTransactionRow> rows;

        public RecordingTransactionService(IReadOnlyList<InnolaTransactionRow> rows)
        {
            this.rows = rows;
        }

        public InnolaTransactionQuery? LastQuery { get; private set; }

        public Task<InnolaTransactionListResult> GetAvailableTransactionsAsync(InnolaTransactionQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(InnolaTransactionListResult.Succeeded(rows));
        }
    }

    private sealed class StubDetailService : IInnolaTransactionDetailService
    {
        private readonly byte[] content;

        public StubDetailService(byte[] content)
        {
            this.content = content;
        }

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
            return Task.FromResult(InnolaAttachmentContentResult.Succeeded(content));
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
            throw new NotSupportedException();
        }
    }

    private sealed class PlaBCurrentSourceDetailService : IInnolaTransactionDetailService
    {
        private readonly InnolaTransactionDetail detail;
        private readonly Func<InnolaAttachmentMetadata, InnolaAttachmentContentResult> contentFactory;

        public PlaBCurrentSourceDetailService(
            InnolaTransactionDetail detail,
            Func<InnolaAttachmentMetadata, InnolaAttachmentContentResult> contentFactory)
        {
            this.detail = detail;
            this.contentFactory = contentFactory;
        }

        public Task<InnolaTransactionDetailResult> GetTransactionDetailAsync(
            InnolaSession session,
            SelectedInnolaTransaction selectedTransaction,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InnolaTransactionDetailResult.Succeeded(detail));
        }

        public Task<InnolaAttachmentContentResult> GetAttachmentContentAsync(
            InnolaSession session,
            InnolaTransactionDetail detail,
            InnolaAttachmentMetadata attachment,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(contentFactory(attachment));
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
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingPlaBGeneratedEvidenceUploader : IPlaBGeneratedEvidenceUploader
    {
        private readonly bool fail;

        public RecordingPlaBGeneratedEvidenceUploader(bool fail = false)
        {
            this.fail = fail;
        }

        public List<(string ArtifactPath, string SourceType, string ContentType)> Uploads { get; } = new();

        public Task<PlaGeneratedOutputAttachmentResult> UploadAsync(
            SelectedInnolaTransaction transaction,
            string artifactPath,
            string sourceType,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            Uploads.Add((artifactPath, sourceType, contentType));
            return Task.FromResult(fail
                ? PlaGeneratedOutputAttachmentResult.Failed("Upload failed. Try again.", "upload_failed")
                : PlaGeneratedOutputAttachmentResult.Succeeded(sourceType, artifactPath));
        }
    }
}
