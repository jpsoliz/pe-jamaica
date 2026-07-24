using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Controls;
using ParcelWorkflowAddIn.Innola;
using System.Windows;

namespace ParcelWorkflowAddIn;

public partial class LoginWindow : ProWindow
{
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromSeconds(30);

    public LoginWindow()
    {
        InitializeComponent();
        ServerTextBlock.Text = ShellState.ConfiguredServerUrl;
        StatusTextBlock.Text = ShellState.Session.StatusText;
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        LoginButton.IsEnabled = false;
        StatusTextBlock.Text = "Logging in.";

        try
        {
            using var timeout = new CancellationTokenSource(LoginTimeout);
            var result = await ShellState.Session.LoginAsync(ShellState.ConfiguredServerUrl, UsernameTextBox.Text, PasswordBox.Password, timeout.Token);
            StatusTextBlock.Text = ShellState.Session.StatusText;

            if (result.Success)
            {
                FrameworkApplication.DockPaneManager.Find(TransactionPanelDockpaneViewModel.DockPaneId)?.Activate();
                DialogResult = true;
            }
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "Login timed out. Check server, certificate, and network.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Net.Http.HttpRequestException)
        {
            StatusTextBlock.Text = "Login failed. Check server, certificate, and network.";
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
