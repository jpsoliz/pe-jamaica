using System.IO;
using ParcelWorkflowAddIn.Contracts;

namespace ParcelWorkflowAddIn.CaseFolders;

public sealed class AttachmentSourceFileWriter
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".dwg",
        ".txt",
        ".csv",
        ".tif",
        ".tiff",
        ".png",
        ".jpg",
        ".jpeg"
    };

    private readonly Func<DateTimeOffset> getUtcNow;

    public AttachmentSourceFileWriter()
        : this(() => DateTimeOffset.UtcNow)
    {
    }

    public AttachmentSourceFileWriter(Func<DateTimeOffset> getUtcNow)
    {
        this.getUtcNow = getUtcNow;
    }

    public AttachmentSourceFileWriteResult Write(
        CaseFolderLayout layout,
        string originalReference,
        string fileName,
        byte[] content,
        string? sourceRole)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return AttachmentSourceFileWriteResult.Failed("Attachment file name is required.");
            }

            var safeFileName = Path.GetFileName(fileName);
            if (!string.Equals(safeFileName, fileName, StringComparison.Ordinal)
                || safeFileName.Contains("..", StringComparison.Ordinal)
                || safeFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return AttachmentSourceFileWriteResult.Failed("Attachment file name must not contain a path.");
            }

            var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
            if (!SupportedExtensions.Contains(extension))
            {
                return AttachmentSourceFileWriteResult.Failed($"Unsupported attachment file type: {extension}.");
            }

            if (content.Length == 0)
            {
                return AttachmentSourceFileWriteResult.Failed("Attachment content is empty.");
            }

            Directory.CreateDirectory(layout.SourceDirectory);
            var destinationPath = GetAvailableDestinationPath(layout.SourceDirectory, safeFileName);
            var fullDestinationPath = Path.GetFullPath(destinationPath);
            if (!IsPathInside(layout.SourceDirectory, fullDestinationPath))
            {
                return AttachmentSourceFileWriteResult.Failed("Attachment must stay inside the Case Folder source area.");
            }

            File.WriteAllBytes(fullDestinationPath, content);
            var fileInfo = new FileInfo(fullDestinationPath);
            var copiedAt = getUtcNow().UtcDateTime.ToString("O");
            return AttachmentSourceFileWriteResult.Written(
                new ManifestSourceFile(
                    originalReference,
                    fullDestinationPath,
                    extension,
                    fileInfo.Length,
                    copiedAt,
                    sourceRole),
                copiedAt);
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return AttachmentSourceFileWriteResult.Failed($"Attachment could not be written: {exception.Message}");
        }
    }

    private static string GetAvailableDestinationPath(string sourceDirectory, string fileName)
    {
        var destination = Path.Combine(sourceDirectory, fileName);
        if (!File.Exists(destination))
        {
            return destination;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var index = 2;
        while (true)
        {
            destination = Path.Combine(sourceDirectory, $"{name}_{index}{extension}");
            if (!File.Exists(destination))
            {
                return destination;
            }

            index++;
        }
    }

    private static bool IsPathInside(string parentPath, string childPath)
    {
        var normalizedParent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedChild = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record AttachmentSourceFileWriteResult(
    bool Success,
    ManifestSourceFile? ManifestSourceFile,
    string? CopiedAt,
    string? ErrorMessage)
{
    public static AttachmentSourceFileWriteResult Written(ManifestSourceFile manifestSourceFile, string copiedAt)
    {
        return new AttachmentSourceFileWriteResult(true, manifestSourceFile, copiedAt, null);
    }

    public static AttachmentSourceFileWriteResult Failed(string errorMessage)
    {
        return new AttachmentSourceFileWriteResult(false, null, null, errorMessage);
    }
}
