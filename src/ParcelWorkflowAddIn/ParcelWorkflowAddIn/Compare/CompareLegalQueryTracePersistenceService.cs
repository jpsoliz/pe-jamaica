using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Compare;

public sealed class CompareLegalQueryTracePersistenceService
{
    private const string SchemaVersion = "1.0.0";
    private const string FileName = "compare_legal_query_trace.json";
    private const string RawDebugFileName = "compare_legal_query_raw_debug.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string GetTracePath(CaseFolderLayout layout)
    {
        return Path.Combine(layout.WorkingDirectory, FileName);
    }

    public string GetRawDebugPath(CaseFolderLayout layout)
    {
        return Path.Combine(layout.WorkingDirectory, RawDebugFileName);
    }

    public void Append(CaseFolderLayout? layout, string transactionNumber, LegalCadasterQueryResult result, DateTimeOffset tracedAtUtc)
    {
        if (layout is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(layout.WorkingDirectory);
            var path = GetTracePath(layout);
            var existing = Load(path);
            var entries = existing?.Entries?.ToList() ?? new List<CompareLegalQueryTraceEntry>();
            entries.Add(CompareLegalQueryTraceEntry.FromResult(transactionNumber, result, tracedAtUtc));

            var document = new CompareLegalQueryTraceDocument(
                SchemaVersion,
                transactionNumber,
                tracedAtUtc.ToString("O"),
                entries);

            File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions));
            AppendRawDebug(layout, transactionNumber, result, tracedAtUtc);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (JsonException)
        {
        }
    }

    private static void AppendRawDebug(
        CaseFolderLayout layout,
        string transactionNumber,
        LegalCadasterQueryResult result,
        DateTimeOffset tracedAtUtc)
    {
        if (result.RawDebug is null)
        {
            return;
        }

        var path = Path.Combine(layout.WorkingDirectory, RawDebugFileName);
        var existing = LoadRawDebug(path);
        var entries = existing?.Entries?.ToList() ?? new List<CompareLegalQueryRawDebugEntry>();
        entries.Add(CompareLegalQueryRawDebugEntry.FromResult(transactionNumber, result, tracedAtUtc));

        var document = new CompareLegalQueryRawDebugDocument(
            SchemaVersion,
            transactionNumber,
            tracedAtUtc.ToString("O"),
            entries);

        File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions));
    }

    private static CompareLegalQueryTraceDocument? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return JsonSerializer.Deserialize<CompareLegalQueryTraceDocument>(text, JsonOptions);
    }

    private static CompareLegalQueryRawDebugDocument? LoadRawDebug(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return JsonSerializer.Deserialize<CompareLegalQueryRawDebugDocument>(text, JsonOptions);
    }
}

public sealed record CompareLegalQueryTraceDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("updated_at_utc")] string UpdatedAtUtc,
    [property: JsonPropertyName("entries")] IReadOnlyList<CompareLegalQueryTraceEntry> Entries);

public sealed record CompareLegalQueryTraceEntry(
    [property: JsonPropertyName("traced_at_utc")] string TracedAtUtc,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("query_kind")] string QueryKind,
    [property: JsonPropertyName("query_key")] string QueryKey,
    [property: JsonPropertyName("parcel_id")] string? ParcelId,
    [property: JsonPropertyName("volume")] string? Volume,
    [property: JsonPropertyName("folio")] string? Folio,
    [property: JsonPropertyName("land_valuation_number")] string? LandValuationNumber,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("parish")] string? Parish,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("retryable")] bool Retryable,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("diagnostic")] string? Diagnostic,
    [property: JsonPropertyName("record_count")] int RecordCount,
    [property: JsonPropertyName("records")] IReadOnlyList<CompareLegalQueryTraceRecord> Records)
{
    public static CompareLegalQueryTraceEntry FromResult(
        string transactionNumber,
        LegalCadasterQueryResult result,
        DateTimeOffset tracedAtUtc)
    {
        return new CompareLegalQueryTraceEntry(
            tracedAtUtc.ToString("O"),
            transactionNumber,
            result.Query.QueryKind,
            LegalCadasterQueryResult.BuildLegalQueryKey(result.Query),
            result.Query.ParcelId,
            result.Query.Volume,
            result.Query.Folio,
            result.Query.LandValuationNumber,
            result.Query.Name,
            result.Query.Parish,
            result.Success,
            result.Retryable,
            result.Status,
            LegalCadasterQueryResult.Redact(result.Message),
            string.IsNullOrWhiteSpace(result.Diagnostic) ? null : LegalCadasterQueryResult.Redact(result.Diagnostic),
            result.Records.Count,
            result.Records.Select(CompareLegalQueryTraceRecord.FromRecord).ToArray());
    }
}

public sealed record CompareLegalQueryRawDebugDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("updated_at_utc")] string UpdatedAtUtc,
    [property: JsonPropertyName("entries")] IReadOnlyList<CompareLegalQueryRawDebugEntry> Entries);

public sealed record CompareLegalQueryRawDebugEntry(
    [property: JsonPropertyName("traced_at_utc")] string TracedAtUtc,
    [property: JsonPropertyName("captured_at_utc")] string CapturedAtUtc,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("query_kind")] string QueryKind,
    [property: JsonPropertyName("query_key")] string QueryKey,
    [property: JsonPropertyName("parcel_id")] string? ParcelId,
    [property: JsonPropertyName("volume")] string? Volume,
    [property: JsonPropertyName("folio")] string? Folio,
    [property: JsonPropertyName("land_valuation_number")] string? LandValuationNumber,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("parish")] string? Parish,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("diagnostic")] string? Diagnostic,
    [property: JsonPropertyName("response_page_count")] int ResponsePageCount,
    [property: JsonPropertyName("reported_total")] int? ReportedTotal,
    [property: JsonPropertyName("raw_record_count")] int RawRecordCount,
    [property: JsonPropertyName("response_root_fields")] IReadOnlyList<string> ResponseRootFields,
    [property: JsonPropertyName("rows")] IReadOnlyList<CompareLegalQueryRawDebugRow> Rows)
{
    public static CompareLegalQueryRawDebugEntry FromResult(
        string transactionNumber,
        LegalCadasterQueryResult result,
        DateTimeOffset tracedAtUtc)
    {
        var rawDebug = result.RawDebug ?? new LegalCadasterQueryRawDebug(
            tracedAtUtc,
            0,
            0,
            null,
            Array.Empty<string>(),
            Array.Empty<LegalCadasterQueryRawDebugRow>());

        return new CompareLegalQueryRawDebugEntry(
            tracedAtUtc.ToString("O"),
            rawDebug.CapturedAt.ToString("O"),
            transactionNumber,
            result.Query.QueryKind,
            LegalCadasterQueryResult.BuildLegalQueryKey(result.Query),
            result.Query.ParcelId,
            result.Query.Volume,
            result.Query.Folio,
            result.Query.LandValuationNumber,
            result.Query.Name,
            result.Query.Parish,
            result.Success,
            result.Status,
            LegalCadasterQueryResult.Redact(result.Message),
            string.IsNullOrWhiteSpace(result.Diagnostic) ? null : LegalCadasterQueryResult.Redact(result.Diagnostic),
            rawDebug.ResponsePageCount,
            rawDebug.ReportedTotal,
            rawDebug.RawRecordCount,
            rawDebug.ResponseRootFields,
            rawDebug.Rows.Select(CompareLegalQueryRawDebugRow.FromRow).ToArray());
    }
}

public sealed record CompareLegalQueryRawDebugRow(
    [property: JsonPropertyName("page_number")] int PageNumber,
    [property: JsonPropertyName("row_number")] int RowNumber,
    [property: JsonPropertyName("values")] IReadOnlyDictionary<string, string?> Values)
{
    public static CompareLegalQueryRawDebugRow FromRow(LegalCadasterQueryRawDebugRow row)
    {
        return new CompareLegalQueryRawDebugRow(
            row.PageNumber,
            row.RowNumber,
            row.Values.ToDictionary(
                pair => pair.Key,
                pair => string.IsNullOrEmpty(pair.Value) ? pair.Value : LegalCadasterQueryResult.Redact(pair.Value),
                StringComparer.OrdinalIgnoreCase));
    }
}

public sealed record CompareLegalQueryTraceRecord(
    [property: JsonPropertyName("owner_name")] string? OwnerName,
    [property: JsonPropertyName("party_role")] string? PartyRole,
    [property: JsonPropertyName("parcel_id")] string? ParcelId,
    [property: JsonPropertyName("volume")] string? Volume,
    [property: JsonPropertyName("folio")] string? Folio,
    [property: JsonPropertyName("title_record_id")] string? TitleRecordId,
    [property: JsonPropertyName("land_valuation_number")] string? LandValuationNumber,
    [property: JsonPropertyName("parish")] string? Parish,
    [property: JsonPropertyName("source_label")] string SourceLabel,
    [property: JsonPropertyName("status")] string Status)
{
    public static CompareLegalQueryTraceRecord FromRecord(LegalCadasterRecord record)
    {
        return new CompareLegalQueryTraceRecord(
            record.OwnerName,
            record.PartyRole,
            record.ParcelId,
            record.Volume,
            record.Folio,
            record.TitleRecordId,
            record.LandValuationNumber,
            record.Parish,
            record.SourceLabel,
            record.Status);
    }
}
