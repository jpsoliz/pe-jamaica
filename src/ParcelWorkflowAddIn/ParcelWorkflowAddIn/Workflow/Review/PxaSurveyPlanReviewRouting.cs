namespace ParcelWorkflowAddIn.Workflow.Review;

internal static class PxaSurveyPlanReviewRouting
{
    public static bool IsPxaSurveyPlanDocument(ExtractionReviewDocument document)
    {
        var source = document.ExtractionSource ?? string.Empty;
        if (source.Contains("survey_plan", StringComparison.OrdinalIgnoreCase)
            || source.Contains("ocr_vision", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var activeExtractor = document.RootMetadata["active_extractor_id"]?.ToString() ?? string.Empty;
        if (activeExtractor.Contains("survey_plan", StringComparison.OrdinalIgnoreCase)
            || activeExtractor.Contains("ocr_vision", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var sourceProfile = document.RootMetadata["source_profile"]?.ToString() ?? string.Empty;
        var primarySourceRole = document.RootMetadata["primary_source_role"]?.ToString() ?? string.Empty;
        return sourceProfile.Contains("survey_plan", StringComparison.OrdinalIgnoreCase)
            || primarySourceRole.Contains("survey_plan", StringComparison.OrdinalIgnoreCase);
    }
}
