using System.IO;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.WorkflowRules;

namespace ParcelWorkflowAddIn.Workflow.Execution;

public sealed class CreateParcelDraftExtractionAdapter : IWorkflowScriptAdapter
{
    private const string ReviewArtifactPathKey = "review_artifact_path";
    private const string ReviewReportJsonKey = "review_report_json";

    private readonly IProcessRunner processRunner;

    public CreateParcelDraftExtractionAdapter()
        : this(new ProcessRunner())
    {
    }

    public CreateParcelDraftExtractionAdapter(IProcessRunner processRunner)
    {
        this.processRunner = processRunner;
    }

    public string AdapterId => "extraction_adapter";

    public async Task<WorkflowScriptStepExecutionResult> ExecuteAsync(WorkflowScriptExecutionContext context, CancellationToken cancellationToken = default)
    {
        return context.Step.Script switch
        {
            "extract_points_from_computation_pdf" or "normalize_points_computation_source"
                => await ExecuteDraftExtractionAsync(context, cancellationToken).ConfigureAwait(false),
            "ocr_plan_map_pdf"
                => CreatePlanOcrArtifact(context),
            "inspect_dwg_reference"
                => CreateDwgContextArtifact(context),
            _
                => WorkflowScriptStepExecutionResult.Failed($"Unsupported workflow script '{context.Step.Script}'.")
        };
    }

    private async Task<WorkflowScriptStepExecutionResult> ExecuteDraftExtractionAsync(WorkflowScriptExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.ExecutionSettings.PythonExecutable) || !File.Exists(context.ExecutionSettings.PythonExecutable))
        {
            return WorkflowScriptStepExecutionResult.Failed("Configured ArcGIS Python executable is not available for extraction.");
        }

        if (string.IsNullOrWhiteSpace(context.ExecutionSettings.CreateParcelScriptPath) || !File.Exists(context.ExecutionSettings.CreateParcelScriptPath))
        {
            return WorkflowScriptStepExecutionResult.Failed("CreateParcelFromFile.py is not available for extraction.");
        }

        var manifest = context.Manifest;
        var pointsSource = ResolvePointsSource(manifest.Payload.SourceFiles, context.Step.InputRoles);
        if (pointsSource is null)
        {
            return WorkflowScriptStepExecutionResult.Failed("No computation or points source is available for draft extraction.");
        }

        var planSource = ResolveFirstSource(manifest.Payload.SourceFiles, "plan_map_reference");
        var dwgSource = ResolveFirstSource(manifest.Payload.SourceFiles, "dwg_reference");
        Directory.CreateDirectory(context.Layout.WorkingDirectory);
        Directory.CreateDirectory(context.Layout.LogsDirectory);

        var generatedConfigPath = Path.Combine(context.Layout.WorkingDirectory, "CreateParcelFromFile_case.ini");
        var reviewArtifactPath = Path.Combine(context.Layout.WorkingDirectory, "extraction_review_data.json");
        var transactionNumber = manifest.Payload.InnolaTransaction?.TransactionNumber ?? manifest.TransactionId;

        WriteGeneratedConfig(
            generatedConfigPath,
            context,
            transactionNumber,
            pointsSource,
            planSource,
            dwgSource);

        var processEnvironment = BuildProcessEnvironment(context.RuleSettings);
        var stopwatch = Stopwatch.StartNew();
        var result = await processRunner.RunAsync(
            context.ExecutionSettings.PythonExecutable,
            BuildScriptArguments(context.ExecutionSettings.CreateParcelScriptPath, generatedConfigPath, transactionNumber),
            TimeSpan.FromSeconds(Math.Max(30, context.Step.TimeoutSeconds)),
            processEnvironment,
            cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        if (result.TimedOut)
        {
            return WorkflowScriptStepExecutionResult.Failed("Draft extraction timed out before review data could be generated.");
        }

        if (result.ExitCode != 0)
        {
            return WorkflowScriptStepExecutionResult.Failed($"Draft extraction failed. {Sanitize(result.StandardError, result.StandardOutput)}");
        }

        JsonDocument? reportDocument = null;
        try
        {
            reportDocument = JsonDocument.Parse(result.StandardOutput);
        }
        catch (JsonException)
        {
            return WorkflowScriptStepExecutionResult.Failed("Draft extraction returned malformed JSON output.");
        }

        using (reportDocument)
        {
            var reviewJsonPath = ResolveReviewJsonPath(reportDocument.RootElement);
            if (string.IsNullOrWhiteSpace(reviewJsonPath) || !File.Exists(reviewJsonPath))
            {
                return WorkflowScriptStepExecutionResult.Failed("Draft extraction finished without producing the review JSON output.");
            }

            File.Copy(reviewJsonPath, reviewArtifactPath, overwrite: true);
            context.SharedItems[ReviewArtifactPathKey] = reviewArtifactPath;
            context.SharedItems[ReviewReportJsonKey] = result.StandardOutput;

            var artifactPaths = new List<string> { reviewArtifactPath };
            foreach (var outputArtifact in context.Step.OutputArtifacts)
            {
                var stepArtifactPath = ResolveArtifactPath(context, outputArtifact);
                WriteStepArtifactSummary(stepArtifactPath, reportDocument.RootElement, transactionNumber, pointsSource, planSource, stopwatch.Elapsed);
                artifactPaths.Add(stepArtifactPath);
            }

            return WorkflowScriptStepExecutionResult.Passed(artifactPaths.ToArray());
        }
    }

    private static WorkflowScriptStepExecutionResult CreatePlanOcrArtifact(WorkflowScriptExecutionContext context)
    {
        var reviewArtifactPath = GetReviewArtifactPath(context);
        if (string.IsNullOrWhiteSpace(reviewArtifactPath) || !File.Exists(reviewArtifactPath))
        {
            return WorkflowScriptStepExecutionResult.Failed("Plan/reference review artifact is unavailable because draft extraction has not run.");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(reviewArtifactPath));
        var root = document.RootElement;
        var transactionNumber = ReadString(root, "transaction_number") ?? context.Manifest.TransactionId;
        var planDoc = ReadString(root, "plan_doc") ?? string.Empty;
        var extractionSource = ReadString(root, "extraction_source") ?? "unknown";
        var rowCount = ReadInt(root, "row_count");
        var outputPath = ResolveArtifactPath(context, context.Step.OutputArtifacts.FirstOrDefault() ?? "working/plan_ocr.json");

        var payload = new Dictionary<string, object?>
        {
            ["transaction_number"] = transactionNumber,
            ["plan_doc"] = planDoc,
            ["extraction_source"] = extractionSource,
            ["row_count"] = rowCount,
            ["status"] = string.IsNullOrWhiteSpace(planDoc) ? "not_available" : "ready_for_review"
        };

        File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        return WorkflowScriptStepExecutionResult.Passed(outputPath);
    }

    private static WorkflowScriptStepExecutionResult CreateDwgContextArtifact(WorkflowScriptExecutionContext context)
    {
        var dwgSource = ResolveFirstSource(context.Manifest.Payload.SourceFiles, "dwg_reference");
        if (dwgSource is null)
        {
            return WorkflowScriptStepExecutionResult.Failed("DWG context could not be generated because no DWG reference was copied to the case folder.");
        }

        var outputPath = ResolveArtifactPath(context, context.Step.OutputArtifacts.FirstOrDefault() ?? "working/dwg_context.json");
        var payload = new Dictionary<string, object?>
        {
            ["transaction_number"] = context.Manifest.Payload.InnolaTransaction?.TransactionNumber ?? context.Manifest.TransactionId,
            ["source_role"] = dwgSource.SourceRole,
            ["file_name"] = Path.GetFileName(dwgSource.CopiedPath),
            ["copied_path"] = dwgSource.CopiedPath,
            ["status"] = File.Exists(dwgSource.CopiedPath) ? "context_available" : "missing"
        };

        File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        return WorkflowScriptStepExecutionResult.Passed(outputPath);
    }

    private static void WriteGeneratedConfig(
        string generatedConfigPath,
        WorkflowScriptExecutionContext context,
        string transactionNumber,
        ManifestSourceFile pointsSource,
        ManifestSourceFile? planSource,
        ManifestSourceFile? dwgSource)
    {
        var openAiEnabled = context.Step.OpenAiEnabled && context.RuleSettings.OpenAiEnabled;
        var outputGdbName = $"{transactionNumber}_submission_work.gdb";
        var builder = new StringBuilder();
        builder.AppendLine("[paths]");
        builder.AppendLine($"scanned_images_dir = {context.Layout.SourceDirectory}");
        builder.AppendLine($"results_dir = {context.Layout.WorkingDirectory}");
        builder.AppendLine($"logs_dir = {context.Layout.LogsDirectory}");
        builder.AppendLine();
        builder.AppendLine("[source_files]");
        builder.AppendLine($"dwg_file = {PathOrNone(dwgSource)}");
        builder.AppendLine($"points_file = {Path.GetFileName(pointsSource.CopiedPath)}");
        builder.AppendLine($"plot_pdf_file = {PathOrEmpty(planSource)}");
        builder.AppendLine();
        builder.AppendLine("[arcgis]");
        builder.AppendLine("coordinate_system = JAD 2001 Jamaica Grid");
        builder.AppendLine("output_epsg = 3448");
        builder.AppendLine();
        builder.AppendLine("[processing]");
        builder.AppendLine($"use_openai = {(openAiEnabled ? "yes" : "no")}");
        builder.AppendLine($"case1_extraction_mode = {(openAiEnabled ? "openai_table" : "local")}");
        builder.AppendLine("case1_openai_max_pages = 8");
        builder.AppendLine("case1_openai_retries_per_page = 3");
        builder.AppendLine("case1_expected_parcel_count = 0");
        builder.AppendLine("case1_expected_min_segment_rows = 0");
        builder.AppendLine();
        builder.AppendLine("[openai]");
        builder.AppendLine("api_key = ");
        builder.AppendLine("api_base = https://api.openai.com/v1");
        builder.AppendLine($"model = {context.RuleSettings.OpenAiModel}");
        builder.AppendLine();
        builder.AppendLine("[parcel_builder]");
        builder.AppendLine("input_coordinate_system = JAD 2001 Jamaica Grid");
        builder.AppendLine("input_epsg = 3448");
        builder.AppendLine($"transaction_number = {transactionNumber}");
        builder.AppendLine($"output_gdb_name = {outputGdbName}");
        builder.AppendLine("segment_value_decimals = 3");
        builder.AppendLine("reset_output_gdb = yes");
        builder.AppendLine("case1_unique_output_gdb = no");

        File.WriteAllText(generatedConfigPath, builder.ToString());
    }

    private static IReadOnlyDictionary<string, string?>? BuildProcessEnvironment(WorkflowRuleSettings ruleSettings)
    {
        if (!ruleSettings.OpenAiEnabled)
        {
            return null;
        }

        var configuredVariable = string.IsNullOrWhiteSpace(ruleSettings.OpenAiApiKeyEnvironmentVariable)
            ? "OPENAI_API_KEY"
            : ruleSettings.OpenAiApiKeyEnvironmentVariable;
        var configuredValue = Environment.GetEnvironmentVariable(configuredVariable);
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return null;
        }

        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["OPENAI_API_KEY"] = configuredValue
        };
    }

    private static string BuildScriptArguments(string scriptPath, string configPath, string transactionNumber)
    {
        return $"\"{scriptPath}\" --config \"{configPath}\" --review-data --review-case \"{transactionNumber}\"";
    }

    private static void WriteStepArtifactSummary(
        string outputPath,
        JsonElement reportRoot,
        string transactionNumber,
        ManifestSourceFile pointsSource,
        ManifestSourceFile? planSource,
        TimeSpan elapsed)
    {
        var summary = new Dictionary<string, object?>
        {
            ["transaction_number"] = transactionNumber,
            ["points_source"] = Path.GetFileName(pointsSource.CopiedPath),
            ["plan_source"] = planSource is null ? null : Path.GetFileName(planSource.CopiedPath),
            ["row_count"] = ReadInt(reportRoot, "row_count"),
            ["segment_row_count"] = ReadInt(reportRoot, "segment_row_count"),
            ["extraction_source"] = ReadString(reportRoot, "extraction_source"),
            ["elapsed_ms"] = (long)elapsed.TotalMilliseconds,
            ["errors"] = ReadArray(reportRoot, "errors")
        };

        File.WriteAllText(outputPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static ManifestSourceFile? ResolvePointsSource(IReadOnlyList<ManifestSourceFile> sourceFiles, IReadOnlyList<string> preferredRoles)
    {
        foreach (var role in preferredRoles)
        {
            var match = ResolveFirstSource(sourceFiles, role);
            if (match is not null)
            {
                return match;
            }
        }

        return ResolveFirstSource(sourceFiles, "computation_source")
            ?? ResolveFirstSource(sourceFiles, "points_computation");
    }

    private static ManifestSourceFile? ResolveFirstSource(IReadOnlyList<ManifestSourceFile> sourceFiles, string sourceRole)
    {
        return sourceFiles.FirstOrDefault(sourceFile =>
            string.Equals(sourceFile.SourceRole, sourceRole, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveArtifactPath(WorkflowScriptExecutionContext context, string outputArtifact)
    {
        var normalized = outputArtifact.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(context.Layout.RootDirectory, normalized));
    }

    private static string? ResolveReviewJsonPath(JsonElement root)
    {
        if (!root.TryGetProperty("outputs", out var outputs) || outputs.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ReadString(outputs, "review_json");
    }

    private static string GetReviewArtifactPath(WorkflowScriptExecutionContext context)
    {
        if (context.SharedItems.TryGetValue(ReviewArtifactPathKey, out var reviewArtifact)
            && reviewArtifact is string reviewArtifactPath
            && !string.IsNullOrWhiteSpace(reviewArtifactPath))
        {
            return reviewArtifactPath;
        }

        return Path.Combine(context.Layout.WorkingDirectory, "extraction_review_data.json");
    }

    private static string PathOrNone(ManifestSourceFile? sourceFile)
    {
        return sourceFile is null ? "None" : Path.GetFileName(sourceFile.CopiedPath);
    }

    private static string PathOrEmpty(ManifestSourceFile? sourceFile)
    {
        return sourceFile is null ? string.Empty : Path.GetFileName(sourceFile.CopiedPath);
    }

    private static string Sanitize(params string?[] values)
    {
        var joined = string.Join(Environment.NewLine, values.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(joined))
        {
            return "No additional details were returned.";
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

        return string.IsNullOrWhiteSpace(sanitized) ? "No additional details were returned." : sanitized;
    }

    private static bool LooksSensitive(string value)
    {
        return value.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            || value.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || value.Contains("bearer", StringComparison.OrdinalIgnoreCase)
            || value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("token", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int ReadInt(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : 0;
    }

    private static IReadOnlyList<string> ReadArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();
    }
}
