using ArcGIS.Desktop.Framework.Controls;
using System.Windows;

namespace ParcelWorkflowAddIn;

public partial class ConfigurationWindow : ProWindow
{
    public ConfigurationWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
