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
