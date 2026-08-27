namespace ParcelWorkflowAddIn.Tests.Workflow;

using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Tests;
using ParcelWorkflowAddIn.Workflow;
using ParcelWorkflowAddIn.Workflow.Pla;
using ParcelWorkflowAddIn.Workflow.Review;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class SupportingDocumentCropTests
{
    public static void CropCommandEligibilityAllowsCopiedPdfAndRasterOnly()
    {
        using var tempRoot = new TempDirectory();
        var caseRoot = Path.Combine(tempRoot.Path, "case");
        Directory.CreateDirectory(caseRoot);
        var pdfPath = Path.Combine(caseRoot, "plan.pdf");
        var pngPath = Path.Combine(caseRoot, "plan.png");
        var txtPath = Path.Combine(caseRoot, "notes.txt");
        File.WriteAllText(pdfPath, "%PDF placeholder");
        WritePng(pngPath, 10, 10);
        File.WriteAllText(txtPath, "notes");

        TestAssert.True(SupportingDocumentWorkspaceProjection.CanCropSupportingDocument(Source("plan.pdf", ".pdf", pdfPath), caseRoot, out _), "Copied case-folder PDF should enable crop.");
        TestAssert.True(SupportingDocumentWorkspaceProjection.CanCropSupportingDocument(Source("plan.png", ".png", pngPath), caseRoot, out _), "Copied case-folder PNG should enable crop.");
        TestAssert.False(SupportingDocumentWorkspaceProjection.CanCropSupportingDocument(Source("notes.txt", ".txt", txtPath), caseRoot, out var unsupportedReason), "TXT should not enable crop.");
        TestAssert.True(unsupportedReason.Contains("PDF, PNG, JPG", StringComparison.Ordinal), "Unsupported tooltip should name croppable formats.");
        TestAssert.False(SupportingDocumentWorkspaceProjection.CanCropSupportingDocument(Source("missing.png", ".png", Path.Combine(caseRoot, "missing.png")), caseRoot, out var missingReason), "Missing copied document should disable crop.");
        TestAssert.True(missingReason.Contains("missing", StringComparison.OrdinalIgnoreCase), "Missing tooltip should be clear.");
        TestAssert.False(SupportingDocumentWorkspaceProjection.CanCropSupportingDocument(Source("outside.png", ".png", pngPath), Path.Combine(tempRoot.Path, "other-case"), out var outsideReason), "Documents outside the active case folder should be blocked.");
        TestAssert.True(outsideReason.Contains("outside", StringComparison.OrdinalIgnoreCase), "Outside-case tooltip should be clear.");
    }

    public static void CropRequestValidationBlocksBadPageEmptyBoundsAndUnsupportedSource()
    {
        using var tempRoot = new TempDirectory();
        var pngPath = Path.Combine(tempRoot.Path, "plan.png");
        var txtPath = Path.Combine(tempRoot.Path, "notes.txt");
        WritePng(pngPath, 100, 80);
        File.WriteAllText(txtPath, "notes");
        var service = new DocumentCropRenderingService();

        var invalidPage = service.ValidateExportRequest(Request(pngPath, pageIndex: 2, new DocumentCropRectangle(0, 0, 10, 10)));
        var empty = service.ValidateExportRequest(Request(pngPath, pageIndex: 0, new DocumentCropRectangle(0, 0, 0, 10)));
        var outOfBounds = service.ValidateExportRequest(Request(pngPath, pageIndex: 0, new DocumentCropRectangle(90, 70, 20, 20)));
        var unsupported = service.ValidateExportRequest(Request(txtPath, pageIndex: 0, new DocumentCropRectangle(0, 0, 10, 10)));

        TestAssert.False(invalidPage.CanContinue, "Invalid page should be blocked.");
        TestAssert.Equal("page_invalid", invalidPage.Code, "Invalid page code mismatch.");
        TestAssert.False(empty.CanContinue, "Empty selection should be blocked.");
        TestAssert.Equal("selection_empty", empty.Code, "Empty selection code mismatch.");
        TestAssert.False(outOfBounds.CanContinue, "Out-of-bounds selection should be blocked.");
        TestAssert.Equal("selection_out_of_bounds", outOfBounds.Code, "Out-of-bounds code mismatch.");
        TestAssert.False(unsupported.CanContinue, "Unsupported source should be blocked.");
        TestAssert.Equal("source_unsupported", unsupported.Code, "Unsupported source code mismatch.");
    }

    public static void CropDpiDefaultsAndOutputSizeGuardAreExplicit()
    {
        var service = new DocumentCropRenderingService();
        var warning = service.ValidateExportRequest(new DocumentCropExportRequest(
            Path.Combine(AppContext.BaseDirectory, "fake.pdf"),
            0,
            new DocumentCropRectangle(0, 0, 3000, 2000),
            300));
        var hardBlock = service.ValidateExportRequest(new DocumentCropExportRequest(
            Path.Combine(AppContext.BaseDirectory, "fake.pdf"),
            0,
            new DocumentCropRectangle(0, 0, 6000, 3000),
            600));

        TestAssert.Equal(300, DocumentCropRenderingService.DefaultDpi, "Crop DPI default should be 300.");
        TestAssert.Equal("200,300,400,600", string.Join(",", DocumentCropRenderingService.SupportedDpiValues), "Crop DPI options mismatch.");
        TestAssert.False(warning.CanContinue, "Missing fake source should fail before size validation.");

        using var tempRoot = new TempDirectory();
        var fakePdf = Path.Combine(tempRoot.Path, "plan.pdf");
        File.WriteAllText(fakePdf, "%PDF placeholder");
        warning = service.ValidateExportRequest(new DocumentCropExportRequest(fakePdf, 0, new DocumentCropRectangle(0, 0, 3000, 2000), 300));
        hardBlock = service.ValidateExportRequest(new DocumentCropExportRequest(fakePdf, 0, new DocumentCropRectangle(0, 0, 6000, 3000), 600));
        TestAssert.True(warning.CanContinue, "Large but allowed PDF crop should continue with warning.");
        TestAssert.True(!string.IsNullOrWhiteSpace(warning.Warning), "Large allowed crop should warn.");
        TestAssert.False(hardBlock.CanContinue, "Extremely large PDF crop should be blocked.");
        TestAssert.Equal("output_too_large", hardBlock.Code, "Hard output guard code mismatch.");
    }

    public static async Task CropSavePersistsPngMetadataAndRestoresForCurrentTransaction()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateCase(tempRoot.Path, out var source);
        var service = new PlaBSupportingDocumentCropService(new DocumentCropRenderingService(), new RecordingDetailService(), () => CreateSession(), getUtcNow: () => FixedNow());

        var result = await service.SaveCropAsync(layout, CreateTransaction("100001999"), "100000628", source, Request(source.CopiedPath!, 0, new DocumentCropRectangle(5, 5, 30, 20)));
        var restored = PlaBSupportingDocumentCropService.LoadCrop(layout, "100001999");
        var mismatched = PlaBSupportingDocumentCropService.LoadCrop(layout, "100009999");

        TestAssert.True(result.Success, result.Message);
        TestAssert.True(File.Exists(Path.Combine(layout.RootDirectory, "working", "pla_b", "survey_diagram_selection.png")), "Crop PNG should use the fixed PLA_B path.");
        TestAssert.Equal("source/source-plan.png", restored?.SourceRelativePath, "Crop metadata should store case-relative source path.");
        TestAssert.Equal("working/pla_b/survey_diagram_selection.png", restored?.PngRelativePath, "Crop metadata should store case-relative PNG path.");
        TestAssert.Equal("100001999", restored?.CurrentTransactionNumber, "Crop metadata should target the Current TR.");
        TestAssert.Equal("100000628", restored?.PeNumber, "Crop metadata should preserve PE number when available.");
        TestAssert.Equal("source_pixels", restored?.SourceCoordinateUnits, "Raster crop coordinates should be source pixels.");
        TestAssert.Equal(300, restored?.RequestedDpi, "Default requested DPI mismatch.");
        TestAssert.Equal("st_plan_annex_image", restored?.ConfiguredSourceType, "Configured source type mismatch.");
        TestAssert.Equal(null, mismatched, "Reopen should not surface crop evidence for a different Current TR.");
    }

    public static async Task CropAttachUsesCurrentTransactionAndPersistsSuccessOrFailure()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateCase(tempRoot.Path, out var source);
        var successDetail = new RecordingDetailService();
        var transaction = CreateTransaction("100001999");
        var successService = new PlaBSupportingDocumentCropService(new DocumentCropRenderingService(), successDetail, () => CreateSession(), getUtcNow: () => FixedNow());
        await successService.SaveCropAsync(layout, transaction, "100000628", source, Request(source.CopiedPath!, 0, new DocumentCropRectangle(5, 5, 30, 20)));

        var success = await successService.AttachSavedCropAsync(layout, transaction);
        var uploaded = PlaBSupportingDocumentCropService.LoadCrop(layout, "100001999");
        var failingDetail = new RecordingDetailService(fail: true, message: "token expired");
        var failService = new PlaBSupportingDocumentCropService(new DocumentCropRenderingService(), failingDetail, () => CreateSession(), getUtcNow: () => FixedNow().AddMinutes(1));

        var failure = await failService.AttachSavedCropAsync(layout, transaction);
        var failed = PlaBSupportingDocumentCropService.LoadCrop(layout, "100001999");

        TestAssert.True(success.Success, success.Message);
        TestAssert.Equal("100001999", successDetail.UploadedTransactionNumber, "Crop upload must target Current TR.");
        TestAssert.Equal("st_plan_annex_image", successDetail.UploadedSourceType, "Crop upload source type mismatch.");
        TestAssert.Equal(PlaBSupportingDocumentCropService.PngContentType, successDetail.UploadedContentType, "Crop upload content type mismatch.");
        TestAssert.Equal("uploaded", uploaded?.UploadStatus, "Success should persist upload status.");
        TestAssert.Equal("source/sources/attach", uploaded?.UploadRoute, "Success should persist upload route.");
        TestAssert.Equal("query_only", uploaded?.UploadBindingMode, "Success should persist upload binding mode.");
        TestAssert.Equal("attach_then_register_source", uploaded?.UploadMode, "Success should persist upload mode.");
        TestAssert.Equal("bearer", uploaded?.UploadAuthMode, "Success should persist upload auth mode.");
        TestAssert.Equal("tx-current", uploaded?.UploadTaskValue, "Success should persist upload task value.");
        TestAssert.Equal(PlaBSupportingDocumentCropService.PngContentType, uploaded?.UploadContentType, "Success should persist upload content type.");
        TestAssert.True(uploaded?.UploadByteCount > 0, "Success should persist upload byte count.");
        TestAssert.False(failure.Success, "Failed upload should be returned for retry.");
        TestAssert.Equal("failed", failed?.UploadStatus, "Failure should persist retry state.");
        TestAssert.Equal("source/sources/attach", failed?.UploadRoute, "Failure should persist upload route.");
        TestAssert.Equal("bearer", failed?.UploadAuthMode, "Failure should persist upload auth mode.");
        TestAssert.True(File.Exists(PlaBSupportingDocumentCropService.GetPngPath(layout)), "Local crop PNG should remain after upload failure.");
        TestAssert.True(failed?.Message?.Contains("Sensitive diagnostic was redacted", StringComparison.Ordinal) == true, "Sensitive upload diagnostics should be redacted.");
    }

    private static DocumentCropExportRequest Request(string sourcePath, int pageIndex, DocumentCropRectangle rectangle)
    {
        return new DocumentCropExportRequest(sourcePath, pageIndex, rectangle, DocumentCropRenderingService.DefaultDpi);
    }

    private static CaseFolderLayout CreateCase(string outputRoot, out SourceFileCopyResult source)
    {
        var store = new CaseFolderStore(() => FixedNow(), () => "run-crop");
        var created = store.CreateCase(outputRoot, "100001999", "tester");
        var layout = created.Layout!;
        var sourcePath = Path.Combine(layout.SourceDirectory, "source-plan.png");
        WritePng(sourcePath, 80, 60);
        source = Source("source-plan.png", ".png", sourcePath, sourceType: "st_plan_annexation_pdf");
        return layout;
    }

    private static SourceFileCopyResult Source(string fileName, string fileType, string copiedPath, bool copied = true, string? sourceType = null)
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
            copied,
            sourceType);
    }

    private static SelectedInnolaTransaction CreateTransaction(string transactionNumber)
    {
        return new SelectedInnolaTransaction(
            "task-current",
            "tx-current",
            transactionNumber,
            "PLA_B",
            "parcel_workflow",
            FixedNow(),
            TransactionType: "PLA_B");
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

    private static void WritePng(string path, int width, int height)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 32;
            pixels[i + 1] = 128;
            pixels[i + 2] = 220;
            pixels[i + 3] = 255;
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static DateTimeOffset FixedNow() => new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    private sealed class RecordingDetailService : IInnolaTransactionDetailService
    {
        private readonly bool fail;
        private readonly string? message;

        public RecordingDetailService(bool fail = false, string? message = null)
        {
            this.fail = fail;
            this.message = message;
        }

        public string? UploadedTransactionNumber { get; private set; }
        public string? UploadedSourceType { get; private set; }
        public string? UploadedContentType { get; private set; }

        public Task<InnolaTransactionDetailResult> GetTransactionDetailAsync(InnolaSession session, SelectedInnolaTransaction selectedTransaction, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<InnolaAttachmentContentResult> GetAttachmentContentAsync(InnolaSession session, InnolaTransactionDetail detail, InnolaAttachmentMetadata attachment, CancellationToken cancellationToken = default)
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
            UploadedTransactionNumber = selectedTransaction.TransactionNumber;
            UploadedSourceType = sourceType;
            UploadedContentType = contentType;
            var diagnostics = new InnolaAttachmentUploadDiagnostics(
                "source/sources/attach",
                "query_only",
                "attach_then_register_source",
                "bearer",
                selectedTransaction.TransactionId,
                contentType,
                content.Length);
            return Task.FromResult(fail
                ? InnolaAttachmentUploadResult.Failure(message ?? "Upload failed.", "upload_failed", diagnostics)
                : InnolaAttachmentUploadResult.Succeeded(diagnostics));
        }
    }

}
