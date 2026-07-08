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

    public static void LoadAcceptsNumericSurveyPlanReviewFields()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000562");
        var service = new ExtractionReviewPersistenceService();
        File.WriteAllText(
            Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"),
            """
            {
              "schema_version": "2.18.0",
              "transaction_number": "100000562",
              "extraction_source": "survey_plan_ocr_vision",
              "errors": [404, "reviewable"],
              "rows": [
                {
                  "parcel_group_id": "parcel-001",
                  "traverse_id": "parcel-001",
                  "sequence_in_group": 1,
                  "point_id": 15,
                  "easting": 712897.345,
                  "northing": 670582.156,
                  "length": 33.47,
                  "confidence": 0.95,
                  "source_page": 1,
                  "review_notes": null,
                  "row_provenance": "survey_plan_ocr_vision"
                }
              ]
            }
            """);

        var document = service.Load(layout)!;

        TestAssert.Equal(1, document.Rows.Count, "Numeric survey-plan row should load.");
        TestAssert.Equal("15", document.Rows[0].PointIdentifier, "Numeric point id should be stringified.");
        TestAssert.Equal("712897.345", document.Rows[0].Easting, "Numeric easting should be stringified.");
        TestAssert.Equal("670582.156", document.Rows[0].Northing, "Numeric northing should be stringified.");
        TestAssert.Equal("33.47", document.Rows[0].Length, "Numeric length should be stringified.");
        TestAssert.Equal("0.95", document.Rows[0].ExtractionStatus, "Numeric confidence fallback should be stringified when status is absent.");
        TestAssert.Equal("404", document.Errors[0], "Numeric error entries should not crash string-array loading.");
    }

    public static void LoadEditAndSaveReviewArtifactPersistsSegments()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000562");
        var service = new ExtractionReviewPersistenceService();
        File.WriteAllText(
            Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"),
            """
            {
              "schema_version": "2.18.0",
              "transaction_number": "100000562",
              "segment_row_count": 1,
              "segments": [
                {
                  "segment_no": 1,
                  "from_point": "18",
                  "to_point": "15",
                  "bearing_txt": "S84°56'E",
                  "distance_txt": "33.470",
                  "confidence": 0.82,
                  "source_page": 1,
                  "source_zone": "parcel_sketch",
                  "status": "candidate",
                  "review_note": "OCR candidate"
                }
              ],
              "rows": [
                {
                  "point_id": "15",
                  "easting": "712897.345",
                  "northing": "670582.156",
                  "status": "candidate"
                }
              ]
            }
            """);

        var document = service.Load(layout)!;
        var originalHash = service.ComputeReviewHash(document);

        TestAssert.Equal(1, document.Segments.Count, "Segment row should load into typed review document.");
        TestAssert.Equal("18", document.Segments[0].FromPoint, "Segment from point should load.");
        TestAssert.Equal("S84°56'E", document.Segments[0].BearingText, "Segment bearing should load.");
        TestAssert.Equal("0.82", document.Segments[0].Confidence, "Numeric confidence should load as text.");

        document.Segments[0].ReviewToPoint = "30";
        document.Segments[0].ReviewNotes = "Corrected endpoint after source review.";
        service.Save(layout, document, "tester");
        var reloaded = service.Load(layout)!;
        var editedHash = service.ComputeReviewHash(reloaded);

        TestAssert.Equal("30", reloaded.Segments[0].ReviewToPoint, "Reviewed segment endpoint should persist.");
        TestAssert.Equal("15", reloaded.Segments[0].OriginalValues.ToPoint, "Original segment endpoint should remain preserved.");
        TestAssert.Equal("Corrected endpoint after source review.", reloaded.Segments[0].ReviewNotes, "Reviewed segment note should persist.");
        TestAssert.True(!string.Equals(originalHash, editedHash, StringComparison.OrdinalIgnoreCase), "Segment edits should change the review hash.");
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

    public static void LoadDerivesParcelGroupingFromParcelNameWhenGroupIdsAreMissing()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000232");
        var service = new ExtractionReviewPersistenceService();
        File.WriteAllText(
            Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"),
            """
            {
              "transaction_number": "100000232",
              "rows": [
                {
                  "point_id": "110900201_1",
                  "parcel_name": "110900201",
                  "easting": "670077.022",
                  "northing": "644221.7717",
                  "status": "Matched",
                  "source_evidence": "page 1"
                },
                {
                  "point_id": "110900201_2",
                  "parcel_name": "110900201",
                  "easting": "670069.022",
                  "northing": "644266.6157",
                  "status": "Matched",
                  "source_evidence": "page 1"
                },
                {
                  "point_id": "110900202_1",
                  "parcel_name": "110900202",
                  "easting": "670000.000",
                  "northing": "644000.000",
                  "status": "Matched",
                  "source_evidence": "page 2"
                }
              ]
            }
            """);

        var document = service.Load(layout)!;

        TestAssert.Equal("110900201", document.Rows[0].ParcelGroupId, "Parcel group id should derive from parcel_name.");
        TestAssert.Equal("110900201", document.Rows[0].TraverseId, "Traverse id should derive from parcel_name when missing.");
        TestAssert.Equal(1, document.Rows[0].SequenceInGroup ?? -1, "First row in a derived parcel group should start at sequence 1.");
        TestAssert.Equal(2, document.Rows[1].SequenceInGroup ?? -1, "Second row in the same derived parcel group should increment sequence.");
        TestAssert.Equal("110900202", document.Rows[2].ParcelGroupId, "A new parcel_name should start a new derived parcel group.");
        TestAssert.Equal(1, document.Rows[2].SequenceInGroup ?? -1, "The first row of a new derived parcel group should reset sequence.");
        TestAssert.Equal("derived_from_parcel_name", document.Rows[0].GroupConfidence, "Derived grouping should carry an explicit confidence label.");
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
