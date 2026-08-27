using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn;

internal sealed class ShowAboutWindowButton : Button
{
    protected override void OnUpdate()
    {
        Enabled = true;
    }

    protected override void OnClick()
    {
        try
        {
            var window = new AboutWindow
            {
                Owner = FrameworkApplication.Current.MainWindow
            };
            window.ShowDialog();
        }
        catch (Exception exception)
        {
            RibbonCommandErrorReporter.Show("About", exception);
        }
    }
}
