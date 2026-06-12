using ParcelWorkflowAddIn.CaseFolders;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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

        document.RowCount = document.Rows.Count > 0 ? document.Rows.Count : document.RowCount;
        if (string.IsNullOrWhiteSpace(document.ReviewHash))
        {
            document.ReviewHash = ComputeReviewHash(document);
        }

        return document;
    }

    public ExtractionReviewSaveResult Save(CaseFolderLayout layout, ExtractionReviewDocument document, string? operatorId)
    {
        if (document.Rows.Count == 0)
        {
            return ExtractionReviewSaveResult.Failed("Review data is empty. Run extraction before saving review changes.");
        }

        document.ReviewVersion = Math.Max(document.ReviewVersion + 1, 1);
        document.RowCount = document.Rows.Count;
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
            })
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
        return root;
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
        var value = node?[propertyName];
        return value is null ? null : value.GetValue<string?>();
    }

    private static bool ReadBool(JsonObject? node, string propertyName)
    {
        var value = node?[propertyName];
        return value is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var result) && result;
    }

    private static int ReadInt(JsonObject? node, string propertyName)
    {
        var value = node?[propertyName];
        return value is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var result) ? result : 0;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonObject? node, string propertyName)
    {
        if (node?[propertyName] is not JsonArray array)
        {
            return Array.Empty<string>();
        }

        return array
            .Select(item => item?.GetValue<string?>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();
    }
}
