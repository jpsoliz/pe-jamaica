using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow.Execution;
using System.IO;

namespace ParcelWorkflowAddIn.Workflow.Validation;

public sealed class ValidationAdapterExecutionService : IValidationExecutionService
{
    private readonly IProcessRunner processRunner;
    private readonly Func<WorkflowExecutionSettings> getExecutionSettings;
    private readonly ValidationSummaryPersistenceService persistenceService;

    public ValidationAdapterExecutionService()
        : this(new ProcessRunner(), WorkflowExecutionSettings.Load, new ValidationSummaryPersistenceService())
    {
    }

    public ValidationAdapterExecutionService(
        IProcessRunner processRunner,
        Func<WorkflowExecutionSettings> getExecutionSettings,
        ValidationSummaryPersistenceService persistenceService)
    {
        this.processRunner = processRunner;
        this.getExecutionSettings = getExecutionSettings;
        this.persistenceService = persistenceService;
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
            return ValidationExecutionResult.Failed("Configured ArcGIS Python executable is not available for validation.");
        }

        if (string.IsNullOrWhiteSpace(executionSettings.ValidationAdapterScriptPath) || !File.Exists(executionSettings.ValidationAdapterScriptPath))
        {
            return ValidationExecutionResult.Failed("validation_adapter.py is not available for validation.");
        }

        var approvedReviewPath = Path.Combine(layout.WorkingDirectory, "approved_review.json");
        var reviewDataPath = Path.Combine(layout.WorkingDirectory, "extraction_review_data.json");
        var outputPath = Path.Combine(layout.WorkingDirectory, persistenceService.ValidationArtifactFileName);
        var rulesPath = executionSettings.ValidationRulesPath;

        var arguments = string.Join(" ",
            Quote(executionSettings.ValidationAdapterScriptPath),
            "--manifest", Quote(layout.ManifestPath),
            "--approved-review", Quote(approvedReviewPath),
            "--review-data", Quote(reviewDataPath),
            "--output", Quote(outputPath),
            "--operator", Quote(operatorId ?? string.Empty),
            "--rules", Quote(rulesPath ?? string.Empty));

        var result = await processRunner.RunAsync(
            executionSettings.PythonExecutable,
            arguments,
            TimeSpan.FromSeconds(60),
            null,
            cancellationToken).ConfigureAwait(false);

        if (result.TimedOut)
        {
            return ValidationExecutionResult.Failed("Validation timed out before completion.");
        }

        if (result.ExitCode != 0)
        {
            return ValidationExecutionResult.Failed(Sanitize(result.StandardError, result.StandardOutput));
        }

        if (!File.Exists(outputPath))
        {
            return ValidationExecutionResult.Failed("Validation completed without producing validation_summary.json.");
        }

        var summary = persistenceService.Load(layout);
        if (summary is null)
        {
            return ValidationExecutionResult.Failed("Validation summary could not be loaded after validation completed.");
        }

        return new ValidationExecutionResult(true, null, outputPath, summary);
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
            return "Validation failed without additional details.";
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

        return string.IsNullOrWhiteSpace(sanitized) ? "Validation failed without additional details." : sanitized;
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
