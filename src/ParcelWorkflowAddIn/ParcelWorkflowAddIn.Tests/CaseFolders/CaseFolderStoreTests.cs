using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Workflow;

namespace ParcelWorkflowAddIn.Tests.CaseFolders;

internal static class CaseFolderStoreTests
{
    public static void CreateCaseCreatesLayoutAndManifest()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");

        var result = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");

        TestAssert.True(result.Success, "Case creation should succeed.");
        TestAssert.True(Directory.Exists(result.Layout!.SourceDirectory), "source folder should exist.");
        TestAssert.True(Directory.Exists(result.Layout.WorkingDirectory), "working folder should exist.");
        TestAssert.True(Directory.Exists(result.Layout.OutputDirectory), "output folder should exist.");
        TestAssert.True(Directory.Exists(result.Layout.ReportsDirectory), "reports folder should exist.");
        TestAssert.True(Directory.Exists(result.Layout.LogsDirectory), "logs folder should exist.");
        TestAssert.True(File.Exists(result.Layout.ManifestPath), "manifest.json should exist.");

        using var document = JsonDocument.Parse(File.ReadAllText(result.Layout.ManifestPath));
        foreach (var property in document.RootElement.EnumerateObject())
        {
            TestAssert.True(IsSnakeCase(property.Name), $"Top-level field '{property.Name}' should be lowercase snake_case.");
        }

        TestAssert.Equal("TR-SMD-0000001", document.RootElement.GetProperty("transaction_id").GetString(), "Manifest transaction ID mismatch.");
        TestAssert.Equal("intake", document.RootElement.GetProperty("payload").GetProperty("workflow_state").GetString(), "Manifest workflow state mismatch.");
        TestAssert.Equal(0, document.RootElement.GetProperty("payload").GetProperty("source_files").GetArrayLength(), "Manifest source_files should be empty.");
    }

    public static void CreateCaseAcceptsInnolaTransactionNumber()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero), () => "run-innola");

        var result = store.CreateCase(tempRoot.Path, "TR100000004", "tester");

        TestAssert.True(result.Success, "Innola transaction number should be accepted as a safe Case Folder id.");
        TestAssert.True(Directory.Exists(result.Layout!.RootDirectory), "Innola Case Folder should exist.");
        TestAssert.Equal("TR100000004", ManifestSerializer.Read(result.Layout.ManifestPath).TransactionId, "Manifest should preserve Innola transaction number.");
    }

    public static void CreateCaseAcceptsLiveInnolaNumericTransactionNumber()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-live-innola");

        var result = store.CreateCase(tempRoot.Path, "100000206", "tester");

        TestAssert.True(result.Success, "Live Innola numeric transaction number should be accepted as a safe Case Folder id.");
        TestAssert.True(Directory.Exists(result.Layout!.RootDirectory), "Live Innola Case Folder should exist.");
        TestAssert.Equal("100000206", ManifestSerializer.Read(result.Layout.ManifestPath).TransactionId, "Manifest should preserve live Innola transaction number.");
    }

    public static void CreateCaseRejectsDuplicateTransactionFolder()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");

        var first = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");
        var second = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");

        TestAssert.True(first.Success, "First case creation should succeed.");
        TestAssert.True(!second.Success, "Duplicate case creation should fail.");
        TestAssert.True(second.ErrorMessage!.Contains("already exists", StringComparison.OrdinalIgnoreCase), "Duplicate error should be clear.");
    }

    public static void CreateCaseRejectsUnsafeTransactionIds()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore();
        var unsafeIds = new[] { "", "   ", "..\\escape", "TR-SMD-1", "TR-SMD-0000001\\nested" };

        foreach (var transactionId in unsafeIds)
        {
            var result = store.CreateCase(tempRoot.Path, transactionId, null);
            TestAssert.True(!result.Success, $"Unsafe transaction ID '{transactionId}' should fail.");
        }
    }

    public static void CreateCaseReturnsFailureForInvalidOutputRoot()
    {
        var store = new CaseFolderStore();

        var result = store.CreateCase("bad\0path", "TR-SMD-0000001", null);

        TestAssert.True(!result.Success, "Invalid output root should fail.");
        TestAssert.True(result.ErrorMessage!.Contains("could not be created", StringComparison.OrdinalIgnoreCase), "Invalid output root error should be clear.");
    }

    public static void ReopenCaseFolderLoadsManifestStateAndArtifacts()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");
        var sourcePath = Path.Combine(created.Layout!.SourceDirectory, "points.csv");
        File.WriteAllText(sourcePath, "point_id,x,y");
        var preflightPath = Path.Combine(created.Layout.WorkingDirectory, "preflight_summary.json");
        File.WriteAllText(preflightPath, "{}");
        var manifest = ManifestSerializer.Read(created.Layout.ManifestPath);
        var profile = new DetectedSourceInputProfile(
            "scenario_b",
            "Scenario B - points/computation + DWG + plan/map reference",
            "matched",
            "2026-06-09T02:00:00Z",
            Array.Empty<string>(),
            Array.Empty<string>());
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
                    DetectedProfile = profile
                }
            });

        var result = store.ReopenCaseFolder(created.Layout.RootDirectory);

        TestAssert.True(result.Success, "Valid Case Folder should reopen.");
        TestAssert.Equal("TR-SMD-0000001", result.Manifest!.TransactionId, "Reopened manifest transaction ID mismatch.");
        TestAssert.Equal(WorkflowState.Intake, result.ResolvedState, "Reopen should resolve intake state.");
        TestAssert.Equal(1, result.SourceFiles.Count, "Reopen should expose source file rows.");
        TestAssert.Equal("points.csv", result.SourceFiles[0].FileName, "Reopened source file row mismatch.");
        TestAssert.True(result.AvailableArtifacts.Any(artifact => artifact.ArtifactName == "preflight_summary.json" && artifact.Path == preflightPath), "Existing canonical artifact should be discovered.");
        TestAssert.Equal(0, result.RecoverabilityIssues.Count, "Valid Case Folder should not report issues.");
    }

    public static void ReopenCaseFolderReportsMissingManifest()
    {
        using var tempRoot = new TempDirectory();
        var casePath = Path.Combine(tempRoot.Path, "TR-SMD-0000001");
        Directory.CreateDirectory(casePath);
        var store = new CaseFolderStore();

        var result = store.ReopenCaseFolder(casePath);

        TestAssert.True(!result.Success, "Missing manifest should fail reopen.");
        TestAssert.True(result.RecoverabilityIssues.Any(issue => issue.Code == "missing_manifest" && issue.BlocksReopen), "Missing manifest should be a blocking recoverability issue.");
    }

    public static void ReopenCaseFolderReportsCorruptManifest()
    {
        using var tempRoot = new TempDirectory();
        var casePath = Path.Combine(tempRoot.Path, "TR-SMD-0000001");
        Directory.CreateDirectory(casePath);
        File.WriteAllText(Path.Combine(casePath, "manifest.json"), "{ not valid json");
        var store = new CaseFolderStore();

        var result = store.ReopenCaseFolder(casePath);

        TestAssert.True(!result.Success, "Corrupt manifest should fail reopen.");
        TestAssert.True(result.RecoverabilityIssues.Any(issue => issue.Code == "corrupt_manifest" && issue.BlocksReopen), "Corrupt manifest should be a blocking recoverability issue.");
    }

    public static void ReopenCaseFolderReportsMissingCopiedSourceFiles()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");
        var missingCopiedPath = Path.Combine(created.Layout!.SourceDirectory, "missing.pdf");
        var manifest = ManifestSerializer.Read(created.Layout.ManifestPath);
        ManifestSerializer.Write(
            created.Layout.ManifestPath,
            manifest with
            {
                Payload = manifest.Payload with
                {
                    SourceFiles = new[]
                    {
                        new ManifestSourceFile("C:\\incoming\\missing.pdf", missingCopiedPath, ".pdf", 12, "2026-06-09T01:00:00Z", "plan_map_reference")
                    }
                }
            });

        var result = store.ReopenCaseFolder(created.Layout.RootDirectory);

        TestAssert.True(result.Success, "Readable manifest should reopen even when a copied source is missing.");
        TestAssert.Equal(1, result.SourceFiles.Count, "Missing copied source row should still be preserved.");
        TestAssert.True(result.RecoverabilityIssues.Any(issue => issue.Code == "missing_copied_source_file" && !issue.BlocksReopen), "Missing copied source file should be reported as non-blocking recoverability issue.");
    }

    public static void ReopenCaseFolderReportsUnknownWorkflowState()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");
        var manifest = ManifestSerializer.Read(created.Layout!.ManifestPath);
        ManifestSerializer.Write(created.Layout.ManifestPath, manifest with { Payload = manifest.Payload with { WorkflowState = "review_pending" } });

        var result = store.ReopenCaseFolder(created.Layout.RootDirectory);

        TestAssert.True(result.Success, "Readable review-pending manifest should reopen successfully.");
        TestAssert.Equal(WorkflowState.ReviewPending, result.ResolvedState, "Review pending state should now be supported.");
        TestAssert.True(!result.RecoverabilityIssues.Any(issue => issue.Code == "unsupported_workflow_state"), "Supported review pending state should not be reported as unsupported.");
    }

    public static void ReopenCaseFolderSupportsReviewApprovedState()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(tempRoot.Path, "100000206", "tester");
        var manifest = ManifestSerializer.Read(created.Layout!.ManifestPath);
        ManifestSerializer.Write(created.Layout.ManifestPath, manifest with { Payload = manifest.Payload with { WorkflowState = "review_approved" } });

        var result = store.ReopenCaseFolder(created.Layout.RootDirectory);

        TestAssert.True(result.Success, "Readable review-approved manifest should reopen successfully.");
        TestAssert.Equal(WorkflowState.ReviewApproved, result.ResolvedState, "Review approved state should resume correctly.");
    }

    public static void ReopenCaseFolderSupportsValidationStates()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(tempRoot.Path, "100000206", "tester");
        var manifest = ManifestSerializer.Read(created.Layout!.ManifestPath);

        ManifestSerializer.Write(created.Layout.ManifestPath, manifest with { Payload = manifest.Payload with { WorkflowState = "validation_blocked" } });
        var blocked = store.ReopenCaseFolder(created.Layout.RootDirectory);
        TestAssert.True(blocked.Success, "Validation blocked manifest should reopen.");
        TestAssert.Equal(WorkflowState.ValidationBlocked, blocked.ResolvedState, "Validation blocked state should resume correctly.");

        ManifestSerializer.Write(created.Layout.ManifestPath, manifest with { Payload = manifest.Payload with { WorkflowState = "validation_passed" } });
        var passed = store.ReopenCaseFolder(created.Layout.RootDirectory);
        TestAssert.True(passed.Success, "Validation passed manifest should reopen.");
        TestAssert.Equal(WorkflowState.ValidationPassed, passed.ResolvedState, "Validation passed state should resume correctly.");
    }

    public static void ReopenCaseFolderSupportsOutputStates()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(tempRoot.Path, "100000206", "tester");
        var manifest = ManifestSerializer.Read(created.Layout!.ManifestPath);

        ManifestSerializer.Write(created.Layout.ManifestPath, manifest with { Payload = manifest.Payload with { WorkflowState = "output_running" } });
        var running = store.ReopenCaseFolder(created.Layout.RootDirectory);
        TestAssert.True(running.Success, "Output running manifest should reopen.");
        TestAssert.Equal(WorkflowState.ValidationPassed, running.ResolvedState, "Interrupted output generation should recover to validation passed.");

        ManifestSerializer.Write(created.Layout.ManifestPath, manifest with { Payload = manifest.Payload with { WorkflowState = "output_created" } });
        var createdState = store.ReopenCaseFolder(created.Layout.RootDirectory);
        TestAssert.True(createdState.Success, "Output created manifest should reopen.");
        TestAssert.Equal(WorkflowState.OutputCreated, createdState.ResolvedState, "Output created state should resume correctly.");
    }

    private static bool IsSnakeCase(string value)
    {
        return value.All(character => char.IsLower(character) || char.IsDigit(character) || character == '_');
    }
}
