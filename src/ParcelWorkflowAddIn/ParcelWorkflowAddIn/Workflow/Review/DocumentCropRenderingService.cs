using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow.Execution;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ParcelWorkflowAddIn.Workflow.Review;

internal sealed class DocumentCropRenderingService
{
    public const int DefaultDpi = 300;
    public static IReadOnlyList<int> SupportedDpiValues { get; } = new[] { 200, 300, 400, 600 };
    private const long WarningPixelCount = 40_000_000;
    private const long HardPixelCount = 120_000_000;
    private static readonly TimeSpan PdfRenderTimeout = TimeSpan.FromMinutes(2);
    private readonly IProcessRunner processRunner;
    private readonly Func<WorkflowExecutionSettings> getExecutionSettings;

    public DocumentCropRenderingService()
        : this(new ProcessRunner(), () => WorkflowExecutionSettings.Load())
    {
    }

    public DocumentCropRenderingService(IProcessRunner processRunner, Func<WorkflowExecutionSettings>? getExecutionSettings = null)
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        this.getExecutionSettings = getExecutionSettings ?? (() => WorkflowExecutionSettings.Load());
    }

    public DocumentCropValidationResult ValidateExportRequest(DocumentCropExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SourcePath) || !File.Exists(request.SourcePath))
        {
            return DocumentCropValidationResult.Blocked("source_missing", "Selected document is missing.");
        }

        if (!SupportedDpiValues.Contains(request.Dpi))
        {
            return DocumentCropValidationResult.Blocked("dpi_invalid", "Select a supported DPI value: 200, 300, 400, or 600.");
        }

        if (request.PageIndex < 0)
        {
            return DocumentCropValidationResult.Blocked("page_invalid", "Selected page/frame must be 1 or greater.");
        }

        if (request.SourceRectangle.Width <= 0 || request.SourceRectangle.Height <= 0)
        {
            return DocumentCropValidationResult.Blocked("selection_empty", "Draw a crop rectangle before saving.");
        }

        var extension = Path.GetExtension(request.SourcePath).ToLowerInvariant();
        if (!IsSupportedSourceExtension(extension))
        {
            return DocumentCropValidationResult.Blocked("source_unsupported", "Crop supports PDF, PNG, JPG, JPEG, TIFF, and TIF documents.");
        }

        try
        {
            return extension == ".pdf"
                ? ValidatePdfExportRequest(request)
                : ValidateRasterExportRequest(request, extension is ".tif" or ".tiff");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FileFormatException or NotSupportedException)
        {
            return DocumentCropValidationResult.Blocked("source_unreadable", $"Selected document could not be read: {exception.Message}");
        }
    }

    public async Task<DocumentCropExportResult> ExportCropAsync(DocumentCropExportRequest request, CancellationToken cancellationToken = default)
    {
        var validation = ValidateExportRequest(request);
        if (!validation.CanContinue)
        {
            return DocumentCropExportResult.Failed(validation.Code, validation.Message);
        }

        var extension = Path.GetExtension(request.SourcePath).ToLowerInvariant();
        return extension == ".pdf"
            ? await ExportPdfCropAsync(request, validation.Warning, cancellationToken).ConfigureAwait(false)
            : ExportRasterCrop(request, extension is ".tif" or ".tiff", validation.Warning);
    }

    public async Task<DocumentCropPreviewPage> RenderPreviewAsync(string sourcePath, int pageIndex, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Selected document is missing.", sourcePath);
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension == ".pdf")
        {
            return await RenderPdfPreviewAsync(sourcePath, pageIndex, cancellationToken).ConfigureAwait(false);
        }

        return RenderRasterPreview(sourcePath, pageIndex, extension is ".tif" or ".tiff");
    }

    private static bool IsSupportedSourceExtension(string extension)
    {
        return extension is ".pdf" or ".png" or ".jpg" or ".jpeg" or ".tif" or ".tiff";
    }

    private static DocumentCropValidationResult ValidateRasterExportRequest(DocumentCropExportRequest request, bool allowMultipleFrames)
    {
        using var stream = File.Open(request.SourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var pageCount = allowMultipleFrames ? decoder.Frames.Count : 1;
        if (pageCount == 0 || request.PageIndex >= pageCount)
        {
            return DocumentCropValidationResult.Blocked("page_invalid", "Selected page/frame is outside the source document.");
        }

        var frame = decoder.Frames[request.PageIndex];
        return ValidateBoundsAndSize(request.SourceRectangle, frame.PixelWidth, frame.PixelHeight, "source pixels", dpi: null);
    }

    private static DocumentCropValidationResult ValidatePdfExportRequest(DocumentCropExportRequest request)
    {
        var estimatedWidth = (long)Math.Ceiling(request.SourceRectangle.Width * request.Dpi / 72d);
        var estimatedHeight = (long)Math.Ceiling(request.SourceRectangle.Height * request.Dpi / 72d);
        return ValidateOutputSize(estimatedWidth, estimatedHeight);
    }

    private static DocumentCropValidationResult ValidateBoundsAndSize(DocumentCropRectangle rectangle, double sourceWidth, double sourceHeight, string units, int? dpi)
    {
        if (rectangle.X < 0 || rectangle.Y < 0 || rectangle.X + rectangle.Width > sourceWidth || rectangle.Y + rectangle.Height > sourceHeight)
        {
            return DocumentCropValidationResult.Blocked("selection_out_of_bounds", $"Crop rectangle must stay inside the source {units}.");
        }

        var width = dpi.HasValue ? (long)Math.Ceiling(rectangle.Width * dpi.Value / 72d) : (long)Math.Ceiling(rectangle.Width);
        var height = dpi.HasValue ? (long)Math.Ceiling(rectangle.Height * dpi.Value / 72d) : (long)Math.Ceiling(rectangle.Height);
        return ValidateOutputSize(width, height);
    }

    private static DocumentCropValidationResult ValidateOutputSize(long width, long height)
    {
        var pixels = width * height;
        if (width <= 0 || height <= 0)
        {
            return DocumentCropValidationResult.Blocked("selection_empty", "Crop output dimensions must be greater than zero.");
        }

        if (pixels > HardPixelCount)
        {
            return DocumentCropValidationResult.Blocked("output_too_large", "Selected crop is too large to render safely. Reduce the crop area or DPI.");
        }

        return pixels > WarningPixelCount
            ? DocumentCropValidationResult.WithWarning($"Large crop output estimated at {width:N0} x {height:N0} pixels. Save may take longer and upload may be slower.")
            : DocumentCropValidationResult.Valid();
    }

    private static DocumentCropPreviewPage RenderRasterPreview(string sourcePath, int pageIndex, bool allowMultipleFrames)
    {
        using var stream = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var pageCount = allowMultipleFrames ? decoder.Frames.Count : 1;
        if (pageCount == 0 || pageIndex < 0 || pageIndex >= pageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Selected page/frame is outside the source document.");
        }

        var frame = decoder.Frames[pageIndex];
        frame.Freeze();
        return new DocumentCropPreviewPage(sourcePath, pageIndex, pageCount, frame, frame.PixelWidth, frame.PixelHeight, "source_pixels", "Raster");
    }

    private DocumentCropExportResult ExportRasterCrop(DocumentCropExportRequest request, bool allowMultipleFrames, string? warning)
    {
        using var stream = File.Open(request.SourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[request.PageIndex];
        var rect = new Int32Rect(
            (int)Math.Round(request.SourceRectangle.X),
            (int)Math.Round(request.SourceRectangle.Y),
            (int)Math.Round(request.SourceRectangle.Width),
            (int)Math.Round(request.SourceRectangle.Height));
        var crop = new CroppedBitmap(frame, rect);
        crop.Freeze();
        return DocumentCropExportResult.Png(EncodePng(crop), crop.PixelWidth, crop.PixelHeight, frame.PixelWidth, frame.PixelHeight, allowMultipleFrames ? decoder.Frames.Count : 1, "source_pixels", warning);
    }

    private async Task<DocumentCropPreviewPage> RenderPdfPreviewAsync(string sourcePath, int pageIndex, CancellationToken cancellationToken)
    {
        var result = await RunPdfScriptAsync(
            BuildPdfPreviewScript(),
            new[]
            {
                sourcePath,
                pageIndex.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.Message);
        }

        var image = LoadBitmapImage(result.PngContent);
        return new DocumentCropPreviewPage(sourcePath, pageIndex, result.PageCount, image, result.SourceWidth, result.SourceHeight, "pdf_points", "PDF");
    }

    private async Task<DocumentCropExportResult> ExportPdfCropAsync(DocumentCropExportRequest request, string? warning, CancellationToken cancellationToken)
    {
        var result = await RunPdfScriptAsync(
            BuildPdfCropScript(),
            new[]
            {
                request.SourcePath,
                request.PageIndex.ToString(CultureInfo.InvariantCulture),
                request.SourceRectangle.X.ToString(CultureInfo.InvariantCulture),
                request.SourceRectangle.Y.ToString(CultureInfo.InvariantCulture),
                request.SourceRectangle.Width.ToString(CultureInfo.InvariantCulture),
                request.SourceRectangle.Height.ToString(CultureInfo.InvariantCulture),
                request.Dpi.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken).ConfigureAwait(false);
        return result.Success
            ? DocumentCropExportResult.Png(result.PngContent, result.OutputWidth, result.OutputHeight, result.SourceWidth, result.SourceHeight, result.PageCount, "pdf_points", warning)
            : DocumentCropExportResult.Failed(result.Code, result.Message);
    }

    private async Task<PdfScriptResult> RunPdfScriptAsync(string script, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var settings = getExecutionSettings();
        if (string.IsNullOrWhiteSpace(settings.PythonExecutable) || !File.Exists(settings.PythonExecutable))
        {
            return PdfScriptResult.Failed("python_unavailable", "Configured ArcGIS Python executable is not available for PDF crop rendering.");
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"sidwell-document-crop-{Guid.NewGuid():N}");
        var scriptPath = Path.Combine(tempDirectory, "render_document_crop.py");
        var outputPath = Path.Combine(tempDirectory, "crop.png");
        var metadataPath = Path.Combine(tempDirectory, "metadata.json");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            await File.WriteAllTextAsync(scriptPath, script, cancellationToken).ConfigureAwait(false);
            var commandArguments = string.Join(" ", new[] { Quote(scriptPath), Quote(outputPath), Quote(metadataPath) }.Concat(arguments.Select(Quote)));
            var process = await processRunner.RunAsync(settings.PythonExecutable, commandArguments, PdfRenderTimeout, null, cancellationToken).ConfigureAwait(false);
            if (process.TimedOut)
            {
                return PdfScriptResult.Failed("render_timeout", "PDF crop rendering timed out.");
            }

            if (process.ExitCode != 0 || !File.Exists(outputPath) || !File.Exists(metadataPath))
            {
                var detail = string.IsNullOrWhiteSpace(process.StandardError) ? process.StandardOutput : process.StandardError;
                return PdfScriptResult.Failed("render_failed", $"PDF crop rendering failed. {detail.Trim()}");
            }

            using var metadata = JsonDocument.Parse(await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false));
            var root = metadata.RootElement;
            return PdfScriptResult.Succeeded(
                await File.ReadAllBytesAsync(outputPath, cancellationToken).ConfigureAwait(false),
                root.GetProperty("source_width").GetDouble(),
                root.GetProperty("source_height").GetDouble(),
                root.GetProperty("output_width").GetInt32(),
                root.GetProperty("output_height").GetInt32(),
                root.GetProperty("page_count").GetInt32());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException)
        {
            return PdfScriptResult.Failed("render_failed", $"PDF crop rendering failed: {exception.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
            catch (Exception)
            {
            }
        }
    }

    private static byte[] EncodePng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static BitmapImage LoadBitmapImage(byte[] content)
    {
        using var stream = new MemoryStream(content);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string BuildPdfPreviewScript()
    {
        return """
import json
import sys
import pypdfium2 as pdfium

output_path = sys.argv[1]
metadata_path = sys.argv[2]
source_path = sys.argv[3]
page_index = int(sys.argv[4])

document = pdfium.PdfDocument(source_path)
if page_index < 0 or page_index >= len(document):
    raise ValueError("Selected page is outside the source PDF page range.")
page = document[page_index]
page_width, page_height = page.get_size()
bitmap = page.render(scale=2).to_pil()
bitmap.save(output_path)
with open(metadata_path, "w", encoding="utf-8") as handle:
    json.dump({
        "source_width": page_width,
        "source_height": page_height,
        "output_width": bitmap.width,
        "output_height": bitmap.height,
        "page_count": len(document)
    }, handle)
""";
    }

    private static string BuildPdfCropScript()
    {
        return """
import json
import sys
import pypdfium2 as pdfium

output_path = sys.argv[1]
metadata_path = sys.argv[2]
source_path = sys.argv[3]
page_index = int(sys.argv[4])
x = float(sys.argv[5])
y = float(sys.argv[6])
width = float(sys.argv[7])
height = float(sys.argv[8])
dpi = int(sys.argv[9])

document = pdfium.PdfDocument(source_path)
if page_index < 0 or page_index >= len(document):
    raise ValueError("Selected page is outside the source PDF page range.")
if width <= 0 or height <= 0:
    raise ValueError("Selection width and height must be greater than zero.")
page = document[page_index]
page_width, page_height = page.get_size()
if x < 0 or y < 0 or x + width > page_width or y + height > page_height:
    raise ValueError("Selection region is outside the selected PDF page.")
scale = dpi / 72.0
bitmap = page.render(scale=scale).to_pil()
crop_box = (
    int(round(x * scale)),
    int(round(y * scale)),
    int(round((x + width) * scale)),
    int(round((y + height) * scale)),
)
cropped = bitmap.crop(crop_box)
cropped.save(output_path)
with open(metadata_path, "w", encoding="utf-8") as handle:
    json.dump({
        "source_width": page_width,
        "source_height": page_height,
        "output_width": cropped.width,
        "output_height": cropped.height,
        "page_count": len(document)
    }, handle)
""";
    }

    private sealed record PdfScriptResult(
        bool Success,
        byte[] PngContent,
        double SourceWidth,
        double SourceHeight,
        int OutputWidth,
        int OutputHeight,
        int PageCount,
        string Code,
        string Message)
    {
        public static PdfScriptResult Succeeded(byte[] content, double sourceWidth, double sourceHeight, int outputWidth, int outputHeight, int pageCount)
        {
            return new PdfScriptResult(true, content, sourceWidth, sourceHeight, outputWidth, outputHeight, pageCount, string.Empty, string.Empty);
        }

        public static PdfScriptResult Failed(string code, string message)
        {
            return new PdfScriptResult(false, Array.Empty<byte>(), 0, 0, 0, 0, 0, code, message);
        }
    }
}

internal sealed record DocumentCropPreviewPage(
    string SourcePath,
    int PageIndex,
    int PageCount,
    BitmapSource ImageSource,
    double SourceWidth,
    double SourceHeight,
    string SourceCoordinateUnits,
    string DocumentKind);

internal sealed record DocumentCropExportRequest(
    string SourcePath,
    int PageIndex,
    DocumentCropRectangle SourceRectangle,
    int Dpi,
    DocumentCropRectangle? PreviewRectangle = null,
    int? PreviewWidthPixels = null,
    int? PreviewHeightPixels = null);

internal sealed record DocumentCropRectangle(
    double X,
    double Y,
    double Width,
    double Height);

internal sealed record DocumentCropValidationResult(
    bool CanContinue,
    string Code,
    string Message,
    string? Warning)
{
    public static DocumentCropValidationResult Valid() => new(true, "valid", "Crop request is valid.", null);
    public static DocumentCropValidationResult WithWarning(string warning) => new(true, "warning", warning, warning);
    public static DocumentCropValidationResult Blocked(string code, string message) => new(false, code, message, null);
}

internal sealed record DocumentCropExportResult(
    bool Success,
    byte[] PngContent,
    int OutputWidthPixels,
    int OutputHeightPixels,
    double SourceWidth,
    double SourceHeight,
    int PageCount,
    string SourceCoordinateUnits,
    string? Warning,
    string? ErrorCode,
    string Message)
{
    public static DocumentCropExportResult Png(byte[] content, int outputWidth, int outputHeight, double sourceWidth, double sourceHeight, int pageCount, string sourceCoordinateUnits, string? warning)
    {
        return new DocumentCropExportResult(true, content, outputWidth, outputHeight, sourceWidth, sourceHeight, pageCount, sourceCoordinateUnits, warning, null, "Crop PNG rendered.");
    }

    public static DocumentCropExportResult Failed(string errorCode, string message)
    {
        return new DocumentCropExportResult(false, Array.Empty<byte>(), 0, 0, 0, 0, 0, string.Empty, null, errorCode, message);
    }
}
