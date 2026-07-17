using System.Text.RegularExpressions;

namespace ParcelWorkflowAddIn.Compare;

public static class CompareEvidenceSourceType
{
    public const string SurveyPlan = "survey_plan";
    public const string LegalCadaster = "legal_cadaster";
    public const string FiscalCadaster = "fiscal_cadaster";
}

public static class CompareEvidenceStatus
{
    public const string Ready = "ready";
    public const string NoRecordReturned = "no_record_returned";
    public const string Mismatch = "mismatch";
    public const string Ambiguous = "ambiguous";
    public const string ServiceUnavailable = "service_unavailable";
    public const string Unsupported = "unsupported";
}

public static class CompareEvidenceSearchMode
{
    public const string Pid = "PID";
    public const string VolumeFolio = "Volume/Folio";
    public const string LandValuationNumber = "Land Val No.";
    public const string Name = "Name";
}

public static class CompareEvidenceRoleTag
{
    public const string Owner = "Owner";
    public const string Occupant = "Occupant";
    public const string InPossession = "In Possession";
    public const string Neighbor = "Neighbor";
    public const string Other = "Other";
}

public sealed record CompareSurveyPlanEvidence(
    string? ParcelId,
    string? Volume,
    string? Folio,
    string? OwnerName,
    IReadOnlyList<string> AdjacentOwnerLabels,
    string EvidenceSource,
    DateTimeOffset QueriedAt,
    string QueryKey,
    string? Diagnostic);

public sealed record LegalCadasterQuery(
    string QueryKind,
    string? ParcelId,
    string? Volume,
    string? Folio,
    string? LandValuationNumber = null,
    string? Name = null,
    string? Parish = null);

public sealed record LegalCadasterRecord(
    string? OwnerName,
    string? ParcelId,
    string? Volume,
    string? Folio,
    string? TitleRecordId,
    string SourceLabel,
    DateTimeOffset QueriedAt,
    string QueryKey,
    string Status,
    string? Diagnostic,
    string? LandValuationNumber = null,
    string? Parish = null,
    string? PartyRole = null,
    string? PropertyType = null,
    string? Tenure = null,
    DateTimeOffset? RegisteredAt = null);

public sealed record LegalCadasterPartyRecord(
    string? PartyName,
    string? Prid,
    string? FullAddress,
    string? TaxNumber,
    string? PartyStatus,
    string PartyType,
    string SourceLabel,
    DateTimeOffset QueriedAt,
    string QueryKey,
    string? Diagnostic = null)
{
    public string DisplaySummary
    {
        get
        {
            var parts = new[]
            {
                string.IsNullOrWhiteSpace(PartyName) ? null : PartyName,
                string.IsNullOrWhiteSpace(Prid) ? null : $"PRID: {Prid}",
                string.IsNullOrWhiteSpace(FullAddress) ? null : $"Address: {FullAddress}",
                string.IsNullOrWhiteSpace(TaxNumber) ? null : $"Tax No.: {TaxNumber}",
                string.IsNullOrWhiteSpace(PartyStatus) ? null : $"Status: {PartyStatus}",
                string.IsNullOrWhiteSpace(PartyType) ? null : $"Type: {PartyType}"
            }.Where(part => !string.IsNullOrWhiteSpace(part));
            return string.Join("; ", parts);
        }
    }
}

public sealed record CompareEvidenceSearchRequest(
    string QueryKind,
    string? Pid,
    string? Volume,
    string? Folio,
    string? LandValuationNumber,
    string? Name,
    string? Parish)
{
    public string QueryKey => QueryKind switch
    {
        "parcel_id" => $"parcel_id={Pid ?? string.Empty}",
        "volume_folio" => $"volume={Volume ?? string.Empty};folio={Folio ?? string.Empty}",
        "land_valuation_number" => $"land_val_no={LandValuationNumber ?? string.Empty}",
        "name" => $"name={Name ?? string.Empty}",
        "name_parish" => $"name={Name ?? string.Empty};parish={Parish ?? string.Empty}",
        _ => QueryKind
    };
}

public sealed record CompareEvidenceSearchResult(
    string SourceType,
    string SourceLabel,
    string QueryKey,
    string? DisplayName,
    string? PartyRole,
    string? ParcelId,
    string? Volume,
    string? Folio,
    string? LandValuationNumber,
    string? Parish,
    string Status,
    DateTimeOffset QueriedAt,
    string? Diagnostic,
    string? PropertyType = null,
    string? Tenure = null,
    DateTimeOffset? RegisteredAt = null)
{
    public static CompareEvidenceSearchResult FromLegalRecord(LegalCadasterRecord record)
    {
        return new CompareEvidenceSearchResult(
            CompareEvidenceSourceType.LegalCadaster,
            record.SourceLabel,
            record.QueryKey,
            record.OwnerName,
            record.PartyRole,
            record.ParcelId,
            record.Volume,
            record.Folio,
            record.LandValuationNumber,
            record.Parish,
            record.Status,
            record.QueriedAt,
            record.Diagnostic,
            record.PropertyType,
            record.Tenure,
            record.RegisteredAt);
    }

    public string DisplaySummary
    {
        get
        {
            var parts = new[]
            {
                string.IsNullOrWhiteSpace(DisplayName) ? null : DisplayName,
                string.IsNullOrWhiteSpace(PartyRole) ? null : $"Role: {PartyRole}",
                string.IsNullOrWhiteSpace(ParcelId) ? null : $"PID: {ParcelId}",
                string.IsNullOrWhiteSpace(Volume) && string.IsNullOrWhiteSpace(Folio) ? null : $"Vol/Folio: {Volume ?? string.Empty}/{Folio ?? string.Empty}",
                string.IsNullOrWhiteSpace(LandValuationNumber) ? null : $"Land Val No.: {LandValuationNumber}",
                string.IsNullOrWhiteSpace(Parish) ? null : $"Parish: {Parish}",
                string.IsNullOrWhiteSpace(PropertyType) ? null : $"Type: {PropertyType}",
                string.IsNullOrWhiteSpace(Tenure) ? null : $"Tenure: {Tenure}",
                RegisteredAt is null ? null : $"Date Registered: {RegisteredAt:dd/MMM/yyyy}",
                string.IsNullOrWhiteSpace(Status) ? null : $"Status: {Status}"
            }.Where(part => !string.IsNullOrWhiteSpace(part));
            return string.Join("; ", parts);
        }
    }
}

public sealed record CompareValuableEvidence(
    string EvidenceId,
    string SourceType,
    string SourceLabel,
    string QueryKey,
    string DisplaySummary,
    string RoleTag,
    DateTimeOffset CapturedAt,
    string? Diagnostic);

public sealed record FiscalCadasterNeighborRecord(
    string? ParcelId,
    string? SpatialRelationship,
    string? BoundarySide,
    string? OwnerOrTaxpayerDisplay,
    string SourceLabel,
    DateTimeOffset QueriedAt,
    string QueryKey,
    string Status,
    string? Diagnostic);

public sealed record CompareEvidenceDiscrepancy(
    string Title,
    string EvidenceSource,
    string Status,
    bool IsResolved,
    string? Diagnostic);

public sealed record LegalCadasterQueryResult(
    bool Success,
    bool Retryable,
    LegalCadasterQuery Query,
    IReadOnlyList<LegalCadasterRecord> Records,
    string Status,
    string Message,
    string? Diagnostic,
    LegalCadasterQueryRawDebug? RawDebug = null,
    IReadOnlyList<LegalCadasterPartyRecord>? PartyRecords = null)
{
    public static LegalCadasterQueryResult NoRecord(LegalCadasterQuery query, DateTimeOffset queriedAt)
    {
        return new LegalCadasterQueryResult(
            true,
            false,
            query,
            Array.Empty<LegalCadasterRecord>(),
            CompareEvidenceStatus.NoRecordReturned,
            "No record returned",
            $"No legal cadaster record returned for {BuildLegalQueryKey(query)} at {queriedAt:O}.");
    }

    public static LegalCadasterQueryResult Failed(LegalCadasterQuery query, string message, string? diagnostic = null)
    {
        return new LegalCadasterQueryResult(
            false,
            true,
            query,
            Array.Empty<LegalCadasterRecord>(),
            CompareEvidenceStatus.ServiceUnavailable,
            Redact(message),
            Redact(diagnostic ?? message));
    }

    public static string BuildLegalQueryKey(LegalCadasterQuery query)
    {
        if (query.QueryKind.Equals("volume_folio", StringComparison.OrdinalIgnoreCase))
        {
            return $"volume={query.Volume ?? string.Empty};folio={query.Folio ?? string.Empty}";
        }

        if (query.QueryKind.Equals("land_valuation_number", StringComparison.OrdinalIgnoreCase))
        {
            return $"land_val_no={query.LandValuationNumber ?? string.Empty}";
        }

        if (query.QueryKind.Equals("name_parish", StringComparison.OrdinalIgnoreCase))
        {
            return $"name={query.Name ?? string.Empty};parish={query.Parish ?? string.Empty}";
        }

        if (query.QueryKind.Equals("name", StringComparison.OrdinalIgnoreCase))
        {
            return $"name={query.Name ?? string.Empty}";
        }

        return $"parcel_id={query.ParcelId ?? string.Empty}";
    }

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var redacted = Regex.Replace(value, "(token|password|authorization|bearer)\\s*[:=]\\s*[^\\s;]+", "$1=[REDACTED]", RegexOptions.IgnoreCase);
        redacted = Regex.Replace(redacted, "raw\\s+unauthorized\\s+response[^.\\r\\n]*", "unauthorized response redacted", RegexOptions.IgnoreCase);
        return redacted;
    }
}

public sealed record LegalCadasterQueryRawDebug(
    DateTimeOffset CapturedAt,
    int ResponsePageCount,
    int RawRecordCount,
    int? ReportedTotal,
    IReadOnlyList<string> ResponseRootFields,
    IReadOnlyList<LegalCadasterQueryRawDebugRow> Rows);

public sealed record LegalCadasterQueryRawDebugRow(
    int PageNumber,
    int RowNumber,
    IReadOnlyDictionary<string, string?> Values);

public sealed record FiscalCadasterNeighborQuery(
    string TransactionNumber,
    string? GeometryScopeField,
    string? GeometryScopeValue);

public sealed record FiscalCadasterNeighborQueryResult(
    bool Success,
    bool Retryable,
    FiscalCadasterNeighborQuery Query,
    IReadOnlyList<FiscalCadasterNeighborRecord> Records,
    string Status,
    string Message,
    string? Diagnostic)
{
    public static FiscalCadasterNeighborQueryResult NoRecord(FiscalCadasterNeighborQuery query, DateTimeOffset queriedAt)
    {
        return new FiscalCadasterNeighborQueryResult(
            true,
            false,
            query,
            Array.Empty<FiscalCadasterNeighborRecord>(),
            CompareEvidenceStatus.NoRecordReturned,
            "No record returned",
            $"No fiscal neighbor record returned for {query.TransactionNumber} at {queriedAt:O}.");
    }

    public static FiscalCadasterNeighborQueryResult Failed(FiscalCadasterNeighborQuery query, string message, string? diagnostic = null)
    {
        return new FiscalCadasterNeighborQueryResult(
            false,
            true,
            query,
            Array.Empty<FiscalCadasterNeighborRecord>(),
            CompareEvidenceStatus.ServiceUnavailable,
            LegalCadasterQueryResult.Redact(message),
            LegalCadasterQueryResult.Redact(diagnostic ?? message));
    }
}
