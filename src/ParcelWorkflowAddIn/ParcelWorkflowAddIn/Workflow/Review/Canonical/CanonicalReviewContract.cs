using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ParcelWorkflowAddIn.Workflow.Review.Canonical;

public sealed record CanonicalSource(string Id, string Role, string Status, JsonElement Data);
public sealed record CanonicalTrace(string Id, string SourceId, int? Page, string Zone, JsonElement Data);
public sealed record CanonicalParcel(string Key, string Label, string ValidationStatus, JsonElement Data);
public sealed record CanonicalEntity(string Key, string ParcelKey, JsonElement Data);
public sealed record CanonicalMembership(string ParcelKey, string EntityKey, int Sequence, string Direction, string Role);
public sealed record CanonicalEvidenceLink(string EvidenceId, string ParcelKey, string Role);
public sealed record CanonicalExtractionFinding(string Id, string Scope, ImmutableArray<string> ParcelKeys,
    string EvidenceId, JsonElement Data);

// Owned JsonElements are immutable copies, including unknown nested provider fields.
public sealed class CanonicalReviewContract
{
    private CanonicalReviewContract(JsonObject root)
    {
        JsonSnapshot = root.ToJsonString();
        TransactionNumber = Text(root, "transaction_number");
        Sources = Objects(root, "source_inventory").Select(x => new CanonicalSource(
            Required(x, "source_id"), Text(x, "source_role"), Default(x, "status", "unknown"), Copy(x))).ToImmutableArray();
        Unique(Sources.Select(x => x.Id), "source");
        Traces = Objects(root, "source_traces").Select(x => new CanonicalTrace(
            Required(x, "evidence_id"), Required(x, "source_id"), TracePage(x),
            Default(x, "source_zone", Text(x, "zone")), Copy(x))).ToImmutableArray();
        Unique(Traces.Select(x => x.Id), "evidence");
        foreach (var trace in Traces)
        {
            Require(Sources.Any(x => Same(x.Id, trace.SourceId)), "Unknown source for evidence: " + trace.Id);
            Require(trace.Page is null or > 0, "Evidence page must be positive.");
        }
        var rowObjects = Objects(root, "rows").ToArray();
        var segmentObjects = Objects(root, "segments").ToArray();
        var groups = Objects(root, "parcel_groups").ToArray();
        var declared = groups.Length > 0;
        Require(!root.ContainsKey("parcel_groups") || declared || rowObjects.Length + segmentObjects.Length == 0,
            "Explicit empty candidate inventory conflicts with geometry.");
        if (!declared)
        {
            var ids = rowObjects.Concat(segmentObjects).Select(x => Text(x, "parcel_group_id"))
                .Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (ids.Length == 0 && rowObjects.Concat(segmentObjects).Any())
            {
                var labels = rowObjects.Concat(segmentObjects).Select(x => Text(x, "parcel_name"))
                    .Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                Require(labels.Length <= 1, "Ambiguous legacy groups require explicit canonical keys.");
                ids = ["parcel-001"];
            }
            groups = ids.Select(id => new JsonObject { ["parcel_group_id"] = id }).ToArray();
        }
        Parcels = groups.Select(x => new CanonicalParcel(Required(x, "parcel_group_id"),
            Default(x, "display_label", Default(x, "parcel_name", Default(x, "lot_number", Required(x, "parcel_group_id")))),
            Default(x, "validation_status", "unreviewed"), Copy(x))).ToImmutableArray();
        Unique(Parcels.Select(x => x.Key), "parcel");
        Rows = Entities(rowObjects, "row_id", declared);
        Segments = Entities(segmentObjects, "segment_id", declared);
        PointMemberships = Memberships(root, "point_memberships", Rows, rowObjects, "row_id", "sequence_in_group");
        SegmentMemberships = Memberships(root, "segment_memberships", Segments, segmentObjects, "segment_id", "sequence");
        Links = Objects(root, "evidence_links").Select(x => new CanonicalEvidenceLink(
            Required(x, "evidence_id"), Required(x, "parcel_group_id"), Required(x, "role"))).ToImmutableArray();
        Unique(Links.Select(x => x.EvidenceId + "\0" + x.ParcelKey + "\0" + x.Role), "link");
        foreach (var link in Links)
        {
            Require(HasParcel(link.ParcelKey), "Unknown linked parcel.");
            Require(HasEvidence(link.EvidenceId), "Unknown linked evidence.");
        }
        Findings = Objects(root, "findings").Select(x => new CanonicalExtractionFinding(
            Required(x, "finding_id"), Required(x, "scope"), Strings(x, "parcel_group_ids"),
            Text(x, "evidence_id"), Copy(x))).ToImmutableArray();
        Unique(Findings.Select(x => x.Id), "finding");
        foreach (var finding in Findings)
        {
            Require(new[] { "transaction", "case", "document", "parcel", "segment", "point", "participant", "identifier_pair" }
                .Contains(finding.Scope, StringComparer.Ordinal), "Unknown finding scope.");
            Require(finding.ParcelKeys.All(HasParcel), "Unknown finding parcel.");
            Require(finding.EvidenceId.Length == 0 || HasEvidence(finding.EvidenceId), "Unknown finding evidence.");
        }
        Quality = Copy(root["extraction_quality_summary"] as JsonObject ?? new JsonObject { ["status"] = "unknown" });
        GlobalData = Copy(root);
        foreach (var name in new[] { "participants", "identifier_pairs" })
        {
            if (!root.ContainsKey(name)) continue;
            Require(root[name] is JsonObject, "Expected fact inventory object: " + name);
            var facts = root[name]!.AsObject();
            foreach (var fact in facts)
            {
                Require(!string.IsNullOrWhiteSpace(fact.Key) && fact.Value is JsonObject, "Invalid global fact.");
                var data = fact.Value!.AsObject();
                if (data.ContainsKey("parcel_ids"))
                {
                    Require(data["parcel_ids"] is JsonArray, "Expected parcel_ids array: " + fact.Key);
                    var ids = new List<string>();
                    foreach (var id in data["parcel_ids"]!.AsArray())
                    {
                        Require(id is JsonValue value && value.TryGetValue<string>(out _), "Expected string parcel link: " + fact.Key);
                        var key = id!.GetValue<string>();
                        Require(!string.IsNullOrWhiteSpace(key) && HasParcel(key), "Foreign fact/party parcel link.");
                        ids.Add(key);
                    }
                    Unique(ids, "fact/party parcel link");
                }
                var evidence = Text(data, "source_evidence_id");
                Require(evidence.Length == 0 || HasEvidence(evidence), "Unknown fact evidence.");
            }
            Unique(facts.Select(x => x.Key), name);
        }
    }

    public string TransactionNumber { get; }
    public string JsonSnapshot { get; }
    public JsonElement GlobalData { get; }
    public JsonElement Quality { get; }
    public ImmutableArray<CanonicalSource> Sources { get; }
    public ImmutableArray<CanonicalTrace> Traces { get; }
    public ImmutableArray<CanonicalParcel> Parcels { get; }
    public ImmutableArray<CanonicalEntity> Rows { get; }
    public ImmutableArray<CanonicalEntity> Segments { get; }
    public ImmutableArray<CanonicalMembership> PointMemberships { get; }
    public ImmutableArray<CanonicalMembership> SegmentMemberships { get; }
    public ImmutableArray<CanonicalEvidenceLink> Links { get; }
    public ImmutableArray<CanonicalExtractionFinding> Findings { get; }

    public static CanonicalReviewContract Read(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return new CanonicalReviewContract((JsonObject)root.DeepClone());
    }

    public JsonObject ToJson() => JsonNode.Parse(JsonSnapshot)!.AsObject();
    public bool HasEvidence(string id) => Traces.Any(x => Same(x.Id, id));
    private bool HasParcel(string id) => Parcels.Any(x => Same(x.Key, id));

    private ImmutableArray<CanonicalEntity> Entities(JsonObject[] values, string key, bool declared)
    {
        var result = values.Select(x =>
        {
            var group = Text(x, "parcel_group_id");
            if (group.Length == 0 && !declared && Parcels.Length == 1) group = Parcels[0].Key;
            Require(HasParcel(group), "Missing or unknown parcel for " + key);
            var evidence = Text(x, "source_evidence_id");
            Require(evidence.Length == 0 || HasEvidence(evidence), "Unknown entity evidence.");
            return new CanonicalEntity(Required(x, key), group, Copy(x));
        }).ToImmutableArray();
        Unique(result.Select(x => x.Key), key);
        return result;
    }

    private ImmutableArray<CanonicalMembership> Memberships(JsonObject root, string name,
        ImmutableArray<CanonicalEntity> entities, JsonObject[] values, string key, string order)
    {
        var explicitMemberships = root.ContainsKey(name);
        var members = explicitMemberships
            ? Objects(root, name).Select(x => new CanonicalMembership(Required(x, "parcel_group_id"),
                Required(x, "entity_key"), Number(x, "sequence") ?? 0, Required(x, "direction"), Required(x, "role"))).ToImmutableArray()
            : values.Select((x, i) => new CanonicalMembership(entities[i].ParcelKey, entities[i].Key,
                Number(x, order) ?? 0, "forward", "boundary")).ToImmutableArray();
        Unique(members.Select(x => x.ParcelKey + "\0" + x.Sequence), name + " order");
        foreach (var member in members)
        {
            Require(member.Sequence > 0 && member.Direction is "forward" or "reverse" && member.Role.Length > 0,
                "Missing/invalid membership order, direction or role.");
            Require(entities.Any(x => Same(x.Key, member.EntityKey) && Same(x.ParcelKey, member.ParcelKey)),
                "Orphan or foreign membership.");
        }
        Require(entities.All(x => members.Any(m => Same(m.EntityKey, x.Key))), "Entity missing membership.");
        return members;
    }

    private static IEnumerable<JsonObject> Objects(JsonObject root, string key)
    {
        if (!root.ContainsKey(key)) return [];
        Require(root[key] is JsonArray, "Expected array: " + key);
        var array = root[key]!.AsArray();
        Require(array.All(x => x is JsonObject), "Expected object entries: " + key);
        return array.Cast<JsonObject>();
    }
    private static ImmutableArray<string> Strings(JsonObject root, string key)
    {
        if (!root.ContainsKey(key))
            return Text(root, "parcel_group_id") is { Length: > 0 } id ? [id] : [];
        Require(root[key] is JsonArray, "Expected string array: " + key);
        var values = root[key]!.AsArray().Select(x => x?.GetValue<string>() ?? "").ToImmutableArray();
        Unique(values, key);
        return values;
    }
    private static JsonElement Copy(JsonNode node) => JsonSerializer.SerializeToElement(node);
    private static string Text(JsonObject node, string key) => node[key]?.GetValue<string>()?.Trim() ?? "";
    private static string Default(JsonObject node, string key, string fallback) => Text(node, key) is { Length: > 0 } v ? v : fallback;
    private static string Required(JsonObject node, string key)
    {
        var value = Text(node, key);
        Require(value.Length > 0, "Missing canonical key: " + key);
        return value;
    }
    private static int? Number(JsonObject node, string key)
    {
        if (node[key] is null) return null;
        if (node[key] is JsonValue value && value.TryGetValue<int>(out var number)) return number;
        if (node[key] is JsonValue text && text.TryGetValue<string>(out var raw)
            && int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out number)) return number;
        throw new InvalidDataException("Expected integer: " + key);
    }
    private static int? TracePage(JsonObject node)
    {
        var canonical = Number(node, "source_page"); var alias = Number(node, "page");
        Require(canonical is null || alias is null || canonical == alias, "Conflicting source page aliases.");
        Require(Text(node, "source_zone").Length == 0 || Text(node, "zone").Length == 0
            || Text(node, "source_zone") == Text(node, "zone"), "Conflicting source zone aliases.");
        return canonical ?? alias;
    }
    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static void Unique(IEnumerable<string> values, string kind)
    {
        var keys = values.ToArray();
        Require(keys.All(x => !string.IsNullOrWhiteSpace(x)) && keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() == keys.Length,
            "Blank or duplicate canonical " + kind + " key.");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
