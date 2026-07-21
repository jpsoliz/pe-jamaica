namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed class ParcelScopedManualPointService
{
    public ExtractionReviewRow CreateManualRow(
        ExtractionReviewDocument document,
        string parcelGroupId,
        string? parcelName,
        string? traverseId,
        int? insertAfterSequence = null,
        string? insertAfterPointIdentifier = null)
    {
        var normalizedGroupId = NormalizeRequired(parcelGroupId, "parcel");
        var normalizedParcelName = NormalizeOptional(parcelName) ?? normalizedGroupId;
        var normalizedTraverseId = NormalizeOptional(traverseId) ?? normalizedGroupId;
        NormalizeSequences(document.Rows, normalizedGroupId);
        var existingParcelRows = document.Rows
            .Where(row => string.Equals(row.ParcelGroupId, normalizedGroupId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var nextSequence = ResolveNextSequence(existingParcelRows, insertAfterSequence);
        if (insertAfterSequence is > 0)
        {
            foreach (var row in existingParcelRows.Where(row => row.SequenceInGroup >= nextSequence))
            {
                row.SequenceInGroup++;
            }
        }

        return new ExtractionReviewRow
        {
            RowId = BuildUniqueManualRowId(document, normalizedGroupId, nextSequence),
            ParcelGroupId = normalizedGroupId,
            ParcelName = normalizedParcelName,
            TraverseId = normalizedTraverseId,
            SequenceInGroup = nextSequence,
            GroupConfidence = "manual_current_parcel",
            PointIdentifier = BuildPointIdentifier(normalizedGroupId, nextSequence, insertAfterSequence, insertAfterPointIdentifier, existingParcelRows),
            Easting = string.Empty,
            Northing = string.Empty,
            Length = string.Empty,
            ExtractionStatus = "Manual entry",
            SourceEvidence = "Manual correction",
            RowProvenance = "manual",
            IsManual = true,
            IsEdited = true,
            OriginalValues = new ExtractionReviewOriginalValues()
        };
    }

    public int NormalizeSequences(IList<ExtractionReviewRow> rows, string parcelGroupId)
    {
        var normalizedGroupId = NormalizeRequired(parcelGroupId, "parcel");
        var sequence = 1;
        var changedCount = 0;
        foreach (var row in rows.Where(row => string.Equals(row.ParcelGroupId, normalizedGroupId, StringComparison.OrdinalIgnoreCase)))
        {
            if (row.SequenceInGroup != sequence)
            {
                row.SequenceInGroup = sequence;
                changedCount++;
            }

            sequence++;
        }

        return changedCount;
    }

    private static int ResolveNextSequence(IReadOnlyList<ExtractionReviewRow> existingRows, int? insertAfterSequence)
    {
        if (insertAfterSequence is > 0)
        {
            return insertAfterSequence.Value + 1;
        }

        return existingRows
            .Select(row => row.SequenceInGroup ?? 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
    }

    private static string BuildUniqueManualRowId(ExtractionReviewDocument document, string parcelGroupId, int sequence)
    {
        var baseId = $"manual-{NormalizeToken(parcelGroupId)}-{sequence:000}";
        var candidate = baseId;
        var suffix = 2;
        while (document.Rows.Any(row => string.Equals(row.RowId, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseId}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string BuildPointIdentifier(
        string parcelGroupId,
        int sequence,
        int? insertAfterSequence,
        string? insertAfterPointIdentifier,
        IReadOnlyCollection<ExtractionReviewRow> existingRows)
    {
        string candidate;
        if (insertAfterSequence is > 0
            && TryIncrementTrailingPointNumber(insertAfterPointIdentifier, out var insertedPointIdentifier))
        {
            candidate = insertedPointIdentifier;
        }
        else
        {
            candidate = $"{parcelGroupId}_P{sequence}";
        }

        return BuildUniquePointIdentifier(candidate, existingRows);
    }

    private static string BuildUniquePointIdentifier(string candidate, IReadOnlyCollection<ExtractionReviewRow> existingRows)
    {
        var uniqueCandidate = candidate;
        while (existingRows.Any(row => string.Equals(row.PointIdentifier, uniqueCandidate, StringComparison.OrdinalIgnoreCase)))
        {
            if (TryIncrementTrailingPointNumber(uniqueCandidate, out var incrementedCandidate))
            {
                uniqueCandidate = incrementedCandidate;
                continue;
            }

            uniqueCandidate = $"{uniqueCandidate}_2";
        }

        return uniqueCandidate;
    }

    private static bool TryIncrementTrailingPointNumber(string? pointIdentifier, out string insertedPointIdentifier)
    {
        insertedPointIdentifier = string.Empty;
        var normalized = NormalizeOptional(pointIdentifier);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var index = normalized.Length - 1;
        while (index >= 0 && char.IsDigit(normalized[index]))
        {
            index--;
        }

        if (index == normalized.Length - 1)
        {
            return false;
        }

        var prefix = normalized[..(index + 1)];
        var numberText = normalized[(index + 1)..];
        if (!int.TryParse(numberText, out var number))
        {
            return false;
        }

        insertedPointIdentifier = $"{prefix}{number + 1}";
        return true;
    }

    private static string NormalizeRequired(string? value, string fallback)
    {
        var normalized = NormalizeOptional(value);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeToken(string value)
    {
        return value
            .Trim()
            .Replace(" ", "-", StringComparison.Ordinal)
            .Replace("_", "-", StringComparison.Ordinal);
    }
}
