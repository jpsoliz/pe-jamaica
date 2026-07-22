using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaLoginTraceService
{
    private const string SchemaVersion = "1.0.0";
    private const string DiagnosticsFolderName = "_diagnostics";
    private const string TraceFileName = "innola_login_trace.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string diagnosticsRoot;
    private readonly Func<DateTimeOffset> getUtcNow;

    public InnolaLoginTraceService(string caseFolderOutputRoot)
        : this(caseFolderOutputRoot, () => DateTimeOffset.UtcNow)
    {
    }

    internal InnolaLoginTraceService(string caseFolderOutputRoot, Func<DateTimeOffset> getUtcNow)
    {
        diagnosticsRoot = string.IsNullOrWhiteSpace(caseFolderOutputRoot)
            ? Path.Combine(AppContext.BaseDirectory, DiagnosticsFolderName)
            : Path.Combine(caseFolderOutputRoot, DiagnosticsFolderName);
        this.getUtcNow = getUtcNow;
    }

    public string TracePath => Path.Combine(diagnosticsRoot, TraceFileName);

    public void Append(
        string step,
        bool success,
        string message,
        IReadOnlyDictionary<string, string?>? details = null)
    {
        try
        {
            Directory.CreateDirectory(diagnosticsRoot);
            var now = getUtcNow().ToString("O");
            var existing = Load();
            var entries = existing?.Entries?.ToList() ?? new List<InnolaLoginTraceEntry>();
            entries.Add(new InnolaLoginTraceEntry(
                now,
                step,
                success,
                Redact(message),
                Redact(details)));

            var document = new InnolaLoginTraceDocument(SchemaVersion, now, entries);
            File.WriteAllText(TracePath, JsonSerializer.Serialize(document, JsonOptions));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
        }
    }

    private InnolaLoginTraceDocument? Load()
    {
        if (!File.Exists(TracePath))
        {
            return null;
        }

        var text = File.ReadAllText(TracePath);
        return string.IsNullOrWhiteSpace(text)
            ? null
            : JsonSerializer.Deserialize<InnolaLoginTraceDocument>(text, JsonOptions);
    }

    private static IReadOnlyDictionary<string, string?>? Redact(IReadOnlyDictionary<string, string?>? details)
    {
        return details?.ToDictionary(
            pair => pair.Key,
            pair => pair.Value is null ? null : Redact(pair.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string Redact(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("token", StringComparison.OrdinalIgnoreCase)
            || value.Contains("access", StringComparison.OrdinalIgnoreCase)
            || value.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || value.Contains("cookie", StringComparison.OrdinalIgnoreCase))
        {
            return "Sensitive diagnostic was redacted.";
        }

        return value;
    }
}

public sealed record InnolaLoginTraceDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("updated_at_utc")] string UpdatedAtUtc,
    [property: JsonPropertyName("entries")] IReadOnlyList<InnolaLoginTraceEntry> Entries);

public sealed record InnolaLoginTraceEntry(
    [property: JsonPropertyName("traced_at_utc")] string TracedAtUtc,
    [property: JsonPropertyName("step")] string Step,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("details")] IReadOnlyDictionary<string, string?>? Details);
