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
        private const double PageWidth = 612;
        private const double PageHeight = 792;
        private const double MarginX = 42;
        private const double TopY = 748;
        private const double BottomY = 56;
        private const double UsableWidth = PageWidth - (MarginX * 2);

        private const double PrimaryR = 0.094;
        private const double PrimaryG = 0.204;
        private const double PrimaryB = 0.290;
        private const double BorderR = 0.792;
        private const double BorderG = 0.835;
        private const double BorderB = 0.862;
        private const double AlternateR = 0.965;
        private const double AlternateG = 0.980;
        private const double AlternateB = 0.984;
        private const double MutedR = 0.310;
        private const double MutedG = 0.380;
        private const double MutedB = 0.420;

        public static void Write(string path, CompareReviewReportDocument report)
        {
            var pages = new PdfReportRenderer(report).Render();
            var objects = new List<string>();
            var pageObjectNumbers = new List<int>();

            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
            objects.Add(string.Empty);
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");

            foreach (var pageContent in pages)
            {
                var contentObjectNumber = objects.Count + 2;
                var pageObjectNumber = objects.Count + 1;
                pageObjectNumbers.Add(pageObjectNumber);
                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObjectNumber} 0 R >>");
                objects.Add(BuildContentObject(pageContent.Content));
            }

            objects[1] = $"<< /Type /Pages /Count {pageObjectNumbers.Count} /Kids [{string.Join(" ", pageObjectNumbers.Select(number => $"{number} 0 R"))}] >>";
            File.WriteAllBytes(path, BuildPdfBytes(objects));
        }

        private static string BuildContentObject(string content)
        {
            return $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream";
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

        private static string EscapePdfText(string text)
        {
            return text
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);
        }

        private static string PdfNumber(double value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        private sealed record PdfPageContent(string Content);

        private sealed record PdfColumn(string Header, double Width);

        private sealed class PdfReportRenderer
        {
            private readonly CompareReviewReportDocument _report;
            private readonly List<PdfPageContent> _pages = new();
            private StringBuilder _stream = new();
            private double _y;

            public PdfReportRenderer(CompareReviewReportDocument report)
            {
                _report = report;
            }

            public IReadOnlyList<PdfPageContent> Render()
            {
                BeginPage(includeRunningHeader: false);
                DrawReportHeader();
                DrawSummaryStrip();

                DrawSection("Executive Summary");
                DrawKeyValueTable(new[]
                {
                    ("Decision", _report.DecisionState),
                    ("Transaction", _report.TransactionNumber),
                    ("Generated At UTC", _report.GeneratedAtUtc),
                    ("Reviewer", _report.ReviewerDisplayName ?? _report.ReviewerId ?? "Not provided"),
                    ("Legal Evidence Reviewed", _report.LegalEvidenceReviewed ? "Yes" : "No"),
                    ("Fiscal Evidence Reviewed", _report.FiscalEvidenceReviewed ? "Yes" : "No")
                });

                DrawSection("Transaction Info");
                DrawKeyValueTable(new[]
                {
                    ("Transaction Number", _report.TransactionNumber),
                    ("Transaction Id", _report.TransactionId),
                    ("Task Id", _report.TaskId),
                    ("Task Name", _report.TaskName)
                });

                DrawSection("Compare Evidence Summary");
                DrawKeyValueTable(new[]
                {
                    ("Survey Plan", EmptyToNone(_report.SurveyPlanSummary)),
                    ("Legal Cadaster", EmptyToNone(_report.LegalCadasterSummary)),
                    ("Fiscal / Neighbor", EmptyToNone(_report.FiscalNeighborSummary))
                });

                DrawSection("Valuable Evidence");
                DrawTable(
                    new[]
                    {
                        new PdfColumn("Role", 86),
                        new PdfColumn("Source", 110),
                        new PdfColumn("Summary", 252),
                        new PdfColumn("Captured", 80)
                    },
                    _report.ValuableEvidence.Count == 0
                        ? new[] { new[] { "None retained.", string.Empty, string.Empty, string.Empty } }
                        : _report.ValuableEvidence.Select((evidence, index) => new[]
                        {
                            $"{index + 1}. {evidence.RoleTag}",
                            evidence.SourceLabel,
                            evidence.DisplaySummary,
                            evidence.CapturedAtUtc
                        }));

                DrawSection("Enterprise Cadaster Evidence");
                DrawTable(
                    new[]
                    {
                        new PdfColumn("Source", 86),
                        new PdfColumn("Owner", 126),
                        new PdfColumn("Parcel / PID", 92),
                        new PdfColumn("Vol./Folio", 76),
                        new PdfColumn("Relationship", 82),
                        new PdfColumn("Status", 66)
                    },
                    _report.EnterpriseCadasterEvidence.Count == 0
                        ? new[] { new[] { "No enterprise cadaster evidence retained.", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty } }
                        : _report.EnterpriseCadasterEvidence.Select(evidence => new[]
                        {
                            evidence.SourceLabel,
                            FirstNonEmpty(evidence.OwnerName, evidence.OccupantName, evidence.TaxpayerName),
                            FirstNonEmpty(evidence.ParcelId, evidence.Pid, evidence.Suid),
                            JoinParts(evidence.Volume, evidence.Folio, "/"),
                            evidence.SpatialRelationship,
                            evidence.Status
                        }));

                DrawSection("Manual Query History");
                DrawTable(
                    new[]
                    {
                        new PdfColumn("Source", 98),
                        new PdfColumn("Name / Parcel", 136),
                        new PdfColumn("Vol./Folio", 70),
                        new PdfColumn("Parish", 72),
                        new PdfColumn("Status", 82),
                        new PdfColumn("Diagnostic", 70)
                    },
                    _report.ManualQueryHistory.Count == 0
                        ? new[] { new[] { "No manual query history recorded.", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty } }
                        : _report.ManualQueryHistory.Select(query => new[]
                        {
                            query.SourceLabel,
                            FirstNonEmpty(query.DisplayName, query.ParcelId, query.LandValuationNumber),
                            JoinParts(query.Volume, query.Folio, "/"),
                            query.Parish ?? string.Empty,
                            query.Status,
                            query.Diagnostic ?? string.Empty
                        }));

                DrawSection("Discrepancies");
                DrawTable(
                    new[]
                    {
                        new PdfColumn("Title", 206),
                        new PdfColumn("Source", 122),
                        new PdfColumn("Status", 100),
                        new PdfColumn("Resolved", 100)
                    },
                    _report.Discrepancies.Count == 0
                        ? new[] { new[] { "No discrepancies recorded.", string.Empty, string.Empty, string.Empty } }
                        : _report.Discrepancies.Select(discrepancy => new[]
                        {
                            discrepancy.Title,
                            discrepancy.Source,
                            discrepancy.Status,
                            discrepancy.IsResolved ? "Yes" : "No"
                        }));

                DrawSection("Notes");
                DrawKeyValueTable(new[] { ("Reviewer Notes", EmptyToNone(_report.Notes)) });

                DrawSection("Artifact References");
                DrawTable(
                    new[]
                    {
                        new PdfColumn("Artifact", 160),
                        new PdfColumn("Relative Path", UsableWidth - 160)
                    },
                    _report.ArtifactRefs.Count == 0
                        ? new[] { new[] { "No artifact references recorded.", string.Empty } }
                        : _report.ArtifactRefs.Select(reference => new[] { reference.ArtifactType, reference.RelativePath }));

                FinishPage();
                return _pages;
            }

            private void BeginPage(bool includeRunningHeader)
            {
                _stream = new StringBuilder();
                _y = TopY;
                if (includeRunningHeader)
                {
                    DrawText("Compare Review Report", MarginX, _y, 8, bold: true, PrimaryR, PrimaryG, PrimaryB);
                    _y -= 18;
                }
            }

            private void FinishPage()
            {
                DrawFooter();
                _pages.Add(new PdfPageContent(_stream.ToString()));
            }

            private void NewPage()
            {
                FinishPage();
                BeginPage(includeRunningHeader: true);
            }

            private void EnsureSpace(double requiredHeight)
            {
                if (_y - requiredHeight < BottomY)
                {
                    NewPage();
                }
            }

            private void DrawReportHeader()
            {
                EnsureSpace(76);
                DrawText("Compare Review Report", MarginX, _y, 22, bold: true, PrimaryR, PrimaryG, PrimaryB);
                _y -= 22;
                DrawText($"NLA Transaction {_report.TransactionNumber} - {_report.TaskName}", MarginX, _y, 10, bold: true, MutedR, MutedG, MutedB);
                _y -= 14;
                DrawText($"Generated {_report.GeneratedAtUtc} by {_report.ReviewerDisplayName ?? _report.ReviewerId ?? "Not provided"}", MarginX, _y, 8, bold: false, MutedR, MutedG, MutedB);
                _y -= 18;
                DrawRule();
                _y -= 12;
            }

            private void DrawSummaryStrip()
            {
                EnsureSpace(54);
                var values = new[]
                {
                    ("Transaction", _report.TransactionNumber),
                    ("Task", _report.TaskName),
                    ("Decision", _report.DecisionState),
                    ("Evidence", $"{_report.ValuableEvidence.Count} retained")
                };
                var boxWidth = UsableWidth / values.Length;
                for (var i = 0; i < values.Length; i++)
                {
                    var x = MarginX + (i * boxWidth);
                    DrawRect(x, _y - 42, boxWidth - 4, 38, fill: true, r: AlternateR, g: AlternateG, b: AlternateB);
                    DrawRect(x, _y - 42, boxWidth - 4, 38, stroke: true, r: BorderR, g: BorderG, b: BorderB);
                    DrawText(values[i].Item1, x + 6, _y - 16, 7, bold: true, MutedR, MutedG, MutedB);
                    DrawText(values[i].Item2, x + 6, _y - 31, 9, bold: true);
                }

                _y -= 54;
            }

            private void DrawSection(string title)
            {
                EnsureSpace(32);
                _y -= 4;
                DrawText(title, MarginX, _y, 12, bold: true, PrimaryR, PrimaryG, PrimaryB);
                _y -= 8;
                DrawRule();
                _y -= 12;
            }

            private void DrawKeyValueTable(IEnumerable<(string Field, string Value)> rows)
            {
                DrawTable(
                    new[]
                    {
                        new PdfColumn("Field", 170),
                        new PdfColumn("Value", UsableWidth - 170)
                    },
                    rows.Select(row => new[] { row.Field, row.Value }));
            }

            private void DrawTable(IReadOnlyList<PdfColumn> columns, IEnumerable<IReadOnlyList<string>> rowValues)
            {
                var rows = rowValues.ToArray();
                EnsureSpace(24);
                DrawRect(MarginX, _y - 18, columns.Sum(column => column.Width), 18, fill: true, r: PrimaryR, g: PrimaryG, b: PrimaryB);
                var x = MarginX;
                foreach (var column in columns)
                {
                    DrawText(column.Header, x + 4, _y - 12, 7.5, bold: true, 1, 1, 1);
                    x += column.Width;
                }

                _y -= 18;

                for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
                {
                    var row = rows[rowIndex];
                    var wrappedCells = new List<IReadOnlyList<string>>();
                    for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                    {
                        var value = columnIndex < row.Count ? row[columnIndex] : string.Empty;
                        wrappedCells.Add(WrapText(value, columns[columnIndex].Width - 8, 8));
                    }

                    var lineCount = Math.Max(1, wrappedCells.Max(cell => cell.Count));
                    var rowHeight = Math.Max(17, 7 + (lineCount * 10));
                    EnsureSpace(rowHeight + 2);
                    if (rowIndex % 2 == 0)
                    {
                        DrawRect(MarginX, _y - rowHeight, columns.Sum(column => column.Width), rowHeight, fill: true, r: AlternateR, g: AlternateG, b: AlternateB);
                    }

                    DrawRect(MarginX, _y - rowHeight, columns.Sum(column => column.Width), rowHeight, stroke: true, r: BorderR, g: BorderG, b: BorderB);
                    x = MarginX;
                    for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                    {
                        var textY = _y - 11;
                        foreach (var line in wrappedCells[columnIndex])
                        {
                            DrawText(line, x + 4, textY, 8);
                            textY -= 10;
                        }

                        x += columns[columnIndex].Width;
                    }

                    _y -= rowHeight;
                }

                _y -= 10;
            }

            private void DrawRule()
            {
                DrawRuleAt(_y);
            }

            private void DrawFooter()
            {
                var pageNumber = _pages.Count + 1;
                DrawRuleAt(BottomY - 12);
                DrawText($"Compare Review Report - Transaction {_report.TransactionNumber}", MarginX, BottomY - 28, 7, bold: false, MutedR, MutedG, MutedB);
                DrawText($"Page {pageNumber}", PageWidth - MarginX - 38, BottomY - 28, 7, bold: false, MutedR, MutedG, MutedB);
            }

            private void DrawRuleAt(double y)
            {
                _stream
                    .Append(PdfNumber(BorderR)).Append(' ')
                    .Append(PdfNumber(BorderG)).Append(' ')
                    .Append(PdfNumber(BorderB)).Append(" RG ")
                    .Append(PdfNumber(MarginX)).Append(' ')
                    .Append(PdfNumber(y)).Append(" m ")
                    .Append(PdfNumber(PageWidth - MarginX)).Append(' ')
                    .Append(PdfNumber(y)).AppendLine(" l S");
            }

            private void DrawRect(double x, double y, double width, double height, bool fill = false, bool stroke = false, double r = 0, double g = 0, double b = 0)
            {
                if (fill)
                {
                    _stream
                        .Append(PdfNumber(r)).Append(' ')
                        .Append(PdfNumber(g)).Append(' ')
                        .Append(PdfNumber(b)).Append(" rg ")
                        .Append(PdfNumber(x)).Append(' ')
                        .Append(PdfNumber(y)).Append(' ')
                        .Append(PdfNumber(width)).Append(' ')
                        .Append(PdfNumber(height)).AppendLine(" re f");
                }

                if (stroke)
                {
                    _stream
                        .Append(PdfNumber(r)).Append(' ')
                        .Append(PdfNumber(g)).Append(' ')
                        .Append(PdfNumber(b)).Append(" RG ")
                        .Append(PdfNumber(x)).Append(' ')
                        .Append(PdfNumber(y)).Append(' ')
                        .Append(PdfNumber(width)).Append(' ')
                        .Append(PdfNumber(height)).AppendLine(" re S");
                }
            }

            private void DrawText(string text, double x, double y, double fontSize, bool bold = false, double r = 0, double g = 0, double b = 0)
            {
                var safe = EscapePdfText(SanitizeText(text));
                _stream
                    .Append("BT /").Append(bold ? "F2" : "F1").Append(' ')
                    .Append(PdfNumber(fontSize)).Append(" Tf ")
                    .Append(PdfNumber(r)).Append(' ')
                    .Append(PdfNumber(g)).Append(' ')
                    .Append(PdfNumber(b)).Append(" rg ")
                    .Append(PdfNumber(x)).Append(' ')
                    .Append(PdfNumber(y)).Append(" Td (")
                    .Append(safe)
                    .AppendLine(") Tj ET");
            }

            private static IReadOnlyList<string> WrapText(string text, double width, double fontSize)
            {
                var clean = SanitizeText(text);
                if (string.IsNullOrWhiteSpace(clean))
                {
                    return new[] { string.Empty };
                }

                var maxChars = Math.Max(8, (int)Math.Floor(width / (fontSize * 0.55)));
                var lines = new List<string>();
                var remaining = clean;
                while (remaining.Length > maxChars)
                {
                    var splitAt = remaining.LastIndexOf(' ', maxChars);
                    if (splitAt <= 0)
                    {
                        splitAt = maxChars;
                    }

                    lines.Add(remaining[..splitAt]);
                    remaining = remaining[splitAt..].TrimStart();
                }

                if (remaining.Length > 0)
                {
                    lines.Add(remaining);
                }

                return lines;
            }

            private static string EmptyToNone(string? value)
            {
                return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
            }

            private static string FirstNonEmpty(params string?[] values)
            {
                return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
            }

            private static string JoinParts(string? first, string? second, string separator)
            {
                if (string.IsNullOrWhiteSpace(first))
                {
                    return second ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(second))
                {
                    return first;
                }

                return $"{first}{separator}{second}";
            }

            private static string SanitizeText(string? text)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return string.Empty;
                }

                return text
                    .Replace("\r", " ", StringComparison.Ordinal)
                    .Replace("\n", " ", StringComparison.Ordinal)
                    .Replace("–", "-", StringComparison.Ordinal)
                    .Replace("—", "-", StringComparison.Ordinal)
                    .Trim();
            }
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
