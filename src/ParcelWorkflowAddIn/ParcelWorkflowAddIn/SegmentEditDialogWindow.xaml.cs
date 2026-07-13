using ArcGIS.Desktop.Framework.Controls;
using System.Windows;

namespace ParcelWorkflowAddIn;

internal partial class SegmentEditDialogWindow : ProWindow
{
    private readonly SegmentEditDialogViewModel viewModel;

    internal SegmentEditDialogWindow(SegmentEditDialogViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!viewModel.TryCommit())
        {
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
