using ArcGIS.Desktop.Framework.Controls;
using System.Windows;

namespace ParcelWorkflowAddIn;

internal partial class PointEditDialogWindow : ProWindow
{
    private readonly PointEditDialogViewModel viewModel;

    internal PointEditDialogWindow(PointEditDialogViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
    }

    internal PointEditDialogViewModel ViewModel => viewModel;

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
