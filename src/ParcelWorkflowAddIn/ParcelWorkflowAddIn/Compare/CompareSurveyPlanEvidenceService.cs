using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Compare;

public sealed class CompareSurveyPlanEvidenceService
{
    private readonly ExtractionReviewPersistenceService persistence;
    private readonly Func<DateTimeOffset> getUtcNow;

    public CompareSurveyPlanEvidenceService(
        ExtractionReviewPersistenceService? persistence = null,
        Func<DateTimeOffset>? getUtcNow = null)
    {
        this.persistence = persistence ?? new ExtractionReviewPersistenceService();
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public CompareSurveyPlanEvidence Load(CaseFolderLayout? layout, string transactionNumber)
    {
        if (layout is null)
        {
            return Empty(transactionNumber, "Compare Case Folder is not loaded.");
        }

        var document = persistence.Load(layout);
        if (document is null)
        {
            return Empty(transactionNumber, "Extraction review evidence is not available.");
        }

        var volumeFolio = document.VolumeFolios.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(item.Volume) || !string.IsNullOrWhiteSpace(item.Folio));
        var adjacentOwners = document.AdjacentOwners
            .Select(owner => owner.Name)
            .Concat(document.Segments.Select(segment => segment.AdjacentOwner))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new CompareSurveyPlanEvidence(
            ResolveMetadata(document, "parcel_id", "parcel", "parcel_number", "lot")
                ?? document.Rows.FirstOrDefault(row => !string.IsNullOrWhiteSpace(row.ParcelName))?.ParcelName,
            volumeFolio?.Volume,
            volumeFolio?.Folio,
            ResolveOwner(document),
            adjacentOwners,
            CompareEvidenceSourceType.SurveyPlan,
            getUtcNow(),
            transactionNumber,
            null);
    }

    private CompareSurveyPlanEvidence Empty(string transactionNumber, string diagnostic)
    {
        return new CompareSurveyPlanEvidence(
            null,
            null,
            null,
            null,
            Array.Empty<string>(),
            CompareEvidenceSourceType.SurveyPlan,
            getUtcNow(),
            transactionNumber,
            diagnostic);
    }

    private static string? ResolveOwner(ExtractionReviewDocument document)
    {
        return document.Parties
            .FirstOrDefault(party => party.Role.Contains("owner", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(party.Name))
            ?.Name
            ?? document.Parties.FirstOrDefault(party => !string.IsNullOrWhiteSpace(party.Name))?.Name
            ?? ResolveMetadata(document, "owner", "registered_owner", "proprietor");
    }

    private static string? ResolveMetadata(ExtractionReviewDocument document, params string[] keys)
    {
        foreach (var key in keys)
        {
            var field = document.SurveyMetadataFields.FirstOrDefault(item =>
                item.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
                || item.Label.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(field?.Value))
            {
                return field.Value.Trim();
            }
        }

        return null;
    }
}
