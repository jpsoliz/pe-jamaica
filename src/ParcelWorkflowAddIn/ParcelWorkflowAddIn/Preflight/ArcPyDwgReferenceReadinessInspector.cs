using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Preflight;

public sealed class ArcPyDwgReferenceReadinessInspector : IDwgReferenceReadinessInspector
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);
    private readonly IProcessRunner processRunner;
    private readonly string pythonExecutable;

    public ArcPyDwgReferenceReadinessInspector(IProcessRunner processRunner)
        : this(processRunner, ProcessingEnvironmentSettings.Load().PythonExecutable)
    {
    }

    public ArcPyDwgReferenceReadinessInspector(IProcessRunner processRunner, string pythonExecutable)
    {
        this.processRunner = processRunner;
        this.pythonExecutable = pythonExecutable;
    }

    public async Task<DwgReferenceReadinessProbeResult> InspectAsync(string copiedPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pythonExecutable) || !File.Exists(pythonExecutable))
        {
            Debug.WriteLine("Innola DWG CAD probe skipped: Python executable is not configured.");
            return new DwgReferenceReadinessProbeResult(
                ProbeExecuted: false,
                Success: true,
                Message: "Python executable is not configured for DWG CAD probe.",
                Correction: "Configure arcgis_python_executable in WorkflowSettings.json.");
        }

        var script = BuildProbeScript(copiedPath);
        var escapedScript = EscapeArgument(script);
        Debug.WriteLine($"Innola DWG CAD probe starting. Path={copiedPath}; Timeout={ProbeTimeout.TotalSeconds}.");
        var result = await processRunner.RunAsync(
            pythonExecutable,
            $"-c \"{escapedScript}\"",
            ProbeTimeout,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.TimedOut)
        {
            Debug.WriteLine($"Innola DWG CAD probe timed out. StdOut={Sanitize(result.StandardOutput)}; StdErr={Sanitize(result.StandardError)}.");
            return new DwgReferenceReadinessProbeResult(
                ProbeExecuted: true,
                Success: false,
                "DWG CAD probe timed out while checking sub-layers.",
                "Check the ArcGIS Python environment and ensure ArcPy can access the file quickly.");
        }

        if (result.ExitCode != 0)
        {
            Debug.WriteLine($"Innola DWG CAD probe failed. ExitCode={result.ExitCode}; StdOut={Sanitize(result.StandardOutput)}; StdErr={Sanitize(result.StandardError)}.");
            return new DwgReferenceReadinessProbeResult(
                ProbeExecuted: true,
                Success: false,
                $"DWG CAD probe returned {result.ExitCode}: {GetProbeMessage(result.StandardOutput, result.StandardError)}",
                "Verify the DWG reference file is readable and the ArcGIS Python environment can import arcpy.");
        }

        if (ContainsMarker(result.StandardOutput, "dwg_probe_result:ok"))
        {
            return new DwgReferenceReadinessProbeResult(
                ProbeExecuted: true,
                Success: true,
                null,
                null,
                ReadMarkerJsonArray(result.StandardOutput, "dwg_probe_layers:"));
        }

        if (ContainsMarker(result.StandardOutput, "dwg_probe_result:no_sublayers"))
        {
            return new DwgReferenceReadinessProbeResult(
                ProbeExecuted: true,
                Success: false,
                "DWG file has no readable CAD sub-layers.",
                "Use a DWG reference that includes at least one point/polyline/polygon feature.");
        }

        if (ContainsMarker(result.StandardOutput, "dwg_probe_result:error"))
        {
            return new DwgReferenceReadinessProbeResult(
                ProbeExecuted: true,
                Success: false,
                GetProbeMessage(result.StandardOutput, result.StandardError),
                "Verify the selected file is a valid DWG and accessible to the Python environment.");
        }

        return new DwgReferenceReadinessProbeResult(
            ProbeExecuted: true,
            Success: false,
            "DWG CAD probe returned an unexpected result.",
            "Retry the transaction after refreshing intake.");
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<empty>";
        }

        const int maxLength = 512;
        if (value.Length <= maxLength)
        {
            return value.Replace("\r", "\\r").Replace("\n", "\\n");
        }

        return $"{value.Substring(0, maxLength)}...";
    }

    private static bool ContainsMarker(string output, string marker)
    {
        return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.StartsWith(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetProbeMessage(string stdout, string stderr)
    {
        var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Concat(stderr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        return lines.Length > 0 ? lines[^1] : "Unknown probe error.";
    }

    private static IReadOnlyList<string> ReadMarkerJsonArray(string output, string marker)
    {
        var line = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(item => item.StartsWith(marker, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(line))
        {
            return Array.Empty<string>();
        }

        try
        {
            var json = line.Substring(marker.Length);
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static string BuildProbeScript(string path)
    {
        return $"""
import arcpy
import json

path = r'{EscapeForPythonSingleQuotedString(path)}'
children = []
layer_names = []
try:
    desc = arcpy.Describe(path)
    raw_children = getattr(desc, "children", None)
    if raw_children:
        children = list(raw_children)
    for child in children:
        name = getattr(child, "name", None) or getattr(child, "baseName", None) or getattr(child, "datasetName", None)
        if name:
            layer_names.append(str(name))
        shape_type = getattr(child, "shapeType", None)
        if shape_type:
            layer_names.append(str(shape_type))
    layer_names = sorted(set([item.strip() for item in layer_names if item and item.strip()]))
    print("dwg_probe_layers:" + json.dumps(layer_names))
    print("dwg_probe_result:ok" if len(children) > 0 else "dwg_probe_result:no_sublayers")
except Exception as ex:
    print("dwg_probe_result:error:" + str(ex).replace('\\n', ' ').replace('\\r', ' '))
""";
    }

    private static string EscapeForPythonSingleQuotedString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    private static string EscapeArgument(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
