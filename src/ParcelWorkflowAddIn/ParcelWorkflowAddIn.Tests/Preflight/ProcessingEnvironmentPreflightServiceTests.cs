using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Tests.Preflight;
using ParcelWorkflowAddIn.Workflow;

namespace ParcelWorkflowAddIn.Tests.Preflight;

internal static class ProcessingEnvironmentPreflightServiceTests
{
    private const string SidwellPython = @"C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe";

    public static void EnvironmentProbeContributesPassedChecksToSummary()
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
        var service = new ManifestPreflightService(
            () => new DateTimeOffset(2026, 6, 10, 4, 0, 0, TimeSpan.Zero),
            () => "preflight-env",
            new FakeEnvironmentPreflightService(new ProcessingEnvironmentPreflightResult(
                Array.Empty<PreflightCheck>(),
                Array.Empty<PreflightCheck>(),
                new[]
                {
                    PreflightCheck.PassedForCategory("python", "python_package_arcpy_available", "Passed: ArcPy is available.", SidwellPython)
                })));

        var summary = service.Run(layout, "tester");

        TestAssert.Equal("passed", summary.Payload.Status, "Environment passed checks should keep a valid preflight passed.");
        TestAssert.True(summary.Payload.PassedChecks.Any(check => check.Category == "python" && check.CheckId == "python_package_arcpy_available"), "Summary should include environment passed checks.");
        var written = PreflightSummarySerializer.Read(layout.PreflightSummaryPath);
        TestAssert.True(written.Payload.PassedChecks.Any(check => check.Category == "python"), "Written summary should persist environment checks.");
    }

    public static void MissingPythonExecutableCreatesBlocker()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateBareLayout(tempRoot.Path);
        var missingPython = Path.Combine(tempRoot.Path, "missing-python", "python.exe");
        var service = new ProcessingEnvironmentPreflightService(
            Settings(missingPython),
            new FakeProcessRunner(new ProcessRunResult(0, "package:arcpy:ok", string.Empty, false)),
            new FakeArcGisProEnvironmentProvider("3.6.0"));

        var result = service.RunAsync(layout).GetAwaiter().GetResult();

        TestAssert.True(result.Blockers.Any(check => check.CheckId == "python_executable_exists" && check.AffectedPath == missingPython), "Missing configured Python executable should block with affected path.");
    }

    public static void SidwellPythonPathIsAcceptedWhenFakeProbeReportsAvailable()
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
        var service = new ManifestPreflightService(
            () => new DateTimeOffset(2026, 6, 10, 4, 0, 0, TimeSpan.Zero),
            () => "preflight-sidwell-path",
            new FakeEnvironmentPreflightService(new ProcessingEnvironmentPreflightResult(
                Array.Empty<PreflightCheck>(),
                Array.Empty<PreflightCheck>(),
                new[]
                {
                    PreflightCheck.PassedForCategory("python", "python_executable_exists", "Passed: configured Python executable exists.", SidwellPython),
                    PreflightCheck.PassedForCategory("python", "python_executable_invokable", "Passed: configured Python executable can be invoked.", SidwellPython),
                    PreflightCheck.PassedForCategory("python", "python_package_arcpy_available", "Passed: Python package arcpy is available.", SidwellPython)
                })));

        var summary = service.Run(layout, "tester");

        TestAssert.Equal("passed", summary.Payload.Status, "Sidwell Python path should be accepted when the injected probe reports it available.");
        TestAssert.True(summary.Payload.PassedChecks.Any(check => check.AffectedPath == SidwellPython && check.CheckId == "python_executable_exists"), "Summary should preserve the configured Sidwell Python path.");
    }

    public static void MissingArcPyIsRequiredBlocker()
    {
        using var tempRoot = new TempDirectory();
        var pythonPath = Path.Combine(tempRoot.Path, "python.exe");
        File.WriteAllText(pythonPath, "fake");
        var layout = CreateBareLayout(tempRoot.Path);
        var service = new ProcessingEnvironmentPreflightService(
            Settings(pythonPath),
            new FakeProcessRunner(new ProcessRunResult(0, "package:arcpy:missing", string.Empty, false)),
            new FakeArcGisProEnvironmentProvider("3.6.0"));

        var result = service.RunAsync(layout).GetAwaiter().GetResult();

        TestAssert.True(result.Blockers.Any(check => check.CheckId == "python_package_arcpy_available"), "Missing ArcPy should block by default.");
    }

    public static void OptionalPackageMissingIsWarning()
    {
        using var tempRoot = new TempDirectory();
        var pythonPath = Path.Combine(tempRoot.Path, "python.exe");
        File.WriteAllText(pythonPath, "fake");
        var layout = CreateBareLayout(tempRoot.Path);
        var settings = new ProcessingEnvironmentSettings("3.6", "net8.0-windows", pythonPath, new[] { "arcpy" }, new[] { "pandas" }, true, true);
        var service = new ProcessingEnvironmentPreflightService(
            settings,
            new FakeProcessRunner(new ProcessRunResult(0, "package:arcpy:ok\npackage:pandas:missing", string.Empty, false)),
            new FakeArcGisProEnvironmentProvider("3.6.0"));

        var result = service.RunAsync(layout).GetAwaiter().GetResult();

        TestAssert.True(result.Warnings.Any(check => check.CheckId == "python_package_pandas_available"), "Optional missing packages should warn.");
        TestAssert.True(result.Blockers.All(check => check.CheckId != "python_package_pandas_available"), "Optional missing packages should not block.");
    }

    public static void DisabledPackageProbeRecordsDisabledCheck()
    {
        using var tempRoot = new TempDirectory();
        var pythonPath = Path.Combine(tempRoot.Path, "python.exe");
        File.WriteAllText(pythonPath, "fake");
        var layout = CreateBareLayout(tempRoot.Path);
        var catalog = new PreflightRuleCatalog(
            Path.Combine(tempRoot.Path, "PreflightRules.json"),
            UsingSafeDefaults: false,
            LoadWarning: null,
            new[]
            {
                new PreflightRuleDefinition("arcgis_unknown_version_behavior", "arcgis_pro", "Unknown ArcGIS Pro version handling", string.Empty, true, "warning", false),
                new PreflightRuleDefinition("python_package_probe", "python", "Python package probe", string.Empty, false, "configured", false),
                new PreflightRuleDefinition("dwg_readiness_probe", "dwg", "DWG readiness probe", string.Empty, true, "blocker", false)
            });
        var service = new ProcessingEnvironmentPreflightService(
            Settings(pythonPath),
            new FakeProcessRunner(new ProcessRunResult(0, "package:arcpy:missing", string.Empty, false)),
            new FakeArcGisProEnvironmentProvider("3.6.0"),
            catalog);

        var result = service.RunAsync(layout).GetAwaiter().GetResult();

        TestAssert.True(result.Warnings.Any(check => check.CheckId == "python_package_probe" && check.Status == "disabled"), "Disabled package probe should be recorded as disabled.");
        TestAssert.True(result.Blockers.All(check => check.CheckId != "python_package_arcpy_available"), "Disabled package probe should skip missing package blockers.");
    }

    public static void ArcGisPro37IsCompatibleWithConfigured36Lane()
    {
        using var tempRoot = new TempDirectory();
        var pythonPath = Path.Combine(tempRoot.Path, "python.exe");
        File.WriteAllText(pythonPath, "fake");
        var layout = CreateBareLayout(tempRoot.Path);
        var service = new ProcessingEnvironmentPreflightService(
            Settings(pythonPath),
            new FakeProcessRunner(new ProcessRunResult(0, "package:arcpy:ok", string.Empty, false)),
            new FakeArcGisProEnvironmentProvider("3.7.0"));

        var result = service.RunAsync(layout).GetAwaiter().GetResult();

        TestAssert.True(result.Blockers.All(check => check.CheckId != "arcgis_pro_version_compatible"), "ArcGIS Pro 3.7 should be compatible with a 3.6 add-in lane.");
        TestAssert.True(result.PassedChecks.Any(check => check.CheckId == "arcgis_pro_version_compatible"), "Compatible 3.7 runtime should be recorded as passed.");
    }

    public static void ArcGisProAssembly136IsCompatibleWithConfigured36Lane()
    {
        using var tempRoot = new TempDirectory();
        var pythonPath = Path.Combine(tempRoot.Path, "python.exe");
        File.WriteAllText(pythonPath, "fake");
        var layout = CreateBareLayout(tempRoot.Path);
        var service = new ProcessingEnvironmentPreflightService(
            Settings(pythonPath),
            new FakeProcessRunner(new ProcessRunResult(0, "package:arcpy:ok", string.Empty, false)),
            new FakeArcGisProEnvironmentProvider("13.6.0.0"));

        var result = service.RunAsync(layout).GetAwaiter().GetResult();

        TestAssert.True(result.Blockers.All(check => check.CheckId != "arcgis_pro_version_compatible"), "ArcGIS Pro SDK assembly version 13.6 should normalize to Pro 3.6.");
        TestAssert.True(result.PassedChecks.Any(check => check.CheckId == "arcgis_pro_version_compatible" && check.Message.Contains("3.6.0.0", StringComparison.OrdinalIgnoreCase)), "Normalized Pro 3.6 version should be recorded as passed.");
    }

    public static void ArcGisPro35IsNotCompatibleWithConfigured36Lane()
    {
        using var tempRoot = new TempDirectory();
        var pythonPath = Path.Combine(tempRoot.Path, "python.exe");
        File.WriteAllText(pythonPath, "fake");
        var layout = CreateBareLayout(tempRoot.Path);
        var service = new ProcessingEnvironmentPreflightService(
            Settings(pythonPath),
            new FakeProcessRunner(new ProcessRunResult(0, "package:arcpy:ok", string.Empty, false)),
            new FakeArcGisProEnvironmentProvider("3.5.2"));

        var result = service.RunAsync(layout).GetAwaiter().GetResult();

        TestAssert.True(result.Blockers.Any(check => check.CheckId == "arcgis_pro_version_compatible"), "ArcGIS Pro 3.5 should remain incompatible.");
    }

    public static void PythonTimeoutIsSanitizedBlocker()
    {
        using var tempRoot = new TempDirectory();
        var pythonPath = Path.Combine(tempRoot.Path, "python.exe");
        File.WriteAllText(pythonPath, "fake");
        var layout = CreateBareLayout(tempRoot.Path);
        var service = new ProcessingEnvironmentPreflightService(
            Settings(pythonPath),
            new FakeProcessRunner(new ProcessRunResult(-1, "token=abc password=secret", "stack trace", true)),
            new FakeArcGisProEnvironmentProvider("3.6.0"));

        var result = service.RunAsync(layout).GetAwaiter().GetResult();

        var blocker = result.Blockers.Single(check => check.CheckId == "python_probe_timeout");
        TestAssert.True(!blocker.Message.Contains("secret", StringComparison.OrdinalIgnoreCase), "Timeout blocker should not leak raw output.");
        TestAssert.True(!blocker.Message.Contains("token", StringComparison.OrdinalIgnoreCase), "Timeout blocker should not leak tokens.");
    }

    public static void UnknownArcGisVersionRuleCanEscalateToBlocker()
    {
        using var tempRoot = new TempDirectory();
        var pythonPath = Path.Combine(tempRoot.Path, "python.exe");
        File.WriteAllText(pythonPath, "fake");
        var layout = CreateBareLayout(tempRoot.Path);
        var catalog = new PreflightRuleCatalog(
            Path.Combine(tempRoot.Path, "PreflightRules.json"),
            UsingSafeDefaults: false,
            LoadWarning: null,
            new[]
            {
                new PreflightRuleDefinition("arcgis_unknown_version_behavior", "arcgis_pro", "Unknown ArcGIS Pro version handling", string.Empty, true, "blocker", false),
                new PreflightRuleDefinition("python_package_probe", "python", "Python package probe", string.Empty, true, "configured", false),
                new PreflightRuleDefinition("dwg_readiness_probe", "dwg", "DWG readiness probe", string.Empty, true, "blocker", false)
            });
        var service = new ProcessingEnvironmentPreflightService(
            Settings(pythonPath),
            new FakeProcessRunner(new ProcessRunResult(0, "package:arcpy:ok", string.Empty, false)),
            new FakeArcGisProEnvironmentProvider(null),
            catalog);

        var result = service.RunAsync(layout).GetAwaiter().GetResult();

        TestAssert.True(result.Blockers.Any(check => check.CheckId == "arcgis_pro_version_detected"), "Unknown ArcGIS version should become a blocker when the rule severity is blocker.");
    }

    public static void WorkflowSessionAsyncPreflightPreventsDuplicateRun()
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
        var gate = new TaskCompletionSource<ProcessingEnvironmentPreflightResult>();
        var session = CreateSession(new ManifestPreflightService(
            () => new DateTimeOffset(2026, 6, 10, 4, 0, 0, TimeSpan.Zero),
            () => "preflight-async",
            new DelayedEnvironmentPreflightService(gate.Task)));
        session.ReopenCaseFolder(layout.RootDirectory);

        var first = session.RunManifestPreflightAsync("tester");
        var second = session.RunManifestPreflightAsync("tester").GetAwaiter().GetResult();
        gate.SetResult(ProcessingEnvironmentPreflightResult.Empty);
        first.GetAwaiter().GetResult();

        TestAssert.Equal("not-run", second.RunId, "Duplicate preflight should not start a second run.");
        TestAssert.True(second.Errors.Any(error => error.Contains("already running", StringComparison.OrdinalIgnoreCase)), "Duplicate preflight should explain the running state.");
    }

    public static void ReopenShowsEnvironmentChecks()
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
        var service = new ManifestPreflightService(
            () => new DateTimeOffset(2026, 6, 10, 4, 0, 0, TimeSpan.Zero),
            () => "preflight-reopen",
            new FakeEnvironmentPreflightService(new ProcessingEnvironmentPreflightResult(
                Array.Empty<PreflightCheck>(),
                new[] { PreflightCheck.WarningForCategory("arcgis_pro", "arcgis_pro_version_detected", "ArcGIS Pro version could not be detected.") },
                new[] { PreflightCheck.PassedForCategory("python", "python_executable_invokable", "Passed: Python can be invoked.") })));
        service.Run(layout, "tester");
        var session = new WorkflowSession(new CaseFolderStore());

        var result = session.ReopenCaseFolder(layout.RootDirectory);

        TestAssert.True(result.Success, "Case should reopen.");
        TestAssert.True(session.PreflightWarnings.Any(check => check.Category == "arcgis_pro"), "Reopen should show environment warnings.");
        TestAssert.True(session.PreflightPassedChecks.Any(check => check.Category == "python"), "Reopen should show environment passed checks.");
    }

    public static void EnvironmentPreflightDoesNotCreateDownstreamArtifacts()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateBareLayout(tempRoot.Path);
        var pythonPath = Path.Combine(tempRoot.Path, "python.exe");
        File.WriteAllText(pythonPath, "fake");
        var service = new ProcessingEnvironmentPreflightService(
            Settings(pythonPath),
            new FakeProcessRunner(new ProcessRunResult(0, "package:arcpy:ok", string.Empty, false)),
            new FakeArcGisProEnvironmentProvider("3.6.0"));

        service.RunAsync(layout).GetAwaiter().GetResult();

        foreach (var artifactPath in new[]
        {
            Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"),
            Path.Combine(layout.WorkingDirectory, "approved_review.json"),
            Path.Combine(layout.WorkingDirectory, "validation_summary.json"),
            Path.Combine(layout.OutputDirectory, "output_summary.json"),
            Path.Combine(layout.OutputDirectory, "extracted_geometry.geojson")
        })
        {
            TestAssert.True(!File.Exists(artifactPath), $"Environment preflight must not create downstream artifact: {artifactPath}");
        }
    }

    private static ProcessingEnvironmentSettings Settings(string pythonPath)
    {
        return new ProcessingEnvironmentSettings("3.6", "net8.0-windows", pythonPath, new[] { "arcpy" }, Array.Empty<string>(), true, true);
    }

    private static CaseFolderLayout CreateBareLayout(string root)
    {
        var layout = CaseFolderLayout.FromRootDirectory(Path.Combine(root, "TR-SMD-0000001"));
        Directory.CreateDirectory(layout.RootDirectory);
        Directory.CreateDirectory(layout.SourceDirectory);
        Directory.CreateDirectory(layout.WorkingDirectory);
        Directory.CreateDirectory(layout.OutputDirectory);
        return layout;
    }

    private static WorkflowSession CreateSession(ManifestPreflightService service)
    {
        return new WorkflowSession(
            new CaseFolderStore(),
            new SourceFileCopyService(),
            new SourceInputProfileDetector(),
            new SourceFileActionService(),
            new SourceFileActionAuditService(),
            service);
    }

    private sealed class FakeEnvironmentPreflightService : IProcessingEnvironmentPreflightService
    {
        private readonly ProcessingEnvironmentPreflightResult result;

        public FakeEnvironmentPreflightService(ProcessingEnvironmentPreflightResult result)
        {
            this.result = result;
        }

        public Task<ProcessingEnvironmentPreflightResult> RunAsync(CaseFolderLayout layout, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class DelayedEnvironmentPreflightService : IProcessingEnvironmentPreflightService
    {
        private readonly Task<ProcessingEnvironmentPreflightResult> resultTask;

        public DelayedEnvironmentPreflightService(Task<ProcessingEnvironmentPreflightResult> resultTask)
        {
            this.resultTask = resultTask;
        }

        public Task<ProcessingEnvironmentPreflightResult> RunAsync(CaseFolderLayout layout, CancellationToken cancellationToken = default)
        {
            return resultTask;
        }
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        private readonly ProcessRunResult result;

        public FakeProcessRunner(ProcessRunResult result)
        {
            this.result = result;
        }

        public Task<ProcessRunResult> RunAsync(
            string executablePath,
            string arguments,
            TimeSpan timeout,
            IReadOnlyDictionary<string, string?>? environmentVariables = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class FakeArcGisProEnvironmentProvider : IArcGisProEnvironmentProvider
    {
        private readonly string? version;

        public FakeArcGisProEnvironmentProvider(string? version)
        {
            this.version = version;
        }

        public string? GetArcGisProVersion()
        {
            return version;
        }
    }
}
