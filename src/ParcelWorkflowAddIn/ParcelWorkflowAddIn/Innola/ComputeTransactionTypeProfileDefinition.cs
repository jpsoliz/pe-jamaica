using System.Text.Json.Serialization;
using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.Innola;

public sealed record ComputeTransactionTypeProfileDefinition(
    [property: JsonPropertyName("profile_id")] string ProfileId,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("transaction_type_codes")] IReadOnlyList<string> TransactionTypeCodes,
    [property: JsonPropertyName("transaction_type_names")] IReadOnlyList<string> TransactionTypeNames,
    [property: JsonPropertyName("workflow_profile")] string WorkflowProfile,
    [property: JsonPropertyName("required_source_roles")] IReadOnlyList<string> RequiredSourceRoles,
    [property: JsonPropertyName("optional_source_roles")] IReadOnlyList<string> OptionalSourceRoles,
    [property: JsonPropertyName("primary_extraction_role")] string PrimaryExtractionRole,
    [property: JsonPropertyName("document_profile")] string DocumentProfile)
{
    public bool Matches(string? transactionType, string? taskName = null, string? profileHint = null)
    {
        return MatchScore(transactionType, taskName, profileHint) > 0;
    }

    public int MatchScore(string? transactionType, string? taskName = null, string? profileHint = null)
    {
        var score = 0;
        if (MatchesAny(TransactionTypeCodes, profileHint))
        {
            score = Math.Max(score, 110);
        }

        if (MatchesAny(TransactionTypeCodes, transactionType))
        {
            score = Math.Max(score, 100);
        }

        if (MatchesAny(TransactionTypeNames, profileHint))
        {
            score = Math.Max(score, 90);
        }

        if (MatchesAny(TransactionTypeNames, transactionType))
        {
            score = Math.Max(score, 50);
        }

        if (MatchesAny(TransactionTypeNames, taskName))
        {
            score = Math.Max(score, 40);
        }

        return score;
    }

    private static bool MatchesAny(IReadOnlyList<string> candidates, string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && candidates.Any(candidate => candidate.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record ResolvedComputeTransactionTypeProfile(
    [property: JsonPropertyName("profile_id")] string ProfileId,
    [property: JsonPropertyName("workflow_profile")] string WorkflowProfile,
    [property: JsonPropertyName("required_source_roles")] IReadOnlyList<string> RequiredSourceRoles,
    [property: JsonPropertyName("optional_source_roles")] IReadOnlyList<string> OptionalSourceRoles,
    [property: JsonPropertyName("primary_extraction_role")] string PrimaryExtractionRole,
    [property: JsonPropertyName("document_profile")] string DocumentProfile);

internal static class ComputeTransactionTypeProfileCatalog
{
    public static IReadOnlyList<ComputeTransactionTypeProfileDefinition> SafeDefaults { get; } = new[]
    {
        new ComputeTransactionTypeProfileDefinition(
            "pe_computation_review",
            true,
            new[] { "PE" },
            new[]
            {
                "Plan Examination",
                "Cadastral Plan Examination",
                "Compute Survey Plan",
                "Assign Computation Task",
                "Computation Check"
            },
            "pe_computation_sheet_review",
            new[] { SourceRole.ComputationSheet, SourceRole.PlanMapReference },
            new[] { SourceRole.CoordinateTextSource, SourceRole.DwgSource },
            SourceRole.ComputationSheet,
            "computation_sheet_multi_or_single_parcel"),
        new ComputeTransactionTypeProfileDefinition(
            "pxa_single_parcel_survey_plan",
            true,
            new[] { "PXA" },
            new[] { "PXA", "Plan Examination by Area" },
            "pxa_single_parcel_survey_plan",
            new[] { SourceRole.SurveyPlanPdf },
            new[] { SourceRole.CoordinateTextSource, SourceRole.DwgSource },
            SourceRole.SurveyPlanPdf,
            "scanned_single_parcel_survey_plan_pdf")
    };

    public static ResolvedComputeTransactionTypeProfile ToResolved(ComputeTransactionTypeProfileDefinition definition)
    {
        return new ResolvedComputeTransactionTypeProfile(
            definition.ProfileId,
            definition.WorkflowProfile,
            definition.RequiredSourceRoles,
            definition.OptionalSourceRoles,
            definition.PrimaryExtractionRole,
            definition.DocumentProfile);
    }
}
