using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn;

internal sealed class ShowConfigurationWindowButton : Button
{
    protected override void OnUpdate()
    {
        Enabled = ShellState.Session.CanOpenConfiguration;
    }

    protected override void OnClick()
    {
        var window = new ConfigurationWindow
        {
            Owner = FrameworkApplication.Current.MainWindow
        };
        window.ShowDialog();
    }
}
