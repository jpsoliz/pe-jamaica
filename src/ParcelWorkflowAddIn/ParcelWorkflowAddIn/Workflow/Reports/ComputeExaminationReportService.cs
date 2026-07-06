using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow.Disposition;
using ParcelWorkflowAddIn.Workflow.Output;
using ParcelWorkflowAddIn.Workflow.SpatialReview;

namespace ParcelWorkflowAddIn.Workflow.Reports;

public interface IComputeExaminationReportService
{
    Task<ComputeExaminationReportResult> GenerateAsync(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        ComputeReviewDispositionDocument disposition,
        string? operatorId,
        CancellationToken cancellationToken = default);
}

public sealed record ComputeExaminationReportResult(
    bool Success,
    string Message,
    string? ReportPath,
    string? ErrorCategory)
{
    public static ComputeExaminationReportResult Succeeded(string reportPath)
    {
        return new ComputeExaminationReportResult(true, "Compute examination report generated.", reportPath, null);
    }

    public static ComputeExaminationReportResult Failed(string message, string? errorCategory = null)
    {
        return new ComputeExaminationReportResult(false, message, null, errorCategory);
    }
}

public sealed class ComputeExaminationReportService : IComputeExaminationReportService
{
    public const string ReportFileName = "compute_examination_report.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public Task<ComputeExaminationReportResult> GenerateAsync(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        ComputeReviewDispositionDocument disposition,
        string? operatorId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var manifest = RequireManifest(layout);
            var outputSummary = RequireJsonArtifact(layout, layout.OutputDirectory, "output_summary.json", "Create Spatial Units");
            var enterprisePublish = RequireJsonArtifact(layout, layout.OutputDirectory, "enterprise_working_publish.json", "Enterprise working-layer publish");
            var enterpriseDisposition = RequireJsonArtifact(layout, layout.WorkingDirectory, "enterprise_working_disposition.json", "Enterprise disposition writeback");
            var spatialReviewApproval = RequireJsonArtifact(layout, layout.WorkingDirectory, "spatial_review_approval.json", "Final Review");
            var dispositionArtifact = RequireJsonArtifact(layout, layout.WorkingDirectory, ComputeReviewDispositionPersistenceService.DispositionArtifactFileName, "Compute disposition");

            var report = new ComputeExaminationReportDocument(
                "compute_examination_report_v1",
                transaction.TransactionId,
                transaction.TransactionNumber,
                transaction.TaskId,
                DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
                operatorId,
                manifest.RunId,
                new[]
                {
                    BuildPreflightStage(layout.StructureCheckSummaryPath, "structure_check", "Structure Check"),
                    BuildPreflightStage(layout.GeoreferenceCheckSummaryPath, "georeference_check", "Georeference Check"),
                    BuildPreflightStage(layout.DimensionCheckSummaryPath, "dimension_check", "Dimension Check"),
                    BuildArtifactStage("validate_points_and_lines", "Validate Points and Lines", layout, Path.Combine(layout.WorkingDirectory, "approved_review.json")),
                    BuildJsonStage("create_spatial_units", "Create Spatial Units", outputSummary),
                    BuildJsonStage("final_review", "Final Review", spatialReviewApproval),
                    BuildJsonStage("enterprise_working_publish", "Enterprise working-layer publish", enterprisePublish),
                    BuildJsonStage("enterprise_disposition", "Enterprise disposition writeback", enterpriseDisposition),
                    BuildJsonStage("innola_spatial_unit", "Innola Spatial Unit creation/update", dispositionArtifact),
                    BuildJsonStage("working_package_attachment", "Working package attachment", dispositionArtifact)
                },
                new ComputeExaminationReportCloseout(
                    disposition.Decision,
                    disposition.OperatorId,
                    disposition.DecidedAtUtc,
                    disposition.EnterpriseDispositionStatus,
                    disposition.EnterpriseDispositionRef,
                    disposition.SpatialUnitApiStatus,
                    disposition.SpatialUnitId,
                    disposition.WorkingPackageFileName,
                    disposition.WorkingPackageSourceType,
                    disposition.WorkingPackageUploadStatus),
                new[]
                {
                    MakeReference(layout, layout.ManifestPath),
                    MakeReference(layout, layout.StructureCheckSummaryPath),
                    MakeReference(layout, layout.GeoreferenceCheckSummaryPath),
                    MakeReference(layout, layout.DimensionCheckSummaryPath),
                    MakeReference(layout, Path.Combine(layout.WorkingDirectory, "approved_review.json")),
                    MakeReference(layout, Path.Combine(layout.WorkingDirectory, "spatial_review_approval.json")),
                    MakeReference(layout, Path.Combine(layout.WorkingDirectory, "enterprise_working_disposition.json")),
                    MakeReference(layout, Path.Combine(layout.WorkingDirectory, ComputeReviewDispositionPersistenceService.DispositionArtifactFileName)),
                    MakeReference(layout, Path.Combine(layout.OutputDirectory, "output_summary.json")),
                    MakeReference(layout, Path.Combine(layout.OutputDirectory, "enterprise_working_publish.json"))
                });

            Directory.CreateDirectory(layout.ReportsDirectory);
            var reportPath = Path.Combine(layout.ReportsDirectory, ReportFileName);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions));
            return Task.FromResult(ComputeExaminationReportResult.Succeeded(reportPath));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidOperationException
            or NotSupportedException)
        {
            return Task.FromResult(ComputeExaminationReportResult.Failed(
                $"Compute examination report could not be generated: {exception.Message}",
                exception.GetType().Name));
        }
    }

    private static ManifestDocument RequireManifest(CaseFolderLayout layout)
    {
        if (!File.Exists(layout.ManifestPath))
        {
            throw new InvalidOperationException("manifest.json is missing.");
        }

        return ManifestSerializer.Read(layout.ManifestPath);
    }

    private static JsonDocument RequireJsonArtifact(CaseFolderLayout layout, string directory, string fileName, string stageName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"{stageName} evidence is missing: {Path.GetRelativePath(layout.RootDirectory, path)}.");
        }

        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static ComputeExaminationReportStage BuildPreflightStage(string path, string stageId, string stageName)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"{stageName} findings are missing.");
        }

        var summary = PreflightSummarySerializer.Read(path);
        var findings = summary.Payload.Blockers
            .Concat(summary.Payload.Warnings)
            .Concat(summary.Payload.PassedChecks)
            .Select(check => new ComputeExaminationReportFinding(
                stageId,
                check.CheckId,
                check.DisplayName ?? check.CheckId,
                check.Outcome ?? check.Status,
                check.Severity,
                check.WorkflowEffect,
                check.Message,
                check.Correction,
                check.AffectedPath,
                check.SourceRole,
                check.Evidence))
            .ToArray();

        return new ComputeExaminationReportStage(
            stageId,
            stageName,
            summary.Payload.Status,
            summary.CreatedBy,
            summary.CreatedAt,
            summary.RunId,
            findings,
            Array.Empty<string>());
    }

    private static ComputeExaminationReportStage BuildArtifactStage(string stageId, string stageName, CaseFolderLayout layout, string artifactPath)
    {
        if (!File.Exists(artifactPath))
        {
            throw new InvalidOperationException($"{stageName} evidence is missing: {Path.GetRelativePath(layout.RootDirectory, artifactPath)}.");
        }

        var info = new FileInfo(artifactPath);
        return new ComputeExaminationReportStage(
            stageId,
            stageName,
            "available",
            null,
            info.LastWriteTimeUtc.ToString("O"),
            null,
            Array.Empty<ComputeExaminationReportFinding>(),
            new[] { MakeReference(layout, artifactPath) });
    }

    private static ComputeExaminationReportStage BuildJsonStage(string stageId, string stageName, JsonDocument document)
    {
        var root = document.RootElement;
        var status = ReadString(root, "status")
            ?? ReadString(root, "decision")
            ?? ReadString(root, "enterprise_disposition_status")
            ?? ReadString(root, "spatial_unit_api_status")
            ?? ReadString(root, "working_package_upload_status")
            ?? "available";

        return new ComputeExaminationReportStage(
            stageId,
            stageName,
            status,
            ReadString(root, "created_by") ?? ReadString(root, "operator_id") ?? ReadString(root, "published_by"),
            ReadString(root, "created_at") ?? ReadString(root, "decided_at_utc") ?? ReadString(root, "published_at"),
            ReadString(root, "run_id") ?? ReadString(root, "publish_run_id"),
            Array.Empty<ComputeExaminationReportFinding>(),
            Array.Empty<string>());
    }

    private static string MakeReference(CaseFolderLayout layout, string path)
    {
        return Path.GetRelativePath(layout.RootDirectory, path).Replace('\\', '/');
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }
}

public sealed record ComputeExaminationReportDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("task_id")] string? TaskId,
    [property: JsonPropertyName("generated_at_utc")] string GeneratedAtUtc,
    [property: JsonPropertyName("generated_by")] string? GeneratedBy,
    [property: JsonPropertyName("manifest_run_id")] string ManifestRunId,
    [property: JsonPropertyName("stages")] IReadOnlyList<ComputeExaminationReportStage> Stages,
    [property: JsonPropertyName("closeout")] ComputeExaminationReportCloseout Closeout,
    [property: JsonPropertyName("artifact_references")] IReadOnlyList<string> ArtifactReferences);

public sealed record ComputeExaminationReportStage(
    [property: JsonPropertyName("stage_id")] string StageId,
    [property: JsonPropertyName("stage_name")] string StageName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("operator_id")] string? OperatorId,
    [property: JsonPropertyName("timestamp_utc")] string? TimestampUtc,
    [property: JsonPropertyName("run_id")] string? RunId,
    [property: JsonPropertyName("findings")] IReadOnlyList<ComputeExaminationReportFinding> Findings,
    [property: JsonPropertyName("artifact_references")] IReadOnlyList<string> ArtifactReferences);

public sealed record ComputeExaminationReportFinding(
    [property: JsonPropertyName("stage_id")] string StageId,
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("workflow_effect")] string? WorkflowEffect,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("correction")] string? Correction,
    [property: JsonPropertyName("affected_path")] string? AffectedPath,
    [property: JsonPropertyName("source_role")] string? SourceRole,
    [property: JsonPropertyName("evidence")] IReadOnlyDictionary<string, IReadOnlyList<string>>? Evidence);

public sealed record ComputeExaminationReportCloseout(
    [property: JsonPropertyName("decision")] string Decision,
    [property: JsonPropertyName("operator_id")] string? OperatorId,
    [property: JsonPropertyName("decided_at_utc")] string DecidedAtUtc,
    [property: JsonPropertyName("enterprise_disposition_status")] string EnterpriseDispositionStatus,
    [property: JsonPropertyName("enterprise_disposition_ref")] string? EnterpriseDispositionRef,
    [property: JsonPropertyName("spatial_unit_api_status")] string? SpatialUnitApiStatus,
    [property: JsonPropertyName("spatial_unit_id")] string? SpatialUnitId,
    [property: JsonPropertyName("working_package_file_name")] string? WorkingPackageFileName,
    [property: JsonPropertyName("working_package_source_type")] string? WorkingPackageSourceType,
    [property: JsonPropertyName("working_package_upload_status")] string? WorkingPackageUploadStatus);
