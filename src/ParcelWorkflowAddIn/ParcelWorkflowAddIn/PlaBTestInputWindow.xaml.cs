using System.Windows;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Controls;
using ParcelWorkflowAddIn.Workflow.Pla;

namespace ParcelWorkflowAddIn;

public partial class PlaBTestInputWindow : ProWindow
{
    private static PlaBTestInputWindow? activeWindow;
    private readonly PlaBTestEmulationInputViewModel viewModel;
    private Func<PlaBTestEmulationInputViewModel, Task<PlaBTestInputPreparationResult>> prepareHandler;
    private Func<PlaBTestEmulationInputViewModel, Task<PlaBTestInputPreparationResult>> openViewerHandler;

    internal PlaBTestInputWindow(
        PlaBTestEmulationInputViewModel viewModel,
        Func<PlaBTestEmulationInputViewModel, Task<PlaBTestInputPreparationResult>> prepareHandler,
        Func<PlaBTestEmulationInputViewModel, Task<PlaBTestInputPreparationResult>> openViewerHandler)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        this.prepareHandler = prepareHandler;
        this.openViewerHandler = openViewerHandler;
        DataContext = viewModel;
        Owner = FrameworkApplication.Current?.MainWindow;
        Closed += OnClosed;
    }

    internal static void ShowOrActivate(
        string? currentTransactionNumber,
        string? peNumber,
        Func<PlaBTestEmulationInputViewModel, Task<PlaBTestInputPreparationResult>> prepareHandler,
        Func<PlaBTestEmulationInputViewModel, Task<PlaBTestInputPreparationResult>> openViewerHandler)
    {
        if (activeWindow is { IsVisible: true } window)
        {
            window.SetInputs(currentTransactionNumber, peNumber);
            window.prepareHandler = prepareHandler;
            window.openViewerHandler = openViewerHandler;
            window.Activate();
            return;
        }

        var input = new PlaBTestEmulationInputViewModel
        {
            CurrentTransactionNumber = currentTransactionNumber ?? string.Empty,
            PeNumber = peNumber ?? string.Empty
        };
        activeWindow = new PlaBTestInputWindow(input, prepareHandler, openViewerHandler);
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

    private void SetInputs(string? currentTransactionNumber, string? peNumber)
    {
        viewModel.CurrentTransactionNumber = currentTransactionNumber ?? string.Empty;
        viewModel.PeNumber = peNumber ?? string.Empty;
    }

    private async void PrepareButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await prepareHandler(viewModel).ConfigureAwait(true);
        MessageBox.Show(
            this,
            result.Message,
            "PLA_B Test Input",
            MessageBoxButton.OK,
            result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        if (result.Success)
        {
            Close();
        }
    }

    private async void OpenViewerButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await openViewerHandler(viewModel).ConfigureAwait(true);
        MessageBox.Show(
            this,
            result.Message,
            "PLA_B Test Input",
            MessageBoxButton.OK,
            result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        if (ReferenceEquals(activeWindow, this))
        {
            activeWindow = null;
        }
    }
}
