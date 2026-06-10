using System.Reflection;

namespace ParcelWorkflowAddIn.Preflight;

public sealed class ArcGisProEnvironmentProvider : IArcGisProEnvironmentProvider
{
    public string? GetArcGisProVersion()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(item => item.GetName().Name?.StartsWith("ArcGIS.Desktop.", StringComparison.OrdinalIgnoreCase) == true);

        return assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly?.GetName().Version?.ToString();
    }
}
