using System.IO;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Intake;
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
    private const string SurveyPlanOcrVisionScriptRelativePath = @"src\ProcessingTools\adapters\survey_plan_ocr_vision_extraction.py";

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
            "extract_single_parcel_survey_plan_pdf"
                => await ExecuteSingleParcelSurveyPlanExtractionAsync(context, cancellationToken).ConfigureAwait(false),
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

        if (string.IsNullOrWhiteSpace(context.ExecutionSettings.CreateParcelScriptPath) || !File.Exists(context.ExecutionSettings.CreateParcelScriptPath))
        {
            return WorkflowScriptStepExecutionResult.Failed("Legacy CreateParcelFromFile.py fallback is not available for extraction. Re-run extraction with a supported text/AI route, or switch to the manual review workspace.");
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

    private async Task<WorkflowScriptStepExecutionResult> ExecuteSingleParcelSurveyPlanExtractionAsync(
        WorkflowScriptExecutionContext context,
        CancellationToken cancellationToken)
    {
        var manifest = context.Manifest;
        var route = ResolveExtractionRoute(context);
        if (route.PrimarySource is null)
        {
            return WorkflowScriptStepExecutionResult.Failed("No survey plan PDF source is available for single-parcel survey-plan extraction.");
        }

        Directory.CreateDirectory(context.Layout.WorkingDirectory);
        Directory.CreateDirectory(context.Layout.LogsDirectory);

        var transactionNumber = manifest.Payload.InnolaTransaction?.TransactionNumber ?? manifest.TransactionId;
        var reviewArtifactPath = Path.Combine(context.Layout.WorkingDirectory, "extraction_review_data.json");
        var routeArtifactPath = Path.Combine(context.Layout.WorkingDirectory, "extraction_route.json");
        var summaryArtifactPath = Path.Combine(context.Layout.WorkingDirectory, "survey_plan_extraction_summary.json");

        var probe = ProbeSurveyPlanSource(route.PrimarySource.CopiedPath);
        if (!probe.TextLayerAvailable)
        {
            var externalAttempt = await TryExecuteSurveyPlanExternalExtractionAsync(
                context,
                route,
                transactionNumber,
                reviewArtifactPath,
                summaryArtifactPath,
                routeArtifactPath,
                probe,
                cancellationToken).ConfigureAwait(false);
            if (externalAttempt is not null)
            {
                return externalAttempt;
            }
        }

        var extraction = ExtractSurveyPlanCandidate(route.PrimarySource, transactionNumber, probe);
        WriteSurveyPlanReviewArtifact(reviewArtifactPath, route, extraction);
        WriteSurveyPlanSummaryArtifact(summaryArtifactPath, route, extraction);

        var diagnostics = CreateRuntimeDiagnostics(
            route.ActiveExtractorId,
            route.ProviderUsed,
            extraction.FallbackReason,
            textLayerProbeStatus: probe.TextLayerProbeStatus,
            textLayerAvailable: probe.TextLayerAvailable,
            parsedParcelCount: extraction.ParcelCountHint,
            parsedRowCount: extraction.Points.Count);
        EnrichReviewArtifact(reviewArtifactPath, route, diagnostics);
        WriteRouteArtifact(routeArtifactPath, route, transactionNumber, diagnostics);

        context.SharedItems[ReviewArtifactPathKey] = reviewArtifactPath;
        context.SharedItems[RouteArtifactPathKey] = routeArtifactPath;
        context.SharedItems[ReviewReportJsonKey] = JsonSerializer.Serialize(new
        {
            status = extraction.Status,
            text_layer_available = probe.TextLayerAvailable,
            parser_status = probe.TextLayerProbeStatus,
            fallback_reason = extraction.FallbackReason,
            parsed_parcel_count = extraction.ParcelCountHint,
            parsed_row_count = extraction.Points.Count,
            outputs = new
            {
                review_json = reviewArtifactPath,
                route_json = routeArtifactPath,
                survey_plan_summary_json = summaryArtifactPath
            }
        });

        var artifactPaths = new List<string> { summaryArtifactPath, reviewArtifactPath, routeArtifactPath };
        foreach (var outputArtifact in context.Step.OutputArtifacts)
        {
            var stepArtifactPath = ResolveArtifactPath(context, outputArtifact);
            if (!artifactPaths.Any(path => string.Equals(Path.GetFullPath(path), Path.GetFullPath(stepArtifactPath), StringComparison.OrdinalIgnoreCase))
                && File.Exists(stepArtifactPath))
            {
                artifactPaths.Add(stepArtifactPath);
            }
        }

        return WorkflowScriptStepExecutionResult.Passed(artifactPaths.ToArray());
    }

    private async Task<WorkflowScriptStepExecutionResult?> TryExecuteSurveyPlanExternalExtractionAsync(
        WorkflowScriptExecutionContext context,
        ResolvedExtractionRoute route,
        string transactionNumber,
        string reviewArtifactPath,
        string summaryArtifactPath,
        string routeArtifactPath,
        SurveyPlanSourceProbe probe,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.ExecutionSettings.PythonExecutable) || !File.Exists(context.ExecutionSettings.PythonExecutable))
        {
            return null;
        }

        var scriptPath = ResolveSurveyPlanOcrVisionScriptPath(context.ExecutionSettings);
        if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
        {
            return null;
        }

        var processEnvironment = BuildProcessEnvironment(context.RuleSettings, route);
        var stopwatch = Stopwatch.StartNew();
        var result = await processRunner.RunAsync(
            context.ExecutionSettings.PythonExecutable,
            BuildSurveyPlanOcrVisionScriptArguments(scriptPath, route.PrimarySource!.CopiedPath, reviewArtifactPath, transactionNumber, context.RuleSettings),
            TimeSpan.FromSeconds(Math.Max(30, context.Step.TimeoutSeconds)),
            processEnvironment,
            cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        if (result.TimedOut)
        {
            return WorkflowScriptStepExecutionResult.Failed("Survey-plan OCR/vision extraction timed out before review data could be generated.");
        }

        if (result.ExitCode != 0)
        {
            return WorkflowScriptStepExecutionResult.Failed($"Survey-plan OCR/vision extraction failed. {Sanitize(result.StandardError, result.StandardOutput)}");
        }

        if (!TryParseStructuredTextEnvelope(result.StandardOutput, out var envelope))
        {
            return WorkflowScriptStepExecutionResult.Failed("Survey-plan OCR/vision extraction returned malformed JSON output.");
        }

        var diagnostics = CreateRuntimeDiagnostics(
            route.ActiveExtractorId,
            route.ProviderUsed,
            envelope.FallbackReason,
            envelope.ParserStatus ?? probe.TextLayerProbeStatus,
            envelope.TextLayerAvailable ?? probe.TextLayerAvailable,
            envelope.ParsedParcelCount,
            envelope.ParsedRowCount);

        var reviewJsonPath = envelope.ReviewJsonPath;
        if (!string.IsNullOrWhiteSpace(reviewJsonPath) && File.Exists(reviewJsonPath))
        {
            CopyFileIfDifferent(reviewJsonPath, reviewArtifactPath);
            EnrichReviewArtifact(reviewArtifactPath, route, diagnostics);
            WriteSurveyPlanSummaryArtifactFromReviewJson(summaryArtifactPath, reviewArtifactPath, route, transactionNumber);
            WriteRouteArtifact(routeArtifactPath, route, transactionNumber, diagnostics);
            context.SharedItems[ReviewArtifactPathKey] = reviewArtifactPath;
            context.SharedItems[RouteArtifactPathKey] = routeArtifactPath;
            context.SharedItems[ReviewReportJsonKey] = result.StandardOutput;

            return WorkflowScriptStepExecutionResult.Passed(CollectSurveyPlanArtifactPaths(context, summaryArtifactPath, reviewArtifactPath, routeArtifactPath));
        }

        return null;
    }

    private static SurveyPlanSourceProbe ProbeSurveyPlanSource(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        var isPdf = string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
        var text = TryReadTextPayload(sourcePath);
        var looksLikeRawPdfContainer = isPdf
                                       && text.TrimStart().StartsWith("%PDF", StringComparison.OrdinalIgnoreCase);
        var hasEmbeddedText = !looksLikeRawPdfContainer
                              && !string.IsNullOrWhiteSpace(text)
                              && Regex.IsMatch(text, "[A-Za-z]{3,}", RegexOptions.CultureInvariant);

        return new SurveyPlanSourceProbe(
            isPdf,
            hasEmbeddedText,
            hasEmbeddedText ? "embedded_text_probe_available" : "no_embedded_text_layer_or_unreadable_image_pdf",
            hasEmbeddedText ? text : string.Empty);
    }

    private static SurveyPlanExtractionCandidate ExtractSurveyPlanCandidate(
        ManifestSourceFile source,
        string transactionNumber,
        SurveyPlanSourceProbe probe)
    {
        var text = probe.TextContent ?? string.Empty;
        var metadata = new Dictionary<string, SurveyPlanField>(StringComparer.OrdinalIgnoreCase)
        {
            ["coordinate_system"] = Field("coordinate_system", ContainsToken(text, "JAD 2001") ? "JAD 2001" : FindFirst(text, @"\bJAD\s*2001\b"), ContainsToken(text, "JAD 2001") ? 0.95 : 0.0, "plan_header", "Coordinate system was inferred from visible plan text."),
            ["parish"] = Field("parish", FindFirst(text, @"Parish\s*[:\-]?\s*(?<value>[A-Za-z .'-]+)"), 0.85, "memorandum", "Parish captured from plan text."),
            ["document_area"] = Field("document_area", FindFirst(text, @"Area\s*[:\-]?\s*(?<value>[0-9,]+(?:\.[0-9]+)?\s*(?:sq\.?\s*m(?:etres|eters)?|m2|square\s+metres?))"), 0.85, "memorandum", "Area captured from document."),
            ["survey_date"] = Field("survey_date", FindFirst(text, @"(?:Survey\s+date|Date\s+surveyed|Surveyed\s+on|Date)\s*[:\-]?\s*(?<value>[A-Za-z]+\s+\d{1,2},?\s+\d{4}|\d{1,2}[/-]\d{1,2}[/-]\d{2,4})"), 0.75, "signature_block", "Survey date captured from plan text."),
            ["instrument"] = Field("instrument", FindFirst(text, @"Instrument\s*[:\-]?\s*(?<value>[A-Za-z0-9 #.\-_/]+)"), 0.75, "instrument_block", "Instrument make/no. captured from plan text."),
            ["surveyed_by"] = Field("surveyed_by", FindFirst(text, @"Surveyed\s+by\s*[:\-]?\s*(?<value>[A-Za-z .'-]+)"), 0.8, "signature_block", "Surveyor captured from plan text.")
        };

        var northArrowDetected = ContainsToken(text, "North Arrow")
                                 || ContainsToken(text, "Grid North")
                                 || Regex.IsMatch(text, @"\bN\s*(?:arrow|orth)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var northArrow = new SurveyPlanDetectedFeature(
            "north_arrow",
            northArrowDetected,
            northArrowDetected ? "map_sketch" : null,
            northArrowDetected ? 0.8 : 0.0,
            northArrowDetected ? "North arrow/grid north evidence detected." : "North arrow was not confidently detected.");

        var points = ExtractSurveyPlanPoints(text);
        var segments = ExtractSurveyPlanSegments(text);
        var parties = ExtractNamedList(text, @"(?:Owner|Owners|Party|Parties)\s*[:\-]?\s*(?<value>[^\r\n]+)");
        var representatives = ExtractNamedList(text, @"(?:Representative|Representatives)\s*[:\-]?\s*(?<value>[^\r\n]+)");
        var adjacentOwners = ExtractNamedList(text, @"(?:Adjacent\s+Owners?|Adjoining\s+Owners?|Adjoining)\s*[:\-]?\s*(?<value>[^\r\n]+)");

        var warnings = new List<string>();
        if (!probe.TextLayerAvailable)
        {
            warnings.Add("PDF embedded text was unavailable; OCR/vision extraction is required for production parsing.");
        }

        if (points.Count == 0)
        {
            warnings.Add("No coordinate table rows were confidently extracted; manual point review is required.");
        }

        if (segments.Count == 0)
        {
            warnings.Add("No bearing/distance segment rows were confidently extracted; manual line review is required.");
        }

        if (string.IsNullOrWhiteSpace(metadata["coordinate_system"].Value))
        {
            warnings.Add("Coordinate system was not confidently extracted.");
        }

        var status = points.Count > 0 || segments.Count > 0 || metadata.Values.Any(field => !string.IsNullOrWhiteSpace(field.Value))
            ? "review_required"
            : "manual_review_required";
        var fallbackReason = status == "manual_review_required" ? "low_confidence_or_no_ocr_text" : "ocr_vision_review_required";

        return new SurveyPlanExtractionCandidate(
            transactionNumber,
            "scanned_single_parcel_survey_plan_pdf",
            "survey_plan_ocr_vision",
            Path.GetFileName(source.CopiedPath),
            status,
            fallbackReason,
            1,
            metadata,
            northArrow,
            points,
            segments,
            parties,
            representatives,
            adjacentOwners,
            warnings);
    }

    private static IReadOnlyList<SurveyPlanPointCandidate> ExtractSurveyPlanPoints(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<SurveyPlanPointCandidate>();
        }

        var matches = Regex.Matches(
            text,
            @"(?:(?:Point|Pt\.?|P)\s*)?(?<id>[A-Za-z]?\d{1,5})\s+(?:N(?:orth(?:ing)?)?\s*[:=]?\s*)?(?<north>\d{5,7}(?:\.\d+)?)\s+(?:E(?:ast(?:ing)?)?\s*[:=]?\s*)?(?<east>\d{5,7}(?:\.\d+)?)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var rows = new List<SurveyPlanPointCandidate>();
        var sequence = 0;
        foreach (Match match in matches.Cast<Match>().Take(250))
        {
            sequence++;
            rows.Add(new SurveyPlanPointCandidate(
                $"parcel-001-{sequence:000}",
                match.Groups["id"].Value,
                match.Groups["east"].Value,
                match.Groups["north"].Value,
                sequence,
                1,
                "coordinate_table",
                0.72,
                match.Value.Trim()));
        }

        return rows;
    }

    private static IReadOnlyList<SurveyPlanSegmentCandidate> ExtractSurveyPlanSegments(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<SurveyPlanSegmentCandidate>();
        }

        var matches = Regex.Matches(
            text,
            @"(?<bearing>[NS]\s*\d{1,2}\s*[°º]\s*\d{1,2}\s*['’]\s*\d{1,2}(?:\s*[""”])?\s*[EW])\s+(?:Length|Distance)?\s*[:=]?\s*(?<distance>\d{1,5}(?:\.\d+)?)\s*(?:m|metres?|meters?)?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var rows = new List<SurveyPlanSegmentCandidate>();
        var sequence = 0;
        foreach (Match match in matches.Cast<Match>().Take(250))
        {
            sequence++;
            var distance = match.Groups["distance"].Value;
            rows.Add(new SurveyPlanSegmentCandidate(
                $"seg-{sequence:000}",
                NormalizeBearingText(match.Groups["bearing"].Value),
                distance,
                distance,
                sequence,
                1,
                "map_sketch",
                0.68,
                match.Value.Trim()));
        }

        return rows;
    }

    private static IReadOnlyList<SurveyPlanPartyCandidate> ExtractNamedList(string text, string pattern)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<SurveyPlanPartyCandidate>();
        }

        return Regex.Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Cast<Match>()
            .Select(match => match.Groups["value"].Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(value => new SurveyPlanPartyCandidate(value, 0.65, "memorandum"))
            .ToArray();
    }

    private static void WriteSurveyPlanReviewArtifact(
        string reviewArtifactPath,
        ResolvedExtractionRoute route,
        SurveyPlanExtractionCandidate extraction)
    {
        var rows = new JsonArray();
        foreach (var point in extraction.Points)
        {
            rows.Add(new JsonObject
            {
                ["row_id"] = point.RowId,
                ["parcel_group_id"] = "parcel-001",
                ["parcel_name"] = "single parcel",
                ["traverse_id"] = "parcel-001",
                ["sequence_in_group"] = point.Sequence,
                ["point_identifier"] = point.PointId,
                ["point_id"] = point.PointId,
                ["easting"] = point.Easting,
                ["northing"] = point.Northing,
                ["source_page"] = point.SourcePage,
                ["source_zone"] = point.SourceZone,
                ["confidence"] = point.Confidence,
                ["source_evidence"] = point.SourceEvidence,
                ["status"] = point.Confidence >= 0.7 ? "candidate" : "needs_review",
                ["review_unresolved"] = point.Confidence < 0.7,
                ["review_notes"] = point.Confidence < 0.7 ? "Low-confidence survey-plan OCR point candidate." : null,
                ["row_provenance"] = "survey_plan_ocr"
            });
        }

        var segments = new JsonArray();
        foreach (var segment in extraction.Segments)
        {
            segments.Add(new JsonObject
            {
                ["segment_id"] = segment.SegmentId,
                ["parcel_group_id"] = "parcel-001",
                ["sequence_in_group"] = segment.Sequence,
                ["bearing_txt"] = segment.BearingText,
                ["distance_txt"] = segment.DistanceText,
                ["length_txt"] = segment.LengthText,
                ["source_page"] = segment.SourcePage,
                ["source_zone"] = segment.SourceZone,
                ["confidence"] = segment.Confidence,
                ["source_evidence"] = segment.SourceEvidence,
                ["status"] = segment.Confidence >= 0.7 ? "candidate" : "needs_review"
            });
        }

        var root = BuildSurveyPlanRoot(route, extraction);
        root["row_count"] = extraction.Points.Count;
        root["segment_row_count"] = extraction.Segments.Count;
        root["rows"] = rows;
        root["segments"] = segments;
        File.WriteAllText(reviewArtifactPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteSurveyPlanSummaryArtifact(
        string summaryArtifactPath,
        ResolvedExtractionRoute route,
        SurveyPlanExtractionCandidate extraction)
    {
        var root = BuildSurveyPlanRoot(route, extraction);
        root["point_count"] = extraction.Points.Count;
        root["segment_count"] = extraction.Segments.Count;
        root["stage_evidence"] = BuildSurveyPlanStageEvidence(extraction);
        File.WriteAllText(summaryArtifactPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteSurveyPlanSummaryArtifactFromReviewJson(
        string summaryArtifactPath,
        string reviewArtifactPath,
        ResolvedExtractionRoute route,
        string transactionNumber)
    {
        using var reviewDocument = JsonDocument.Parse(File.ReadAllText(reviewArtifactPath));
        var reviewRoot = reviewDocument.RootElement;
        var root = new JsonObject
        {
            ["schema_version"] = "2.18.0",
            ["transaction_number"] = ReadString(reviewRoot, "transaction_number") ?? transactionNumber,
            ["source_profile"] = ReadString(reviewRoot, "source_profile") ?? "scanned_single_parcel_survey_plan_pdf",
            ["parcel_count_hint"] = ReadInt(reviewRoot, "parcel_count"),
            ["extraction_source"] = ReadString(reviewRoot, "extraction_source") ?? route.ActiveExtractorId,
            ["extractor_id"] = ReadString(reviewRoot, "extractor_id") ?? route.DocumentTypeMatch.Definition.Extraction.ExtractorId,
            ["active_extractor_id"] = route.ActiveExtractorId,
            ["provider_used"] = route.ProviderUsed,
            ["primary_source_role"] = route.PrimarySourceRole,
            ["primary_source_file"] = route.PrimarySourceFile,
            ["status"] = ReadString(reviewRoot, "status") ?? "review_required",
            ["fallback_reason"] = ReadString(reviewRoot, "fallback_reason"),
            ["coordinate_system"] = CloneObjectOrDefault(reviewRoot, "coordinate_system"),
            ["north_arrow"] = CloneObjectOrDefault(reviewRoot, "north_arrow"),
            ["survey_metadata"] = CloneObjectOrDefault(reviewRoot, "survey_metadata"),
            ["parties"] = CloneArrayOrEmpty(reviewRoot, "parties"),
            ["representatives"] = CloneArrayOrEmpty(reviewRoot, "representatives"),
            ["adjacent_owners"] = CloneArrayOrEmpty(reviewRoot, "adjacent_owners"),
            ["field_confidence"] = CloneObjectOrDefault(reviewRoot, "field_confidence"),
            ["review_notes"] = CloneArrayOrEmpty(reviewRoot, "review_notes"),
            ["point_count"] = ReadInt(reviewRoot, "row_count"),
            ["segment_count"] = ReadInt(reviewRoot, "segment_row_count")
        };

        root["stage_evidence"] = new JsonObject
        {
            ["structure_check"] = new JsonObject
            {
                ["source_profile"] = root["source_profile"]?.DeepClone(),
                ["pdf_readable"] = true,
                ["extractor_eligible"] = true,
                ["expected_zones"] = new JsonArray("plan_sketch", "coordinate_table", "memorandum", "signature_block")
            },
            ["georeference_check"] = new JsonObject
            {
                ["coordinate_system"] = ReadNestedFieldValue(reviewRoot, "coordinate_system"),
                ["parish"] = ReadNestedFieldValue(reviewRoot, "survey_metadata", "parish"),
                ["coordinate_table_point_count"] = ReadInt(reviewRoot, "row_count")
            },
            ["dimension_check"] = new JsonObject
            {
                ["point_count"] = ReadInt(reviewRoot, "row_count"),
                ["segment_count"] = ReadInt(reviewRoot, "segment_row_count"),
                ["document_area"] = ReadNestedFieldValue(reviewRoot, "survey_metadata", "document_area"),
                ["geometry_candidate_status"] = ReadInt(reviewRoot, "row_count") >= 3 || ReadInt(reviewRoot, "segment_row_count") >= 3 ? "candidate" : "manual_review_required"
            }
        };

        File.WriteAllText(summaryArtifactPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string[] CollectSurveyPlanArtifactPaths(
        WorkflowScriptExecutionContext context,
        string summaryArtifactPath,
        string reviewArtifactPath,
        string routeArtifactPath)
    {
        var artifactPaths = new List<string> { summaryArtifactPath, reviewArtifactPath, routeArtifactPath };
        foreach (var outputArtifact in context.Step.OutputArtifacts)
        {
            var stepArtifactPath = ResolveArtifactPath(context, outputArtifact);
            if (!artifactPaths.Any(path => string.Equals(Path.GetFullPath(path), Path.GetFullPath(stepArtifactPath), StringComparison.OrdinalIgnoreCase))
                && File.Exists(stepArtifactPath))
            {
                artifactPaths.Add(stepArtifactPath);
            }
        }

        return artifactPaths.ToArray();
    }

    private static JsonObject BuildSurveyPlanRoot(ResolvedExtractionRoute route, SurveyPlanExtractionCandidate extraction)
    {
        return new JsonObject
        {
            ["schema_version"] = "2.18.0",
            ["transaction_number"] = extraction.TransactionNumber,
            ["source_profile"] = extraction.SourceProfile,
            ["parcel_count_hint"] = extraction.ParcelCountHint,
            ["extraction_source"] = extraction.ExtractorId,
            ["extractor_id"] = extraction.ExtractorId,
            ["active_extractor_id"] = route.ActiveExtractorId,
            ["provider_used"] = route.ProviderUsed,
            ["primary_source_role"] = route.PrimarySourceRole,
            ["primary_source_file"] = extraction.SourceFileName,
            ["status"] = extraction.Status,
            ["fallback_reason"] = extraction.FallbackReason,
            ["coordinate_system"] = BuildFieldNode(extraction.Metadata["coordinate_system"]),
            ["north_arrow"] = JsonSerializer.SerializeToNode(extraction.NorthArrow)!.AsObject(),
            ["survey_metadata"] = new JsonObject
            {
                ["parish"] = BuildFieldNode(extraction.Metadata["parish"]),
                ["document_area"] = BuildFieldNode(extraction.Metadata["document_area"]),
                ["survey_date"] = BuildFieldNode(extraction.Metadata["survey_date"]),
                ["instrument"] = BuildFieldNode(extraction.Metadata["instrument"]),
                ["surveyed_by"] = BuildFieldNode(extraction.Metadata["surveyed_by"])
            },
            ["parties"] = new JsonArray(extraction.Parties.Select(item => JsonSerializer.SerializeToNode(item)).ToArray()),
            ["representatives"] = new JsonArray(extraction.Representatives.Select(item => JsonSerializer.SerializeToNode(item)).ToArray()),
            ["adjacent_owners"] = new JsonArray(extraction.AdjacentOwners.Select(item => JsonSerializer.SerializeToNode(item)).ToArray()),
            ["field_confidence"] = JsonSerializer.SerializeToNode(extraction.Metadata.ToDictionary(item => item.Key, item => item.Value.Confidence, StringComparer.OrdinalIgnoreCase))!,
            ["review_notes"] = new JsonArray(extraction.Warnings.Select(warning => JsonValue.Create(warning)).ToArray())
        };
    }

    private static JsonObject BuildSurveyPlanStageEvidence(SurveyPlanExtractionCandidate extraction)
    {
        return new JsonObject
        {
            ["structure_check"] = new JsonObject
            {
                ["source_profile"] = extraction.SourceProfile,
                ["pdf_readable"] = true,
                ["extractor_eligible"] = true,
                ["expected_zones"] = new JsonArray("plan_sketch", "coordinate_table", "memorandum", "signature_block")
            },
            ["georeference_check"] = new JsonObject
            {
                ["coordinate_system"] = extraction.Metadata["coordinate_system"].Value,
                ["coordinate_system_confidence"] = extraction.Metadata["coordinate_system"].Confidence,
                ["parish"] = extraction.Metadata["parish"].Value,
                ["parish_confidence"] = extraction.Metadata["parish"].Confidence,
                ["coordinate_table_point_count"] = extraction.Points.Count
            },
            ["dimension_check"] = new JsonObject
            {
                ["point_count"] = extraction.Points.Count,
                ["segment_count"] = extraction.Segments.Count,
                ["document_area"] = extraction.Metadata["document_area"].Value,
                ["geometry_candidate_status"] = extraction.Points.Count >= 3 || extraction.Segments.Count >= 3 ? "candidate" : "manual_review_required"
            }
        };
    }

    private static JsonObject BuildFieldNode(SurveyPlanField field)
    {
        return new JsonObject
        {
            ["field"] = field.FieldName,
            ["value"] = string.IsNullOrWhiteSpace(field.Value) ? null : field.Value,
            ["confidence"] = field.Confidence,
            ["source_page"] = 1,
            ["source_zone"] = field.SourceZone,
            ["status"] = string.IsNullOrWhiteSpace(field.Value) ? "not_extracted" : field.Confidence >= 0.7 ? "candidate" : "needs_review",
            ["review_note"] = field.ReviewNote
        };
    }

    private static SurveyPlanField Field(string fieldName, string? value, double confidence, string sourceZone, string reviewNote)
    {
        var normalized = value?.Trim();
        return new SurveyPlanField(fieldName, string.IsNullOrWhiteSpace(normalized) ? null : normalized, string.IsNullOrWhiteSpace(normalized) ? 0.0 : confidence, sourceZone, string.IsNullOrWhiteSpace(normalized) ? "Field was not confidently extracted." : reviewNote);
    }

    private static string? FindFirst(string text, string pattern)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        return match.Groups["value"].Success
            ? match.Groups["value"].Value.Trim()
            : match.Value.Trim();
    }

    private static bool ContainsToken(string text, string token)
    {
        return !string.IsNullOrWhiteSpace(text)
               && text.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeBearingText(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", string.Empty, RegexOptions.CultureInvariant)
            .Replace('º', '°')
            .Replace('’', '\'')
            .Replace("”", "\"", StringComparison.Ordinal);
    }

    private static string TryReadTextPayload(string sourcePath)
    {
        try
        {
            var bytes = File.ReadAllBytes(sourcePath);
            if (bytes.Length == 0)
            {
                return string.Empty;
            }

            var text = Encoding.UTF8.GetString(bytes);
            return text.Count(char.IsControl) > text.Length / 3
                ? string.Empty
                : text;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return string.Empty;
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
        var dwgSource = ResolveFirstSource(context.Manifest.Payload.SourceFiles, SourceRole.DwgSource);
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
        if ((!route.AiUsed && !string.Equals(route.ActiveExtractorId, "survey_plan_ocr_vision", StringComparison.OrdinalIgnoreCase))
            || !ruleSettings.OpenAiEnabled)
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
            ["OPENAI_API_KEY"] = configuredValue,
            ["OPENAI_MODEL"] = ResolveEffectiveOpenAiModel(ruleSettings),
            ["OPENAI_EXTRACTION_PROFILE"] = ruleSettings.OpenAiExtractionProfile
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
        var sourceFiles = SupportingDocumentSourceFilter.Apply(
            context.Manifest.Payload.SourceFiles,
            context.Manifest.Payload.SupportingDocumentOptions);
        var allSourceFiles = context.Manifest.Payload.SourceFiles;
        var candidates = sourceFiles
            .Where(source => !SourceRole.Matches(source.SourceRole, SourceRole.DwgSource))
            .Select(source => new SourceRouteCandidate(
                source,
                ResolveDocumentTypeMatchForSource(
                    catalog,
                    source,
                    source.SourceRole ?? "unknown_source",
                    Path.GetFileName(source.CopiedPath),
                    source.FileType)))
            .ToArray();

        var computationCandidate = candidates
            .Where(candidate => SourceRole.Matches(candidate.Source.SourceRole, SourceRole.ComputationSheet))
            .OrderBy(candidate => GetSourcePriority(candidate.Source, context.Step.InputRoles))
            .ThenByDescending(candidate => candidate.Match.MatchScore)
            .ThenByDescending(candidate => candidate.Match.Definition.Priority)
            .FirstOrDefault();

        var structuredCandidate = candidates
            .Where(candidate => string.Equals(candidate.Match.Definition.Family, "structured_points", StringComparison.OrdinalIgnoreCase)
                                && !candidate.Match.LowConfidence
                                && candidate.Match.MatchScore >= candidate.Match.ScoreThreshold)
            .OrderBy(candidate => GetSourcePriority(candidate.Source, context.Step.InputRoles))
            .ThenByDescending(candidate => candidate.Match.MatchScore)
            .ThenByDescending(candidate => candidate.Match.Definition.Priority)
            .FirstOrDefault();

        var selected = computationCandidate
            ?? structuredCandidate
            ?? candidates
                .OrderBy(candidate => GetSourcePriority(candidate.Source, context.Step.InputRoles))
                .ThenByDescending(candidate => candidate.Match.MatchScore)
                .ThenByDescending(candidate => candidate.Match.Definition.Priority)
                .FirstOrDefault();

        var primarySource = selected?.Source ?? ResolvePointsSource(sourceFiles, context.Step.InputRoles);
        var primaryMatch = selected?.Match
            ?? (primarySource is null
                ? catalog.ResolveBestMatch(new DocumentTypeMatchCandidate("unknown_source", string.Empty, string.Empty))
                : ResolveDocumentTypeMatchForSource(
                    catalog,
                    primarySource,
                    primarySource.SourceRole ?? "unknown_source",
                    Path.GetFileName(primarySource.CopiedPath),
                    primarySource.FileType));

        var planSource = ResolveFirstSource(allSourceFiles, SourceRole.PlanMapReference);
        var dwgSource = ResolveFirstSource(allSourceFiles, SourceRole.DwgSource);
        var secondarySources = allSourceFiles
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

    private static DocumentTypeMatchResult ResolveDocumentTypeMatchForSource(
        DocumentTypeCatalog catalog,
        ManifestSourceFile source,
        string role,
        string fileName,
        string extension)
    {
        if (SourceRole.Matches(source.SourceRole, SourceRole.SurveyPlanPdf)
            && catalog.ResolveById("SINGLE_PARCEL_SURVEY_PLAN_PDF_V1") is { } surveyPlanDefinition)
        {
            return new DocumentTypeMatchResult(
                surveyPlanDefinition,
                "survey_plan_pdf_role_match",
                1.0,
                LowConfidence: false,
                CandidateRole: role,
                CandidateName: fileName,
                MatchScore: surveyPlanDefinition.Match.ScoreThreshold,
                ScoreThreshold: surveyPlanDefinition.Match.ScoreThreshold,
                CatalogPath: catalog.CatalogPath);
        }

        return catalog.ResolveBestMatch(new DocumentTypeMatchCandidate(role, fileName, extension));
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

        return ResolveFirstSource(sourceFiles, SourceRole.ComputationSheet)
            ?? ResolveFirstSource(sourceFiles, SourceRole.CoordinateTextSource);
    }

    private static ManifestSourceFile? ResolveFirstSource(IReadOnlyList<ManifestSourceFile> sourceFiles, string sourceRole)
    {
        return sourceFiles.FirstOrDefault(sourceFile =>
            SourceRole.Matches(sourceFile.SourceRole, sourceRole));
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
            "survey_plan_ocr_vision" => "single_parcel_survey_plan_pdf",
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
        return string.Equals(extractorId, "openai_table_pdf", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extractorId, "survey_plan_ocr_vision", StringComparison.OrdinalIgnoreCase);
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

    private static string BuildSurveyPlanOcrVisionScriptArguments(
        string scriptPath,
        string sourcePdfPath,
        string outputJsonPath,
        string transactionNumber,
        WorkflowRuleSettings ruleSettings)
    {
        var model = ResolveEffectiveOpenAiModel(ruleSettings);
        var profile = string.IsNullOrWhiteSpace(ruleSettings.OpenAiExtractionProfile)
            ? "balanced"
            : ruleSettings.OpenAiExtractionProfile.Trim();
        return $"\"{scriptPath}\" --source-pdf \"{sourcePdfPath}\" --output-json \"{outputJsonPath}\" --transaction-number \"{transactionNumber}\" --model \"{model}\" --profile \"{profile}\"";
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

    private static string? ResolveSurveyPlanOcrVisionScriptPath(WorkflowExecutionSettings executionSettings)
    {
        var configuredAdapterCandidates = new[]
        {
            executionSettings.OutputAdapterScriptPath,
            executionSettings.ValidationAdapterScriptPath
        }
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Select(path => Path.GetDirectoryName(path!))
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Select(path => Path.Combine(path!, Path.GetFileName(SurveyPlanOcrVisionScriptRelativePath)))
        .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in configuredAdapterCandidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return ResolveProjectFile(SurveyPlanOcrVisionScriptRelativePath);
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
            && preferredRoles.Any(role => SourceRole.Matches(source.SourceRole, role)))
        {
            return 0;
        }

        return SourceRole.Normalize(source.SourceRole) switch
        {
            SourceRole.CoordinateTextSource => 0,
            SourceRole.ComputationSheet => 0,
            SourceRole.PlanMapReference => 1,
            SourceRole.DwgSource => 2,
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

    private static JsonObject CloneObjectOrDefault(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? JsonNode.Parse(value.GetRawText())!.AsObject()
            : new JsonObject();
    }

    private static JsonArray CloneArrayOrEmpty(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? JsonNode.Parse(value.GetRawText())!.AsArray()
            : new JsonArray();
    }

    private static string? ReadNestedFieldValue(JsonElement element, string objectName)
    {
        return element.TryGetProperty(objectName, out var value)
               && value.ValueKind == JsonValueKind.Object
            ? ReadString(value, "value")
            : null;
    }

    private static string? ReadNestedFieldValue(JsonElement element, string objectName, string fieldName)
    {
        return element.TryGetProperty(objectName, out var value)
               && value.ValueKind == JsonValueKind.Object
               && value.TryGetProperty(fieldName, out var field)
               && field.ValueKind == JsonValueKind.Object
            ? ReadString(field, "value")
            : null;
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

    private sealed record SurveyPlanSourceProbe(
        bool IsPdf,
        bool TextLayerAvailable,
        string TextLayerProbeStatus,
        string TextContent);

    private sealed record SurveyPlanExtractionCandidate(
        string TransactionNumber,
        string SourceProfile,
        string ExtractorId,
        string SourceFileName,
        string Status,
        string FallbackReason,
        int ParcelCountHint,
        IReadOnlyDictionary<string, SurveyPlanField> Metadata,
        SurveyPlanDetectedFeature NorthArrow,
        IReadOnlyList<SurveyPlanPointCandidate> Points,
        IReadOnlyList<SurveyPlanSegmentCandidate> Segments,
        IReadOnlyList<SurveyPlanPartyCandidate> Parties,
        IReadOnlyList<SurveyPlanPartyCandidate> Representatives,
        IReadOnlyList<SurveyPlanPartyCandidate> AdjacentOwners,
        IReadOnlyList<string> Warnings);

    private sealed record SurveyPlanField(
        string FieldName,
        string? Value,
        double Confidence,
        string SourceZone,
        string ReviewNote);

    private sealed record SurveyPlanDetectedFeature(
        string Feature,
        bool Detected,
        string? ApproximatePageLocation,
        double Confidence,
        string ReviewNote);

    private sealed record SurveyPlanPointCandidate(
        string RowId,
        string PointId,
        string Easting,
        string Northing,
        int Sequence,
        int SourcePage,
        string SourceZone,
        double Confidence,
        string SourceEvidence);

    private sealed record SurveyPlanSegmentCandidate(
        string SegmentId,
        string BearingText,
        string DistanceText,
        string LengthText,
        int Sequence,
        int SourcePage,
        string SourceZone,
        double Confidence,
        string SourceEvidence);

    private sealed record SurveyPlanPartyCandidate(
        string Name,
        double Confidence,
        string SourceZone);

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
