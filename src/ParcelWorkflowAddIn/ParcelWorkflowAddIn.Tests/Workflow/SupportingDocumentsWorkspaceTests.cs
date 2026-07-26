namespace ParcelWorkflowAddIn.Tests.Workflow;

using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Workflow;
using ParcelWorkflowAddIn.Workflow.Review;

internal static class SupportingDocumentsWorkspaceTests
{
    public static void ReadableSupportingDocumentsKeepOnlyCopiedSupportedFiles()
    {
        var sourceFiles = new[]
        {
            Item("plan.pdf", ".pdf", copied: true),
            Item("notes.txt", ".txt", copied: true),
            Item("survey.dwg", ".dwg", copied: true),
            Item("letter.docx", ".docx", copied: true),
            Item("archive.zip", ".zip", copied: true),
            Item("bundle.rar", ".rar", copied: true),
            Item("image.png", ".png", copied: true),
            Item("missing.pdf", ".pdf", copied: false)
        };

        var options = SupportingDocumentWorkspaceProjection.BuildReadableSupportingDocumentOptions(sourceFiles);

        TestAssert.Equal("letter.docx,notes.txt,plan.pdf,survey.dwg", string.Join(",", options.Select(item => item.FileLabel)), "Supporting document viewer should list copied PDF/TXT/DOC/DOCX/DWG files only.");
    }

    public static void SupportingDocumentsTabTitleUsesTransactionNumber()
    {
        TestAssert.Equal("TR-100000872", SupportingDocumentWorkspaceProjection.FormatTransactionLabel("100000872"), "Numeric transaction numbers should be shown with the TR prefix.");
        TestAssert.Equal("TR-100000872", SupportingDocumentWorkspaceProjection.FormatTransactionLabel("TR100000872"), "Compact TR transaction numbers should be normalized for display.");
        TestAssert.Equal("TR-100000872", SupportingDocumentWorkspaceProjection.FormatTransactionLabel("TR-100000872"), "Already formatted transaction numbers should be preserved.");
    }

    public static void SupportingDocumentPdfUsesEmbeddedViewerProjection()
    {
        using var tempRoot = new TempDirectory();
        var copiedPath = Path.Combine(tempRoot.Path, "plan.pdf");
        File.WriteAllText(copiedPath, "%PDF test");
        var state = ReviewSourceViewerStateProjector.Build(Source("plan.pdf", ".pdf", copiedPath), "embedded_browser");

        TestAssert.True(state.UsesBrowser, "Copied PDFs should project to the embedded browser viewer state when browser mode is enabled.");
        TestAssert.Equal(copiedPath, state.FullPath, "PDF viewer should use the copied case-folder path.");
    }

    public static void SupportingDocumentTextAndOfficeDwgProjectionUseExpectedModes()
    {
        using var tempRoot = new TempDirectory();
        var textPath = Path.Combine(tempRoot.Path, "notes.txt");
        var docxPath = Path.Combine(tempRoot.Path, "letter.docx");
        var dwgPath = Path.Combine(tempRoot.Path, "survey.dwg");
        File.WriteAllText(textPath, "notes");
        File.WriteAllText(docxPath, "office document placeholder");
        File.WriteAllText(dwgPath, "dwg placeholder");
        var text = Source("notes.txt", ".txt", textPath);
        var docx = Source("letter.docx", ".docx", docxPath);
        var dwg = Source("survey.dwg", ".dwg", dwgPath);

        TestAssert.True(SupportingDocumentWorkspaceProjection.IsTextDocument(text), "TXT files should be routed to the read-only text viewer.");
        TestAssert.Equal(ReviewSourceViewerMode.Unsupported, ReviewSourceViewerStateProjector.Build(docx, "embedded_browser").Mode, "Existing DOCX files should use the explicit unsupported-format fallback.");
        TestAssert.Equal(ReviewSourceViewerMode.Unsupported, ReviewSourceViewerStateProjector.Build(dwg, "embedded_browser").Mode, "Existing DWG files should use the explicit unsupported-format fallback.");
        TestAssert.True(!ReviewSourceViewerStateProjector.Build(docx, "embedded_browser").CanRenderEmbedded, "DOCX files should use preview-unavailable fallback unless an embedded viewer is added later.");
        TestAssert.True(!ReviewSourceViewerStateProjector.Build(dwg, "embedded_browser").CanRenderEmbedded, "DWG files should use preview-unavailable fallback unless an embedded viewer is added later.");
    }

    public static void ReadableSupportingDocumentsTolerateMalformedCopiedPaths()
    {
        var sourceFiles = new[]
        {
            new SourceFileListItem(Source("plan.pdf", ".pdf", "bad\0path.pdf")),
            new SourceFileListItem(Source("plan.pdf", ".pdf", "bad\0path.pdf")),
            new SourceFileListItem(Source("notes.txt", ".txt", "C:\\case\\source\\notes.txt"))
        };

        var options = SupportingDocumentWorkspaceProjection.BuildReadableSupportingDocumentOptions(sourceFiles);

        TestAssert.Equal(2, options.Count, "Malformed copied paths should not crash option projection and should still be grouped by fallback identity.");
        TestAssert.Equal("notes.txt,plan.pdf", string.Join(",", options.Select(item => item.FileLabel)), "Fallback grouping should keep one readable entry per malformed copied-path identity.");
    }

    private static SourceFileListItem Item(string fileName, string fileType, bool copied)
    {
        return new SourceFileListItem(Source(
            fileName,
            fileType,
            copied ? $"C:\\case\\source\\{fileName}" : $"C:\\case\\source\\missing-{fileName}",
            copied));
    }

    private static SourceFileCopyResult Source(string fileName, string fileType, string copiedPath, bool copied = true)
    {
        return new SourceFileCopyResult(
            $"C:\\incoming\\{fileName}",
            copiedPath,
            fileName,
            fileType,
            10,
            "supporting_document",
            copied ? "copied" : "missing",
            copied ? "Copied" : "Missing",
            copied);
    }
}
