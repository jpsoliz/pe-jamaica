using System.Collections.Immutable;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.Workflow.Review.Canonical;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

namespace ParcelWorkflowAddIn.Workflow.Toolbox.Model;

public sealed record ToolboxCaseIdentity(string Environment, string CaseId, string TransactionId, string TransactionNumber)
{
    public static ToolboxCaseIdentity From(ToolboxCaseContext context) =>
        new(context.ServerEnvironment, context.CaseId, context.TransactionId, context.NormalizedTransactionNumber);
}
public sealed record ToolboxIdentityMap(ToolboxCaseIdentity Case, ImmutableDictionary<string, string> Ids);
public enum ToolboxInclusion { Included, Excluded, Unresolved }
public sealed record ToolboxInclusionRecord(string ParcelId, ToolboxInclusion State, string Actor, DateTimeOffset At,
    string Reason, string EvidenceId, string PolicyVersion);
public sealed record ToolboxParcel(string Id, string SourceKey, string Label);
public sealed record ToolboxEntity(string Id, string SourceKey, string ParcelId, JsonElement Evidence);
public sealed record ToolboxMembership(string ParcelId, string EntityId, int Sequence, string Direction, string Role);

public sealed class ToolboxParcelModel
{
    private ToolboxParcelModel(ToolboxCaseIdentity identity, CanonicalReviewContract source, ToolboxIdentityMap map,
        ImmutableDictionary<string, ToolboxInclusion> inclusions, ImmutableArray<ToolboxInclusionRecord> history,
        ImmutableArray<ToolboxSourceReference> linkedSources, ToolboxModelRelations? relations = null)
    {
        Case = identity; Source = source; IdentityMap = map; Inclusions = inclusions; InclusionHistory = history;
        LinkedSources = linkedSources;
        Relations = relations;
        Parcels = source.Parcels.Select(x => new ToolboxParcel(Id("parcel", x.Key), x.Key, x.Label)).ToImmutableArray();
        Rows = source.Rows.Select(x => new ToolboxEntity(Id("row", x.Key), x.Key, Id("parcel", x.ParcelKey), x.Data)).ToImmutableArray();
        Segments = source.Segments.Select(x => new ToolboxEntity(Id("segment", x.Key), x.Key, Id("parcel", x.ParcelKey), x.Data)).ToImmutableArray();
        PointMemberships = source.PointMemberships.Select(x => Member(x, "row")).ToImmutableArray();
        SegmentMemberships = source.SegmentMemberships.Select(x => Member(x, "segment")).ToImmutableArray();
    }

    public ToolboxCaseIdentity Case { get; }
    public CanonicalReviewContract Source { get; }
    public ToolboxIdentityMap IdentityMap { get; }
    public ImmutableArray<ToolboxParcel> Parcels { get; }
    public ImmutableArray<ToolboxEntity> Rows { get; }
    public ImmutableArray<ToolboxEntity> Segments { get; }
    public ImmutableArray<ToolboxMembership> PointMemberships { get; }
    public ImmutableArray<ToolboxMembership> SegmentMemberships { get; }
    public ImmutableDictionary<string, ToolboxInclusion> Inclusions { get; }
    public ImmutableArray<ToolboxInclusionRecord> InclusionHistory { get; }
    public ImmutableArray<ToolboxSourceReference> LinkedSources { get; }
    public ToolboxModelRelations? Relations { get; }

    internal static ToolboxParcelModel Restore(ToolboxCaseIdentity identity, CanonicalReviewContract source,
        ToolboxIdentityMap map, ImmutableDictionary<string, ToolboxInclusion> inclusions,
        ImmutableArray<ToolboxInclusionRecord> history, ImmutableArray<ToolboxSourceReference> linkedSources,
        ToolboxModelRelations? relations, ImmutableArray<CanonicalReviewContract> auditCandidates = default)
    {
        Persistence.ToolboxStorageValidation.Identity(identity);
        Persistence.ToolboxStorageJson.Require(map.Case == identity, "foreign_case");
        Persistence.ToolboxStorageJson.Require(string.Equals(source.TransactionNumber.Trim(), identity.TransactionNumber,
            StringComparison.OrdinalIgnoreCase), "foreign_case");
        Persistence.ToolboxStorageJson.Require(map.Ids.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() == map.Ids.Count
            && inclusions.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() == inclusions.Count, "duplicate_identity");
        var ids = map.Ids.WithComparers(StringComparer.OrdinalIgnoreCase);
        var keys = source.Parcels.Select(p => Key("parcel", p.Key)).Concat(source.Rows.Select(p => Key("row", p.Key)))
            .Concat(source.Segments.Select(p => Key("segment", p.Key))).ToArray();
        Persistence.ToolboxStorageJson.Require(ids.Count == keys.Length && keys.All(ids.ContainsKey), "missing_reference");
        Persistence.ToolboxStorageJson.Require(ids.Values.All(id => !string.IsNullOrWhiteSpace(id) && id == id.Trim())
            && ids.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == ids.Count, "duplicate_identity");
        var states = inclusions.WithComparers(StringComparer.OrdinalIgnoreCase);
        var parcels = source.Parcels.Select(p => ids[Key("parcel", p.Key)]).ToArray();
        Persistence.ToolboxStorageJson.Require(states.Count == parcels.Length && parcels.All(states.ContainsKey)
            && states.Values.All(Enum.IsDefined), "missing_reference");
        var restored = new ToolboxParcelModel(identity, source, new(identity, ids), states, history, linkedSources, relations);
        var candidates = auditCandidates.IsDefault ? ImmutableArray<CanonicalReviewContract>.Empty : auditCandidates;
        Persistence.ToolboxStorageJson.Require(candidates.All(candidate => string.Equals(candidate.TransactionNumber.Trim(), identity.TransactionNumber,
            StringComparison.OrdinalIgnoreCase)), "foreign_case");
        Persistence.ToolboxStorageValidation.Model(restored, candidates);
        return restored;
    }

    internal ToolboxParcelModel WithRelations(ToolboxModelRelations relations) =>
        new(Case, Source, IdentityMap, Inclusions, InclusionHistory, LinkedSources, relations);

    internal ToolboxParcelModel Reconcile(ToolboxCaseContext context, CanonicalReviewContract source,
        ToolboxIdentityMap map, HashSet<string> retired)
    {
        var projected = Adapt(context, source, map);
        Require(!projected.Parcels.Any(x => retired.Contains(x.Id)), "Retired parent reused as active parcel.");
        var inclusions = projected.Parcels.ToImmutableDictionary(x => x.Id,
            x => Inclusions.TryGetValue(x.Id, out var old) ? old : ToolboxInclusion.Unresolved, StringComparer.OrdinalIgnoreCase);
        return new(Case, source, map, inclusions, InclusionHistory, LinkedSources);
    }

    public ImmutableArray<string> FindingParcels(ToolboxFinding finding) => Relations?.FindingParcels(this, finding) ?? finding.ParcelIds;

    public static ToolboxParcelModel Adapt(ToolboxCaseContext context, CanonicalReviewContract source,
        ToolboxIdentityMap? previous = null, bool newImport = false, Func<string>? allocateId = null)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(source);
        var identity = ToolboxCaseIdentity.From(context);
        Require(context.Sources.Select(x => x.SourceId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == context.Sources.Count,
            "Duplicate linked source identity.");
        Require(string.Equals(source.TransactionNumber.Trim(), identity.TransactionNumber, StringComparison.OrdinalIgnoreCase), "Canonical TR differs from context.");
        Require(previous is not null || newImport, "Re-adaptation requires a carried-forward identity map.");
        Require(previous is null || previous.Case == identity, "Foreign case identity map.");
        var keys = source.Parcels.Select(x => Key("parcel", x.Key)).Concat(source.Rows.Select(x => Key("row", x.Key)))
            .Concat(source.Segments.Select(x => Key("segment", x.Key))).ToArray();
        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);
        if (previous is not null)
        {
            Require(previous.Ids.Count == keys.Length && keys.All(previous.Ids.ContainsKey), "Incomplete/inconsistent carried-forward map.");
            foreach (var key in keys) builder.Add(key, previous.Ids[key]);
        }
        else foreach (var key in keys) builder.Add(key, (allocateId ?? (() => Guid.NewGuid().ToString("N")))());
        Require(builder.Values.All(x => !string.IsNullOrWhiteSpace(x) && x == x.Trim())
            && builder.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == builder.Count, "Blank or reused domain IDs.");
        var map = new ToolboxIdentityMap(identity, builder.ToImmutable());
        var inclusions = source.Parcels.ToImmutableDictionary(x => map.Ids[Key("parcel", x.Key)],
            _ => ToolboxInclusion.Included, StringComparer.OrdinalIgnoreCase);
        return new(identity, source, map, inclusions, [], context.Sources.ToImmutableArray());
    }

    public ToolboxParcelModel ReAdapt(ToolboxCaseContext context, CanonicalReviewContract source)
    {
        var projected = Adapt(context, source, IdentityMap);
        return new(projected.Case, source, IdentityMap, Inclusions, InclusionHistory, projected.LinkedSources, Relations);
    }

    public ImmutableArray<ToolboxParcel> ResolveSelection(ToolboxCaseContext context)
    {
        Require(ToolboxCaseIdentity.From(context) == Case, "Foreign context.");
        Require(context.SelectedParcelIds.All(id => Parcels.Any(x => Same(x.Id, id))), "Unknown selected parcel.");
        return context.SelectedParcelIds.Select(id => Parcels.Single(x => Same(x.Id, id))).ToImmutableArray();
    }

    public ToolboxParcelModel SetInclusion(ToolboxInclusionRecord record, string acceptedPolicyVersion, bool authorized)
    {
        Require(Parcels.Any(x => Same(x.Id, record.ParcelId)) && Enum.IsDefined(record.State), "Unknown parcel or inclusion.");
        Require(authorized && !string.IsNullOrWhiteSpace(record.Actor) && !string.IsNullOrWhiteSpace(record.Reason)
            && Source.HasEvidence(record.EvidenceId) && !string.IsNullOrWhiteSpace(acceptedPolicyVersion)
            && record.PolicyVersion == acceptedPolicyVersion, "Inclusion disposition lacks authority, policy, reason or evidence.");
        return new(Case, Source, IdentityMap, Inclusions.SetItem(record.ParcelId, record.State), InclusionHistory.Add(record), LinkedSources, Relations);
    }

    public string InclusionFingerprint => ToolboxSemanticHash.Of(new
    {
        Case, included = Inclusions.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new { x.Key, x.Value }),
        history = InclusionHistory.Select(x => new { x.ParcelId, x.State, x.Actor, x.Reason, x.EvidenceId, x.PolicyVersion, x.At })
    });

    public string ParcelFingerprint(string parcelId, string geometryPolicyToken)
        => Relations is null ? BaseParcelFingerprint(parcelId, geometryPolicyToken) : ToolboxSemanticHash.Of(new
        {
            version = "toolbox_geometry_dependencies_v1", geometry = BaseParcelFingerprint(parcelId, geometryPolicyToken),
            dependencies = Relations.Dependencies.ConsumerFingerprint(this, ToolboxDependencyKind.Parcel, parcelId)
        });

    internal string BaseParcelFingerprint(string parcelId, string geometryPolicyToken)
    {
        Require(!string.IsNullOrWhiteSpace(geometryPolicyToken), "Geometry policy token is required.");
        Require(Parcels.Any(x => Same(x.Id, parcelId)), "Unknown parcel.");
        object Entities(ImmutableArray<ToolboxEntity> entities) => entities.Where(x => Same(x.ParcelId, parcelId))
            .OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => new { x.Id, data = ToolboxSemanticHash.WithoutLabels(x.Evidence) }).ToArray();
        object Members(ImmutableArray<ToolboxMembership> members) => members.Where(x => Same(x.ParcelId, parcelId)).OrderBy(x => x.Sequence).ToArray();
        return ToolboxSemanticHash.Of(new { Case, parcelId, geometryPolicyToken, rows = Entities(Rows), segments = Entities(Segments),
            points = Members(PointMemberships), boundaries = Members(SegmentMemberships) });
    }

    public bool KnowsScope(ToolboxScopeKind kind, string? id) => id is not null && (kind switch
    {
        ToolboxScopeKind.Case => Same(id, Case.CaseId),
        ToolboxScopeKind.Parcel => Parcels.Any(x => Same(x.Id, id)),
        ToolboxScopeKind.Document => Source.Sources.Any(x => Same(x.Id, id)),
        ToolboxScopeKind.Boundary => Segments.Any(x => Same(x.Id, id)) || Relations?.Boundaries.Any(x => Same(x.Id, id)) == true,
        ToolboxScopeKind.Participant => HasGlobalKey("participants", id),
        ToolboxScopeKind.IdentifierPair => HasGlobalKey("identifier_pairs", id),
        _ => false
    });

    public ImmutableArray<string> ParcelsForScope(ToolboxScopeKind kind, string? id)
    {
        if (kind == ToolboxScopeKind.Parcel) return Parcels.Where(x => Same(x.Id, id ?? "")).Select(x => x.Id).ToImmutableArray();
        if (kind == ToolboxScopeKind.Boundary) return Segments.Where(x => Same(x.Id, id ?? "")).Select(x => x.ParcelId)
            .Concat((Relations?.Boundaries ?? []).Where(x => Same(x.Id, id ?? "")).SelectMany(x => x.Members.Select(m => m.ParcelId)))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToImmutableArray();
        var property = kind switch { ToolboxScopeKind.Participant => "participants", ToolboxScopeKind.IdentifierPair => "identifier_pairs", _ => "" };
        if (property.Length > 0 && Source.GlobalData.TryGetProperty(property, out var facts)
            && facts.TryGetProperty(id ?? "", out var fact) && fact.TryGetProperty("parcel_ids", out var links))
            return links.EnumerateArray().Select(x => IdentityMap.Ids[Key("parcel", x.GetString()!)]).Distinct().ToImmutableArray();
        return Parcels.Select(x => x.Id).ToImmutableArray();
    }

    private bool HasGlobalKey(string property, string id) => Source.GlobalData.TryGetProperty(property, out var values)
        && values.ValueKind == JsonValueKind.Object && values.EnumerateObject().Any(x => Same(x.Name, id));
    private ToolboxMembership Member(CanonicalMembership x, string kind) =>
        new(Id("parcel", x.ParcelKey), Id(kind, x.EntityKey), x.Sequence, x.Direction, x.Role);
    private string Id(string kind, string key) => IdentityMap.Ids[Key(kind, key)];
    private static string Key(string kind, string value) => kind + ":" + value;
    internal static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}

internal static class ToolboxSemanticHash
{
    public static string Of(object value)
    {
        var node = JsonSerializer.SerializeToNode(value)!;
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Sort(node).ToJsonString()))).ToLowerInvariant();
    }
    public static JsonElement WithoutLabels(JsonElement element)
    {
        var node = JsonNode.Parse(element.GetRawText())!.AsObject();
        foreach (var name in new[] { "parcel_name", "display_label", "label", "object_id", "saved_at" }) node.Remove(name);
        return JsonSerializer.SerializeToElement(node);
    }
    private static JsonNode Sort(JsonNode node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new KeyValuePair<string, JsonNode?>(x.Key, x.Value is null ? null : Sort(x.Value)))),
        JsonArray array => new JsonArray(array.Select(x => x is null ? null : Sort(x)).ToArray()),
        _ => node.DeepClone()
    };
}
