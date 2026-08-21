using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow.Disposition;
using ParcelWorkflowAddIn.Workflow.Reports;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class ComputeExaminationReportServiceTests
{
    public static void ReportGenerationUsesPersistedStageFindings()
    {
        using var tempRoot = new TempDirectory();
        var layout = CaseFolderLayout.For(tempRoot.Path, "TR100000379");
        Directory.CreateDirectory(layout.WorkingDirectory);
        Directory.CreateDirectory(layout.OutputDirectory);
        Directory.CreateDirectory(layout.SourceDirectory);
        File.WriteAllText(Path.Combine(layout.SourceDirectory, "DOC_PLAN_490957_D.pdf"), "placeholder pdf");

        var createdAt = DateTimeOffset.Parse("2026-07-03T12:00:00Z");
        ManifestSerializer.Write(
            layout.ManifestPath,
            ManifestDocument.CreateInitial("TR100000379", "run-report", createdAt, "mary"));

        WriteSummary(layout.StructureCheckSummaryPath, "structure_check", "dwg_required_point_layer", "DWG required point layer exists");
        WriteSummary(layout.GeoreferenceCheckSummaryPath, "georeference_check", "points_inside_parish", "Extracted points are inside parish");
        WriteSummary(layout.DimensionCheckSummaryPath, "dimension_check", "closure_tolerance", "Computed closure is within tolerance");

        File.WriteAllText(
            Path.Combine(layout.WorkingDirectory, "approved_review.json"),
            """
            {
              "status": "approved",
              "approved_by": "mary"
            }
            """);
        File.WriteAllText(
            Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"),
            """
            {
              "schema_version": "1.0.0",
              "transaction_number": "100000379",
              "coordinate_system": {
                "value": "JAD 2001 Coordinates",
                "confidence": "0.95",
                "review_status": "reviewed"
              },
              "north_arrow": {
                "present": true,
                "confidence": "0.9",
                "review_status": "reviewed"
              },
              "scale_bar": {
                "present": false,
                "confidence": "0.2",
                "review_status": "needs_review"
              },
              "document_sections": {
                "memorandum": {
                  "detected": true,
                  "matched_text": "MEMORANDUM",
                  "source_page": "1",
                  "source_zone": "memorandum heading",
                  "confidence": "0.92"
                }
              },
              "survey_metadata": {
                "document_area": {
                  "value": "569.896 sq. metres",
                  "confidence": "0.85",
                  "review_status": "reviewed"
                },
                "file_reference": {
                  "value": "PE:490957",
                  "confidence": "0.85",
                  "review_status": "reviewed"
                },
                "parish": {
                  "value": "Saint Catherine",
                  "confidence": "0.85",
                  "review_status": "reviewed"
                },
                "plan_check_date": {
                  "value": "2025/08/11",
                  "confidence": "0.85",
                  "review_status": "reviewed"
                },
                "survey_date": {
                  "value": "June 4, 2025",
                  "confidence": "0.85",
                  "review_status": "reviewed"
                },
                "survey_instrument": {
                  "value": "Hi-Target ZTS 360R",
                  "confidence": "0.85",
                  "review_status": "reviewed"
                },
                "instrument_check_date": {
                  "value": "2025/08/10",
                  "confidence": "0.85",
                  "review_status": "accepted"
                },
                "instrument_check_result": {
                  "value": "Checked",
                  "confidence": "0.85",
                  "review_status": "accepted"
                },
                "gps_instrument_number": {
                  "value": "GPS-77",
                  "confidence": "0.85",
                  "review_status": "reviewed"
                },
                "surveyed_by": {
                  "value": "Kevon L. Jarrett",
                  "confidence": "0.85",
                  "review_status": "reviewed"
                },
                "registration_details": {
                  "value": "Vol.1298 Fol.769",
                  "confidence": "0.85",
                  "review_status": "reviewed"
                },
                "volume_folio": [
                  {
                    "volume": "1298",
                    "folio": "769",
                    "raw_text": "Vol.1298 Fol.769",
                    "source_page": "page 1",
                    "source_zone": "near fence post",
                    "review_status": "reviewed"
                  }
                ]
              },
              "property_name_near_parcel_diagram": {
                "present": true,
                "value": "Lot 12 Bellevue",
                "source_zone": "parcel diagram",
                "confidence": "0.72"
              },
              "surveyed_property_names": [
                {
                  "value": "Lot 12 Bellevue",
                  "source_page": "page 1",
                  "source_zone": "memorandum",
                  "review_status": "accepted"
                }
              ],
              "surveyed_for_names": [
                {
                  "name": "Roxine Campbell",
                  "role": "surveyed_for",
                  "source_page": "page 1",
                  "source_zone": "memorandum",
                  "review_status": "accepted"
                }
              ],
              "notice_served_on": [
                {
                  "name": "Austin S. Singh",
                  "source_page": "page 1",
                  "source_zone": "notice paragraph",
                  "review_status": "accepted"
                }
              ],
              "appeared_parties": [
                {
                  "name": "Maria Brown",
                  "appearance_mode": "representative",
                  "representative": "Kevon L. Jarrett",
                  "source_page": "page 1",
                  "source_zone": "attendance paragraph",
                  "review_status": "accepted"
                }
              ],
              "parties": [
                {
                  "name": "Roxine Campbell",
                  "role": "Surveyed For",
                  "source_page": "page 1",
                  "source_zone": "memorandum",
                  "review_status": "reviewed"
                }
              ],
              "representatives": [
                {
                  "name": "Kevon L. Jarrett",
                  "role": "representative",
                  "source_page": "page 1",
                  "source_zone": "memorandum",
                  "review_status": "reviewed"
                }
              ],
              "adjacent_owners": [
                {
                  "name": "Austin S. Singh",
                  "related_segment_from": "19",
                  "related_segment_to": "W",
                  "volume": "1571",
                  "folio": "993",
                  "review_status": "reviewed"
                }
              ],
              "segments": [
                {
                  "sequence": 1,
                  "from_point": "19",
                  "to_point": "W",
                  "bearing_txt": "N74 36E",
                  "distance_txt": "8.495 m",
                  "include_in_boundary": true,
                  "review_notes": "Used segment"
                },
                {
                  "sequence": 2,
                  "from_point": "X",
                  "to_point": "Y",
                  "bearing_txt": "S12 30W",
                  "distance_txt": "10.000 m",
                  "include_in_boundary": false,
                  "review_notes": "Excluded segment"
                }
              ],
              "rows": [
                {
                  "point_identifier": "19",
                  "easting": "738860.904",
                  "northing": "643112.324",
                  "sequence_in_group": 1,
                  "review_status": "reviewed"
                },
                {
                  "point_identifier": "W",
                  "easting": "738869.094",
                  "northing": "643114.58",
                  "sequence_in_group": 2,
                  "review_status": "reviewed"
                }
              ]
            }
            """);
        File.WriteAllText(Path.Combine(layout.WorkingDirectory, "spatial_review_approval.json"), "{\"status\":\"approved\",\"operator_id\":\"mary\"}");
        File.WriteAllText(Path.Combine(layout.WorkingDirectory, "enterprise_working_disposition.json"), "{\"status\":\"succeeded\",\"run_id\":\"disp-run\"}");
        File.WriteAllText(Path.Combine(layout.OutputDirectory, "output_summary.json"), "{\"status\":\"succeeded\",\"run_id\":\"output-run\"}");
        File.WriteAllText(Path.Combine(layout.OutputDirectory, "enterprise_working_publish.json"), "{\"status\":\"succeeded\",\"publish_run_id\":\"publish-run\"}");

        var disposition = new ComputeReviewDispositionDocument(
            "compute_review_disposition_v1",
            "tx-379",
            "100000379",
            "task-379",
            "approved",
            "Geometry passed Compute review.",
            "mary",
            createdAt.UtcDateTime.ToString("O"),
            "output/output_summary.json",
            "output/enterprise_working_publish.json",
            "publish-run",
            "succeeded",
            "working/enterprise_working_disposition.json",
            "saved",
            "spatial-unit-379",
            "TR100000379-compute-working-package.zip",
            "COMPUTE_WORKING_PACKAGE",
            "uploaded");
        new ComputeReviewDispositionPersistenceService().Save(layout, disposition);

        var transaction = new SelectedInnolaTransaction(
            "task-379",
            "tx-379",
            "100000379",
            "Compute Survey Plan",
            "Plan Examination",
            createdAt,
            ApplicationId: "app-379",
            TransactionType: "Compute Survey Plan",
            Status: InnolaTransactionStatus.InProgress,
            AssignedUser: "mary",
            AssignedGroup: "Plan Examination");

        var result = new ComputeExaminationReportService().GenerateAsync(layout, transaction, disposition, "mary").GetAwaiter().GetResult();

        TestAssert.True(result.Success, result.Message);
        TestAssert.True(File.Exists(result.ReportPath), "Report file should be written.");
        TestAssert.True(File.Exists(result.PdfReportPath), "High-level PDF report should be written.");
        var pdfText = File.ReadAllText(result.PdfReportPath!);
        TestAssert.True(pdfText[..8] == "%PDF-1.4", "PDF report should use a PDF header.");
        TestAssert.True(pdfText.Contains("Transaction Info", StringComparison.OrdinalIgnoreCase), "PDF report should include Transaction Info section.");
        TestAssert.True(pdfText.Contains("General Info", StringComparison.OrdinalIgnoreCase), "PDF report should include General Info section.");
        TestAssert.True(pdfText.Contains("Volume / Folio", StringComparison.OrdinalIgnoreCase), "PDF report should include Volume/Folio section.");
        TestAssert.True(pdfText.Contains("Owners / Neighbors / Participants", StringComparison.OrdinalIgnoreCase), "PDF report should include participant section.");
        TestAssert.True(pdfText.Contains("Adjacent Owners / Neighbors", StringComparison.OrdinalIgnoreCase), "PDF report should include adjacent owner section.");
        TestAssert.True(pdfText.Contains("Memorandum Findings", StringComparison.OrdinalIgnoreCase), "PDF report should include memorandum findings section.");
        TestAssert.True(pdfText.Contains("Boundary Segments", StringComparison.OrdinalIgnoreCase), "PDF report should include boundary segments.");
        TestAssert.True(pdfText.Contains("Survey Points", StringComparison.OrdinalIgnoreCase), "PDF report should include points.");
        TestAssert.True(pdfText.Contains("Executive Summary", StringComparison.OrdinalIgnoreCase), "PDF report should include a professional executive summary section.");
        TestAssert.True(pdfText.Contains("Workflow Stage Summary", StringComparison.OrdinalIgnoreCase), "PDF report should include a workflow stage summary section.");
        TestAssert.True(pdfText.Contains("Page 1", StringComparison.OrdinalIgnoreCase), "PDF report should include page footer numbering.");
        TestAssert.True(pdfText.Contains("0.094 0.204 0.29 rg", StringComparison.Ordinal), "PDF report should draw dark blue table/header fills.");
        TestAssert.True(pdfText.Contains("/Helvetica-Bold", StringComparison.OrdinalIgnoreCase), "PDF report should include a bold font for headings and labels.");

        using var report = JsonDocument.Parse(File.ReadAllText(result.ReportPath!));
        var root = report.RootElement;

        TestAssert.Equal("compute_examination_report_v1", root.GetProperty("schema_version").GetString(), "Report schema should be explicit.");
        TestAssert.Equal("100000379", root.GetProperty("transaction_number").GetString(), "Report should carry transaction number.");
        TestAssert.Equal("run-report", root.GetProperty("manifest_run_id").GetString(), "Report should reference manifest run.");
        var transactionInfo = root.GetProperty("transaction_info").GetProperty("fields").EnumerateArray().ToArray();
        TestAssert.True(transactionInfo.Any(field =>
            field.GetProperty("field").GetString() == "Transaction Type"
            && field.GetProperty("value").GetString() == "Compute Survey Plan"), "Report should include transaction type.");
        TestAssert.True(transactionInfo.Any(field =>
            field.GetProperty("field").GetString() == "Assigned To"
            && field.GetProperty("value").GetString() == "mary"), "Report should include assigned user.");

        var generalInfo = root.GetProperty("general_info").GetProperty("fields").EnumerateArray().ToArray();
        TestAssert.True(generalInfo.Any(field =>
            field.GetProperty("field").GetString() == "Coordinate system"
            && field.GetProperty("value").GetString() == "JAD 2001 Coordinates"), "Report should include reviewed coordinate system.");
        TestAssert.True(generalInfo.Any(field =>
            field.GetProperty("field").GetString() == "Document area"
            && field.GetProperty("value").GetString() == "569.896 sq. metres"), "Report should include reviewed document area.");
        TestAssert.True(generalInfo.Any(field =>
            field.GetProperty("field").GetString() == "Source document"
            && field.GetProperty("value").GetString() == "DOC_PLAN_490957_D.pdf"), "Report should include source document.");
        TestAssert.True(generalInfo.Any(field =>
            field.GetProperty("field").GetString() == "Surveyed property name"
            && field.GetProperty("value").GetString() == "Lot 12 Bellevue"), "Report should include memorandum surveyed property name.");
        TestAssert.True(generalInfo.Any(field =>
            field.GetProperty("field").GetString() == "Scale bar"
            && field.GetProperty("value").GetString() == "Not present"), "Report should include memorandum scale-bar presence.");

        var volumeFolio = root.GetProperty("volume_folios").EnumerateArray().Single();
        TestAssert.Equal("1298", volumeFolio.GetProperty("volume").GetString(), "Report should include reviewed Volume.");
        TestAssert.Equal("769", volumeFolio.GetProperty("folio").GetString(), "Report should include reviewed Folio.");

        var participants = root.GetProperty("participants").EnumerateArray().ToArray();
        TestAssert.True(participants.Any(participant =>
            participant.GetProperty("name").GetString() == "Roxine Campbell"
            && participant.GetProperty("role").GetString() == "Surveyed For"), "Report should include reviewed party rows.");
        TestAssert.True(participants.Any(participant =>
            participant.GetProperty("name").GetString() == "Kevon L. Jarrett"
            && participant.GetProperty("group").GetString() == "Representative"), "Report should include reviewed representatives.");
        TestAssert.True(participants.Any(participant =>
            participant.GetProperty("name").GetString() == "Maria Brown"
            && participant.GetProperty("role").GetString() == "Appeared (representative: Kevon L. Jarrett)"), "Report should include memorandum attendance rows.");

        var memorandumFindings = root.GetProperty("memorandum_findings").EnumerateArray().ToArray();
        TestAssert.True(memorandumFindings.Any(finding =>
            finding.GetProperty("rule").GetString() == "Memorandum text detected"
            && finding.GetProperty("outcome").GetString() == "passed"), "Report should include passed memorandum detection finding.");
        TestAssert.True(memorandumFindings.Any(finding =>
            finding.GetProperty("rule").GetString() == "Scale bar"
            && finding.GetProperty("outcome").GetString() == "not_available"
            && finding.GetProperty("workflow_effect").GetString() == "report_only"), "Report should include report-only unresolved scale-bar finding.");

        var adjacentOwner = root.GetProperty("adjacent_owners").EnumerateArray().Single();
        TestAssert.Equal("Austin S. Singh", adjacentOwner.GetProperty("name").GetString(), "Report should include reviewed adjacent owners.");
        TestAssert.Equal("Participant", adjacentOwner.GetProperty("role").GetString(), "Unclear adjacent owner roles should normalize to Participant.");
        TestAssert.Equal("19", adjacentOwner.GetProperty("from").GetString(), "Report should include adjacent owner from-point.");
        TestAssert.Equal("W", adjacentOwner.GetProperty("to").GetString(), "Report should include adjacent owner to-point.");

        TestAssert.Equal(1, root.GetProperty("boundary_segments").GetArrayLength(), "Report should include only used boundary segments.");
        var segment = root.GetProperty("boundary_segments").EnumerateArray().Single();
        TestAssert.Equal("19", segment.GetProperty("from").GetString(), "Report should include the latest reviewed segment from point.");
        TestAssert.Equal("W", segment.GetProperty("to").GetString(), "Report should include the latest reviewed segment to point.");
        TestAssert.Equal(2, root.GetProperty("points").GetArrayLength(), "Report should include reviewed points.");
        var firstPoint = root.GetProperty("points").EnumerateArray().First();
        TestAssert.Equal("19", firstPoint.GetProperty("point").GetString(), "Report should include reviewed point label.");
        TestAssert.Equal("738860.904", firstPoint.GetProperty("easting").GetString(), "Report should include reviewed point easting.");

        var stageIds = root.GetProperty("stages").EnumerateArray()
            .Select(stage => stage.GetProperty("stage_id").GetString())
            .ToArray();

        TestAssert.True(stageIds.Contains("structure_check"), "Report should include Structure Check stage.");
        TestAssert.True(stageIds.Contains("georeference_check"), "Report should include Georeference Check stage.");
        TestAssert.True(stageIds.Contains("dimension_check"), "Report should include Dimension Check stage.");
        TestAssert.True(stageIds.Contains("validate_points_and_lines"), "Report should include Validate Points and Lines stage.");
        TestAssert.True(stageIds.Contains("working_package_attachment"), "Report should include package attachment closeout stage.");

        var structureStage = root.GetProperty("stages").EnumerateArray()
            .First(stage => stage.GetProperty("stage_id").GetString() == "structure_check");
        var finding = structureStage.GetProperty("findings").EnumerateArray().Single();
        TestAssert.Equal("dwg_required_point_layer", finding.GetProperty("rule_id").GetString(), "Report should preserve rule id.");
        TestAssert.Equal("passed", finding.GetProperty("outcome").GetString(), "Report should preserve rule outcome.");
        TestAssert.Equal("info", finding.GetProperty("workflow_effect").GetString(), "Report should preserve workflow effect.");

        var closeout = root.GetProperty("closeout");
        TestAssert.Equal("spatial-unit-379", closeout.GetProperty("spatial_unit_id").GetString(), "Report should include Spatial Unit id.");
        TestAssert.Equal("uploaded", closeout.GetProperty("working_package_upload_status").GetString(), "Report should include package upload status.");

        var references = root.GetProperty("artifact_references").EnumerateArray()
            .Select(reference => reference.GetString())
            .ToArray();
        TestAssert.True(references.Contains("working/compute_review_disposition.json"), "Report should reference disposition artifact.");
        TestAssert.True(references.Contains("output/enterprise_working_publish.json"), "Report should reference Enterprise publish artifact.");
    }

    private static void WriteSummary(string path, string stageId, string checkId, string message)
    {
        PreflightSummarySerializer.Write(
            path,
            new PreflightSummaryDocument(
                "preflight_summary_v1",
                "TR100000379",
                stageId,
                $"{stageId}-run",
                "2026-07-03T12:00:00.0000000Z",
                "mary",
                "hash",
                new PreflightSummaryPayload(
                    "passed",
                    Array.Empty<PreflightCheck>(),
                    Array.Empty<PreflightCheck>(),
                    new[]
                    {
                        PreflightCheck.PassedForCategory(stageId, checkId, message)
                            .WithDisplayName(message)
                    }),
                Array.Empty<string>(),
                Array.Empty<string>()));
    }
}
