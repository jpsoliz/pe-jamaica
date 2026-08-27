using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn;

internal sealed class ShowConfigurationWindowButton : Button
{
    protected override void OnUpdate()
    {
        Enabled = true;
    }

    protected override void OnClick()
    {
        try
        {
            var window = new ConfigurationWindow
            {
                Owner = FrameworkApplication.Current.MainWindow
            };
            window.ShowDialog();
        }
        catch (Exception exception)
        {
            RibbonCommandErrorReporter.Show("Settings", exception);
        }
    }
}
