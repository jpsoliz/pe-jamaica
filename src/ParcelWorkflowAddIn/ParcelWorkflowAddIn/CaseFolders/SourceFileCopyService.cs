using System.IO;
using ParcelWorkflowAddIn.Contracts;

namespace ParcelWorkflowAddIn.CaseFolders;

public sealed class SourceFileCopyService
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

    public SourceFileCopyService()
        : this(() => DateTimeOffset.UtcNow)
    {
    }

    public SourceFileCopyService(Func<DateTimeOffset> getUtcNow)
    {
        this.getUtcNow = getUtcNow;
    }

    public SourceFileCopyBatchResult CopySourceFiles(CaseFolderLayout layout, IReadOnlyList<string> sourcePaths, string? sourceRole = null, string? sourceType = null)
    {
        var results = new List<SourceFileCopyResult>();
        var copiedManifestEntries = new List<ManifestSourceFile>();

        foreach (var sourcePath in sourcePaths)
        {
            var result = CopySourceFile(layout, sourcePath, sourceRole, sourceType);
            results.Add(result.Result);

            if (result.ManifestEntry is not null)
            {
                copiedManifestEntries.Add(result.ManifestEntry);
            }
        }

        if (copiedManifestEntries.Count > 0)
        {
            var manifest = ManifestSerializer.Read(layout.ManifestPath);
            var sourceFiles = manifest.Payload.SourceFiles.Concat(copiedManifestEntries).ToArray();
            var updatedManifest = manifest with
            {
                Payload = manifest.Payload with { SourceFiles = sourceFiles }
            };

            ManifestSerializer.Write(layout.ManifestPath, updatedManifest);
        }

        return new SourceFileCopyBatchResult(results);
    }

    private SourceFileCopyOperationResult CopySourceFile(CaseFolderLayout layout, string sourcePath, string? sourceRole, string? sourceType)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return Failed(sourcePath, "Source file not found.");
            }

            var fullSourcePath = Path.GetFullPath(sourcePath);
            var extension = Path.GetExtension(fullSourcePath).ToLowerInvariant();
            if (!SupportedExtensions.Contains(extension))
            {
                return Failed(fullSourcePath, $"Unsupported source file type: {extension}.");
            }

            Directory.CreateDirectory(layout.SourceDirectory);
            var destinationPath = GetAvailableDestinationPath(layout.SourceDirectory, Path.GetFileName(fullSourcePath));
            var fullDestinationPath = Path.GetFullPath(destinationPath);

            if (!IsPathInside(layout.SourceDirectory, fullDestinationPath))
            {
                return Failed(fullSourcePath, "Copied source file must stay inside the Case Folder source area.");
            }

            File.Copy(fullSourcePath, fullDestinationPath, overwrite: false);

            var fileInfo = new FileInfo(fullDestinationPath);
            var result = new SourceFileCopyResult(
                fullSourcePath,
                fullDestinationPath,
                fileInfo.Name,
                extension,
                fileInfo.Length,
                sourceRole,
                "copied",
                "Copied to Case Folder source area.",
                Copied: true,
                SourceType: sourceType);

            var manifestEntry = new ManifestSourceFile(
                fullSourcePath,
                fullDestinationPath,
                extension,
                fileInfo.Length,
                getUtcNow().UtcDateTime.ToString("O"),
                sourceRole,
                sourceType);

            return new SourceFileCopyOperationResult(result, manifestEntry);
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return Failed(sourcePath, $"Source file could not be copied: {exception.Message}");
        }
    }

    private static SourceFileCopyOperationResult Failed(string sourcePath, string message)
    {
        var fileName = string.IsNullOrWhiteSpace(sourcePath) ? string.Empty : Path.GetFileName(sourcePath);
        var extension = string.IsNullOrWhiteSpace(sourcePath) ? string.Empty : Path.GetExtension(sourcePath).ToLowerInvariant();
        var result = new SourceFileCopyResult(
            sourcePath,
            null,
            fileName,
            extension,
            null,
            null,
            "rejected",
            message,
            Copied: false);

        return new SourceFileCopyOperationResult(result, null);
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

    private sealed record SourceFileCopyOperationResult(SourceFileCopyResult Result, ManifestSourceFile? ManifestEntry);
}
