using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class ExtractionReviewPersistenceServiceTests
{
    public static void LoadEditAndSaveReviewArtifactPersistsOverrides()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000206");
        var service = new ExtractionReviewPersistenceService();
        File.WriteAllText(
            Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"),
            """
            {
              "transaction_number": "100000206",
              "rows": [
                {
                  "parcel_group_id": "parcel-a",
                  "traverse_id": "trav-1",
                  "sequence_in_group": 1,
                  "group_confidence": "high",
                  "point_id": "P-101",
                  "easting": "731245.22",
                  "northing": "612994.10",
                  "length": "45.120",
                  "status": "Extracted",
                  "source_evidence": "page 1"
                }
              ]
            }
            """);

        var document = service.Load(layout)!;
        document.Rows[0].Easting = "731245.99";
        document.Rows[0].ReviewNotes = "Adjusted from review.";

        var saveResult = service.Save(layout, document, "tester");
        var reloaded = service.Load(layout)!;

        TestAssert.True(saveResult.Success, "Review save should succeed.");
        TestAssert.Equal("731245.99", reloaded.Rows[0].Easting, "Saved easting override should persist.");
        TestAssert.Equal("731245.22", reloaded.Rows[0].OriginalValues.Easting, "Original easting should remain preserved.");
        TestAssert.Equal("45.120", reloaded.Rows[0].Length, "Length should persist through compact review projection.");
        TestAssert.Equal("45.120", reloaded.Rows[0].OriginalValues.Length, "Original length should remain preserved.");
        TestAssert.Equal("parcel-a", reloaded.Rows[0].ParcelGroupId, "Parcel group id should persist.");
        TestAssert.Equal("trav-1", reloaded.Rows[0].TraverseId, "Traverse id should persist.");
        TestAssert.Equal(1, reloaded.Rows[0].SequenceInGroup ?? -1, "Sequence in group should persist.");
        TestAssert.Equal("high", reloaded.Rows[0].GroupConfidence, "Group confidence should persist.");
        TestAssert.True(reloaded.Rows[0].IsEdited, "Edited row should remain marked edited.");
    }

    public static void ManualPointSavePersistsAsManualRow()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000206");
        var service = new ExtractionReviewPersistenceService();
        File.WriteAllText(Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"), """{ "transaction_number": "100000206", "rows": [] }""");

        var document = service.Load(layout)!;
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "manual-001",
            PointIdentifier = "P-999",
            Easting = "731000.11",
            Northing = "612000.22",
            ExtractionStatus = "Manual entry",
            SourceEvidence = "Manual correction",
            RowProvenance = "manual",
            IsManual = true,
            IsEdited = true
        });

        service.Save(layout, document, "tester");
        var reloaded = service.Load(layout)!;

        TestAssert.Equal(1, reloaded.Rows.Count, "Manual row should persist.");
        TestAssert.True(reloaded.Rows[0].IsManual, "Manual row should reload as manual.");
        TestAssert.Equal("P-999", reloaded.Rows[0].PointIdentifier, "Manual row point id should persist.");
    }

    public static void ApprovalIsBlockedWhenUnresolvedRowsRemain()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000206");
        var service = new ExtractionReviewPersistenceService();
        File.WriteAllText(
            Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"),
            """
            {
              "transaction_number": "100000206",
              "rows": [
                {
                  "point_id": "P-103",
                  "easting": "",
                  "northing": "612965.18",
                  "status": "Missing easting",
                  "source_evidence": "page 2",
                  "review_unresolved": true
                }
              ]
            }
            """);

        var document = service.Load(layout)!;
        var approval = service.Approve(layout, document, "tester");

        TestAssert.True(!approval.Success, "Approval should be blocked when unresolved rows remain.");
        TestAssert.True(!File.Exists(Path.Combine(layout.WorkingDirectory, "approved_review.json")), "Blocked approval must not write approved_review.json.");
    }

    public static void ApprovalWritesHashAndIsInvalidatedByLaterEdit()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000206");
        var service = new ExtractionReviewPersistenceService();
        File.WriteAllText(
            Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"),
            """
            {
              "transaction_number": "100000206",
              "rows": [
                {
                  "point_id": "P-101",
                  "easting": "731245.22",
                  "northing": "612994.10",
                  "status": "Extracted",
                  "source_evidence": "page 1"
                }
              ]
            }
            """);

        var document = service.Load(layout)!;
        var saveResult = service.Save(layout, document, "tester");
        var approval = service.Approve(layout, saveResult.Document!, "tester");
        var approvedPath = Path.Combine(layout.WorkingDirectory, "approved_review.json");

        TestAssert.True(approval.Success, "Approval should succeed for complete rows.");
        TestAssert.True(File.Exists(approvedPath), "Approved review artifact should be written.");

        var approvedBeforeEdit = File.ReadAllText(approvedPath);
        var editedDocument = service.Load(layout)!;
        editedDocument.Rows[0].Northing = "612994.99";
        service.Save(layout, editedDocument, "tester");

        TestAssert.True(!File.Exists(approvedPath), "Later review edits should invalidate approved review artifact.");
        TestAssert.True(approvedBeforeEdit.Contains("\"review_hash\"", StringComparison.OrdinalIgnoreCase), "Approved review artifact should contain review hash.");
    }

    private static CaseFolderLayout CreateLayout(string root, string transactionNumber)
    {
        var layout = CaseFolderLayout.For(root, transactionNumber);
        Directory.CreateDirectory(layout.RootDirectory);
        Directory.CreateDirectory(layout.SourceDirectory);
        Directory.CreateDirectory(layout.WorkingDirectory);
        Directory.CreateDirectory(layout.OutputDirectory);
        Directory.CreateDirectory(layout.ReportsDirectory);
        Directory.CreateDirectory(layout.LogsDirectory);
        return layout;
    }
}
