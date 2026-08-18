using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.SpatialReview;

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
            var overlapReview = LoadOverlapReview(layout);
            var artifactRefs = BuildArtifactReferences(layout, overlapReview);
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
                overlapReview,
                artifactRefs);

            var path = Path.Combine(layout.ReportsDirectory, ReportFileName);
            File.WriteAllText(path, JsonSerializer.Serialize(Redact(report), JsonOptions));

            var pdfPath = Path.Combine(layout.ReportsDirectory, PdfReportFileName);
            SimplePdfReportWriter.Write(pdfPath, report, layout.RootDirectory);

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

    private static CompareReviewOverlapReviewSummary? LoadOverlapReview(CaseFolderLayout layout)
    {
        var path = Path.Combine(layout.WorkingDirectory, SpatialOverlapReviewPersistenceService.CompareArtifactFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var document = JsonSerializer.Deserialize<SpatialOverlapReviewDocument>(File.ReadAllText(path), JsonOptions);
        if (document is null)
        {
            return null;
        }

        return new CompareReviewOverlapReviewSummary(
            document.Scope,
            document.Summary.Status,
            document.Summary.Message,
            document.Records.Count,
            document.Layers.Count,
            (document.Snapshots ?? Array.Empty<SpatialOverlapReviewSnapshotRef>())
                .Select(snapshot => new CompareReviewOverlapSnapshotRef(
                    snapshot.OverlapGroupId,
                    snapshot.OverlapId,
                    snapshot.Caption,
                    snapshot.RelativePath,
                    snapshot.Status))
                .ToArray(),
            document.Warnings,
            document.Errors);
    }

    private static IReadOnlyList<CompareReviewArtifactReference> BuildArtifactReferences(
        CaseFolderLayout layout,
        CompareReviewOverlapReviewSummary? overlapReview)
    {
        var refs = new List<CompareReviewArtifactReference>
        {
            MakeReference(layout, Path.Combine(layout.WorkingDirectory, "compare_review_draft.json"))
        };

        var overlapPath = Path.Combine(layout.WorkingDirectory, SpatialOverlapReviewPersistenceService.CompareArtifactFileName);
        if (File.Exists(overlapPath))
        {
            refs.Add(MakeReference(layout, overlapPath));
        }

        if (overlapReview is not null)
        {
            foreach (var snapshot in overlapReview.Snapshots.Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.RelativePath)))
            {
                var relativePath = snapshot.RelativePath!.Replace('\\', '/');
                if (refs.Any(existing => string.Equals(existing.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                refs.Add(new CompareReviewArtifactReference("compare_overlap_snapshot", relativePath));
            }
        }

        return refs;
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

        public static void Write(string path, CompareReviewReportDocument report, string rootDirectory)
        {
            var pages = new PdfReportRenderer(report, rootDirectory).Render();
            var objects = new List<byte[]>();
            var pageObjectNumbers = new List<int>();

            objects.Add(Ascii("<< /Type /Catalog /Pages 2 0 R >>"));
            objects.Add(Array.Empty<byte>());
            objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));
            objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"));

            foreach (var pageContent in pages)
            {
                var pageObjectNumber = objects.Count + 1;
                var contentObjectNumber = objects.Count + 2;
                var imageObjectNumbers = pageContent.Images
                    .Select((_, index) => objects.Count + 3 + index)
                    .ToArray();
                pageObjectNumbers.Add(pageObjectNumber);

                objects.Add(Ascii(BuildPageObject(contentObjectNumber, pageContent.Images, imageObjectNumbers)));
                objects.Add(BuildContentObject(pageContent.Content));
                foreach (var image in pageContent.Images)
                {
                    objects.Add(BuildImageObject(image));
                }
            }

            objects[1] = Ascii($"<< /Type /Pages /Count {pageObjectNumbers.Count} /Kids [{string.Join(" ", pageObjectNumbers.Select(number => $"{number} 0 R"))}] >>");
            File.WriteAllBytes(path, BuildPdfBytes(objects));
        }

        private static string BuildPageObject(int contentObjectNumber, IReadOnlyList<PdfImageContent> images, IReadOnlyList<int> imageObjectNumbers)
        {
            var xObjectSection = images.Count == 0
                ? string.Empty
                : $" /XObject << {string.Join(" ", images.Select((image, index) => $"/{image.Name} {imageObjectNumbers[index]} 0 R"))} >>";
            return $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R /F2 4 0 R >>{xObjectSection} >> /Contents {contentObjectNumber} 0 R >>";
        }

        private static byte[] BuildContentObject(string content)
        {
            return Ascii($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream");
        }

        private static byte[] BuildImageObject(PdfImageContent image)
        {
            using var stream = new MemoryStream();
            var prefix = $"<< /Type /XObject /Subtype /Image /Width {image.PixelWidth} /Height {image.PixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {image.JpegBytes.Length} >>\nstream\n";
            var prefixBytes = Ascii(prefix);
            stream.Write(prefixBytes, 0, prefixBytes.Length);
            stream.Write(image.JpegBytes, 0, image.JpegBytes.Length);
            var suffixBytes = Ascii("\nendstream");
            stream.Write(suffixBytes, 0, suffixBytes.Length);
            return stream.ToArray();
        }

        private static byte[] BuildPdfBytes(IReadOnlyList<byte[]> objects)
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
                writer.Flush();
                stream.Write(objects[i], 0, objects[i].Length);
                writer.WriteLine();
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

        private static byte[] Ascii(string value)
        {
            return Encoding.ASCII.GetBytes(value);
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

        private sealed record PdfPageContent(string Content, IReadOnlyList<PdfImageContent> Images);

        private sealed record PdfImageContent(string Name, byte[] JpegBytes, int PixelWidth, int PixelHeight);

        private sealed record PdfColumn(string Header, double Width);

        private sealed class PdfReportRenderer
        {
            private readonly CompareReviewReportDocument report;
            private readonly string rootDirectory;
            private readonly List<PdfPageContent> pages = new();
            private StringBuilder stream = new();
            private List<PdfImageContent> images = new();
            private double y;

            public PdfReportRenderer(CompareReviewReportDocument report, string rootDirectory)
            {
                this.report = report;
                this.rootDirectory = rootDirectory;
            }

            public IReadOnlyList<PdfPageContent> Render()
            {
                BeginPage(includeRunningHeader: false);
                DrawReportHeader();
                DrawSummaryStrip();

                DrawSection("Executive Summary");
                DrawKeyValueTable(new[]
                {
                    ("Decision", report.DecisionState),
                    ("Transaction", report.TransactionNumber),
                    ("Generated At UTC", report.GeneratedAtUtc),
                    ("Reviewer", report.ReviewerDisplayName ?? report.ReviewerId ?? "Not provided"),
                    ("Legal Evidence Reviewed", report.LegalEvidenceReviewed ? "Yes" : "No"),
                    ("Fiscal Evidence Reviewed", report.FiscalEvidenceReviewed ? "Yes" : "No")
                });

                DrawSection("Transaction Info");
                DrawKeyValueTable(new[]
                {
                    ("Transaction Number", report.TransactionNumber),
                    ("Transaction Id", report.TransactionId),
                    ("Task Id", report.TaskId),
                    ("Task Name", report.TaskName)
                });

                DrawSection("Compare Evidence Summary");
                DrawKeyValueTable(new[]
                {
                    ("Survey Plan", EmptyToNone(report.SurveyPlanSummary)),
                    ("Legal Cadaster", EmptyToNone(report.LegalCadasterSummary)),
                    ("Fiscal / Neighbor", EmptyToNone(report.FiscalNeighborSummary))
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
                    report.ValuableEvidence.Count == 0
                        ? new[] { new[] { "None retained.", string.Empty, string.Empty, string.Empty } }
                        : report.ValuableEvidence.Select((evidence, index) => new[]
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
                    report.EnterpriseCadasterEvidence.Count == 0
                        ? new[] { new[] { "No enterprise cadaster evidence retained.", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty } }
                        : report.EnterpriseCadasterEvidence.Select(evidence => new[]
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
                    report.ManualQueryHistory.Count == 0
                        ? new[] { new[] { "No manual query history recorded.", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty } }
                        : report.ManualQueryHistory.Select(query => new[]
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
                    report.Discrepancies.Count == 0
                        ? new[] { new[] { "No discrepancies recorded.", string.Empty, string.Empty, string.Empty } }
                        : report.Discrepancies.Select(discrepancy => new[]
                        {
                            discrepancy.Title,
                            discrepancy.Source,
                            discrepancy.Status,
                            discrepancy.IsResolved ? "Yes" : "No"
                        }));

                DrawSection("Overlap Review");
                DrawKeyValueTable(new[]
                {
                    ("Status", report.OverlapReview?.Status ?? "(not run)"),
                    ("Summary", EmptyToNone(report.OverlapReview?.Message)),
                    ("Overlap Records", (report.OverlapReview?.RecordCount ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("Configured Layers", (report.OverlapReview?.LayerCount ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("Snapshots", (report.OverlapReview?.Snapshots.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture))
                });

                if (report.OverlapReview is not null)
                {
                    DrawSection("Overlap Review Snapshots");
                    if (report.OverlapReview.Snapshots.Count == 0)
                    {
                        DrawKeyValueTable(new[]
                        {
                            ("Snapshot", "No overlap snapshot was saved.")
                        });
                    }
                    else
                    {
                        foreach (var snapshot in report.OverlapReview.Snapshots)
                        {
                            DrawSnapshot(snapshot);
                        }
                    }
                }

                DrawSection("Notes");
                DrawKeyValueTable(new[] { ("Reviewer Notes", EmptyToNone(report.Notes)) });

                DrawSection("Artifact References");
                DrawTable(
                    new[]
                    {
                        new PdfColumn("Artifact", 160),
                        new PdfColumn("Relative Path", UsableWidth - 160)
                    },
                    report.ArtifactRefs.Count == 0
                        ? new[] { new[] { "No artifact references recorded.", string.Empty } }
                        : report.ArtifactRefs.Select(reference => new[] { reference.ArtifactType, reference.RelativePath }));

                FinishPage();
                return pages;
            }

            private void BeginPage(bool includeRunningHeader)
            {
                stream = new StringBuilder();
                images = new List<PdfImageContent>();
                y = TopY;
                if (includeRunningHeader)
                {
                    DrawText("Compare Review Report", MarginX, y, 8, bold: true, PrimaryR, PrimaryG, PrimaryB);
                    y -= 18;
                }
            }

            private void FinishPage()
            {
                DrawFooter();
                pages.Add(new PdfPageContent(stream.ToString(), images.ToArray()));
            }

            private void NewPage()
            {
                FinishPage();
                BeginPage(includeRunningHeader: true);
            }

            private void EnsureSpace(double requiredHeight)
            {
                if (y - requiredHeight < BottomY)
                {
                    NewPage();
                }
            }

            private void DrawReportHeader()
            {
                EnsureSpace(76);
                DrawText("Compare Review Report", MarginX, y, 22, bold: true, PrimaryR, PrimaryG, PrimaryB);
                y -= 22;
                DrawText($"NLA Transaction {report.TransactionNumber} - {report.TaskName}", MarginX, y, 10, bold: true, MutedR, MutedG, MutedB);
                y -= 14;
                DrawText($"Generated {report.GeneratedAtUtc} by {report.ReviewerDisplayName ?? report.ReviewerId ?? "Not provided"}", MarginX, y, 8, bold: false, MutedR, MutedG, MutedB);
                y -= 18;
                DrawRule();
                y -= 12;
            }

            private void DrawSummaryStrip()
            {
                EnsureSpace(54);
                var values = new[]
                {
                    ("Transaction", report.TransactionNumber),
                    ("Task", report.TaskName),
                    ("Decision", report.DecisionState),
                    ("Evidence", $"{report.ValuableEvidence.Count} retained")
                };
                var boxWidth = UsableWidth / values.Length;
                for (var i = 0; i < values.Length; i++)
                {
                    var x = MarginX + (i * boxWidth);
                    DrawRect(x, y - 42, boxWidth - 4, 38, fill: true, r: AlternateR, g: AlternateG, b: AlternateB);
                    DrawRect(x, y - 42, boxWidth - 4, 38, stroke: true, r: BorderR, g: BorderG, b: BorderB);
                    DrawText(values[i].Item1, x + 6, y - 16, 7, bold: true, MutedR, MutedG, MutedB);
                    DrawText(values[i].Item2, x + 6, y - 31, 9, bold: true);
                }

                y -= 54;
            }

            private void DrawSection(string title)
            {
                EnsureSpace(32);
                y -= 4;
                DrawText(title, MarginX, y, 12, bold: true, PrimaryR, PrimaryG, PrimaryB);
                y -= 8;
                DrawRule();
                y -= 12;
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
                DrawRect(MarginX, y - 18, columns.Sum(column => column.Width), 18, fill: true, r: PrimaryR, g: PrimaryG, b: PrimaryB);
                var x = MarginX;
                foreach (var column in columns)
                {
                    DrawText(column.Header, x + 4, y - 12, 7.5, bold: true, 1, 1, 1);
                    x += column.Width;
                }

                y -= 18;

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
                        DrawRect(MarginX, y - rowHeight, columns.Sum(column => column.Width), rowHeight, fill: true, r: AlternateR, g: AlternateG, b: AlternateB);
                    }

                    DrawRect(MarginX, y - rowHeight, columns.Sum(column => column.Width), rowHeight, stroke: true, r: BorderR, g: BorderG, b: BorderB);
                    x = MarginX;
                    for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                    {
                        var textY = y - 11;
                        foreach (var line in wrappedCells[columnIndex])
                        {
                            DrawText(line, x + 4, textY, 8);
                            textY -= 10;
                        }

                        x += columns[columnIndex].Width;
                    }

                    y -= rowHeight;
                }

                y -= 10;
            }

            private void DrawSnapshot(CompareReviewOverlapSnapshotRef snapshot)
            {
                DrawKeyValueTable(new[]
                {
                    ("Caption", snapshot.Caption),
                    ("Status", snapshot.Status),
                    ("Path", string.IsNullOrWhiteSpace(snapshot.RelativePath) ? "(not saved)" : snapshot.RelativePath!)
                });

                if (string.IsNullOrWhiteSpace(snapshot.RelativePath))
                {
                    return;
                }

                var absolutePath = Path.Combine(rootDirectory, snapshot.RelativePath!.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absolutePath))
                {
                    DrawKeyValueTable(new[]
                    {
                        ("Snapshot file", "Missing on disk at report generation time.")
                    });
                    return;
                }

                var image = LoadImage(absolutePath, $"Im{images.Count + 1}");
                var maxWidth = UsableWidth;
                var maxHeight = 360d;
                var scale = Math.Min(maxWidth / image.PixelWidth, maxHeight / image.PixelHeight);
                var drawWidth = image.PixelWidth * scale;
                var drawHeight = image.PixelHeight * scale;
                EnsureSpace(drawHeight + 18);
                var imageX = MarginX + ((UsableWidth - drawWidth) / 2);
                var imageY = y - drawHeight;
                DrawImage(image, imageX, imageY, drawWidth, drawHeight);
                y -= drawHeight + 12;
            }

            private void DrawImage(PdfImageContent image, double imageX, double imageY, double width, double height)
            {
                images.Add(image);
                stream
                    .Append("q ")
                    .Append(PdfNumber(width)).Append(" 0 0 ")
                    .Append(PdfNumber(height)).Append(' ')
                    .Append(PdfNumber(imageX)).Append(' ')
                    .Append(PdfNumber(imageY)).Append(" cm /")
                    .Append(image.Name)
                    .AppendLine(" Do Q");
            }

            private void DrawRule()
            {
                DrawRuleAt(y);
            }

            private void DrawFooter()
            {
                var pageNumber = pages.Count + 1;
                DrawRuleAt(BottomY - 12);
                DrawText($"Compare Review Report - Transaction {report.TransactionNumber}", MarginX, BottomY - 28, 7, bold: false, MutedR, MutedG, MutedB);
                DrawText($"Page {pageNumber}", PageWidth - MarginX - 38, BottomY - 28, 7, bold: false, MutedR, MutedG, MutedB);
            }

            private void DrawRuleAt(double ruleY)
            {
                stream
                    .Append(PdfNumber(BorderR)).Append(' ')
                    .Append(PdfNumber(BorderG)).Append(' ')
                    .Append(PdfNumber(BorderB)).Append(" RG ")
                    .Append(PdfNumber(MarginX)).Append(' ')
                    .Append(PdfNumber(ruleY)).Append(" m ")
                    .Append(PdfNumber(PageWidth - MarginX)).Append(' ')
                    .Append(PdfNumber(ruleY)).AppendLine(" l S");
            }

            private void DrawRect(double x, double rectY, double width, double height, bool fill = false, bool stroke = false, double r = 0, double g = 0, double b = 0)
            {
                if (fill)
                {
                    stream
                        .Append(PdfNumber(r)).Append(' ')
                        .Append(PdfNumber(g)).Append(' ')
                        .Append(PdfNumber(b)).Append(" rg ")
                        .Append(PdfNumber(x)).Append(' ')
                        .Append(PdfNumber(rectY)).Append(' ')
                        .Append(PdfNumber(width)).Append(' ')
                        .Append(PdfNumber(height)).AppendLine(" re f");
                }

                if (stroke)
                {
                    stream
                        .Append(PdfNumber(r)).Append(' ')
                        .Append(PdfNumber(g)).Append(' ')
                        .Append(PdfNumber(b)).Append(" RG ")
                        .Append(PdfNumber(x)).Append(' ')
                        .Append(PdfNumber(rectY)).Append(' ')
                        .Append(PdfNumber(width)).Append(' ')
                        .Append(PdfNumber(height)).AppendLine(" re S");
                }
            }

            private void DrawText(string text, double x, double textY, double fontSize, bool bold = false, double r = 0, double g = 0, double b = 0)
            {
                var safe = EscapePdfText(SanitizeText(text));
                stream
                    .Append("BT /").Append(bold ? "F2" : "F1").Append(' ')
                    .Append(PdfNumber(fontSize)).Append(" Tf ")
                    .Append(PdfNumber(r)).Append(' ')
                    .Append(PdfNumber(g)).Append(' ')
                    .Append(PdfNumber(b)).Append(" rg ")
                    .Append(PdfNumber(x)).Append(' ')
                    .Append(PdfNumber(textY)).Append(" Td (")
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

            private static PdfImageContent LoadImage(string path, string name)
            {
                using var file = File.OpenRead(path);
                var decoder = BitmapDecoder.Create(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                var encoder = new JpegBitmapEncoder
                {
                    QualityLevel = 90
                };
                encoder.Frames.Add(frame);
                using var output = new MemoryStream();
                encoder.Save(output);
                return new PdfImageContent(name, output.ToArray(), frame.PixelWidth, frame.PixelHeight);
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
    [property: JsonPropertyName("overlap_review")] CompareReviewOverlapReviewSummary? OverlapReview,
    [property: JsonPropertyName("artifact_refs")] IReadOnlyList<CompareReviewArtifactReference> ArtifactRefs);

public sealed record CompareReviewOverlapReviewSummary(
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("record_count")] int RecordCount,
    [property: JsonPropertyName("layer_count")] int LayerCount,
    [property: JsonPropertyName("snapshots")] IReadOnlyList<CompareReviewOverlapSnapshotRef> Snapshots,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings,
    [property: JsonPropertyName("errors")] IReadOnlyList<string> Errors);

public sealed record CompareReviewOverlapSnapshotRef(
    [property: JsonPropertyName("overlap_group_id")] string? OverlapGroupId,
    [property: JsonPropertyName("overlap_id")] string? OverlapId,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("relative_path")] string? RelativePath,
    [property: JsonPropertyName("status")] string Status);

public sealed record CompareReviewArtifactReference(
    [property: JsonPropertyName("artifact_type")] string ArtifactType,
    [property: JsonPropertyName("relative_path")] string RelativePath);
