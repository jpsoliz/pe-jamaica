using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Tests.CaseFolders;

internal static class SourceFileActionServiceTests
{
    public static void OpenUsesCopiedCaseFolderPath()
    {
        using var tempRoot = new TempDirectory();
        var layout = CaseFolderLayout.For(tempRoot.Path, "TR-SMD-0000001");
        Directory.CreateDirectory(layout.SourceDirectory);
        var copiedPath = Path.Combine(layout.SourceDirectory, "plan.pdf");
        File.WriteAllText(copiedPath, "source");
        var launcher = new FakeSourceFileLauncher();
        var service = new SourceFileActionService(launcher);
        var row = SourceRow(copiedPath, ".pdf");

        var result = service.Execute(layout, row, SourceFileAction.Open);

        TestAssert.True(result.Success, "Open should succeed for an existing copied source file.");
        TestAssert.Equal("opened", result.Status, "Open status mismatch.");
        TestAssert.Equal(copiedPath, launcher.OpenedPath, "Open should use copied Case Folder path.");
    }

    public static void RevealUsesCopiedCaseFolderPath()
    {
        using var tempRoot = new TempDirectory();
        var layout = CaseFolderLayout.For(tempRoot.Path, "TR-SMD-0000001");
        Directory.CreateDirectory(layout.SourceDirectory);
        var copiedPath = Path.Combine(layout.SourceDirectory, "plan.pdf");
        File.WriteAllText(copiedPath, "source");
        var launcher = new FakeSourceFileLauncher();
        var service = new SourceFileActionService(launcher);
        var row = SourceRow(copiedPath, ".pdf");

        var result = service.Execute(layout, row, SourceFileAction.Reveal);

        TestAssert.True(result.Success, "Reveal should succeed for an existing copied source file.");
        TestAssert.Equal("revealed", result.Status, "Reveal status mismatch.");
        TestAssert.Equal(copiedPath, launcher.RevealedPath, "Reveal should target copied Case Folder path.");
    }

    public static void MissingCopiedFileReturnsNonBlockingFailure()
    {
        using var tempRoot = new TempDirectory();
        var layout = CaseFolderLayout.For(tempRoot.Path, "TR-SMD-0000001");
        Directory.CreateDirectory(layout.SourceDirectory);
        var row = SourceRow(Path.Combine(layout.SourceDirectory, "missing.pdf"), ".pdf");
        var service = new SourceFileActionService(new FakeSourceFileLauncher());

        var result = service.Execute(layout, row, SourceFileAction.Open);

        TestAssert.True(!result.Success, "Missing source file should fail.");
        TestAssert.Equal("missing", result.Status, "Missing source file status mismatch.");
        TestAssert.True(result.Message.Contains("missing", StringComparison.OrdinalIgnoreCase), "Missing file message should be clear.");
    }

    public static void TamperedCopiedPathOutsideSourceDirectoryIsBlocked()
    {
        using var tempRoot = new TempDirectory();
        var layout = CaseFolderLayout.For(tempRoot.Path, "TR-SMD-0000001");
        Directory.CreateDirectory(layout.SourceDirectory);
        var outsidePath = Path.Combine(tempRoot.Path, "outside.pdf");
        File.WriteAllText(outsidePath, "source");
        var service = new SourceFileActionService(new FakeSourceFileLauncher());
        var row = SourceRow(outsidePath, ".pdf");

        var result = service.Execute(layout, row, SourceFileAction.Open);

        TestAssert.True(!result.Success, "Source action should reject copied paths outside the source folder.");
        TestAssert.Equal("blocked", result.Status, "Tampered path status mismatch.");
    }

    public static void DwgTifAndTiffAreMapRouteCandidatesWithUnavailableStatus()
    {
        using var tempRoot = new TempDirectory();
        var layout = CaseFolderLayout.For(tempRoot.Path, "TR-SMD-0000001");
        Directory.CreateDirectory(layout.SourceDirectory);
        var service = new SourceFileActionService(new FakeSourceFileLauncher());

        foreach (var extension in new[] { ".dwg", ".tif", ".tiff" })
        {
            var copiedPath = Path.Combine(layout.SourceDirectory, $"source{extension}");
            File.WriteAllText(copiedPath, "source");

            var result = service.Execute(layout, SourceRow(copiedPath, extension), SourceFileAction.RouteToMap);

            TestAssert.True(!result.Success, $"Map routing should be unavailable for {extension} until an adapter exists.");
            TestAssert.Equal("map_route_unavailable", result.Status, $"Map route status mismatch for {extension}.");
            TestAssert.True(SourceFileActionService.IsMapRouteCandidate(extension), $"{extension} should be a map-route candidate.");
        }
    }

    public static void AuditIsWrittenOnlyWhenOperatorIdentityIsAvailable()
    {
        using var tempRoot = new TempDirectory();
        var layout = CaseFolderLayout.For(tempRoot.Path, "TR-SMD-0000001");
        Directory.CreateDirectory(layout.SourceDirectory);
        var copiedPath = Path.Combine(layout.SourceDirectory, "plan.pdf");
        File.WriteAllText(copiedPath, "source");
        var audit = new SourceFileActionAuditService(() => new DateTimeOffset(2026, 6, 9, 3, 0, 0, TimeSpan.Zero));
        var result = SourceFileActionResult.Succeeded(SourceFileAction.Open, copiedPath, "opened", "Opened source file.");
        var row = SourceRow(copiedPath, ".pdf");

        audit.Record(layout, "TR-SMD-0000001", row, result, null);
        TestAssert.True(!File.Exists(Path.Combine(layout.WorkingDirectory, "source_action_audit.json")), "Audit should not be written without operator identity.");

        audit.Record(layout, "TR-SMD-0000001", row, result, "tester");

        var auditPath = Path.Combine(layout.WorkingDirectory, "source_action_audit.json");
        TestAssert.True(File.Exists(auditPath), "Audit file should be written when operator identity is available.");
        using var document = JsonDocument.Parse(File.ReadAllText(auditPath));
        var root = document.RootElement;
        TestAssert.Equal("1.0.0", root.GetProperty("schema_version").GetString(), "Audit schema version mismatch.");
        TestAssert.Equal("TR-SMD-0000001", root.GetProperty("transaction_id").GetString(), "Audit transaction ID mismatch.");
        var firstEvent = root.GetProperty("events")[0];
        TestAssert.Equal("tester", firstEvent.GetProperty("operator_id").GetString(), "Audit operator ID mismatch.");
        TestAssert.Equal("open", firstEvent.GetProperty("action").GetString(), "Audit action mismatch.");
        TestAssert.Equal("opened", firstEvent.GetProperty("status").GetString(), "Audit status mismatch.");
    }

    private static SourceFileCopyResult SourceRow(string copiedPath, string extension)
    {
        return new SourceFileCopyResult(
            Path.Combine("C:\\incoming", Path.GetFileName(copiedPath)),
            copiedPath,
            Path.GetFileName(copiedPath),
            extension,
            10,
            null,
            "copied",
            "Copied to Case Folder source area.",
            Copied: true);
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
}
