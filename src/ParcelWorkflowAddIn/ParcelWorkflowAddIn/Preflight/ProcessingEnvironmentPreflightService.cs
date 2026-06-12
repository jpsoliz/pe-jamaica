using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Preflight;

public sealed class ProcessingEnvironmentPreflightService : IProcessingEnvironmentPreflightService
{
    private static readonly TimeSpan PythonProbeTimeout = TimeSpan.FromSeconds(30);
    private readonly ProcessingEnvironmentSettings settings;
    private readonly IProcessRunner processRunner;
    private readonly IArcGisProEnvironmentProvider arcGisProEnvironmentProvider;

    public ProcessingEnvironmentPreflightService()
        : this(ProcessingEnvironmentSettings.Load(), new ProcessRunner(), new ArcGisProEnvironmentProvider())
    {
    }

    public ProcessingEnvironmentPreflightService(
        ProcessingEnvironmentSettings settings,
        IProcessRunner processRunner,
        IArcGisProEnvironmentProvider arcGisProEnvironmentProvider)
    {
        this.settings = settings;
        this.processRunner = processRunner;
        this.arcGisProEnvironmentProvider = arcGisProEnvironmentProvider;
    }

    public async Task<ProcessingEnvironmentPreflightResult> RunAsync(CaseFolderLayout layout, CancellationToken cancellationToken = default)
    {
        var blockers = new List<PreflightCheck>();
        var warnings = new List<PreflightCheck>();
        var passed = new List<PreflightCheck>();

        CheckArcGisProCompatibility(blockers, warnings, passed);
        CheckWorkspaceAccess(layout, blockers, passed);
        await CheckPythonAsync(blockers, warnings, passed, cancellationToken).ConfigureAwait(false);

        return new ProcessingEnvironmentPreflightResult(blockers, warnings, passed);
    }

    private void CheckArcGisProCompatibility(List<PreflightCheck> blockers, List<PreflightCheck> warnings, List<PreflightCheck> passed)
    {
        if (!string.Equals(settings.ArcGisProSdkLane, "3.6", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(PreflightCheck.BlockerForCategory(
                "arcgis_pro",
                "arcgis_pro_sdk_lane_compatible",
                $"ArcGIS Pro SDK lane {settings.ArcGisProSdkLane} is not compatible with the configured 3.6 lane.",
                null,
                null,
                "Set arcgis_pro_sdk_lane to 3.6 for this add-in lane."));
            return;
        }

        passed.Add(PreflightCheck.PassedForCategory(
            "arcgis_pro",
            "arcgis_pro_sdk_lane_compatible",
            "Passed: ArcGIS Pro SDK lane is configured for 3.6."));

        if (!string.Equals(settings.TargetFramework, "net8.0-windows", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(PreflightCheck.BlockerForCategory(
                "arcgis_pro",
                "target_framework_compatible",
                $"Target framework {settings.TargetFramework} is not compatible with the ArcGIS Pro 3.6 add-in lane.",
                null,
                null,
                "Set target_framework to net8.0-windows."));
        }
        else
        {
            passed.Add(PreflightCheck.PassedForCategory(
                "arcgis_pro",
                "target_framework_compatible",
                "Passed: target framework is net8.0-windows."));
        }

        var detectedVersion = arcGisProEnvironmentProvider.GetArcGisProVersion();
        if (string.IsNullOrWhiteSpace(detectedVersion))
        {
            var check = PreflightCheck.WarningForCategory(
                "arcgis_pro",
                "arcgis_pro_version_detected",
                "ArcGIS Pro version could not be detected from the current runtime.",
                null,
                null,
                "Run this check inside ArcGIS Pro 3.6 or confirm the add-in manager target version.");
            if (settings.UnknownArcGisVersionIsWarning)
            {
                warnings.Add(check);
            }
            else
            {
                blockers.Add(check with { Severity = "blocker", Status = "blocked" });
            }

            return;
        }

        var normalizedDetectedVersion = NormalizeDetectedArcGisProVersion(detectedVersion);
        if (!IsDetectedArcGisVersionCompatible(normalizedDetectedVersion, settings.ArcGisProSdkLane))
        {
            blockers.Add(PreflightCheck.BlockerForCategory(
                "arcgis_pro",
                "arcgis_pro_version_compatible",
                $"Detected ArcGIS Pro version {DisplayDetectedArcGisVersion(detectedVersion, normalizedDetectedVersion)} is not compatible with the configured {settings.ArcGisProSdkLane} lane.",
                null,
                null,
                "Use ArcGIS Pro 3.6/3.7 or rebuild/package for the detected lane."));
            return;
        }

        passed.Add(PreflightCheck.PassedForCategory(
            "arcgis_pro",
            "arcgis_pro_version_compatible",
            $"Passed: detected ArcGIS Pro version {DisplayDetectedArcGisVersion(detectedVersion, normalizedDetectedVersion)} is compatible."));
    }

    private static bool IsDetectedArcGisVersionCompatible(string detectedVersion, string configuredLane)
    {
        if (detectedVersion.StartsWith(configuredLane, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return configuredLane.StartsWith("3.6", StringComparison.OrdinalIgnoreCase)
            && detectedVersion.StartsWith("3.7", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDetectedArcGisProVersion(string detectedVersion)
    {
        var sanitized = Sanitize(detectedVersion);
        var match = Regex.Match(sanitized, @"^(?<major>\d+)\.(?<minor>\d+)(?<rest>(?:\.\d+)*)");
        if (!match.Success)
        {
            return sanitized;
        }

        var major = int.Parse(match.Groups["major"].Value);
        var minor = int.Parse(match.Groups["minor"].Value);
        if (major >= 10 && minor is >= 0 and <= 9)
        {
            return $"3.{minor}{match.Groups["rest"].Value}";
        }

        return sanitized;
    }

    private static string DisplayDetectedArcGisVersion(string detectedVersion, string normalizedDetectedVersion)
    {
        var sanitized = Sanitize(detectedVersion);
        return string.Equals(sanitized, normalizedDetectedVersion, StringComparison.OrdinalIgnoreCase)
            ? normalizedDetectedVersion
            : $"{normalizedDetectedVersion} (assembly {sanitized})";
    }

    private static void CheckWorkspaceAccess(CaseFolderLayout layout, List<PreflightCheck> blockers, List<PreflightCheck> passed)
    {
        CheckDirectoryReadable("workspace", "case_folder_readable", layout.RootDirectory, "Case Folder root is missing or unreadable.", blockers, passed);
        CheckDirectoryReadable("workspace", "source_directory_readable", layout.SourceDirectory, "Case Folder source directory is missing or unreadable.", blockers, passed);
        CheckDirectoryWritable("write_access", "working_directory_writable", layout.WorkingDirectory, "Case Folder working directory is not writable.", blockers, passed);
        CheckDirectoryWritable("write_access", "output_directory_writable", layout.OutputDirectory, "Case Folder output directory is not writable.", blockers, passed);
        CheckDirectoryWritable("write_access", "preflight_summary_writable", Path.GetDirectoryName(layout.PreflightSummaryPath)!, "Preflight summary location is not writable.", blockers, passed);
    }

    private async Task CheckPythonAsync(List<PreflightCheck> blockers, List<PreflightCheck> warnings, List<PreflightCheck> passed, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.PythonExecutable))
        {
            blockers.Add(PreflightCheck.BlockerForCategory(
                "python",
                "python_executable_configured",
                "Python executable is not configured.",
                null,
                null,
                "Set arcgis_python_executable in WorkflowSettings.json."));
            return;
        }

        passed.Add(PreflightCheck.PassedForCategory(
            "python",
            "python_executable_configured",
            "Passed: Python executable is configured.",
            settings.PythonExecutable));

        if (!File.Exists(settings.PythonExecutable))
        {
            blockers.Add(PreflightCheck.BlockerForCategory(
                "python",
                "python_executable_exists",
                "Configured Python executable does not exist.",
                settings.PythonExecutable,
                null,
                "Update arcgis_python_executable to the ArcGIS Pro Python environment python.exe."));
            return;
        }

        passed.Add(PreflightCheck.PassedForCategory(
            "python",
            "python_executable_exists",
            "Passed: configured Python executable exists.",
            settings.PythonExecutable));

        var packages = settings.RequiredPackages
            .Concat(settings.OptionalPackages)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var importScript = BuildPythonProbeScript(packages);
        Debug.WriteLine($"Innola Python preflight: running import probe. Python={settings.PythonExecutable}; Timeout={PythonProbeTimeout.TotalSeconds:F0}s; Packages={string.Join(',', packages)}.");
        var pythonProbeClock = Stopwatch.StartNew();
        ProcessRunResult result;
        try
        {
            result = await processRunner.RunAsync(
                settings.PythonExecutable,
                $"-c \"{EscapeArgument(importScript)}\"",
                PythonProbeTimeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            pythonProbeClock.Stop();
            Debug.WriteLine($"Innola Python preflight: import probe completed in {pythonProbeClock.ElapsedMilliseconds} ms (exit={result.ExitCode}, timedOut={result.TimedOut}). OutputLen={result.StandardOutput?.Length ?? 0}, ErrorLen={result.StandardError?.Length ?? 0}.");
            Debug.WriteLine($"Innola Python preflight: probe output:\n{Sanitize(result.StandardOutput ?? string.Empty)}");
            LogPythonProbePackageTimings(result.StandardOutput ?? string.Empty);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            blockers.Add(PreflightCheck.BlockerForCategory(
                "python",
                "python_executable_invokable",
                "Configured Python executable could not be invoked.",
                settings.PythonExecutable,
                null,
                "Repair the configured Python environment or update arcgis_python_executable."));
            return;
        }

        if (result.TimedOut)
        {
            var timedOutPackage = TryGetLastCheckedPackage(result.StandardOutput ?? string.Empty);
            var timeoutDetail = timedOutPackage is null
                ? "No package check completed before timeout."
                : $"Last package seen before timeout: {timedOutPackage}.";
            Debug.WriteLine($"Innola Python preflight timed out. Python={settings.PythonExecutable}; Output={Sanitize(result.StandardOutput ?? string.Empty)}; {timeoutDetail}");
            blockers.Add(PreflightCheck.BlockerForCategory(
                "python",
                "python_probe_timeout",
                $"Python environment check timed out. {timeoutDetail}",
                settings.PythonExecutable,
                null,
                "Close conflicting Python processes or repair the configured environment."));
            return;
        }

        if (result.ExitCode != 0)
        {
            blockers.Add(PreflightCheck.BlockerForCategory(
                "python",
                "python_executable_invokable",
                "Configured Python executable returned an error during environment checks.",
                settings.PythonExecutable,
                null,
                "Repair the configured Python environment or update arcgis_python_executable."));
            return;
        }

        passed.Add(PreflightCheck.PassedForCategory(
            "python",
            "python_executable_invokable",
            "Passed: configured Python executable can be invoked.",
            settings.PythonExecutable));

        foreach (var package in packages)
        {
            var checkId = $"python_package_{NormalizeCheckId(package)}_available";
            if (ProbeOutputMarksPackageAvailable(result.StandardOutput ?? string.Empty, package))
            {
                passed.Add(PreflightCheck.PassedForCategory(
                    "python",
                    checkId,
                    $"Passed: Python package {package} is available.",
                    settings.PythonExecutable));
                continue;
            }

            var missingMessage = package.Equals("arcpy", StringComparison.OrdinalIgnoreCase)
                ? "ArcPy is not available in the configured Python environment."
                : $"Python package {package} is not available in the configured environment.";
            var correction = package.Equals("arcpy", StringComparison.OrdinalIgnoreCase)
                ? "Use an ArcGIS Pro Python environment with ArcPy available."
                : $"Install {package} in the configured Python environment or mark it optional.";
            var isRequired = settings.RequiredPackages.Contains(package, StringComparer.OrdinalIgnoreCase)
                || (settings.ArcPyRequired && package.Equals("arcpy", StringComparison.OrdinalIgnoreCase));
            if (isRequired)
            {
                blockers.Add(PreflightCheck.BlockerForCategory("python", checkId, missingMessage, settings.PythonExecutable, null, correction));
            }
            else
            {
                warnings.Add(PreflightCheck.WarningForCategory("python", checkId, missingMessage, settings.PythonExecutable, null, correction));
            }
        }
    }

    private static void CheckDirectoryReadable(string category, string checkId, string path, string failureMessage, List<PreflightCheck> blockers, List<PreflightCheck> passed)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                blockers.Add(PreflightCheck.BlockerForCategory(category, checkId, failureMessage, path, null, "Reopen or recreate the Case Folder."));
                return;
            }

            _ = Directory.EnumerateFileSystemEntries(path).Take(1).ToArray();
            passed.Add(PreflightCheck.PassedForCategory(category, checkId, $"Passed: {ReadableName(checkId)}.", path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            blockers.Add(PreflightCheck.BlockerForCategory(category, checkId, failureMessage, path, null, "Repair permissions or recreate the Case Folder."));
        }
    }

    private static void CheckDirectoryWritable(string category, string checkId, string path, string failureMessage, List<PreflightCheck> blockers, List<PreflightCheck> passed)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probePath = Path.Combine(path, $".preflight_write_probe_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "probe");
            File.Delete(probePath);
            passed.Add(PreflightCheck.PassedForCategory(category, checkId, $"Passed: {ReadableName(checkId)}.", path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            blockers.Add(PreflightCheck.BlockerForCategory(category, checkId, failureMessage, path, null, "Repair permissions or select a writable Case Folder location."));
        }
    }

    private static string BuildPythonProbeScript(IReadOnlyList<string> packages)
    {
        var imports = string.Join(",", packages.Select(package => $"'{package.Replace("'", string.Empty)}'"));
        return "import importlib,time,sys\n"
            + "print('python_version_available')\n"
            + $"mods=[{imports}]\n"
            + "for m in mods:\n"
            + "    print('python_probe_package:'+m+':checking')\n"
            + "    start=time.perf_counter()\n"
            + "    try:\n"
            + "        importlib.import_module(m)\n"
            + "        elapsed=(time.perf_counter()-start)*1000\n"
            + "        print('python_probe_package:'+m+':ok:'+str(round(elapsed,3)) )\n"
            + "        print('package:'+m+':ok')\n"
            + "    except Exception:\n"
            + "        elapsed=(time.perf_counter()-start)*1000\n"
            + "        print('python_probe_package:'+m+':missing:'+str(round(elapsed,3)) )\n"
            + "        print('package:'+m+':missing')\n";
    }

    private static bool ProbeOutputMarksPackageAvailable(string output, string package)
    {
        return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(line =>
            {
                var trimmed = line.Trim();
                return string.Equals(trimmed, $"package:{package}:ok", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trimmed, $"python_probe_package:{package}:ok", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith($"python_probe_package:{package}:ok:", StringComparison.OrdinalIgnoreCase);
            });
    }

    private static void LogPythonProbePackageTimings(string output)
    {
        foreach (var entry in ParsePythonProbePackageTimings(output))
        {
            var elapsed = entry.ElapsedMilliseconds.HasValue
                ? $"{entry.ElapsedMilliseconds.Value:F3} ms"
                : "n/a";
            Debug.WriteLine($"Innola Python preflight: package probe timing package='{entry.Package}' status='{entry.Status}' elapsed_ms='{elapsed}'.");
        }
    }

    private static IEnumerable<(string Package, string Status, double? ElapsedMilliseconds)> ParsePythonProbePackageTimings(string output)
    {
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("python_probe_package:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = trimmed.Split(':');
            if (parts.Length < 3)
            {
                continue;
            }

            var packageName = parts[1];
            var status = parts[2];
            if (string.Equals(status, "checking", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            double? elapsedMs = null;
            if (parts.Length >= 4 && double.TryParse(parts[3], out var parsedElapsed))
            {
                elapsedMs = parsedElapsed;
            }

            yield return (packageName, status, elapsedMs);
        }
    }

    private static string? TryGetLastCheckedPackage(string output)
    {
        var markerPrefix = "python_probe_package:";
        foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Reverse())
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(markerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = trimmed.Split(':');
            return parts.Length >= 2 ? parts[1] : null;
        }

        return null;
    }

    private static string EscapeArgument(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string NormalizeCheckId(string value)
    {
        return Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
    }

    private static string Sanitize(string value)
    {
        var withoutSecrets = Regex.Replace(value, "(token|password|secret|authorization)[^\\s]*", "$1_redacted", RegexOptions.IgnoreCase);
        return withoutSecrets.Length > 120 ? withoutSecrets[..120] : withoutSecrets;
    }

    private static string ReadableName(string checkId)
    {
        return checkId.Replace('_', ' ');
    }
}
