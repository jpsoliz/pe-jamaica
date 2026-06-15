using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Tests.Preflight;
using ParcelWorkflowAddIn.Workflow;
using ParcelWorkflowAddIn.Workflow.Execution;
using ParcelWorkflowAddIn.Workflow.Output;
using ParcelWorkflowAddIn.Workflow.Review;
using ParcelWorkflowAddIn.Workflow.SpatialReview;
using ParcelWorkflowAddIn.Workflow.Validation;
using ParcelWorkflowAddIn.WorkflowRules;
using System.Text.Json.Nodes;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class WorkflowSessionTests
{
    public static void WorkflowSessionStartsAtNoCase()
    {
        var session = CreateManifestOnlySession();

        TestAssert.Equal(WorkflowState.NoCase, session.CurrentState, "Initial workflow state mismatch.");
        TestAssert.Equal("No Case", session.CurrentStep, "Initial current step mismatch.");
        TestAssert.Equal("No active case", session.StatusText, "Initial status text mismatch.");
    }

    public static void WorkflowSessionExposesIntakeStateAfterCreation()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var session = new WorkflowSession(store);

        var result = session.CreateCase("TR-SMD-0000001", tempRoot.Path, "tester");

        TestAssert.True(result.Success, "Workflow case creation should succeed.");
        TestAssert.Equal(WorkflowState.Intake, session.CurrentState, "Workflow should move to intake.");
        TestAssert.Equal("TR-SMD-0000001", session.TransactionId, "Transaction ID should be exposed.");
        TestAssert.Equal("Intake", session.CurrentStep, "Current step should be intake.");
        TestAssert.Equal("Case created", session.StatusText, "Status text should indicate creation.");
    }

    public static void WorkflowSessionExposesValidationFailureStatus()
    {
        var session = CreateManifestOnlySession();

        session.SetValidationFailure("Transaction ID and output location are required.");

        TestAssert.Equal(WorkflowState.NoCase, session.CurrentState, "Validation failure should not change workflow state.");
        TestAssert.Equal("Transaction ID and output location are required.", session.StatusText, "Validation failure should update status text.");
    }

    public static void WorkflowSessionResetClearsLoadedCaseState()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var session = new WorkflowSession(store);
        session.CreateCase("TR-SMD-0000001", tempRoot.Path, "tester");

        session.ResetToDefault("Current process cancelled locally.");

        TestAssert.Equal(WorkflowState.NoCase, session.CurrentState, "Reset should return workflow to default state.");
        TestAssert.Equal(null, session.TransactionId, "Reset should clear transaction id.");
        TestAssert.Equal(null, session.CaseFolderPath, "Reset should clear case folder path.");
        TestAssert.Equal(0, session.SourceFiles.Count, "Reset should clear source files.");
        TestAssert.Equal("Current process cancelled locally.", session.StatusText, "Reset should keep the supplied status.");
    }

    public static void WorkflowSessionRejectsSourceFilesWithoutActiveCase()
    {
        using var incoming = new TempDirectory();
        var sourcePath = Path.Combine(incoming.Path, "plan.pdf");
        File.WriteAllText(sourcePath, "source");
        var session = CreateManifestOnlySession();

        var result = session.AddSourceFiles(new[] { sourcePath });

        TestAssert.True(!result.Success, "Source files should be rejected without an active case.");
        TestAssert.Equal(WorkflowState.NoCase, session.CurrentState, "Rejected source files should not change workflow state.");
        TestAssert.Equal("Create or reopen a Case Folder before adding source files.", session.StatusText, "Missing case status should be clear.");
    }

    public static void WorkflowSessionAddsSourceFilesDuringIntake()
    {
        using var tempRoot = new TempDirectory();
        using var incoming = new TempDirectory();
        var sourcePath = Path.Combine(incoming.Path, "plan.pdf");
        File.WriteAllText(sourcePath, "source");
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var session = new WorkflowSession(store, new SourceFileCopyService(() => new DateTimeOffset(2026, 6, 9, 1, 2, 3, TimeSpan.Zero)));
        session.CreateCase("TR-SMD-0000001", tempRoot.Path, "tester");

        var result = session.AddSourceFiles(new[] { sourcePath });

        TestAssert.True(result.Success, "Source files should be added during intake.");
        TestAssert.Equal(WorkflowState.Intake, session.CurrentState, "Source copy should keep workflow in intake.");
        TestAssert.Equal(1, session.SourceFiles.Count, "Session should expose copied source file list.");
        TestAssert.Equal("Copied 1 source file to Case Folder source area.", session.StatusText, "Source copy status mismatch.");
    }

    public static void WorkflowSessionExposesRejectedSourceFileRows()
    {
        using var tempRoot = new TempDirectory();
        using var incoming = new TempDirectory();
        var sourcePath = Path.Combine(incoming.Path, "notes.docx");
        File.WriteAllText(sourcePath, "source");
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var session = new WorkflowSession(store, new SourceFileCopyService(() => new DateTimeOffset(2026, 6, 9, 1, 2, 3, TimeSpan.Zero)));
        session.CreateCase("TR-SMD-0000001", tempRoot.Path, "tester");

        var result = session.AddSourceFiles(new[] { sourcePath });

        TestAssert.True(!result.Success, "Unsupported source file should fail.");
        TestAssert.Equal(1, session.SourceFiles.Count, "Session should expose rejected source file row.");
        TestAssert.True(!session.SourceFiles[0].Copied, "Rejected source file row should not be marked copied.");
        TestAssert.True(session.SourceFiles[0].Message.Contains("Unsupported source file type: .docx", StringComparison.OrdinalIgnoreCase), "Rejected row should expose unsupported extension message.");
    }

    public static void WorkflowSessionPersistsDetectedProfileWithoutRemovingSourceFiles()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var session = new WorkflowSession(
            store,
            new SourceFileCopyService(() => new DateTimeOffset(2026, 6, 9, 1, 2, 3, TimeSpan.Zero)),
            new SourceInputProfileDetector(() => new DateTimeOffset(2026, 6, 9, 2, 0, 0, TimeSpan.Zero)));
        var caseResult = session.CreateCase("TR-SMD-0000001", tempRoot.Path, "tester");
        var manifest = ManifestSerializer.Read(caseResult.Layout!.ManifestPath);
        var sourceFiles = new[]
        {
            new ManifestSourceFile("C:\\incoming\\points.csv", Path.Combine(caseResult.Layout.SourceDirectory, "points.csv"), ".csv", 10, "2026-06-09T01:00:00Z", "points_computation"),
            new ManifestSourceFile("C:\\incoming\\reference.dwg", Path.Combine(caseResult.Layout.SourceDirectory, "reference.dwg"), ".dwg", 10, "2026-06-09T01:00:00Z", "dwg_reference"),
            new ManifestSourceFile("C:\\incoming\\plan.pdf", Path.Combine(caseResult.Layout.SourceDirectory, "plan.pdf"), ".pdf", 10, "2026-06-09T01:00:00Z", "plan_map_reference")
        };
        ManifestSerializer.Write(caseResult.Layout.ManifestPath, manifest with { Payload = manifest.Payload with { SourceFiles = sourceFiles } });

        var profile = session.RefreshInputProfile();

        var updatedManifest = ManifestSerializer.Read(caseResult.Layout.ManifestPath);
        TestAssert.Equal("scenario_b", profile.ProfileCode, "Workflow profile code mismatch.");
        TestAssert.Equal("scenario_b", updatedManifest.Payload.DetectedProfile!.ProfileCode, "Manifest profile code mismatch.");
        TestAssert.Equal(3, updatedManifest.Payload.SourceFiles.Count, "Profile persistence must preserve source files.");
        TestAssert.Equal("Scenario B - points/computation + DWG + plan/map reference", session.DetectedProfileLabel, "Session detected profile label mismatch.");
        TestAssert.Equal(0, session.IntakeIssues.Count, "Scenario B should not expose intake issues.");
    }

    public static void WorkflowSessionRefreshInputProfileResolvesInnolaWorkflowRule()
    {
        using var tempRoot = new TempDirectory();
        using var rules = TempFile.FromExisting(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "Settings", "WorkflowRules.json"));
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var session = new WorkflowSession(
            store,
            new SourceFileCopyService(() => new DateTimeOffset(2026, 6, 9, 1, 2, 3, TimeSpan.Zero)),
            new SourceInputProfileDetector(() => new DateTimeOffset(2026, 6, 9, 2, 0, 0, TimeSpan.Zero)),
            new SourceFileActionService(),
            new SourceFileActionAuditService(),
            new ManifestPreflightService(),
            new WorkflowRuleResolver(new WorkflowRuleRegistry(() => rules.Path), () => new DateTimeOffset(2026, 6, 9, 3, 0, 0, TimeSpan.Zero)),
            () => new WorkflowRuleSettings("openai", false, "gpt-4.1-mini", "OPENAI_API_KEY", "local"),
            new FakeWorkflowScriptExecutor((_, _) => WorkflowScriptExecutionResult.Failed("Not used.")));
        var caseResult = session.CreateCase("100000206", tempRoot.Path, "tester");
        var computationPath = Path.Combine(caseResult.Layout!.SourceDirectory, "BELLEV029GEOLANCOMSHEET.pdf");
        var planPath = Path.Combine(caseResult.Layout.SourceDirectory, "BELLEV029GEOLAN20230811.pdf");
        File.WriteAllText(computationPath, "computation");
        File.WriteAllText(planPath, "plan");
        var manifest = ManifestSerializer.Read(caseResult.Layout.ManifestPath);
        ManifestSerializer.Write(
            caseResult.Layout.ManifestPath,
            manifest with
            {
                Payload = manifest.Payload with
                {
                    SourceFiles = new[]
                    {
                        new ManifestSourceFile("innola-attachment:computation", computationPath, ".pdf", 10, "2026-06-09T01:00:00Z", "computation_source"),
                        new ManifestSourceFile("innola-attachment:plan", planPath, ".pdf", 10, "2026-06-09T01:00:00Z", "plan_map_reference")
                    },
                    InnolaTransaction = new ManifestInnolaTransaction(
                        "txn-1",
                        "100000206",
                        "task-1",
                        "Assign Computation Task",
                        "parcel_workflow",
                        "Plan Examination",
                        null,
                        "tester",
                        "tester",
                        "Super Group",
                        null,
                        null,
                        "2026-06-09T01:00:00Z")
                }
            });

        var profile = session.RefreshInputProfile();

        var updatedManifest = ManifestSerializer.Read(caseResult.Layout.ManifestPath);
        TestAssert.Equal("scenario_a", profile.ProfileCode, "Profile refresh should detect Scenario A.");
        TestAssert.Equal("scenario_a_two_pdf", updatedManifest.Payload.WorkflowProfile, "Refresh should persist workflow profile.");
        TestAssert.Equal("scenario_a_two_pdf_v1", updatedManifest.Payload.WorkflowRuleId, "Refresh should persist workflow rule id.");
        TestAssert.True(updatedManifest.Payload.ScriptPlan is not null, "Refresh should persist script plan.");
        TestAssert.Equal(2, updatedManifest.Payload.ScriptPlan!.Steps.Count, "Refresh should persist planned Scenario A steps.");
    }

    public static void WorkflowSessionReopensValidCaseFolder()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");
        var sourcePath = Path.Combine(created.Layout!.SourceDirectory, "points.csv");
        File.WriteAllText(sourcePath, "point_id,x,y");
        var manifest = ManifestSerializer.Read(created.Layout.ManifestPath);
        ManifestSerializer.Write(
            created.Layout.ManifestPath,
            manifest with
            {
                Payload = manifest.Payload with
                {
                    SourceFiles = new[]
                    {
                        new ManifestSourceFile("C:\\incoming\\points.csv", sourcePath, ".csv", 12, "2026-06-09T01:00:00Z", "points_computation")
                    },
                    DetectedProfile = new DetectedSourceInputProfile(
                        "scenario_b",
                        "Scenario B - points/computation + DWG + plan/map reference",
                        "matched",
                        "2026-06-09T02:00:00Z",
                        Array.Empty<string>(),
                        Array.Empty<string>())
                }
            });
        var session = new WorkflowSession(store);

        var result = session.ReopenCaseFolder(created.Layout.RootDirectory);

        TestAssert.True(result.Success, "Workflow session should reopen a valid Case Folder.");
        TestAssert.Equal("TR-SMD-0000001", session.TransactionId, "Session transaction ID should be restored.");
        TestAssert.Equal(WorkflowState.Intake, session.CurrentState, "Session should resume intake.");
        TestAssert.Equal(1, session.SourceFiles.Count, "Session source rows should be restored.");
        TestAssert.Equal("Scenario B - points/computation + DWG + plan/map reference", session.DetectedProfileLabel, "Detected profile should be restored.");
        TestAssert.Equal("Case reopened", session.StatusText, "Reopen status text mismatch.");
    }

    public static void WorkflowSessionReopensCaseWithMissingDetectedProfileIssue()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");
        var session = new WorkflowSession(store);

        var result = session.ReopenCaseFolder(created.Layout!.RootDirectory);

        TestAssert.True(result.Success, "Case without detected profile should reopen.");
        TestAssert.Equal("Detected profile: not refreshed", session.DetectedProfileLabel, "Missing detected profile label mismatch.");
        TestAssert.True(session.IntakeIssues.Any(issue => issue.Contains("Refresh Intake", StringComparison.OrdinalIgnoreCase)), "Missing detected profile should surface a refresh issue.");
    }

    public static void WorkflowSessionAllowsSourceActionsAfterReviewApproved()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(tempRoot.Path, "100000206", "tester");
        var copiedPath = Path.Combine(created.Layout!.SourceDirectory, "BELLEV029GEOLANCOMSHEET.pdf");
        File.WriteAllText(copiedPath, "computation");
        var manifest = ManifestSerializer.Read(created.Layout.ManifestPath);
        ManifestSerializer.Write(
            created.Layout.ManifestPath,
            manifest with
            {
                Payload = manifest.Payload with
                {
                    WorkflowState = WorkflowState.ReviewApproved.ToContractValue(),
                    SourceFiles = new[]
                    {
                        new ManifestSourceFile("innola-attachment:computation", copiedPath, ".pdf", 10, "2026-06-12T00:00:00Z", "computation_source")
                    }
                }
            });
        var launcher = new FakeSourceFileLauncher();
        var session = new WorkflowSession(
            store,
            new SourceFileCopyService(),
            new SourceInputProfileDetector(),
            new SourceFileActionService(launcher),
            new SourceFileActionAuditService(),
            new ManifestPreflightService());
        session.ReopenCaseFolder(created.Layout.RootDirectory);

        var result = session.ExecuteSourceFileAction(session.SourceFiles[0], SourceFileAction.Open, "tester");

        TestAssert.True(result.Success, "Copied source files should still open after review approval.");
        TestAssert.Equal(copiedPath, launcher.OpenedPath, "Approved review should still allow source file preview/open actions.");
    }

    public static void WorkflowSessionReviewApprovedDoesNotAllowDraftExtractionRerun()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(tempRoot.Path, "100000206", "tester");
        var manifest = ManifestSerializer.Read(created.Layout!.ManifestPath);
        ManifestSerializer.Write(
            created.Layout.ManifestPath,
            manifest with
            {
                Payload = manifest.Payload with
                {
                    WorkflowState = WorkflowState.ReviewApproved.ToContractValue()
                }
            });
        var session = new WorkflowSession(store);

        session.ReopenCaseFolder(created.Layout.RootDirectory);

        TestAssert.True(!session.CanRunExtractionReview, "Approved review should lock draft extraction rerun.");
    }

    public static void WorkflowSessionReportsReopenFailuresWithoutReplacingActiveCase()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var activeCase = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");
        var session = new WorkflowSession(store);
        session.ReopenCaseFolder(activeCase.Layout!.RootDirectory);
        var invalidCasePath = Path.Combine(tempRoot.Path, "TR-SMD-0000002");
        Directory.CreateDirectory(invalidCasePath);

        var result = session.ReopenCaseFolder(invalidCasePath);

        TestAssert.True(!result.Success, "Missing manifest should fail through session.");
        TestAssert.Equal("TR-SMD-0000001", session.TransactionId, "Failed reopen should not replace active transaction.");
        TestAssert.Equal(WorkflowState.Intake, session.CurrentState, "Failed reopen should not replace active state.");
        TestAssert.True(session.IntakeIssues.Any(issue => issue.Contains("Manifest", StringComparison.OrdinalIgnoreCase)), "Failed reopen should expose recoverability issue.");
    }

    public static void WorkflowSessionDoesNotCreateProcessingArtifactsDuringReopen()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");
        var session = new WorkflowSession(store);

        var result = session.ReopenCaseFolder(created.Layout!.RootDirectory);

        TestAssert.True(result.Success, "Valid Case Folder should reopen.");
        foreach (var artifactPath in new[]
        {
            Path.Combine(created.Layout.WorkingDirectory, "preflight_summary.json"),
            Path.Combine(created.Layout.WorkingDirectory, "extraction_review_data.json"),
            Path.Combine(created.Layout.WorkingDirectory, "approved_review.json"),
            Path.Combine(created.Layout.WorkingDirectory, "validation_summary.json"),
            Path.Combine(created.Layout.OutputDirectory, "output_summary.json"),
            Path.Combine(created.Layout.LogsDirectory, "process.log"),
            Path.Combine(created.Layout.OutputDirectory, "extracted_geometry.geojson")
        })
        {
            TestAssert.True(!File.Exists(artifactPath), $"Reopen must not create processing artifact: {artifactPath}");
        }
    }

    public static void WorkflowSessionClearsReopenedCaseStateWhenCreatingNewCase()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var reopenedCase = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");
        var sourcePath = Path.Combine(reopenedCase.Layout!.SourceDirectory, "points.csv");
        File.WriteAllText(sourcePath, "point_id,x,y");
        var preflightPath = Path.Combine(reopenedCase.Layout.WorkingDirectory, "preflight_summary.json");
        File.WriteAllText(preflightPath, "{}");
        var manifest = ManifestSerializer.Read(reopenedCase.Layout.ManifestPath);
        ManifestSerializer.Write(
            reopenedCase.Layout.ManifestPath,
            manifest with
            {
                Payload = manifest.Payload with
                {
                    SourceFiles = new[]
                    {
                        new ManifestSourceFile("C:\\incoming\\points.csv", sourcePath, ".csv", 12, "2026-06-09T01:00:00Z", "points_computation")
                    },
                    DetectedProfile = new DetectedSourceInputProfile(
                        "scenario_b",
                        "Scenario B - points/computation + DWG + plan/map reference",
                        "matched",
                        "2026-06-09T02:00:00Z",
                        Array.Empty<string>(),
                        Array.Empty<string>())
                }
            });
        var session = new WorkflowSession(store);
        session.ReopenCaseFolder(reopenedCase.Layout.RootDirectory);

        var newCase = session.CreateCase("TR-SMD-0000002", tempRoot.Path, "tester");

        TestAssert.True(newCase.Success, "New case creation should still work after reopen.");
        TestAssert.Equal("TR-SMD-0000002", session.TransactionId, "New case transaction ID should replace reopened case.");
        TestAssert.Equal(0, session.SourceFiles.Count, "New case should not retain reopened source rows.");
        TestAssert.Equal(0, session.IntakeIssues.Count, "New case should not retain reopened recoverability issues.");
        TestAssert.Equal(0, session.AvailableArtifacts.Count, "New case should not retain reopened available artifacts.");
        TestAssert.Equal("Detected profile: not refreshed", session.DetectedProfileLabel, "New case should reset detected profile label.");
    }

    public static void WorkflowSessionSourceActionsWorkAfterReopen()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");
        var sourcePath = Path.Combine(created.Layout!.SourceDirectory, "plan.pdf");
        File.WriteAllText(sourcePath, "source");
        var manifest = ManifestSerializer.Read(created.Layout.ManifestPath);
        ManifestSerializer.Write(
            created.Layout.ManifestPath,
            manifest with
            {
                Payload = manifest.Payload with
                {
                    SourceFiles = new[]
                    {
                        new ManifestSourceFile("C:\\incoming\\plan.pdf", sourcePath, ".pdf", 10, "2026-06-09T01:00:00Z", "plan_map_reference")
                    }
                }
            });
        var launcher = new FakeSourceFileLauncher();
        var session = new WorkflowSession(
            store,
            new SourceFileCopyService(),
            new SourceInputProfileDetector(),
            new SourceFileActionService(launcher),
            new SourceFileActionAuditService(() => new DateTimeOffset(2026, 6, 9, 3, 0, 0, TimeSpan.Zero)));
        session.ReopenCaseFolder(created.Layout.RootDirectory);

        var result = session.ExecuteSourceFileAction(session.SourceFiles[0], SourceFileAction.Open, "tester");

        TestAssert.True(result.Success, "Source action should work after reopen.");
        TestAssert.Equal(sourcePath, launcher.OpenedPath, "Workflow source action should open copied path.");
        TestAssert.Equal("Opened source file.", session.StatusText, "Workflow status should reflect open action.");
    }

    public static void WorkflowSessionSourceActionFailuresAreNonBlocking()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");
        var session = new WorkflowSession(
            store,
            new SourceFileCopyService(),
            new SourceInputProfileDetector(),
            new SourceFileActionService(new FakeSourceFileLauncher()),
            new SourceFileActionAuditService(() => new DateTimeOffset(2026, 6, 9, 3, 0, 0, TimeSpan.Zero)));
        session.ReopenCaseFolder(created.Layout!.RootDirectory);
        var row = new SourceFileCopyResult("C:\\incoming\\missing.pdf", Path.Combine(created.Layout.SourceDirectory, "missing.pdf"), "missing.pdf", ".pdf", 10, null, "copied", "Copied to Case Folder source area.", Copied: true);

        var result = session.ExecuteSourceFileAction(row, SourceFileAction.Open, "tester");

        TestAssert.True(!result.Success, "Missing source action should fail.");
        TestAssert.Equal(WorkflowState.Intake, session.CurrentState, "Failed source action should not change workflow state.");
        TestAssert.True(session.StatusText.Contains("missing", StringComparison.OrdinalIgnoreCase), "Failure should be reported in status text.");
    }

    public static void WorkflowSessionSourceActionsAuditAndAvoidProcessingArtifacts()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");
        var sourcePath = Path.Combine(created.Layout!.SourceDirectory, "plan.pdf");
        File.WriteAllText(sourcePath, "source");
        var session = new WorkflowSession(
            store,
            new SourceFileCopyService(),
            new SourceInputProfileDetector(),
            new SourceFileActionService(new FakeSourceFileLauncher()),
            new SourceFileActionAuditService(() => new DateTimeOffset(2026, 6, 9, 3, 0, 0, TimeSpan.Zero)));
        session.ReopenCaseFolder(created.Layout.RootDirectory);
        var row = new SourceFileCopyResult("C:\\incoming\\plan.pdf", sourcePath, "plan.pdf", ".pdf", 10, null, "copied", "Copied to Case Folder source area.", Copied: true);

        var result = session.ExecuteSourceFileAction(row, SourceFileAction.Reveal, "tester");

        TestAssert.True(result.Success, "Reveal should succeed.");
        TestAssert.True(File.Exists(Path.Combine(created.Layout.WorkingDirectory, "source_action_audit.json")), "Source action audit should be written.");
        foreach (var artifactPath in new[]
        {
            Path.Combine(created.Layout.WorkingDirectory, "preflight_summary.json"),
            Path.Combine(created.Layout.WorkingDirectory, "extraction_review_data.json"),
            Path.Combine(created.Layout.WorkingDirectory, "approved_review.json"),
            Path.Combine(created.Layout.WorkingDirectory, "validation_summary.json"),
            Path.Combine(created.Layout.OutputDirectory, "output_summary.json"),
            Path.Combine(created.Layout.LogsDirectory, "process.log"),
            Path.Combine(created.Layout.OutputDirectory, "extracted_geometry.geojson")
        })
        {
            TestAssert.True(!File.Exists(artifactPath), $"Source actions must not create processing artifact: {artifactPath}");
        }
    }

    public static void WorkflowSessionSourceActionSucceedsWhenExistingAuditIsCorrupt()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");
        var sourcePath = Path.Combine(created.Layout!.SourceDirectory, "plan.pdf");
        File.WriteAllText(sourcePath, "source");
        Directory.CreateDirectory(created.Layout.WorkingDirectory);
        File.WriteAllText(SourceFileActionAuditService.GetAuditPath(created.Layout), "{ not valid json");
        var launcher = new FakeSourceFileLauncher();
        var session = new WorkflowSession(
            store,
            new SourceFileCopyService(),
            new SourceInputProfileDetector(),
            new SourceFileActionService(launcher),
            new SourceFileActionAuditService(() => new DateTimeOffset(2026, 6, 9, 3, 0, 0, TimeSpan.Zero)));
        session.ReopenCaseFolder(created.Layout.RootDirectory);
        var row = new SourceFileCopyResult("C:\\incoming\\plan.pdf", sourcePath, "plan.pdf", ".pdf", 10, null, "copied", "Copied to Case Folder source area.", Copied: true);

        var result = session.ExecuteSourceFileAction(row, SourceFileAction.Open, "tester");

        TestAssert.True(result.Success, "Source action should remain successful even when audit append fails.");
        TestAssert.Equal(sourcePath, launcher.OpenedPath, "Open action should still launch copied path.");
        TestAssert.Equal("Opened source file.", session.StatusText, "Audit failure should not replace source action status.");
        TestAssert.Equal(WorkflowState.Intake, session.CurrentState, "Audit failure should not change workflow state.");
    }

    public static void WorkflowSessionRunManifestPreflightBlocksMissingRole()
    {
        using var tempRoot = new TempDirectory();
        var (layout, _) = ManifestPreflightServiceTests.CreateCaseWithSources(
            tempRoot.Path,
            "scenario_b",
            new[]
            {
                ManifestPreflightServiceTests.Source("points.csv", ".csv", "points_computation"),
                ManifestPreflightServiceTests.Source("reference.dwg", ".dwg", "dwg_reference")
            });
        var session = CreateManifestOnlySession();
        session.ReopenCaseFolder(layout.RootDirectory);

        var summary = session.RunManifestPreflight("tester");

        TestAssert.Equal(WorkflowState.PreflightBlocked, session.CurrentState, "Blockers should move workflow to preflight blocked.");
        TestAssert.Equal("Preflight blocked: missing plan/map reference.", session.StatusText, "Blocked preflight status mismatch.");
        TestAssert.Equal(1, session.PreflightBlockers.Count, "Session should expose preflight blockers.");
        TestAssert.Equal(0, session.PreflightWarnings.Count, "Session should expose preflight warnings.");
        TestAssert.True(session.PreflightPassedChecks.Count > 0, "Session should expose passed checks.");
        TestAssert.Equal("blocked", summary.Payload.Status, "Workflow preflight summary should be blocked.");
        TestAssert.Equal("preflight_blocked", ManifestSerializer.Read(layout.ManifestPath).Payload.WorkflowState, "Manifest workflow state should persist preflight blocked.");
    }

    public static void WorkflowSessionRunManifestPreflightCanPassManifestLayer()
    {
        using var tempRoot = new TempDirectory();
        var (layout, _) = ManifestPreflightServiceTests.CreateCaseWithSources(
            tempRoot.Path,
            "scenario_a",
            new[]
            {
                ManifestPreflightServiceTests.Source("computation.pdf", ".pdf", "computation_source"),
                ManifestPreflightServiceTests.Source("plan.pdf", ".pdf", "plan_map_reference")
            });
        var session = CreateManifestOnlySession();
        session.ReopenCaseFolder(layout.RootDirectory);

        var summary = session.RunManifestPreflight("tester");

        TestAssert.Equal(WorkflowState.PreflightPassed, session.CurrentState, "No blockers should move workflow to preflight passed.");
        TestAssert.Equal("Preflight passed: manifest and environment checks complete.", session.StatusText, "Passed preflight status mismatch.");
        TestAssert.Equal(0, session.PreflightBlockers.Count, "Passed preflight should expose no blockers.");
        TestAssert.True(session.PreflightPassedChecks.Count > 0, "Passed preflight should expose passed checks.");
        TestAssert.Equal("passed", summary.Payload.Status, "Workflow preflight summary should pass.");
        TestAssert.Equal("preflight_passed", ManifestSerializer.Read(layout.ManifestPath).Payload.WorkflowState, "Manifest workflow state should persist preflight passed.");
    }

    public static void WorkflowSessionDraftExtractionCreatesReviewArtifact()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var session = new WorkflowSession(
            store,
            new SourceFileCopyService(),
            new SourceInputProfileDetector(),
            new SourceFileActionService(),
            new SourceFileActionAuditService(),
            new ManifestPreflightService(),
            new WorkflowRuleResolver(),
            WorkflowRuleSettingsLoader.Load,
            new FakeWorkflowScriptExecutor((layout, manifest) =>
            {
                var reviewArtifactPath = Path.Combine(layout.WorkingDirectory, "extraction_review_data.json");
                Directory.CreateDirectory(layout.WorkingDirectory);
                File.WriteAllText(reviewArtifactPath, "{\"transaction_number\":\"100000206\",\"rows\":[]}");
                return new WorkflowScriptExecutionResult(
                    true,
                    null,
                    reviewArtifactPath,
                    new[] { new AvailableArtifact("extraction_review_data.json", reviewArtifactPath) });
            }));
        var layout = CreateInnolaScenarioACase(store, tempRoot.Path);
        session.ReopenCaseFolder(layout.RootDirectory);
        session.RunManifestPreflight("tester");

        var result = session.RunDraftExtractionAsync().GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Draft extraction should succeed.");
        TestAssert.Equal(WorkflowState.ReviewPending, session.CurrentState, "Successful draft extraction should move to review pending.");
        TestAssert.True(File.Exists(Path.Combine(layout.WorkingDirectory, "extraction_review_data.json")), "Draft extraction should create extraction review artifact.");
        TestAssert.True(session.AvailableArtifacts.Any(artifact => artifact.ArtifactName == "extraction_review_data.json"), "Review artifact should be registered.");
        TestAssert.Equal("Draft extraction complete: review artifact generated.", session.StatusText, "Draft extraction success status mismatch.");
    }

    public static void WorkflowSessionDraftExtractionFailureStaysContained()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var session = new WorkflowSession(
            store,
            new SourceFileCopyService(),
            new SourceInputProfileDetector(),
            new SourceFileActionService(),
            new SourceFileActionAuditService(),
            new ManifestPreflightService(),
            new WorkflowRuleResolver(),
            WorkflowRuleSettingsLoader.Load,
            new FakeWorkflowScriptExecutor((_, _) => WorkflowScriptExecutionResult.Failed("Draft extraction failed.")));
        var layout = CreateInnolaScenarioACase(store, tempRoot.Path);
        session.ReopenCaseFolder(layout.RootDirectory);
        session.RunManifestPreflight("tester");

        var result = session.RunDraftExtractionAsync().GetAwaiter().GetResult();

        TestAssert.True(!result.Success, "Draft extraction failure should be reported.");
        TestAssert.Equal(WorkflowState.ExtractionFailed, session.CurrentState, "Failure should move workflow to extraction failed.");
        TestAssert.True(!File.Exists(Path.Combine(layout.WorkingDirectory, "extraction_review_data.json")), "Failure should not create review artifact.");
        TestAssert.True(!File.Exists(Path.Combine(layout.OutputDirectory, "output_summary.json")), "Failure should not create output summary.");
        TestAssert.Equal("Draft extraction failed.", session.StatusText, "Failure status should be preserved.");
    }

    public static void WorkflowSessionDraftExtractionRequiresPreflightPass()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var session = new WorkflowSession(
            store,
            new SourceFileCopyService(),
            new SourceInputProfileDetector(),
            new SourceFileActionService(),
            new SourceFileActionAuditService(),
            new ManifestPreflightService(),
            new WorkflowRuleResolver(),
            WorkflowRuleSettingsLoader.Load,
            new FakeWorkflowScriptExecutor((_, _) => throw new InvalidOperationException("Should not be called.")));
        var layout = CreateInnolaScenarioACase(store, tempRoot.Path);
        session.ReopenCaseFolder(layout.RootDirectory);

        var result = session.RunDraftExtractionAsync().GetAwaiter().GetResult();

        TestAssert.True(!result.Success, "Draft extraction should be blocked before preflight passes.");
        TestAssert.Equal(WorkflowState.Intake, session.CurrentState, "Blocked extraction should not change workflow state.");
        TestAssert.Equal("Run preflight successfully before starting extraction review.", session.StatusText, "Blocked extraction status mismatch.");
    }

    public static void WorkflowSessionAddingSourceAfterPreflightInvalidatesPreflight()
    {
        using var tempRoot = new TempDirectory();
        using var incoming = new TempDirectory();
        var incomingSource = Path.Combine(incoming.Path, "extra-plan.pdf");
        File.WriteAllText(incomingSource, "source");
        var (layout, _) = ManifestPreflightServiceTests.CreateCaseWithSources(
            tempRoot.Path,
            "scenario_a",
            new[]
            {
                ManifestPreflightServiceTests.Source("computation.pdf", ".pdf", "computation_source"),
                ManifestPreflightServiceTests.Source("plan.pdf", ".pdf", "plan_map_reference")
            });
        var session = CreateManifestOnlySession();
        session.ReopenCaseFolder(layout.RootDirectory);
        session.RunManifestPreflight("tester");

        var result = session.AddSourceFiles(new[] { incomingSource }, "plan_map_reference");

        TestAssert.True(result.Success, "Adding source after preflight should still copy the source file.");
        TestAssert.Equal(WorkflowState.Intake, session.CurrentState, "Intake edits should reset workflow state to intake.");
        TestAssert.True(!File.Exists(layout.PreflightSummaryPath), "Intake edits should remove stale preflight summary.");
        TestAssert.Equal(0, session.PreflightBlockers.Count, "Intake edits should clear stale blockers.");
        TestAssert.Equal(0, session.PreflightPassedChecks.Count, "Intake edits should clear stale passed checks.");
        TestAssert.Equal("intake", ManifestSerializer.Read(layout.ManifestPath).Payload.WorkflowState, "Manifest should persist intake state after source edits.");
    }

    public static void WorkflowSessionRefreshingProfileAfterPreflightInvalidatesPreflight()
    {
        using var tempRoot = new TempDirectory();
        var (layout, _) = ManifestPreflightServiceTests.CreateCaseWithSources(
            tempRoot.Path,
            "scenario_a",
            new[]
            {
                ManifestPreflightServiceTests.Source("computation.pdf", ".pdf", "computation_source"),
                ManifestPreflightServiceTests.Source("plan.pdf", ".pdf", "plan_map_reference")
            });
        var session = CreateManifestOnlySession();
        session.ReopenCaseFolder(layout.RootDirectory);
        session.RunManifestPreflight("tester");

        var profile = session.RefreshInputProfile();

        TestAssert.Equal("scenario_a", profile.ProfileCode, "Profile refresh should still detect Scenario A.");
        TestAssert.Equal(WorkflowState.Intake, session.CurrentState, "Profile refresh should reset workflow state to intake.");
        TestAssert.True(!File.Exists(layout.PreflightSummaryPath), "Profile refresh should remove stale preflight summary.");
        TestAssert.Equal(0, session.PreflightBlockers.Count, "Profile refresh should clear stale blockers.");
        TestAssert.Equal(0, session.PreflightPassedChecks.Count, "Profile refresh should clear stale passed checks.");
        TestAssert.Equal("intake", ManifestSerializer.Read(layout.ManifestPath).Payload.WorkflowState, "Manifest should persist intake state after profile refresh.");
    }

    public static void WorkflowSessionRunManifestPreflightHandlesCorruptManifest()
    {
        using var tempRoot = new TempDirectory();
        var (layout, _) = ManifestPreflightServiceTests.CreateCaseWithSources(
            tempRoot.Path,
            "scenario_a",
            new[]
            {
                ManifestPreflightServiceTests.Source("computation.pdf", ".pdf", "computation_source"),
                ManifestPreflightServiceTests.Source("plan.pdf", ".pdf", "plan_map_reference")
            });
        var session = new WorkflowSession(new CaseFolderStore());
        session.ReopenCaseFolder(layout.RootDirectory);
        File.WriteAllText(layout.ManifestPath, "{ not valid json");

        var summary = session.RunManifestPreflight("tester");

        TestAssert.Equal(WorkflowState.PreflightBlocked, session.CurrentState, "Corrupt manifest should move session to preflight blocked.");
        TestAssert.Equal("blocked", summary.Payload.Status, "Corrupt manifest should return a blocked summary.");
        TestAssert.True(summary.Payload.Blockers.Any(check => check.CheckId == "manifest_readable"), "Corrupt manifest blocker should be present.");
        TestAssert.True(session.PreflightBlockers.Any(check => check.CheckId == "manifest_readable"), "Session should expose corrupt manifest blocker.");
        TestAssert.Equal("Preflight blocked: manifest could not be read.", session.StatusText, "Corrupt manifest should produce a non-crashing status.");
    }

    public static void WorkflowSessionReopensPreflightArtifactWithoutDownstreamCommands()
    {
        using var tempRoot = new TempDirectory();
        var (layout, _) = ManifestPreflightServiceTests.CreateCaseWithSources(
            tempRoot.Path,
            "scenario_a",
            new[]
            {
                ManifestPreflightServiceTests.Source("computation.pdf", ".pdf", "computation_source"),
                ManifestPreflightServiceTests.Source("plan.pdf", ".pdf", "plan_map_reference")
            });
        new WorkflowSession(new CaseFolderStore()).ReopenCaseFolder(layout.RootDirectory);
        new ManifestPreflightService().Run(layout, "tester");
        var session = new WorkflowSession(new CaseFolderStore());

        var result = session.ReopenCaseFolder(layout.RootDirectory);

        TestAssert.True(result.Success, "Case with preflight artifact should reopen.");
        TestAssert.True(session.AvailableArtifacts.Any(artifact => artifact.ArtifactName == "preflight_summary.json"), "Preflight summary should be available after reopen.");
        TestAssert.True(!File.Exists(Path.Combine(layout.WorkingDirectory, "extraction_review_data.json")), "Reopen must not create extraction artifact.");
        TestAssert.True(!File.Exists(Path.Combine(layout.OutputDirectory, "output_summary.json")), "Reopen must not create output artifact.");
    }

    public static void WorkflowSessionValidationPassWritesSummaryAndState()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var layout = CreateApprovedReviewCase(store, tempRoot.Path);
        var session = CreateValidationSession(store, new FakeValidationExecutionService(blocked: false));
        session.ReopenCaseFolder(layout.RootDirectory);

        var result = session.RunValidationAsync("tester").GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Validation should succeed for current approved review data.");
        TestAssert.Equal(WorkflowState.ValidationPassed, session.CurrentState, "Passed validation should move workflow to validation passed.");
        TestAssert.True(File.Exists(Path.Combine(layout.WorkingDirectory, "validation_summary.json")), "Validation should create validation summary artifact.");
        TestAssert.True(session.AvailableArtifacts.Any(artifact => artifact.ArtifactName == "validation_summary.json"), "Validation summary should be registered.");
        TestAssert.Equal("Validation passed: output stage is now eligible.", session.StatusText, "Validation pass status mismatch.");
        TestAssert.True(!File.Exists(Path.Combine(layout.OutputDirectory, "output_summary.json")), "Validation should not create output summary.");
    }

    public static void WorkflowSessionValidationBlockedMovesWorkflowToBlockedState()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var layout = CreateApprovedReviewCase(store, tempRoot.Path);
        var session = CreateValidationSession(store, new FakeValidationExecutionService(blocked: true));
        session.ReopenCaseFolder(layout.RootDirectory);

        var result = session.RunValidationAsync("tester").GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Blocked validation should still complete and write a summary.");
        TestAssert.Equal(WorkflowState.ValidationBlocked, session.CurrentState, "Blocking findings should move workflow to validation blocked.");
        TestAssert.Equal("Validation blocked: blocking findings require correction before outputs.", session.StatusText, "Validation blocked status mismatch.");
    }

    public static void WorkflowSessionValidationExceptionBecomesBlockedFailure()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var layout = CreateApprovedReviewCase(store, tempRoot.Path);
        var session = CreateValidationSession(store, new ThrowingValidationExecutionService());
        session.ReopenCaseFolder(layout.RootDirectory);

        var result = session.RunValidationAsync("tester").GetAwaiter().GetResult();

        TestAssert.True(!result.Success, "Validation exception should be converted into a failed result.");
        TestAssert.Equal(WorkflowState.ValidationBlocked, session.CurrentState, "Validation exception should leave workflow in blocked state.");
        TestAssert.True(session.StatusText.Contains("Validation failed:", StringComparison.OrdinalIgnoreCase), "Validation exception should surface a readable failure status.");
    }

    public static void WorkflowSessionValidationRejectsStaleApprovalAndReturnsToReview()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var layout = CreateApprovedReviewCase(store, tempRoot.Path);
        var reviewService = new ExtractionReviewPersistenceService();
        var review = reviewService.Load(layout)!;
        review.Rows[0].Easting = "9999.0";
        reviewService.Save(layout, review, "tester");
        var staleValidationPath = Path.Combine(layout.WorkingDirectory, "validation_summary.json");
        File.WriteAllText(staleValidationPath, "{\"schema_version\":\"1.0.0\"}");
        var session = CreateValidationSession(store, new FakeValidationExecutionService(blocked: false));
        session.ReopenCaseFolder(layout.RootDirectory);

        var result = session.RunValidationAsync("tester").GetAwaiter().GetResult();

        TestAssert.True(!result.Success, "Stale approved review should block validation.");
        TestAssert.Equal(WorkflowState.ReviewPending, session.CurrentState, "Stale approval should return workflow to review pending.");
        TestAssert.True(!File.Exists(staleValidationPath), "Stale validation summary should be cleared.");
        TestAssert.True(session.StatusText.Contains("approve the review again", StringComparison.OrdinalIgnoreCase), "User guidance should instruct re-approval.");
    }

    public static void WorkflowSessionReopenRestoresValidationStateAndArtifact()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var layout = CreateApprovedReviewCase(store, tempRoot.Path);
        var validationService = new FakeValidationExecutionService(blocked: false);
        var firstSession = CreateValidationSession(store, validationService);
        firstSession.ReopenCaseFolder(layout.RootDirectory);
        firstSession.RunValidationAsync("tester").GetAwaiter().GetResult();
        var reopenSession = CreateValidationSession(store, validationService);

        var reopenResult = reopenSession.ReopenCaseFolder(layout.RootDirectory);

        TestAssert.True(reopenResult.Success, "Case with validation summary should reopen.");
        TestAssert.Equal(WorkflowState.ValidationPassed, reopenSession.CurrentState, "Reopen should restore validation passed state.");
        TestAssert.True(reopenSession.CurrentValidationSummary is not null, "Reopen should restore validation summary document.");
        TestAssert.True(reopenSession.AvailableArtifacts.Any(artifact => artifact.ArtifactName == "validation_summary.json"), "Reopen should expose validation summary artifact.");
    }

    public static void WorkflowSessionOutputGenerationCreatesArtifactsAndAdvancesState()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var layout = CreateApprovedReviewCase(store, tempRoot.Path);
        var session = CreateOutputSession(store, new FakeOutputExecutionService(shouldFail: false));
        session.ReopenCaseFolder(layout.RootDirectory);
        session.RunValidationAsync("tester").GetAwaiter().GetResult();

        var result = session.RunOutputsAsync("tester").GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Outputs should succeed for a validated approved review case.");
        TestAssert.Equal(WorkflowState.SpatialReviewPending, session.CurrentState, "Successful outputs should move workflow to spatial review pending.");
        TestAssert.True(File.Exists(Path.Combine(layout.OutputDirectory, "output_summary.json")), "Outputs should create output summary.");
        TestAssert.True(session.AvailableArtifacts.Any(artifact => artifact.ArtifactName == "output_summary.json"), "Output summary should be registered.");
        TestAssert.True(session.AvailableArtifacts.Any(artifact => artifact.ArtifactName.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase)), "Generated geodatabase should be registered.");
        TestAssert.True(session.CurrentOutputSummary is not null, "Output summary should be loaded into session state.");
        TestAssert.Equal("Outputs created: local geometry is ready for spatial review in the map.", session.StatusText, "Output success status mismatch.");
    }

    public static void WorkflowSessionOutputGenerationRequiresValidationPass()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var layout = CreateApprovedReviewCase(store, tempRoot.Path);
        var session = CreateOutputSession(store, new FakeOutputExecutionService(shouldFail: false));
        session.ReopenCaseFolder(layout.RootDirectory);

        var result = session.RunOutputsAsync("tester").GetAwaiter().GetResult();

        TestAssert.True(!result.Success, "Outputs should be blocked until validation passes.");
        TestAssert.Equal(WorkflowState.ReviewApproved, session.CurrentState, "Blocked outputs should not change workflow state.");
        TestAssert.Equal("Validation must pass before output generation can start.", session.StatusText, "Blocked outputs status mismatch.");
    }

    public static void WorkflowSessionOutputFailureReturnsToValidationPassed()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var layout = CreateApprovedReviewCase(store, tempRoot.Path);
        var session = CreateOutputSession(store, new FakeOutputExecutionService(shouldFail: true));
        session.ReopenCaseFolder(layout.RootDirectory);
        session.RunValidationAsync("tester").GetAwaiter().GetResult();

        var result = session.RunOutputsAsync("tester").GetAwaiter().GetResult();

        TestAssert.True(!result.Success, "Output failure should be surfaced.");
        TestAssert.Equal(WorkflowState.ValidationPassed, session.CurrentState, "Failed outputs should return workflow to validation passed.");
        TestAssert.True(!File.Exists(Path.Combine(layout.OutputDirectory, "output_summary.json")), "Failed outputs should not leave output summary behind.");
    }

    public static void WorkflowSessionReopenRestoresSpatialReviewPendingStateAndArtifacts()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var layout = CreateApprovedReviewCase(store, tempRoot.Path);
        var session = CreateOutputSession(store, new FakeOutputExecutionService(shouldFail: false));
        session.ReopenCaseFolder(layout.RootDirectory);
        session.RunValidationAsync("tester").GetAwaiter().GetResult();
        session.RunOutputsAsync("tester").GetAwaiter().GetResult();
        var reopenSession = CreateOutputSession(store, new FakeOutputExecutionService(shouldFail: false));

        var reopenResult = reopenSession.ReopenCaseFolder(layout.RootDirectory);

        TestAssert.True(reopenResult.Success, "Case with outputs should reopen.");
        TestAssert.Equal(WorkflowState.SpatialReviewPending, reopenSession.CurrentState, "Reopen should restore spatial review pending state.");
        TestAssert.True(reopenSession.CurrentOutputSummary is not null, "Reopen should restore output summary.");
        TestAssert.True(reopenSession.AvailableArtifacts.Any(artifact => artifact.ArtifactName == "output_summary.json"), "Reopen should expose output summary artifact.");
    }

    public static void WorkflowSessionSpatialReviewApprovalUnlocksReadyToComplete()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var layout = CreateApprovedReviewCase(store, tempRoot.Path);
        var session = CreateOutputSession(store, new FakeOutputExecutionService(shouldFail: false));
        session.ReopenCaseFolder(layout.RootDirectory);
        session.RunValidationAsync("tester").GetAwaiter().GetResult();
        session.RunOutputsAsync("tester").GetAwaiter().GetResult();

        var approval = session.ApproveSpatialReview("tester");

        TestAssert.True(approval.IsCurrent, "Spatial review approval should succeed when outputs are current.");
        TestAssert.Equal(WorkflowState.SpatialReviewApproved, session.CurrentState, "Spatial review approval should advance workflow to approved.");
        TestAssert.True(session.AvailableArtifacts.Any(artifact => artifact.ArtifactName == "spatial_review_approval.json"), "Approval artifact should be registered.");
    }

    public static void WorkflowSessionReopenInvalidatesStaleSpatialReviewApproval()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var layout = CreateApprovedReviewCase(store, tempRoot.Path);
        var session = CreateOutputSession(store, new FakeOutputExecutionService(shouldFail: false));
        session.ReopenCaseFolder(layout.RootDirectory);
        session.RunValidationAsync("tester").GetAwaiter().GetResult();
        session.RunOutputsAsync("tester").GetAwaiter().GetResult();
        session.ApproveSpatialReview("tester");
        File.AppendAllText(Path.Combine(layout.OutputDirectory, "extracted_geometry.geojson"), "\n{\"changed\":true}");
        var reopenSession = CreateOutputSession(store, new FakeOutputExecutionService(shouldFail: false));

        var reopenResult = reopenSession.ReopenCaseFolder(layout.RootDirectory);

        TestAssert.True(reopenResult.Success, "Case with approved spatial review should reopen.");
        TestAssert.Equal(WorkflowState.SpatialReviewPending, reopenSession.CurrentState, "Changed output artifacts should invalidate spatial review approval.");
        TestAssert.True(reopenSession.IntakeIssues.Any(issue => issue.Contains("Spatial review approval", StringComparison.OrdinalIgnoreCase)), "Reopen should explain why approval was invalidated.");
    }

    private sealed class FakeSourceFileLauncher : ISourceFileLauncher
    {
        public string? OpenedPath { get; private set; }

        public string? RevealedPath { get; private set; }

        public void OpenFile(string copiedPath)
        {
            OpenedPath = copiedPath;
        }

        public void RevealFile(string copiedPath)
        {
            RevealedPath = copiedPath;
        }
    }

    private sealed class FakeWorkflowScriptExecutor : IWorkflowScriptExecutor
    {
        private readonly Func<CaseFolderLayout, ManifestDocument, WorkflowScriptExecutionResult> execute;

        public FakeWorkflowScriptExecutor(Func<CaseFolderLayout, ManifestDocument, WorkflowScriptExecutionResult> execute)
        {
            this.execute = execute;
        }

        public Task<WorkflowScriptExecutionResult> ExecuteDraftExtractionAsync(CaseFolderLayout layout, ManifestDocument manifest, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(execute(layout, manifest));
        }
    }

    private sealed class FakeValidationExecutionService : IValidationExecutionService
    {
        private readonly bool blocked;

        public FakeValidationExecutionService(bool blocked)
        {
            this.blocked = blocked;
        }

        public Task<ValidationExecutionResult> RunAsync(CaseFolderLayout layout, ManifestDocument manifest, string? operatorId, CancellationToken cancellationToken = default)
        {
            var summary = new ValidationSummaryDocument(
                "1.0.0",
                manifest.TransactionId,
                "validation-test",
                "2026-06-12T00:00:00Z",
                operatorId,
                manifest.Payload.ScriptPlan?.SourceManifestHash ?? string.Empty,
                new ValidationSummaryPayload(
                    blocked ? "blocked" : "passed",
                    "sidwell_validation_v1",
                    "1.0.0",
                    new ValidationFindingCounts(0, blocked ? 1 : 0, 0, 0, blocked ? 0 : 1),
                    new[]
                    {
                        new ValidationFinding(
                            "review_rows_resolved",
                            blocked ? "Unresolved rows remain." : "Review rows are resolved.",
                            blocked ? "high" : "passed",
                            blocked ? "failed" : "passed",
                            null,
                            blocked ? "Resolve rows and approve again." : null)
                    }),
                Array.Empty<string>(),
                Array.Empty<string>());
            var summaryPath = Path.Combine(layout.WorkingDirectory, "validation_summary.json");
            File.WriteAllText(summaryPath, System.Text.Json.JsonSerializer.Serialize(summary));
            return Task.FromResult(new ValidationExecutionResult(true, null, summaryPath, summary));
        }
    }

    private sealed class ThrowingValidationExecutionService : IValidationExecutionService
    {
        public Task<ValidationExecutionResult> RunAsync(CaseFolderLayout layout, ManifestDocument manifest, string? operatorId, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Injected validation failure.");
        }
    }

    private sealed class FakeOutputExecutionService : IOutputExecutionService
    {
        private readonly bool shouldFail;

        public FakeOutputExecutionService(bool shouldFail)
        {
            this.shouldFail = shouldFail;
        }

        public Task<OutputExecutionResult> RunAsync(CaseFolderLayout layout, ManifestDocument manifest, string? operatorId, CancellationToken cancellationToken = default)
        {
            if (shouldFail)
            {
                return Task.FromResult(OutputExecutionResult.Failed("Injected output failure."));
            }

            Directory.CreateDirectory(layout.OutputDirectory);
            var gdbPath = Path.Combine(layout.OutputDirectory, $"{manifest.TransactionId}_parcel_output.gdb");
            Directory.CreateDirectory(gdbPath);
            var geoJsonPath = Path.Combine(layout.OutputDirectory, "extracted_geometry.geojson");
            File.WriteAllText(geoJsonPath, "{\"type\":\"FeatureCollection\",\"features\":[]}");
            var summary = new OutputSummaryDocument(
                "1.0.0",
                manifest.TransactionId,
                "output-test",
                "2026-06-12T00:00:00Z",
                operatorId,
                manifest.Payload.ScriptPlan?.SourceManifestHash ?? string.Empty,
                new OutputSummaryPayload(
                    "created",
                    gdbPath,
                    new[] { geoJsonPath },
                    new[] { Path.Combine(gdbPath, "parcel_points") },
                    Path.Combine(gdbPath, "parcel_points"),
                    Path.Combine(gdbPath, "parcel_lines"),
                    Path.Combine(gdbPath, "parcel_polygon"),
                    3,
                    2,
                    1,
                    null,
                    null),
                Array.Empty<string>(),
                Array.Empty<string>());
            var summaryPath = Path.Combine(layout.OutputDirectory, "output_summary.json");
            File.WriteAllText(summaryPath, System.Text.Json.JsonSerializer.Serialize(summary));
            return Task.FromResult(new OutputExecutionResult(true, null, summaryPath, summary));
        }
    }

    private static WorkflowSession CreateManifestOnlySession()
    {
        return new WorkflowSession(
            new CaseFolderStore(),
            new SourceFileCopyService(),
            new SourceInputProfileDetector(),
            new SourceFileActionService(),
            new SourceFileActionAuditService(),
            new ManifestPreflightService());
    }

    private static WorkflowSession CreateValidationSession(CaseFolderStore store, IValidationExecutionService validationExecutionService)
    {
        return new WorkflowSession(
            store,
            new SourceFileCopyService(),
            new SourceInputProfileDetector(),
            new SourceFileActionService(),
            new SourceFileActionAuditService(),
            new ManifestPreflightService(),
            new WorkflowRuleResolver(),
            WorkflowRuleSettingsLoader.Load,
            new FakeWorkflowScriptExecutor((_, _) => WorkflowScriptExecutionResult.Failed("Not used.")),
            new ExtractionReviewPersistenceService(),
            validationExecutionService,
            new ValidationSummaryPersistenceService());
    }

    private static WorkflowSession CreateOutputSession(CaseFolderStore store, IOutputExecutionService outputExecutionService)
    {
        return new WorkflowSession(
            store,
            new SourceFileCopyService(),
            new SourceInputProfileDetector(),
            new SourceFileActionService(),
            new SourceFileActionAuditService(),
            new ManifestPreflightService(),
            new WorkflowRuleResolver(),
            WorkflowRuleSettingsLoader.Load,
            new FakeWorkflowScriptExecutor((_, _) => WorkflowScriptExecutionResult.Failed("Not used.")),
            new ExtractionReviewPersistenceService(),
            new FakeValidationExecutionService(blocked: false),
            new ValidationSummaryPersistenceService(),
            outputExecutionService,
            new OutputSummaryPersistenceService(),
            new SpatialReviewApprovalPersistenceService());
    }

    private static CaseFolderLayout CreateInnolaScenarioACase(CaseFolderStore store, string outputRoot)
    {
        var created = store.CreateCase(outputRoot, "100000206", "tester");
        var layout = created.Layout!;
        var computationPath = Path.Combine(layout.SourceDirectory, "BELLEV029GEOLANCOMSHEET.pdf");
        var planPath = Path.Combine(layout.SourceDirectory, "BELLEV029GEOLAN20230811.pdf");
        Directory.CreateDirectory(layout.SourceDirectory);
        File.WriteAllText(computationPath, "computation");
        File.WriteAllText(planPath, "plan");
        var sourceFiles = new[]
        {
            new ManifestSourceFile("innola-attachment:computation", computationPath, ".pdf", 10, "2026-06-12T00:00:00Z", "computation_source"),
            new ManifestSourceFile("innola-attachment:plan", planPath, ".pdf", 10, "2026-06-12T00:00:00Z", "plan_map_reference")
        };
        var scriptPlan = new WorkflowScriptPlan(
            "1.0.0",
            "scenario_a_two_pdf_v1",
            "1.0.0",
            "scenario_a_two_pdf",
            "2026-06-12T00:00:00Z",
            WorkflowRuleResolver.ComputeSourceManifestHash(sourceFiles),
            new[]
            {
                new WorkflowScriptStep(
                    "extract_points_from_computation",
                    "extraction_adapter",
                    "extract_points_from_computation_pdf",
                    new[] { "computation_source" },
                    new[] { "working/extraction_points.json" },
                    new Dictionary<string, string> { ["provider"] = "local" },
                    300,
                    false,
                    "local",
                    "local"),
                new WorkflowScriptStep(
                    "ocr_plan_map_reference",
                    "extraction_adapter",
                    "ocr_plan_map_pdf",
                    new[] { "plan_map_reference" },
                    new[] { "working/plan_ocr.json" },
                    new Dictionary<string, string> { ["provider"] = "local" },
                    300,
                    false,
                    "local",
                    "local")
            });
        var manifest = ManifestSerializer.Read(layout.ManifestPath);
        ManifestSerializer.Write(
            layout.ManifestPath,
            manifest with
            {
                Payload = manifest.Payload with
                {
                    SourceFiles = sourceFiles,
                    DetectedProfile = new DetectedSourceInputProfile(
                        "scenario_a",
                        "Scenario A - computation + plan/map reference",
                        "matched",
                        "2026-06-12T00:00:00Z",
                        Array.Empty<string>(),
                        Array.Empty<string>()),
                    InnolaTransaction = new ManifestInnolaTransaction(
                        "txn-1",
                        "100000206",
                        "task-1",
                        "Assign Computation Task",
                        "parcel_workflow",
                        "Plan Examination",
                        null,
                        "tester",
                        "tester",
                        "Super Group",
                        null,
                        null,
                        "2026-06-12T00:00:00Z"),
                    WorkflowProfile = "scenario_a_two_pdf",
                    WorkflowRuleId = "scenario_a_two_pdf_v1",
                    WorkflowRuleVersion = "1.0.0",
                    ScriptPlan = scriptPlan
                }
            });
        return layout;
    }

    private static CaseFolderLayout CreateApprovedReviewCase(CaseFolderStore store, string outputRoot)
    {
        var layout = CreateInnolaScenarioACase(store, outputRoot);
        var session = CreateManifestOnlySession();
        session.ReopenCaseFolder(layout.RootDirectory);
        session.RunManifestPreflight("tester");
        var reviewDocument = new ExtractionReviewDocument
        {
            SchemaVersion = "1.0.0",
            TransactionNumber = "100000206",
            ExtractionSource = "draft",
            RootMetadata = new JsonObject()
        };
        reviewDocument.Rows.Add(new ExtractionReviewRow
        {
            RowId = "row-001",
            PointIdentifier = "P1",
            Easting = "1000.0",
            Northing = "2000.0",
            Length = "10.0",
            ExtractionStatus = "Verified",
            SourceEvidence = "Sheet 1",
            RowProvenance = "extracted",
            OriginalValues = new ExtractionReviewOriginalValues
            {
                PointIdentifier = "P1",
                Easting = "1000.0",
                Northing = "2000.0",
                Length = "10.0",
                ExtractionStatus = "Verified",
                SourceEvidence = "Sheet 1"
            }
        });
        reviewDocument.Rows.Add(new ExtractionReviewRow
        {
            RowId = "row-002",
            PointIdentifier = "P2",
            Easting = "1010.0",
            Northing = "2000.0",
            Length = "10.0",
            ExtractionStatus = "Verified",
            SourceEvidence = "Sheet 1",
            RowProvenance = "extracted",
            OriginalValues = new ExtractionReviewOriginalValues
            {
                PointIdentifier = "P2",
                Easting = "1010.0",
                Northing = "2000.0",
                Length = "10.0",
                ExtractionStatus = "Verified",
                SourceEvidence = "Sheet 1"
            }
        });
        reviewDocument.Rows.Add(new ExtractionReviewRow
        {
            RowId = "row-003",
            PointIdentifier = "P3",
            Easting = "1010.0",
            Northing = "2010.0",
            Length = "10.0",
            ExtractionStatus = "Verified",
            SourceEvidence = "Sheet 1",
            RowProvenance = "extracted",
            OriginalValues = new ExtractionReviewOriginalValues
            {
                PointIdentifier = "P3",
                Easting = "1010.0",
                Northing = "2010.0",
                Length = "10.0",
                ExtractionStatus = "Verified",
                SourceEvidence = "Sheet 1"
            }
        });
        var reviewService = new ExtractionReviewPersistenceService();
        var saved = reviewService.Save(layout, reviewDocument, "tester");
        reviewService.Approve(layout, saved.Document!, "tester");
        var manifest = ManifestSerializer.Read(layout.ManifestPath);
        ManifestSerializer.Write(layout.ManifestPath, manifest with { Payload = manifest.Payload with { WorkflowState = WorkflowState.ReviewApproved.ToContractValue() } });
        return layout;
    }
}
