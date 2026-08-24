using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow.Disposition;
using ParcelWorkflowAddIn.Workflow.Output;
using ParcelWorkflowAddIn.Workflow.Review;
using ParcelWorkflowAddIn.Workflow.SpatialReview;

namespace ParcelWorkflowAddIn.Workflow.Reports;

public interface IComputeExaminationReportService
{
    Task<ComputeExaminationReportResult> GenerateAsync(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        ComputeReviewDispositionDocument disposition,
        string? operatorId,
        CancellationToken cancellationToken = default);
}

public sealed record ComputeExaminationReportResult(
    bool Success,
    string Message,
    string? ReportPath,
    string? ErrorCategory,
    string? PdfReportPath = null)
{
    public static ComputeExaminationReportResult Succeeded(string reportPath, string? pdfReportPath = null)
    {
        return new ComputeExaminationReportResult(true, "Compute examination report generated.", reportPath, null, pdfReportPath);
    }

    public static ComputeExaminationReportResult Failed(string message, string? errorCategory = null)
    {
        return new ComputeExaminationReportResult(false, message, null, errorCategory);
    }
}

public sealed class ComputeExaminationReportService : IComputeExaminationReportService
{
    public const string ReportFileName = "compute_examination_report.json";
    public const string PdfReportFileName = "compute_examination_report.pdf";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public Task<ComputeExaminationReportResult> GenerateAsync(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        ComputeReviewDispositionDocument disposition,
        string? operatorId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var manifest = RequireManifest(layout);
            var outputSummary = RequireJsonArtifact(layout, layout.OutputDirectory, "output_summary.json", "Create Spatial Units");
            var enterprisePublish = RequireJsonArtifact(layout, layout.OutputDirectory, "enterprise_working_publish.json", "Enterprise working-layer publish");
            var enterpriseDisposition = RequireJsonArtifact(layout, layout.WorkingDirectory, "enterprise_working_disposition.json", "Enterprise disposition writeback");
            var spatialReviewApproval = RequireJsonArtifact(layout, layout.WorkingDirectory, "spatial_review_approval.json", "Final Review");
            var dispositionArtifact = RequireJsonArtifact(layout, layout.WorkingDirectory, ComputeReviewDispositionPersistenceService.DispositionArtifactFileName, "Compute disposition");
            using var approvedReview = ReadOptionalJson(Path.Combine(layout.WorkingDirectory, "approved_review.json"));
            using var extractionReviewData = ReadOptionalJson(Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"));
            var reviewedData = new ExtractionReviewPersistenceService().Load(layout);
            var generatedAtUtc = DateTimeOffset.UtcNow.UtcDateTime.ToString("O");

            var report = new ComputeExaminationReportDocument(
                "compute_examination_report_v1",
                transaction.TransactionId,
                transaction.TransactionNumber,
                transaction.TaskId,
                generatedAtUtc,
                operatorId,
                manifest.RunId,
                new[]
                {
                    BuildPreflightStage(layout.StructureCheckSummaryPath, "structure_check", "Structure Check"),
                    BuildPreflightStage(layout.GeoreferenceCheckSummaryPath, "georeference_check", "Georeference Check"),
                    BuildPreflightStage(layout.DimensionCheckSummaryPath, "dimension_check", "Dimension Check"),
                    BuildArtifactStage("validate_points_and_lines", "Validate Points and Lines", layout, Path.Combine(layout.WorkingDirectory, "approved_review.json")),
                    BuildJsonStage("create_spatial_units", "Create Spatial Units", outputSummary),
                    BuildJsonStage("final_review", "Final Review", spatialReviewApproval),
                    BuildJsonStage("enterprise_working_publish", "Enterprise working-layer publish", enterprisePublish),
                    BuildJsonStage("enterprise_disposition", "Enterprise disposition writeback", enterpriseDisposition),
                    BuildJsonStage("innola_spatial_unit", "Innola Spatial Unit creation/update", dispositionArtifact),
                    BuildJsonStage("working_package_attachment", "Working package attachment", dispositionArtifact)
                },
                new ComputeExaminationReportCloseout(
                    disposition.Decision,
                    disposition.OperatorId,
                    disposition.DecidedAtUtc,
                    disposition.EnterpriseDispositionStatus,
                    disposition.EnterpriseDispositionRef,
                    disposition.SpatialUnitApiStatus,
                    disposition.SpatialUnitId,
                    disposition.WorkingPackageFileName,
                    disposition.WorkingPackageSourceType,
                    disposition.WorkingPackageUploadStatus),
                new[]
                {
                    MakeReference(layout, layout.ManifestPath),
                    MakeReference(layout, layout.StructureCheckSummaryPath),
                    MakeReference(layout, layout.GeoreferenceCheckSummaryPath),
                    MakeReference(layout, layout.DimensionCheckSummaryPath),
                    MakeReference(layout, Path.Combine(layout.WorkingDirectory, "approved_review.json")),
                    MakeReference(layout, Path.Combine(layout.WorkingDirectory, "spatial_review_approval.json")),
                    MakeReference(layout, Path.Combine(layout.WorkingDirectory, "enterprise_working_disposition.json")),
                    MakeReference(layout, Path.Combine(layout.WorkingDirectory, ComputeReviewDispositionPersistenceService.DispositionArtifactFileName)),
                    MakeReference(layout, Path.Combine(layout.OutputDirectory, "output_summary.json")),
                    MakeReference(layout, Path.Combine(layout.OutputDirectory, "enterprise_working_publish.json"))
                },
                BuildTransactionInfo(manifest, transaction, generatedAtUtc, operatorId),
                BuildGeneralInfo(layout, reviewedData),
                BuildVolumeFolios(reviewedData, approvedReview, extractionReviewData),
                BuildParticipants(reviewedData, approvedReview, extractionReviewData),
                BuildAdjacentOwners(reviewedData),
                BuildMemorandumFindings(reviewedData),
                BuildBoundarySegments(reviewedData, approvedReview, extractionReviewData),
                BuildPoints(reviewedData, approvedReview, extractionReviewData));

            Directory.CreateDirectory(layout.ReportsDirectory);
            var reportPath = Path.Combine(layout.ReportsDirectory, ReportFileName);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions));
            var pdfReportPath = Path.Combine(layout.ReportsDirectory, PdfReportFileName);
            SimplePdfReportWriter.Write(pdfReportPath, report);
            return Task.FromResult(ComputeExaminationReportResult.Succeeded(reportPath, pdfReportPath));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidOperationException
            or NotSupportedException)
        {
            return Task.FromResult(ComputeExaminationReportResult.Failed(
                $"Compute examination report could not be generated: {exception.Message}",
                exception.GetType().Name));
        }
    }

    private static ManifestDocument RequireManifest(CaseFolderLayout layout)
    {
        if (!File.Exists(layout.ManifestPath))
        {
            throw new InvalidOperationException("manifest.json is missing.");
        }

        return ManifestSerializer.Read(layout.ManifestPath);
    }

    private static JsonDocument RequireJsonArtifact(CaseFolderLayout layout, string directory, string fileName, string stageName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"{stageName} evidence is missing: {Path.GetRelativePath(layout.RootDirectory, path)}.");
        }

        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static JsonDocument? ReadOptionalJson(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ComputeExaminationReportGeneralInfo BuildTransactionInfo(
        ManifestDocument manifest,
        SelectedInnolaTransaction transaction,
        string generatedAtUtc,
        string? operatorId)
    {
        var innola = manifest.Payload.InnolaTransaction;
        var lifecycle = manifest.Payload.InnolaLifecycle;

        return new ComputeExaminationReportGeneralInfo(new[]
        {
            new ComputeExaminationReportFieldValue("Transaction", transaction.TransactionNumber),
            new ComputeExaminationReportFieldValue("Transaction Id", transaction.TransactionId),
            new ComputeExaminationReportFieldValue("Transaction Type", transaction.TransactionType ?? innola?.CaseType ?? "Not provided"),
            new ComputeExaminationReportFieldValue("Task", transaction.TaskName),
            new ComputeExaminationReportFieldValue("Stage", transaction.ProcessStep),
            new ComputeExaminationReportFieldValue("Status", transaction.Status.ToString()),
            new ComputeExaminationReportFieldValue("Selected At UTC", transaction.SelectedAt.UtcDateTime.ToString("O")),
            new ComputeExaminationReportFieldValue("Loaded At UTC", innola?.LoadedAt ?? "Not provided"),
            new ComputeExaminationReportFieldValue("Assigned To", FirstNonBlank(transaction.AssignedUser, innola?.AssignedUser, transaction.AssignedGroup, innola?.AssignedGroup) ?? "Not provided"),
            new ComputeExaminationReportFieldValue("Applicant", "Not provided"),
            new ComputeExaminationReportFieldValue("Owner / Responsible", FirstNonBlank(innola?.OwnerUser, transaction.AssignedUser, innola?.AssignedUser) ?? "Not provided"),
            new ComputeExaminationReportFieldValue("Operator", operatorId ?? "Not provided"),
            new ComputeExaminationReportFieldValue("Source System", SafeLoadInnolaServerUrl()),
            new ComputeExaminationReportFieldValue("Generated At UTC", generatedAtUtc)
        });
    }

    private static ComputeExaminationReportGeneralInfo BuildGeneralInfo(
        CaseFolderLayout layout,
        ExtractionReviewDocument? reviewedData)
    {
        var sourceDocument = Directory.Exists(layout.SourceDirectory)
            ? Directory.EnumerateFiles(layout.SourceDirectory)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Select(Path.GetFileName)
                .FirstOrDefault()
            : null;

        var fields = new List<ComputeExaminationReportFieldValue>
        {
            BuildMetadataField(reviewedData, "Coordinate system", "coordinate_system", "JAD2001 / EPSG:3448 metres"),
            BuildMetadataField(reviewedData, "Document area", "document_area", "Not provided"),
            BuildMetadataField(reviewedData, "File reference", "file_reference", "Not provided"),
            BuildMetadataField(reviewedData, "Grounds of objection", "grounds_of_objection", "Not provided"),
            BuildMetadataField(reviewedData, "North arrow", "north_arrow", "Not provided"),
            BuildMetadataField(reviewedData, "Parish", "parish", "Not provided"),
            BuildMetadataField(reviewedData, "Scale bar", "scale_bar", "Not provided"),
            BuildMetadataField(reviewedData, "Surveyor decision grounds", "surveyor_decision_grounds", "Not provided"),
            BuildMetadataField(reviewedData, "Surveyed property name", "surveyed_property_name", "Not provided"),
            BuildMetadataField(reviewedData, "Property name near parcel diagram", "property_name_near_parcel_diagram", "Not provided"),
            BuildMetadataField(reviewedData, "Instrument check date", "instrument_check_date", "Not provided"),
            BuildMetadataField(reviewedData, "Instrument check result", "instrument_check_result", "Not provided"),
            BuildMetadataField(reviewedData, "GPS instrument number", "gps_instrument_number", "Not provided"),
            BuildMetadataField(reviewedData, "GPS serial number", "gps_serial_number", "Not provided"),
            BuildMetadataField(reviewedData, "Plan check date", "plan_check_date", "Not provided"),
            BuildMetadataField(reviewedData, "Survey date", "survey_date", "Not provided"),
            BuildMetadataField(reviewedData, "Survey instrument", "survey_instrument", "Not provided"),
            BuildMetadataField(reviewedData, "Surveyed by / Surveyor", "surveyed_by", "Not provided"),
            BuildMetadataField(reviewedData, "Registration details", "registration_details", "Not provided"),
            new("Source document", sourceDocument ?? "Not found")
        };

        return new ComputeExaminationReportGeneralInfo(fields);
    }

    private static IReadOnlyList<ComputeExaminationReportVolumeFolio> BuildVolumeFolios(
        ExtractionReviewDocument? reviewedData,
        JsonDocument? approvedReview,
        JsonDocument? extractionReviewData)
    {
        if (reviewedData?.VolumeFolios.Count > 0)
        {
            return reviewedData.VolumeFolios
                .Select(item => new ComputeExaminationReportVolumeFolio(
                    NonEmpty(item.Volume),
                    NonEmpty(item.Folio),
                    NonEmpty(item.RawText),
                    BuildSourceText(item.SourcePage, item.SourceZone),
                    BuildStatusText(item.ReviewStatus, item.ReviewNotes)))
                .ToArray();
        }

        var fallback = FindFirstString(approvedReview, extractionReviewData, "volume_folio", "volumeFolio", "volume/folio", "vol_folio", "volFolio");
        if (string.IsNullOrWhiteSpace(fallback))
        {
            return Array.Empty<ComputeExaminationReportVolumeFolio>();
        }

        var parts = fallback.Split(new[] { '/', '\\' }, 2, StringSplitOptions.TrimEntries);
        return new[]
        {
            new ComputeExaminationReportVolumeFolio(
                parts.Length > 0 ? parts[0] : string.Empty,
                parts.Length > 1 ? parts[1] : string.Empty,
                fallback,
                "review artifact",
                string.Empty)
        };
    }

    private static IReadOnlyList<ComputeExaminationReportParticipant> BuildParticipants(
        ExtractionReviewDocument? reviewedData,
        JsonDocument? approvedReview,
        JsonDocument? extractionReviewData)
    {
        var participants = new List<ComputeExaminationReportParticipant>();

        if (reviewedData is not null)
        {
            foreach (var party in reviewedData.Parties)
            {
                AddParticipant(participants, "Party / Owner", party.Name, party.Role, BuildSourceText(party.SourcePage, party.SourceZone), BuildStatusText(party.ReviewStatus, party.ReviewNotes));
            }

            foreach (var representative in reviewedData.Representatives)
            {
                AddParticipant(participants, "Representative", representative.Name, representative.Role, BuildSourceText(representative.SourcePage, representative.SourceZone), BuildStatusText(representative.ReviewStatus, representative.ReviewNotes));
            }

            foreach (var party in reviewedData.MemorandumParties)
            {
                AddParticipant(
                    participants,
                    "Memorandum",
                    party.Name,
                    BuildMemorandumPartyRole(party),
                    BuildSourceText(party.SourcePage, party.SourceZone),
                    BuildStatusText(party.ReviewStatus, party.ReviewNotes));
            }
        }

        foreach (var root in ExistingRoots(approvedReview, extractionReviewData))
        {
            foreach (var propertyName in new[] { "owner", "owners", "neighbor", "neighbors", "occupant", "occupants", "possessor", "possessors", "participant", "participants", "applicant", "applicants" })
            {
                foreach (var value in FindStringsByProperty(root, propertyName))
                {
                    AddParticipant(participants, "Participant", value, "Participant", string.Empty, string.Empty);
                }
            }
        }

        return participants
            .Where(participant => !string.IsNullOrWhiteSpace(participant.Name))
            .GroupBy(participant => $"{participant.Group}|{participant.Name}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(participant => participant.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<ComputeExaminationReportAdjacentOwner> BuildAdjacentOwners(ExtractionReviewDocument? reviewedData)
    {
        if (reviewedData is null || reviewedData.AdjacentOwners.Count == 0)
        {
            return Array.Empty<ComputeExaminationReportAdjacentOwner>();
        }

        return reviewedData.AdjacentOwners
            .Where(owner => !string.IsNullOrWhiteSpace(owner.Name))
            .Select(owner => new ComputeExaminationReportAdjacentOwner(
                owner.Name.Trim(),
                NormalizeParticipantRole(owner.Role),
                NonEmpty(owner.RelatedSegmentFrom),
                NonEmpty(owner.RelatedSegmentTo),
                NonEmpty(owner.Volume),
                NonEmpty(owner.Folio),
                BuildStatusText(owner.ReviewStatus, owner.ReviewNotes)))
            .ToArray();
    }

    private static IReadOnlyList<ComputeExaminationReportMemorandumFinding> BuildMemorandumFindings(ExtractionReviewDocument? reviewedData)
    {
        if (reviewedData is null || reviewedData.MemorandumRuleResults.Count == 0)
        {
            return Array.Empty<ComputeExaminationReportMemorandumFinding>();
        }

        return reviewedData.MemorandumRuleResults
            .Where(rule => rule.ReportVisible)
            .Select(rule => new ComputeExaminationReportMemorandumFinding(
                rule.Group,
                rule.Label,
                rule.Outcome,
                rule.ReviewerStatus,
                rule.WorkflowEffect,
                BuildSourceText(rule.SourcePage, rule.SourceZone),
                NonEmpty(rule.Message),
                string.IsNullOrWhiteSpace(rule.EvidenceState) ? null : rule.EvidenceState))
            .ToArray();
    }

    private static IReadOnlyList<ComputeExaminationReportBoundarySegment> BuildBoundarySegments(
        ExtractionReviewDocument? reviewedData,
        params JsonDocument?[] documents)
    {
        if (reviewedData?.Segments.Count > 0)
        {
            return reviewedData.Segments
                .Where(segment => segment.EffectiveIncludeInBoundary)
                .OrderBy(segment => segment.EffectiveSequence)
                .Select(segment => new ComputeExaminationReportBoundarySegment(
                    segment.EffectiveSequence == int.MaxValue ? string.Empty : segment.EffectiveSequence.ToString(),
                    NonEmpty(segment.EffectiveFromPoint),
                    NonEmpty(segment.EffectiveToPoint),
                    NonEmpty(segment.EffectiveBearingText),
                    FirstNonBlank(segment.EffectiveDistanceText, segment.EffectiveLengthText) ?? string.Empty,
                    FirstNonBlank(segment.ReviewNotes, segment.Status, segment.AdjacentOwner) ?? string.Empty,
                    true))
                .ToArray();
        }

        return ExistingRoots(documents)
            .SelectMany(EnumerateObjects)
            .Where(IsLikelyBoundarySegment)
            .Select(obj => new ComputeExaminationReportBoundarySegment(
                ReadFlexibleString(obj, "seq", "sequence", "segment_seq", "segmentSequence") ?? string.Empty,
                ReadFlexibleString(obj, "from", "from_point", "fromPoint", "from_label") ?? string.Empty,
                ReadFlexibleString(obj, "to", "to_point", "toPoint", "to_label") ?? string.Empty,
                ReadFlexibleString(obj, "bearing", "bearing_text", "bearingText") ?? string.Empty,
                ReadFlexibleString(obj, "distance", "distance_m", "distanceText", "distance_text") ?? string.Empty,
                ReadFlexibleString(obj, "notes", "note", "status", "review_note") ?? string.Empty,
                ReadFlexibleBool(obj, "use_for_points", "useForPoints", "use", "used") ?? true))
            .Where(segment => segment.UseForPoints)
            .GroupBy(segment => $"{segment.Sequence}|{segment.From}|{segment.To}|{segment.Bearing}|{segment.Distance}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static IReadOnlyList<ComputeExaminationReportPoint> BuildPoints(
        ExtractionReviewDocument? reviewedData,
        params JsonDocument?[] documents)
    {
        if (reviewedData?.Rows.Count > 0)
        {
            return reviewedData.Rows
                .Where(row => !string.IsNullOrWhiteSpace(row.PointIdentifier))
                .OrderBy(row => row.SequenceInGroup ?? int.MaxValue)
                .ThenBy(row => row.PointIdentifier, StringComparer.OrdinalIgnoreCase)
                .Select(row => new ComputeExaminationReportPoint(
                    row.PointIdentifier.Trim(),
                    NonEmpty(row.Easting),
                    NonEmpty(row.Northing),
                    row.SequenceInGroup?.ToString() ?? string.Empty))
                .ToArray();
        }

        return ExistingRoots(documents)
            .SelectMany(EnumerateObjects)
            .Where(IsLikelyPoint)
            .Select(obj => new ComputeExaminationReportPoint(
                ReadFlexibleString(obj, "point", "label", "point_label", "pointLabel", "name") ?? string.Empty,
                ReadFlexibleString(obj, "easting", "east", "x") ?? string.Empty,
                ReadFlexibleString(obj, "northing", "north", "y") ?? string.Empty,
                ReadFlexibleString(obj, "seq", "sequence") ?? string.Empty))
            .Where(point => !string.IsNullOrWhiteSpace(point.Point))
            .GroupBy(point => point.Point, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(point => ParseSequence(point.Sequence), Comparer<int?>.Create((left, right) =>
            {
                if (left.HasValue && right.HasValue)
                {
                    return left.Value.CompareTo(right.Value);
                }

                if (left.HasValue)
                {
                    return -1;
                }

                if (right.HasValue)
                {
                    return 1;
                }

                return 0;
            }))
            .ThenBy(point => point.Point, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ComputeExaminationReportFieldValue BuildMetadataField(
        ExtractionReviewDocument? reviewedData,
        string label,
        string key,
        string defaultValue)
    {
        var field = reviewedData?.SurveyMetadataFields.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Label, label, StringComparison.OrdinalIgnoreCase));

        if (field is null)
        {
            return new ComputeExaminationReportFieldValue(label, defaultValue);
        }

        var value = FirstNonBlank(field.Value, field.RawText);
        if (string.IsNullOrWhiteSpace(value) && field.Present.HasValue)
        {
            value = field.Present.Value ? "Present" : "Not present";
        }

        return new ComputeExaminationReportFieldValue(
            label,
            value ?? defaultValue,
            string.IsNullOrWhiteSpace(field.SemanticState) ? null : field.SemanticState,
            string.IsNullOrWhiteSpace(field.Unit) ? null : field.Unit,
            string.IsNullOrWhiteSpace(field.Title) ? null : field.Title,
            string.IsNullOrWhiteSpace(field.Organization) ? null : field.Organization);
    }

    private static IEnumerable<JsonElement> ExistingRoots(params JsonDocument?[] documents)
    {
        foreach (var document in documents)
        {
            if (document is not null)
            {
                yield return document.RootElement;
            }
        }
    }

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
            foreach (var property in element.EnumerateObject())
            {
                foreach (var child in EnumerateObjects(property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var child in EnumerateObjects(item))
                {
                    yield return child;
                }
            }
        }
    }

    private static bool IsLikelyBoundarySegment(JsonElement element)
    {
        return !string.IsNullOrWhiteSpace(ReadFlexibleString(element, "bearing", "bearing_text", "bearingText"))
            && !string.IsNullOrWhiteSpace(ReadFlexibleString(element, "distance", "distance_m", "distanceText", "distance_text"))
            && (!string.IsNullOrWhiteSpace(ReadFlexibleString(element, "from", "from_point", "fromPoint", "from_label"))
                || !string.IsNullOrWhiteSpace(ReadFlexibleString(element, "to", "to_point", "toPoint", "to_label")));
    }

    private static bool IsLikelyPoint(JsonElement element)
    {
        return !string.IsNullOrWhiteSpace(ReadFlexibleString(element, "point", "label", "point_label", "pointLabel", "name"))
            && !string.IsNullOrWhiteSpace(ReadFlexibleString(element, "easting", "east", "x"))
            && !string.IsNullOrWhiteSpace(ReadFlexibleString(element, "northing", "north", "y"));
    }

    private static string? FindFirstString(JsonDocument? first, JsonDocument? second, params string[] propertyNames)
    {
        foreach (var root in ExistingRoots(first, second))
        {
            foreach (var propertyName in propertyNames)
            {
                var value = FindStringsByProperty(root, propertyName).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> FindStringsByProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var value in EnumerateStringValues(property.Value))
                    {
                        yield return value;
                    }
                }

                foreach (var nested in FindStringsByProperty(property.Value, propertyName))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var nested in FindStringsByProperty(item, propertyName))
                {
                    yield return nested;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateStringValues(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var value in EnumerateStringValues(item))
                {
                    yield return value;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            var name = ReadFlexibleString(element, "name", "label", "value", "owner", "neighbor", "occupant", "possessor");
            if (!string.IsNullOrWhiteSpace(name))
            {
                yield return name;
            }
        }
    }

    private static string? ReadFlexibleString(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            var found = false;
            var property = default(JsonElement);
            foreach (var candidate in element.EnumerateObject())
            {
                if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    property = candidate.Value;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                continue;
            }

            var value = property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool? ReadFlexibleBool(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            var found = false;
            var property = default(JsonElement);
            foreach (var candidate in element.EnumerateObject())
            {
                if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    property = candidate.Value;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            {
                return property.GetBoolean();
            }

            if (property.ValueKind == JsonValueKind.String && bool.TryParse(property.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static int? ParseSequence(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static void AddParticipant(
        List<ComputeExaminationReportParticipant> participants,
        string group,
        string? name,
        string? role,
        string source,
        string status)
    {
        if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, "Not provided", StringComparison.OrdinalIgnoreCase))
        {
            participants.Add(new ComputeExaminationReportParticipant(group, name.Trim(), NormalizeParticipantRole(role), source, status));
        }
    }

    private static string BuildMemorandumPartyRole(ExtractionReviewMemorandumParty party)
    {
        var role = party.Role switch
        {
            "surveyed_for" => "Surveyed For",
            "interested_party" => "Interested Party",
            "notice_served_on" => "Notice Served On",
            "appeared" => "Appeared",
            _ => party.Role
        };

        if (!string.Equals(party.Role, "appeared", StringComparison.OrdinalIgnoreCase))
        {
            return role;
        }

        var mode = string.IsNullOrWhiteSpace(party.AppearanceMode) ? "unknown" : party.AppearanceMode;
        return string.IsNullOrWhiteSpace(party.Representative)
            ? $"{role} ({mode})"
            : $"{role} ({mode}: {party.Representative})";
    }

    private static string NormalizeParticipantRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return "Participant";
        }

        var trimmed = role.Trim();
        return string.Equals(trimmed, "unknown", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "unclear", StringComparison.OrdinalIgnoreCase)
            ? "Participant"
            : trimmed;
    }

    private static string BuildSourceText(string? sourcePage, string? sourceZone)
    {
        var page = NonEmpty(sourcePage);
        var zone = NonEmpty(sourceZone);
        if (string.IsNullOrWhiteSpace(page))
        {
            return zone;
        }

        return string.IsNullOrWhiteSpace(zone) ? page : $"{page} - {zone}";
    }

    private static string BuildStatusText(string? reviewStatus, string? reviewNotes)
    {
        return FirstNonBlank(reviewStatus, reviewNotes) ?? string.Empty;
    }

    private static string NonEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string? FirstNonBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string SafeLoadInnolaServerUrl()
    {
        try
        {
            return InnolaTransactionSettings.Load().ServerUrl;
        }
        catch (Exception)
        {
            return "Not provided";
        }
    }

    private static ComputeExaminationReportStage BuildPreflightStage(string path, string stageId, string stageName)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"{stageName} findings are missing.");
        }

        var summary = PreflightSummarySerializer.Read(path);
        var findings = summary.Payload.Blockers
            .Concat(summary.Payload.Warnings)
            .Concat(summary.Payload.PassedChecks)
            .Select(check => new ComputeExaminationReportFinding(
                stageId,
                check.CheckId,
                check.DisplayName ?? check.CheckId,
                check.Outcome ?? check.Status,
                check.Severity,
                check.WorkflowEffect,
                check.Message,
                check.Correction,
                check.AffectedPath,
                check.SourceRole,
                check.Evidence))
            .ToArray();

        return new ComputeExaminationReportStage(
            stageId,
            stageName,
            summary.Payload.Status,
            summary.CreatedBy,
            summary.CreatedAt,
            summary.RunId,
            findings,
            Array.Empty<string>());
    }

    private static ComputeExaminationReportStage BuildArtifactStage(string stageId, string stageName, CaseFolderLayout layout, string artifactPath)
    {
        if (!File.Exists(artifactPath))
        {
            throw new InvalidOperationException($"{stageName} evidence is missing: {Path.GetRelativePath(layout.RootDirectory, artifactPath)}.");
        }

        var info = new FileInfo(artifactPath);
        return new ComputeExaminationReportStage(
            stageId,
            stageName,
            "available",
            null,
            info.LastWriteTimeUtc.ToString("O"),
            null,
            Array.Empty<ComputeExaminationReportFinding>(),
            new[] { MakeReference(layout, artifactPath) });
    }

    private static ComputeExaminationReportStage BuildJsonStage(string stageId, string stageName, JsonDocument document)
    {
        var root = document.RootElement;
        var status = ReadString(root, "status")
            ?? ReadString(root, "decision")
            ?? ReadString(root, "enterprise_disposition_status")
            ?? ReadString(root, "spatial_unit_api_status")
            ?? ReadString(root, "working_package_upload_status")
            ?? "available";

        return new ComputeExaminationReportStage(
            stageId,
            stageName,
            status,
            ReadString(root, "created_by") ?? ReadString(root, "operator_id") ?? ReadString(root, "published_by"),
            ReadString(root, "created_at") ?? ReadString(root, "decided_at_utc") ?? ReadString(root, "published_at"),
            ReadString(root, "run_id") ?? ReadString(root, "publish_run_id"),
            Array.Empty<ComputeExaminationReportFinding>(),
            Array.Empty<string>());
    }

    private static string MakeReference(CaseFolderLayout layout, string path)
    {
        return Path.GetRelativePath(layout.RootDirectory, path).Replace('\\', '/');
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
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

        public static void Write(string path, ComputeExaminationReportDocument report)
        {
            var pages = new PdfReportRenderer(report).Render();
            var objects = new List<string>();
            var pageObjectNumbers = new List<int>();

            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
            objects.Add(string.Empty);
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");

            foreach (var pageLines in pages)
            {
                var contentObjectNumber = objects.Count + 2;
                var pageObjectNumber = objects.Count + 1;
                pageObjectNumbers.Add(pageObjectNumber);
                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObjectNumber} 0 R >>");
                objects.Add(BuildContentObject(pageLines.Content));
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

        private static bool IsReportableFinding(ComputeExaminationReportFinding finding)
        {
            return string.Equals(finding.Severity, "blocker", StringComparison.OrdinalIgnoreCase)
                || string.Equals(finding.Severity, "warning", StringComparison.OrdinalIgnoreCase)
                || string.Equals(finding.Outcome, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(finding.Outcome, "warning", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeStatus(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }

        private static string PdfNumber(double value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        private sealed record PdfPageContent(string Content);

        private sealed record PdfColumn(string Header, double Width);

        private sealed class PdfReportRenderer
        {
            private readonly ComputeExaminationReportDocument _report;
            private readonly List<PdfPageContent> _pages = new();
            private StringBuilder _stream = new();
            private double _y;

            public PdfReportRenderer(ComputeExaminationReportDocument report)
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
                    ("Decision", _report.Closeout.Decision),
                    ("Transaction", _report.TransactionNumber),
                    ("Generated At UTC", _report.GeneratedAtUtc),
                    ("Operator", _report.GeneratedBy ?? string.Empty),
                    ("Source System", FindField(_report.TransactionInfo, "Source System"))
                });

                DrawSection("Transaction Info");
                DrawKeyValueTable(_report.TransactionInfo.Fields.Select(field => (field.Field, field.Value)));

                DrawSection("General Info");
                DrawKeyValueTable(_report.GeneralInfo.Fields.Select(field => (field.Field, field.Value)));

                DrawSection("Volume / Folio");
                DrawTable(
                    new[]
                    {
                        new PdfColumn("Volume", 54),
                        new PdfColumn("Folio", 54),
                        new PdfColumn("Raw Text", 150),
                        new PdfColumn("Source", 150),
                        new PdfColumn("Status", 120)
                    },
                    _report.VolumeFolios.Count == 0
                        ? new[] { new[] { "Not found", string.Empty, string.Empty, string.Empty, string.Empty } }
                        : _report.VolumeFolios.Select(item => new[] { item.Volume, item.Folio, item.RawText, item.Source, item.Status }));

                DrawSection("Owners / Neighbors / Participants");
                DrawTable(
                    new[]
                    {
                        new PdfColumn("Group", 90),
                        new PdfColumn("Name", 130),
                        new PdfColumn("Role", 82),
                        new PdfColumn("Source", 126),
                        new PdfColumn("Status", 100)
                    },
                    _report.Participants.Count == 0
                        ? new[] { new[] { "No participant evidence recorded.", string.Empty, string.Empty, string.Empty, string.Empty } }
                        : _report.Participants.Select(item => new[] { item.Group, item.Name, item.Role, item.Source, item.Status }));

                DrawSection("Adjacent Owners / Neighbors");
                DrawTable(
                    new[]
                    {
                        new PdfColumn("Name", 140),
                        new PdfColumn("Role", 76),
                        new PdfColumn("From", 46),
                        new PdfColumn("To", 46),
                        new PdfColumn("Vol.", 50),
                        new PdfColumn("Folio", 58),
                        new PdfColumn("Status", 112)
                    },
                    _report.AdjacentOwners.Count == 0
                        ? new[] { new[] { "No adjacent owner or neighbor evidence recorded.", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty } }
                        : _report.AdjacentOwners.Select(item => new[] { item.Name, item.Role, item.From, item.To, item.Volume, item.Folio, item.Status }));

                DrawSection("Memorandum Findings");
                DrawTable(
                    new[]
                    {
                        new PdfColumn("Group", 100),
                        new PdfColumn("Rule", 132),
                        new PdfColumn("Status", 82),
                        new PdfColumn("Effect", 92),
                        new PdfColumn("Source", 122)
                    },
                    _report.MemorandumFindings.Count == 0
                        ? new[] { new[] { "No memorandum findings recorded.", string.Empty, string.Empty, string.Empty, string.Empty } }
                        : _report.MemorandumFindings.Select(item => new[] { item.Group, item.Rule, item.ReviewerStatus, item.WorkflowEffect, item.Source }));

                DrawSection("Boundary Segments");
                DrawTable(
                    new[]
                    {
                        new PdfColumn("Seq", 38),
                        new PdfColumn("From", 45),
                        new PdfColumn("To", 45),
                        new PdfColumn("Bearing", 82),
                        new PdfColumn("Distance", 68),
                        new PdfColumn("Notes", 250)
                    },
                    _report.BoundarySegments.Count == 0
                        ? new[] { new[] { "No used boundary segments recorded.", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty } }
                        : _report.BoundarySegments.Select(item => new[] { item.Sequence, item.From, item.To, item.Bearing, item.Distance, item.Notes }));

                DrawSection("Survey Points");
                DrawTable(
                    new[]
                    {
                        new PdfColumn("Point", 80),
                        new PdfColumn("Easting", 130),
                        new PdfColumn("Northing", 130),
                        new PdfColumn("Sequence", 188)
                    },
                    _report.Points.Count == 0
                        ? new[] { new[] { "No reviewed points recorded.", string.Empty, string.Empty, string.Empty } }
                        : _report.Points.Select(item => new[] { item.Point, item.Easting, item.Northing, item.Sequence }));

                DrawSection("Workflow Stage Summary");
                DrawTable(
                    new[]
                    {
                        new PdfColumn("Stage", 150),
                        new PdfColumn("Status", 78),
                        new PdfColumn("Findings", 110),
                        new PdfColumn("Notes", 190)
                    },
                    _report.Stages.Select(stage => new[]
                    {
                        stage.StageName,
                        stage.Status,
                        BuildFindingSummary(stage),
                        BuildFindingNotes(stage)
                    }));

                DrawSection("Closeout");
                DrawKeyValueTable(new[]
                {
                    ("Decision", _report.Closeout.Decision),
                    ("Operator", _report.Closeout.OperatorId ?? string.Empty),
                    ("Enterprise Disposition", _report.Closeout.EnterpriseDispositionStatus),
                    ("Spatial Unit Status", _report.Closeout.SpatialUnitApiStatus ?? string.Empty),
                    ("Spatial Unit Id", _report.Closeout.SpatialUnitId ?? string.Empty),
                    ("Working Package", _report.Closeout.WorkingPackageFileName ?? string.Empty),
                    ("Working Package Upload", _report.Closeout.WorkingPackageUploadStatus ?? string.Empty)
                });

                DrawSection("Artifact References");
                DrawTable(
                    new[] { new PdfColumn("Artifact", UsableWidth) },
                    _report.ArtifactReferences.Count == 0
                        ? new[] { new[] { "No artifact references recorded." } }
                        : _report.ArtifactReferences.Select(reference => new[] { reference }));

                FinishPage();
                return _pages;
            }

            private void BeginPage(bool includeRunningHeader)
            {
                _stream = new StringBuilder();
                _y = TopY;
                if (includeRunningHeader)
                {
                    DrawText("Compute Examination Report", MarginX, _y, 8, bold: true, PrimaryR, PrimaryG, PrimaryB);
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
                DrawText("Compute Examination Report", MarginX, _y, 22, bold: true, PrimaryR, PrimaryG, PrimaryB);
                _y -= 22;
                DrawText($"NLA Transaction {_report.TransactionNumber} - {FindField(_report.TransactionInfo, "Transaction Type")}", MarginX, _y, 10, bold: true, MutedR, MutedG, MutedB);
                _y -= 14;
                DrawText($"Generated {_report.GeneratedAtUtc} by {_report.GeneratedBy ?? "Not provided"}", MarginX, _y, 8, bold: false, MutedR, MutedG, MutedB);
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
                    ("Task", FindField(_report.TransactionInfo, "Task")),
                    ("Current Status", FindField(_report.TransactionInfo, "Status")),
                    ("Decision", _report.Closeout.Decision)
                };
                var boxWidth = UsableWidth / values.Length;
                for (var i = 0; i < values.Length; i++)
                {
                    var x = MarginX + (i * boxWidth);
                    DrawRect(x, _y - 42, boxWidth - 4, 38, fill: true, r: 0.965, g: 0.980, b: 0.984);
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
                _stream
                    .Append(PdfNumber(BorderR)).Append(' ')
                    .Append(PdfNumber(BorderG)).Append(' ')
                    .Append(PdfNumber(BorderB)).Append(" RG ")
                    .Append(PdfNumber(MarginX)).Append(' ')
                    .Append(PdfNumber(_y)).Append(" m ")
                    .Append(PdfNumber(PageWidth - MarginX)).Append(' ')
                    .Append(PdfNumber(_y)).AppendLine(" l S");
            }

            private void DrawFooter()
            {
                var pageNumber = _pages.Count + 1;
                DrawRuleAt(BottomY - 12);
                DrawText($"Compute Examination Report - Transaction {_report.TransactionNumber}", MarginX, BottomY - 28, 7, bold: false, MutedR, MutedG, MutedB);
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

            private static string FindField(ComputeExaminationReportGeneralInfo info, string fieldName)
            {
                return info.Fields
                    .FirstOrDefault(field => string.Equals(field.Field, fieldName, StringComparison.OrdinalIgnoreCase))
                    ?.Value ?? "Not provided";
            }

            private static string BuildFindingSummary(ComputeExaminationReportStage stage)
            {
                if (stage.Findings.Count == 0)
                {
                    return "None";
                }

                return string.Join(", ", stage.Findings
                    .GroupBy(finding => NormalizeStatus(finding.Outcome))
                    .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(group => $"{group.Key}={group.Count()}"));
            }

            private static string BuildFindingNotes(ComputeExaminationReportStage stage)
            {
                return string.Join("; ", stage.Findings
                    .Where(IsReportableFinding)
                    .Take(3)
                    .Select(finding => $"{finding.Outcome}: {finding.DisplayName}"));
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

public sealed record ComputeExaminationReportDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("task_id")] string? TaskId,
    [property: JsonPropertyName("generated_at_utc")] string GeneratedAtUtc,
    [property: JsonPropertyName("generated_by")] string? GeneratedBy,
    [property: JsonPropertyName("manifest_run_id")] string ManifestRunId,
    [property: JsonPropertyName("stages")] IReadOnlyList<ComputeExaminationReportStage> Stages,
    [property: JsonPropertyName("closeout")] ComputeExaminationReportCloseout Closeout,
    [property: JsonPropertyName("artifact_references")] IReadOnlyList<string> ArtifactReferences,
    [property: JsonPropertyName("transaction_info")] ComputeExaminationReportGeneralInfo TransactionInfo,
    [property: JsonPropertyName("general_info")] ComputeExaminationReportGeneralInfo GeneralInfo,
    [property: JsonPropertyName("volume_folios")] IReadOnlyList<ComputeExaminationReportVolumeFolio> VolumeFolios,
    [property: JsonPropertyName("participants")] IReadOnlyList<ComputeExaminationReportParticipant> Participants,
    [property: JsonPropertyName("adjacent_owners")] IReadOnlyList<ComputeExaminationReportAdjacentOwner> AdjacentOwners,
    [property: JsonPropertyName("memorandum_findings")] IReadOnlyList<ComputeExaminationReportMemorandumFinding> MemorandumFindings,
    [property: JsonPropertyName("boundary_segments")] IReadOnlyList<ComputeExaminationReportBoundarySegment> BoundarySegments,
    [property: JsonPropertyName("points")] IReadOnlyList<ComputeExaminationReportPoint> Points);

public sealed record ComputeExaminationReportGeneralInfo(
    [property: JsonPropertyName("fields")] IReadOnlyList<ComputeExaminationReportFieldValue> Fields);

public sealed record ComputeExaminationReportFieldValue(
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("semantic_state")] string? SemanticState = null,
    [property: JsonPropertyName("unit")] string? Unit = null,
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("organization")] string? Organization = null);

public sealed record ComputeExaminationReportVolumeFolio(
    [property: JsonPropertyName("volume")] string Volume,
    [property: JsonPropertyName("folio")] string Folio,
    [property: JsonPropertyName("raw_text")] string RawText,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("status")] string Status);

public sealed record ComputeExaminationReportParticipant(
    [property: JsonPropertyName("group")] string Group,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("status")] string Status);

public sealed record ComputeExaminationReportAdjacentOwner(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("volume")] string Volume,
    [property: JsonPropertyName("folio")] string Folio,
    [property: JsonPropertyName("status")] string Status);

public sealed record ComputeExaminationReportMemorandumFinding(
    [property: JsonPropertyName("group")] string Group,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("reviewer_status")] string ReviewerStatus,
    [property: JsonPropertyName("workflow_effect")] string WorkflowEffect,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("evidence_state")] string? EvidenceState = null);

public sealed record ComputeExaminationReportBoundarySegment(
    [property: JsonPropertyName("seq")] string Sequence,
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("bearing")] string Bearing,
    [property: JsonPropertyName("distance")] string Distance,
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("use_for_points")] bool UseForPoints);

public sealed record ComputeExaminationReportPoint(
    [property: JsonPropertyName("point")] string Point,
    [property: JsonPropertyName("easting")] string Easting,
    [property: JsonPropertyName("northing")] string Northing,
    [property: JsonPropertyName("sequence")] string Sequence);

public sealed record ComputeExaminationReportStage(
    [property: JsonPropertyName("stage_id")] string StageId,
    [property: JsonPropertyName("stage_name")] string StageName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("operator_id")] string? OperatorId,
    [property: JsonPropertyName("timestamp_utc")] string? TimestampUtc,
    [property: JsonPropertyName("run_id")] string? RunId,
    [property: JsonPropertyName("findings")] IReadOnlyList<ComputeExaminationReportFinding> Findings,
    [property: JsonPropertyName("artifact_references")] IReadOnlyList<string> ArtifactReferences);

public sealed record ComputeExaminationReportFinding(
    [property: JsonPropertyName("stage_id")] string StageId,
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("workflow_effect")] string? WorkflowEffect,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("correction")] string? Correction,
    [property: JsonPropertyName("affected_path")] string? AffectedPath,
    [property: JsonPropertyName("source_role")] string? SourceRole,
    [property: JsonPropertyName("evidence")] IReadOnlyDictionary<string, IReadOnlyList<string>>? Evidence);

public sealed record ComputeExaminationReportCloseout(
    [property: JsonPropertyName("decision")] string Decision,
    [property: JsonPropertyName("operator_id")] string? OperatorId,
    [property: JsonPropertyName("decided_at_utc")] string DecidedAtUtc,
    [property: JsonPropertyName("enterprise_disposition_status")] string EnterpriseDispositionStatus,
    [property: JsonPropertyName("enterprise_disposition_ref")] string? EnterpriseDispositionRef,
    [property: JsonPropertyName("spatial_unit_api_status")] string? SpatialUnitApiStatus,
    [property: JsonPropertyName("spatial_unit_id")] string? SpatialUnitId,
    [property: JsonPropertyName("working_package_file_name")] string? WorkingPackageFileName,
    [property: JsonPropertyName("working_package_source_type")] string? WorkingPackageSourceType,
    [property: JsonPropertyName("working_package_upload_status")] string? WorkingPackageUploadStatus);
