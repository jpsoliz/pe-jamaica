using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ParcelWorkflowAddIn.Workflow.Toolbox.Model;

namespace ParcelWorkflowAddIn.Workflow.Toolbox.Persistence;

public sealed record ToolboxDiskLimits(int MetadataBytes = 1024 * 1024, int Assets = 256,
    int AssetBytes = 64 * 1024 * 1024, long RevisionBytes = 512L * 1024 * 1024,
    int Records = 10_000, long TraversalBytes = 64L * 1024 * 1024,
    int JsonDepth = 64, int DirectoryDepth = 8, int Entries = 3_000_000)
{
    public void Validate()
    {
        var defaults = new ToolboxDiskLimits();
        if (MetadataBytes <= 0 || MetadataBytes > defaults.MetadataBytes || Assets < 0 || Assets > defaults.Assets
            || AssetBytes <= 0 || AssetBytes > defaults.AssetBytes || RevisionBytes <= 0 || RevisionBytes > defaults.RevisionBytes
            || Records <= 0 || Records > defaults.Records || TraversalBytes <= 0 || TraversalBytes > defaults.TraversalBytes
            || JsonDepth <= 0 || JsonDepth > defaults.JsonDepth || DirectoryDepth <= 0 || DirectoryDepth > defaults.DirectoryDepth
            || Entries <= 0 || Entries > defaults.Entries) throw new ToolboxDiskException("invalid_limits");
    }
}

public sealed class ToolboxDiskException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed record ToolboxDiskOwner(string Contract, int SchemaVersion, ToolboxCaseIdentity Case,
    string StoreId, string StorageMode);
public sealed record ToolboxDiskParent(string RevisionId, string RevisionDigest, long Sequence);
public sealed record ToolboxDiskAsset(string Sha256, long ByteLength);
public sealed record ToolboxDiskIntent(string Contract, int SchemaVersion, string StoreId, ToolboxCaseIdentity Case,
    string OperationId, string ProposedRevisionId, ToolboxDiskParent? ExpectedParent, string Actor, string PreparedAt,
    ToolboxDiskAsset Bundle, ImmutableArray<ToolboxDiskAsset>? Assets);
public sealed record ToolboxDiskRevision(string Contract, int SchemaVersion, string StoreId, ToolboxCaseIdentity Case,
    string OperationId, string RevisionId, ToolboxDiskParent? Parent, long Sequence, string Actor, string PreparedAt,
    ToolboxDiskAsset Bundle, ImmutableArray<ToolboxDiskAsset>? Assets, string RequestDigest, string RevisionDigest);
public sealed record ToolboxDiskPointer(string Contract, int SchemaVersion, string StoreId, ToolboxCaseIdentity Case,
    string RevisionId, string RevisionDigest, long Sequence);
public sealed record ToolboxDiskAbandonment(string Actor, string At, string Reason);
public sealed record ToolboxDiskJournal(string Contract, int SchemaVersion, ToolboxDiskRevision Revision,
    ToolboxDiskPointer? PreviousPointer, string State, ToolboxDiskAbandonment? Abandonment);
public sealed record ToolboxDiskReceipt(string Contract, int SchemaVersion, string StoreId, ToolboxCaseIdentity Case,
    string OperationId, string RequestDigest, string RevisionId, string RevisionDigest, long Sequence);
public sealed record ToolboxPreparedSave(ToolboxDiskIntent Intent, ImmutableArray<byte> Bundle,
    ImmutableArray<ImmutableArray<byte>>? Assets);

public static class ToolboxPrototypeDiskContract
{
    public const string Contract = "toolbox_prototype_disk_v1";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, Encoder = JavaScriptEncoder.Default };
    public static readonly ImmutableDictionary<Type, ImmutableArray<string>> Fields = new Dictionary<Type, string>
    {
        [typeof(ToolboxDiskOwner)] = "contract schema_version case store_id storage_mode",
        [typeof(ToolboxCaseIdentity)] = "environment case_id transaction_id transaction_number",
        [typeof(ToolboxDiskParent)] = "revision_id revision_digest sequence",
        [typeof(ToolboxDiskAsset)] = "sha256 byte_length",
        [typeof(ToolboxDiskIntent)] = "contract schema_version store_id case operation_id proposed_revision_id expected_parent actor prepared_at bundle assets",
        [typeof(ToolboxDiskRevision)] = "contract schema_version store_id case operation_id revision_id parent sequence actor prepared_at bundle assets request_digest revision_digest",
        [typeof(ToolboxDiskPointer)] = "contract schema_version store_id case revision_id revision_digest sequence",
        [typeof(ToolboxDiskAbandonment)] = "actor at reason",
        [typeof(ToolboxDiskJournal)] = "contract schema_version revision previous_pointer state abandonment",
        [typeof(ToolboxDiskReceipt)] = "contract schema_version store_id case operation_id request_digest revision_id revision_digest sequence"
    }.ToImmutableDictionary(p => p.Key, p => p.Value.Split(' ').Order(StringComparer.Ordinal).ToImmutableArray());
    private static readonly NullabilityInfoContext Nullability = new();
    public static readonly ImmutableArray<string> JournalStates = ["prepared", "sealed", "published", "committed", "abandoned"];

    public static byte[] Encode<T>(T value)
    {
        Validate(value!);
        return Canonical(JsonSerializer.SerializeToElement(value, Options));
    }

    public static T Decode<T>(ReadOnlyMemory<byte> bytes, ToolboxDiskLimits? limits = null) where T : class
    {
        limits ??= new(); limits.Validate();
        Require(bytes.Length <= limits.MetadataBytes, "limit_exceeded");
        try
        {
            _ = Utf8.GetCharCount(bytes.Span);
            using var document = JsonDocument.Parse(bytes, new() { MaxDepth = limits.JsonDepth });
            Shape(document.RootElement, typeof(T), false, limits);
            var value = document.RootElement.Deserialize<T>(Options) ?? throw new ToolboxDiskException("invalid_metadata");
            Validate(value); ValidateLimits(value, limits); return value;
        }
        catch (Exception e) when (e is JsonException or DecoderFallbackException or NotSupportedException or FormatException or OverflowException or InvalidOperationException)
        { throw new ToolboxDiskException("invalid_metadata"); }
    }

    public static ToolboxPreparedSave Prepare(string storeId, ToolboxCaseIdentity identity, string operationId,
        string revisionId, ToolboxDiskParent? parent, string actor, DateTimeOffset preparedAt,
        ReadOnlyMemory<byte> bundle, IEnumerable<ReadOnlyMemory<byte>>? assets = null, ToolboxDiskLimits? limits = null)
    {
        limits ??= new(); limits.Validate();
        Require(bundle.Length <= new ToolboxStorageLimits().BundleBytes && bundle.Length <= limits.RevisionBytes, "limit_exceeded");
        long total = bundle.Length;
        var ownedBundle = ImmutableArray.Create(bundle.ToArray());
        ImmutableArray<ImmutableArray<byte>>? ownedAssets = null;
        ImmutableArray<ToolboxDiskAsset>? inventory = null;
        if (assets is not null)
        {
            var copies = ImmutableArray.CreateBuilder<ImmutableArray<byte>>();
            var entries = ImmutableArray.CreateBuilder<ToolboxDiskAsset>(); var hashes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asset in assets)
            {
                Require(copies.Count < limits.Assets && asset.Length <= limits.AssetBytes, "limit_exceeded");
                total = checked(total + asset.Length); Require(total <= limits.RevisionBytes, "limit_exceeded");
                var copy = ImmutableArray.Create(asset.ToArray()); var hash = Hash(copy.AsSpan());
                Require(hashes.Add(hash), "duplicate_asset"); copies.Add(copy); entries.Add(new(hash, copy.Length));
            }
            ownedAssets = copies.ToImmutable(); inventory = entries.ToImmutable();
        }
        Require(total <= limits.RevisionBytes, "limit_exceeded");
        var intent = new ToolboxDiskIntent(Contract, 1, storeId, identity, operationId, revisionId, parent,
            actor, preparedAt.ToString("O", CultureInfo.InvariantCulture), new(Hash(ownedBundle.AsSpan()), ownedBundle.Length), inventory);
        Validate(intent);
        Require(Encode(intent).Length <= limits.MetadataBytes, "limit_exceeded");
        _ = Revision(intent);
        return new(intent, ownedBundle, ownedAssets);
    }

    public static string RequestDigest(ToolboxDiskIntent intent) => Hash(Encode(intent));
    public static ToolboxDiskRevision Revision(ToolboxDiskIntent intent)
    {
        Validate(intent);
        long sequence;
        try { sequence = checked((intent.ExpectedParent?.Sequence ?? 0) + 1); }
        catch (OverflowException) { throw new ToolboxDiskException("sequence_overflow"); }
        var revision = new ToolboxDiskRevision(Contract, 1, intent.StoreId, intent.Case, intent.OperationId,
            intent.ProposedRevisionId, intent.ExpectedParent, sequence, intent.Actor, intent.PreparedAt,
            intent.Bundle, intent.Assets, RequestDigest(intent), "");
        return revision with { RevisionDigest = RevisionDigest(revision) };
    }

    public static ToolboxDiskIntent Intent(ToolboxDiskRevision revision) => new(Contract, 1, revision.StoreId,
        revision.Case, revision.OperationId, revision.RevisionId, revision.Parent, revision.Actor, revision.PreparedAt, revision.Bundle, revision.Assets);
    public static string RevisionDigest(ToolboxDiskRevision revision) => Hash(Canonical(JsonSerializer.SerializeToElement(revision, Options), "revision_digest"));
    public static ToolboxDiskPointer Pointer(ToolboxDiskRevision revision) => new(Contract, 1, revision.StoreId,
        revision.Case, revision.RevisionId, revision.RevisionDigest, revision.Sequence);
    public static ToolboxDiskParent Parent(ToolboxDiskPointer pointer) => new(pointer.RevisionId, pointer.RevisionDigest, pointer.Sequence);
    public static ToolboxDiskReceipt Receipt(ToolboxDiskRevision revision) => new(Contract, 1, revision.StoreId,
        revision.Case, revision.OperationId, revision.RequestDigest, revision.RevisionId, revision.RevisionDigest, revision.Sequence);
    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public static bool Same<T>(T left, T right) => Encode(left).AsSpan().SequenceEqual(Encode(right));

    public static void Validate(object value)
    {
        switch (value)
        {
            case ToolboxDiskOwner o:
                Header(o.Contract, o.SchemaVersion); Identity(o.Case); Id(o.StoreId); Require(o.StorageMode == "prototype_store"); break;
            case ToolboxDiskIntent i:
                Header(i.Contract, i.SchemaVersion); Identity(i.Case); Id(i.StoreId); Id(i.OperationId); Id(i.ProposedRevisionId);
                if (i.ExpectedParent is not null) Validate(i.ExpectedParent);
                Text(i.Actor); Timestamp(i.PreparedAt); Validate(i.Bundle); Inventory(i.Assets);
                Require(i.Bundle.ByteLength <= new ToolboxStorageLimits().BundleBytes
                    && i.Bundle.ByteLength + (i.Assets?.Sum(a => a.ByteLength) ?? 0) <= new ToolboxDiskLimits().RevisionBytes, "limit_exceeded"); break;
            case ToolboxDiskRevision r:
                Header(r.Contract, r.SchemaVersion); Validate(Intent(r)); Require(r.Parent?.Sequence != long.MaxValue, "sequence_overflow");
                Require(r.Sequence > 0 && r.Sequence == (r.Parent?.Sequence ?? 0) + 1);
                Digest(r.RequestDigest); Digest(r.RevisionDigest);
                Require(r.RequestDigest == RequestDigest(Intent(r)) && r.RevisionDigest == RevisionDigest(r), "checksum_mismatch"); break;
            case ToolboxDiskPointer p:
                Header(p.Contract, p.SchemaVersion); Identity(p.Case); Id(p.StoreId); Id(p.RevisionId); Digest(p.RevisionDigest); Require(p.Sequence > 0); break;
            case ToolboxDiskParent p: Id(p.RevisionId); Digest(p.RevisionDigest); Require(p.Sequence > 0); break;
            case ToolboxDiskAsset a: Digest(a.Sha256); Require(a.ByteLength >= 0 && a.ByteLength <= new ToolboxDiskLimits().RevisionBytes); break;
            case ToolboxDiskReceipt r:
                Header(r.Contract, r.SchemaVersion); Identity(r.Case); Id(r.StoreId); Id(r.OperationId); Id(r.RevisionId);
                Digest(r.RequestDigest); Digest(r.RevisionDigest); Require(r.Sequence > 0); break;
            case ToolboxDiskJournal j:
                Header(j.Contract, j.SchemaVersion); Validate(j.Revision); Require(JournalStates.Contains(j.State));
                Require(j.PreviousPointer is null ? j.Revision.Parent is null
                    : j.Revision.Parent == Parent(j.PreviousPointer) && j.PreviousPointer.Case == j.Revision.Case
                      && j.PreviousPointer.StoreId == j.Revision.StoreId, "parent_conflict");
                if (j.PreviousPointer is not null) Validate(j.PreviousPointer);
                Require((j.State == "abandoned") == (j.Abandonment is not null));
                if (j.Abandonment is not null) Validate(j.Abandonment); break;
            case ToolboxDiskAbandonment a: Text(a.Actor); Timestamp(a.At); Text(a.Reason); break;
            default: throw new ToolboxDiskException("unsupported_record");
        }
    }

    private static void Inventory(ImmutableArray<ToolboxDiskAsset>? inventory)
    {
        if (inventory is not { } values) return;
        Require(!values.IsDefault && values.Length <= 256, "limit_exceeded");
        var hashes = new HashSet<string>(StringComparer.Ordinal); long total = 0;
        foreach (var asset in values) { Validate(asset); Require(asset.ByteLength <= 64 * 1024 * 1024); Require(hashes.Add(asset.Sha256), "duplicate_asset"); total = checked(total + asset.ByteLength); }
        Require(total <= 512L * 1024 * 1024, "limit_exceeded");
    }
    private static void ValidateLimits(object value, ToolboxDiskLimits limits)
    {
        ToolboxDiskIntent? intent = value switch
        { ToolboxDiskIntent i => i, ToolboxDiskRevision r => Intent(r), ToolboxDiskJournal j => Intent(j.Revision), _ => null };
        if (intent is null) return;
        long total = intent.Bundle.ByteLength;
        if (intent.Assets is { } assets)
        {
            Require(assets.Length <= limits.Assets, "limit_exceeded");
            foreach (var asset in assets)
            { Require(asset.ByteLength <= limits.AssetBytes, "limit_exceeded"); total = checked(total + asset.ByteLength); }
        }
        Require(total <= limits.RevisionBytes, "limit_exceeded");
    }
    private static void Header(string contract, int schema) => Require(contract == Contract && schema == 1, "unsupported_contract");
    private static void Identity(ToolboxCaseIdentity c)
    { Require(c is not null); Text(c!.Environment); Text(c.CaseId); Text(c.TransactionId); Text(c.TransactionNumber); }
    private static void Text(string? s)
    {
        Require(!string.IsNullOrWhiteSpace(s) && s.Length <= 4096);
        try { _ = Utf8.GetByteCount(s!); }
        catch (EncoderFallbackException) { throw new ToolboxDiskException("invalid_metadata"); }
    }
    private static void Timestamp(string s) => Require(DateTimeOffset.TryParseExact(s, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
    public static void Id(string? s) => Require(s is { Length: 32 } && s.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'), "invalid_id");
    public static void Digest(string? s) => Require(s is { Length: 64 } && s.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'), "invalid_digest");
    public static void Require(bool condition, string code = "invalid_metadata") { if (!condition) throw new ToolboxDiskException(code); }

    private static void Shape(JsonElement node, Type type, bool nullable, ToolboxDiskLimits limits)
    {
        if (node.ValueKind == JsonValueKind.Null) { Require(nullable); return; }
        if (Nullable.GetUnderlyingType(type) is { } inner) { Shape(node, inner, true, limits); return; }
        if (type == typeof(string)) { Require(node.ValueKind == JsonValueKind.String && node.GetString()!.Length <= 4096); return; }
        if (type == typeof(int)) { Require(node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out _)); return; }
        if (type == typeof(long)) { Require(node.ValueKind == JsonValueKind.Number && node.TryGetInt64(out _)); return; }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
        {
            Require(node.ValueKind == JsonValueKind.Array && node.GetArrayLength() <= limits.Assets, "limit_exceeded");
            foreach (var item in node.EnumerateArray())
            {
                Shape(item, type.GenericTypeArguments[0], false, limits);
                Require(item.GetProperty("byte_length").GetInt64() <= limits.AssetBytes, "limit_exceeded");
            }
            return;
        }
        Require(node.ValueKind == JsonValueKind.Object);
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(p => JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name), StringComparer.Ordinal);
        Require(Fields.TryGetValue(type, out var manifest)
            && properties.Keys.Order(StringComparer.Ordinal).SequenceEqual(manifest), "unsupported_record");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in node.EnumerateObject())
        {
            Require(seen.Add(field.Name), "duplicate_field");
            Require(properties.TryGetValue(field.Name, out var property), "unknown_field");
            Shape(field.Value, property!.PropertyType, Nullability.Create(property).ReadState == NullabilityState.Nullable
                || Nullable.GetUnderlyingType(property.PropertyType) is not null, limits);
        }
        Require(seen.Count == properties.Count, "missing_field");
        if (type == typeof(ToolboxDiskIntent) || type == typeof(ToolboxDiskRevision))
        {
            long total = node.GetProperty("bundle").GetProperty("byte_length").GetInt64();
            var assets = node.GetProperty("assets");
            if (assets.ValueKind != JsonValueKind.Null)
                foreach (var asset in assets.EnumerateArray()) total = checked(total + asset.GetProperty("byte_length").GetInt64());
            Require(total >= 0 && total <= limits.RevisionBytes, "limit_exceeded");
        }
    }

    private static byte[] Canonical(JsonElement node, string? excludedRootField = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new() { Encoder = JavaScriptEncoder.Default })) Write(node, writer, excludedRootField);
        return stream.ToArray();
        static void Write(JsonElement element, Utf8JsonWriter writer, string? excluded)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                writer.WriteStartObject();
                foreach (var p in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                { if (p.Name == excluded) continue; writer.WritePropertyName(p.Name); Write(p.Value, writer, null); }
                writer.WriteEndObject();
            }
            else if (element.ValueKind == JsonValueKind.Array)
            { writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) Write(item, writer, null); writer.WriteEndArray(); }
            else element.WriteTo(writer);
        }
    }
}
