using System.IO;

namespace ParcelWorkflowAddIn.CaseFolders;

public sealed class SourceFileActionService
{
    private readonly ISourceFileLauncher launcher;

    public SourceFileActionService()
        : this(new WindowsSourceFileLauncher())
    {
    }

    public SourceFileActionService(ISourceFileLauncher launcher)
    {
        this.launcher = launcher;
    }

    public SourceFileActionResult Execute(CaseFolderLayout layout, SourceFileCopyResult sourceFile, SourceFileAction action)
    {
        if (!sourceFile.Copied || string.IsNullOrWhiteSpace(sourceFile.CopiedPath))
        {
            return SourceFileActionResult.Failed(action, sourceFile.CopiedPath, "blocked", "Only copied Case Folder source files can be opened or revealed.");
        }

        string copiedPath;
        try
        {
            copiedPath = Path.GetFullPath(sourceFile.CopiedPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return SourceFileActionResult.Failed(action, sourceFile.CopiedPath, "blocked", $"Source file path could not be read: {exception.Message}");
        }

        if (!IsPathInside(layout.SourceDirectory, copiedPath))
        {
            return SourceFileActionResult.Failed(action, copiedPath, "blocked", "Source file action is blocked because the copied path is outside the Case Folder source area.");
        }

        if (!File.Exists(copiedPath))
        {
            return SourceFileActionResult.Failed(action, copiedPath, "missing", "Source file is missing from the Case Folder.");
        }

        if (action == SourceFileAction.RouteToMap)
        {
            return ExecuteMapRoute(sourceFile, copiedPath);
        }

        try
        {
            if (action == SourceFileAction.Open)
            {
                launcher.OpenFile(copiedPath);
                return SourceFileActionResult.Succeeded(action, copiedPath, "opened", "Opened source file.");
            }

            if (action == SourceFileAction.Reveal)
            {
                launcher.RevealFile(copiedPath);
                return SourceFileActionResult.Succeeded(action, copiedPath, "revealed", "Revealed source file in folder.");
            }

            return SourceFileActionResult.Failed(action, copiedPath, "blocked", "Source file action is not supported.");
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or ObjectDisposedException)
        {
            return SourceFileActionResult.Failed(action, copiedPath, "failed", $"Source file action failed: {exception.Message}");
        }
    }

    public static bool IsMapRouteCandidate(string? fileType)
    {
        return string.Equals(fileType, ".dwg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileType, ".tif", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileType, ".tiff", StringComparison.OrdinalIgnoreCase);
    }

    private static SourceFileActionResult ExecuteMapRoute(SourceFileCopyResult sourceFile, string copiedPath)
    {
        if (!IsMapRouteCandidate(sourceFile.FileType))
        {
            return SourceFileActionResult.Failed(SourceFileAction.RouteToMap, copiedPath, "blocked", "Source file is not eligible for map routing.");
        }

        return SourceFileActionResult.Failed(
            SourceFileAction.RouteToMap,
            copiedPath,
            "map_route_unavailable",
            "Map routing is not available in this build. Open or reveal the source file instead.");
    }

    private static bool IsPathInside(string parentPath, string childPath)
    {
        var normalizedParent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedChild = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }
}
