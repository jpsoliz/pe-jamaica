using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Controls;
using ParcelWorkflowAddIn.Innola;
using System.Windows;

namespace ParcelWorkflowAddIn;

public partial class LoginWindow : ProWindow
{
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromSeconds(30);
    private readonly InnolaLoginPreferenceStore preferenceStore;

    public LoginWindow()
        : this(new InnolaLoginPreferenceStore())
    {
    }

    internal LoginWindow(InnolaLoginPreferenceStore preferenceStore)
    {
        this.preferenceStore = preferenceStore;
        InitializeComponent();
        ServerTextBlock.Text = ShellState.ConfiguredServerUrl;
        StatusTextBlock.Text = ShellState.Session.StatusText;
        var preferences = preferenceStore.Load();
        UsernameTextBox.Text = preferences.RememberedUsername ?? string.Empty;
        RememberMeCheckBox.IsChecked = preferences.RememberMe;
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
                if (RememberMeCheckBox.IsChecked == true)
                {
                    preferenceStore.SaveRememberedUser(UsernameTextBox.Text);
                }
                else
                {
                    preferenceStore.Clear();
                }

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
        catch (Exception exception)
        {
            StatusTextBlock.Text = $"Login failed before completion: {exception.Message}";
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
