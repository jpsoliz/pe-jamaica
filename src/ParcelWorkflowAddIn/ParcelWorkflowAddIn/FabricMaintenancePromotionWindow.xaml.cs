using System.Windows;
using System.ComponentModel;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Controls;
using ParcelWorkflowAddIn.Workflow.FabricMaintenance;

namespace ParcelWorkflowAddIn;

public partial class FabricMaintenancePromotionWindow : ProWindow
{
    private static FabricMaintenancePromotionWindow? activeWindow;
    private bool allowClose;

    public FabricMaintenancePromotionWindow(FabricMaintenancePromotionViewModel viewModel)
    {
        InitializeComponent();
        SetViewModel(viewModel);
        Owner = FrameworkApplication.Current?.MainWindow;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    public static void ShowOrActivate(FabricMaintenancePromotionViewModel viewModel)
    {
        if (activeWindow is { IsVisible: true } window)
        {
            window.SetViewModel(viewModel);
            window.Activate();
            return;
        }

        activeWindow = new FabricMaintenancePromotionWindow(viewModel);
        try
        {
            activeWindow.Show();
        }
        catch
        {
            activeWindow = null;
            throw;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose)
        {
            return;
        }

        e.Cancel = true;
        if (DataContext is FabricMaintenancePromotionViewModel viewModel
            && viewModel.CancelCommand.CanExecute(null))
        {
            viewModel.CancelCommand.Execute(null);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closing -= OnClosing;
        Closed -= OnClosed;
        if (DataContext is FabricMaintenancePromotionViewModel viewModel)
        {
            viewModel.RequestClose -= OnViewModelRequestClose;
        }

        if (ReferenceEquals(activeWindow, this))
        {
            activeWindow = null;
        }
    }

    private void OnViewModelRequestClose(object? sender, EventArgs e)
    {
        allowClose = true;
        Close();
    }

    private void SetViewModel(FabricMaintenancePromotionViewModel viewModel)
    {
        if (DataContext is FabricMaintenancePromotionViewModel existing)
        {
            existing.RequestClose -= OnViewModelRequestClose;
        }

        DataContext = viewModel;
        viewModel.RequestClose += OnViewModelRequestClose;
    }
}
