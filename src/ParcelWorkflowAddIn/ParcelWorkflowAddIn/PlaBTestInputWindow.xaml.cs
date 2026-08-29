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
    private Func<PlaBTestEmulationInputViewModel, Task<PlaBTaskCompletionResult>> completeHandler;
    private Func<PlaBTestEmulationInputViewModel, Task<PlaBTaskCompletionResult>> cancelHandler;

    internal PlaBTestInputWindow(
        PlaBTestEmulationInputViewModel viewModel,
        Func<PlaBTestEmulationInputViewModel, Task<PlaBTestInputPreparationResult>> prepareHandler,
        Func<PlaBTestEmulationInputViewModel, Task<PlaBTaskCompletionResult>> completeHandler,
        Func<PlaBTestEmulationInputViewModel, Task<PlaBTaskCompletionResult>> cancelHandler)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        this.prepareHandler = prepareHandler;
        this.completeHandler = completeHandler;
        this.cancelHandler = cancelHandler;
        DataContext = viewModel;
        Owner = FrameworkApplication.Current?.MainWindow;
        Closed += OnClosed;
    }

    internal static void ShowOrActivate(
        string? currentTransactionNumber,
        string? peNumber,
        Func<PlaBTestEmulationInputViewModel, Task<PlaBTestInputPreparationResult>> prepareHandler,
        Func<PlaBTestEmulationInputViewModel, Task<PlaBTaskCompletionResult>> completeHandler,
        Func<PlaBTestEmulationInputViewModel, Task<PlaBTaskCompletionResult>> cancelHandler,
        string? statusText = null)
    {
        if (activeWindow is { IsVisible: true } window)
        {
            window.SetInputs(currentTransactionNumber, peNumber);
            window.prepareHandler = prepareHandler;
            window.completeHandler = completeHandler;
            window.cancelHandler = cancelHandler;
            window.viewModel.StatusText = statusText ?? string.Empty;
            window.Activate();
            return;
        }

        var input = new PlaBTestEmulationInputViewModel
        {
            CurrentTransactionNumber = currentTransactionNumber ?? string.Empty,
            PeNumber = peNumber ?? string.Empty,
            StatusText = statusText ?? string.Empty
        };
        activeWindow = new PlaBTestInputWindow(input, prepareHandler, completeHandler, cancelHandler);
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
        viewModel.ProcessSucceeded = result.Success;
        viewModel.ProcessMapGroupNames = result.Success
            ? result.MapGroupNames ?? Array.Empty<string>()
            : Array.Empty<string>();
        viewModel.StatusText = result.Message;
        if (result.Success)
        {
            return;
        }

        MessageBox.Show(
            this,
            result.Message,
            "Plan Annexation Task",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private async void CompleteButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(
            this,
            "Complete Plan Annexation Preparation and move to Review and Sign Plan Annexed Diagram?",
            "Plan Annexation Task",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var result = await completeHandler(viewModel).ConfigureAwait(true);
        viewModel.StatusText = result.Message;
        if (result.Success)
        {
            viewModel.ClearProcessState(result.Message);
        }

        MessageBox.Show(
            this,
            result.Message,
            "Plan Annexation Task",
            MessageBoxButton.OK,
            result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);

        if (result.Success)
        {
            Close();
        }
    }

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await cancelHandler(viewModel).ConfigureAwait(true);
        viewModel.StatusText = result.Message;
        MessageBox.Show(
            this,
            result.Message,
            "Plan Annexation Task",
            MessageBoxButton.OK,
            result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);

        if (result.Success)
        {
            Close();
        }
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
