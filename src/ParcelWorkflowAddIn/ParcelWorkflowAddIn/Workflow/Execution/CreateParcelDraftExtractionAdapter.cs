using System.IO;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.WorkflowRules;

namespace ParcelWorkflowAddIn.Workflow.Execution;

public sealed class CreateParcelDraftExtractionAdapter : IWorkflowScriptAdapter
{
    private const string ReviewArtifactPathKey = "review_artifact_path";
    private const string ReviewReportJsonKey = "review_report_json";
    private const string RouteArtifactPathKey = "route_artifact_path";
    private const string DefaultDocumentTypeCatalogPath = @"C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\CreateParcel_doc_types.json";
    private const string TextStructuredExtractionScriptRelativePath = @"src\ProcessingTools\adapters\pdf_text_structured_extraction.py";

    private readonly IProcessRunner processRunner;
    private readonly string documentTypeCatalogPath;

    public CreateParcelDraftExtractionAdapter()
        : this(new ProcessRunner(), DefaultDocumentTypeCatalogPath)
    {
    }

    public CreateParcelDraftExtractionAdapter(IProcessRunner processRunner, string? documentTypeCatalogPath = null)
    {
        this.processRunner = processRunner;
        this.documentTypeCatalogPath = string.IsNullOrWhiteSpace(documentTypeCatalogPath)
            ? DefaultDocumentTypeCatalogPath
            : documentTypeCatalogPath;
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
        var route = ResolveExtractionRoute(context);
        if (route.PrimarySource is null)
        {
            return WorkflowScriptStepExecutionResult.Failed("No computation or points source is available for draft extraction.");
        }

        Directory.CreateDirectory(context.Layout.WorkingDirectory);
        Directory.CreateDirectory(context.Layout.LogsDirectory);

        var generatedConfigPath = Path.Combine(context.Layout.WorkingDirectory, "CreateParcelFromFile_case.ini");
        var reviewArtifactPath = Path.Combine(context.Layout.WorkingDirectory, "extraction_review_data.json");
        var routeArtifactPath = Path.Combine(context.Layout.WorkingDirectory, "extraction_route.json");
        var transactionNumber = manifest.Payload.InnolaTransaction?.TransactionNumber ?? manifest.TransactionId;

        if (route.UnsafeToAutomate)
        {
            WriteRouteArtifact(routeArtifactPath, route, transactionNumber, CreateRuntimeDiagnostics(route.ActiveExtractorId, route.ProviderUsed, route.FallbackReason));
            context.SharedItems[RouteArtifactPathKey] = routeArtifactPath;
            return WorkflowScriptStepExecutionResult.Failed(route.OperatorMessage ?? "No supported extraction route is available for the selected source package.");
        }

        context.SharedItems[RouteArtifactPathKey] = routeArtifactPath;

        ExtractionRuntimeDiagnostics? runtimeDiagnostics = null;
        if (ShouldAttemptStructuredTextExtraction(route))
        {
            var textAttempt = await TryExecuteStructuredTextExtractionAsync(
                context,
                route,
                transactionNumber,
                reviewArtifactPath,
                cancellationToken).ConfigureAwait(false);

            if (textAttempt.Outcome == StructuredTextExtractionOutcome.Success)
            {
                route = textAttempt.Route;
                runtimeDiagnostics = textAttempt.Diagnostics;
                context.SharedItems[ReviewArtifactPathKey] = reviewArtifactPath;
                context.SharedItems[ReviewReportJsonKey] = textAttempt.ReportJson;
                EnrichReviewArtifact(reviewArtifactPath, route, runtimeDiagnostics);
                WriteRouteArtifact(routeArtifactPath, route, transactionNumber, runtimeDiagnostics);

                var artifactPaths = new List<string> { reviewArtifactPath, routeArtifactPath };
                using var textReportDocument = JsonDocument.Parse(textAttempt.ReportJson!);
                foreach (var outputArtifact in context.Step.OutputArtifacts)
                {
                    var stepArtifactPath = ResolveArtifactPath(context, outputArtifact);
                    WriteStepArtifactSummary(stepArtifactPath, textReportDocument.RootElement, transactionNumber, route, textAttempt.Elapsed);
                    artifactPaths.Add(stepArtifactPath);
                }

                return WorkflowScriptStepExecutionResult.Passed(artifactPaths.ToArray());
            }

            if (textAttempt.Outcome == StructuredTextExtractionOutcome.FatalFailure)
            {
                WriteRouteArtifact(routeArtifactPath, route, transactionNumber, textAttempt.Diagnostics);
                return WorkflowScriptStepExecutionResult.Failed(textAttempt.ErrorMessage ?? "Structured PDF extraction failed.");
            }

            route = textAttempt.Route;
            runtimeDiagnostics = textAttempt.Diagnostics;
            if (route.UnsafeToAutomate)
            {
                WriteRouteArtifact(routeArtifactPath, route, transactionNumber, runtimeDiagnostics);
                return WorkflowScriptStepExecutionResult.Failed(route.OperatorMessage ?? "No supported extraction route is available for the selected source package.");
            }
        }

        WriteGeneratedConfig(
            generatedConfigPath,
            context,
            transactionNumber,
            route);

        var processEnvironment = BuildProcessEnvironment(context.RuleSettings, route);
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

            CopyFileIfDifferent(reviewJsonPath, reviewArtifactPath);
            runtimeDiagnostics ??= CreateRuntimeDiagnostics(
                route.ActiveExtractorId,
                route.ProviderUsed,
                route.FallbackReason);
            EnrichReviewArtifact(reviewArtifactPath, route, runtimeDiagnostics);
            WriteRouteArtifact(routeArtifactPath, route, transactionNumber, runtimeDiagnostics);
            context.SharedItems[ReviewArtifactPathKey] = reviewArtifactPath;
            context.SharedItems[ReviewReportJsonKey] = result.StandardOutput;

            var artifactPaths = new List<string> { reviewArtifactPath, routeArtifactPath };
            foreach (var outputArtifact in context.Step.OutputArtifacts)
            {
                var stepArtifactPath = ResolveArtifactPath(context, outputArtifact);
                WriteStepArtifactSummary(stepArtifactPath, reportDocument.RootElement, transactionNumber, route, stopwatch.Elapsed);
                artifactPaths.Add(stepArtifactPath);
            }

            return WorkflowScriptStepExecutionResult.Passed(artifactPaths.ToArray());
        }
    }

    private async Task<StructuredTextExtractionAttemptResult> TryExecuteStructuredTextExtractionAsync(
        WorkflowScriptExecutionContext context,
        ResolvedExtractionRoute route,
        string transactionNumber,
        string reviewArtifactPath,
        CancellationToken cancellationToken)
    {
        if (route.PrimarySource is null)
        {
            return StructuredTextExtractionAttemptResult.Fatal("No primary computation PDF is available for structured text extraction.");
        }

        var scriptPath = ResolveTextStructuredExtractionScriptPath(context.ExecutionSettings);
        Debug.WriteLine(
            $"Innola structured text extraction script resolution. TransactionNumber={transactionNumber}; ResolvedPath={scriptPath ?? "(missing)"}; OutputAdapter={context.ExecutionSettings.OutputAdapterScriptPath}; ValidationAdapter={context.ExecutionSettings.ValidationAdapterScriptPath}.");
        if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
        {
            var missingScriptReason = "text_parser_script_missing";
            return StructuredTextExtractionAttemptResult.FallbackRequested(
                ApplyRuntimeFallback(route, missingScriptReason),
                CreateRuntimeDiagnostics(
                    "pdf_text_structured_computation",
                    route.ProviderUsed,
                    missingScriptReason,
                    textLayerProbeStatus: "script_missing",
                    textLayerAvailable: null));
        }

        var stopwatch = Stopwatch.StartNew();
        var processResult = await processRunner.RunAsync(
            context.ExecutionSettings.PythonExecutable,
            BuildTextStructuredScriptArguments(scriptPath, route.PrimarySource.CopiedPath, reviewArtifactPath, transactionNumber),
            TimeSpan.FromSeconds(Math.Max(30, context.Step.TimeoutSeconds)),
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        if (processResult.TimedOut)
        {
            var timeoutReason = "text_parser_timeout";
            return StructuredTextExtractionAttemptResult.FallbackRequested(
                ApplyRuntimeFallback(route, timeoutReason),
                CreateRuntimeDiagnostics(
                    "pdf_text_structured_computation",
                    route.ProviderUsed,
                    timeoutReason,
                    textLayerProbeStatus: "timed_out",
                    textLayerAvailable: null),
                elapsed: stopwatch.Elapsed);
        }

        if (!TryParseStructuredTextEnvelope(processResult.StandardOutput, out var envelope))
        {
            var parseReason = processResult.ExitCode == 0 ? "text_parser_malformed_output" : "text_parser_error";
            return StructuredTextExtractionAttemptResult.FallbackRequested(
                ApplyRuntimeFallback(route, parseReason),
                CreateRuntimeDiagnostics(
                    "pdf_text_structured_computation",
                    route.ProviderUsed,
                    parseReason,
                    textLayerProbeStatus: "malformed_output",
                    textLayerAvailable: null),
                elapsed: stopwatch.Elapsed);
        }

        if (string.Equals(envelope.Status, "success", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(envelope.ReviewJsonPath)
            && File.Exists(envelope.ReviewJsonPath))
        {
            CopyFileIfDifferent(envelope.ReviewJsonPath, reviewArtifactPath);
            return StructuredTextExtractionAttemptResult.Success(
                ApplyStructuredTextSuccess(route),
                CreateRuntimeDiagnostics(
                    "pdf_text_structured_computation",
                    "pdf_text_structured_computation",
                    fallbackReason: null,
                    textLayerProbeStatus: envelope.ParserStatus ?? "parsed",
                    textLayerAvailable: envelope.TextLayerAvailable,
                    parsedParcelCount: envelope.ParsedParcelCount,
                    parsedRowCount: envelope.ParsedRowCount),
                processResult.StandardOutput,
                stopwatch.Elapsed);
        }

        var fallbackReason = envelope.FallbackReason
                             ?? envelope.ParserStatus
                             ?? "text_first_fallback_requested";
        return StructuredTextExtractionAttemptResult.FallbackRequested(
            ApplyRuntimeFallback(route, fallbackReason),
            CreateRuntimeDiagnostics(
                "pdf_text_structured_computation",
                route.ProviderUsed,
                fallbackReason,
                textLayerProbeStatus: envelope.ParserStatus ?? "fallback_requested",
                textLayerAvailable: envelope.TextLayerAvailable,
                parsedParcelCount: envelope.ParsedParcelCount,
                parsedRowCount: envelope.ParsedRowCount),
            elapsed: stopwatch.Elapsed);
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
        ResolvedExtractionRoute route)
    {
        var openAiEnabled = route.AiUsed;
        var outputGdbName = $"{transactionNumber}_submission_work.gdb";
        var effectiveOpenAiModel = ResolveEffectiveOpenAiModel(context.RuleSettings);
        var builder = new StringBuilder();
        builder.AppendLine("[paths]");
        builder.AppendLine($"scanned_images_dir = {context.Layout.SourceDirectory}");
        builder.AppendLine($"results_dir = {context.Layout.WorkingDirectory}");
        builder.AppendLine($"logs_dir = {context.Layout.LogsDirectory}");
        builder.AppendLine();
        builder.AppendLine("[source_files]");
        builder.AppendLine($"dwg_file = {PathOrNone(route.DwgSource)}");
        builder.AppendLine($"points_file = {Path.GetFileName(route.PrimarySource!.CopiedPath)}");
        builder.AppendLine($"plot_pdf_file = {PathOrEmpty(route.PlanSource)}");
        builder.AppendLine($"primary_source_role = {route.PrimarySourceRole}");
        builder.AppendLine($"primary_source_file = {route.PrimarySourceFile}");
        builder.AppendLine($"secondary_source_files = {string.Join("|", route.SecondarySources.Select(source => Path.GetFileName(source.CopiedPath)))}");
        builder.AppendLine();
        builder.AppendLine("[arcgis]");
        builder.AppendLine("coordinate_system = JAD 2001 Jamaica Grid");
        builder.AppendLine("output_epsg = 3448");
        builder.AppendLine();
        builder.AppendLine("[processing]");
        builder.AppendLine($"use_openai = {(openAiEnabled ? "yes" : "no")}");
        builder.AppendLine($"case1_extraction_mode = {route.CaseExtractionMode}");
        builder.AppendLine("case1_openai_max_pages = 8");
        builder.AppendLine("case1_openai_retries_per_page = 3");
        builder.AppendLine("case1_expected_parcel_count = 0");
        builder.AppendLine("case1_expected_min_segment_rows = 0");
        builder.AppendLine();
        builder.AppendLine("[document_types]");
        builder.AppendLine($"catalog_json = {route.DocumentTypeMatch.CatalogPath}");
        builder.AppendLine($"matched_doc_type_id = {route.DocumentTypeMatch.Definition.DocTypeId}");
        builder.AppendLine($"matched_doc_type_name = {route.DocumentTypeMatch.Definition.Name}");
        builder.AppendLine($"matched_doc_type_match_mode = {route.DocumentTypeMatch.MatchMode}");
        builder.AppendLine($"matched_doc_type_family = {route.DocumentTypeMatch.Definition.Family}");
        builder.AppendLine($"matched_extractor_id = {route.DocumentTypeMatch.Definition.Extraction.ExtractorId}");
        builder.AppendLine($"matched_active_extractor_id = {route.ActiveExtractorId}");
        builder.AppendLine($"matched_fallback_extractors = {string.Join("|", route.FallbackExtractors)}");
        builder.AppendLine($"matched_geometry_mode = {route.DocumentTypeMatch.Definition.Geometry.GeometryMode}");
        builder.AppendLine($"matched_validation_profile = {route.DocumentTypeMatch.Definition.Validation.ValidationProfile}");
        builder.AppendLine($"matched_review_mode = {route.DocumentTypeMatch.Definition.Review.ReviewMode}");
        builder.AppendLine($"matched_confidence = {route.DocumentTypeMatch.MatchConfidence:0.###}");
        builder.AppendLine($"ai_requested = {BoolToIni(route.AiRequested)}");
        builder.AppendLine($"ai_available = {BoolToIni(route.AiAvailable)}");
        builder.AppendLine($"ai_used = {BoolToIni(route.AiUsed)}");
        builder.AppendLine($"openai_profile = {context.RuleSettings.OpenAiExtractionProfile}");
        builder.AppendLine($"provider_used = {route.ProviderUsed}");
        builder.AppendLine($"fallback_reason = {route.FallbackReason ?? string.Empty}");
        builder.AppendLine();
        builder.AppendLine("[openai]");
        builder.AppendLine("api_key = ");
        builder.AppendLine("api_base = https://api.openai.com/v1");
        builder.AppendLine($"model = {effectiveOpenAiModel}");
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

    private static IReadOnlyDictionary<string, string?>? BuildProcessEnvironment(WorkflowRuleSettings ruleSettings, ResolvedExtractionRoute route)
    {
        if (!route.AiUsed || !ruleSettings.OpenAiEnabled)
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
        ResolvedExtractionRoute route,
        TimeSpan elapsed)
    {
        var summary = new Dictionary<string, object?>
        {
            ["transaction_number"] = transactionNumber,
            ["points_source"] = Path.GetFileName(route.PrimarySource!.CopiedPath),
            ["plan_source"] = route.PlanSource is null ? null : Path.GetFileName(route.PlanSource.CopiedPath),
            ["row_count"] = ReadInt(reportRoot, "row_count"),
            ["segment_row_count"] = ReadInt(reportRoot, "segment_row_count"),
            ["extraction_source"] = ReadString(reportRoot, "extraction_source"),
            ["doc_type_id"] = route.DocumentTypeMatch.Definition.DocTypeId,
            ["active_extractor_id"] = route.ActiveExtractorId,
            ["provider_used"] = route.ProviderUsed,
            ["elapsed_ms"] = (long)elapsed.TotalMilliseconds,
            ["errors"] = ReadArray(reportRoot, "errors")
        };

        File.WriteAllText(outputPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
    }

    private ResolvedExtractionRoute ResolveExtractionRoute(WorkflowScriptExecutionContext context)
    {
        var catalog = new DocumentTypeCatalogLoader(documentTypeCatalogPath).Load();
        var sourceFiles = context.Manifest.Payload.SourceFiles;
        var candidates = sourceFiles
            .Where(source => !string.Equals(source.SourceRole, "dwg_reference", StringComparison.OrdinalIgnoreCase))
            .Select(source => new SourceRouteCandidate(
                source,
                catalog.ResolveBestMatch(new DocumentTypeMatchCandidate(
                    source.SourceRole ?? "unknown_source",
                    Path.GetFileName(source.CopiedPath),
                    source.FileType))))
            .ToArray();

        var structuredCandidate = candidates
            .Where(candidate => string.Equals(candidate.Match.Definition.Family, "structured_points", StringComparison.OrdinalIgnoreCase)
                                && !candidate.Match.LowConfidence
                                && candidate.Match.MatchScore >= candidate.Match.ScoreThreshold)
            .OrderBy(candidate => GetSourcePriority(candidate.Source, context.Step.InputRoles))
            .ThenByDescending(candidate => candidate.Match.MatchScore)
            .ThenByDescending(candidate => candidate.Match.Definition.Priority)
            .FirstOrDefault();

        var selected = structuredCandidate
            ?? candidates
                .OrderBy(candidate => GetSourcePriority(candidate.Source, context.Step.InputRoles))
                .ThenByDescending(candidate => candidate.Match.MatchScore)
                .ThenByDescending(candidate => candidate.Match.Definition.Priority)
                .FirstOrDefault();

        var primarySource = selected?.Source ?? ResolvePointsSource(sourceFiles, context.Step.InputRoles);
        var primaryMatch = selected?.Match
            ?? catalog.ResolveBestMatch(new DocumentTypeMatchCandidate(
                primarySource?.SourceRole ?? "unknown_source",
                primarySource is null ? string.Empty : Path.GetFileName(primarySource.CopiedPath),
                primarySource?.FileType ?? string.Empty));

        var planSource = ResolveFirstSource(sourceFiles, "plan_map_reference");
        var dwgSource = ResolveFirstSource(sourceFiles, "dwg_reference");
        var secondarySources = sourceFiles
            .Where(source => primarySource is null || !string.Equals(source.CopiedPath, primarySource.CopiedPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var fallbackExtractors = primaryMatch.Definition.Extraction.FallbackExtractors
            .Where(extractor => !string.IsNullOrWhiteSpace(extractor))
            .Select(extractor => extractor.Trim())
            .ToArray();
        var aiRequested = AllowsAiFallback(primaryMatch);
        var aiAvailable = aiRequested && context.Step.OpenAiEnabled && context.RuleSettings.OpenAiEnabled && HasConfiguredOpenAiApiKey(context.RuleSettings);
        var activeExtractorId = ResolveActiveExtractorId(primaryMatch, aiAvailable);
        var aiUsed = aiRequested && aiAvailable && ExtractorRequiresAi(activeExtractorId);
        var providerUsed = aiUsed ? "openai" : activeExtractorId;
        var fallbackReason = ResolveFallbackReason(primaryMatch, aiRequested, aiAvailable, activeExtractorId);
        var unsafeToAutomate = primaryMatch.LowConfidence
            || string.Equals(primaryMatch.Definition.Family, "unknown", StringComparison.OrdinalIgnoreCase)
            || string.Equals(activeExtractorId, "manual_only_source", StringComparison.OrdinalIgnoreCase);

        return new ResolvedExtractionRoute(
            primarySource,
            planSource,
            dwgSource,
            secondarySources,
            primaryMatch,
            activeExtractorId,
            fallbackExtractors,
            aiRequested,
            aiAvailable,
            aiUsed,
            providerUsed,
            fallbackReason,
            primarySource?.SourceRole ?? primaryMatch.CandidateRole,
            primarySource is null ? string.Empty : Path.GetFileName(primarySource.CopiedPath),
            ResolveCaseExtractionMode(activeExtractorId),
            unsafeToAutomate,
            ResolveOperatorMessage(primaryMatch, activeExtractorId));
    }

    private static void EnrichReviewArtifact(string reviewArtifactPath, ResolvedExtractionRoute route, ExtractionRuntimeDiagnostics? diagnostics)
    {
        var rootNode = JsonNode.Parse(File.ReadAllText(reviewArtifactPath)) as JsonObject;
        if (rootNode is null)
        {
            return;
        }

        rootNode["doc_type_id"] = string.IsNullOrWhiteSpace(route.DocumentTypeMatch.Definition.DocTypeId) ? null : route.DocumentTypeMatch.Definition.DocTypeId;
        rootNode["doc_type_name"] = string.IsNullOrWhiteSpace(route.DocumentTypeMatch.Definition.Name) ? null : route.DocumentTypeMatch.Definition.Name;
        rootNode["doc_type_family"] = string.IsNullOrWhiteSpace(route.DocumentTypeMatch.Definition.Family) ? null : route.DocumentTypeMatch.Definition.Family;
        rootNode["doc_type_catalog_path"] = string.IsNullOrWhiteSpace(route.DocumentTypeMatch.CatalogPath) ? null : route.DocumentTypeMatch.CatalogPath;
        rootNode["doc_type_match_mode"] = string.IsNullOrWhiteSpace(route.DocumentTypeMatch.MatchMode) ? null : route.DocumentTypeMatch.MatchMode;
        rootNode["match_confidence"] = route.DocumentTypeMatch.MatchConfidence;
        rootNode["match_score"] = route.DocumentTypeMatch.MatchScore;
        rootNode["match_score_threshold"] = route.DocumentTypeMatch.ScoreThreshold;
        rootNode["match_low_confidence"] = route.DocumentTypeMatch.LowConfidence;
        rootNode["extractor_id"] = string.IsNullOrWhiteSpace(route.DocumentTypeMatch.Definition.Extraction.ExtractorId) ? null : route.DocumentTypeMatch.Definition.Extraction.ExtractorId;
        rootNode["active_extractor_id"] = string.IsNullOrWhiteSpace(route.ActiveExtractorId) ? null : route.ActiveExtractorId;
        rootNode["fallback_extractor_ids"] = new JsonArray(route.FallbackExtractors.Select(extractor => JsonValue.Create(extractor)).ToArray());
        rootNode["geometry_mode"] = string.IsNullOrWhiteSpace(route.DocumentTypeMatch.Definition.Geometry.GeometryMode) ? null : route.DocumentTypeMatch.Definition.Geometry.GeometryMode;
        rootNode["validation_profile"] = string.IsNullOrWhiteSpace(route.DocumentTypeMatch.Definition.Validation.ValidationProfile) ? null : route.DocumentTypeMatch.Definition.Validation.ValidationProfile;
        rootNode["review_mode"] = string.IsNullOrWhiteSpace(route.DocumentTypeMatch.Definition.Review.ReviewMode) ? null : route.DocumentTypeMatch.Definition.Review.ReviewMode;
        rootNode["primary_source_role"] = string.IsNullOrWhiteSpace(route.PrimarySourceRole) ? null : route.PrimarySourceRole;
        rootNode["primary_source_file"] = string.IsNullOrWhiteSpace(route.PrimarySourceFile) ? null : route.PrimarySourceFile;
        rootNode["secondary_source_files"] = new JsonArray(route.SecondarySources.Select(source => JsonValue.Create(Path.GetFileName(source.CopiedPath))).ToArray());
        rootNode["secondary_source_roles"] = new JsonArray(route.SecondarySources.Select(source => JsonValue.Create(source.SourceRole ?? string.Empty)).ToArray());
        rootNode["ai_requested"] = route.AiRequested;
        rootNode["ai_available"] = route.AiAvailable;
        rootNode["ai_used"] = route.AiUsed;
        rootNode["provider_used"] = route.ProviderUsed;
        rootNode["fallback_reason"] = route.FallbackReason;
        rootNode["extraction_method"] = diagnostics?.ExtractionMethod ?? route.ActiveExtractorId;
        rootNode["text_layer_probe_status"] = diagnostics?.TextLayerProbeStatus;
        rootNode["text_layer_available"] = diagnostics?.TextLayerAvailable;
        rootNode["parsed_parcel_count"] = diagnostics?.ParsedParcelCount;
        rootNode["parsed_row_count"] = diagnostics?.ParsedRowCount;
        rootNode["routing_contract_version"] = "2.16b";
        rootNode["routing_case_extraction_mode"] = route.CaseExtractionMode;
        ApplyGroupingMetadata(rootNode, route);

        File.WriteAllText(reviewArtifactPath, rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void ApplyGroupingMetadata(JsonObject rootNode, ResolvedExtractionRoute route)
    {
        if (rootNode["rows"] is not JsonArray rows || rows.Count == 0)
        {
            return;
        }

        var geometry = route.DocumentTypeMatch.Definition.Geometry;
        var hasExplicitGrouping = false;
        var hasBoundaryBreaks = false;
        var currentGroup = string.Empty;
        var nextGroupNumber = 1;
        var sequenceInGroup = 0;

        foreach (var rowNode in rows.OfType<JsonObject>())
        {
            var existingGroup = ReadNodeString(rowNode, "parcel_group_id") ?? ReadNodeString(rowNode, "traverse_id");
            var boundaryBreak = ReadNodeBool(rowNode, "is_boundary_break");
            hasBoundaryBreaks |= boundaryBreak;

            if (!string.IsNullOrWhiteSpace(existingGroup))
            {
                hasExplicitGrouping = true;
                if (!string.Equals(currentGroup, existingGroup, StringComparison.OrdinalIgnoreCase))
                {
                    currentGroup = existingGroup;
                    sequenceInGroup = 0;
                }
            }
            else if (string.IsNullOrWhiteSpace(currentGroup))
            {
                currentGroup = $"parcel-{nextGroupNumber:000}";
                nextGroupNumber++;
                sequenceInGroup = 0;
            }

            sequenceInGroup++;
            rowNode["parcel_group_id"] = string.IsNullOrWhiteSpace(existingGroup) ? currentGroup : existingGroup;
            rowNode["traverse_id"] ??= rowNode["parcel_group_id"]?.DeepClone();
            rowNode["sequence_in_group"] ??= sequenceInGroup;
            rowNode["is_boundary_break"] = boundaryBreak;
            rowNode["group_confidence"] ??= hasExplicitGrouping || hasBoundaryBreaks ? "preserved" : "inferred";
            rowNode["review_parcel_group_id"] ??= rowNode["parcel_group_id"]?.DeepClone();
            rowNode["review_traverse_id"] ??= rowNode["traverse_id"]?.DeepClone();
            rowNode["review_sequence_in_group"] ??= rowNode["sequence_in_group"]?.DeepClone();
            rowNode["review_is_boundary_break"] ??= rowNode["is_boundary_break"]?.DeepClone();
            rowNode["review_group_confidence"] ??= rowNode["group_confidence"]?.DeepClone();

            if (boundaryBreak && geometry.SupportsBoundaryBreaks)
            {
                currentGroup = string.Empty;
                sequenceInGroup = 0;
            }
        }

        rootNode["grouping_status"] = hasExplicitGrouping
            ? "preserved"
            : geometry.RequiresGrouping && rows.Count > 1
                ? "inferred_single_group"
                : "single_group";
        rootNode["grouping_requires_review"] = geometry.RequiresGrouping && !hasExplicitGrouping && !hasBoundaryBreaks && rows.Count > 1;
        rootNode["supports_multi_parcel"] = geometry.SupportsMultiParcel;
        rootNode["supports_boundary_breaks"] = geometry.SupportsBoundaryBreaks;
        rootNode["requires_grouping"] = geometry.RequiresGrouping;
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

    private static void WriteRouteArtifact(string routeArtifactPath, ResolvedExtractionRoute route, string transactionNumber, ExtractionRuntimeDiagnostics? diagnostics)
    {
        var payload = new Dictionary<string, object?>
        {
            ["schema_version"] = "2.16b",
            ["transaction_number"] = transactionNumber,
            ["doc_type_id"] = route.DocumentTypeMatch.Definition.DocTypeId,
            ["doc_type_name"] = route.DocumentTypeMatch.Definition.Name,
            ["doc_type_family"] = route.DocumentTypeMatch.Definition.Family,
            ["doc_type_match_mode"] = route.DocumentTypeMatch.MatchMode,
            ["match_confidence"] = route.DocumentTypeMatch.MatchConfidence,
            ["match_score"] = route.DocumentTypeMatch.MatchScore,
            ["match_score_threshold"] = route.DocumentTypeMatch.ScoreThreshold,
            ["match_low_confidence"] = route.DocumentTypeMatch.LowConfidence,
            ["primary_source_role"] = route.PrimarySourceRole,
            ["primary_source_file"] = route.PrimarySourceFile,
            ["secondary_sources"] = route.SecondarySources
                .Select(source => new Dictionary<string, object?>
                {
                    ["source_role"] = source.SourceRole,
                    ["file_name"] = Path.GetFileName(source.CopiedPath),
                    ["file_type"] = source.FileType
                })
                .ToArray(),
            ["extractor_id"] = route.DocumentTypeMatch.Definition.Extraction.ExtractorId,
            ["active_extractor_id"] = route.ActiveExtractorId,
            ["fallback_extractor_ids"] = route.FallbackExtractors.ToArray(),
            ["geometry_mode"] = route.DocumentTypeMatch.Definition.Geometry.GeometryMode,
            ["validation_profile"] = route.DocumentTypeMatch.Definition.Validation.ValidationProfile,
            ["review_mode"] = route.DocumentTypeMatch.Definition.Review.ReviewMode,
            ["ai_requested"] = route.AiRequested,
            ["ai_available"] = route.AiAvailable,
            ["ai_used"] = route.AiUsed,
            ["provider_used"] = route.ProviderUsed,
            ["fallback_reason"] = route.FallbackReason,
            ["extraction_method"] = diagnostics?.ExtractionMethod ?? route.ActiveExtractorId,
            ["text_layer_probe_status"] = diagnostics?.TextLayerProbeStatus,
            ["text_layer_available"] = diagnostics?.TextLayerAvailable,
            ["parsed_parcel_count"] = diagnostics?.ParsedParcelCount,
            ["parsed_row_count"] = diagnostics?.ParsedRowCount,
            ["case_extraction_mode"] = route.CaseExtractionMode,
            ["unsafe_to_automate"] = route.UnsafeToAutomate,
            ["operator_message"] = route.OperatorMessage
        };

        File.WriteAllText(routeArtifactPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
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

    private static string BoolToIni(bool value)
    {
        return value ? "yes" : "no";
    }

    private static bool HasConfiguredOpenAiApiKey(WorkflowRuleSettings ruleSettings)
    {
        var configuredVariable = string.IsNullOrWhiteSpace(ruleSettings.OpenAiApiKeyEnvironmentVariable)
            ? "OPENAI_API_KEY"
            : ruleSettings.OpenAiApiKeyEnvironmentVariable;
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(configuredVariable));
    }

    private static string ResolveEffectiveOpenAiModel(WorkflowRuleSettings ruleSettings)
    {
        var configuredModel = ruleSettings.OpenAiModel?.Trim();
        if (!string.IsNullOrWhiteSpace(configuredModel))
        {
            return configuredModel;
        }

        return ruleSettings.OpenAiExtractionProfile?.Trim().ToLowerInvariant() switch
        {
            "balanced" => "gpt-4.1-mini",
            "high_accuracy" => "gpt-4.1",
            _ => "gpt-4.1-mini"
        };
    }

    private static string ResolveActiveExtractorId(DocumentTypeMatchResult match, bool aiAvailable)
    {
        var configuredExtractor = match.Definition.Extraction.ExtractorId;
        if (string.IsNullOrWhiteSpace(configuredExtractor))
        {
            return "manual_only_source";
        }

        if (!ExtractorRequiresAi(configuredExtractor))
        {
            return configuredExtractor;
        }

        if (aiAvailable)
        {
            return configuredExtractor;
        }

        return match.Definition.Extraction.FallbackExtractors.FirstOrDefault(extractor => !string.IsNullOrWhiteSpace(extractor))
               ?? configuredExtractor
               ?? "manual_only_source";
    }

    private static string? ResolveFallbackReason(DocumentTypeMatchResult match, bool aiRequested, bool aiAvailable, string activeExtractorId)
    {
        if (match.LowConfidence)
        {
            return "low_confidence_doc_type_match";
        }

        if (string.Equals(match.Definition.Family, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            return "unsupported_document_family";
        }

        if (aiRequested && !aiAvailable && !string.Equals(activeExtractorId, match.Definition.Extraction.ExtractorId, StringComparison.OrdinalIgnoreCase))
        {
            return "ai_disabled_or_unavailable";
        }

        return null;
    }

    private static string ResolveOperatorMessage(DocumentTypeMatchResult match, string activeExtractorId)
    {
        if (match.LowConfidence)
        {
            return "Document classification is low confidence. Review the matched source and update the document-type catalog before automated extraction.";
        }

        if (string.Equals(match.Definition.Family, "unknown", StringComparison.OrdinalIgnoreCase)
            || string.Equals(activeExtractorId, "manual_only_source", StringComparison.OrdinalIgnoreCase))
        {
            return "No supported document family matched this source package. Update the document-type catalog or handle the case manually.";
        }

        return string.Empty;
    }

    private static string ResolveCaseExtractionMode(string activeExtractorId)
    {
        return activeExtractorId switch
        {
            "pdf_text_structured_computation" => "text_structured_pdf",
            "openai_table_pdf" => "openai_table",
            "structured_csv_points" or "structured_txt_points" => "structured_points",
            "ocr_table_pdf" or "text_regex_pdf" => "local",
            _ => "manual"
        };
    }

    private static ResolvedExtractionRoute ApplyStructuredTextSuccess(ResolvedExtractionRoute route)
    {
        var extractorId = "pdf_text_structured_computation";
        var unsafeToAutomate = route.DocumentTypeMatch.LowConfidence
            || string.Equals(route.DocumentTypeMatch.Definition.Family, "unknown", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extractorId, "manual_only_source", StringComparison.OrdinalIgnoreCase);

        return route with
        {
            ActiveExtractorId = extractorId,
            AiUsed = false,
            ProviderUsed = extractorId,
            FallbackReason = null,
            CaseExtractionMode = ResolveCaseExtractionMode(extractorId),
            UnsafeToAutomate = unsafeToAutomate,
            OperatorMessage = ResolveOperatorMessage(route.DocumentTypeMatch, extractorId)
        };
    }

    private static bool ExtractorRequiresAi(string extractorId)
    {
        return string.Equals(extractorId, "openai_table_pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool AllowsAiFallback(DocumentTypeMatchResult match)
    {
        return match.Definition.Extraction.AiAssisted
               || ExtractorRequiresAi(match.Definition.Extraction.ExtractorId)
               || match.Definition.Extraction.FallbackExtractors.Any(ExtractorRequiresAi);
    }

    private static bool ShouldAttemptStructuredTextExtraction(ResolvedExtractionRoute route)
    {
        if (route.PrimarySource is null)
        {
            return false;
        }

        if (!string.Equals(Path.GetExtension(route.PrimarySource.CopiedPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(route.ActiveExtractorId, "pdf_text_structured_computation", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return route.DocumentTypeMatch.Definition.Extraction.PrefersTextLayer
               || (string.Equals(route.DocumentTypeMatch.Definition.Family, "computation_sheet", StringComparison.OrdinalIgnoreCase)
                   && string.Equals(route.DocumentTypeMatch.Definition.Extraction.ParserMode, "parcel_block_rows", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryParseStructuredTextEnvelope(string? standardOutput, out StructuredTextExtractionEnvelope envelope)
    {
        envelope = StructuredTextExtractionEnvelope.Empty;
        if (string.IsNullOrWhiteSpace(standardOutput))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(standardOutput);
            var root = document.RootElement;
            envelope = new StructuredTextExtractionEnvelope(
                ReadString(root, "status") ?? string.Empty,
                ReadBoolOrNull(root, "text_layer_available"),
                ReadString(root, "parser_status"),
                ReadString(root, "fallback_reason"),
                ReadIntOrNull(root, "parsed_parcel_count"),
                ReadIntOrNull(root, "parsed_row_count"),
                ResolveReviewJsonPath(root));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string BuildTextStructuredScriptArguments(string scriptPath, string sourcePdfPath, string outputJsonPath, string transactionNumber)
    {
        return $"\"{scriptPath}\" --source-pdf \"{sourcePdfPath}\" --output-json \"{outputJsonPath}\" --transaction-number \"{transactionNumber}\"";
    }

    private static string? ResolveTextStructuredExtractionScriptPath(WorkflowExecutionSettings executionSettings)
    {
        var configuredAdapterCandidates = new[]
        {
            executionSettings.OutputAdapterScriptPath,
            executionSettings.ValidationAdapterScriptPath
        }
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Select(path => Path.GetDirectoryName(path!))
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Select(path => Path.Combine(path!, Path.GetFileName(TextStructuredExtractionScriptRelativePath)))
        .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in configuredAdapterCandidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return ResolveProjectFile(TextStructuredExtractionScriptRelativePath);
    }

    private static string? ResolveProjectFile(string relativePath)
    {
        var searchRoots = new[]
        {
            Path.GetDirectoryName(typeof(CreateParcelDraftExtractionAdapter).Assembly.Location),
            AppContext.BaseDirectory,
            Environment.CurrentDirectory
        }
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var root in searchRoots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var current = new DirectoryInfo(root);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }
        }

        return null;
    }

    private static ExtractionRuntimeDiagnostics CreateRuntimeDiagnostics(
        string extractionMethod,
        string providerUsed,
        string? fallbackReason,
        string? textLayerProbeStatus = null,
        bool? textLayerAvailable = null,
        int? parsedParcelCount = null,
        int? parsedRowCount = null)
    {
        return new ExtractionRuntimeDiagnostics(
            extractionMethod,
            providerUsed,
            fallbackReason,
            textLayerProbeStatus,
            textLayerAvailable,
            parsedParcelCount,
            parsedRowCount);
    }

    private static ResolvedExtractionRoute ApplyRuntimeFallback(ResolvedExtractionRoute route, string fallbackReason)
    {
        var nextExtractor = ResolveRuntimeFallbackExtractor(route);
        var aiUsed = string.Equals(nextExtractor, "openai_table_pdf", StringComparison.OrdinalIgnoreCase) && route.AiAvailable;
        var providerUsed = aiUsed ? "openai" : nextExtractor;
        var unsafeToAutomate = route.DocumentTypeMatch.LowConfidence
            || string.Equals(route.DocumentTypeMatch.Definition.Family, "unknown", StringComparison.OrdinalIgnoreCase)
            || string.Equals(nextExtractor, "manual_only_source", StringComparison.OrdinalIgnoreCase);

        return route with
        {
            ActiveExtractorId = nextExtractor,
            AiUsed = aiUsed,
            ProviderUsed = providerUsed,
            FallbackReason = fallbackReason,
            CaseExtractionMode = ResolveCaseExtractionMode(nextExtractor),
            UnsafeToAutomate = unsafeToAutomate,
            OperatorMessage = ResolveOperatorMessage(route.DocumentTypeMatch, nextExtractor)
        };
    }

    private static string ResolveRuntimeFallbackExtractor(ResolvedExtractionRoute route)
    {
        foreach (var extractor in route.FallbackExtractors)
        {
            if (string.IsNullOrWhiteSpace(extractor))
            {
                continue;
            }

            if (ExtractorRequiresAi(extractor) && !route.AiAvailable)
            {
                continue;
            }

            return extractor;
        }

        return "manual_only_source";
    }

    private static void CopyFileIfDifferent(string sourcePath, string destinationPath)
    {
        var normalizedSource = Path.GetFullPath(sourcePath);
        var normalizedDestination = Path.GetFullPath(destinationPath);
        if (string.Equals(normalizedSource, normalizedDestination, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        File.Copy(normalizedSource, normalizedDestination, overwrite: true);
    }

    private static int GetSourcePriority(ManifestSourceFile source, IReadOnlyList<string> preferredRoles)
    {
        if (!string.IsNullOrWhiteSpace(source.SourceRole)
            && preferredRoles.Any(role => string.Equals(role, source.SourceRole, StringComparison.OrdinalIgnoreCase)))
        {
            return 0;
        }

        return source.SourceRole?.ToLowerInvariant() switch
        {
            "points_computation" => 0,
            "computation_source" => 0,
            "plan_map_reference" => 1,
            "dwg_reference" => 2,
            _ => 3
        };
    }

    private static string? ReadNodeString(JsonObject rowNode, string propertyName)
    {
        var value = rowNode[propertyName];
        return value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
            ? text
            : null;
    }

    private static bool ReadNodeBool(JsonObject rowNode, string propertyName)
    {
        var value = rowNode[propertyName];
        return value is JsonValue jsonValue
            && ((jsonValue.TryGetValue<bool>(out var boolValue) && boolValue)
                || (jsonValue.TryGetValue<string>(out var textValue) && bool.TryParse(textValue, out var parsed) && parsed));
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

    private static int? ReadIntOrNull(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : null;
    }

    private static bool? ReadBoolOrNull(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;
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

    private sealed record SourceRouteCandidate(
        ManifestSourceFile Source,
        DocumentTypeMatchResult Match);

    private sealed record ResolvedExtractionRoute(
        ManifestSourceFile? PrimarySource,
        ManifestSourceFile? PlanSource,
        ManifestSourceFile? DwgSource,
        IReadOnlyList<ManifestSourceFile> SecondarySources,
        DocumentTypeMatchResult DocumentTypeMatch,
        string ActiveExtractorId,
        IReadOnlyList<string> FallbackExtractors,
        bool AiRequested,
        bool AiAvailable,
        bool AiUsed,
        string ProviderUsed,
        string? FallbackReason,
        string PrimarySourceRole,
        string PrimarySourceFile,
        string CaseExtractionMode,
        bool UnsafeToAutomate,
        string OperatorMessage);

    private sealed record ExtractionRuntimeDiagnostics(
        string ExtractionMethod,
        string ProviderUsed,
        string? FallbackReason,
        string? TextLayerProbeStatus,
        bool? TextLayerAvailable,
        int? ParsedParcelCount,
        int? ParsedRowCount);

    private sealed record StructuredTextExtractionEnvelope(
        string Status,
        bool? TextLayerAvailable,
        string? ParserStatus,
        string? FallbackReason,
        int? ParsedParcelCount,
        int? ParsedRowCount,
        string? ReviewJsonPath)
    {
        public static StructuredTextExtractionEnvelope Empty { get; } = new(string.Empty, null, null, null, null, null, null);
    }

    private sealed record StructuredTextExtractionAttemptResult(
        StructuredTextExtractionOutcome Outcome,
        ResolvedExtractionRoute Route,
        ExtractionRuntimeDiagnostics Diagnostics,
        string? ReportJson,
        TimeSpan Elapsed,
        string? FallbackReason,
        string? ErrorMessage)
    {
        public static StructuredTextExtractionAttemptResult Success(
            ResolvedExtractionRoute route,
            ExtractionRuntimeDiagnostics diagnostics,
            string reportJson,
            TimeSpan elapsed)
            => new(StructuredTextExtractionOutcome.Success, route, diagnostics, reportJson, elapsed, null, null);

        public static StructuredTextExtractionAttemptResult FallbackRequested(
            ResolvedExtractionRoute route,
            ExtractionRuntimeDiagnostics diagnostics,
            TimeSpan? elapsed = null)
            => new(StructuredTextExtractionOutcome.FallbackRequested, route, diagnostics, null, elapsed ?? TimeSpan.Zero, diagnostics.FallbackReason, null);

        public static StructuredTextExtractionAttemptResult Fatal(string errorMessage)
            => new(StructuredTextExtractionOutcome.FatalFailure, default!, CreateRuntimeDiagnostics("pdf_text_structured_computation", "pdf_text_structured_computation", "fatal_error"), null, TimeSpan.Zero, null, errorMessage);
    }

    private enum StructuredTextExtractionOutcome
    {
        Success,
        FallbackRequested,
        FatalFailure
    }
}
