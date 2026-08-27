using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Intake;
using System.IO;

namespace ParcelWorkflowAddIn.Workflow;

internal static class SupportingDocumentWorkspaceProjection
{
    public static IReadOnlyList<SourceFileListItem> BuildReadableSupportingDocumentOptions(IReadOnlyList<SourceFileListItem> sourceFiles)
    {
        if (sourceFiles.Count == 0)
        {
            return Array.Empty<SourceFileListItem>();
        }

        return sourceFiles
            .Where(item => item.SourceFile.Copied && !string.IsNullOrWhiteSpace(item.SourceFile.CopiedPath))
            .Where(item => !IsInternalGeneratedDocument(item.SourceFile))
            .Where(item => IsReadableSupportingDocumentFile(item.SourceFile))
            .GroupBy(item => BuildSafeSourceFileIdentity(item.SourceFile), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.FileLabel, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string FormatTransactionLabel(string? transactionId)
    {
        var value = transactionId?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "TR-Unknown";
        }

        if (value.StartsWith("TR-", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (value.StartsWith("TR", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..].TrimStart('-', ' ');
        }

        return $"TR-{value}";
    }

    public static bool IsTextDocument(SourceFileCopyResult sourceFile)
    {
        return string.Equals(ResolveSourceFileExtension(sourceFile), ".txt", StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanCropSupportingDocument(SourceFileCopyResult? sourceFile, string? activeCaseFolderPath, out string reason)
    {
        if (sourceFile is null)
        {
            reason = "Select a PDF, PNG, JPG, or TIFF document to crop.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(activeCaseFolderPath))
        {
            reason = "Load a transaction before cropping supporting documents.";
            return false;
        }

        if (!sourceFile.Copied || string.IsNullOrWhiteSpace(sourceFile.CopiedPath))
        {
            reason = "Only copied case-folder documents can be cropped.";
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(sourceFile.CopiedPath);
            var caseRoot = Path.GetFullPath(activeCaseFolderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(caseRoot, StringComparison.OrdinalIgnoreCase))
            {
                reason = "Selected document is outside the active case folder.";
                return false;
            }

            if (!File.Exists(fullPath))
            {
                reason = "Selected document is missing from the case folder.";
                return false;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            reason = "Selected document path cannot be read.";
            return false;
        }

        if (!IsCroppableSupportingDocumentFile(sourceFile))
        {
            reason = "Crop supports PDF, PNG, JPG, JPEG, TIFF, and TIF documents.";
            return false;
        }

        reason = "Crop selected document.";
        return true;
    }

    public static bool IsCroppableSupportingDocumentFile(SourceFileCopyResult sourceFile)
    {
        var extension = ResolveSourceFileExtension(sourceFile);
        return extension is ".pdf" or ".png" or ".jpg" or ".jpeg" or ".tif" or ".tiff";
    }

    private static bool IsReadableSupportingDocumentFile(SourceFileCopyResult sourceFile)
    {
        var extension = ResolveSourceFileExtension(sourceFile);
        return extension is ".pdf"
            or ".txt"
            or ".doc"
            or ".docx"
            or ".dwg"
            or ".png"
            or ".jpg"
            or ".jpeg"
            or ".tif"
            or ".tiff";
    }

    private static bool IsInternalGeneratedDocument(SourceFileCopyResult sourceFile)
    {
        var role = SourceRole.Normalize(sourceFile.SourceRole);
        if (role is SourceRole.WorkflowResumePackage or SourceRole.ComputeReport or SourceRole.UnsupportedSource)
        {
            return true;
        }

        if (string.Equals(sourceFile.SourceType, "st_compute_report", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var fileName = sourceFile.FileName;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = Path.GetFileName(sourceFile.CopiedPath);
        }

        return !string.IsNullOrWhiteSpace(fileName)
            && fileName.Contains("compute_examination_report", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveSourceFileExtension(SourceFileCopyResult sourceFile)
    {
        if (!string.IsNullOrWhiteSpace(sourceFile.FileType))
        {
            var fileType = sourceFile.FileType.Trim();
            return fileType.StartsWith(".", StringComparison.Ordinal) ? fileType.ToLowerInvariant() : $".{fileType.ToLowerInvariant()}";
        }

        return Path.GetExtension(sourceFile.FileName).ToLowerInvariant();
    }

    private static string BuildSafeSourceFileIdentity(SourceFileCopyResult sourceFile)
    {
        if (!string.IsNullOrWhiteSpace(sourceFile.CopiedPath))
        {
            try
            {
                return Path.GetFullPath(sourceFile.CopiedPath);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return BuildFallbackSourceFileIdentity(sourceFile);
            }
        }

        return BuildFallbackSourceFileIdentity(sourceFile);
    }

    private static string BuildFallbackSourceFileIdentity(SourceFileCopyResult sourceFile)
    {
        return string.IsNullOrWhiteSpace(sourceFile.OriginalPath)
            ? sourceFile.FileName
            : sourceFile.OriginalPath;
    }
}
