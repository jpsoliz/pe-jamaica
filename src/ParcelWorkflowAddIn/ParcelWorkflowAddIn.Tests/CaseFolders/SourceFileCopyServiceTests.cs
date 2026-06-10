using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Tests.CaseFolders;

internal static class SourceFileCopyServiceTests
{
    public static void CopySourceFilesCopiesAcceptedFilesAndUpdatesManifest()
    {
        using var tempRoot = new TempDirectory();
        using var incoming = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var caseResult = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");
        var sourcePath = Path.Combine(incoming.Path, "Plan.PDF");
        var content = new byte[] { 1, 2, 3, 5, 8 };
        File.WriteAllBytes(sourcePath, content);
        var service = new SourceFileCopyService(() => new DateTimeOffset(2026, 6, 9, 1, 2, 3, TimeSpan.Zero));

        var result = service.CopySourceFiles(caseResult.Layout!, new[] { sourcePath }, "plan_reference");

        TestAssert.True(result.Success, "Accepted source copy should succeed.");
        TestAssert.Equal(1, result.Results.Count, "One file result expected.");
        var copy = result.Results[0];
        TestAssert.True(copy.Copied, "Accepted source should be marked copied.");
        TestAssert.True(File.Exists(copy.CopiedPath!), "Copied file should exist.");
        TestAssert.Equal(content.Length, File.ReadAllBytes(copy.CopiedPath!).Length, "Copied file should preserve byte length.");
        TestAssert.True(copy.CopiedPath!.StartsWith(caseResult.Layout!.SourceDirectory, StringComparison.OrdinalIgnoreCase), "Copied file should stay in source directory.");

        using var document = JsonDocument.Parse(File.ReadAllText(caseResult.Layout.ManifestPath));
        var sourceFile = document.RootElement.GetProperty("payload").GetProperty("source_files")[0];
        TestAssert.Equal(Path.GetFullPath(sourcePath), sourceFile.GetProperty("original_path").GetString(), "Manifest original path mismatch.");
        TestAssert.Equal(Path.GetFullPath(copy.CopiedPath!), sourceFile.GetProperty("copied_path").GetString(), "Manifest copied path mismatch.");
        TestAssert.Equal(".pdf", sourceFile.GetProperty("file_type").GetString(), "Manifest file type mismatch.");
        TestAssert.Equal(content.Length, sourceFile.GetProperty("file_size").GetInt64(), "Manifest file size mismatch.");
        TestAssert.Equal("2026-06-09T01:02:03.0000000Z", sourceFile.GetProperty("copied_at").GetString(), "Manifest copied timestamp mismatch.");
        TestAssert.Equal("plan_reference", sourceFile.GetProperty("source_role").GetString(), "Manifest source role mismatch.");
    }

    public static void CopySourceFilesRejectsUnsupportedExtensions()
    {
        using var tempRoot = new TempDirectory();
        using var incoming = new TempDirectory();
        var store = new CaseFolderStore();
        var caseResult = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");
        var sourcePath = Path.Combine(incoming.Path, "notes.docx");
        File.WriteAllText(sourcePath, "not supported");
        var service = new SourceFileCopyService();

        var result = service.CopySourceFiles(caseResult.Layout!, new[] { sourcePath });

        TestAssert.True(!result.Success, "Unsupported source copy should fail.");
        TestAssert.Equal(1, result.Results.Count, "One file result expected.");
        TestAssert.True(!result.Results[0].Copied, "Unsupported source should not be copied.");
        TestAssert.True(result.Results[0].Message.Contains("Unsupported source file type: .docx", StringComparison.OrdinalIgnoreCase), "Unsupported extension message should be clear.");
        TestAssert.True(!File.Exists(Path.Combine(caseResult.Layout!.SourceDirectory, "notes.docx")), "Unsupported file should not exist in source directory.");
    }

    public static void CopySourceFilesDoesNotOverwriteDuplicateFileNames()
    {
        using var tempRoot = new TempDirectory();
        using var incomingA = new TempDirectory();
        using var incomingB = new TempDirectory();
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), () => "run-test");
        var caseResult = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");
        var firstPath = Path.Combine(incomingA.Path, "points.csv");
        var secondPath = Path.Combine(incomingB.Path, "points.csv");
        File.WriteAllText(firstPath, "first");
        File.WriteAllText(secondPath, "second");
        var service = new SourceFileCopyService(() => new DateTimeOffset(2026, 6, 9, 1, 2, 3, TimeSpan.Zero));

        var result = service.CopySourceFiles(caseResult.Layout!, new[] { firstPath, secondPath });

        TestAssert.True(result.Success, "Duplicate source filenames should be handled non-destructively.");
        TestAssert.Equal(2, result.Results.Count, "Two file results expected.");
        TestAssert.True(result.Results[0].CopiedPath != result.Results[1].CopiedPath, "Duplicate copies should have different destination paths.");
        TestAssert.Equal("first", File.ReadAllText(result.Results[0].CopiedPath!), "First copy should preserve original content.");
        TestAssert.Equal("second", File.ReadAllText(result.Results[1].CopiedPath!), "Second copy should preserve original content.");
    }
}
