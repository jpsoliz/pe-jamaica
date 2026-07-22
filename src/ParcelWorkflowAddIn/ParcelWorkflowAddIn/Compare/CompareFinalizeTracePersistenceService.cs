using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Compare;

public sealed class CompareFinalizeTracePersistenceService
{
    private const string SchemaVersion = "1.0.0";
    private const string FileName = "compare_finalize_trace.json";

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
        DateTimeOffset tracedAtUtc,
        string step,
        bool success,
        string message,
        IReadOnlyDictionary<string, string?>? details = null)
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
            var entries = existing?.Entries?.ToList() ?? new List<CompareFinalizeTraceEntry>();
            entries.Add(new CompareFinalizeTraceEntry(
                tracedAtUtc.ToString("O"),
                transactionNumber,
                step,
                success,
                LegalCadasterQueryResult.Redact(message),
                Redact(details)));

            var document = new CompareFinalizeTraceDocument(
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

    private static CompareFinalizeTraceDocument? Load(string path)
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

        return JsonSerializer.Deserialize<CompareFinalizeTraceDocument>(text, JsonOptions);
    }

    private static IReadOnlyDictionary<string, string?>? Redact(IReadOnlyDictionary<string, string?>? details)
    {
        return details?.ToDictionary(
            pair => pair.Key,
            pair => pair.Value is null ? null : LegalCadasterQueryResult.Redact(pair.Value),
            StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record CompareFinalizeTraceDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("updated_at_utc")] string UpdatedAtUtc,
    [property: JsonPropertyName("entries")] IReadOnlyList<CompareFinalizeTraceEntry> Entries);

public sealed record CompareFinalizeTraceEntry(
    [property: JsonPropertyName("traced_at_utc")] string TracedAtUtc,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("step")] string Step,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("details")] IReadOnlyDictionary<string, string?>? Details);
