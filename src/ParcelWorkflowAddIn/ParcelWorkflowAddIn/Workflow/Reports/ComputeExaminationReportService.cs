using System.IO;
using System.Text;
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
    string? ErrorCategory,
    string? PdfReportPath = null)
{
    public static ComputeExaminationReportResult Succeeded(string reportPath, string? pdfReportPath = null)
    {
        return new ComputeExaminationReportResult(true, "Compute examination report generated.", reportPath, null, pdfReportPath);
    }

    public static ComputeExaminationReportResult Failed(string message, string? errorCategory = null)
    {
        return new ComputeExaminationReportResult(false, message, null, errorCategory);
    }
}

public sealed class ComputeExaminationReportService : IComputeExaminationReportService
{
    public const string ReportFileName = "compute_examination_report.json";
    public const string PdfReportFileName = "compute_examination_report.pdf";

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
            var pdfReportPath = Path.Combine(layout.ReportsDirectory, PdfReportFileName);
            SimplePdfReportWriter.Write(pdfReportPath, report);
            return Task.FromResult(ComputeExaminationReportResult.Succeeded(reportPath, pdfReportPath));
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

    private static class SimplePdfReportWriter
    {
        private const int MaxLineLength = 96;
        private const int MaxLinesPerPage = 45;

        public static void Write(string path, ComputeExaminationReportDocument report)
        {
            var pages = BuildPages(report);
            var objects = new List<string>();
            var pageObjectNumbers = new List<int>();

            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
            objects.Add(string.Empty);
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

            foreach (var pageLines in pages)
            {
                var contentObjectNumber = objects.Count + 2;
                var pageObjectNumber = objects.Count + 1;
                pageObjectNumbers.Add(pageObjectNumber);
                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObjectNumber} 0 R >>");
                objects.Add(BuildContentObject(pageLines));
            }

            objects[1] = $"<< /Type /Pages /Count {pageObjectNumbers.Count} /Kids [{string.Join(" ", pageObjectNumbers.Select(number => $"{number} 0 R"))}] >>";

            File.WriteAllBytes(path, BuildPdfBytes(objects));
        }

        private static IReadOnlyList<IReadOnlyList<string>> BuildPages(ComputeExaminationReportDocument report)
        {
            var lines = new List<string>
            {
                "Compute Examination Report",
                $"Transaction Number: {report.TransactionNumber}",
                $"Transaction Id: {report.TransactionId}",
                $"Task Id: {report.TaskId ?? string.Empty}",
                $"Generated At UTC: {report.GeneratedAtUtc}",
                $"Generated By: {report.GeneratedBy ?? string.Empty}",
                $"Manifest Run Id: {report.ManifestRunId}",
                string.Empty,
                "Stage Summary"
            };

            foreach (var stage in report.Stages)
            {
                lines.Add($"- {stage.StageName}: {stage.Status}");
                if (stage.Findings.Count > 0)
                {
                    var grouped = stage.Findings
                        .GroupBy(finding => NormalizeStatus(finding.Outcome))
                        .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(group => $"{group.Key}={group.Count()}");
                    lines.Add($"  Findings: {string.Join(", ", grouped)}");
                }

                foreach (var blocker in stage.Findings.Where(finding => IsReportableFinding(finding)).Take(4))
                {
                    lines.Add($"  {blocker.Outcome}: {blocker.DisplayName}");
                }
            }

            lines.Add(string.Empty);
            lines.Add("Closeout");
            lines.Add($"Decision: {report.Closeout.Decision}");
            lines.Add($"Operator: {report.Closeout.OperatorId ?? string.Empty}");
            lines.Add($"Enterprise Disposition: {report.Closeout.EnterpriseDispositionStatus}");
            lines.Add($"Spatial Unit Status: {report.Closeout.SpatialUnitApiStatus ?? string.Empty}");
            lines.Add($"Spatial Unit Id: {report.Closeout.SpatialUnitId ?? string.Empty}");
            lines.Add($"Working Package: {report.Closeout.WorkingPackageFileName ?? string.Empty}");
            lines.Add($"Working Package Upload: {report.Closeout.WorkingPackageUploadStatus ?? string.Empty}");
            lines.Add(string.Empty);
            lines.Add("Artifact References");
            lines.AddRange(report.ArtifactReferences.Select(reference => $"- {reference}"));

            var wrapped = lines.SelectMany(WrapLine).ToArray();
            return wrapped
                .Select((line, index) => new { line, index })
                .GroupBy(item => item.index / MaxLinesPerPage)
                .Select(group => (IReadOnlyList<string>)group.Select(item => item.line).ToArray())
                .ToArray();
        }

        private static string BuildContentObject(IReadOnlyList<string> lines)
        {
            var stream = new StringBuilder();
            stream.AppendLine("BT");
            stream.AppendLine("/F1 10 Tf");
            stream.AppendLine("50 750 Td");
            foreach (var line in lines)
            {
                stream.Append('(').Append(EscapePdfText(line)).AppendLine(") Tj");
                stream.AppendLine("0 -15 Td");
            }

            stream.AppendLine("ET");
            var text = stream.ToString();
            return $"<< /Length {Encoding.ASCII.GetByteCount(text)} >>\nstream\n{text}endstream";
        }

        private static byte[] BuildPdfBytes(IReadOnlyList<string> objects)
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
            var offsets = new List<long> { 0 };
            writer.WriteLine("%PDF-1.4");
            for (var i = 0; i < objects.Count; i++)
            {
                writer.Flush();
                offsets.Add(stream.Position);
                writer.WriteLine($"{i + 1} 0 obj");
                writer.WriteLine(objects[i]);
                writer.WriteLine("endobj");
            }

            writer.Flush();
            var xrefOffset = stream.Position;
            writer.WriteLine("xref");
            writer.WriteLine($"0 {objects.Count + 1}");
            writer.WriteLine("0000000000 65535 f ");
            foreach (var offset in offsets.Skip(1))
            {
                writer.WriteLine($"{offset:0000000000} 00000 n ");
            }

            writer.WriteLine("trailer");
            writer.WriteLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
            writer.WriteLine("startxref");
            writer.WriteLine(xrefOffset);
            writer.WriteLine("%%EOF");
            writer.Flush();
            return stream.ToArray();
        }

        private static IEnumerable<string> WrapLine(string line)
        {
            if (line.Length <= MaxLineLength)
            {
                yield return line;
                yield break;
            }

            var remaining = line;
            while (remaining.Length > MaxLineLength)
            {
                var splitAt = remaining.LastIndexOf(' ', MaxLineLength);
                if (splitAt <= 0)
                {
                    splitAt = MaxLineLength;
                }

                yield return remaining[..splitAt];
                remaining = remaining[splitAt..].TrimStart();
            }

            if (remaining.Length > 0)
            {
                yield return remaining;
            }
        }

        private static string EscapePdfText(string text)
        {
            return text
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);
        }

        private static bool IsReportableFinding(ComputeExaminationReportFinding finding)
        {
            return string.Equals(finding.Severity, "blocker", StringComparison.OrdinalIgnoreCase)
                || string.Equals(finding.Severity, "warning", StringComparison.OrdinalIgnoreCase)
                || string.Equals(finding.Outcome, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(finding.Outcome, "warning", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeStatus(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }
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
