using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Workflow.Output;
using ParcelWorkflowAddIn.Workflow.Review;
using ParcelWorkflowAddIn.Workflow.SpatialReview;

namespace ParcelWorkflowAddIn.Workflow.Pla;

internal sealed class PlaVisualComparisonService
{
    public const string WorkingDirectoryName = "pla_visual_comparison";
    public const string ComparisonArtifactFileName = "pla_visual_comparison.json";
    public const string GeometryVisualFileName = "pla_generated_geometry_visual.svg";
    public const string ComparisonModeApproximate = "approximate_visual_similarity";
    public const string ComparisonModeTitlePlanOverlay = "title_plan_overlay_two_point_similarity";
    public const string ComparisonModeSpatialReviewApproval = "spatial_review_approval";

    private const string DisclaimerText = "Approximate visual similarity only; not survey-accurate georeferencing or authoritative parcel fabric alignment.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Func<DateTimeOffset> getUtcNow;

    public PlaVisualComparisonService(Func<DateTimeOffset>? getUtcNow = null)
    {
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public PlaVisualComparisonResult GenerateVisualEvidence(
        CaseFolderLayout layout,
        string transactionNumber,
        ExtractionReviewDocument review,
        OutputSummaryDocument outputSummary,
        string? createdBy)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(outputSummary);

        var selection = PlaPlanEvidenceSelectionService.LoadSelection(layout);
        var selectedEvidencePath = string.Empty;
        if (selection is null
            || !PlaPlanEvidenceSelectionService.TryResolveCaseRelativePath(layout, selection.GeneratedPlanEvidenceRelativePath, out selectedEvidencePath)
            || !File.Exists(selectedEvidencePath))
        {
            return PlaVisualComparisonResult.Failed("Saved PLA selected-plan evidence is required before visual comparison.");
        }

        if (outputSummary.Payload.PolygonCount <= 0 && outputSummary.Payload.BuiltParcelCount <= 0)
        {
            return PlaVisualComparisonResult.Failed("Generated PLA geometry evidence is required before visual comparison.");
        }

        Directory.CreateDirectory(GetWorkingDirectory(layout));
        var now = getUtcNow();
        var visualPath = GetGeometryVisualPath(layout);
        var solverResult = new SurveyPlanBoundarySolver().Apply(
            review,
            documentAreaSqM: null,
            useLocalOriginWhenUnreferenced: true);
        var points = solverResult.Status.Equals("blocked", StringComparison.OrdinalIgnoreCase)
            ? Array.Empty<SolverPoint>()
            : solverResult.DerivedPoints;
        File.WriteAllText(visualPath, BuildSvg(transactionNumber, points, outputSummary), Encoding.UTF8);

        var document = new PlaVisualComparisonDocument
        {
            SchemaVersion = "1.0.0",
            TransactionNumber = transactionNumber,
            ComparisonMode = ComparisonModeApproximate,
            Disclaimer = DisclaimerText,
            SelectedPlanEvidenceRelativePath = ToCaseRelativePath(layout, selectedEvidencePath),
            GeometryVisualRelativePath = ToCaseRelativePath(layout, visualPath),
            OutputArtifactRelativePaths = outputSummary.Payload.ArtifactPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => ToCaseRelativePathIfInside(layout, path))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>()
                .ToArray(),
            GeneratedGeometryPointCount = points.Count,
            GeneratedGeometryLineCount = outputSummary.Payload.LineCount,
            GeneratedGeometryPolygonCount = Math.Max(outputSummary.Payload.PolygonCount, outputSummary.Payload.BuiltParcelCount),
            GeometryReferenceMode = "local_origin_or_source_review",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedBy = createdBy
        }.WithCaseRoot(layout.RootDirectory);

        Save(layout, document);
        return PlaVisualComparisonResult.Succeeded(document);
    }

    public PlaVisualComparisonResult SaveReviewDecision(
        CaseFolderLayout layout,
        string reviewerDecision,
        string? reviewerNotes,
        string? reviewedBy)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var document = Load(layout);
        if (document is null)
        {
            return PlaVisualComparisonResult.Failed("Generate PLA visual comparison evidence before saving review decision.");
        }

        if (!IsSupportedDecision(reviewerDecision))
        {
            return PlaVisualComparisonResult.Failed("PLA visual comparison decision must be accepted, flagged, or rejected.");
        }

        var updated = document with
        {
            ReviewerDecision = reviewerDecision.Trim().ToLowerInvariant(),
            ReviewerNotes = reviewerNotes?.Trim(),
            ReviewedBy = reviewedBy,
            ReviewedAtUtc = getUtcNow(),
            UpdatedAtUtc = getUtcNow()
        };
        Save(layout, updated);
        return PlaVisualComparisonResult.Succeeded(updated);
    }

    public PlaVisualComparisonDocument? Load(CaseFolderLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var path = GetComparisonArtifactPath(layout);
        if (!File.Exists(path))
        {
            return LoadTitlePlanOverlayComparison(layout)
                ?? LoadSpatialReviewApprovalComparison(layout);
        }

        try
        {
            var document = JsonSerializer.Deserialize<PlaVisualComparisonDocument>(File.ReadAllText(path), JsonOptions);
            if (document is null
                || !PlaPlanEvidenceSelectionService.TryResolveCaseRelativePath(layout, document.SelectedPlanEvidenceRelativePath, out _)
                || !PlaPlanEvidenceSelectionService.TryResolveCaseRelativePath(layout, document.GeometryVisualRelativePath, out _))
            {
                return null;
            }

            return document.WithCaseRoot(layout.RootDirectory);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static string GetWorkingDirectory(CaseFolderLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return Path.Combine(layout.WorkingDirectory, WorkingDirectoryName);
    }

    public static string GetComparisonArtifactPath(CaseFolderLayout layout)
    {
        return Path.Combine(GetWorkingDirectory(layout), ComparisonArtifactFileName);
    }

    public static string GetGeometryVisualPath(CaseFolderLayout layout)
    {
        return Path.Combine(GetWorkingDirectory(layout), GeometryVisualFileName);
    }

    private static void Save(CaseFolderLayout layout, PlaVisualComparisonDocument document)
    {
        Directory.CreateDirectory(GetWorkingDirectory(layout));
        File.WriteAllText(GetComparisonArtifactPath(layout), JsonSerializer.Serialize(document, JsonOptions));
    }

    private static PlaVisualComparisonDocument? LoadTitlePlanOverlayComparison(CaseFolderLayout layout)
    {
        var artifactPath = MapGeoreferenceOverlayArtifactPlan.BuildMetadataPath(
            layout.RootDirectory,
            MapGeoreferenceOverlayKind.TitlePlanComparison);
        if (!File.Exists(artifactPath))
        {
            return null;
        }

        try
        {
            var artifact = JsonSerializer.Deserialize<MapGeoreferenceOverlayArtifactDocument>(
                File.ReadAllText(artifactPath),
                JsonOptions);
            if (artifact is null
                || !MatchesActiveTransaction(layout, artifact.TransactionNumber)
                || !string.Equals(artifact.OverlayKind, nameof(MapGeoreferenceOverlayKind.TitlePlanComparison), StringComparison.OrdinalIgnoreCase)
                || !TryResolveInsideCase(layout, artifact.ImagePath, out var overlayImagePath)
                || !File.Exists(overlayImagePath)
                || !TryResolveInsideCase(layout, artifact.OutputGeodatabasePath, out var outputGeodatabasePath))
            {
                return null;
            }

            var selection = PlaPlanEvidenceSelectionService.LoadSelection(layout);
            if (selection is null
                || !PlaPlanEvidenceSelectionService.TryResolveCaseRelativePath(layout, selection.GeneratedPlanEvidenceRelativePath, out _))
            {
                return null;
            }

            var outputPaths = new List<string> { outputGeodatabasePath };
            if (TryResolveInsideCase(layout, artifact.RasterDatasetPath, out var rasterDatasetPath))
            {
                outputPaths.Add(rasterDatasetPath);
            }

            return new PlaVisualComparisonDocument
            {
                SchemaVersion = "1.0.0",
                TransactionNumber = artifact.TransactionNumber,
                ComparisonMode = ComparisonModeTitlePlanOverlay,
                Disclaimer = DisclaimerText,
                SelectedPlanEvidenceRelativePath = selection.GeneratedPlanEvidenceRelativePath,
                GeometryVisualRelativePath = ToCaseRelativePath(layout, overlayImagePath),
                OutputArtifactRelativePaths = outputPaths
                    .Select(path => ToCaseRelativePath(layout, path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                GeometryReferenceMode = "title_plan_overlay_two_point_similarity",
                ReviewerDecision = "accepted",
                ReviewerNotes = "Title-plan comparison overlay was created from examiner-selected image and map control points.",
                ReviewedAtUtc = artifact.CreatedAtUtc,
                CreatedAtUtc = artifact.CreatedAtUtc,
                UpdatedAtUtc = artifact.CreatedAtUtc
            }.WithCaseRoot(layout.RootDirectory);
        }
        catch (Exception exception) when (exception is JsonException
            or IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return null;
        }
    }

    private PlaVisualComparisonDocument? LoadSpatialReviewApprovalComparison(CaseFolderLayout layout)
    {
        try
        {
            var selection = PlaPlanEvidenceSelectionService.LoadSelection(layout);
            if (selection is null
                || !PlaPlanEvidenceSelectionService.TryResolveCaseRelativePath(layout, selection.GeneratedPlanEvidenceRelativePath, out _))
            {
                return null;
            }

            var outputSummary = new OutputSummaryPersistenceService().Load(layout);
            if (outputSummary is null)
            {
                return null;
            }

            var approvalService = new SpatialReviewApprovalPersistenceService();
            var validation = approvalService.ValidateCurrent(layout, outputSummary);
            if (!validation.IsCurrent || validation.Approval is null)
            {
                return null;
            }

            var geometryArtifact = outputSummary.Payload.ArtifactPaths
                .Select(path => ToCaseRelativePathIfInside(layout, path))
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path))
                ?? ToCaseRelativePath(layout, approvalService.GetApprovalPath(layout));
            var outputPaths = outputSummary.Payload.ArtifactPaths
                .Select(path => ToCaseRelativePathIfInside(layout, path))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>()
                .ToArray();

            return new PlaVisualComparisonDocument
            {
                SchemaVersion = "1.0.0",
                TransactionNumber = validation.Approval.TransactionId,
                ComparisonMode = ComparisonModeSpatialReviewApproval,
                Disclaimer = DisclaimerText,
                SelectedPlanEvidenceRelativePath = selection.GeneratedPlanEvidenceRelativePath,
                GeometryVisualRelativePath = geometryArtifact,
                OutputArtifactRelativePaths = outputPaths,
                GeneratedGeometryPointCount = outputSummary.Payload.PointCount,
                GeneratedGeometryLineCount = outputSummary.Payload.LineCount,
                GeneratedGeometryPolygonCount = Math.Max(outputSummary.Payload.PolygonCount, outputSummary.Payload.BuiltParcelCount),
                GeometryReferenceMode = "spatial_review_approved_output_layers",
                ReviewerDecision = "accepted",
                ReviewerNotes = "Final Review approved the generated PLA output layers.",
                ReviewedBy = validation.Approval.ApprovedBy,
                ReviewedAtUtc = DateTimeOffset.TryParse(validation.Approval.ApprovedAt, out var reviewedAt) ? reviewedAt : null,
                CreatedAtUtc = DateTimeOffset.TryParse(validation.Approval.ApprovedAt, out var createdAt) ? createdAt : getUtcNow(),
                UpdatedAtUtc = DateTimeOffset.TryParse(validation.Approval.ApprovedAt, out var updatedAt) ? updatedAt : getUtcNow()
            }.WithCaseRoot(layout.RootDirectory);
        }
        catch (Exception exception) when (exception is JsonException
            or IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or PathTooLongException)
        {
            return null;
        }
    }

    private static bool MatchesActiveTransaction(CaseFolderLayout layout, string? transactionNumber)
    {
        if (string.IsNullOrWhiteSpace(transactionNumber))
        {
            return false;
        }

        try
        {
            if (File.Exists(layout.ManifestPath))
            {
                var manifest = ManifestSerializer.Read(layout.ManifestPath);
                return string.Equals(manifest.TransactionId, transactionNumber, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(manifest.Payload.InnolaTransaction?.TransactionNumber, transactionNumber, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or ArgumentException
            or NotSupportedException)
        {
            return false;
        }

        var caseFolderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(layout.RootDirectory));
        return string.Equals(caseFolderName, transactionNumber, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSvg(string transactionNumber, IReadOnlyList<SolverPoint> points, OutputSummaryDocument outputSummary)
    {
        var polyline = BuildPolyline(points);
        return $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="720" height="520" viewBox="0 0 720 520" role="img" aria-label="PLA generated geometry visual evidence">
              <rect x="0" y="0" width="720" height="520" fill="#f7f8fa" />
              <text x="24" y="36" font-family="Segoe UI, Arial, sans-serif" font-size="20" fill="#1f2933">PLA Generated Geometry Visual</text>
              <text x="24" y="60" font-family="Segoe UI, Arial, sans-serif" font-size="12" fill="#52616b">Transaction {{Escape(transactionNumber)}} - approximate visual similarity only</text>
              <rect x="24" y="84" width="672" height="352" fill="#ffffff" stroke="#c9d2d8" stroke-width="1" />
              {{polyline}}
              <text x="24" y="466" font-family="Segoe UI, Arial, sans-serif" font-size="12" fill="#52616b">{{Escape(DisclaimerText)}}</text>
              <text x="24" y="488" font-family="Segoe UI, Arial, sans-serif" font-size="12" fill="#52616b">Output polygons: {{outputSummary.Payload.PolygonCount}}, lines: {{outputSummary.Payload.LineCount}}, points: {{outputSummary.Payload.PointCount}}</text>
            </svg>
            """;
    }

    private static string BuildPolyline(IReadOnlyList<SolverPoint> points)
    {
        if (points.Count == 0)
        {
            return """<text x="48" y="260" font-family="Segoe UI, Arial, sans-serif" font-size="14" fill="#52616b">Generated output evidence available; boundary point preview unavailable.</text>""";
        }

        var minX = points.Min(point => point.Easting);
        var maxX = points.Max(point => point.Easting);
        var minY = points.Min(point => point.Northing);
        var maxY = points.Max(point => point.Northing);
        var width = Math.Max(maxX - minX, 1d);
        var height = Math.Max(maxY - minY, 1d);
        var projected = points
            .OrderBy(point => point.SourceSegment, StringComparer.OrdinalIgnoreCase)
            .Select(point =>
            {
                var x = 72d + ((point.Easting - minX) / width * 576d);
                var y = 396d - ((point.Northing - minY) / height * 264d);
                return FormattableString.Invariant($"{x:0.###},{y:0.###}");
            })
            .ToArray();
        var pointText = string.Join(" ", projected.Concat(new[] { projected[0] }));
        return $$"""
              <polyline points="{{pointText}}" fill="rgba(71, 123, 184, 0.16)" stroke="#2f6fab" stroke-width="3" />
              <circle cx="{{projected[0].Split(',')[0]}}" cy="{{projected[0].Split(',')[1]}}" r="4" fill="#1f2933" />
            """;
    }

    private static string Escape(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }

    private static bool IsSupportedDecision(string? decision)
    {
        return string.Equals(decision, "accepted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(decision, "flagged", StringComparison.OrdinalIgnoreCase)
            || string.Equals(decision, "rejected", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToCaseRelativePath(CaseFolderLayout layout, string path)
    {
        var relativePath = Path.GetRelativePath(layout.RootDirectory, path);
        return relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string? ToCaseRelativePathIfInside(CaseFolderLayout layout, string path)
    {
        try
        {
            var normalizedRoot = Path.GetFullPath(layout.RootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var normalizedPath = Path.GetFullPath(path);
            return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                ? ToCaseRelativePath(layout, normalizedPath)
                : null;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static bool TryResolveInsideCase(CaseFolderLayout layout, string? path, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var normalizedRoot = Path.GetFullPath(layout.RootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var candidate = Path.IsPathFullyQualified(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(layout.RootDirectory, path.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            resolvedPath = candidate;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}

internal sealed record PlaVisualComparisonResult(
    bool Success,
    string? Message,
    PlaVisualComparisonDocument? Document)
{
    public static PlaVisualComparisonResult Succeeded(PlaVisualComparisonDocument document) =>
        new(true, null, document);

    public static PlaVisualComparisonResult Failed(string message) =>
        new(false, message, null);
}

internal sealed record PlaVisualComparisonDocument
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = "1.0.0";

    [JsonPropertyName("transaction_number")]
    public string TransactionNumber { get; init; } = string.Empty;

    [JsonPropertyName("comparison_mode")]
    public string ComparisonMode { get; init; } = PlaVisualComparisonService.ComparisonModeApproximate;

    [JsonPropertyName("disclaimer")]
    public string Disclaimer { get; init; } = string.Empty;

    [JsonPropertyName("selected_plan_evidence_path")]
    public string SelectedPlanEvidenceRelativePath { get; init; } = string.Empty;

    [JsonPropertyName("geometry_visual_path")]
    public string GeometryVisualRelativePath { get; init; } = string.Empty;

    [JsonPropertyName("output_artifact_paths")]
    public IReadOnlyList<string> OutputArtifactRelativePaths { get; init; } = Array.Empty<string>();

    [JsonPropertyName("generated_geometry_point_count")]
    public int GeneratedGeometryPointCount { get; init; }

    [JsonPropertyName("generated_geometry_line_count")]
    public int GeneratedGeometryLineCount { get; init; }

    [JsonPropertyName("generated_geometry_polygon_count")]
    public int GeneratedGeometryPolygonCount { get; init; }

    [JsonPropertyName("geometry_reference_mode")]
    public string GeometryReferenceMode { get; init; } = string.Empty;

    [JsonPropertyName("reviewer_decision")]
    public string? ReviewerDecision { get; init; }

    [JsonPropertyName("reviewer_notes")]
    public string? ReviewerNotes { get; init; }

    [JsonPropertyName("reviewed_by")]
    public string? ReviewedBy { get; init; }

    [JsonPropertyName("reviewed_at_utc")]
    public DateTimeOffset? ReviewedAtUtc { get; init; }

    [JsonPropertyName("created_at_utc")]
    public DateTimeOffset CreatedAtUtc { get; init; }

    [JsonPropertyName("updated_at_utc")]
    public DateTimeOffset UpdatedAtUtc { get; init; }

    [JsonPropertyName("created_by")]
    public string? CreatedBy { get; init; }

    [JsonIgnore]
    public string CaseRootDirectory { get; init; } = string.Empty;

    [JsonIgnore]
    public string GeometryVisualPath => string.IsNullOrWhiteSpace(CaseRootDirectory)
        ? GeometryVisualRelativePath
        : Path.GetFullPath(Path.Combine(CaseRootDirectory, GeometryVisualRelativePath.Replace('/', Path.DirectorySeparatorChar)));

    public PlaVisualComparisonDocument WithCaseRoot(string caseRootDirectory)
    {
        return this with { CaseRootDirectory = caseRootDirectory };
    }
}
