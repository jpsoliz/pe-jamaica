using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Compare;

public sealed class CompareTitleSourceTracePersistenceService
{
    private const string SchemaVersion = "1.0.0";
    private const string FileName = "compare_title_source_trace.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string GetTracePath(CaseFolderLayout layout)
    {
        return Path.Combine(layout.WorkingDirectory, FileName);
    }

    public void Append(
        CaseFolderLayout? layout,
        string transactionNumber,
        string volume,
        string folio,
        CompareTitleSourceSearchResult search,
        CompareTitleDownloadResult? download,
        DateTimeOffset tracedAtUtc)
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
            var entries = existing?.Entries?.ToList() ?? new List<CompareTitleSourceTraceEntry>();
            entries.Add(CompareTitleSourceTraceEntry.FromResult(
                transactionNumber,
                volume,
                folio,
                search,
                download,
                tracedAtUtc));

            var document = new CompareTitleSourceTraceDocument(
                SchemaVersion,
                transactionNumber,
                tracedAtUtc.ToString("O"),
                entries);

            File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions));
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

    private static CompareTitleSourceTraceDocument? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var text = File.ReadAllText(path);
        return string.IsNullOrWhiteSpace(text)
            ? null
            : JsonSerializer.Deserialize<CompareTitleSourceTraceDocument>(text, JsonOptions);
    }
}

public sealed record CompareTitleSourceTraceDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("updated_at_utc")] string UpdatedAtUtc,
    [property: JsonPropertyName("entries")] IReadOnlyList<CompareTitleSourceTraceEntry> Entries);

public sealed record CompareTitleSourceTraceEntry(
    [property: JsonPropertyName("traced_at_utc")] string TracedAtUtc,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("volume")] string Volume,
    [property: JsonPropertyName("folio")] string Folio,
    [property: JsonPropertyName("reference_no")] string ReferenceNo,
    [property: JsonPropertyName("search_success")] bool SearchSuccess,
    [property: JsonPropertyName("search_message")] string SearchMessage,
    [property: JsonPropertyName("search_diagnostic")] string? SearchDiagnostic,
    [property: JsonPropertyName("source_count")] int SourceCount,
    [property: JsonPropertyName("sources")] IReadOnlyList<CompareTitleSourceTraceRecord> Sources,
    [property: JsonPropertyName("download_success")] bool? DownloadSuccess,
    [property: JsonPropertyName("download_message")] string? DownloadMessage,
    [property: JsonPropertyName("download_file_path")] string? DownloadFilePath,
    [property: JsonPropertyName("download_diagnostic")] string? DownloadDiagnostic)
{
    public static CompareTitleSourceTraceEntry FromResult(
        string transactionNumber,
        string volume,
        string folio,
        CompareTitleSourceSearchResult search,
        CompareTitleDownloadResult? download,
        DateTimeOffset tracedAtUtc)
    {
        var cleanVolume = volume.Trim();
        var cleanFolio = folio.Trim();
        return new CompareTitleSourceTraceEntry(
            tracedAtUtc.ToString("O"),
            transactionNumber,
            cleanVolume,
            cleanFolio,
            $"{cleanVolume}/{cleanFolio}",
            search.Success,
            LegalCadasterQueryResult.Redact(search.Message),
            string.IsNullOrWhiteSpace(search.Diagnostic) ? null : LegalCadasterQueryResult.Redact(search.Diagnostic),
            search.Sources.Count,
            search.Sources.Select(CompareTitleSourceTraceRecord.FromRecord).ToArray(),
            download?.Success,
            download is null ? null : LegalCadasterQueryResult.Redact(download.Message),
            download?.FilePath,
            string.IsNullOrWhiteSpace(download?.Diagnostic) ? null : LegalCadasterQueryResult.Redact(download.Diagnostic));
    }
}

public sealed record CompareTitleSourceTraceRecord(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("reference_no")] string? ReferenceNo,
    [property: JsonPropertyName("document_type")] string? DocumentType,
    [property: JsonPropertyName("status")] string? Status)
{
    public static CompareTitleSourceTraceRecord FromRecord(CompareTitleSourceRecord record)
    {
        return new CompareTitleSourceTraceRecord(
            record.SourceId,
            record.DisplayName,
            record.ReferenceNo,
            record.DocumentType,
            record.Status);
    }
}
