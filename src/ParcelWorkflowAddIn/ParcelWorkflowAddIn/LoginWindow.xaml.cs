using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Controls;
using ParcelWorkflowAddIn.Innola;
using System.Windows;

namespace ParcelWorkflowAddIn;

public partial class LoginWindow : ProWindow
{
    public LoginWindow()
    {
        InitializeComponent();
        ServerTextBox.Text = ShellState.ConfiguredServerUrl;
        StatusTextBlock.Text = ShellState.Session.StatusText;
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        LoginButton.IsEnabled = false;
        StatusTextBlock.Text = "Logging in.";

        try
        {
            var result = await ShellState.Session.LoginAsync(ServerTextBox.Text, UsernameTextBox.Text, PasswordBox.Password);
            StatusTextBlock.Text = ShellState.Session.StatusText;

            if (result.Success)
            {
                FrameworkApplication.DockPaneManager.Find(TransactionPanelDockpaneViewModel.DockPaneId)?.Activate();
                DialogResult = true;
            }
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
