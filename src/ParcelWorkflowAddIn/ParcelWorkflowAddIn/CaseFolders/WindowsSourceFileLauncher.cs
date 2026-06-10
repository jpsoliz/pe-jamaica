using System.Diagnostics;

namespace ParcelWorkflowAddIn.CaseFolders;

public sealed class WindowsSourceFileLauncher : ISourceFileLauncher
{
    public void OpenFile(string copiedPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = copiedPath,
            UseShellExecute = true
        });
    }

    public void RevealFile(string copiedPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{copiedPath}\"",
            UseShellExecute = true
        });
    }
}
