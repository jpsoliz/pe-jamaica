using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow.Execution;
using System.IO;

namespace ParcelWorkflowAddIn.Workflow.Validation;

public sealed class ValidationPreviewExecutionService
{
    public const string PreviewArtifactFileName = "validation_preview_summary.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IProcessRunner processRunner;
    private readonly Func<WorkflowExecutionSettings> getExecutionSettings;

    public ValidationPreviewExecutionService()
        : this(new ProcessRunner(), () => WorkflowExecutionSettings.Load())
    {
    }

    public ValidationPreviewExecutionService(
        IProcessRunner processRunner,
        Func<WorkflowExecutionSettings> getExecutionSettings)
    {
        this.processRunner = processRunner;
        this.getExecutionSettings = getExecutionSettings;
    }

    public async Task<ValidationExecutionResult> RunAsync(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        string? operatorId,
        CancellationToken cancellationToken = default)
    {
        var executionSettings = getExecutionSettings();
        if (string.IsNullOrWhiteSpace(executionSettings.PythonExecutable) || !File.Exists(executionSettings.PythonExecutable))
        {
            return ValidationExecutionResult.Failed("Configured ArcGIS Python executable is not available for validation preview.");
        }

        if (string.IsNullOrWhiteSpace(executionSettings.ValidationAdapterScriptPath) || !File.Exists(executionSettings.ValidationAdapterScriptPath))
        {
            return ValidationExecutionResult.Failed("validation_adapter.py is not available for validation preview.");
        }

        var reviewDataPath = Path.Combine(layout.WorkingDirectory, "extraction_review_data.json");
        if (!File.Exists(reviewDataPath))
        {
            return ValidationExecutionResult.Failed("Point review data is not available for validation preview.");
        }

        var outputPath = Path.Combine(layout.WorkingDirectory, PreviewArtifactFileName);
        var dwgContextPath = Path.Combine(layout.WorkingDirectory, "dwg_context.json");
        var rulesPath = executionSettings.ValidationRulesPath;
        var settingsPath = InnolaTransactionSettings.ResolveActiveSettingsPath();

        var arguments = string.Join(" ",
            Quote(executionSettings.ValidationAdapterScriptPath),
            "--manifest", Quote(layout.ManifestPath),
            "--approved-review", Quote(reviewDataPath),
            "--review-data", Quote(reviewDataPath),
            "--source-root", Quote(layout.SourceDirectory),
            "--dwg-context", Quote(dwgContextPath),
            "--output", Quote(outputPath),
            "--operator", Quote(operatorId ?? string.Empty),
            "--rules", Quote(rulesPath ?? string.Empty),
            "--settings", Quote(settingsPath));

        var result = await processRunner.RunAsync(
            executionSettings.PythonExecutable,
            arguments,
            TimeSpan.FromSeconds(60),
            null,
            cancellationToken).ConfigureAwait(false);

        if (result.TimedOut)
        {
            return ValidationExecutionResult.Failed("Validation preview timed out before completion.");
        }

        if (result.ExitCode != 0)
        {
            return ValidationExecutionResult.Failed(Sanitize(result.StandardError, result.StandardOutput));
        }

        if (!File.Exists(outputPath))
        {
            return ValidationExecutionResult.Failed("Validation preview completed without producing validation_preview_summary.json.");
        }

        var summary = JsonSerializer.Deserialize<ValidationSummaryDocument>(File.ReadAllText(outputPath), JsonOptions);
        return summary is null
            ? ValidationExecutionResult.Failed("Validation preview summary could not be loaded.")
            : new ValidationExecutionResult(true, null, outputPath, summary);
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
            return "Validation preview failed without additional details.";
        }

        var lines = joined
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !LooksSensitive(line))
            .Take(6)
            .ToArray();
        var sanitized = string.Join(" ", lines);
        return sanitized.Length > 400 ? sanitized[..400] : sanitized;
    }

    private static bool LooksSensitive(string value)
    {
        return value.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            || value.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || value.Contains("bearer", StringComparison.OrdinalIgnoreCase)
            || value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("token", StringComparison.OrdinalIgnoreCase);
    }
}
