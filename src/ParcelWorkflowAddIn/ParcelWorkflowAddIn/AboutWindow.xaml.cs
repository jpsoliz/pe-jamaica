using ArcGIS.Desktop.Framework.Controls;
using System.Windows;

namespace ParcelWorkflowAddIn;

public partial class AboutWindow : ProWindow
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
