using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ParcelWorkflowAddIn.Workflow.Toolbox.Model;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

namespace ParcelWorkflowAddIn.Workflow.Toolbox.Persistence;

internal sealed class ToolboxStorageException(string code, string path) : Exception(code)
{
    public string Code { get; } = code;
    public string Path { get; } = path;
}

internal static class ToolboxStorageJson
{
    internal static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly NullabilityInfoContext Nullability = new();
    internal static readonly ImmutableDictionary<Type, ImmutableArray<string>> Fields = new Dictionary<Type, string>
    {
        [typeof(ToolboxWorkspaceStorageDocument)] = "contract schema_version storage_mode case fingerprint_contracts root_model_id models examination current_check_reviews reconciliation payloads policy_assets",
        [typeof(ToolboxStoredModel)] = "snapshot_id case source_payload_id identity_map inclusions inclusion_history linked_sources relations",
        [typeof(ToolboxStoredRelations)] = "policy source_hashes boundaries dependencies archive_model_ids lineage applicability readiness_barriers human_input_bindings finding_input_bindings adjacency",
        [typeof(ToolboxStoredExamination)] = "case findings human_reviews decisions",
        [typeof(ToolboxStoredReconciliation)] = "revision root_model_id reruns lineage operations accepted_dispositions",
        [typeof(ToolboxStoredRerun)] = "case batch_id run_id tool_version provider_version source_hashes base_revision base_fingerprint candidate_digest reconciliation_policy review_policy status candidate_payload_id",
        [typeof(ToolboxStoredPayload)] = "payload_id kind utf8_base64 sha256",
        [typeof(ToolboxStoredPolicyAsset)] = "pin payload_id",
        [typeof(ToolboxCaseIdentity)] = "environment case_id transaction_id transaction_number",
        [typeof(ToolboxIdentityMap)] = "case ids",
        [typeof(ToolboxInclusionRecord)] = "parcel_id state actor at reason evidence_id policy_version",
        [typeof(ToolboxSourceReference)] = "source_id server_environment transaction_id transaction_number revision_id",
        [typeof(ToolboxPolicyPin)] = "policy_id policy_version sha256",
        [typeof(ToolboxAcceptedOperation)] = "operation_id digest result_revision",
        [typeof(ToolboxAcceptedDispositions)] = "operation_id digest parent_revision result_revision accepted_at dispositions",
        [typeof(ToolboxCandidateDisposition)] = "kind old_id incoming_key output_key choice actor reason evidence_id explicit_identity",
        [typeof(ToolboxAppliedLineage)] = "proposal child_ids",
        [typeof(ToolboxParcelLineage)] = "operation_id case kind parent_revision result_revision parent_ids child_keys actor reason evidence_id child_inclusions",
        [typeof(ToolboxHistoryApplicability)] = "record_id original_revision target_parcel_keys disposition actor reason evidence_id",
        [typeof(ToolboxPhysicalBoundary)] = "id case revision geometry_digest geometry_semantics semantics_verified evidence_ids members consistency actor reason policy_version point_ids",
        [typeof(ToolboxBoundaryMember)] = "parcel_id segment_id sequence direction role",
        [typeof(ToolboxAdjacencyReview)] = "case parcel_keys complete evidence_id actor reason policy_version",
        [typeof(ToolboxDependencyInventory)] = "case version complete nodes edges",
        [typeof(ToolboxDependencyNode)] = "id kind reference_id check_binding decision_parcel_id",
        [typeof(ToolboxDependencyCheckBinding)] = "stage item_id check_id scope_kind scope_id revision input_fingerprint geometry_check",
        [typeof(ToolboxDependencyEdge)] = "producer_id consumer_id",
        [typeof(ToolboxFinding)] = "id origin parcel_ids document_id page zone extraction_corrected geometry_defect history rule_id code severity",
        [typeof(ToolboxFindingEvent)] = "stage actor at status reason evidence_id input_fingerprint rule_version policy_version dependency_fingerprint",
        [typeof(ToolboxHumanReview)] = "evidence_id raw_value reviewed_value capture_method presence accuracy stage actor at reason input_revision source_fingerprint",
        [typeof(ToolboxCheckReview)] = "resolution_fingerprint outcome reason evidence_ids actor at finding_id geometry_check evidence_fingerprint geometry_fingerprints canonical_input_fingerprint dependency_fingerprint",
        [typeof(ToolboxReadinessSnapshot)] = "case stage status geometry_approved fingerprint actor at detected included excluded unresolved current_accepted reasons findings reviews parcel_dispositions",
        [typeof(ToolboxParcelDisposition)] = "case stage policy parcel_id fingerprint ready actor at reasons"
    }.ToImmutableDictionary(p => p.Key, p => p.Value.Split(' ').Order(StringComparer.Ordinal).ToImmutableArray());
    internal static readonly ImmutableDictionary<Type, string> Enums = new Dictionary<Type, string>
    {
        [typeof(ToolboxPreset)] = "compute:0 compare:1 pla_a:2 pla_b:3 rt:4",
        [typeof(ToolboxInclusion)] = "included:0 excluded:1 unresolved:2",
        [typeof(ToolboxRerunStatus)] = "succeeded:0 partial:1 failed:2 canceled:3",
        [typeof(ToolboxValueChoice)] = "unresolved:0 retain:1 replace:2 new:3 retire:4",
        [typeof(ToolboxLineageKind)] = "split:0 merge:1",
        [typeof(ToolboxApplicabilityDisposition)] = "retained:0 applicable:1 not_applicable:2 unresolved:3",
        [typeof(ToolboxBoundaryConsistency)] = "confirmed:0 unresolved:1 not_applicable:2",
        [typeof(ToolboxDependencyKind)] = "parcel:0 point:1 segment:2 boundary:3 evidence:4 source:5 participant:6 identifier_pair:7 global_facts:8 policy:9 check:10 decision:11 artifact:12 case:13",
        [typeof(ToolboxScopeKind)] = "case:0 document:1 parcel:2 participant:3 boundary:4 identifier_pair:5",
        [typeof(ToolboxFindingOrigin)] = "extraction_mismatch:0 source_defect:1 mixed:2 unknown:3",
        [typeof(ToolboxCheckOutcome)] = "passed:0 failed:1 not_evaluable:2 pending:3",
        [typeof(ToolboxPresence)] = "present:0 missing_from_source:1 not_extracted:2 uncertain:3 not_reviewed:4",
        [typeof(ToolboxAccuracy)] = "confirmed:0 corrected:1 uncertain:2 not_reviewed:3",
        [typeof(ToolboxReadinessStatus)] = "blocked:0 pending:1 ready:2 ready_with_recorded_defects:3"
    }.ToImmutableDictionary();
    internal const string FieldShapeSha256 = "3af2d0aca8232223cdefc4fd1cb5b634814ef3b5c8309031237098689e4e4300";
    internal static readonly JsonSerializerOptions Options = CreateOptions();
    private static readonly Lazy<bool> ManifestValidated = new(() =>
    {
        Require(SchemaFingerprint() == FieldShapeSha256, "unsupported_version");
        return true;
    });

    internal static string SchemaFingerprint()
    {
        var lines = new List<string>();
        foreach (var record in Fields.OrderBy(p => p.Key.FullName, StringComparer.Ordinal))
            foreach (var property in Options.GetTypeInfo(record.Key).Properties.OrderBy(p => p.Name, StringComparer.Ordinal))
                lines.Add(record.Key.FullName + "." + property.Name + ":" + TypeName(property.PropertyType) + ":"
                    + NullabilityShape(Nullability.Create((PropertyInfo)property.AttributeProvider!)));
        foreach (var entry in Enums.OrderBy(p => p.Key.FullName, StringComparer.Ordinal)) lines.Add(entry.Key.FullName + ":" + entry.Value);
        return Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(string.Join("\n", lines)))).ToLowerInvariant();

        static string TypeName(Type type) => type.IsGenericType
            ? type.GetGenericTypeDefinition().FullName + "(" + string.Join(",", type.GenericTypeArguments.Select(TypeName)) + ")" : type.FullName!;
        static string NullabilityShape(NullabilityInfo info) => info.ReadState + "(" + string.Join(",", info.GenericTypeArguments.Select(NullabilityShape)) + ")";
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type.IsEnum)
            {
                var actual = string.Join(" ", Enum.GetNames(info.Type).Select(name => JsonNamingPolicy.SnakeCaseLower.ConvertName(name) + ":"
                    + Convert.ToInt64(Enum.Parse(info.Type, name), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)));
                if (!Enums.TryGetValue(info.Type, out var expected) || actual != expected)
                    throw new InvalidOperationException("The enum is not in the v1 storage value manifest.");
            }
            if (info.Type == typeof(ToolboxReadinessSnapshot))
                foreach (var p in info.Properties.Where(p => p.Name is "remote_action_allowed" or "route").ToArray()) info.Properties.Remove(p);
            if (info.Type == typeof(ToolboxSourceReference))
                foreach (var p in info.Properties.Where(p => p.Name == "is_read_only").ToArray()) info.Properties.Remove(p);
            if (info.Kind == JsonTypeInfoKind.Object)
            {
                if (!Fields.TryGetValue(info.Type, out var fields)
                    || !info.Properties.Select(p => p.Name).Order(StringComparer.Ordinal).SequenceEqual(fields))
                    throw new InvalidOperationException("The record shape is not in the v1 storage field manifest.");
                foreach (var p in info.Properties) p.IsRequired = true;
            }
        });
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            TypeInfoResolver = resolver, MaxDepth = 64
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, false));
        options.Converters.Add(new ArrayConverterFactory());
        options.Converters.Add(new DateConverter());
        options.MakeReadOnly();
        return options;
    }

    internal static void Require(bool condition, string code = "invalid_value", string path = "$")
    { if (!condition) throw new ToolboxStorageException(code, path); }

    internal static byte[] Bytes<T>(T value)
    {
        _ = ManifestValidated.Value;
        var node = JsonSerializer.SerializeToNode(value, Options)!;
        return Utf8.GetBytes(Sort(node).ToJsonString(Options));
    }

    private static JsonNode Sort(JsonNode node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new KeyValuePair<string, JsonNode?>(x.Key, x.Value is null ? null : Sort(x.Value)))),
        JsonArray array => new JsonArray(array.Select(x => x is null ? null : Sort(x)).ToArray()),
        _ => node.DeepClone()
    };

    internal static long Inspect(JsonElement root, ToolboxStorageLimits limits, ref long entries, bool wrapper)
    {
        long local = 0;
        var pending = new Stack<(JsonElement Value, int Depth, string Path)>(); pending.Push((root, 0, "$"));
        while (pending.TryPop(out var item))
        {
            Require(item.Depth <= limits.JsonDepth, "limit_exceeded", item.Path);
            if (item.Value.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in item.Value.EnumerateObject())
                {
                    Require(names.Add(p.Name), "duplicate_field", item.Path);
                    Require(p.Name.Length <= limits.TokenCharacters, "limit_exceeded", item.Path);
                    Count(); pending.Push((p.Value, item.Depth + 1, item.Path + "." + p.Name));
                }
            }
            else if (item.Value.ValueKind == JsonValueKind.Array)
                foreach (var v in item.Value.EnumerateArray()) { Count(); pending.Push((v, item.Depth + 1, item.Path + "[]")); }
            else if (item.Value.ValueKind == JsonValueKind.String)
            {
                var s = item.Value.GetString()!;
                // Base64 has its own decoded and encoded budgets; it is not free-text evidence.
                if (!(wrapper && item.Path.EndsWith(".utf8_base64", StringComparison.Ordinal)))
                    Require(Utf8.GetByteCount(s) <= limits.TextBytes, "limit_exceeded", item.Path);
                if (wrapper && IsToken(item.Path)) Require(s.Length <= limits.TokenCharacters, "limit_exceeded", item.Path);
            }
        }
        entries = checked(entries + local);
        Require(entries <= limits.CollectionEntries, "limit_exceeded");
        return local;

        void Count()
        {
            local++;
            Require(local <= limits.CollectionEntries, "limit_exceeded");
        }
    }

    private static bool IsToken(string path) => !new[] { ".reason", ".raw_value", ".reviewed_value", ".zone", ".geometry_semantics", ".utf8_base64", ".readiness_barriers[]", ".reasons[]" }
        .Any(suffix => path.EndsWith(suffix, StringComparison.Ordinal));

    internal static void Preflight(object root, ToolboxStorageLimits limits)
    {
        long entries = 0, byteEstimate = 0;
        var pending = new Stack<(object? Value, int Depth, string Path)>(); pending.Push((root, 0, "$"));
        while (pending.TryPop(out var item))
        {
            Require(item.Depth <= limits.JsonDepth, "limit_exceeded");
            if (item.Value is null) continue;
            if (item.Value is string text)
            {
                var size = Utf8.GetByteCount(text);
                byteEstimate = checked(byteEstimate + size * (item.Path.EndsWith(".utf8_base64", StringComparison.Ordinal) ? 1L : 6L));
                Require(byteEstimate <= limits.BundleBytes, "limit_exceeded");
                if (!item.Path.EndsWith(".utf8_base64", StringComparison.Ordinal)) Require(size <= limits.TextBytes, "limit_exceeded");
                if (IsToken(item.Path)) Require(text.Length <= limits.TokenCharacters, "limit_exceeded");
                continue;
            }
            var type = item.Value.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>)
                && (bool)type.GetProperty("IsDefault")!.GetValue(item.Value)!) continue;
            if (item.Value is System.Collections.IEnumerable values)
            {
                var dictionary = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableDictionary<,>);
                if (dictionary)
                {
                    var comparer = type.GetProperty("KeyComparer")!.GetValue(item.Value);
                    var ignoreCase = item.Path.EndsWith(".ids", StringComparison.Ordinal)
                        || item.Path.EndsWith(".inclusions", StringComparison.Ordinal) || item.Path.EndsWith(".source_hashes", StringComparison.Ordinal);
                    Require(ignoreCase ? ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase)
                        : ReferenceEquals(comparer, StringComparer.Ordinal) || ReferenceEquals(comparer, EqualityComparer<string>.Default),
                        "unsupported_capture", item.Path);
                }
                foreach (var value in values)
                {
                    entries++; Require(entries <= limits.CollectionEntries, "limit_exceeded");
                    if (dictionary)
                    {
                        var pair = value!.GetType(); var key = (string)pair.GetProperty("Key")!.GetValue(value)!;
                        Require(key.Length <= limits.TokenCharacters, "limit_exceeded"); byteEstimate = checked(byteEstimate + 6L * Utf8.GetByteCount(key) + 4);
                        Require(byteEstimate <= limits.BundleBytes, "limit_exceeded");
                        pending.Push((pair.GetProperty("Value")!.GetValue(value), item.Depth + 1, item.Path + ".entry"));
                    }
                    else pending.Push((value, item.Depth + 1, item.Path + "[]"));
                }
                continue;
            }
            var info = Options.GetTypeInfo(type);
            if (info.Kind == JsonTypeInfoKind.Object)
                foreach (var p in info.Properties)
                {
                    entries++; Require(entries <= limits.CollectionEntries, "limit_exceeded");
                    byteEstimate = checked(byteEstimate + p.Name.Length + 8);
                    Require(byteEstimate <= limits.BundleBytes, "limit_exceeded");
                    pending.Push((p.Get!(item.Value), item.Depth + 1, item.Path + "." + p.Name));
                }
        }
    }

    internal static void Shape(JsonElement value, Type type, string path = "$")
    {
        _ = ManifestValidated.Value;
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null) { if (value.ValueKind == JsonValueKind.Null) return; type = underlying; }
        if (type == typeof(string)) { Require(value.ValueKind == JsonValueKind.String, path: path); return; }
        if (type.IsEnum)
        {
            Require(value.ValueKind == JsonValueKind.String && Enums.TryGetValue(type, out var names)
                && names.Split(' ').Select(p => p.Split(':')[0]).Contains(value.GetString(), StringComparer.Ordinal), path: path);
            return;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
        {
            Require(value.ValueKind == JsonValueKind.Array, path: path);
            foreach (var item in value.EnumerateArray()) Shape(item, type.GenericTypeArguments[0], path + "[]");
            return;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableDictionary<,>))
        {
            Require(value.ValueKind == JsonValueKind.Object, path: path);
            var names = new HashSet<string>(path.EndsWith(".ids", StringComparison.Ordinal)
                || path.EndsWith(".inclusions", StringComparison.Ordinal) || path.EndsWith(".source_hashes", StringComparison.Ordinal)
                ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (var p in value.EnumerateObject())
            { Require(names.Add(p.Name), "duplicate_identity", path); Shape(p.Value, type.GenericTypeArguments[1], path + ".entry"); }
            return;
        }
        var info = Options.GetTypeInfo(type);
        if (info.Kind != JsonTypeInfoKind.Object) return; // Primitive conversion is checked by the serializer.
        Require(value.ValueKind == JsonValueKind.Object, path: path);
        var known = info.Properties.ToDictionary(p => p.Name, StringComparer.Ordinal);
        foreach (var p in value.EnumerateObject()) Require(known.ContainsKey(p.Name), "invalid_value", path);
        foreach (var property in info.Properties)
        {
            Require(value.TryGetProperty(property.Name, out var field), "missing_reference", path + "." + property.Name);
            if (field.ValueKind == JsonValueKind.Null)
            {
                var optionalPoints = type == typeof(ToolboxPhysicalBoundary) && property.Name == "point_ids";
                var nullable = Nullable.GetUnderlyingType(property.PropertyType) is not null
                    || property.AttributeProvider is PropertyInfo p && Nullability.Create(p).ReadState == NullabilityState.Nullable;
                Require(optionalPoints || nullable, path: path + "." + property.Name);
            }
            else Shape(field, property.PropertyType, path + "." + property.Name);
        }
    }

    private sealed class DateConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            DateTimeOffset.ParseExact(reader.GetString()!, "O", CultureInfo.InvariantCulture, DateTimeStyles.None);
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
    }

    private sealed class ArrayConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>);
        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(ArrayConverter<>).MakeGenericType(type.GenericTypeArguments[0]))!;
    }

    private sealed class ArrayConverter<T> : JsonConverter<ImmutableArray<T>>
    {
        public override bool HandleNull => true;
        public override ImmutableArray<T> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null ? default : JsonSerializer.Deserialize<T[]>(ref reader, options)!.ToImmutableArray();
        public override void Write(Utf8JsonWriter writer, ImmutableArray<T> value, JsonSerializerOptions options)
        {
            if (value.IsDefault) { writer.WriteNullValue(); return; }
            writer.WriteStartArray(); foreach (var item in value) JsonSerializer.Serialize(writer, item, options); writer.WriteEndArray();
        }
    }
}
