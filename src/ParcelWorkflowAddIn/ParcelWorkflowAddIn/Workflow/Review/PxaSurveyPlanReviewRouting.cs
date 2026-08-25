namespace ParcelWorkflowAddIn.Workflow.Review;

internal static class PxaSurveyPlanReviewRouting
{
    public static bool IsPxaSurveyPlanDocument(ExtractionReviewDocument document)
    {
        if (IsPlaPlanAnnexationDocument(document))
        {
            return true;
        }

        return IsPxaOnlySurveyPlanDocument(document);
    }

    public static bool IsPxaOnlySurveyPlanDocument(ExtractionReviewDocument document)
    {
        if (IsPlaPlanAnnexationDocument(document))
        {
            return false;
        }

        var source = document.ExtractionSource ?? string.Empty;
        if (source.Contains("survey_plan", StringComparison.OrdinalIgnoreCase)
            || source.Contains("ocr_vision", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var activeExtractor = document.RootMetadata["active_extractor_id"]?.ToString() ?? string.Empty;
        if (activeExtractor.Contains("survey_plan", StringComparison.OrdinalIgnoreCase)
            || activeExtractor.Contains("ocr_vision", StringComparison.OrdinalIgnoreCase)
            || activeExtractor.Contains("pla_plan", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var sourceProfile = document.RootMetadata["source_profile"]?.ToString() ?? string.Empty;
        var primarySourceRole = document.RootMetadata["primary_source_role"]?.ToString() ?? string.Empty;
        return sourceProfile.Contains("survey_plan", StringComparison.OrdinalIgnoreCase)
            || primarySourceRole.Contains("survey_plan", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPlaPlanAnnexationDocument(ExtractionReviewDocument document)
    {
        var sourceProfile = document.RootMetadata["source_profile"]?.ToString() ?? string.Empty;
        var activeExtractor = document.RootMetadata["active_extractor_id"]?.ToString() ?? string.Empty;
        var primarySourceRole = document.RootMetadata["primary_source_role"]?.ToString() ?? string.Empty;
        return sourceProfile.Contains("pla_plan_annexation", StringComparison.OrdinalIgnoreCase)
            || activeExtractor.Contains("pla_plan", StringComparison.OrdinalIgnoreCase)
            || primarySourceRole.Contains("plan_annexation", StringComparison.OrdinalIgnoreCase);
    }
}
