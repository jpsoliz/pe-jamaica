using ArcGIS.Desktop.Framework.Controls;
using System.Reflection;
using System.Windows;

namespace ParcelWorkflowAddIn;

public partial class AboutWindow : ProWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        ReleaseVersionTextBlock.Text = $"Release {GetReleaseVersion()}";
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
}
