namespace ParcelWorkflowAddIn.Compare;

public sealed class CompareEvidenceComparisonService
{
    public IReadOnlyList<CompareEvidenceDiscrepancy> CompareLegal(
        CompareSurveyPlanEvidence plan,
        LegalCadasterQueryResult legalResult)
    {
        var discrepancies = new List<CompareEvidenceDiscrepancy>();
        if (!legalResult.Success)
        {
            discrepancies.Add(Open(
                "Legal cadaster unavailable",
                "Legal cadaster",
                legalResult.Status,
                legalResult.Diagnostic));
            return discrepancies;
        }

        if (legalResult.Status == CompareEvidenceStatus.NoRecordReturned || legalResult.Records.Count == 0)
        {
            discrepancies.Add(Open(
                "No legal cadaster record returned",
                "Legal cadaster",
                CompareEvidenceStatus.NoRecordReturned,
                legalResult.Diagnostic));
            return discrepancies;
        }

        if (legalResult.Records.Count > 1)
        {
            discrepancies.Add(Open(
                "Ambiguous legal cadaster match",
                "Legal cadaster",
                CompareEvidenceStatus.Ambiguous,
                legalResult.Message));
        }

        foreach (var record in legalResult.Records)
        {
            if (!Matches(plan.OwnerName, record.OwnerName))
            {
                discrepancies.Add(Open(
                    "Survey plan owner differs from legal cadaster",
                    "Survey plan vs Legal cadaster",
                    CompareEvidenceStatus.Mismatch,
                    $"Survey plan owner '{plan.OwnerName ?? "(blank)"}' vs legal owner '{record.OwnerName ?? "(blank)"}'."));
            }

            if (!Matches(plan.ParcelId, record.ParcelId))
            {
                discrepancies.Add(Open(
                    "Survey plan parcel ID differs from legal cadaster",
                    "Survey plan vs Legal cadaster",
                    CompareEvidenceStatus.Mismatch,
                    $"Survey plan parcel '{plan.ParcelId ?? "(blank)"}' vs legal parcel '{record.ParcelId ?? "(blank)"}'."));
            }

            if ((!string.IsNullOrWhiteSpace(plan.Volume) || !string.IsNullOrWhiteSpace(plan.Folio))
                && (!Matches(plan.Volume, record.Volume) || !Matches(plan.Folio, record.Folio)))
            {
                discrepancies.Add(Open(
                    "Survey plan volume/folio differs from legal cadaster",
                    "Survey plan vs Legal cadaster",
                    CompareEvidenceStatus.Mismatch,
                    $"Survey plan volume/folio '{plan.Volume ?? "(blank)"}/{plan.Folio ?? "(blank)"}' vs legal '{record.Volume ?? "(blank)"}/{record.Folio ?? "(blank)"}'."));
            }
        }

        return Distinct(discrepancies);
    }

    public IReadOnlyList<CompareEvidenceDiscrepancy> CompareFiscalNeighbors(
        CompareSurveyPlanEvidence plan,
        FiscalCadasterNeighborQueryResult fiscalResult)
    {
        var discrepancies = new List<CompareEvidenceDiscrepancy>();
        if (!fiscalResult.Success)
        {
            discrepancies.Add(Open(
                "Fiscal cadaster unavailable",
                "Fiscal cadaster",
                fiscalResult.Status,
                fiscalResult.Diagnostic));
            return discrepancies;
        }

        if (fiscalResult.Status == CompareEvidenceStatus.NoRecordReturned || fiscalResult.Records.Count == 0)
        {
            discrepancies.Add(Open(
                "No fiscal neighbor record returned",
                "Fiscal cadaster",
                CompareEvidenceStatus.NoRecordReturned,
                fiscalResult.Diagnostic));
            return discrepancies;
        }

        var planLabels = plan.AdjacentOwnerLabels
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (planLabels.Length == 0)
        {
            return discrepancies;
        }

        var fiscalLabels = fiscalResult.Records
            .Select(record => record.OwnerOrTaxpayerDisplay)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => Normalize(label!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var label in planLabels)
        {
            if (!fiscalLabels.Contains(label, StringComparer.OrdinalIgnoreCase))
            {
                discrepancies.Add(Open(
                    "Survey plan adjacent owner not found in fiscal neighbor context",
                    "Survey plan vs Fiscal cadaster",
                    CompareEvidenceStatus.Mismatch,
                    $"Adjacent owner '{label}' was not returned by fiscal neighbor context."));
            }
        }

        return Distinct(discrepancies);
    }

    private static CompareEvidenceDiscrepancy Open(string title, string source, string status, string? diagnostic)
    {
        return new CompareEvidenceDiscrepancy(title, source, status, false, diagnostic);
    }

    private static bool Matches(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
        {
            return true;
        }

        return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? value)
    {
        return string.Join(" ", (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static IReadOnlyList<CompareEvidenceDiscrepancy> Distinct(IReadOnlyList<CompareEvidenceDiscrepancy> discrepancies)
    {
        return discrepancies
            .GroupBy(item => $"{item.Title}|{item.EvidenceSource}|{item.Status}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }
}
