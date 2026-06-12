using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow;
using ParcelWorkflowAddIn.WorkflowRules;

namespace ParcelWorkflowAddIn.Tests.Preflight;

internal static class ManifestPreflightServiceTests
{
    public static void ManifestPreflightPassesValidScenarioA()
    {
        using var tempRoot = new TempDirectory();
        var (layout, _) = CreateCaseWithSources(
            tempRoot.Path,
            "scenario_a",
            new[]
            {
                Source("computation.pdf", ".pdf", "computation_source"),
                Source("plan.pdf", ".pdf", "plan_map_reference")
            });
        var service = new ManifestPreflightService(() => new DateTimeOffset(2026, 6, 9, 4, 0, 0, TimeSpan.Zero), () => "preflight-run");

        var summary = service.Run(layout, "tester");

        TestAssert.Equal("passed", summary.Payload.Status, "Scenario A manifest preflight should pass.");
        TestAssert.Equal(0, summary.Payload.Blockers.Count, "Valid Scenario A should have no blockers.");
        TestAssert.True(summary.Payload.PassedChecks.Any(check => check.CheckId == "required_role_computation_source"), "Scenario A should pass computation role check.");
        TestAssert.True(summary.Payload.PassedChecks.Any(check => check.CheckId == "required_role_plan_map_reference"), "Scenario A should pass plan role check.");
        TestAssert.True(File.Exists(layout.PreflightSummaryPath), "Preflight summary should be written.");
        AssertNoDownstreamArtifacts(layout);
    }

    public static void ManifestPreflightPassesValidScenarioB()
    {
        using var tempRoot = new TempDirectory();
        var (layout, _) = CreateCaseWithSources(
            tempRoot.Path,
            "scenario_b",
            new[]
            {
                Source("points.csv", ".csv", "points_computation"),
                Source("reference.dwg", ".dwg", "dwg_reference"),
                Source("plan.pdf", ".pdf", "plan_map_reference")
            });

        var summary = new ManifestPreflightService().Run(layout, "tester");

        TestAssert.Equal("passed", summary.Payload.Status, "Scenario B manifest preflight should pass.");
        TestAssert.Equal(0, summary.Payload.Blockers.Count, "Valid Scenario B should have no blockers.");
        TestAssert.True(summary.Payload.PassedChecks.Any(check => check.CheckId == "required_role_dwg_reference"), "Scenario B should pass DWG role check.");
    }

    public static void ManifestPreflightBlocksMissingDetectedProfile()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");

        var summary = new ManifestPreflightService().Run(created.Layout!, "tester");

        TestAssert.Equal("blocked", summary.Payload.Status, "Missing detected profile should block.");
        TestAssert.True(summary.Payload.Blockers.Any(check => check.CheckId == "detected_profile_present"), "Missing detected profile blocker should be present.");
    }

    public static void ManifestPreflightBlocksMissingRequiredRole()
    {
        using var tempRoot = new TempDirectory();
        var (layout, _) = CreateCaseWithSources(
            tempRoot.Path,
            "scenario_b",
            new[]
            {
                Source("points.csv", ".csv", "points_computation"),
                Source("reference.dwg", ".dwg", "dwg_reference")
            });

        var summary = new ManifestPreflightService().Run(layout, "tester");

        TestAssert.Equal("blocked", summary.Payload.Status, "Missing plan role should block.");
        TestAssert.True(summary.Payload.Blockers.Any(check => check.CheckId == "required_role_plan_map_reference"), "Missing plan role blocker should be present.");
    }

    public static void ManifestPreflightBlocksMissingCopiedFile()
    {
        using var tempRoot = new TempDirectory();
        var (layout, sources) = CreateCaseWithSources(
            tempRoot.Path,
            "scenario_a",
            new[]
            {
                Source("computation.pdf", ".pdf", "computation_source"),
                Source("plan.pdf", ".pdf", "plan_map_reference")
            });
        File.Delete(sources[1].CopiedPath);

        var summary = new ManifestPreflightService().Run(layout, "tester");

        TestAssert.Equal("blocked", summary.Payload.Status, "Missing copied file should block.");
        TestAssert.True(summary.Payload.Blockers.Any(check => check.CheckId == "source_file_exists_plan_map_reference"), "Missing source file blocker should be present.");
    }

    public static void ManifestPreflightBlocksUnsupportedExtension()
    {
        using var tempRoot = new TempDirectory();
        var (layout, _) = CreateCaseWithSources(
            tempRoot.Path,
            "scenario_a",
            new[]
            {
                Source("computation.pdf", ".pdf", "computation_source"),
                Source("plan.docx", ".docx", "plan_map_reference")
            });

        var summary = new ManifestPreflightService().Run(layout, "tester");

        TestAssert.Equal("blocked", summary.Payload.Status, "Unsupported extension should block.");
        TestAssert.True(summary.Payload.Blockers.Any(check => check.CheckId == "source_file_extension_plan_map_reference"), "Unsupported extension blocker should be present.");
    }

    public static void ManifestPreflightBlocksTamperedCopiedPath()
    {
        using var tempRoot = new TempDirectory();
        var externalPath = System.IO.Path.Combine(tempRoot.Path, "outside.pdf");
        File.WriteAllText(externalPath, "outside");
        var (layout, _) = CreateCaseWithSources(
            tempRoot.Path,
            "scenario_a",
            new[]
            {
                Source("computation.pdf", ".pdf", "computation_source"),
                new SourceSeed("outside.pdf", ".pdf", "plan_map_reference", externalPath, CreateFile: false)
            });

        var summary = new ManifestPreflightService().Run(layout, "tester");

        TestAssert.Equal("blocked", summary.Payload.Status, "Tampered copied path should block.");
        TestAssert.True(summary.Payload.Blockers.Any(check => check.CheckId == "source_file_contained_plan_map_reference"), "Source containment blocker should be present.");
    }

    public static void ManifestPreflightSummaryUsesSnakeCaseGroups()
    {
        using var tempRoot = new TempDirectory();
        var (layout, _) = CreateCaseWithSources(
            tempRoot.Path,
            "scenario_a",
            new[]
            {
                Source("computation.pdf", ".pdf", "computation_source"),
                Source("plan.pdf", ".pdf", "plan_map_reference")
            });

        new ManifestPreflightService(() => new DateTimeOffset(2026, 6, 9, 4, 0, 0, TimeSpan.Zero), () => "preflight-run").Run(layout, "tester");

        using var document = JsonDocument.Parse(File.ReadAllText(layout.PreflightSummaryPath));
        TestAssert.True(document.RootElement.GetProperty("payload").TryGetProperty("blockers", out _), "Preflight payload should include blockers.");
        TestAssert.True(document.RootElement.GetProperty("payload").TryGetProperty("warnings", out _), "Preflight payload should include warnings.");
        TestAssert.True(document.RootElement.GetProperty("payload").TryGetProperty("passed_checks", out _), "Preflight payload should include passed_checks.");
        TestAssert.True(!string.IsNullOrWhiteSpace(document.RootElement.GetProperty("source_manifest_hash").GetString()), "Preflight summary should include manifest hash.");
    }

    public static void ManifestPreflightHashIgnoresWorkflowState()
    {
        using var tempRoot = new TempDirectory();
        var (layout, _) = CreateCaseWithSources(
            tempRoot.Path,
            "scenario_a",
            new[]
            {
                Source("computation.pdf", ".pdf", "computation_source"),
                Source("plan.pdf", ".pdf", "plan_map_reference")
            });
        var service = new ManifestPreflightService(() => new DateTimeOffset(2026, 6, 9, 4, 0, 0, TimeSpan.Zero), () => "preflight-run");

        var firstHash = service.Run(layout, "tester").SourceManifestHash;
        var manifest = ManifestSerializer.Read(layout.ManifestPath);
        ManifestSerializer.Write(
            layout.ManifestPath,
            manifest with { Payload = manifest.Payload with { WorkflowState = "preflight_passed" } });

        var secondHash = service.Run(layout, "tester").SourceManifestHash;

        TestAssert.Equal(firstHash, secondHash, "Preflight source_manifest_hash should ignore workflow state transitions.");
    }

    public static void ManifestPreflightBlocksInnolaTransactionWithoutScriptPlan()
    {
        using var tempRoot = new TempDirectory();
        var (layout, _) = CreateCaseWithSources(
            tempRoot.Path,
            "scenario_a",
            new[]
            {
                Source("BELLEV029GEOLANCOMSHEET.pdf", ".pdf", "computation_source"),
                Source("BELLEV029GEOLAN20230811.pdf", ".pdf", "plan_map_reference")
            });
        var manifest = ManifestSerializer.Read(layout.ManifestPath);
        ManifestSerializer.Write(
            layout.ManifestPath,
            manifest with { Payload = manifest.Payload with { InnolaTransaction = InnolaTransaction() } });

        var summary = new ManifestPreflightService().Run(layout, "tester");

        TestAssert.Equal("blocked", summary.Payload.Status, "Innola transactions without a script plan should block.");
        TestAssert.True(summary.Payload.Blockers.Any(check => check.CheckId == "workflow_rule_resolved"), "Missing workflow rule blocker should be present.");
    }

    public static void ManifestPreflightBlocksStaleScriptPlan()
    {
        using var tempRoot = new TempDirectory();
        var (layout, sources) = CreateCaseWithSources(
            tempRoot.Path,
            "scenario_a",
            new[]
            {
                Source("BELLEV029GEOLANCOMSHEET.pdf", ".pdf", "computation_source"),
                Source("BELLEV029GEOLAN20230811.pdf", ".pdf", "plan_map_reference")
            });
        var manifest = ManifestSerializer.Read(layout.ManifestPath);
        var plan = ScriptPlan("sha256:stale", sources);
        ManifestSerializer.Write(
            layout.ManifestPath,
            manifest with
            {
                Payload = manifest.Payload with
                {
                    InnolaTransaction = InnolaTransaction(),
                    WorkflowProfile = plan.WorkflowProfile,
                    WorkflowRuleId = plan.RuleId,
                    WorkflowRuleVersion = plan.RuleVersion,
                    ScriptPlan = plan
                }
            });

        var summary = new ManifestPreflightService().Run(layout, "tester");

        TestAssert.Equal("blocked", summary.Payload.Status, "Stale script plans should block.");
        TestAssert.True(summary.Payload.Blockers.Any(check => check.CheckId == "script_plan_current"), "Stale script plan blocker should be present.");
    }

    public static void ManifestPreflightPassesCurrentScriptPlan()
    {
        using var tempRoot = new TempDirectory();
        var (layout, sources) = CreateCaseWithSources(
            tempRoot.Path,
            "scenario_a",
            new[]
            {
                Source("BELLEV029GEOLANCOMSHEET.pdf", ".pdf", "computation_source"),
                Source("BELLEV029GEOLAN20230811.pdf", ".pdf", "plan_map_reference")
            });
        var manifest = ManifestSerializer.Read(layout.ManifestPath);
        var plan = ScriptPlan(WorkflowRuleResolver.ComputeSourceManifestHash(sources), sources);
        ManifestSerializer.Write(
            layout.ManifestPath,
            manifest with
            {
                Payload = manifest.Payload with
                {
                    InnolaTransaction = InnolaTransaction(),
                    WorkflowProfile = plan.WorkflowProfile,
                    WorkflowRuleId = plan.RuleId,
                    WorkflowRuleVersion = plan.RuleVersion,
                    ScriptPlan = plan
                }
            });

        var summary = new ManifestPreflightService().Run(layout, "tester");

        TestAssert.Equal("passed", summary.Payload.Status, "Current script plan should pass the manifest preflight layer.");
        TestAssert.True(summary.Payload.PassedChecks.Any(check => check.CheckId == "workflow_rule_resolved"), "Workflow rule passed check should be present.");
    }

    internal static (CaseFolderLayout Layout, IReadOnlyList<ManifestSourceFile> Sources) CreateCaseWithSources(
        string outputRoot,
        string profileCode,
        IReadOnlyList<SourceSeed> sourceSeeds)
    {
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var created = store.CreateCase(outputRoot, "TR-SMD-0000001", "tester");
        var sources = sourceSeeds.Select(seed =>
        {
            var copiedPath = seed.CopiedPath ?? System.IO.Path.Combine(created.Layout!.SourceDirectory, seed.FileName);
            if (seed.CreateFile)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(copiedPath)!);
                File.WriteAllText(copiedPath, seed.FileName);
            }

            return new ManifestSourceFile($"C:\\incoming\\{seed.FileName}", copiedPath, seed.Extension, 12, "2026-06-09T01:00:00Z", seed.SourceRole);
        }).ToArray();
        var manifest = ManifestSerializer.Read(created.Layout!.ManifestPath);
        var profile = profileCode switch
        {
            "scenario_a" => new DetectedSourceInputProfile("scenario_a", SourceInputProfile.ScenarioALabel, "matched", "2026-06-09T02:00:00Z", Array.Empty<string>(), Array.Empty<string>()),
            "scenario_b" => new DetectedSourceInputProfile("scenario_b", SourceInputProfile.ScenarioBLabel, "matched", "2026-06-09T02:00:00Z", Array.Empty<string>(), Array.Empty<string>()),
            "unsupported_intake" => new DetectedSourceInputProfile("unsupported_intake", SourceInputProfile.UnsupportedIntakeLabel, "unsupported", "2026-06-09T02:00:00Z", Array.Empty<string>(), new[] { "Unsupported intake." }),
            _ => new DetectedSourceInputProfile("incomplete_intake", SourceInputProfile.IncompleteIntakeLabel, "incomplete", "2026-06-09T02:00:00Z", new[] { "plan_map_reference" }, new[] { "Missing: plan/map reference." })
        };

        ManifestSerializer.Write(
            created.Layout.ManifestPath,
            manifest with { Payload = manifest.Payload with { SourceFiles = sources, DetectedProfile = profile } });
        return (created.Layout, sources);
    }

    internal static SourceSeed Source(string fileName, string extension, string role)
    {
        return new SourceSeed(fileName, extension, role, CopiedPath: null, CreateFile: true);
    }

    internal sealed record SourceSeed(string FileName, string Extension, string SourceRole, string? CopiedPath, bool CreateFile);

    private static ManifestInnolaTransaction InnolaTransaction()
    {
        return new ManifestInnolaTransaction(
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
            "2026-06-09T03:00:00Z");
    }

    private static WorkflowScriptPlan ScriptPlan(string sourceManifestHash, IReadOnlyList<ManifestSourceFile> sources)
    {
        _ = sources;
        return new WorkflowScriptPlan(
            "1.0.0",
            "scenario_a_two_pdf_v1",
            "1.0.0",
            "scenario_a_two_pdf",
            "2026-06-09T03:00:00Z",
            sourceManifestHash,
            new[]
            {
                new WorkflowScriptStep(
                    "extract_points_from_computation",
                    "extraction_adapter",
                    "extract_points_from_computation_pdf",
                    new[] { "computation_source" },
                    new[] { "working/extraction_points.json" },
                    new Dictionary<string, string>
                    {
                        ["provider"] = "local_or_openai_ocr",
                        ["openai_key_env"] = "OPENAI_API_KEY"
                    },
                    300,
                    true,
                    "openai",
                    "local")
            });
    }

    private static void AssertNoDownstreamArtifacts(CaseFolderLayout layout)
    {
        foreach (var artifactPath in new[]
        {
            System.IO.Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"),
            System.IO.Path.Combine(layout.WorkingDirectory, "approved_review.json"),
            System.IO.Path.Combine(layout.WorkingDirectory, "validation_summary.json"),
            System.IO.Path.Combine(layout.OutputDirectory, "output_summary.json"),
            System.IO.Path.Combine(layout.LogsDirectory, "process.log"),
            System.IO.Path.Combine(layout.OutputDirectory, "extracted_geometry.geojson")
        })
        {
            TestAssert.True(!File.Exists(artifactPath), $"Manifest preflight must not create downstream artifact: {artifactPath}");
        }
    }
}
