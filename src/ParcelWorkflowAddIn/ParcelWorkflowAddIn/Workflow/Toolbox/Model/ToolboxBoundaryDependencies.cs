using System.Collections.Immutable;
using System.Text.Json;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

namespace ParcelWorkflowAddIn.Workflow.Toolbox.Model;

public enum ToolboxBoundaryConsistency { Confirmed, Unresolved, NotApplicable }
public sealed record ToolboxBoundaryMember(string ParcelId, string SegmentId, int Sequence, string Direction, string Role);
public sealed record ToolboxPhysicalBoundary(string Id, ToolboxCaseIdentity Case, string Revision,
    string GeometryDigest, string GeometrySemantics, bool SemanticsVerified, ImmutableArray<string> EvidenceIds,
    ImmutableArray<ToolboxBoundaryMember> Members, ToolboxBoundaryConsistency Consistency,
    string Actor, string Reason, string PolicyVersion, ImmutableArray<string> PointIds = default);
public sealed record ToolboxBoundaryConflict(string BoundaryId, string Code, ImmutableArray<string> ParcelIds);
public sealed record ToolboxAdjacencyReview(ToolboxCaseIdentity Case, ImmutableArray<string> ParcelKeys,
    bool Complete, string EvidenceId, string Actor, string Reason, string PolicyVersion);
public sealed record ToolboxBoundaryRemoval(string BoundaryId, string Actor, string Reason, string EvidenceId);

public static class ToolboxBoundaryValidator
{
    public static ImmutableArray<ToolboxBoundaryConflict> Validate(ToolboxParcelModel model,
        ImmutableArray<ToolboxPhysicalBoundary> boundaries, string policyVersion)
    {
        ToolboxParcelModel.Require(!boundaries.IsDefault && boundaries.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == boundaries.Length,
            "Duplicate/absent physical boundary inventory.");
        var conflicts = ImmutableArray.CreateBuilder<ToolboxBoundaryConflict>();
        foreach (var b in boundaries)
        {
            ToolboxParcelModel.Require(b.Case == model.Case && !string.IsNullOrWhiteSpace(b.Id)
                && !model.IdentityMap.Ids.Values.Contains(b.Id, StringComparer.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(b.Revision) && !string.IsNullOrWhiteSpace(b.GeometryDigest)
                && !string.IsNullOrWhiteSpace(b.Actor) && !string.IsNullOrWhiteSpace(b.Reason)
                && b.PolicyVersion == policyVersion && Enum.IsDefined(b.Consistency)
                && !b.Members.IsDefaultOrEmpty && !b.EvidenceIds.IsDefaultOrEmpty
                && b.EvidenceIds.All(model.Source.HasEvidence), "Invalid physical boundary identity/evidence.");
            ToolboxParcelModel.Require(b.Members.Select(x => x.ParcelId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == b.Members.Length,
                "Repeated boundary parcel membership.");
            foreach (var m in b.Members)
                ToolboxParcelModel.Require(m.Sequence > 0 && m.Direction is "forward" or "reverse" && m.Role == "boundary"
                    && model.SegmentMemberships.Any(x => ToolboxParcelModel.Same(x.ParcelId, m.ParcelId)
                        && ToolboxParcelModel.Same(x.EntityId, m.SegmentId) && x.Sequence == m.Sequence && x.Direction == m.Direction),
                    "Orphan/foreign physical boundary membership or unsupported role.");
            void Conflict(string code) => conflicts.Add(new(b.Id, code, b.Members.Select(x => x.ParcelId).ToImmutableArray()));
            if (!b.SemanticsVerified || string.IsNullOrWhiteSpace(b.GeometrySemantics)) Conflict("unverified_geometry_semantics");
            if (b.Members.Length > 2) Conflict("unsupported_cardinality");
            if (b.Members.Length == 2 && b.Members[0].Direction == b.Members[1].Direction) Conflict("contradictory_directions");
            if (b.PointIds.IsDefaultOrEmpty) Conflict("unknown_point_dependencies");
            else ToolboxParcelModel.Require(b.PointIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() == b.PointIds.Length
                && b.PointIds.All(id => model.Rows.Any(r => ToolboxParcelModel.Same(r.Id, id) && b.Members.Any(m => ToolboxParcelModel.Same(m.ParcelId, r.ParcelId)))), "Foreign/duplicate boundary point dependency.");
            if (b.Consistency == ToolboxBoundaryConsistency.Unresolved) Conflict("consistency_unresolved");
            if (b.Consistency == ToolboxBoundaryConsistency.NotApplicable && b.Members.Length != 1) Conflict("invalid_not_applicable");
        }
        ToolboxParcelModel.Require(boundaries.SelectMany(x => x.Members.Select(m => m.SegmentId))
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() == boundaries.Sum(x => x.Members.Length), "Segment assigned to several physical boundaries.");
        return conflicts.ToImmutable();
    }
}

public enum ToolboxDependencyKind { Parcel, Point, Segment, Boundary, Evidence, Source, Participant, IdentifierPair, GlobalFacts, Policy, Check, Decision, Artifact, Case }
public sealed record ToolboxDependencyCheckBinding(ToolboxPreset Stage, string ItemId, string CheckId,
    ToolboxScopeKind ScopeKind, string ScopeId, string Revision, string InputFingerprint);
public sealed record ToolboxDependencyNode(string Id, ToolboxDependencyKind Kind, string ReferenceId,
    ToolboxDependencyCheckBinding? CheckBinding = null);
public sealed record ToolboxDependencyEdge(string ProducerId, string ConsumerId);
public sealed record ToolboxImpactPath(string ChangedId, string AffectedId, ImmutableArray<string> Path, string Reason);
public sealed record ToolboxDependencyImpact(ImmutableArray<string> ChangedIds, ImmutableArray<string> AffectedIds,
    ImmutableArray<ToolboxImpactPath> Paths, ImmutableArray<string> UnknownCoverage);

// Explicit directed consumption, not inferred spatial adjacency. Core inputs are hashed from the model.
public sealed record ToolboxDependencyInventory(ToolboxCaseIdentity Case, string Version, bool Complete,
    ImmutableArray<ToolboxDependencyNode> Nodes, ImmutableArray<ToolboxDependencyEdge> Edges)
{
    public void Validate(ToolboxParcelModel model)
    {
        ToolboxParcelModel.Require(Case == model.Case && !string.IsNullOrWhiteSpace(Version)
            && !Nodes.IsDefault && !Edges.IsDefault && Nodes.All(x => !string.IsNullOrWhiteSpace(x.Id)
                && !string.IsNullOrWhiteSpace(x.ReferenceId) && Enum.IsDefined(x.Kind))
            && Nodes.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Nodes.Length,
            "Invalid dependency inventory identity.");
        ToolboxParcelModel.Require(Edges.All(e => Nodes.Any(x => ToolboxParcelModel.Same(x.Id, e.ProducerId))
            && Nodes.Any(x => ToolboxParcelModel.Same(x.Id, e.ConsumerId)) && !ToolboxParcelModel.Same(e.ProducerId, e.ConsumerId))
            && Edges.Distinct().Count() == Edges.Length, "Orphan/duplicate dependency edge.");
        foreach (var n in Nodes) _ = ActualInput(model, n);
        ToolboxParcelModel.Require(Nodes.GroupBy(x => (x.Kind, x.ReferenceId.ToUpperInvariant())).All(x => x.Count() == 1), "Duplicate dependency identity aliases.");
        foreach (var n in Nodes) Visit(n.Id, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        void Visit(string id, HashSet<string> path)
        {
            ToolboxParcelModel.Require(path.Add(id), "Dependency cycle.");
            foreach (var e in Edges.Where(x => ToolboxParcelModel.Same(x.ProducerId, id))) Visit(e.ConsumerId, path);
            path.Remove(id);
        }
    }

    public ImmutableArray<string> CoverageIssues(ToolboxParcelModel model)
    {
        var issues = ImmutableArray.CreateBuilder<string>();
        if (!Complete) issues.Add("Dependency coverage is not authoritative.");
        foreach (var (kind, id) in model.Parcels.Select(x => (ToolboxDependencyKind.Parcel, x.Id))
            .Concat(model.Rows.Select(x => (ToolboxDependencyKind.Point, x.Id)))
            .Concat(model.Segments.Select(x => (ToolboxDependencyKind.Segment, x.Id)))
            .Concat((model.Relations?.Boundaries ?? []).Select(x => (ToolboxDependencyKind.Boundary, x.Id)))
            .Concat(model.Source.Traces.Select(x => (ToolboxDependencyKind.Evidence, x.Id)))
            .Concat(model.Source.Sources.Select(x => (ToolboxDependencyKind.Source, x.Id)))
            .Concat(Facts("participants", ToolboxDependencyKind.Participant))
            .Concat(Facts("identifier_pairs", ToolboxDependencyKind.IdentifierPair)))
        {
            var node = Nodes.FirstOrDefault(x => x.Kind == kind && ToolboxParcelModel.Same(x.ReferenceId, id));
            if (node is null || !HasConsumer(node.Id)) issues.Add("Unknown consumers: " + kind + "/" + id);
        }
        foreach (var entity in model.Rows.Concat(model.Segments))
        {
            var kind = model.Rows.Contains(entity) ? ToolboxDependencyKind.Point : ToolboxDependencyKind.Segment;
            var from = Nodes.FirstOrDefault(x => x.Kind == kind && ToolboxParcelModel.Same(x.ReferenceId, entity.Id));
            var to = Nodes.FirstOrDefault(x => x.Kind == ToolboxDependencyKind.Parcel && ToolboxParcelModel.Same(x.ReferenceId, entity.ParcelId));
            if (from is null || to is null || !Consumes(from.Id, to.Id)) issues.Add("Unregistered geometry consumer: " + entity.Id);
        }
        foreach (var boundary in model.Relations?.Boundaries ?? [])
            foreach (var member in boundary.Members)
            {
                var from = Nodes.FirstOrDefault(x => x.Kind == ToolboxDependencyKind.Boundary && ToolboxParcelModel.Same(x.ReferenceId, boundary.Id));
                var to = Nodes.FirstOrDefault(x => x.Kind == ToolboxDependencyKind.Parcel && ToolboxParcelModel.Same(x.ReferenceId, member.ParcelId));
                if (from is null || to is null || !Consumes(from.Id, to.Id)) issues.Add("Unregistered boundary consumer: " + boundary.Id + "/" + member.ParcelId);
                var segment = Nodes.FirstOrDefault(x => x.Kind == ToolboxDependencyKind.Segment && ToolboxParcelModel.Same(x.ReferenceId, member.SegmentId));
                if (segment is null || from is null || !Consumes(segment.Id, from.Id)) issues.Add("Unregistered boundary source segment: " + member.SegmentId);
            }
        foreach (var boundary in model.Relations?.Boundaries ?? [])
            foreach (var id in boundary.PointIds.IsDefault ? [] : boundary.PointIds)
            {
                var point = Nodes.FirstOrDefault(x => x.Kind == ToolboxDependencyKind.Point && ToolboxParcelModel.Same(x.ReferenceId, id));
                var edge = Nodes.FirstOrDefault(x => x.Kind == ToolboxDependencyKind.Boundary && ToolboxParcelModel.Same(x.ReferenceId, boundary.Id));
                if (point is null || edge is null || !Consumes(point.Id, edge.Id)) issues.Add("Unregistered boundary point: " + id);
            }
        return issues.ToImmutable();
        bool HasConsumer(string id) => Edges.Any(x => ToolboxParcelModel.Same(x.ProducerId, id));
        IEnumerable<(ToolboxDependencyKind, string)> Facts(string name, ToolboxDependencyKind kind) =>
            model.Source.GlobalData.TryGetProperty(name, out var facts) ? facts.EnumerateObject().Select(x => (kind, x.Name)).ToArray() : [];
        bool Consumes(string producer, string consumer)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var queue = new Queue<string>(); queue.Enqueue(producer);
            while (queue.TryDequeue(out var id))
            {
                if (!seen.Add(id)) continue;
                if (ToolboxParcelModel.Same(id, consumer)) return true;
                foreach (var e in Edges.Where(x => ToolboxParcelModel.Same(x.ProducerId, id))) queue.Enqueue(e.ConsumerId);
            }
            return false;
        }
    }

    public string ConsumerFingerprint(ToolboxParcelModel model, ToolboxDependencyKind kind, string id)
    {
        var node = Nodes.SingleOrDefault(x => x.Kind == kind && ToolboxParcelModel.Same(x.ReferenceId, id));
        if (node is null) return "unknown";
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Collect(string current)
        {
            if (!visited.Add(current)) return;
            foreach (var e in Edges.Where(x => ToolboxParcelModel.Same(x.ConsumerId, current))) Collect(e.ProducerId);
        }
        Collect(node.Id);
        return ToolboxSemanticHash.Of(new { version = "toolbox_dependencies_v1", inventoryVersion = Version,
            inputs = Nodes.Where(x => visited.Contains(x.Id)).OrderBy(x => x.Id, StringComparer.Ordinal)
                .Select(x => new { x.Id, x.Kind, x.ReferenceId, value = ActualInput(model, x) }),
            edges = Edges.Where(x => visited.Contains(x.ProducerId) && visited.Contains(x.ConsumerId))
                .OrderBy(x => x.ProducerId, StringComparer.Ordinal).ThenBy(x => x.ConsumerId, StringComparer.Ordinal) });
    }

    internal static string ActualInput(ToolboxParcelModel model, ToolboxDependencyNode node)
    {
        var id = node.ReferenceId;
        object? input = node.Kind switch
        {
            ToolboxDependencyKind.Parcel => model.Parcels.Any(x => ToolboxParcelModel.Same(x.Id, id))
                ? model.BaseParcelFingerprint(id, "dependency_geometry_v1") : null,
            ToolboxDependencyKind.Point => model.Rows.FirstOrDefault(x => ToolboxParcelModel.Same(x.Id, id)) is { } point ? ToolboxSemanticHash.WithoutLabels(point.Evidence) : null,
            ToolboxDependencyKind.Segment => model.Segments.FirstOrDefault(x => ToolboxParcelModel.Same(x.Id, id)) is { } segment ? ToolboxSemanticHash.WithoutLabels(segment.Evidence) : null,
            ToolboxDependencyKind.Boundary => model.Relations?.Boundaries.FirstOrDefault(x => ToolboxParcelModel.Same(x.Id, id)),
            ToolboxDependencyKind.Evidence => model.Source.Traces.FirstOrDefault(x => ToolboxParcelModel.Same(x.Id, id))?.Data,
            ToolboxDependencyKind.Source => model.Relations?.SourceHashes.FirstOrDefault(x => ToolboxParcelModel.Same(x.Key, id)).Value,
            ToolboxDependencyKind.Participant => Fact("participants"),
            ToolboxDependencyKind.IdentifierPair => Fact("identifier_pairs"),
            ToolboxDependencyKind.GlobalFacts => new[] { "survey_metadata", "document_sections", "parties", "representatives", "adjacent_owners", "volume_folios", "global_facts" }
                .Where(name => model.Source.GlobalData.TryGetProperty(name, out _)).ToDictionary(name => name, name => model.Source.GlobalData.GetProperty(name)),
            ToolboxDependencyKind.Policy => model.Relations?.Policy,
            ToolboxDependencyKind.Case => model.Case.CaseId == id ? model.Case : null,
            _ => id
        };
        ToolboxParcelModel.Require(input is not null, "Unknown same-case dependency reference: " + node.Id);
        return ToolboxSemanticHash.Of(input!);
        JsonElement? Fact(string name) => model.Source.GlobalData.TryGetProperty(name, out var facts)
            ? facts.EnumerateObject().Where(x => ToolboxParcelModel.Same(x.Name, id)).Select(x => (JsonElement?)x.Value).FirstOrDefault() : null;
    }

    public static ToolboxDependencyImpact Impact(ToolboxParcelModel oldModel, ToolboxParcelModel newModel)
    {
        var old = oldModel.Relations?.Dependencies;
        var next = newModel.Relations?.Dependencies;
        ToolboxParcelModel.Require(old is not null && next is not null && oldModel.Case == newModel.Case, "Impact needs both same-case inventories.");
        old!.Validate(oldModel); next!.Validate(newModel);
        var ids = old.Nodes.Select(x => x.Id).Union(next.Nodes.Select(x => x.Id), StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToArray();
        var changed = ids.Where(id => Value(old, oldModel, id) != Value(next, newModel, id)
            || ToolboxSemanticHash.Of(old.Edges.Where(x => ToolboxParcelModel.Same(x.ConsumerId, id)).OrderBy(x => x.ProducerId))
                != ToolboxSemanticHash.Of(next.Edges.Where(x => ToolboxParcelModel.Same(x.ConsumerId, id)).OrderBy(x => x.ProducerId))).ToImmutableArray();
        var edges = old.Edges.Union(next.Edges).OrderBy(x => x.ProducerId, StringComparer.Ordinal).ThenBy(x => x.ConsumerId, StringComparer.Ordinal).ToArray();
        var paths = ImmutableArray.CreateBuilder<ToolboxImpactPath>();
        foreach (var start in changed)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start };
            var queue = new Queue<ImmutableArray<string>>(); queue.Enqueue([start]);
            while (queue.TryDequeue(out var path))
            {
                paths.Add(new(start, path[^1], path, "Input or consuming relationship changed (old/new union)."));
                foreach (var e in edges.Where(x => ToolboxParcelModel.Same(x.ProducerId, path[^1])))
                    if (seen.Add(e.ConsumerId)) queue.Enqueue(path.Add(e.ConsumerId));
            }
        }
        return new(changed, paths.Select(x => x.AffectedId).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToImmutableArray(),
            paths.ToImmutable(), old.CoverageIssues(oldModel).AddRange(next.CoverageIssues(newModel)).Distinct().Order().ToImmutableArray());
        static string Value(ToolboxDependencyInventory graph, ToolboxParcelModel model, string id) =>
            graph.Nodes.FirstOrDefault(x => ToolboxParcelModel.Same(x.Id, id)) is { } node ? ActualInput(model, node) : "removed";
    }
}
