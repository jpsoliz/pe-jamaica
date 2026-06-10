using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn;

internal sealed class ShowAboutWindowButton : Button
{
    protected override void OnUpdate()
    {
        Enabled = ShellState.Session.CanOpenAbout;
    }

    protected override void OnClick()
    {
        var window = new AboutWindow
        {
            Owner = FrameworkApplication.Current.MainWindow
        };
        window.ShowDialog();
    }
}
