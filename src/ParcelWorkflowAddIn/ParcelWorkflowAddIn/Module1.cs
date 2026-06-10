using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace ParcelWorkflowAddIn;

public sealed class Module1 : Module
{
    private const string ModuleId = "ParcelWorkflow_Module";

    public static Module1 Current => (Module1)FrameworkApplication.FindModule(ModuleId);

    private Module1()
    {
    }

    protected override bool CanUnload()
    {
        return true;
    }
}
