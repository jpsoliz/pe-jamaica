using ParcelWorkflowAddIn.CaseFolders;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;

namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed class ExtractionReviewPersistenceService
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    public string ReviewArtifactFileName => "extraction_review_data.json";

    public string ApprovedReviewArtifactFileName => "approved_review.json";

    public ExtractionReviewDocument? Load(CaseFolderLayout layout)
    {
        var reviewPath = Path.Combine(layout.WorkingDirectory, ReviewArtifactFileName);
        if (!File.Exists(reviewPath))
        {
            return null;
        }

        var rootNode = JsonNode.Parse(File.ReadAllText(reviewPath)) as JsonObject
            ?? throw new InvalidOperationException("Extraction review artifact is not a JSON object.");

        var document = new ExtractionReviewDocument
        {
            SchemaVersion = ReadString(rootNode, "schema_version") ?? "1.0.0",
            TransactionNumber = ReadString(rootNode, "transaction_number") ?? string.Empty,
            ReviewVersion = ReadInt(rootNode, "review_version"),
            ReviewHash = ReadString(rootNode, "review_hash") ?? string.Empty,
            LastSavedAt = ReadString(rootNode, "review_saved_at"),
            LastSavedBy = ReadString(rootNode, "review_saved_by"),
            ExtractionSource = ReadString(rootNode, "extraction_source"),
            RowCount = ReadInt(rootNode, "row_count"),
            SegmentRowCount = ReadInt(rootNode, "segment_row_count"),
            RootMetadata = CloneObject(rootNode)
        };

        foreach (var error in ReadStringArray(rootNode, "errors"))
        {
            document.Errors.Add(error);
        }

        if (rootNode["rows"] is JsonArray rowArray)
        {
            var index = 0;
            foreach (var item in rowArray.OfType<JsonObject>())
            {
                index++;
                document.Rows.Add(MapRow(item, index));
            }
        }

        if (rootNode["segments"] is JsonArray segmentArray)
        {
            var index = 0;
            foreach (var item in segmentArray.OfType<JsonObject>())
            {
                index++;
                document.Segments.Add(MapSegment(item, index));
            }
        }

        LoadSurveyMetadata(rootNode, document);
        ApplyDerivedGrouping(document.Rows);

        document.RowCount = document.Rows.Count > 0 ? document.Rows.Count : document.RowCount;
        document.SegmentRowCount = document.Segments.Count > 0 ? document.Segments.Count : document.SegmentRowCount;
        if (string.IsNullOrWhiteSpace(document.ReviewHash))
        {
            document.ReviewHash = ComputeReviewHash(document);
        }

        return document;
    }

    public ExtractionReviewSaveResult Save(CaseFolderLayout layout, ExtractionReviewDocument document, string? operatorId)
    {
        if (document.Rows.Count == 0 && !IsManualModeDocument(document))
        {
            return ExtractionReviewSaveResult.Failed("Review data is empty. Run extraction before saving review changes.");
        }

        document.ReviewVersion = Math.Max(document.ReviewVersion + 1, 1);
        document.RowCount = document.Rows.Count;
        document.SegmentRowCount = document.Segments.Count;
        document.LastSavedAt = DateTimeOffset.UtcNow.UtcDateTime.ToString("O");
        document.LastSavedBy = operatorId;
        document.ReviewHash = ComputeReviewHash(document);

        var reviewPath = Path.Combine(layout.WorkingDirectory, ReviewArtifactFileName);
        Directory.CreateDirectory(layout.WorkingDirectory);
        File.WriteAllText(reviewPath, SerializeDocument(document).ToJsonString(IndentedJsonOptions));

        InvalidateApprovedArtifact(layout, document.ReviewHash);
        var summary = Summarize(document);
        return new ExtractionReviewSaveResult(true, "Review changes saved to the Case Folder.", document, summary);
    }

    private static bool IsManualModeDocument(ExtractionReviewDocument document)
    {
        if (string.Equals(document.ExtractionSource, "manual_mode", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return document.RootMetadata["manual_mode"]?.GetValue<bool>() == true;
    }

    public ExtractionReviewApprovalResult Approve(CaseFolderLayout layout, ExtractionReviewDocument document, string? operatorId)
    {
        var summary = Summarize(document);
        if (!summary.CanApprove)
        {
            return ExtractionReviewApprovalResult.Failed(BuildApprovalBlockedMessage(summary), summary);
        }

        if (document.ReviewVersion <= 0 || string.IsNullOrWhiteSpace(document.ReviewHash))
        {
            var saveResult = Save(layout, document, operatorId);
            if (!saveResult.Success || saveResult.Document is null)
            {
                return ExtractionReviewApprovalResult.Failed(saveResult.Message, summary);
            }

            document = saveResult.Document;
            summary = saveResult.Summary ?? summary;
        }

        var approvedReviewPath = Path.Combine(layout.WorkingDirectory, ApprovedReviewArtifactFileName);
        var approvalDocument = new ApprovedReviewDocument(
            "1.0.0",
            document.TransactionNumber,
            document.ReviewVersion,
            document.ReviewHash,
            DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
            operatorId,
            summary.TotalRows,
            summary.EditedRows,
            summary.ManualRows,
            summary.UnresolvedRows,
            summary.MissingRequiredRows,
            ReviewArtifactFileName);
        File.WriteAllText(approvedReviewPath, JsonSerializer.Serialize(approvalDocument, IndentedJsonOptions));
        return new ExtractionReviewApprovalResult(true, "Review approved. Downstream geometry generation can now depend on approved review data.", summary, approvedReviewPath);
    }

    public ExtractionReviewSummary Summarize(ExtractionReviewDocument? document)
    {
        if (document is null)
        {
            return new ExtractionReviewSummary(0, 0, 0, 0, 0);
        }

        var editedRows = document.Rows.Count(row => row.IsEdited);
        var manualRows = document.Rows.Count(row => row.IsManual);
        var unresolvedRows = document.Rows.Count(row => row.Unresolved);
        var missingRequiredRows = document.Rows.Count(row => string.IsNullOrWhiteSpace(row.PointIdentifier)
            || string.IsNullOrWhiteSpace(row.Easting)
            || string.IsNullOrWhiteSpace(row.Northing));
        return new ExtractionReviewSummary(document.Rows.Count, editedRows, manualRows, unresolvedRows, missingRequiredRows);
    }

    public string ComputeReviewHash(ExtractionReviewDocument document)
    {
        var payload = JsonSerializer.Serialize(new
        {
            transaction_number = document.TransactionNumber,
            review_version = document.ReviewVersion,
            rows = document.Rows.Select(row => new
            {
                row_id = row.RowId,
                parcel_group_id = row.ParcelGroupId,
                parcel_name = row.ParcelName,
                traverse_id = row.TraverseId,
                sequence_in_group = row.SequenceInGroup,
                is_boundary_break = row.IsBoundaryBreak,
                group_confidence = row.GroupConfidence,
                point_identifier = row.PointIdentifier,
                easting = row.Easting,
                northing = row.Northing,
                length = row.Length,
                extraction_status = row.ExtractionStatus,
                source_evidence = row.SourceEvidence,
                unresolved = row.Unresolved,
                unresolved_reason = row.UnresolvedReason,
                review_notes = row.ReviewNotes,
                row_provenance = row.RowProvenance,
                is_manual = row.IsManual,
                original_values = new
                {
                    point_identifier = row.OriginalValues.PointIdentifier,
                    easting = row.OriginalValues.Easting,
                    northing = row.OriginalValues.Northing,
                    length = row.OriginalValues.Length,
                    extraction_status = row.OriginalValues.ExtractionStatus,
                    source_evidence = row.OriginalValues.SourceEvidence
                }
            }),
            segments = document.Segments.Select(segment => new
            {
                segment_id = segment.SegmentId,
                sequence = segment.Sequence,
                from_point = segment.FromPoint,
                to_point = segment.ToPoint,
                bearing_txt = segment.BearingText,
                distance_txt = segment.DistanceText,
                length_txt = segment.LengthText,
                include_in_boundary = segment.IncludeInBoundary,
                review_sequence = segment.ReviewSequence,
                review_from_point = segment.ReviewFromPoint,
                review_to_point = segment.ReviewToPoint,
                review_bearing_txt = segment.ReviewBearingText,
                review_distance_txt = segment.ReviewDistanceText,
                review_length_txt = segment.ReviewLengthText,
                review_include_in_boundary = segment.ReviewIncludeInBoundary,
                review_status = segment.ReviewStatus,
                review_notes = segment.ReviewNotes,
                adjacent_owner = segment.AdjacentOwner,
                original_values = new
                {
                    sequence = segment.OriginalValues.Sequence,
                    from_point = segment.OriginalValues.FromPoint,
                    to_point = segment.OriginalValues.ToPoint,
                    bearing_txt = segment.OriginalValues.BearingText,
                    distance_txt = segment.OriginalValues.DistanceText,
                    length_txt = segment.OriginalValues.LengthText,
                    include_in_boundary = segment.OriginalValues.IncludeInBoundary
                }
            })
            ,
            survey_metadata = document.SurveyMetadataFields.Select(field => new
            {
                key = field.Key,
                value = field.Value,
                raw_text = field.RawText,
                semantic_state = field.SemanticState,
                unit = field.Unit,
                title = field.Title,
                organization = field.Organization,
                present = field.Present,
                confidence = field.Confidence,
                source_page = field.SourcePage,
                source_zone = field.SourceZone,
                review_status = field.ReviewStatus,
                review_notes = field.ReviewNotes
            }),
            adjacent_owners = document.AdjacentOwners.Select(owner => new
            {
                name = owner.Name,
                role = owner.Role,
                related_segment_from = owner.RelatedSegmentFrom,
                related_segment_to = owner.RelatedSegmentTo,
                volume = owner.Volume,
                folio = owner.Folio,
                source_page = owner.SourcePage,
                source_zone = owner.SourceZone,
                review_status = owner.ReviewStatus,
                review_notes = owner.ReviewNotes
            }),
            parties = document.Parties.Select(party => new
            {
                name = party.Name,
                role = party.Role,
                source_page = party.SourcePage,
                source_zone = party.SourceZone,
                review_status = party.ReviewStatus,
                review_notes = party.ReviewNotes
            }),
            representatives = document.Representatives.Select(representative => new
            {
                name = representative.Name,
                role = representative.Role,
                source_page = representative.SourcePage,
                source_zone = representative.SourceZone,
                review_status = representative.ReviewStatus,
                review_notes = representative.ReviewNotes
            }),
            volume_folios = document.VolumeFolios.Select(volumeFolio => new
            {
                volume = volumeFolio.Volume,
                folio = volumeFolio.Folio,
                raw_text = volumeFolio.RawText,
                source_page = volumeFolio.SourcePage,
                source_zone = volumeFolio.SourceZone,
                review_status = volumeFolio.ReviewStatus,
                review_notes = volumeFolio.ReviewNotes
            }),
            memorandum = new
            {
                detected = document.MemorandumDetected,
                detection_status = document.MemorandumDetectionStatus,
                parties = document.MemorandumParties.Select(party => new
                {
                    name = party.Name,
                    role = party.Role,
                    appearance_mode = party.AppearanceMode,
                    representative = party.Representative,
                    semantic_state = party.SemanticState,
                    source_page = party.SourcePage,
                    source_zone = party.SourceZone,
                    review_status = party.ReviewStatus,
                    review_notes = party.ReviewNotes
                }),
                rules = document.MemorandumRuleResults.Select(rule => new
                {
                    rule_id = rule.RuleId,
                    outcome = rule.Outcome,
                    workflow_effect = rule.WorkflowEffect,
                    message = rule.Message,
                    evidence_value = rule.EvidenceValue,
                    evidence_state = rule.EvidenceState
                })
            }
        });
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static ExtractionReviewRow MapRow(JsonObject rowObject, int index)
    {
        var pointIdentifier = ReadFirstString(rowObject, "point_identifier", "point_id", "point_no", "point_number", "point_name");
        var easting = ReadFirstString(rowObject, "easting", "x", "coord_e", "grid_easting");
        var northing = ReadFirstString(rowObject, "northing", "y", "coord_n", "grid_northing");
        var length = ReadFirstString(rowObject, "length", "distance", "segment_length", "bearing_distance");
        var extractionStatus = ReadFirstString(rowObject, "review_status", "status", "confidence_status", "confidence");
        var sourceEvidence = ReadFirstString(rowObject, "source_evidence", "source_text", "evidence", "page_reference");
        var originalNode = rowObject["review_original_values"] as JsonObject;

        var row = new ExtractionReviewRow
        {
            RowId = ReadFirstString(rowObject, "row_id", "review_row_id") ?? pointIdentifier ?? $"row-{index:000}",
            ParcelGroupId = ReadFirstString(rowObject, "review_parcel_group_id", "parcel_group_id") ?? string.Empty,
            ParcelName = ReadFirstString(rowObject, "review_parcel_name", "parcel_name") ?? string.Empty,
            TraverseId = ReadFirstString(rowObject, "review_traverse_id", "traverse_id") ?? string.Empty,
            SequenceInGroup = ReadNullableInt(rowObject, "review_sequence_in_group", "sequence_in_group"),
            IsBoundaryBreak = ReadBool(rowObject, "review_is_boundary_break") || ReadBool(rowObject, "is_boundary_break"),
            GroupConfidence = ReadFirstString(rowObject, "review_group_confidence", "group_confidence") ?? string.Empty,
            PointIdentifier = ReadFirstString(rowObject, "review_point_identifier", "review_point_identifier_override") ?? pointIdentifier ?? string.Empty,
            Easting = ReadFirstString(rowObject, "review_easting", "review_easting_override") ?? easting ?? string.Empty,
            Northing = ReadFirstString(rowObject, "review_northing", "review_northing_override") ?? northing ?? string.Empty,
            Length = ReadFirstString(rowObject, "review_length", "review_length_override") ?? length ?? string.Empty,
            ExtractionStatus = ReadFirstString(rowObject, "review_extraction_status") ?? extractionStatus ?? string.Empty,
            SourceEvidence = ReadFirstString(rowObject, "review_source_evidence") ?? sourceEvidence ?? string.Empty,
            Unresolved = ReadBool(rowObject, "review_unresolved"),
            UnresolvedReason = ReadFirstString(rowObject, "review_unresolved_reason", "review_reason") ?? string.Empty,
            ReviewNotes = ReadFirstString(rowObject, "review_notes", "notes") ?? string.Empty,
            RowProvenance = ReadFirstString(rowObject, "row_provenance", "review_row_provenance") ?? "extracted",
            IsManual = string.Equals(ReadFirstString(rowObject, "row_provenance", "review_row_provenance"), "manual", StringComparison.OrdinalIgnoreCase),
            RawRow = CloneObject(rowObject)
        };

        row.OriginalValues = new ExtractionReviewOriginalValues
        {
            PointIdentifier = ReadFirstString(originalNode, "point_identifier") ?? pointIdentifier ?? string.Empty,
            Easting = ReadFirstString(originalNode, "easting") ?? easting ?? string.Empty,
            Northing = ReadFirstString(originalNode, "northing") ?? northing ?? string.Empty,
            Length = ReadFirstString(originalNode, "length") ?? length ?? string.Empty,
            ExtractionStatus = ReadFirstString(originalNode, "extraction_status") ?? extractionStatus ?? string.Empty,
            SourceEvidence = ReadFirstString(originalNode, "source_evidence") ?? sourceEvidence ?? string.Empty
        };

        row.IsEdited = row.IsManual
            || HasOverride(row.PointIdentifier, row.OriginalValues.PointIdentifier)
            || HasOverride(row.Easting, row.OriginalValues.Easting)
            || HasOverride(row.Northing, row.OriginalValues.Northing)
            || HasOverride(row.Length, row.OriginalValues.Length)
            || HasOverride(row.ExtractionStatus, row.OriginalValues.ExtractionStatus)
            || HasOverride(row.SourceEvidence, row.OriginalValues.SourceEvidence)
            || row.Unresolved
            || !string.IsNullOrWhiteSpace(row.ReviewNotes);
        return row;
    }

    private static ExtractionReviewSegment MapSegment(JsonObject segmentObject, int index)
    {
        var sequence = ReadNullableInt(segmentObject, "review_sequence", "segment_no", "segment_index", "sequence");
        var fromPoint = ReadFirstString(segmentObject, "from_point", "from_pt", "start_pt") ?? string.Empty;
        var toPoint = ReadFirstString(segmentObject, "to_point", "to_pt", "end_pt") ?? string.Empty;
        var bearing = ReadFirstString(segmentObject, "bearing_txt", "bearing", "course", "direction") ?? string.Empty;
        var distance = ReadFirstString(segmentObject, "distance_txt", "distance", "distance_m") ?? string.Empty;
        var length = ReadFirstString(segmentObject, "length_txt", "length", "length_m") ?? distance;
        var originalNode = segmentObject["review_original_values"] as JsonObject;

        var segment = new ExtractionReviewSegment
        {
            SegmentId = ReadFirstString(segmentObject, "segment_id", "line_id", "row_id") ?? $"segment-{index:000}",
            Sequence = sequence,
            FromPoint = fromPoint,
            ToPoint = toPoint,
            BearingText = bearing,
            DistanceText = distance,
            LengthText = length,
            IncludeInBoundary = ReadNullableBool(segmentObject, "include_in_boundary", "is_boundary_segment") ?? true,
            Confidence = ReadFirstString(segmentObject, "confidence", "group_confidence") ?? string.Empty,
            Status = ReadFirstString(segmentObject, "status", "confidence_status") ?? string.Empty,
            SourcePage = ReadFirstString(segmentObject, "source_page") ?? string.Empty,
            SourceZone = ReadFirstString(segmentObject, "source_zone") ?? string.Empty,
            SourceEvidence = ReadFirstString(segmentObject, "source_evidence", "source_text", "evidence") ?? string.Empty,
            ReviewSequence = ReadNullableInt(segmentObject, "review_sequence"),
            ReviewFromPoint = ReadFirstString(segmentObject, "review_from_point") ?? string.Empty,
            ReviewToPoint = ReadFirstString(segmentObject, "review_to_point") ?? string.Empty,
            ReviewBearingText = ReadFirstString(segmentObject, "review_bearing_txt") ?? string.Empty,
            ReviewDistanceText = ReadFirstString(segmentObject, "review_distance_txt") ?? string.Empty,
            ReviewLengthText = ReadFirstString(segmentObject, "review_length_txt") ?? string.Empty,
            ReviewIncludeInBoundary = ReadNullableBool(segmentObject, "review_include_in_boundary"),
            ReviewStatus = ReadFirstString(segmentObject, "review_status") ?? string.Empty,
            ReviewNotes = ReadFirstString(segmentObject, "review_notes", "review_note", "notes") ?? string.Empty,
            AdjacentOwner = ReadFirstString(segmentObject, "adjacent_owner", "review_adjacent_owner", "adjoining_owner") ?? string.Empty,
            RawSegment = CloneObject(segmentObject)
        };

        segment.OriginalValues = new ExtractionReviewSegmentOriginalValues
        {
            Sequence = ReadNullableInt(originalNode, "sequence") ?? sequence,
            FromPoint = ReadFirstString(originalNode, "from_point") ?? fromPoint,
            ToPoint = ReadFirstString(originalNode, "to_point") ?? toPoint,
            BearingText = ReadFirstString(originalNode, "bearing_txt") ?? bearing,
            DistanceText = ReadFirstString(originalNode, "distance_txt") ?? distance,
            LengthText = ReadFirstString(originalNode, "length_txt") ?? length,
            IncludeInBoundary = ReadNullableBool(originalNode, "include_in_boundary") ?? segment.IncludeInBoundary
        };

        segment.IsEdited = segment.ReviewSequence.HasValue && segment.ReviewSequence != segment.OriginalValues.Sequence
            || HasOverride(segment.ReviewFromPoint, segment.OriginalValues.FromPoint)
            || HasOverride(segment.ReviewToPoint, segment.OriginalValues.ToPoint)
            || HasOverride(segment.ReviewBearingText, segment.OriginalValues.BearingText)
            || HasOverride(segment.ReviewDistanceText, segment.OriginalValues.DistanceText)
            || HasOverride(segment.ReviewLengthText, segment.OriginalValues.LengthText)
            || segment.ReviewIncludeInBoundary.HasValue && segment.ReviewIncludeInBoundary != segment.OriginalValues.IncludeInBoundary
            || !string.IsNullOrWhiteSpace(segment.ReviewStatus)
            || !string.IsNullOrWhiteSpace(segment.ReviewNotes);

        return segment;
    }

    private static JsonObject SerializeDocument(ExtractionReviewDocument document)
    {
        var root = CloneObject(document.RootMetadata);
        root["schema_version"] = document.SchemaVersion;
        root["transaction_number"] = document.TransactionNumber;
        root["review_version"] = document.ReviewVersion;
        root["review_hash"] = document.ReviewHash;
        root["review_saved_at"] = document.LastSavedAt;
        root["review_saved_by"] = document.LastSavedBy;
        root["row_count"] = document.RowCount;
        root["segment_row_count"] = document.SegmentRowCount;
        root["extraction_source"] = document.ExtractionSource;
        root["errors"] = new JsonArray(document.Errors.Select(error => JsonValue.Create(error)).ToArray());
        root["review_summary"] = JsonSerializer.SerializeToNode(new
        {
            total_rows = document.Rows.Count,
            edited_rows = document.Rows.Count(row => row.IsEdited),
            manual_rows = document.Rows.Count(row => row.IsManual),
            unresolved_rows = document.Rows.Count(row => row.Unresolved)
        });

        var rows = new JsonArray();
        foreach (var row in document.Rows)
        {
            var rowObject = CloneObject(row.RawRow);
            rowObject["row_id"] = row.RowId;
            rowObject["parcel_group_id"] = string.IsNullOrWhiteSpace(row.ParcelGroupId) ? null : row.ParcelGroupId;
            rowObject["parcel_name"] = string.IsNullOrWhiteSpace(row.ParcelName) ? null : row.ParcelName;
            rowObject["traverse_id"] = string.IsNullOrWhiteSpace(row.TraverseId) ? null : row.TraverseId;
            rowObject["sequence_in_group"] = row.SequenceInGroup;
            rowObject["is_boundary_break"] = row.IsBoundaryBreak;
            rowObject["group_confidence"] = string.IsNullOrWhiteSpace(row.GroupConfidence) ? null : row.GroupConfidence;
            rowObject["point_identifier"] = row.PointIdentifier;
            rowObject["point_id"] = row.PointIdentifier;
            rowObject["easting"] = row.Easting;
            rowObject["northing"] = row.Northing;
            rowObject["length"] = string.IsNullOrWhiteSpace(row.Length) ? null : row.Length;
            rowObject["status"] = row.ExtractionStatus;
            rowObject["source_evidence"] = row.SourceEvidence;
            rowObject["row_provenance"] = row.IsManual ? "manual" : row.RowProvenance;
            rowObject["review_point_identifier"] = row.PointIdentifier;
            rowObject["review_easting"] = row.Easting;
            rowObject["review_northing"] = row.Northing;
            rowObject["review_length"] = string.IsNullOrWhiteSpace(row.Length) ? null : row.Length;
            rowObject["review_extraction_status"] = row.ExtractionStatus;
            rowObject["review_source_evidence"] = row.SourceEvidence;
            rowObject["review_parcel_group_id"] = string.IsNullOrWhiteSpace(row.ParcelGroupId) ? null : row.ParcelGroupId;
            rowObject["review_parcel_name"] = string.IsNullOrWhiteSpace(row.ParcelName) ? null : row.ParcelName;
            rowObject["review_traverse_id"] = string.IsNullOrWhiteSpace(row.TraverseId) ? null : row.TraverseId;
            rowObject["review_sequence_in_group"] = row.SequenceInGroup;
            rowObject["review_is_boundary_break"] = row.IsBoundaryBreak;
            rowObject["review_group_confidence"] = string.IsNullOrWhiteSpace(row.GroupConfidence) ? null : row.GroupConfidence;
            rowObject["review_unresolved"] = row.Unresolved;
            rowObject["review_unresolved_reason"] = string.IsNullOrWhiteSpace(row.UnresolvedReason) ? null : row.UnresolvedReason;
            rowObject["review_notes"] = string.IsNullOrWhiteSpace(row.ReviewNotes) ? null : row.ReviewNotes;
            rowObject["review_original_values"] = JsonSerializer.SerializeToNode(new
            {
                point_identifier = row.OriginalValues.PointIdentifier,
                easting = row.OriginalValues.Easting,
                northing = row.OriginalValues.Northing,
                length = row.OriginalValues.Length,
                extraction_status = row.OriginalValues.ExtractionStatus,
                source_evidence = row.OriginalValues.SourceEvidence
            });
            rowObject["review_last_modified_at"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O");
            rows.Add(rowObject);
        }

        root["rows"] = rows;
        var segments = new JsonArray();
        foreach (var segment in document.Segments.OrderBy(item => item.EffectiveSequence))
        {
            var segmentObject = CloneObject(segment.RawSegment);
            segmentObject["segment_id"] = segment.SegmentId;
            segmentObject["segment_no"] = segment.Sequence;
            segmentObject["sequence"] = segment.Sequence;
            segmentObject["from_point"] = segment.FromPoint;
            segmentObject["to_point"] = segment.ToPoint;
            segmentObject["bearing_txt"] = segment.BearingText;
            segmentObject["distance_txt"] = segment.DistanceText;
            segmentObject["length_txt"] = string.IsNullOrWhiteSpace(segment.LengthText) ? segment.DistanceText : segment.LengthText;
            segmentObject["include_in_boundary"] = segment.IncludeInBoundary;
            segmentObject["confidence"] = string.IsNullOrWhiteSpace(segment.Confidence) ? null : segment.Confidence;
            segmentObject["status"] = string.IsNullOrWhiteSpace(segment.Status) ? null : segment.Status;
            segmentObject["source_page"] = string.IsNullOrWhiteSpace(segment.SourcePage) ? null : segment.SourcePage;
            segmentObject["source_zone"] = string.IsNullOrWhiteSpace(segment.SourceZone) ? null : segment.SourceZone;
            segmentObject["source_evidence"] = string.IsNullOrWhiteSpace(segment.SourceEvidence) ? null : segment.SourceEvidence;
            segmentObject["review_sequence"] = segment.ReviewSequence;
            segmentObject["review_from_point"] = string.IsNullOrWhiteSpace(segment.ReviewFromPoint) ? null : segment.ReviewFromPoint;
            segmentObject["review_to_point"] = string.IsNullOrWhiteSpace(segment.ReviewToPoint) ? null : segment.ReviewToPoint;
            segmentObject["review_bearing_txt"] = string.IsNullOrWhiteSpace(segment.ReviewBearingText) ? null : segment.ReviewBearingText;
            segmentObject["review_distance_txt"] = string.IsNullOrWhiteSpace(segment.ReviewDistanceText) ? null : segment.ReviewDistanceText;
            segmentObject["review_length_txt"] = string.IsNullOrWhiteSpace(segment.ReviewLengthText) ? null : segment.ReviewLengthText;
            segmentObject["review_include_in_boundary"] = segment.ReviewIncludeInBoundary;
            segmentObject["review_status"] = string.IsNullOrWhiteSpace(segment.ReviewStatus) ? null : segment.ReviewStatus;
            segmentObject["review_notes"] = string.IsNullOrWhiteSpace(segment.ReviewNotes) ? null : segment.ReviewNotes;
            segmentObject["adjacent_owner"] = string.IsNullOrWhiteSpace(segment.AdjacentOwner) ? null : segment.AdjacentOwner;
            segmentObject["review_original_values"] = JsonSerializer.SerializeToNode(new
            {
                sequence = segment.OriginalValues.Sequence,
                from_point = segment.OriginalValues.FromPoint,
                to_point = segment.OriginalValues.ToPoint,
                bearing_txt = segment.OriginalValues.BearingText,
                distance_txt = segment.OriginalValues.DistanceText,
                length_txt = segment.OriginalValues.LengthText,
                include_in_boundary = segment.OriginalValues.IncludeInBoundary
            });
            segmentObject["review_last_modified_at"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O");
            segments.Add(segmentObject);
        }

        root["segments"] = segments;
        WriteSurveyMetadata(root, document);
        return root;
    }

    private static void LoadSurveyMetadata(JsonObject rootNode, ExtractionReviewDocument document)
    {
        document.SurveyMetadataFields.Clear();
        document.AdjacentOwners.Clear();
        document.Parties.Clear();
        document.Representatives.Clear();
        document.VolumeFolios.Clear();
        document.MemorandumParties.Clear();
        document.MemorandumRuleResults.Clear();
        document.MemorandumGroups.Clear();

        AddMetadataField(document, "coordinate_system", "Coordinate system", rootNode["coordinate_system"]);
        AddMetadataField(document, "north_arrow", "North arrow", rootNode["north_arrow"], isPresenceField: true);
        AddMetadataField(document, "scale_bar", "Scale bar", rootNode["scale_bar"], isPresenceField: true);
        AddMetadataField(document, "property_name_near_parcel_diagram", "Property name near parcel diagram", rootNode["property_name_near_parcel_diagram"], isPresenceField: true);

        var surveyMetadata = rootNode["survey_metadata"] as JsonObject;
        AddMetadataField(document, "parish", "Parish", surveyMetadata?["parish"]);
        AddMetadataField(document, "property_name", "Property name", surveyMetadata?["property_name"] ?? rootNode["property_name"]);
        AddMetadataField(document, "document_area", "Document area", surveyMetadata?["document_area"]);
        AddMetadataField(document, "survey_date", "Survey date", surveyMetadata?["survey_date"]);
        AddMetadataField(document, "survey_method", "Survey method", surveyMetadata?["survey_method"]);
        AddMetadataField(document, "grounds_of_objection", "Grounds of objection", surveyMetadata?["grounds_of_objection"] ?? surveyMetadata?["grounds_of_objections"]);
        AddMetadataField(document, "surveyor_decision_grounds", "Surveyor decision grounds", surveyMetadata?["surveyor_decision_grounds"] ?? surveyMetadata?["grounds_of_surveyor_decision"]);
        AddMetadataField(document, "survey_instrument", "Survey instrument", surveyMetadata?["survey_instrument"] ?? surveyMetadata?["instrument"]);
        AddMetadataField(document, "instrument_check_date", "Instrument check date", surveyMetadata?["instrument_check_date"]);
        AddMetadataField(document, "instrument_check_result", "Instrument check result", surveyMetadata?["instrument_check_result"]);
        AddMetadataField(document, "gps_instrument_number", "GPS instrument number", surveyMetadata?["gps_instrument_number"]);
        AddMetadataField(document, "gps_serial_number", "GPS serial number", surveyMetadata?["gps_serial_number"]);
        AddMetadataField(document, "surveyed_by", "Surveyed by / Surveyor", surveyMetadata?["surveyed_by"] ?? surveyMetadata?["surveyor"]);
        AddMetadataField(document, "plan_check_date", "Plan check date", surveyMetadata?["plan_check_date"]);
        AddMetadataField(document, "file_reference", "File reference", surveyMetadata?["file_reference"]);
        AddSurveyedPropertyNameMetadata(rootNode, document);

        if (surveyMetadata?["volume_folio"] is JsonArray volumeFolioArray)
        {
            foreach (var item in volumeFolioArray.OfType<JsonObject>())
            {
                document.VolumeFolios.Add(MapVolumeFolio(item));
            }
        }
        else
        {
            AddMetadataField(document, "volume_folio", "Volume and folio", surveyMetadata?["volume_folio"]);
        }

        if (rootNode["parties"] is JsonArray parties)
        {
            foreach (var item in parties.OfType<JsonObject>())
            {
                document.Parties.Add(MapNamedParty(item));
            }
        }

        if (rootNode["representatives"] is JsonArray representatives)
        {
            foreach (var item in representatives.OfType<JsonObject>())
            {
                document.Representatives.Add(MapNamedParty(item, "representative"));
            }
        }

        if (rootNode["adjacent_owners"] is JsonArray adjacentOwners)
        {
            foreach (var item in adjacentOwners.OfType<JsonObject>())
            {
                document.AdjacentOwners.Add(MapAdjacentOwner(item));
            }
        }

        LoadMemorandumReview(rootNode, document);
    }

    private static void AddMetadataField(
        ExtractionReviewDocument document,
        string key,
        string label,
        JsonNode? sourceNode,
        bool isPresenceField = false)
    {
        var source = sourceNode as JsonObject;
        var value = isPresenceField
            ? ReadPresenceText(sourceNode)
            : ReadFieldValue(sourceNode);
        var present = ReadPresence(sourceNode);
        var rawText = ReadFirstString(source, "raw_text", "source_text", "evidence", "ReviewNote", "review_note") ?? string.Empty;
        var field = new ExtractionReviewMetadataField
        {
            Key = key,
            Label = label,
            Value = value,
            RawText = rawText,
            Confidence = ReadFirstString(source, "confidence", "Confidence") ?? string.Empty,
            SourcePage = ReadFirstString(source, "source_page", "page") ?? string.Empty,
            SourceZone = ReadFirstString(source, "source_zone", "ApproximatePageLocation", "approximate_page_location") ?? string.Empty,
            ReviewStatus = ReadFirstString(source, "review_status") ?? string.Empty,
            ReviewNotes = ReadFirstString(source, "review_notes", "review_note", "ReviewNote") ?? string.Empty,
            SemanticState = NormalizeSemanticState(ReadFirstString(source, "semantic_state", "state"), value, sourceNode),
            Unit = ReadFirstString(source, "unit", "canonical_unit") ?? string.Empty,
            Title = ReadFirstString(source, "title", "surveyor_title") ?? string.Empty,
            Organization = ReadFirstString(source, "organization", "company", "surveyor_organization") ?? string.Empty,
            Present = present,
            OriginalValue = value,
            OriginalRawText = rawText,
            OriginalPresent = present,
            RawField = CloneObject(source)
        };

        if (!string.IsNullOrWhiteSpace(field.Value)
            || !string.IsNullOrWhiteSpace(field.RawText)
            || field.Present.HasValue
            || IsCoreSurveyMetadataKey(key))
        {
            document.SurveyMetadataFields.Add(field);
        }
    }

    private static bool IsCoreSurveyMetadataKey(string key) =>
        key is "coordinate_system"
            or "north_arrow"
            or "parish"
            or "property_name"
            or "document_area"
            or "survey_date"
            or "survey_method"
            or "grounds_of_objection"
            or "surveyor_decision_grounds"
            or "survey_instrument"
            or "instrument_check_date"
            or "instrument_check_result"
            or "gps_instrument_number"
            or "gps_serial_number"
            or "surveyed_by"
            or "volume_folio"
            or "scale_bar"
            or "property_name_near_parcel_diagram"
            or "surveyed_property_name";

    private static ExtractionReviewNamedParty MapNamedParty(JsonObject item, string defaultRole = "")
    {
        return new ExtractionReviewNamedParty
        {
            Name = ReadFirstString(item, "name", "value", "party", "owner", "occupant") ?? string.Empty,
            Role = NormalizePartyRole(ReadFirstString(item, "role", "type") ?? defaultRole),
            SourcePage = ReadFirstString(item, "source_page", "page") ?? string.Empty,
            SourceZone = ReadFirstString(item, "source_zone", "zone") ?? string.Empty,
            ReviewStatus = ReadFirstString(item, "review_status") ?? string.Empty,
            ReviewNotes = ReadFirstString(item, "review_notes", "notes") ?? string.Empty,
            SemanticState = NormalizeSemanticState(ReadFirstString(item, "semantic_state", "state"), ReadFirstString(item, "name", "value", "party", "owner", "occupant") ?? string.Empty, item),
            RawParty = CloneObject(item)
        };
    }

    private static ExtractionReviewMemorandumParty MapMemorandumParty(JsonObject item, string defaultRole)
    {
        return new ExtractionReviewMemorandumParty
        {
            Name = ReadFirstString(item, "name", "value", "party", "owner", "occupant") ?? string.Empty,
            Role = ReadFirstString(item, "role", "type") ?? defaultRole,
            AppearanceMode = ReadFirstString(item, "appearance_mode", "mode") ?? string.Empty,
            Representative = ReadFirstString(item, "representative", "representative_name", "by") ?? string.Empty,
            SourcePage = ReadFirstString(item, "source_page", "page") ?? string.Empty,
            SourceZone = ReadFirstString(item, "source_zone", "zone") ?? string.Empty,
            ReviewStatus = ReadFirstString(item, "review_status") ?? string.Empty,
            ReviewNotes = ReadFirstString(item, "review_notes", "notes") ?? string.Empty,
            SemanticState = NormalizeSemanticState(ReadFirstString(item, "semantic_state", "state"), ReadFirstString(item, "name", "value", "party", "owner", "occupant") ?? string.Empty, item),
            RawParty = CloneObject(item)
        };
    }

    private static void AddSurveyedPropertyNameMetadata(JsonObject rootNode, ExtractionReviewDocument document)
    {
        if (rootNode["surveyed_property_names"] is not JsonArray propertyNames)
        {
            var surveyMetadata = rootNode["survey_metadata"] as JsonObject;
            var fallback = surveyMetadata?["property_name"] ?? rootNode["property_name"];
            if (fallback is not null)
            {
                AddMetadataField(document, "surveyed_property_name", "Surveyed property name", fallback);
            }
            return;
        }

        var first = propertyNames.OfType<JsonObject>().FirstOrDefault();
        if (first is not null)
        {
            AddMetadataField(document, "surveyed_property_name", "Surveyed property name", first);
        }
    }

    private static void LoadMemorandumReview(JsonObject rootNode, ExtractionReviewDocument document)
    {
        var memorandum = rootNode["document_sections"]?["memorandum"] as JsonObject;
        document.MemorandumDetected = ReadNullableBool(memorandum, "detected", "present") == true
            || HasMemorandumEvidence(rootNode, memorandum);
        document.MemorandumDetectionStatus = ReadFirstString(memorandum, "status", "review_status") ?? string.Empty;

        AddMemorandumParties(rootNode, document, "surveyed_for_names", "surveyed_for");
        AddMemorandumParties(rootNode, document, "interested_parties", "interested_party");
        AddMemorandumParties(rootNode, document, "notice_served_on", "notice_served_on");
        AddMemorandumParties(rootNode, document, "appeared_parties", "appeared");

        var ruleService = new PxaMemorandumReviewRuleService();
        foreach (var result in ruleService.Evaluate(document))
        {
            document.MemorandumRuleResults.Add(result);
        }

        foreach (var group in ruleService.BuildGroups(document.MemorandumRuleResults))
        {
            document.MemorandumGroups.Add(group);
        }
    }

    private static bool HasMemorandumEvidence(JsonObject rootNode, JsonObject? memorandum)
    {
        if (ContainsMemorandumText(ReadFirstString(memorandum, "matched_text", "text", "raw_text")))
        {
            return true;
        }

        if (ContainsMemorandumText(ReadFirstString(rootNode, "document_text", "raw_text", "ocr_text", "source_text", "text_content")))
        {
            return true;
        }

        return HasAnyObjectItems(rootNode["surveyed_for_names"])
            || HasAnyObjectItems(rootNode["interested_parties"])
            || HasAnyObjectItems(rootNode["surveyed_property_names"])
            || HasAnyObjectItems(rootNode["notice_served_on"])
            || HasAnyObjectItems(rootNode["appeared_parties"])
            || HasSurveyMetadataFromMemorandum(rootNode);
    }

    private static bool ContainsMemorandumText(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains("MEMORANDUM", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAnyObjectItems(JsonNode? node)
    {
        return node is JsonArray array && array.OfType<JsonObject>().Any();
    }

    private static bool HasSurveyMetadataFromMemorandum(JsonObject rootNode)
    {
        if (rootNode["survey_metadata"] is not JsonObject surveyMetadata)
        {
            return false;
        }

        return surveyMetadata
            .Select(item => item.Value as JsonObject)
            .Where(item => item is not null)
            .Any(item =>
            {
                var sourceZone = ReadFirstString(item, "source_zone", "zone");
                return string.Equals(sourceZone, "memorandum", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(ReadFieldValue(item));
            });
    }

    private static void AddMemorandumParties(JsonObject rootNode, ExtractionReviewDocument document, string propertyName, string role)
    {
        if (rootNode[propertyName] is not JsonArray parties)
        {
            return;
        }

        foreach (var item in parties.OfType<JsonObject>())
        {
            document.MemorandumParties.Add(MapMemorandumParty(item, role));
        }
    }

    private static ExtractionReviewAdjacentOwner MapAdjacentOwner(JsonObject item)
    {
        return new ExtractionReviewAdjacentOwner
        {
            Name = ReadFirstString(item, "name", "value", "owner", "adjacent_owner", "occupant") ?? string.Empty,
            Role = NormalizePartyRole(ReadFirstString(item, "role", "type")),
            LotNumber = ReadFirstString(item, "lot_number", "lot", "lot_no", "lotNumber", "Lot Number") ?? string.Empty,
            Address = ReadFirstString(item, "address", "property_address", "location", "Address") ?? string.Empty,
            LandValuationNumber = ReadFirstString(item, "land_valuation_number", "landval_number", "landval_no", "landvalnumber", "landValuationNumber", "LandVal No.") ?? string.Empty,
            ExaminationNumber = ReadFirstString(item, "examination_number", "exam_number", "exam_no", "examinationNumber", "Exam No") ?? string.Empty,
            RelatedSegmentFrom = ReadFirstString(item, "related_segment_from", "segment_from", "from_point") ?? string.Empty,
            RelatedSegmentTo = ReadFirstString(item, "related_segment_to", "segment_to", "to_point") ?? string.Empty,
            Volume = ReadFirstString(item, "volume", "vol", "Volume", "Vol.") ?? string.Empty,
            Folio = ReadFirstString(item, "folio", "fol", "Folio", "Fol.") ?? string.Empty,
            SourcePage = ReadFirstString(item, "source_page", "page") ?? string.Empty,
            SourceZone = ReadFirstString(item, "source_zone", "zone") ?? string.Empty,
            ReviewStatus = ReadFirstString(item, "review_status") ?? string.Empty,
            ReviewNotes = ReadFirstString(item, "review_notes", "notes") ?? string.Empty,
            RawOwner = CloneObject(item)
        };
    }

    private static ExtractionReviewVolumeFolio MapVolumeFolio(JsonObject item)
    {
        return new ExtractionReviewVolumeFolio
        {
            Volume = ReadFirstString(item, "volume", "vol", "Volume", "Vol.") ?? string.Empty,
            Folio = ReadFirstString(item, "folio", "fol", "Folio", "Fol.") ?? string.Empty,
            RawText = ReadFirstString(item, "raw_text", "value", "text") ?? string.Empty,
            SourcePage = ReadFirstString(item, "source_page", "page") ?? string.Empty,
            SourceZone = ReadFirstString(item, "source_zone", "zone") ?? string.Empty,
            ReviewStatus = ReadFirstString(item, "review_status") ?? string.Empty,
            ReviewNotes = ReadFirstString(item, "review_notes", "notes") ?? string.Empty,
            RawVolumeFolio = CloneObject(item)
        };
    }

    private static string NormalizePartyRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return string.Empty;
        }

        var trimmed = role.Trim();
        var compact = trimmed.TrimEnd('.').Trim();
        return string.Equals(compact, "Occ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(compact, "Occupant", StringComparison.OrdinalIgnoreCase)
            ? "Occupant"
            : trimmed;
    }

    private static void WriteSurveyMetadata(JsonObject root, ExtractionReviewDocument document)
    {
        var metadata = root["survey_metadata"] as JsonObject ?? [];
        foreach (var field in document.SurveyMetadataFields)
        {
            var fieldObject = CloneObject(field.RawField);
            if (field.Present.HasValue)
            {
                fieldObject["present"] = field.Present;
                fieldObject["Detected"] = field.Present;
            }

            if (!string.IsNullOrWhiteSpace(field.Value))
            {
                fieldObject["value"] = field.Value;
            }

            fieldObject["raw_text"] = string.IsNullOrWhiteSpace(field.RawText) ? null : field.RawText;
            fieldObject["confidence"] = string.IsNullOrWhiteSpace(field.Confidence) ? null : field.Confidence;
            fieldObject["source_page"] = string.IsNullOrWhiteSpace(field.SourcePage) ? null : field.SourcePage;
            fieldObject["source_zone"] = string.IsNullOrWhiteSpace(field.SourceZone) ? null : field.SourceZone;
            fieldObject["review_status"] = string.IsNullOrWhiteSpace(field.ReviewStatus) ? null : field.ReviewStatus;
            fieldObject["review_notes"] = string.IsNullOrWhiteSpace(field.ReviewNotes) ? null : field.ReviewNotes;
            fieldObject["semantic_state"] = string.IsNullOrWhiteSpace(field.SemanticState) ? null : field.SemanticState;
            fieldObject["unit"] = string.IsNullOrWhiteSpace(field.Unit) ? fieldObject["unit"]?.DeepClone() : field.Unit;
            fieldObject["title"] = string.IsNullOrWhiteSpace(field.Title) ? fieldObject["title"]?.DeepClone() : field.Title;
            fieldObject["organization"] = string.IsNullOrWhiteSpace(field.Organization) ? fieldObject["organization"]?.DeepClone() : field.Organization;

            switch (field.Key)
            {
                case "coordinate_system":
                    root["coordinate_system"] = fieldObject;
                    break;
                case "north_arrow":
                    root["north_arrow"] = fieldObject;
                    break;
                case "scale_bar":
                    root["scale_bar"] = fieldObject;
                    break;
                case "property_name_near_parcel_diagram":
                    root["property_name_near_parcel_diagram"] = fieldObject;
                    break;
                case "surveyed_property_name":
                    root["surveyed_property_names"] = new JsonArray(fieldObject);
                    break;
                case "survey_instrument":
                    metadata["instrument"] = fieldObject;
                    metadata["survey_instrument"] = fieldObject.DeepClone();
                    break;
                case "surveyed_by":
                    metadata["surveyed_by"] = fieldObject;
                    metadata["surveyor"] = fieldObject.DeepClone();
                    break;
                default:
                    metadata[field.Key] = fieldObject;
                    break;
            }
        }

        if (document.VolumeFolios.Count > 0)
        {
            metadata["volume_folio"] = new JsonArray(document.VolumeFolios.Select(item =>
            {
                var node = CloneObject(item.RawVolumeFolio);
                node["volume"] = string.IsNullOrWhiteSpace(item.Volume) ? null : item.Volume;
                node["folio"] = string.IsNullOrWhiteSpace(item.Folio) ? null : item.Folio;
                node["raw_text"] = string.IsNullOrWhiteSpace(item.RawText) ? null : item.RawText;
                node["source_page"] = string.IsNullOrWhiteSpace(item.SourcePage) ? null : item.SourcePage;
                node["source_zone"] = string.IsNullOrWhiteSpace(item.SourceZone) ? null : item.SourceZone;
                node["review_status"] = string.IsNullOrWhiteSpace(item.ReviewStatus) ? null : item.ReviewStatus;
                node["review_notes"] = string.IsNullOrWhiteSpace(item.ReviewNotes) ? null : item.ReviewNotes;
                return node;
            }).ToArray());
        }

        root["survey_metadata"] = metadata;
        root["parties"] = new JsonArray(document.Parties.Select(item =>
        {
            var node = CloneObject(item.RawParty);
            node["name"] = string.IsNullOrWhiteSpace(item.Name) ? null : item.Name;
            node["role"] = string.IsNullOrWhiteSpace(item.Role) ? null : item.Role;
            node["source_page"] = string.IsNullOrWhiteSpace(item.SourcePage) ? null : item.SourcePage;
            node["source_zone"] = string.IsNullOrWhiteSpace(item.SourceZone) ? null : item.SourceZone;
            node["review_status"] = string.IsNullOrWhiteSpace(item.ReviewStatus) ? null : item.ReviewStatus;
            node["review_notes"] = string.IsNullOrWhiteSpace(item.ReviewNotes) ? null : item.ReviewNotes;
            return node;
        }).ToArray());
        root["representatives"] = new JsonArray(document.Representatives.Select(item =>
        {
            var node = CloneObject(item.RawParty);
            node["name"] = string.IsNullOrWhiteSpace(item.Name) ? null : item.Name;
            node["role"] = string.IsNullOrWhiteSpace(item.Role) ? null : item.Role;
            node["source_page"] = string.IsNullOrWhiteSpace(item.SourcePage) ? null : item.SourcePage;
            node["source_zone"] = string.IsNullOrWhiteSpace(item.SourceZone) ? null : item.SourceZone;
            node["review_status"] = string.IsNullOrWhiteSpace(item.ReviewStatus) ? null : item.ReviewStatus;
            node["review_notes"] = string.IsNullOrWhiteSpace(item.ReviewNotes) ? null : item.ReviewNotes;
            return node;
        }).ToArray());
        root["adjacent_owners"] = new JsonArray(document.AdjacentOwners.Select(item =>
        {
            var node = CloneObject(item.RawOwner);
            node["name"] = string.IsNullOrWhiteSpace(item.Name) ? null : item.Name;
            node["role"] = string.IsNullOrWhiteSpace(item.Role) ? null : item.Role;
            node["lot_number"] = string.IsNullOrWhiteSpace(item.LotNumber) ? null : item.LotNumber;
            node["address"] = string.IsNullOrWhiteSpace(item.Address) ? null : item.Address;
            node["land_valuation_number"] = string.IsNullOrWhiteSpace(item.LandValuationNumber) ? null : item.LandValuationNumber;
            node["examination_number"] = string.IsNullOrWhiteSpace(item.ExaminationNumber) ? null : item.ExaminationNumber;
            node["related_segment_from"] = string.IsNullOrWhiteSpace(item.RelatedSegmentFrom) ? null : item.RelatedSegmentFrom;
            node["related_segment_to"] = string.IsNullOrWhiteSpace(item.RelatedSegmentTo) ? null : item.RelatedSegmentTo;
            node["volume"] = string.IsNullOrWhiteSpace(item.Volume) ? null : item.Volume;
            node["folio"] = string.IsNullOrWhiteSpace(item.Folio) ? null : item.Folio;
            node["source_page"] = string.IsNullOrWhiteSpace(item.SourcePage) ? null : item.SourcePage;
            node["source_zone"] = string.IsNullOrWhiteSpace(item.SourceZone) ? null : item.SourceZone;
            node["review_status"] = string.IsNullOrWhiteSpace(item.ReviewStatus) ? null : item.ReviewStatus;
            node["review_notes"] = string.IsNullOrWhiteSpace(item.ReviewNotes) ? null : item.ReviewNotes;
            return node;
        }).ToArray());

        var documentSections = root["document_sections"] as JsonObject ?? [];
        var memorandum = documentSections["memorandum"] as JsonObject ?? [];
        memorandum["detected"] = document.MemorandumDetected;
        memorandum["status"] = string.IsNullOrWhiteSpace(document.MemorandumDetectionStatus) ? null : document.MemorandumDetectionStatus;
        documentSections["memorandum"] = memorandum;
        root["document_sections"] = documentSections;
        root["surveyed_for_names"] = WriteMemorandumParties(document.MemorandumParties, "surveyed_for");
        root["interested_parties"] = WriteMemorandumParties(document.MemorandumParties, "interested_party");
        root["notice_served_on"] = WriteMemorandumParties(document.MemorandumParties, "notice_served_on");
        root["appeared_parties"] = WriteMemorandumParties(document.MemorandumParties, "appeared");
        root["memorandum_rule_results"] = new JsonArray(document.MemorandumRuleResults.Select(rule =>
            JsonSerializer.SerializeToNode(new
            {
                rule_id = rule.RuleId,
                group = rule.Group,
                label = rule.Label,
                outcome = rule.Outcome,
                reviewer_status = rule.ReviewerStatus,
                workflow_effect = rule.WorkflowEffect,
                message = rule.Message,
                evidence_value = rule.EvidenceValue,
                evidence_state = rule.EvidenceState,
                source_page = rule.SourcePage,
                source_zone = rule.SourceZone,
                report_visible = rule.ReportVisible
            })).ToArray());
    }

    private static JsonArray WriteMemorandumParties(IEnumerable<ExtractionReviewMemorandumParty> parties, string role)
    {
        return new JsonArray(parties
            .Where(item => string.Equals(item.Role, role, StringComparison.OrdinalIgnoreCase))
            .Select(item =>
            {
                var node = CloneObject(item.RawParty);
                node["name"] = string.IsNullOrWhiteSpace(item.Name) ? null : item.Name;
                node["role"] = string.IsNullOrWhiteSpace(item.Role) ? null : item.Role;
                node["appearance_mode"] = string.IsNullOrWhiteSpace(item.AppearanceMode) ? null : item.AppearanceMode;
                node["representative"] = string.IsNullOrWhiteSpace(item.Representative) ? null : item.Representative;
                node["source_page"] = string.IsNullOrWhiteSpace(item.SourcePage) ? null : item.SourcePage;
                node["source_zone"] = string.IsNullOrWhiteSpace(item.SourceZone) ? null : item.SourceZone;
                node["review_status"] = string.IsNullOrWhiteSpace(item.ReviewStatus) ? null : item.ReviewStatus;
                node["review_notes"] = string.IsNullOrWhiteSpace(item.ReviewNotes) ? null : item.ReviewNotes;
                node["semantic_state"] = string.IsNullOrWhiteSpace(item.SemanticState) ? null : item.SemanticState;
                return node;
            })
            .ToArray());
    }

    private static string ReadFieldValue(JsonNode? sourceNode)
    {
        if (sourceNode is JsonObject source)
        {
            return ReadFirstString(source, "review_value", "value", "Value", "text", "raw_value", "raw_text") ?? string.Empty;
        }

        return ReadScalarString(sourceNode) ?? string.Empty;
    }

    private static string ReadPresenceText(JsonNode? sourceNode)
    {
        var value = ReadFieldValue(sourceNode);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var present = ReadPresence(sourceNode);
        if (present.HasValue)
        {
            return present.Value ? "Present" : "Not present";
        }

        return string.Empty;
    }

    private static bool? ReadPresence(JsonNode? sourceNode)
    {
        if (sourceNode is JsonObject source)
        {
            return ReadNullableBool(source, "present", "detected", "Detected");
        }

        if (sourceNode is JsonValue value && value.TryGetValue<bool>(out var boolValue))
        {
            return boolValue;
        }

        return null;
    }

    private static string NormalizeSemanticState(string? explicitState, string? value, JsonNode? sourceNode)
    {
        if (!string.IsNullOrWhiteSpace(explicitState))
        {
            var normalized = explicitState.Trim().ToUpperInvariant();
            return normalized switch
            {
                "VALUE" or "NONE" or "N_A" or "NOT_STATED" or "NOT_FOUND" or "ILLEGIBLE" or "NO_ONE_APPEARED" or "UNKNOWN" => normalized,
                "NA" or "N/A" or "NOT APPLICABLE" or "NOT_APPLICABLE" => "N_A",
                _ => "UNKNOWN"
            };
        }

        var text = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return sourceNode is null ? "NOT_FOUND" : "NOT_STATED";
        }

        if (string.Equals(text, "none", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "nil", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "no objections", StringComparison.OrdinalIgnoreCase))
        {
            return "NONE";
        }

        if (string.Equals(text, "n/a", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "na", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "not applicable", StringComparison.OrdinalIgnoreCase))
        {
            return "N_A";
        }

        if (text.Contains("no one appeared", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "none appeared", StringComparison.OrdinalIgnoreCase))
        {
            return "NO_ONE_APPEARED";
        }

        if (string.Equals(text, "illegible", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "unreadable", StringComparison.OrdinalIgnoreCase))
        {
            return "ILLEGIBLE";
        }

        return "VALUE";
    }

    private static void InvalidateApprovedArtifact(CaseFolderLayout layout, string currentReviewHash)
    {
        var approvedPath = Path.Combine(layout.WorkingDirectory, "approved_review.json");
        if (!File.Exists(approvedPath))
        {
            return;
        }

        try
        {
            var approvedNode = JsonNode.Parse(File.ReadAllText(approvedPath)) as JsonObject;
            var approvedHash = ReadString(approvedNode, "review_hash");
            if (!string.Equals(approvedHash, currentReviewHash, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(approvedPath);
            }
        }
        catch (Exception) when (File.Exists(approvedPath))
        {
            File.Delete(approvedPath);
        }
    }

    private static string BuildApprovalBlockedMessage(ExtractionReviewSummary summary)
    {
        if (summary.UnresolvedRows > 0 && summary.MissingRequiredRows > 0)
        {
            return $"Review approval blocked: {summary.UnresolvedRows} unresolved row(s) and {summary.MissingRequiredRows} row(s) still missing required values.";
        }

        if (summary.UnresolvedRows > 0)
        {
            return $"Review approval blocked: {summary.UnresolvedRows} unresolved row(s) remain.";
        }

        if (summary.MissingRequiredRows > 0)
        {
            return $"Review approval blocked: {summary.MissingRequiredRows} row(s) are still missing point id or coordinates.";
        }

        return "Review approval blocked.";
    }

    private static bool HasOverride(string current, string original)
    {
        return !string.Equals(current?.Trim(), original?.Trim(), StringComparison.Ordinal);
    }

    private static void ApplyDerivedGrouping(IReadOnlyList<ExtractionReviewRow> rows)
    {
        var sequencesByGroup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var effectiveGroupId = ResolveEffectiveGroupId(row);
            if (string.IsNullOrWhiteSpace(effectiveGroupId))
            {
                row.GroupConfidence = string.IsNullOrWhiteSpace(row.GroupConfidence) ? "unknown" : row.GroupConfidence;
                continue;
            }

            row.ParcelGroupId = string.IsNullOrWhiteSpace(row.ParcelGroupId) ? effectiveGroupId : row.ParcelGroupId;
            row.TraverseId = string.IsNullOrWhiteSpace(row.TraverseId) ? row.ParcelGroupId : row.TraverseId;

            if (!sequencesByGroup.TryGetValue(row.ParcelGroupId, out var currentSequence))
            {
                currentSequence = 0;
            }

            currentSequence++;
            sequencesByGroup[row.ParcelGroupId] = currentSequence;
            row.SequenceInGroup ??= currentSequence;

            if (string.IsNullOrWhiteSpace(row.GroupConfidence))
            {
                row.GroupConfidence = string.IsNullOrWhiteSpace(row.ParcelName)
                    ? "inferred_single_group"
                    : "derived_from_parcel_name";
            }
        }
    }

    private static string ResolveEffectiveGroupId(ExtractionReviewRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.ParcelGroupId))
        {
            return row.ParcelGroupId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(row.TraverseId))
        {
            return row.TraverseId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(row.ParcelName))
        {
            return row.ParcelName.Trim();
        }

        return string.Empty;
    }

    private static JsonObject CloneObject(JsonObject? source)
    {
        return source?.DeepClone() as JsonObject ?? [];
    }

    private static string? ReadFirstString(JsonObject? node, params string[] propertyNames)
    {
        if (node is null)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            var value = ReadString(node, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadString(JsonObject? node, string propertyName)
    {
        return ReadScalarString(node?[propertyName]);
    }

    private static bool ReadBool(JsonObject? node, string propertyName)
    {
        var value = node?[propertyName];
        return value is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var result) && result;
    }

    private static bool? ReadNullableBool(JsonObject? node, params string[] propertyNames)
    {
        if (node is null)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            var value = node[propertyName];
            if (value is not JsonValue jsonValue)
            {
                continue;
            }

            if (jsonValue.TryGetValue<bool>(out var boolValue))
            {
                return boolValue;
            }

            if (jsonValue.TryGetValue<string>(out var textValue)
                && bool.TryParse(textValue, out var parsedValue))
            {
                return parsedValue;
            }
        }

        return null;
    }

    private static int ReadInt(JsonObject? node, string propertyName)
    {
        var value = node?[propertyName];
        return value is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var result) ? result : 0;
    }

    private static int? ReadNullableInt(JsonObject? node, params string[] propertyNames)
    {
        if (node is null)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            var value = node[propertyName];
            if (value is not JsonValue jsonValue)
            {
                continue;
            }

            if (jsonValue.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (jsonValue.TryGetValue<string>(out var textValue)
                && int.TryParse(textValue, out var parsedValue))
            {
                return parsedValue;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonObject? node, string propertyName)
    {
        if (node?[propertyName] is not JsonArray array)
        {
            return Array.Empty<string>();
        }

        return array
            .Select(ReadScalarString)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();
    }

    private static string? ReadScalarString(JsonNode? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is not JsonValue jsonValue)
        {
            return null;
        }

        if (jsonValue.TryGetValue<string>(out var stringValue))
        {
            return stringValue;
        }

        if (jsonValue.TryGetValue<int>(out var intValue))
        {
            return intValue.ToString(CultureInfo.InvariantCulture);
        }

        if (jsonValue.TryGetValue<long>(out var longValue))
        {
            return longValue.ToString(CultureInfo.InvariantCulture);
        }

        if (jsonValue.TryGetValue<double>(out var doubleValue))
        {
            return doubleValue.ToString(CultureInfo.InvariantCulture);
        }

        if (jsonValue.TryGetValue<decimal>(out var decimalValue))
        {
            return decimalValue.ToString(CultureInfo.InvariantCulture);
        }

        if (jsonValue.TryGetValue<bool>(out var boolValue))
        {
            return boolValue ? "true" : "false";
        }

        return null;
    }
}
