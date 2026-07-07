using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed class ExtractionDecisionGateService
{
    private const string SchemaVersion = "1.0.0";
    private const double InvalidCoordinateRatioThreshold = 0.35d;
    private readonly Func<int> getManualReviewRetryThreshold;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public ExtractionDecisionGateService(Func<int>? getManualReviewRetryThreshold = null)
    {
        this.getManualReviewRetryThreshold = getManualReviewRetryThreshold
            ?? (() => InnolaTransactionSettings.Load().ManualReviewRetryThreshold);
    }

    public string StateFileName => "extraction_decision_gate.json";

    public ExtractionDecisionGateState LoadState(CaseFolderLayout layout)
    {
        var path = GetStatePath(layout);
        if (!File.Exists(path))
        {
            return ExtractionDecisionGateState.Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<ExtractionDecisionGateState>(File.ReadAllText(path), Options)
                ?? ExtractionDecisionGateState.Empty;
        }
        catch (Exception exception) when (exception is JsonException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            return ExtractionDecisionGateState.Empty with
            {
                Notes = new[] { $"Decision gate state could not be read: {exception.GetType().Name}." }
            };
        }
    }

    public void SaveState(CaseFolderLayout layout, ExtractionDecisionGateState state)
    {
        Directory.CreateDirectory(layout.WorkingDirectory);
        File.WriteAllText(GetStatePath(layout), JsonSerializer.Serialize(state with { SchemaVersion = SchemaVersion }, Options));
    }

    public ExtractionDecisionGateResult Evaluate(ExtractionReviewDocument? document, ExtractionDecisionGateState state)
    {
        var issues = new List<string>();
        var warnings = new List<string>();

        if (document is null)
        {
            issues.Add("No extraction review artifact is available yet.");
            return BuildWeakResult("missing_artifact", state, issues, warnings, usableRows: 0, totalRows: 0);
        }

        var totalRows = document.Rows.Count;
        var usableRows = 0;
        var invalidCoordinateRows = 0;
        var missingParcelGroupingRows = 0;

        foreach (var row in document.Rows)
        {
            var hasPointId = !string.IsNullOrWhiteSpace(row.PointIdentifier);
            var hasCoordinates = TryParseCoordinate(row.Easting, out _) && TryParseCoordinate(row.Northing, out _);
            if (hasCoordinates)
            {
                usableRows++;
            }
            else
            {
                invalidCoordinateRows++;
            }

            if (!IsKnownParcelGroup(row.ParcelGroupId))
            {
                missingParcelGroupingRows++;
            }

            if (!hasPointId)
            {
                warnings.Add("One or more extracted rows are missing point identifiers.");
            }
        }

        var invalidCoordinateRatio = totalRows == 0 ? 0d : (double)invalidCoordinateRows / totalRows;
        var allGroupingUnknown = totalRows > 0 && missingParcelGroupingRows == totalRows;
        var lowConfidenceMatch = document.RootMetadata["match_low_confidence"]?.GetValue<bool?>() == true;
        var documentErrors = document.Errors
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (documentErrors.Length > 0)
        {
            warnings.AddRange(documentErrors);
        }

        if (totalRows == 0)
        {
            issues.Add("Extraction produced zero review rows.");
        }

        if (usableRows == 0)
        {
            issues.Add("Extraction did not produce usable coordinate rows.");
        }

        if (allGroupingUnknown && totalRows > 1)
        {
            issues.Add("Extraction did not produce usable parcel grouping for the current review rows.");
        }

        if (invalidCoordinateRatio >= InvalidCoordinateRatioThreshold)
        {
            issues.Add($"Too many rows have invalid coordinates ({invalidCoordinateRows} of {totalRows}).");
        }

        if (lowConfidenceMatch)
        {
            warnings.Add("Document type matching is low confidence for this extraction.");
        }

        if (issues.Count > 0)
        {
            return BuildWeakResult("weak", state, issues, warnings, usableRows, totalRows);
        }

        var successSummary = usableRows == totalRows
            ? $"Extraction produced {usableRows} usable point row(s)."
            : $"Extraction produced {usableRows} usable point row(s) out of {totalRows}.";

        var successGuidance = "Extraction looks usable. Continue point review in Points Validation Tool as the next step for parcel-by-parcel review before Create Spatial Units.";
        if (warnings.Count > 0)
        {
            successGuidance = $"{successGuidance} Review warnings: {warnings[0]}";
        }

        return new ExtractionDecisionGateResult(
            RequiresDecision: false,
            HasUsableReview: true,
            StronglyRecommendManual: false,
            AttemptCount: state.AttemptCount,
            WeakAttemptCount: state.WeakAttemptCount,
            QualityStatus: "usable",
            SummaryText: successSummary,
            GuidanceText: successGuidance,
            Issues: issues,
            Warnings: warnings,
            TotalRows: totalRows,
            UsableRows: usableRows);
    }

    public string GetStatePath(CaseFolderLayout layout)
    {
        return Path.Combine(layout.WorkingDirectory, StateFileName);
    }

    private ExtractionDecisionGateResult BuildWeakResult(
        string qualityStatus,
        ExtractionDecisionGateState state,
        IReadOnlyList<string> issues,
        IReadOnlyList<string> warnings,
        int usableRows,
        int totalRows)
    {
        var retryThreshold = Math.Max(1, getManualReviewRetryThreshold());
        var stronglyRecommendManual = state.WeakAttemptCount >= retryThreshold;
        var summary = issues.Count > 0
            ? issues[0]
            : "Extraction review needs a routing decision.";
        var guidance = stronglyRecommendManual
            ? $"Extraction is still below the quality threshold after {state.AttemptCount} attempt(s). Manual review workspace is recommended. AI-assisted reruns may differ, but they may still remain insufficient."
            : "Extraction results are not strong enough yet. Re-process extraction if you want another attempt, or switch to the manual review workspace.";

        return new ExtractionDecisionGateResult(
            RequiresDecision: true,
            HasUsableReview: false,
            StronglyRecommendManual: stronglyRecommendManual,
            AttemptCount: state.AttemptCount,
            WeakAttemptCount: state.WeakAttemptCount,
            QualityStatus: qualityStatus,
            SummaryText: summary,
            GuidanceText: guidance,
            Issues: issues,
            Warnings: warnings,
            TotalRows: totalRows,
            UsableRows: usableRows);
    }

    private static bool TryParseCoordinate(string? value, out double coordinate)
    {
        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out coordinate)
            && coordinate > 0d;
    }

    private static bool IsKnownParcelGroup(string? parcelGroupId)
    {
        if (string.IsNullOrWhiteSpace(parcelGroupId))
        {
            return false;
        }

        var normalized = parcelGroupId.Trim();
        return !string.Equals(normalized, "Parcel ?", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalized, "unknown", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalized, "ungrouped", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record ExtractionDecisionGateState(
    string SchemaVersion,
    int AttemptCount,
    int WeakAttemptCount,
    string? LastAttemptAt,
    string? LastMethod,
    string? LastRoute,
    string? LastQualityStatus,
    IReadOnlyList<string> Notes)
{
    public static ExtractionDecisionGateState Empty => new(
        "1.0.0",
        0,
        0,
        null,
        null,
        null,
        null,
        Array.Empty<string>());
}

public sealed record ExtractionDecisionGateResult(
    bool RequiresDecision,
    bool HasUsableReview,
    bool StronglyRecommendManual,
    int AttemptCount,
    int WeakAttemptCount,
    string QualityStatus,
    string SummaryText,
    string GuidanceText,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Warnings,
    int TotalRows,
    int UsableRows)
{
    public static ExtractionDecisionGateResult NotEvaluated => new(
        RequiresDecision: false,
        HasUsableReview: false,
        StronglyRecommendManual: false,
        AttemptCount: 0,
        WeakAttemptCount: 0,
        QualityStatus: "not_evaluated",
        SummaryText: "Extraction has not been evaluated yet.",
        GuidanceText: "Run Validate Points to evaluate extracted point quality.",
        Issues: Array.Empty<string>(),
        Warnings: Array.Empty<string>(),
        TotalRows: 0,
        UsableRows: 0);
}
