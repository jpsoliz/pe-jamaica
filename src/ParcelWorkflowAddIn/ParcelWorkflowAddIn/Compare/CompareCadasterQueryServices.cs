using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Compare;

public interface ILegalCadasterQueryService
{
    Task<LegalCadasterQueryResult> QueryByParcelIdAsync(
        string parcelId,
        CancellationToken cancellationToken = default);

    Task<LegalCadasterQueryResult> QueryByVolumeFolioAsync(
        string volume,
        string folio,
        CancellationToken cancellationToken = default);

    Task<LegalCadasterQueryResult> QueryByLandValuationNumberAsync(
        string landValuationNumber,
        string? parish = null,
        CancellationToken cancellationToken = default);

    Task<LegalCadasterQueryResult> QueryByNameAsync(
        string name,
        string parish,
        CancellationToken cancellationToken = default);
}

public interface IFiscalCadasterQueryService
{
    Task<FiscalCadasterNeighborQueryResult> QueryNeighborsAsync(
        SelectedInnolaTransaction transaction,
        CompareWorkingGeometryLoadPlan? geometryPlan,
        CancellationToken cancellationToken = default);
}

public static class CompareCadasterQueryServiceFactory
{
    public static ILegalCadasterQueryService CreateLegal(InnolaTransactionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Mode.Equals("mock", StringComparison.OrdinalIgnoreCase))
        {
            return new MockLegalCadasterQueryService();
        }

        var source = settings.CompareCadaster.Legal;
        if (!source.Enabled || string.IsNullOrWhiteSpace(source.ServiceUrl))
        {
            return new UnsupportedLegalCadasterQueryService(
                "Legal cadaster endpoint is not configured.",
                "Configure compare_legal_cadaster before enabling live legal cadaster queries.");
        }

        return new UnsupportedLegalCadasterQueryService(
            "Legal cadaster live adapter is not implemented.",
            $"Legal cadaster source '{source.SourceName}' is configured, but the service contract is not implemented yet.");
    }

    public static IFiscalCadasterQueryService CreateFiscal(InnolaTransactionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Mode.Equals("mock", StringComparison.OrdinalIgnoreCase))
        {
            return new MockFiscalCadasterQueryService();
        }

        var source = settings.CompareCadaster.Fiscal;
        if (!source.Enabled || string.IsNullOrWhiteSpace(source.ServiceUrl))
        {
            return new UnsupportedFiscalCadasterQueryService(
                "Fiscal cadaster endpoint is not configured.",
                "Configure compare_fiscal_cadaster before enabling live fiscal neighbor queries.");
        }

        return new UnsupportedFiscalCadasterQueryService(
            "Fiscal cadaster live adapter is not implemented.",
            $"Fiscal cadaster source '{source.SourceName}' is configured, but the service contract is not implemented yet.");
    }
}

public sealed class MockLegalCadasterQueryService : ILegalCadasterQueryService
{
    private readonly Func<DateTimeOffset> getUtcNow;
    private readonly IReadOnlyList<LegalCadasterRecord> records;

    public MockLegalCadasterQueryService(
        IReadOnlyList<LegalCadasterRecord>? records = null,
        Func<DateTimeOffset>? getUtcNow = null)
    {
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
        this.records = records ?? Array.Empty<LegalCadasterRecord>();
    }

    public Task<LegalCadasterQueryResult> QueryByParcelIdAsync(
        string parcelId,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("parcel_id", parcelId.Trim(), null, null);
        var matches = records
            .Where(record => string.Equals(record.ParcelId, parcelId.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return Task.FromResult(BuildResult(query, matches));
    }

    public Task<LegalCadasterQueryResult> QueryByVolumeFolioAsync(
        string volume,
        string folio,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("volume_folio", null, volume.Trim(), folio.Trim());
        var matches = records
            .Where(record => string.Equals(record.Volume, volume.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(record.Folio, folio.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return Task.FromResult(BuildResult(query, matches));
    }

    public Task<LegalCadasterQueryResult> QueryByLandValuationNumberAsync(
        string landValuationNumber,
        string? parish = null,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("land_valuation_number", null, null, null, landValuationNumber.Trim(), null, parish?.Trim());
        var matches = records
            .Where(record => string.Equals(record.LandValuationNumber, landValuationNumber.Trim(), StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(parish)
                    || string.Equals(record.Parish, parish.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        return Task.FromResult(BuildResult(query, matches));
    }

    public Task<LegalCadasterQueryResult> QueryByNameAsync(
        string name,
        string parish,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("name_parish", null, null, null, null, name.Trim(), parish.Trim());
        var matches = records
            .Where(record => !string.IsNullOrWhiteSpace(record.OwnerName)
                && record.OwnerName.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(record.Parish, parish.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return Task.FromResult(BuildResult(query, matches));
    }

    private LegalCadasterQueryResult BuildResult(LegalCadasterQuery query, IReadOnlyList<LegalCadasterRecord> matches)
    {
        if (matches.Count == 0)
        {
            return LegalCadasterQueryResult.NoRecord(query, getUtcNow());
        }

        var queryKey = LegalCadasterQueryResult.BuildLegalQueryKey(query);
        var keyedMatches = matches
            .Select(record => record with { QueryKey = queryKey })
            .ToArray();
        var status = matches.Count == 1 ? CompareEvidenceStatus.Ready : CompareEvidenceStatus.Ambiguous;
        return new LegalCadasterQueryResult(
            true,
            false,
            query,
            keyedMatches,
            status,
            matches.Count == 1 ? "Legal cadaster record returned." : "Multiple legal cadaster records returned.",
            null);
    }
}

public sealed class UnsupportedLegalCadasterQueryService : ILegalCadasterQueryService
{
    private readonly string message;
    private readonly string diagnostic;

    public UnsupportedLegalCadasterQueryService(
        string? message = null,
        string? diagnostic = null)
    {
        this.message = string.IsNullOrWhiteSpace(message)
            ? "Legal cadaster endpoint is not configured."
            : message;
        this.diagnostic = string.IsNullOrWhiteSpace(diagnostic)
            ? "Configure compare_legal_cadaster before enabling live legal cadaster queries."
            : diagnostic;
    }

    public Task<LegalCadasterQueryResult> QueryByParcelIdAsync(
        string parcelId,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("parcel_id", parcelId.Trim(), null, null);
        return Task.FromResult(LegalCadasterQueryResult.Failed(
            query,
            message,
            diagnostic));
    }

    public Task<LegalCadasterQueryResult> QueryByVolumeFolioAsync(
        string volume,
        string folio,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("volume_folio", null, volume.Trim(), folio.Trim());
        return Task.FromResult(LegalCadasterQueryResult.Failed(
            query,
            message,
            diagnostic));
    }

    public Task<LegalCadasterQueryResult> QueryByLandValuationNumberAsync(
        string landValuationNumber,
        string? parish = null,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("land_valuation_number", null, null, null, landValuationNumber.Trim(), null, parish?.Trim());
        return Task.FromResult(LegalCadasterQueryResult.Failed(
            query,
            message,
            diagnostic));
    }

    public Task<LegalCadasterQueryResult> QueryByNameAsync(
        string name,
        string parish,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("name_parish", null, null, null, null, name.Trim(), parish.Trim());
        return Task.FromResult(LegalCadasterQueryResult.Failed(
            query,
            message,
            diagnostic));
    }
}

public sealed class MockFiscalCadasterQueryService : IFiscalCadasterQueryService
{
    private readonly Func<DateTimeOffset> getUtcNow;
    private readonly IReadOnlyList<FiscalCadasterNeighborRecord> records;

    public MockFiscalCadasterQueryService(
        IReadOnlyList<FiscalCadasterNeighborRecord>? records = null,
        Func<DateTimeOffset>? getUtcNow = null)
    {
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
        this.records = records ?? Array.Empty<FiscalCadasterNeighborRecord>();
    }

    public Task<FiscalCadasterNeighborQueryResult> QueryNeighborsAsync(
        SelectedInnolaTransaction transaction,
        CompareWorkingGeometryLoadPlan? geometryPlan,
        CancellationToken cancellationToken = default)
    {
        var query = new FiscalCadasterNeighborQuery(
            transaction.TransactionNumber,
            geometryPlan?.ScopeField,
            geometryPlan?.ScopeValue);
        if (records.Count == 0)
        {
            return Task.FromResult(FiscalCadasterNeighborQueryResult.NoRecord(query, getUtcNow()));
        }

        return Task.FromResult(new FiscalCadasterNeighborQueryResult(
            true,
            false,
            query,
            records,
            CompareEvidenceStatus.Ready,
            "Fiscal cadaster neighbor records returned.",
            null));
    }
}

public sealed class UnsupportedFiscalCadasterQueryService : IFiscalCadasterQueryService
{
    private readonly string message;
    private readonly string diagnostic;

    public UnsupportedFiscalCadasterQueryService(
        string? message = null,
        string? diagnostic = null)
    {
        this.message = string.IsNullOrWhiteSpace(message)
            ? "Fiscal cadaster endpoint is not configured."
            : message;
        this.diagnostic = string.IsNullOrWhiteSpace(diagnostic)
            ? "Configure compare_fiscal_cadaster before enabling live fiscal neighbor queries."
            : diagnostic;
    }

    public Task<FiscalCadasterNeighborQueryResult> QueryNeighborsAsync(
        SelectedInnolaTransaction transaction,
        CompareWorkingGeometryLoadPlan? geometryPlan,
        CancellationToken cancellationToken = default)
    {
        var query = new FiscalCadasterNeighborQuery(
            transaction.TransactionNumber,
            geometryPlan?.ScopeField,
            geometryPlan?.ScopeValue);
        return Task.FromResult(FiscalCadasterNeighborQueryResult.Failed(
            query,
            message,
            diagnostic));
    }
}
