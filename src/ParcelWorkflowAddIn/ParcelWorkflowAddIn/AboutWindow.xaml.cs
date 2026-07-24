using ArcGIS.Desktop.Framework.Controls;
using System.Reflection;
using System.Windows;
using System.IO;

namespace ParcelWorkflowAddIn;

public partial class AboutWindow : ProWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        AddInDetailsTextBlock.Text = $"Add in version: {GetReleaseVersion()}\nAdd in date: {GetInstallDateText()}";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static string GetReleaseVersion()
    {
        var version = typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(version) ? "0.1.7" : version;
    }

    private static string GetInstallDateText()
    {
        var addInPath = FindInstalledAddInPath();
        if (string.IsNullOrWhiteSpace(addInPath))
        {
            addInPath = typeof(AboutWindow).Assembly.Location;
        }

        if (string.IsNullOrWhiteSpace(addInPath) || !File.Exists(addInPath))
        {
            return "Unavailable";
        }

        return File.GetLastWriteTime(addInPath).ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static string? FindInstalledAddInPath()
    {
        var addInRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ArcGIS",
            "AddIns",
            "ArcGISPro");
        if (!Directory.Exists(addInRoot))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(addInRoot, "ParcelWorkflowAddIn.esriAddinX", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();
    }
}
