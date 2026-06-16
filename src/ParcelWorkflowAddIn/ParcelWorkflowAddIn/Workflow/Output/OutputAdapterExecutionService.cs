using System.IO;
using System.Text;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow.Execution;

namespace ParcelWorkflowAddIn.Workflow.Output;

public sealed class OutputAdapterExecutionService : IOutputExecutionService
{
    private readonly IProcessRunner processRunner;
    private readonly Func<WorkflowExecutionSettings> getExecutionSettings;
    private readonly OutputSummaryPersistenceService persistenceService;

    public OutputAdapterExecutionService()
        : this(new ProcessRunner(), () => WorkflowExecutionSettings.Load(), new OutputSummaryPersistenceService())
    {
    }

    public OutputAdapterExecutionService(
        IProcessRunner processRunner,
        Func<WorkflowExecutionSettings> getExecutionSettings,
        OutputSummaryPersistenceService persistenceService)
    {
        this.processRunner = processRunner;
        this.getExecutionSettings = getExecutionSettings;
        this.persistenceService = persistenceService;
    }

    public async Task<OutputExecutionResult> RunAsync(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        string? operatorId,
        CancellationToken cancellationToken = default)
    {
        var executionSettings = getExecutionSettings();
        if (string.IsNullOrWhiteSpace(executionSettings.PythonExecutable) || !File.Exists(executionSettings.PythonExecutable))
        {
            return OutputExecutionResult.Failed("Configured ArcGIS Python executable is not available for output generation.");
        }

        if (string.IsNullOrWhiteSpace(executionSettings.OutputAdapterScriptPath) || !File.Exists(executionSettings.OutputAdapterScriptPath))
        {
            return OutputExecutionResult.Failed("output_adapter.py is not available for output generation.");
        }

        var approvedReviewPath = Path.Combine(layout.WorkingDirectory, "approved_review.json");
        var reviewDataPath = Path.Combine(layout.WorkingDirectory, "extraction_review_data.json");
        var outputSummaryPath = Path.Combine(layout.OutputDirectory, persistenceService.OutputArtifactFileName);

        var arguments = string.Join(" ",
            Quote(executionSettings.OutputAdapterScriptPath),
            "--manifest", Quote(layout.ManifestPath),
            "--approved-review", Quote(approvedReviewPath),
            "--review-data", Quote(reviewDataPath),
            "--review-workspace-mode", Quote(executionSettings.ReviewWorkspaceMode),
            "--output-root", Quote(layout.OutputDirectory),
            "--output-summary", Quote(outputSummaryPath),
            "--operator", Quote(operatorId ?? string.Empty),
            "--template-project", Quote(executionSettings.OutputTemplateProjectPath ?? string.Empty),
            "--template-gdb", Quote(executionSettings.OutputTemplateGdbPath ?? string.Empty));

        var result = await processRunner.RunAsync(
            executionSettings.PythonExecutable,
            arguments,
            TimeSpan.FromSeconds(executionSettings.OutputAdapterTimeoutSeconds),
            null,
            cancellationToken).ConfigureAwait(false);
        WriteExecutionLog(layout, executionSettings, arguments, result);

        if (result.TimedOut)
        {
            return OutputExecutionResult.Failed("Output generation timed out before completion.");
        }

        if (result.ExitCode != 0)
        {
            return OutputExecutionResult.Failed(Sanitize(result.StandardError, result.StandardOutput));
        }

        if (!File.Exists(outputSummaryPath))
        {
            return OutputExecutionResult.Failed("Output generation completed without producing output_summary.json.");
        }

        var summary = persistenceService.Load(layout);
        if (summary is null)
        {
            return OutputExecutionResult.Failed("Output summary could not be loaded after output generation completed.");
        }

        return new OutputExecutionResult(true, null, outputSummaryPath, summary);
    }

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }

    private static string Sanitize(params string?[] values)
    {
        var joined = string.Join(Environment.NewLine, values.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(joined))
        {
            return "Output generation failed without additional details.";
        }

        var lines = joined
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !LooksSensitive(line))
            .Take(6)
            .ToArray();
        var sanitized = string.Join(" ", lines);
        if (sanitized.Length > 400)
        {
            sanitized = sanitized[..400];
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "Output generation failed without additional details." : sanitized;
    }

    private static bool LooksSensitive(string value)
    {
        return value.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            || value.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || value.Contains("bearer", StringComparison.OrdinalIgnoreCase)
            || value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("token", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteExecutionLog(
        CaseFolderLayout layout,
        WorkflowExecutionSettings executionSettings,
        string arguments,
        ProcessRunResult result)
    {
        try
        {
            Directory.CreateDirectory(layout.LogsDirectory);
            var path = Path.Combine(layout.LogsDirectory, "process.log");
            var builder = new StringBuilder();
            builder.AppendLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] Output adapter execution");
            builder.AppendLine($"Python: {executionSettings.PythonExecutable}");
            builder.AppendLine($"Mode: {executionSettings.ReviewWorkspaceMode}");
            builder.AppendLine($"Adapter: {executionSettings.OutputAdapterScriptPath}");
            builder.AppendLine($"Timeout seconds: {executionSettings.OutputAdapterTimeoutSeconds}");
            builder.AppendLine($"Timed out: {result.TimedOut}");
            builder.AppendLine($"Exit code: {result.ExitCode}");
            builder.AppendLine($"Arguments: {Sanitize(arguments)}");

            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                builder.AppendLine("STDOUT:");
                builder.AppendLine(Sanitize(result.StandardOutput));
            }

            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                builder.AppendLine("STDERR:");
                builder.AppendLine(Sanitize(result.StandardError));
            }

            builder.AppendLine();
            File.AppendAllText(path, builder.ToString());
        }
        catch
        {
            // Logging should never block the user-facing workflow result.
        }
    }
}
