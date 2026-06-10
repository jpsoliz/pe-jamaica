using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn;

internal sealed class ShowLoginWindowButton : Button
{
    protected override void OnUpdate()
    {
        Enabled = ShellState.Session.CanOpenLogin;
    }

    protected override void OnClick()
    {
        var loginWindow = new LoginWindow
        {
            Owner = FrameworkApplication.Current.MainWindow
        };
        loginWindow.ShowDialog();
    }
}
