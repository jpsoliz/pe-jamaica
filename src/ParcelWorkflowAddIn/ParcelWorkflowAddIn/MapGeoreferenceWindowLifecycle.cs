using System.IO;
using System.Reflection;

namespace ParcelWorkflowAddIn;

internal static class MapGeoreferenceWindowLifecycle
{
    public static void CloseIfOpen()
    {
        try
        {
            var windowType = Type.GetType("ParcelWorkflowAddIn.MapGeoreferenceWindow", throwOnError: false);
            var closeMethod = windowType?.GetMethod("CloseIfOpen", BindingFlags.NonPublic | BindingFlags.Static);
            closeMethod?.Invoke(null, null);
        }
        catch (Exception exception) when (IsMissingArcGisFramework(exception))
        {
            // Headless unit-test runs do not load ArcGIS Pro UI assemblies. In Pro, this close path is active.
        }
    }

    private static bool IsMissingArcGisFramework(Exception exception)
    {
        return exception is FileNotFoundException fileNotFound
            && (string.Equals(fileNotFound.FileName, "ArcGIS.Desktop.Framework", StringComparison.OrdinalIgnoreCase)
                || fileNotFound.Message.Contains("ArcGIS.Desktop.Framework", StringComparison.OrdinalIgnoreCase));
    }
}
