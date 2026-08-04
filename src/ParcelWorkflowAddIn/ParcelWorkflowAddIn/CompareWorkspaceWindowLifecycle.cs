using System.IO;
using System.Reflection;

namespace ParcelWorkflowAddIn;

internal static class CompareWorkspaceWindowLifecycle
{
    public static bool IsOpen()
    {
        try
        {
            var windowType = Type.GetType("ParcelWorkflowAddIn.CompareWorkspaceWindow", throwOnError: false);
            var isOpenProperty = windowType?.GetProperty("IsOpen", BindingFlags.NonPublic | BindingFlags.Static);
            return isOpenProperty?.GetValue(null) is true;
        }
        catch (Exception exception) when (IsMissingArcGisFramework(exception))
        {
            return false;
        }
    }

    public static bool TryActivateExisting()
    {
        try
        {
            var windowType = Type.GetType("ParcelWorkflowAddIn.CompareWorkspaceWindow", throwOnError: false);
            var activateMethod = windowType?.GetMethod("TryActivateExisting", BindingFlags.NonPublic | BindingFlags.Static);
            return activateMethod?.Invoke(null, null) is true;
        }
        catch (Exception exception) when (IsMissingArcGisFramework(exception))
        {
            return false;
        }
    }

    private static bool IsMissingArcGisFramework(Exception exception)
    {
        return exception is FileNotFoundException fileNotFound
            && (string.Equals(fileNotFound.FileName, "ArcGIS.Desktop.Framework", StringComparison.OrdinalIgnoreCase)
                || fileNotFound.Message.Contains("ArcGIS.Desktop.Framework", StringComparison.OrdinalIgnoreCase));
    }
}
