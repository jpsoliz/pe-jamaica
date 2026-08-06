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
            BuildMetadataField(reviewedData, "North arrow", "north_arrow", "Not provided"),
            BuildMetadataField(reviewedData, "Parish", "parish", "Not provided"),
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

        return new ComputeExaminationReportFieldValue(label, value ?? defaultValue);
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
        private const int MaxLineLength = 96;
        private const int MaxLinesPerPage = 45;

        public static void Write(string path, ComputeExaminationReportDocument report)
        {
            var pages = BuildPages(report);
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
                objects.Add(BuildContentObject(pageLines));
            }

            objects[1] = $"<< /Type /Pages /Count {pageObjectNumbers.Count} /Kids [{string.Join(" ", pageObjectNumbers.Select(number => $"{number} 0 R"))}] >>";

            File.WriteAllBytes(path, BuildPdfBytes(objects));
        }

        private static IReadOnlyList<IReadOnlyList<PdfLine>> BuildPages(ComputeExaminationReportDocument report)
        {
            var lines = new List<PdfLine>
            {
                new("Compute Examination Report", true, 14),
                new($"Transaction Number: {report.TransactionNumber}", true),
                new($"Generated At UTC: {report.GeneratedAtUtc}"),
                new($"Generated By: {report.GeneratedBy ?? string.Empty}"),
                PdfLine.Blank,
                new("Transaction Info", true, 12),
                new("Field | Value", true)
            };

            lines.AddRange(report.TransactionInfo.Fields.Select(field => new PdfLine($"{field.Field} | {field.Value}")));
            lines.Add(PdfLine.Blank);
            lines.Add(new PdfLine("General Info", true, 12));
            lines.Add(new PdfLine("Field | Value", true));
            lines.AddRange(report.GeneralInfo.Fields.Select(field => new PdfLine($"{field.Field} | {field.Value}")));
            lines.Add(PdfLine.Blank);
            lines.Add(new PdfLine("Volume / Folio", true, 12));
            lines.Add(new PdfLine("Volume | Folio | Raw Text | Source | Status", true));
            lines.AddRange(report.VolumeFolios.Count == 0
                ? new[] { new PdfLine("Not found") }
                : report.VolumeFolios.Select(item => new PdfLine($"{item.Volume} | {item.Folio} | {item.RawText} | {item.Source} | {item.Status}")));
            lines.Add(PdfLine.Blank);
            lines.Add(new PdfLine("Owners / Neighbors / Participants", true, 12));
            lines.Add(new PdfLine("Group | Name | Role | Source | Status", true));
            lines.AddRange(report.Participants.Count == 0
                ? new[] { new PdfLine("No owner, neighbor, possessor, representative, or participant evidence recorded.") }
                : report.Participants.Select(participant => new PdfLine($"{participant.Group} | {participant.Name} | {participant.Role} | {participant.Source} | {participant.Status}")));
            lines.Add(PdfLine.Blank);
            lines.Add(new PdfLine("Adjacent Owners / Neighbors", true, 12));
            lines.Add(new PdfLine("Name | Role | From | To | Vol. | Folio | Status", true));
            lines.AddRange(report.AdjacentOwners.Count == 0
                ? new[] { new PdfLine("No adjacent owner or neighbor evidence recorded.") }
                : report.AdjacentOwners.Select(owner => new PdfLine($"{owner.Name} | {owner.Role} | {owner.From} | {owner.To} | {owner.Volume} | {owner.Folio} | {owner.Status}")));
            lines.Add(PdfLine.Blank);
            lines.Add(new PdfLine("Boundary Segments", true, 12));
            lines.Add(new PdfLine("Seq | From | To | Bearing | Distance | Notes", true));
            lines.AddRange(report.BoundarySegments.Count == 0
                ? new[] { new PdfLine("No used boundary segments recorded.") }
                : report.BoundarySegments.Select(segment => new PdfLine($"{segment.Sequence} | {segment.From} | {segment.To} | {segment.Bearing} | {segment.Distance} | {segment.Notes}")));
            lines.Add(PdfLine.Blank);
            lines.Add(new PdfLine("Points", true, 12));
            lines.Add(new PdfLine("Point | Easting | Northing | Sequence", true));
            lines.AddRange(report.Points.Count == 0
                ? new[] { new PdfLine("No reviewed points recorded.") }
                : report.Points.Select(point => new PdfLine($"{point.Point} | {point.Easting} | {point.Northing} | {point.Sequence}")));
            lines.Add(PdfLine.Blank);
            lines.Add(new PdfLine("Stage Summary", true, 12));

            foreach (var stage in report.Stages)
            {
                lines.Add(new PdfLine($"- {stage.StageName}: {stage.Status}"));
                if (stage.Findings.Count > 0)
                {
                    var grouped = stage.Findings
                        .GroupBy(finding => NormalizeStatus(finding.Outcome))
                        .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(group => $"{group.Key}={group.Count()}");
                    lines.Add(new PdfLine($"  Findings: {string.Join(", ", grouped)}"));
                }

                foreach (var blocker in stage.Findings.Where(finding => IsReportableFinding(finding)).Take(4))
                {
                    lines.Add(new PdfLine($"  {blocker.Outcome}: {blocker.DisplayName}"));
                }
            }

            lines.Add(PdfLine.Blank);
            lines.Add(new PdfLine("Closeout", true, 12));
            lines.Add(new PdfLine($"Decision: {report.Closeout.Decision}"));
            lines.Add(new PdfLine($"Operator: {report.Closeout.OperatorId ?? string.Empty}"));
            lines.Add(new PdfLine($"Enterprise Disposition: {report.Closeout.EnterpriseDispositionStatus}"));
            lines.Add(new PdfLine($"Spatial Unit Status: {report.Closeout.SpatialUnitApiStatus ?? string.Empty}"));
            lines.Add(new PdfLine($"Spatial Unit Id: {report.Closeout.SpatialUnitId ?? string.Empty}"));
            lines.Add(new PdfLine($"Working Package: {report.Closeout.WorkingPackageFileName ?? string.Empty}"));
            lines.Add(new PdfLine($"Working Package Upload: {report.Closeout.WorkingPackageUploadStatus ?? string.Empty}"));
            lines.Add(PdfLine.Blank);
            lines.Add(new PdfLine("Artifact References", true, 12));
            lines.AddRange(report.ArtifactReferences.Select(reference => new PdfLine($"- {reference}")));

            var wrapped = lines.SelectMany(WrapLine).ToArray();
            return wrapped
                .Select((line, index) => new { line, index })
                .GroupBy(item => item.index / MaxLinesPerPage)
                .Select(group => (IReadOnlyList<PdfLine>)group.Select(item => item.line).ToArray())
                .ToArray();
        }

        private static string BuildContentObject(IReadOnlyList<PdfLine> lines)
        {
            var stream = new StringBuilder();
            stream.AppendLine("BT");
            stream.AppendLine("50 750 Td");
            foreach (var line in lines)
            {
                stream.Append('/').Append(line.Bold ? "F2" : "F1").Append(' ').Append(line.FontSize).AppendLine(" Tf");
                stream.Append('(').Append(EscapePdfText(line.Text)).AppendLine(") Tj");
                stream.AppendLine("0 -15 Td");
            }

            stream.AppendLine("ET");
            var text = stream.ToString();
            return $"<< /Length {Encoding.ASCII.GetByteCount(text)} >>\nstream\n{text}endstream";
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

        private static IEnumerable<PdfLine> WrapLine(PdfLine line)
        {
            if (line.Text.Length <= MaxLineLength)
            {
                yield return line;
                yield break;
            }

            var remaining = line.Text;
            while (remaining.Length > MaxLineLength)
            {
                var splitAt = remaining.LastIndexOf(' ', MaxLineLength);
                if (splitAt <= 0)
                {
                    splitAt = MaxLineLength;
                }

                yield return new PdfLine(remaining[..splitAt], line.Bold, line.FontSize);
                remaining = remaining[splitAt..].TrimStart();
            }

            if (remaining.Length > 0)
            {
                yield return new PdfLine(remaining, line.Bold, line.FontSize);
            }
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

        private sealed record PdfLine(string Text, bool Bold = false, int FontSize = 10)
        {
            public static PdfLine Blank { get; } = new(string.Empty);
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
    [property: JsonPropertyName("boundary_segments")] IReadOnlyList<ComputeExaminationReportBoundarySegment> BoundarySegments,
    [property: JsonPropertyName("points")] IReadOnlyList<ComputeExaminationReportPoint> Points);

public sealed record ComputeExaminationReportGeneralInfo(
    [property: JsonPropertyName("fields")] IReadOnlyList<ComputeExaminationReportFieldValue> Fields);

public sealed record ComputeExaminationReportFieldValue(
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("value")] string Value);

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
