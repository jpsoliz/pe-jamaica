namespace ParcelWorkflowAddIn.Intake;

public static class SourceRole
{
    public const string ComputationSheet = "computation_sheet";
    public const string CoordinateTextSource = "coordinate_text_source";
    public const string DwgSource = "dwg_source";
    public const string PlanMapReference = "plan_map_reference";
    public const string WorkflowResumePackage = "workflow_resume_package";
    public const string AmbiguousDocument = "ambiguous_document";
    public const string UnsupportedSource = "unsupported_source";

    public const string ComputationSource = "computation_source";
    public const string PointsComputation = "points_computation";
    public const string DwgReference = "dwg_reference";

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            ComputationSource => ComputationSheet,
            ComputationSheet => ComputationSheet,
            PointsComputation => CoordinateTextSource,
            CoordinateTextSource => CoordinateTextSource,
            DwgReference => DwgSource,
            DwgSource => DwgSource,
            PlanMapReference => PlanMapReference,
            WorkflowResumePackage => WorkflowResumePackage,
            AmbiguousDocument => AmbiguousDocument,
            UnsupportedSource => UnsupportedSource,
            var unknown => unknown
        };
    }

    public static bool Matches(string? actual, string? expected)
    {
        var normalizedActual = Normalize(actual);
        var normalizedExpected = Normalize(expected);

        return !string.IsNullOrWhiteSpace(normalizedActual)
            && !string.IsNullOrWhiteSpace(normalizedExpected)
            && string.Equals(normalizedActual, normalizedExpected, StringComparison.OrdinalIgnoreCase);
    }

    public static bool MatchesAny(string? actual, IEnumerable<string>? candidates)
    {
        if (candidates is null)
        {
            return false;
        }

        return candidates.Any(candidate => Matches(actual, candidate));
    }

    public static string DisplayName(string? value)
    {
        return Normalize(value) switch
        {
            ComputationSheet => "survey sheet",
            CoordinateTextSource => "structured survey points",
            DwgSource => "AutoCAD survey file",
            PlanMapReference => "survey plan / map reference",
            WorkflowResumePackage => "workflow package",
            AmbiguousDocument => "unclassified source",
            UnsupportedSource => "unsupported source",
            null or "" => "source",
            var other => other.Replace("_", " ")
        };
    }
}
