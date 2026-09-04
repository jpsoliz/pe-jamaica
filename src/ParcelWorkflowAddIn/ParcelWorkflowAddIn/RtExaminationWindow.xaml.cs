using System.ComponentModel;
using System.Windows;
using ArcGIS.Desktop.Framework.Controls;
using ParcelWorkflowAddIn.Workflow.RtExamination;

namespace ParcelWorkflowAddIn;

public partial class RtExaminationWindow : ProWindow
{
    private static RtExaminationWindow? activeWindow;
    private readonly RtExaminationViewModel viewModel;
    private bool allowClose;

    public RtExaminationWindow(RtExaminationViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
        viewModel.RequestClose += OnViewModelRequestClose;
    }

    internal static bool IsOpen => activeWindow is { IsVisible: true };

    internal static bool TryActivateExisting()
    {
        if (activeWindow is not { IsVisible: true } window)
        {
            return false;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        return true;
    }

    internal static void ShowOrActivate(RtExaminationViewModel viewModel)
    {
        if (TryActivateExisting())
        {
            return;
        }

        var window = new RtExaminationWindow(viewModel)
        {
            Owner = Application.Current?.MainWindow
        };
        activeWindow = window;
        try
        {
            window.Show();
        }
        catch
        {
            activeWindow = null;
            throw;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await viewModel.LoadAsync().ConfigureAwait(true);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose)
        {
            return;
        }

        activeWindow = null;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        viewModel.RequestClose -= OnViewModelRequestClose;
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
}