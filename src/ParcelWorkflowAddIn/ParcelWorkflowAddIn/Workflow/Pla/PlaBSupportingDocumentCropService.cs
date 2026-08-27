using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Review;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.Workflow.Pla;

internal sealed class PlaBSupportingDocumentCropService
{
    public const string PngContentType = "image/png";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly DocumentCropRenderingService renderer;
    private readonly IInnolaTransactionDetailService detailService;
    private readonly Func<InnolaSession?> getSession;
    private readonly Func<InnolaTransactionSettings> settingsProvider;
    private readonly Func<DateTimeOffset> getUtcNow;

    public PlaBSupportingDocumentCropService()
        : this(
            new DocumentCropRenderingService(),
            ShellState.TransactionDetails,
            () => ShellState.Session.CurrentSession,
            InnolaTransactionSettings.Load,
            () => DateTimeOffset.UtcNow)
    {
    }

    public PlaBSupportingDocumentCropService(
        DocumentCropRenderingService renderer,
        IInnolaTransactionDetailService detailService,
        Func<InnolaSession?> getSession,
        Func<InnolaTransactionSettings>? settingsProvider = null,
        Func<DateTimeOffset>? getUtcNow = null)
    {
        this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        this.detailService = detailService ?? throw new ArgumentNullException(nameof(detailService));
        this.getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        this.settingsProvider = settingsProvider ?? InnolaTransactionSettings.Load;
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<PlaBSupportingDocumentCropSaveResult> SaveCropAsync(
        CaseFolderLayout layout,
        SelectedInnolaTransaction currentTransaction,
        string? peNumber,
        SourceFileCopyResult sourceFile,
        DocumentCropExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(currentTransaction);
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentNullException.ThrowIfNull(request);

        if (!TryValidateSource(layout, sourceFile, out var sourcePath, out var sourceRelativePath, out var sourceError))
        {
            return PlaBSupportingDocumentCropSaveResult.Failed(sourceError.Code, sourceError.Message);
        }

        if (!string.Equals(Path.GetFullPath(request.SourcePath), sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            return PlaBSupportingDocumentCropSaveResult.Failed("source_mismatch", "Crop request source must match the selected supporting document.");
        }

        var sourceType = ResolveUploadSourceType(settingsProvider(), out var sourceTypeError);
        if (sourceTypeError is not null)
        {
            return PlaBSupportingDocumentCropSaveResult.Failed(sourceTypeError.Code, sourceTypeError.Message);
        }

        var render = await renderer.ExportCropAsync(request, cancellationToken).ConfigureAwait(false);
        if (!render.Success || render.PngContent.Length == 0)
        {
            return PlaBSupportingDocumentCropSaveResult.Failed(render.ErrorCode ?? "render_failed", render.Message);
        }

        var workingDirectory = GetWorkingDirectory(layout);
        Directory.CreateDirectory(workingDirectory);
        var pngPath = GetPngPath(layout);
        await File.WriteAllBytesAsync(pngPath, render.PngContent, cancellationToken).ConfigureAwait(false);

        var now = getUtcNow();
        var existing = LoadCrop(layout, currentTransaction.TransactionNumber);
        var document = new PlaBSupportingDocumentCropDocument(
            "1.0.0",
            currentTransaction.TransactionId,
            currentTransaction.TransactionNumber,
            currentTransaction.TaskId,
            string.IsNullOrWhiteSpace(peNumber) ? null : peNumber.Trim(),
            string.IsNullOrWhiteSpace(sourceFile.SourceType) ? null : sourceFile.SourceType,
            sourceRelativePath,
            request.PageIndex + 1,
            request.SourceRectangle,
            request.PreviewRectangle,
            request.PreviewWidthPixels,
            request.PreviewHeightPixels,
            "top_left",
            render.SourceCoordinateUnits,
            request.Dpi,
            ToCaseRelativePath(layout, pngPath),
            render.OutputWidthPixels,
            render.OutputHeightPixels,
            sourceType!,
            "saved",
            existing?.UploadStatus ?? "not_uploaded",
            existing?.UploadRoute,
            existing?.UploadBindingMode,
            existing?.UploadMode,
            existing?.UploadAuthMode,
            existing?.UploadTaskValue,
            existing?.UploadContentType,
            existing?.UploadByteCount,
            null,
            render.Warning,
            existing?.CreatedAtUtc ?? now,
            now,
            null);

        await WriteMetadataAsync(layout, document, cancellationToken).ConfigureAwait(false);
        return PlaBSupportingDocumentCropSaveResult.Saved(document);
    }

    public async Task<PlaBSupportingDocumentCropAttachResult> AttachSavedCropAsync(
        CaseFolderLayout layout,
        SelectedInnolaTransaction currentTransaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(currentTransaction);

        var document = LoadCrop(layout, currentTransaction.TransactionNumber);
        if (document is null || !TryResolveCaseRelativePath(layout, document.PngRelativePath, out var pngPath) || !File.Exists(pngPath))
        {
            return PlaBSupportingDocumentCropAttachResult.Failed("crop_missing", "Save a crop PNG before attaching it.");
        }

        var sourceType = ResolveUploadSourceType(settingsProvider(), out var sourceTypeError);
        if (sourceTypeError is not null)
        {
            var failed = document with
            {
                UploadStatus = "failed",
                ErrorCategory = sourceTypeError.Code,
                Message = sourceTypeError.Message,
                UpdatedAtUtc = getUtcNow()
            };
            await WriteMetadataAsync(layout, failed, cancellationToken).ConfigureAwait(false);
            return PlaBSupportingDocumentCropAttachResult.Failed(sourceTypeError.Code, sourceTypeError.Message);
        }

        var session = getSession();
        if (session is null || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            var failed = document with
            {
                UploadStatus = "failed",
                ErrorCategory = "session_unavailable",
                Message = "PLA_B crop image could not be attached because the Innola session is not available.",
                UpdatedAtUtc = getUtcNow()
            };
            await WriteMetadataAsync(layout, failed, cancellationToken).ConfigureAwait(false);
            return PlaBSupportingDocumentCropAttachResult.Failed(failed.ErrorCategory!, failed.Message!);
        }

        try
        {
            var pngContent = await File.ReadAllBytesAsync(pngPath, cancellationToken).ConfigureAwait(false);
            var upload = await detailService.UploadAttachmentAsync(
                session,
                currentTransaction,
                Path.GetFileName(pngPath),
                PngContentType,
                pngContent,
                sourceType!,
                cancellationToken).ConfigureAwait(false);

            var updated = document with
            {
                ConfiguredSourceType = sourceType!,
                UploadStatus = upload.Success ? "uploaded" : "failed",
                UploadRoute = upload.Diagnostics?.Route,
                UploadBindingMode = upload.Diagnostics?.BindingMode,
                UploadMode = upload.Diagnostics?.UploadMode,
                UploadAuthMode = upload.Diagnostics?.AuthMode,
                UploadTaskValue = upload.Diagnostics?.TaskValue,
                UploadContentType = upload.Diagnostics?.ContentType,
                UploadByteCount = upload.Diagnostics?.ByteCount,
                ErrorCategory = upload.Success ? null : upload.ErrorCategory,
                Message = upload.Success ? "PLA_B crop image attached to current transaction." : SanitizeUploadDiagnostic(upload.ErrorMessage),
                UpdatedAtUtc = getUtcNow(),
                UploadedAtUtc = upload.Success ? getUtcNow() : null
            };
            await WriteMetadataAsync(layout, updated, cancellationToken).ConfigureAwait(false);
            return upload.Success
                ? PlaBSupportingDocumentCropAttachResult.Attached(updated)
                : PlaBSupportingDocumentCropAttachResult.Failed(updated.ErrorCategory ?? "upload_failed", updated.Message ?? "PLA_B crop image upload failed. Try again.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var updated = document with
            {
                UploadStatus = "failed",
                ErrorCategory = exception.GetType().Name,
                Message = "PLA_B crop image could not be attached. Try again.",
                UpdatedAtUtc = getUtcNow()
            };
            await WriteMetadataAsync(layout, updated, cancellationToken).ConfigureAwait(false);
            return PlaBSupportingDocumentCropAttachResult.Failed(updated.ErrorCategory!, updated.Message!);
        }
    }

    public static PlaBSupportingDocumentCropDocument? LoadCrop(CaseFolderLayout layout, string? currentTransactionNumber = null)
    {
        var path = GetMetadataPath(layout);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<PlaBSupportingDocumentCropDocument>(File.ReadAllText(path), JsonOptions);
            if (document is null
                || (!string.IsNullOrWhiteSpace(currentTransactionNumber)
                    && !string.Equals(document.CurrentTransactionNumber, currentTransactionNumber, StringComparison.OrdinalIgnoreCase))
                || !TryResolveCaseRelativePath(layout, document.PngRelativePath, out var pngPath)
                || !TryResolveCaseRelativePath(layout, document.SourceRelativePath, out var sourcePath)
                || !File.Exists(pngPath)
                || !File.Exists(sourcePath))
            {
                return null;
            }

            return document;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static string GetWorkingDirectory(CaseFolderLayout layout)
    {
        return Path.Combine(layout.WorkingDirectory, PlaBWorkflowConstants.WorkingDirectoryName);
    }

    public static string GetPngPath(CaseFolderLayout layout)
    {
        return Path.Combine(GetWorkingDirectory(layout), PlaBWorkflowConstants.SelectionPngFileName);
    }

    public static string GetMetadataPath(CaseFolderLayout layout)
    {
        return Path.Combine(GetWorkingDirectory(layout), PlaBWorkflowConstants.SelectionMetadataFileName);
    }

    public static bool TryResolveCaseRelativePath(CaseFolderLayout layout, string? relativePath, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathFullyQualified(relativePath))
        {
            return false;
        }

        try
        {
            var resolved = Path.GetFullPath(Path.Combine(layout.RootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsPathInside(layout.RootDirectory, resolved))
            {
                return false;
            }

            path = resolved;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private async Task WriteMetadataAsync(CaseFolderLayout layout, PlaBSupportingDocumentCropDocument document, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(GetWorkingDirectory(layout));
        await File.WriteAllTextAsync(GetMetadataPath(layout), JsonSerializer.Serialize(document, JsonOptions), cancellationToken).ConfigureAwait(false);
    }

    private static bool TryValidateSource(CaseFolderLayout layout, SourceFileCopyResult sourceFile, out string sourcePath, out string sourceRelativePath, out PlaBSupportingDocumentCropError error)
    {
        sourcePath = string.Empty;
        sourceRelativePath = string.Empty;
        error = new PlaBSupportingDocumentCropError(string.Empty, string.Empty);
        if (!sourceFile.Copied || string.IsNullOrWhiteSpace(sourceFile.CopiedPath))
        {
            error = new PlaBSupportingDocumentCropError("source_not_copied", "Only copied case-folder documents can be cropped.");
            return false;
        }

        try
        {
            sourcePath = Path.GetFullPath(sourceFile.CopiedPath);
            if (!IsPathInside(layout.RootDirectory, sourcePath))
            {
                error = new PlaBSupportingDocumentCropError("source_outside_case", "Selected document must be inside the active Case Folder.");
                return false;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = new PlaBSupportingDocumentCropError("source_path_invalid", "Selected document path cannot be read.");
            return false;
        }

        if (!File.Exists(sourcePath))
        {
            error = new PlaBSupportingDocumentCropError("source_missing", "Selected document is missing from the case folder.");
            return false;
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension is not (".pdf" or ".png" or ".jpg" or ".jpeg" or ".tif" or ".tiff"))
        {
            error = new PlaBSupportingDocumentCropError("source_unsupported", "Crop supports PDF, PNG, JPG, JPEG, TIFF, and TIF documents.");
            return false;
        }

        sourceRelativePath = ToCaseRelativePath(layout, sourcePath);
        return true;
    }

    private static string? ResolveUploadSourceType(InnolaTransactionSettings settings, out PlaBSupportingDocumentCropError? error)
    {
        error = null;
        var definition = settings.ComputeAttachmentSourceTypes.FirstOrDefault(source =>
            string.Equals(source.SourceType, PlaBWorkflowConstants.PlanAnnexImageSourceType, StringComparison.OrdinalIgnoreCase));
        if (definition is null || !definition.InternalOnly || !definition.SupportsExtension(".png"))
        {
            error = new PlaBSupportingDocumentCropError(
                "source_type_unavailable",
                "Upload is blocked because st_plan_annex_image is not configured as an internal PNG source type.");
            return null;
        }

        return definition.SourceType;
    }

    private static string ToCaseRelativePath(CaseFolderLayout layout, string path)
    {
        return Path.GetRelativePath(layout.RootDirectory, path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static bool IsPathInside(string parentPath, string childPath)
    {
        var normalizedParent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedChild = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeUploadDiagnostic(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "PLA_B crop image upload failed. Try again.";
        }

        return value.Contains("token", StringComparison.OrdinalIgnoreCase)
            || value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret", StringComparison.OrdinalIgnoreCase)
                ? "PLA_B crop image upload failed. Sensitive diagnostic was redacted."
                : value;
    }

}

internal sealed record PlaBSupportingDocumentCropDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("current_transaction_id")] string CurrentTransactionId,
    [property: JsonPropertyName("current_transaction_number")] string CurrentTransactionNumber,
    [property: JsonPropertyName("current_task_id")] string CurrentTaskId,
    [property: JsonPropertyName("pe_number")] string? PeNumber,
    [property: JsonPropertyName("source_type")] string? SourceType,
    [property: JsonPropertyName("source_relative_path")] string SourceRelativePath,
    [property: JsonPropertyName("page_or_frame_number")] int PageOrFrameNumber,
    [property: JsonPropertyName("source_crop_rectangle")] DocumentCropRectangle SourceCropRectangle,
    [property: JsonPropertyName("preview_crop_rectangle")] DocumentCropRectangle? PreviewCropRectangle,
    [property: JsonPropertyName("preview_width_pixels")] int? PreviewWidthPixels,
    [property: JsonPropertyName("preview_height_pixels")] int? PreviewHeightPixels,
    [property: JsonPropertyName("origin_convention")] string OriginConvention,
    [property: JsonPropertyName("source_coordinate_units")] string SourceCoordinateUnits,
    [property: JsonPropertyName("requested_dpi")] int RequestedDpi,
    [property: JsonPropertyName("png_path")] string PngRelativePath,
    [property: JsonPropertyName("output_width_pixels")] int OutputWidthPixels,
    [property: JsonPropertyName("output_height_pixels")] int OutputHeightPixels,
    [property: JsonPropertyName("configured_source_type")] string ConfiguredSourceType,
    [property: JsonPropertyName("local_save_status")] string LocalSaveStatus,
    [property: JsonPropertyName("upload_status")] string UploadStatus,
    [property: JsonPropertyName("upload_route")] string? UploadRoute,
    [property: JsonPropertyName("upload_binding_mode")] string? UploadBindingMode,
    [property: JsonPropertyName("upload_mode")] string? UploadMode,
    [property: JsonPropertyName("upload_auth_mode")] string? UploadAuthMode,
    [property: JsonPropertyName("upload_task_value")] string? UploadTaskValue,
    [property: JsonPropertyName("upload_content_type")] string? UploadContentType,
    [property: JsonPropertyName("upload_byte_count")] long? UploadByteCount,
    [property: JsonPropertyName("error_category")] string? ErrorCategory,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("created_at_utc")] DateTimeOffset CreatedAtUtc,
    [property: JsonPropertyName("updated_at_utc")] DateTimeOffset UpdatedAtUtc,
    [property: JsonPropertyName("uploaded_at_utc")] DateTimeOffset? UploadedAtUtc);

internal sealed record PlaBSupportingDocumentCropSaveResult(
    bool Success,
    PlaBSupportingDocumentCropDocument? Document,
    string? ErrorCode,
    string Message)
{
    public static PlaBSupportingDocumentCropSaveResult Saved(PlaBSupportingDocumentCropDocument document)
    {
        return new PlaBSupportingDocumentCropSaveResult(true, document, null, "PLA_B crop PNG saved.");
    }

    public static PlaBSupportingDocumentCropSaveResult Failed(string errorCode, string message)
    {
        return new PlaBSupportingDocumentCropSaveResult(false, null, errorCode, message);
    }
}

internal sealed record PlaBSupportingDocumentCropAttachResult(
    bool Success,
    PlaBSupportingDocumentCropDocument? Document,
    string? ErrorCode,
    string Message)
{
    public static PlaBSupportingDocumentCropAttachResult Attached(PlaBSupportingDocumentCropDocument document)
    {
        return new PlaBSupportingDocumentCropAttachResult(true, document, null, "PLA_B crop image attached to current transaction.");
    }

    public static PlaBSupportingDocumentCropAttachResult Failed(string errorCode, string message)
    {
        return new PlaBSupportingDocumentCropAttachResult(false, null, errorCode, message);
    }
}

internal sealed record PlaBSupportingDocumentCropError(string Code, string Message);
