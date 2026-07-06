using System.Text.Json.Serialization;
using ParcelWorkflowAddIn.Workflow.Output;

namespace ParcelWorkflowAddIn.Workflow.Disposition;

public enum ComputeReviewDecision
{
    Pending,
    Approved,
    Rejected,
    Postponed
}

public static class ComputeReviewDecisionExtensions
{
    public static string ToContractValue(this ComputeReviewDecision decision)
    {
        return decision switch
        {
            ComputeReviewDecision.Approved => "approved",
            ComputeReviewDecision.Rejected => "rejected",
            ComputeReviewDecision.Postponed => "postponed",
            _ => "pending"
        };
    }
}

public sealed record ComputeReviewDispositionDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("task_id")] string? TaskId,
    [property: JsonPropertyName("decision")] string Decision,
    [property: JsonPropertyName("comment")] string? Comment,
    [property: JsonPropertyName("operator_id")] string? OperatorId,
    [property: JsonPropertyName("decided_at_utc")] string DecidedAtUtc,
    [property: JsonPropertyName("output_summary_ref")] string? OutputSummaryRef,
    [property: JsonPropertyName("enterprise_publish_ref")] string? EnterprisePublishRef,
    [property: JsonPropertyName("publish_run_id")] string? PublishRunId,
    [property: JsonPropertyName("enterprise_disposition_status")] string EnterpriseDispositionStatus,
    [property: JsonPropertyName("enterprise_disposition_ref")] string? EnterpriseDispositionRef,
    [property: JsonPropertyName("spatial_unit_api_status")] string? SpatialUnitApiStatus,
    [property: JsonPropertyName("spatial_unit_id")] string? SpatialUnitId,
    [property: JsonPropertyName("working_package_file_name")] string? WorkingPackageFileName,
    [property: JsonPropertyName("working_package_source_type")] string? WorkingPackageSourceType,
    [property: JsonPropertyName("working_package_upload_status")] string? WorkingPackageUploadStatus,
    [property: JsonPropertyName("compute_examination_report_ref")] string? ComputeExaminationReportRef = null);

public sealed record ComputeReviewDispositionRequest(
    ComputeReviewDecision Decision,
    string? Comment,
    string? OperatorId,
    OutputSummaryDocument OutputSummary,
    EnterpriseWorkingPublishSummary EnterprisePublish,
    string? OutputSummaryPath,
    string? EnterprisePublishPath);

public sealed record ComputeReviewDispositionResult(
    bool Success,
    string Message,
    string? ArtifactPath,
    ComputeReviewDispositionDocument? Document)
{
    public static ComputeReviewDispositionResult Succeeded(string message, string artifactPath, ComputeReviewDispositionDocument document)
    {
        return new ComputeReviewDispositionResult(true, message, artifactPath, document);
    }

    public static ComputeReviewDispositionResult Failed(string message)
    {
        return new ComputeReviewDispositionResult(false, message, null, null);
    }
}
