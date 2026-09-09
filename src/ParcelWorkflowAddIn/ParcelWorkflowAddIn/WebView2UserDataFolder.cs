using System.IO;

namespace ParcelWorkflowAddIn;

internal static class WebView2UserDataFolder
{
    internal static string ForViewer(string viewerName)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData",
                "Local");
        }

        var path = Path.Combine(
            localAppData,
            "SidwellCo",
            "ParcelWorkflowAddIn",
            "WebView2",
            viewerName);
        Directory.CreateDirectory(path);
        return path;
    }
}
