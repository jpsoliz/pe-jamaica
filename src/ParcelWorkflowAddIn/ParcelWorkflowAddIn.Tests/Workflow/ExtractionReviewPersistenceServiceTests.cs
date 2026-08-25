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

    public static void ManualSegmentSavePersistsAsManualSegment()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000674");
        var service = new ExtractionReviewPersistenceService();
        File.WriteAllText(
            Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"),
            """
            {
              "schema_version": "2.20.0",
              "transaction_number": "100000674",
              "extraction_source": "survey_plan_ocr_vision",
              "rows": [
                { "point_id": "1", "easting": "1", "northing": "1" }
              ],
              "segments": []
            }
            """);

        var document = service.Load(layout)!;
        var manualSegment = new ManualBoundarySegmentService().CreateManualSegment(document);
        manualSegment.ReviewFromPoint = "1";
        manualSegment.ReviewToPoint = "2";
        manualSegment.ReviewBearingText = "N07 54E";
        manualSegment.ReviewDistanceText = "44.969";
        manualSegment.ReviewLengthText = "44.969";
        manualSegment.AdjacentOwner = "Road reservation";
        manualSegment.ReviewNotes = "Added by examiner from source plan.";
        document.Segments.Add(manualSegment);

        service.Save(layout, document, "tester");
        var reloaded = service.Load(layout)!;

        TestAssert.Equal(1, reloaded.Segments.Count, "Manual segment should persist.");
        TestAssert.True(reloaded.Segments[0].SegmentId.StartsWith("manual-segment-", StringComparison.Ordinal), "Manual segment id should persist.");
        TestAssert.Equal("1", reloaded.Segments[0].ReviewFromPoint, "Manual segment from point should persist as reviewed value.");
        TestAssert.Equal("2", reloaded.Segments[0].ReviewToPoint, "Manual segment to point should persist as reviewed value.");
        TestAssert.Equal("N07 54E", reloaded.Segments[0].ReviewBearingText, "Manual segment bearing should persist as reviewed value.");
        TestAssert.Equal("44.969", reloaded.Segments[0].ReviewDistanceText, "Manual segment distance should persist as reviewed value.");
        TestAssert.Equal("Road reservation", reloaded.Segments[0].AdjacentOwner, "Manual segment adjacent owner should persist.");
        TestAssert.True(reloaded.Segments[0].IsEdited, "Manual segment should reload as edited.");
    }

    public static void LoadEditAndSaveReviewArtifactPersistsSurveyMetadataAndAdjacentOwners()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000562");
        var service = new ExtractionReviewPersistenceService();
        File.WriteAllText(
            Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"),
            """
            {
              "schema_version": "2.20.0",
              "transaction_number": "100000562",
              "extraction_source": "survey_plan_ocr_vision",
              "coordinate_system": { "value": "JAD 2001", "present": true, "confidence": 0.91 },
              "north_arrow": { "present": true, "confidence": 0.88 },
              "survey_metadata": {
                "parish": { "value": "Clarendon", "confidence": 0.87 },
                "document_area": { "value": "854.807", "unit": "sq. metres", "confidence": 0.84 },
                "survey_date": "2024-09-03",
                "instrument": "TOPCON GM-52 #1Y013971",
                "surveyor": "Michael D. Isaacs",
                "volume_folio": [
                  { "volume": "313", "folio": "71", "raw_text": "Vol.313 Fol.71" }
                ]
              },
              "parties": [
                { "name": "Clayon Smith", "role": "Occ." }
              ],
              "representatives": [
                { "name": "P.D.J.", "role": "drawn_by" }
              ],
              "adjacent_owners": [
                {
                  "name": "Rayon Smith",
                  "role": "Occ.",
                  "related_segment_from": "16",
                  "related_segment_to": "17",
                  "volume": "313",
                  "folio": "71"
                }
              ],
              "segments": [
                {
                  "segment_no": 4,
                  "from_point": "16",
                  "to_point": "17",
                  "bearing_txt": "N82°59'W",
                  "distance_txt": "41.415",
                  "adjacent_owner": "Rayon Smith"
                }
              ],
              "rows": [
                { "point_id": "16", "easting": "712897.659", "northing": "670558.591" },
                { "point_id": "17", "easting": "712856.553", "northing": "670563.653" }
              ]
            }
            """);

        var document = service.Load(layout)!;
        var originalHash = service.ComputeReviewHash(document);

        TestAssert.True(document.SurveyMetadataFields.Count >= 6, "PXA survey metadata fields should load.");
        TestAssert.Equal("JAD 2001", document.SurveyMetadataFields.First(field => field.Key == "coordinate_system").Value, "Coordinate system should load.");
        TestAssert.Equal<bool?>(true, document.SurveyMetadataFields.First(field => field.Key == "north_arrow").Present, "North arrow presence should load.");
        TestAssert.Equal("Clayon Smith", document.Parties[0].Name, "Party / owner should load.");
        TestAssert.Equal("Occupant", document.Parties[0].Role, "Occ. party role should expand to Occupant.");
        TestAssert.Equal("P.D.J.", document.Representatives[0].Name, "Representative should load.");
        TestAssert.Equal("313", document.VolumeFolios[0].Volume, "Volume row should load.");
        TestAssert.Equal("71", document.VolumeFolios[0].Folio, "Folio row should load.");
        TestAssert.Equal("Rayon Smith", document.AdjacentOwners[0].Name, "Adjacent owner should load.");
        TestAssert.Equal("Occupant", document.AdjacentOwners[0].Role, "Occ. adjacent-owner role should expand to Occupant.");
        TestAssert.Equal("Rayon Smith", document.Segments[0].AdjacentOwner, "Segment adjacent owner should load.");

        document.SurveyMetadataFields.First(field => field.Key == "parish").Value = "St Andrew";
        document.SurveyMetadataFields.First(field => field.Key == "parish").ReviewStatus = "accepted";
        document.Parties[0].ReviewStatus = "accepted";
        document.Representatives[0].ReviewNotes = "Confirmed from plan title block.";
        document.VolumeFolios[0].ReviewStatus = "accepted";
        document.AdjacentOwners[0].RelatedSegmentFrom = "17";
        document.AdjacentOwners[0].RelatedSegmentTo = "18";
        document.AdjacentOwners[0].ReviewStatus = "accepted";
        document.Segments[0].AdjacentOwner = "Rayon Smith (occ.)";

        service.Save(layout, document, "tester");
        var reloaded = service.Load(layout)!;
        var editedHash = service.ComputeReviewHash(reloaded);

        TestAssert.Equal("St Andrew", reloaded.SurveyMetadataFields.First(field => field.Key == "parish").Value, "Edited parish should persist.");
        TestAssert.Equal("accepted", reloaded.SurveyMetadataFields.First(field => field.Key == "parish").ReviewStatus, "Metadata review status should persist.");
        TestAssert.Equal("accepted", reloaded.Parties[0].ReviewStatus, "Party review status should persist.");
        TestAssert.Equal("Confirmed from plan title block.", reloaded.Representatives[0].ReviewNotes, "Representative review notes should persist.");
        TestAssert.Equal("accepted", reloaded.VolumeFolios[0].ReviewStatus, "Volume / folio review status should persist.");
        TestAssert.Equal("17", reloaded.AdjacentOwners[0].RelatedSegmentFrom, "Edited owner segment start should persist.");
        TestAssert.Equal("18", reloaded.AdjacentOwners[0].RelatedSegmentTo, "Edited owner segment end should persist.");
        TestAssert.Equal("Rayon Smith (occ.)", reloaded.Segments[0].AdjacentOwner, "Edited segment adjacent owner should persist.");
        TestAssert.True(!string.Equals(originalHash, editedHash, StringComparison.OrdinalIgnoreCase), "PXA metadata edits should change the review hash.");
    }

    public static void PxaReviewRoutingRequiresSurveyPlanMetadataNotJustSegments()
    {
        var segmentedPeDocument = new ExtractionReviewDocument
        {
            ExtractionSource = "pdf_text_structured"
        };
        segmentedPeDocument.Segments.Add(new ExtractionReviewSegment
        {
            FromPoint = "1",
            ToPoint = "2",
            BearingText = "N01°00'E",
            DistanceText = "10.000"
        });

        var surveyPlanDocument = new ExtractionReviewDocument
        {
            ExtractionSource = "survey_plan_ocr_vision"
        };

        var profiledSurveyPlanDocument = new ExtractionReviewDocument();
        profiledSurveyPlanDocument.RootMetadata["source_profile"] = "scanned_single_parcel_survey_plan_pdf";
        var plaDocument = new ExtractionReviewDocument
        {
            ExtractionSource = "pla_plan_ocr_vision"
        };
        plaDocument.RootMetadata["source_profile"] = "pla_plan_annexation_selected_plan";
        plaDocument.RootMetadata["primary_source_role"] = "plan_annexation_pdf";

        TestAssert.True(!PxaSurveyPlanReviewRouting.IsPxaSurveyPlanDocument(segmentedPeDocument), "Segment rows alone should not route a PE artifact into the PXA survey-plan UX.");
        TestAssert.True(PxaSurveyPlanReviewRouting.IsPxaSurveyPlanDocument(surveyPlanDocument), "Survey-plan extraction source should route into the PXA survey-plan UX.");
        TestAssert.True(PxaSurveyPlanReviewRouting.IsPxaSurveyPlanDocument(profiledSurveyPlanDocument), "Survey-plan source profile should route into the PXA survey-plan UX.");
        TestAssert.True(PxaSurveyPlanReviewRouting.IsPxaOnlySurveyPlanDocument(surveyPlanDocument), "PXA survey-plan source should keep PXA-only solver behavior.");
        TestAssert.True(PxaSurveyPlanReviewRouting.IsPxaSurveyPlanDocument(plaDocument), "PLA selected-plan extraction should reuse the survey-plan boundary review UX.");
        TestAssert.True(PxaSurveyPlanReviewRouting.IsPlaPlanAnnexationDocument(plaDocument), "PLA selected-plan review should remain distinguishable for local-origin solving.");
        TestAssert.True(!PxaSurveyPlanReviewRouting.IsPxaOnlySurveyPlanDocument(plaDocument), "PLA selected-plan review should not receive PXA-only solver behavior.");
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

    public static void LoadEditAndSaveReviewArtifactPersistsMemorandumFieldsAndHash()
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
              "source_profile": "scanned_single_parcel_survey_plan_pdf",
              "document_sections": {
                "memorandum": {
                  "detected": true,
                  "matched_text": "MEMORANDUM",
                  "source_page": 1,
                  "source_zone": "memorandum",
                  "confidence": 0.91
                }
              },
              "survey_metadata": {
                "parish": { "value": "Clarendon", "confidence": 0.87 },
                "instrument": { "value": "TOPCON GM-52", "source_zone": "instrument_block" },
                "instrument_check_date": { "value": "2024-09-04", "source_zone": "instrument_block" },
                "instrument_check_result": { "value": "Checked", "source_zone": "instrument_block" },
                "gps_instrument_number": { "value": "GPS-7", "source_zone": "instrument_block" },
                "gps_serial_number": { "value": "SN-12345", "source_zone": "instrument_block" }
              },
              "property_name_near_parcel_diagram": {
                "present": true,
                "value": "Lot 12 Bellevue",
                "source_zone": "parcel_diagram",
                "confidence": 0.72
              },
              "scale_bar": { "present": false, "confidence": 0.2, "source_zone": "not visible" },
              "surveyed_for_names": [
                { "name": "Roxine Campbell", "role": "surveyed_for", "source_zone": "memorandum" }
              ],
              "surveyed_property_names": [
                { "value": "Lot 12 Bellevue", "source_zone": "memorandum" }
              ],
              "notice_served_on": [
                { "name": "Austin Singh", "source_zone": "memorandum" }
              ],
              "appeared_parties": [
                { "name": "Maria Brown", "appearance_mode": "representative", "representative": "Kevon Jarrett" }
              ],
              "rows": [
                { "point_id": "16", "easting": "712897.659", "northing": "670558.591" }
              ]
            }
            """);

        var document = service.Load(layout)!;
        var originalHash = service.ComputeReviewHash(document);

        TestAssert.True(document.MemorandumDetected, "Memorandum detection should load.");
        TestAssert.Equal(6, document.MemorandumGroups.Count, "Memorandum rule groups should be available.");
        TestAssert.True(document.MemorandumRuleResults.Any(rule => rule.RuleId == "pxa_memorandum_detected" && rule.Outcome == "passed"), "Memorandum detection rule should pass.");
        TestAssert.True(document.MemorandumRuleResults.Any(rule => rule.RuleId == "pxa_memorandum_scale_bar_present" && rule.Outcome == "not_available"), "Missing scale bar should be not available.");
        TestAssert.Equal("Roxine Campbell", document.MemorandumParties.First(party => party.Role == "surveyed_for").Name, "Surveyed-for party should load.");
        TestAssert.Equal("representative", document.MemorandumParties.First(party => party.Role == "appeared").AppearanceMode, "Appearance mode should load.");

        var scaleField = document.SurveyMetadataFields.First(field => field.Key == "scale_bar");
        scaleField.Present = true;
        scaleField.ReviewStatus = "accepted";
        scaleField.ReviewNotes = "Confirmed on lower left of memorandum.";

        service.Save(layout, document, "tester");
        var reloaded = service.Load(layout)!;
        var editedHash = service.ComputeReviewHash(reloaded);

        TestAssert.True(reloaded.SurveyMetadataFields.First(field => field.Key == "scale_bar").Present == true, "Scale bar review edit should persist.");
        TestAssert.True(!string.Equals(originalHash, editedHash, StringComparison.OrdinalIgnoreCase), "Memorandum review edits should affect review hash.");
    }

    public static void MemorandumRulesAreNotApplicableWhenMemorandumIsNotDetected()
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
              "source_profile": "scanned_single_parcel_survey_plan_pdf",
              "document_sections": {
                "memorandum": { "detected": false, "status": "not_applicable" }
              },
              "survey_metadata": {
                "parish": { "value": "Clarendon" }
              },
              "rows": [
                { "point_id": "16", "easting": "712897.659", "northing": "670558.591" }
              ]
            }
            """);

        var document = service.Load(layout)!;

        TestAssert.True(!document.MemorandumDetected, "Memorandum should not be detected.");
        TestAssert.True(document.MemorandumRuleResults.Count > 0, "Memorandum rules should still be auditable.");
        TestAssert.True(document.MemorandumRuleResults.All(rule => rule.Outcome == "not_applicable"), "All memorandum rules should be not applicable.");
    }

    public static void MemorandumNoOneAppearedIsExplicitAttendanceEvidence()
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
              "source_profile": "scanned_single_parcel_survey_plan_pdf",
              "document_sections": {
                "memorandum": { "detected": true, "matched_text": "MEMORANDUM" }
              },
              "survey_metadata": {
                "parish": { "value": "Clarendon" },
                "instrument": { "value": "FOIF RTS 102R8" },
                "instrument_check_date": { "value": "2024-10-04" },
                "instrument_check_result": { "value": "Satisfactory" }
              },
              "surveyed_for_names": [{ "name": "Mario Smith", "review_status": "accepted" }],
              "surveyed_property_names": [{ "value": "Part of SYMS RUN", "review_status": "accepted" }],
              "notice_served_on": [{ "name": "The C.E.O of St. Ann Municipal Corporation", "review_status": "accepted" }],
              "appeared_parties": [
                { "name": "No one appeared", "appearance_mode": "none", "review_status": "accepted" }
              ],
              "rows": [
                { "point_id": "16", "easting": "712897.659", "northing": "670558.591" }
              ]
            }
            """);

        var document = service.Load(layout)!;
        var appearanceRule = document.MemorandumRuleResults.First(rule => rule.RuleId == "pxa_memorandum_appearance_parties_present");

        TestAssert.Equal("passed", appearanceRule.Outcome, "Explicit no-appearance evidence should pass when accepted.");
        TestAssert.Equal("No one appeared", appearanceRule.EvidenceValue, "Appearance rule should expose the reviewed attendance value.");
        TestAssert.True(appearanceRule.Message.Contains("No one appeared", StringComparison.OrdinalIgnoreCase), "Rule message should make the negative attendance evidence clear.");
    }

    public static void MemorandumSemanticStatesDriveReviewOutcomesAndPersist()
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
              "source_profile": "scanned_single_parcel_survey_plan_pdf",
              "document_sections": {
                "memorandum": { "detected": true, "matched_text": "MEMORANDUM" }
              },
              "survey_metadata": {
                "parish": { "value": "", "semantic_state": "NOT_STATED", "source_zone": "memorandum" },
                "document_area": { "value": "3203.710 Sq. Metres", "numeric_value": 3203.710, "unit": "SQUARE_METRES", "semantic_state": "VALUE", "source_zone": "memorandum" },
                "survey_method": { "value": "Traverse", "semantic_state": "BOGUS_STATE", "source_zone": "memorandum" },
                "grounds_of_objection": { "value": "None", "semantic_state": "NONE", "source_zone": "memorandum" },
                "instrument": { "value": "FOIF RTS 102R8 S/N: A13183", "semantic_state": "VALUE" },
                "instrument_check_date": { "value": "04/10/2024", "semantic_state": "VALUE" },
                "instrument_check_result": { "raw_text": "####", "semantic_state": "ILLEGIBLE", "source_zone": "instrument_block" },
                "surveyor": { "value": "Craig A. Francis", "title": "Commissioned Land Surveyor", "organization": "Precision Surveying Services Ltd.", "semantic_state": "VALUE" }
              },
              "surveyed_for_names": [{ "name": "Mario Smith", "semantic_state": "VALUE" }],
              "interested_parties": [{ "name": "The C.E.O of St. Ann Municipal Corporation", "semantic_state": "VALUE", "source_zone": "memorandum" }],
              "surveyed_property_names": [{ "value": "Part of SYMS RUN", "semantic_state": "VALUE" }],
              "notice_served_on": [{ "name": "The C.E.O of St. Ann Municipal Corporation", "semantic_state": "VALUE" }],
              "appeared_parties": [{ "name": "No one appeared", "appearance_mode": "none", "semantic_state": "NO_ONE_APPEARED", "review_status": "accepted" }],
              "rows": [
                { "point_id": "16", "easting": "712897.659", "northing": "670558.591" }
              ]
            }
            """);

        var document = service.Load(layout)!;

        TestAssert.Equal("NOT_STATED", document.SurveyMetadataFields.First(field => field.Key == "parish").SemanticState, "Blank parish should load as NOT_STATED.");
        TestAssert.Equal("VALUE", document.SurveyMetadataFields.First(field => field.Key == "document_area").SemanticState, "Document area semantic state should load.");
        TestAssert.Equal("UNKNOWN", document.SurveyMetadataFields.First(field => field.Key == "survey_method").SemanticState, "Unsupported semantic states should normalize to UNKNOWN.");
        TestAssert.Equal("NONE", document.SurveyMetadataFields.First(field => field.Key == "grounds_of_objection").SemanticState, "Explicit None should load distinctly.");
        TestAssert.Equal("ILLEGIBLE", document.SurveyMetadataFields.First(field => field.Key == "instrument_check_result").SemanticState, "Illegible instrument result should load distinctly.");
        TestAssert.True(document.MemorandumParties.Any(party => party.Role == "interested_party" && party.Name.Contains("Municipal Corporation", StringComparison.Ordinal)), "Interested parties should load into memorandum review parties.");
        TestAssert.Equal("NO_ONE_APPEARED", document.MemorandumParties.First(party => party.Role == "appeared").SemanticState, "No-appearance attendance should load distinctly.");
        TestAssert.True(document.MemorandumRuleResults.Any(rule => rule.RuleId == "pxa_memorandum_document_area_present" && rule.Outcome == "needs_review"), "Document area should be captured and reviewable until accepted.");
        TestAssert.True(document.MemorandumRuleResults.Any(rule => rule.RuleId == "pxa_memorandum_objections_captured" && rule.Outcome == "passed"), "Explicit None objections should satisfy the objections rule.");
        TestAssert.True(document.MemorandumRuleResults.Any(rule => rule.RuleId == "pxa_memorandum_parish_present" && rule.Outcome == "not_available"), "Blank parish should be not available.");
        TestAssert.True(document.MemorandumRuleResults.Any(rule => rule.RuleId == "pxa_memorandum_instrument_group_complete" && rule.Outcome == "needs_review"), "Illegible instrument result should need review.");
        var originalHash = service.ComputeReviewHash(document);
        document.SurveyMetadataFields.First(field => field.Key == "survey_method").SemanticState = "VALUE";
        var changedHash = service.ComputeReviewHash(document);
        TestAssert.True(!string.Equals(originalHash, changedHash, StringComparison.Ordinal), "Review hash should change when semantic metadata changes.");
        var areaField = document.SurveyMetadataFields.First(field => field.Key == "document_area");
        areaField.Unit = string.Empty;
        areaField.ReviewStatus = "accepted";
        var ambiguousAreaResult = new PxaMemorandumReviewRuleService().Evaluate(document).First(rule => rule.RuleId == "pxa_memorandum_document_area_present");
        TestAssert.Equal("needs_review", ambiguousAreaResult.Outcome, "Accepted area text without deterministic unit parsing should still need review.");
        areaField.Unit = "SQUARE_METRES";
        areaField.ReviewStatus = string.Empty;

        service.Save(layout, document, "tester");
        var saved = File.ReadAllText(Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"));

        TestAssert.True(saved.Contains("\"semantic_state\": \"NOT_STATED\"", StringComparison.Ordinal), "Metadata semantic state should persist.");
        TestAssert.True(saved.Contains("\"interested_parties\"", StringComparison.Ordinal), "Interested parties should persist.");
        TestAssert.True(saved.Contains("\"semantic_state\": \"NO_ONE_APPEARED\"", StringComparison.Ordinal), "Party semantic state should persist.");
        TestAssert.True(saved.Contains("\"unit\": \"SQUARE_METRES\"", StringComparison.Ordinal), "Area unit should persist.");
    }

    public static void MemorandumMatchedTextOverridesFalseDetectionFlagAndUsesScaleEvidence()
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
              "source_profile": "scanned_single_parcel_survey_plan_pdf",
              "document_sections": {
                "memorandum": {
                  "detected": false,
                  "matched_text": "MEMORANDUM",
                  "source_zone": "memorandum"
                }
              },
              "scale_bar": {
                "present": true,
                "value": "SCALE : 1cm To 10m R.F 1/1000",
                "source_zone": "scale_bar_text",
                "confidence": 0.75
              },
              "survey_metadata": {
                "parish": { "value": "Clarendon" },
                "instrument": { "value": "FOIF RTS 102R8" },
                "instrument_check_date": { "value": "2024-10-04" },
                "instrument_check_result": { "value": "Satisfactory" }
              },
              "surveyed_for_names": [{ "name": "Mario Smith", "review_status": "accepted" }],
              "surveyed_property_names": [{ "value": "Part of Retreat", "review_status": "accepted" }],
              "notice_served_on": [{ "name": "The Commissioner of Lands", "review_status": "accepted" }],
              "appeared_parties": [{ "name": "No one appeared", "appearance_mode": "none", "review_status": "accepted" }],
              "rows": [
                { "point_id": "16", "easting": "712897.659", "northing": "670558.591" }
              ]
            }
            """);

        var document = service.Load(layout)!;
        var detectionRule = document.MemorandumRuleResults.First(rule => rule.RuleId == "pxa_memorandum_detected");
        var scaleRule = document.MemorandumRuleResults.First(rule => rule.RuleId == "pxa_memorandum_scale_bar_present");

        TestAssert.True(document.MemorandumDetected, "Matched MEMORANDUM text should override a stale false detection flag.");
        TestAssert.Equal("passed", detectionRule.Outcome, "Memorandum detection rule should pass when matched text is present.");
        TestAssert.Equal("MEMORANDUM", detectionRule.EvidenceValue, "Detection rule should expose the matched memorandum text.");
        TestAssert.Equal("needs_review", scaleRule.Outcome, "Scale bar text evidence should be available for examiner review until accepted.");
        TestAssert.Equal("SCALE : 1cm To 10m R.F 1/1000", scaleRule.EvidenceValue, "Scale rule should expose the visible scale text.");
    }

    public static void MemorandumRootOcrTextEnablesRulesWhenSectionIsMissing()
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
              "source_profile": "scanned_single_parcel_survey_plan_pdf",
              "document_text": "SCALE : 1cm To 10m R.F 1/1000 MEMORANDUM",
              "scale_bar": {
                "present": true,
                "Value": "SCALE : 1cm To 10m R.F 1/1000",
                "ApproximatePageLocation": "scale_bar_text",
                "Confidence": 0.75
              },
              "survey_metadata": {
                "parish": { "value": "Clarendon", "source_zone": "memorandum" }
              },
              "rows": [
                { "point_id": "16", "easting": "712897.659", "northing": "670558.591" }
              ]
            }
            """);

        var document = service.Load(layout)!;
        var detectionRule = document.MemorandumRuleResults.First(rule => rule.RuleId == "pxa_memorandum_detected");
        var scaleRule = document.MemorandumRuleResults.First(rule => rule.RuleId == "pxa_memorandum_scale_bar_present");

        TestAssert.True(document.MemorandumDetected, "Root OCR text containing MEMORANDUM should enable memorandum rules.");
        TestAssert.Equal("passed", detectionRule.Outcome, "Detection should pass when root OCR text contains MEMORANDUM.");
        TestAssert.Equal("needs_review", scaleRule.Outcome, "Scale bar evidence should remain reviewable.");
        TestAssert.Equal("SCALE : 1cm To 10m R.F 1/1000", scaleRule.EvidenceValue, "Scale bar text should remain visible in the rule value.");
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
