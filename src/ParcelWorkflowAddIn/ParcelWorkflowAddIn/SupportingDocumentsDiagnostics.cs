using System.IO;

namespace ParcelWorkflowAddIn;

internal static class SupportingDocumentsDiagnostics
{
    internal static void Write(string message)
    {
        try
        {
            var logRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SidwellCo",
                "ParcelWorkflow",
                "logs");
            Directory.CreateDirectory(logRoot);
            File.AppendAllText(
                Path.Combine(logRoot, "supporting_documents_dockpane.log"),
                $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
