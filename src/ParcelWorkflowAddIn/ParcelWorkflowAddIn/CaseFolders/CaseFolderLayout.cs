namespace ParcelWorkflowAddIn.CaseFolders;

using System.IO;

public sealed class CaseFolderLayout
{
    private CaseFolderLayout(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        ManifestPath = Path.Combine(rootDirectory, "manifest.json");
        LegacyManifestPath = Path.Combine(rootDirectory, "_manifest.json");
        SourceDirectory = Path.Combine(rootDirectory, "source");
        WorkingDirectory = Path.Combine(rootDirectory, "working");
        PreflightSummaryPath = Path.Combine(WorkingDirectory, "preflight_summary.json");
        StructureCheckSummaryPath = Path.Combine(WorkingDirectory, "structure_check_summary.json");
        DimensionCheckSummaryPath = Path.Combine(WorkingDirectory, "dimension_check_summary.json");
        OutputDirectory = Path.Combine(rootDirectory, "output");
        ReportsDirectory = Path.Combine(OutputDirectory, "reports");
        LogsDirectory = Path.Combine(OutputDirectory, "logs");
    }

    public string RootDirectory { get; }

    public string ManifestPath { get; }

    public string LegacyManifestPath { get; }

    public string SourceDirectory { get; }

    public string WorkingDirectory { get; }

    public string PreflightSummaryPath { get; }

    public string StructureCheckSummaryPath { get; }

    public string DimensionCheckSummaryPath { get; }

    public string OutputDirectory { get; }

    public string ReportsDirectory { get; }

    public string LogsDirectory { get; }

    public static CaseFolderLayout For(string outputRoot, string transactionId)
    {
        var fullOutputRoot = Path.GetFullPath(outputRoot);
        return new CaseFolderLayout(Path.Combine(fullOutputRoot, transactionId));
    }

    public static CaseFolderLayout FromRootDirectory(string rootDirectory)
    {
        return new CaseFolderLayout(Path.GetFullPath(rootDirectory));
    }
}
