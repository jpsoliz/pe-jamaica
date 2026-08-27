using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn;

internal sealed class ShowLoginWindowButton : Button
{
    protected override void OnUpdate()
    {
        Enabled = true;
    }

    protected override void OnClick()
    {
        try
        {
            var loginWindow = new LoginWindow
            {
                Owner = FrameworkApplication.Current.MainWindow
            };
            loginWindow.ShowDialog();
        }
        catch (Exception exception)
        {
            RibbonCommandErrorReporter.Show("Login", exception);
        }
    }
}
