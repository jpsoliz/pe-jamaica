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

        var createdAt = DateTimeOffset.Parse("2026-07-03T12:00:00Z");
        ManifestSerializer.Write(
            layout.ManifestPath,
            ManifestDocument.CreateInitial("TR100000379", "run-report", createdAt, "mary"));

        WriteSummary(layout.StructureCheckSummaryPath, "structure_check", "dwg_required_point_layer", "DWG required point layer exists");
        WriteSummary(layout.GeoreferenceCheckSummaryPath, "georeference_check", "points_inside_parish", "Extracted points are inside parish");
        WriteSummary(layout.DimensionCheckSummaryPath, "dimension_check", "closure_tolerance", "Computed closure is within tolerance");

        File.WriteAllText(Path.Combine(layout.WorkingDirectory, "approved_review.json"), "{\"status\":\"approved\",\"approved_by\":\"mary\"}");
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
            createdAt);

        var result = new ComputeExaminationReportService().GenerateAsync(layout, transaction, disposition, "mary").GetAwaiter().GetResult();

        TestAssert.True(result.Success, result.Message);
        TestAssert.True(File.Exists(result.ReportPath), "Report file should be written.");

        using var report = JsonDocument.Parse(File.ReadAllText(result.ReportPath!));
        var root = report.RootElement;

        TestAssert.Equal("compute_examination_report_v1", root.GetProperty("schema_version").GetString(), "Report schema should be explicit.");
        TestAssert.Equal("100000379", root.GetProperty("transaction_number").GetString(), "Report should carry transaction number.");
        TestAssert.Equal("run-report", root.GetProperty("manifest_run_id").GetString(), "Report should reference manifest run.");

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
