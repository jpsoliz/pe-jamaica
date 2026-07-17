using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Compare;

public sealed class CompareReviewReportService
{
    public const string ReportFileName = "compare_review_report.json";
    public const string PdfReportFileName = "compare_review_report.pdf";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly Func<DateTimeOffset> getUtcNow;

    public CompareReviewReportService(Func<DateTimeOffset>? getUtcNow = null)
    {
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public CompareReviewReportResult Generate(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        CompareReviewDraftDocument draft)
    {
        try
        {
            Directory.CreateDirectory(layout.ReportsDirectory);
            var report = new CompareReviewReportDocument(
                "1.0.0",
                transaction.TransactionId,
                transaction.TransactionNumber,
                transaction.TaskId,
                transaction.TaskName,
                draft.ReviewerId,
                draft.ReviewerDisplayName,
                getUtcNow().UtcDateTime.ToString("O"),
                draft.DecisionState,
                draft.Notes,
                draft.LegalEvidenceReviewed,
                draft.FiscalEvidenceReviewed,
                draft.SurveyPlanSummary,
                draft.LegalCadasterSummary,
                draft.FiscalNeighborSummary,
                draft.ManualQueryHistory ?? Array.Empty<CompareEvidenceSearchResultDraft>(),
                draft.ValuableEvidence ?? Array.Empty<CompareValuableEvidenceDraft>(),
                draft.EnterpriseCadasterEvidence ?? Array.Empty<CompareEnterpriseCadasterEvidenceDraft>(),
                draft.Discrepancies,
                new[]
                {
                    MakeReference(layout, Path.Combine(layout.WorkingDirectory, "compare_review_draft.json"))
                });

            var path = Path.Combine(layout.ReportsDirectory, ReportFileName);
            File.WriteAllText(path, JsonSerializer.Serialize(Redact(report), JsonOptions));
            var pdfPath = Path.Combine(layout.ReportsDirectory, PdfReportFileName);
            SimplePdfReportWriter.Write(pdfPath, report);
            return CompareReviewReportResult.Succeeded(
                path,
                Path.GetRelativePath(layout.RootDirectory, path),
                pdfPath,
                Path.GetRelativePath(layout.RootDirectory, pdfPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return CompareReviewReportResult.Failed($"Compare report could not be generated: {exception.Message}");
        }
    }

    private static CompareReviewArtifactReference MakeReference(CaseFolderLayout layout, string path)
    {
        return new CompareReviewArtifactReference(
            Path.GetFileName(path),
            Path.GetRelativePath(layout.RootDirectory, path).Replace('\\', '/'));
    }

    private static CompareReviewReportDocument Redact(CompareReviewReportDocument report)
    {
        return report with
        {
            Notes = LegalCadasterQueryResult.Redact(report.Notes),
            SurveyPlanSummary = LegalCadasterQueryResult.Redact(report.SurveyPlanSummary),
            LegalCadasterSummary = LegalCadasterQueryResult.Redact(report.LegalCadasterSummary),
            FiscalNeighborSummary = LegalCadasterQueryResult.Redact(report.FiscalNeighborSummary),
            ValuableEvidence = report.ValuableEvidence.Select(evidence => evidence with
            {
                DisplaySummary = LegalCadasterQueryResult.Redact(evidence.DisplaySummary),
                Diagnostic = LegalCadasterQueryResult.Redact(evidence.Diagnostic)
            }).ToArray()
        };
    }

    private static class SimplePdfReportWriter
    {
        private const int MaxLineLength = 96;
        private const int MaxLinesPerPage = 45;

        public static void Write(string path, CompareReviewReportDocument report)
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

        private static IReadOnlyList<IReadOnlyList<string>> BuildPages(CompareReviewReportDocument report)
        {
            var lines = new List<string>
            {
                "Compare Review Report",
                $"Transaction Number: {report.TransactionNumber}",
                $"Transaction Id: {report.TransactionId}",
                $"Task Id: {report.TaskId}",
                $"Task Name: {report.TaskName}",
                $"Generated At UTC: {report.GeneratedAtUtc}",
                $"Reviewer: {report.ReviewerDisplayName ?? report.ReviewerId ?? string.Empty}",
                $"Decision: {report.DecisionState}",
                string.Empty,
                "Valuable Evidence"
            };

            if (report.ValuableEvidence.Count == 0)
            {
                lines.Add("None retained.");
            }
            else
            {
                lines.AddRange(report.ValuableEvidence.Select((evidence, index) =>
                    $"{index + 1}. {evidence.RoleTag}: {evidence.DisplaySummary}"));
            }

            lines.Add(string.Empty);
            lines.Add("Notes");
            lines.Add(string.IsNullOrWhiteSpace(report.Notes) ? "(none)" : report.Notes);

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
    }
}

public sealed record CompareReviewReportResult(
    bool Success,
    string Message,
    string? ReportPath,
    string? RelativePath,
    string? PdfReportPath,
    string? PdfRelativePath)
{
    public static CompareReviewReportResult Succeeded(string reportPath, string relativePath, string pdfReportPath, string pdfRelativePath)
    {
        return new CompareReviewReportResult(
            true,
            "Compare review report generated.",
            reportPath,
            relativePath.Replace('\\', '/'),
            pdfReportPath,
            pdfRelativePath.Replace('\\', '/'));
    }

    public static CompareReviewReportResult Failed(string message)
    {
        return new CompareReviewReportResult(false, message, null, null, null, null);
    }
}

public sealed record CompareReviewReportDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("task_id")] string TaskId,
    [property: JsonPropertyName("task_name")] string TaskName,
    [property: JsonPropertyName("reviewer_id")] string? ReviewerId,
    [property: JsonPropertyName("reviewer_display_name")] string? ReviewerDisplayName,
    [property: JsonPropertyName("generated_at_utc")] string GeneratedAtUtc,
    [property: JsonPropertyName("decision_state")] string DecisionState,
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("legal_evidence_reviewed")] bool LegalEvidenceReviewed,
    [property: JsonPropertyName("fiscal_evidence_reviewed")] bool FiscalEvidenceReviewed,
    [property: JsonPropertyName("survey_plan_summary")] string SurveyPlanSummary,
    [property: JsonPropertyName("legal_cadaster_summary")] string LegalCadasterSummary,
    [property: JsonPropertyName("fiscal_neighbor_summary")] string FiscalNeighborSummary,
    [property: JsonPropertyName("manual_query_history")] IReadOnlyList<CompareEvidenceSearchResultDraft> ManualQueryHistory,
    [property: JsonPropertyName("valuable_evidence")] IReadOnlyList<CompareValuableEvidenceDraft> ValuableEvidence,
    [property: JsonPropertyName("enterprise_cadaster_evidence")] IReadOnlyList<CompareEnterpriseCadasterEvidenceDraft> EnterpriseCadasterEvidence,
    [property: JsonPropertyName("discrepancies")] IReadOnlyList<CompareDiscrepancyDraft> Discrepancies,
    [property: JsonPropertyName("artifact_refs")] IReadOnlyList<CompareReviewArtifactReference> ArtifactRefs);

public sealed record CompareReviewArtifactReference(
    [property: JsonPropertyName("artifact_type")] string ArtifactType,
    [property: JsonPropertyName("relative_path")] string RelativePath);
