using System.Collections.Immutable;
using ParcelWorkflowAddIn.Workflow.Toolbox.Model;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;
using static ParcelWorkflowAddIn.Workflow.Toolbox.Persistence.ToolboxStorageJson;

namespace ParcelWorkflowAddIn.Workflow.Toolbox.Persistence;

internal static class ToolboxStorageValidation
{
    internal static void Text(string? value) => Require(!string.IsNullOrWhiteSpace(value));
    internal static void Identity(ToolboxCaseIdentity identity)
    { Text(identity.Environment); Text(identity.CaseId); Text(identity.TransactionId); Text(identity.TransactionNumber); }
    internal static void Pin(ToolboxPolicyPin pin)
    { Text(pin.PolicyId); Text(pin.PolicyVersion); Text(pin.Sha256); }

    internal static ImmutableArray<ToolboxParcelModel> History(ToolboxParcelModel root)
    {
        var seen = new HashSet<ToolboxParcelModel>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<ToolboxParcelModel>(); queue.Enqueue(root);
        var result = ImmutableArray.CreateBuilder<ToolboxParcelModel>();
        while (queue.TryDequeue(out var model))
        {
            if (!seen.Add(model)) continue;
            Require(seen.Count <= 512, "limit_exceeded");
            Require(model.Case == root.Case, "foreign_case"); result.Add(model);
            if (model.Relations is { } r)
            {
                Require(!r.Archives.IsDefault);
                foreach (var archived in r.Archives) { Require(archived is not null); queue.Enqueue(archived!); }
            }
        }
        return result.ToImmutable();
    }

    internal sealed class Inventory
    {
        internal readonly HashSet<string> Parcels = new(StringComparer.OrdinalIgnoreCase);
        internal readonly HashSet<string> Evidence = new(StringComparer.OrdinalIgnoreCase);
        internal readonly HashSet<string> Documents = new(StringComparer.OrdinalIgnoreCase);
        internal readonly HashSet<string> Entities = new(StringComparer.OrdinalIgnoreCase);
        internal readonly Dictionary<string, List<Review.Canonical.CanonicalTrace>> Traces = new(StringComparer.OrdinalIgnoreCase);
        internal Inventory(IEnumerable<ToolboxParcelModel> models)
        {
            foreach (var m in models)
            {
                Parcels.UnionWith(m.Parcels.Select(p => p.Id)); Entities.UnionWith(m.IdentityMap.Ids.Values);
                Documents.UnionWith(m.Source.Sources.Select(s => s.Id)); Evidence.UnionWith(m.Source.Traces.Select(t => t.Id));
                foreach (var t in m.Source.Traces)
                { if (!Traces.TryGetValue(t.Id, out var values)) Traces[t.Id] = values = []; values.Add(t); }
            }
        }
    }

    internal static void Model(ToolboxParcelModel model, ImmutableArray<Review.Canonical.CanonicalReviewContract> candidates)
    {
        var models = History(model); var known = new Inventory(models);
        Require(!model.InclusionHistory.IsDefault && !model.LinkedSources.IsDefault);
        foreach (var h in model.InclusionHistory)
        {
            Require(Enum.IsDefined(h.State)); Text(h.Actor); Text(h.Reason); Text(h.PolicyVersion);
            Require(known.Parcels.Contains(h.ParcelId) && known.Evidence.Contains(h.EvidenceId), "missing_reference");
        }
        Require(model.LinkedSources.Select(x => x.SourceId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == model.LinkedSources.Length, "duplicate_identity");
        foreach (var s in model.LinkedSources)
        { Text(s.SourceId); Text(s.ServerEnvironment); Text(s.TransactionId); Text(s.TransactionNumber); }
        if (model.Relations is not { } r) return;
        Pin(r.Policy);
        var sourceIds = model.Source.Sources.Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Require(r.SourceHashes.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() == r.SourceHashes.Count, "duplicate_identity");
        Require(r.SourceHashes.Count == sourceIds.Count && sourceIds.SetEquals(r.SourceHashes.Keys)
            && r.SourceHashes.Values.All(x => !string.IsNullOrWhiteSpace(x)), "missing_reference");
        Require(!r.Boundaries.IsDefault && !r.Lineage.IsDefault && !r.Applicability.IsDefault && !r.ReadinessBarriers.IsDefault);
        Boundaries(model, r.Boundaries);
        Dependencies(model, r.Dependencies);
        if (r.Adjacency is { } a)
        {
            Require(a.Case == model.Case, "foreign_case"); Text(a.Actor); Text(a.Reason); Text(a.PolicyVersion);
            Require(!a.ParcelKeys.IsDefault && a.ParcelKeys.Length == model.Parcels.Length
                && a.ParcelKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count() == a.ParcelKeys.Length
                && a.ParcelKeys.All(k => model.IdentityMap.Ids.ContainsKey("parcel:" + k))
                && model.Source.HasEvidence(a.EvidenceId), "missing_reference");
        }
        foreach (var applicability in r.Applicability)
        {
            Text(applicability.RecordId); Text(applicability.OriginalRevision); Text(applicability.Actor); Text(applicability.Reason);
            Require(Enum.IsDefined(applicability.Disposition) && !applicability.TargetParcelKeys.IsDefault);
            Require(known.Evidence.Contains(applicability.EvidenceId)
                || candidates.Any(candidate => candidate.HasEvidence(applicability.EvidenceId)), "missing_reference");
            // Unresolved/latest applicability and retired target keys are readiness state, not broken storage.
        }
        Lineage(r.Lineage, models, candidates);
    }

    private static void Boundaries(ToolboxParcelModel model, ImmutableArray<ToolboxPhysicalBoundary> boundaries)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var members = model.SegmentMemberships.Select(m => (m.ParcelId.ToUpperInvariant(), m.EntityId.ToUpperInvariant(), m.Sequence, m.Direction)).ToHashSet();
        var points = model.Rows.ToDictionary(p => p.Id, p => p.ParcelId, StringComparer.OrdinalIgnoreCase);
        var evidence = model.Source.Traces.Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var b in boundaries)
        {
            Text(b.Id); Text(b.Revision); Text(b.GeometryDigest); Text(b.Actor); Text(b.Reason);
            Require(b.Case == model.Case, "foreign_case"); Require(ids.Add(b.Id), "duplicate_identity");
            Require(!model.IdentityMap.Ids.Values.Contains(b.Id, StringComparer.OrdinalIgnoreCase));
            Require(b.PolicyVersion == ToolboxReconciliation.PolicyVersion && Enum.IsDefined(b.Consistency)
                && !b.Members.IsDefaultOrEmpty && !b.EvidenceIds.IsDefaultOrEmpty);
            Require(b.EvidenceIds.All(evidence.Contains), "missing_reference");
            var parcels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in b.Members)
            {
                Require(m.Sequence > 0 && m.Direction is "forward" or "reverse" && m.Role == "boundary");
                Require(parcels.Add(m.ParcelId) && assigned.Add(m.SegmentId), "duplicate_identity");
                Require(members.Contains((m.ParcelId.ToUpperInvariant(), m.SegmentId.ToUpperInvariant(), m.Sequence, m.Direction)), "missing_reference");
            }
            if (!b.PointIds.IsDefault)
            {
                Require(b.PointIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() == b.PointIds.Length, "duplicate_identity");
                Require(b.PointIds.All(id => points.TryGetValue(id, out var parcel) && parcels.Contains(parcel)), "missing_reference");
            }
        }
    }

    private static void Dependencies(ToolboxParcelModel model, ToolboxDependencyInventory graph)
    {
        Require(graph.Case == model.Case, "foreign_case"); Text(graph.Version);
        Require(!graph.Nodes.IsDefault && !graph.Edges.IsDefault);
        var nodes = new Dictionary<string, ToolboxDependencyNode>(StringComparer.OrdinalIgnoreCase);
        var aliases = new HashSet<(ToolboxDependencyKind, string)>();
        var parcels = model.Parcels.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var points = model.Rows.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var segments = model.Segments.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var boundaries = model.Relations!.Boundaries.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sources = model.Source.Sources.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var traces = model.Source.Traces.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> Facts(string name) => model.Source.GlobalData.TryGetProperty(name, out var obj)
            ? obj.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase) : new(StringComparer.OrdinalIgnoreCase);
        var participants = Facts("participants"); var pairs = Facts("identifier_pairs");
        bool Scope(ToolboxScopeKind kind, string id) => kind switch
        {
            ToolboxScopeKind.Case => ToolboxParcelModel.Same(id, model.Case.CaseId),
            ToolboxScopeKind.Document => sources.Contains(id), ToolboxScopeKind.Parcel => parcels.Contains(id),
            ToolboxScopeKind.Boundary => segments.Contains(id) || boundaries.Contains(id),
            ToolboxScopeKind.Participant => participants.Contains(id), ToolboxScopeKind.IdentifierPair => pairs.Contains(id), _ => false
        };
        foreach (var n in graph.Nodes)
        {
            Text(n.Id); Text(n.ReferenceId); Require(Enum.IsDefined(n.Kind));
            Require(nodes.TryAdd(n.Id, n) && aliases.Add((n.Kind, n.ReferenceId.ToUpperInvariant())), "duplicate_identity");
            Require(n.CheckBinding is null || n.Kind == ToolboxDependencyKind.Check);
            Require(n.DecisionParcelId is null || n.Kind == ToolboxDependencyKind.Decision);
            var known = n.Kind switch
            {
                ToolboxDependencyKind.Parcel => parcels.Contains(n.ReferenceId), ToolboxDependencyKind.Point => points.Contains(n.ReferenceId),
                ToolboxDependencyKind.Segment => segments.Contains(n.ReferenceId), ToolboxDependencyKind.Boundary => boundaries.Contains(n.ReferenceId),
                ToolboxDependencyKind.Source => sources.Contains(n.ReferenceId), ToolboxDependencyKind.Evidence => traces.Contains(n.ReferenceId),
                ToolboxDependencyKind.Participant => participants.Contains(n.ReferenceId), ToolboxDependencyKind.IdentifierPair => pairs.Contains(n.ReferenceId),
                ToolboxDependencyKind.Decision => parcels.Contains(n.DecisionParcelId ?? n.ReferenceId),
                ToolboxDependencyKind.Case => n.ReferenceId == model.Case.CaseId, _ => true
            };
            Require(known, "missing_reference");
            if (n.Kind == ToolboxDependencyKind.Check)
            {
                Require(n.CheckBinding is not null); var b = n.CheckBinding!;
                Require(b.Stage is ToolboxPreset.Compute or ToolboxPreset.Compare && Enum.IsDefined(b.ScopeKind));
                Text(b.ItemId); Text(b.CheckId); Text(b.Revision); Text(b.InputFingerprint); Text(b.ScopeId);
                Require(Scope(b.ScopeKind, b.ScopeId), "missing_reference");
            }
        }
        var consumers = nodes.Keys.ToDictionary(id => id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        var indegree = nodes.Keys.ToDictionary(id => id, _ => 0, StringComparer.OrdinalIgnoreCase);
        var edges = new HashSet<(string, string)>();
        foreach (var e in graph.Edges)
        {
            Require(nodes.ContainsKey(e.ProducerId) && nodes.ContainsKey(e.ConsumerId), "missing_reference");
            Require(!ToolboxParcelModel.Same(e.ProducerId, e.ConsumerId)
                && edges.Add((e.ProducerId.ToUpperInvariant(), e.ConsumerId.ToUpperInvariant())), "duplicate_identity");
            consumers[e.ProducerId].Add(e.ConsumerId); indegree[e.ConsumerId]++;
        }
        var queue = new Queue<string>(indegree.Where(p => p.Value == 0).Select(p => p.Key)); var visited = 0;
        while (queue.TryDequeue(out var id)) { visited++; foreach (var next in consumers[id]) if (--indegree[next] == 0) queue.Enqueue(next); }
        Require(visited == nodes.Count, "inconsistent_state");
    }

    internal static void Examination(ToolboxParcelModel model, ToolboxCaseIdentity? identity,
        ImmutableArray<ToolboxFinding> findings, ImmutableArray<ToolboxHumanReview> human,
        ImmutableArray<ToolboxReadinessSnapshot> decisions, ImmutableArray<ToolboxCheckReview> live)
    {
        Require(!findings.IsDefault && !human.IsDefault && !decisions.IsDefault && !live.IsDefault);
        Require(identity is null || identity == model.Case, "foreign_case");
        Require(identity is not null || findings.Length + human.Length + decisions.Length == 0);
        var known = new Inventory(History(model)); Findings(findings);
        var findingIds = findings.Select(f => f.Id).Concat(decisions.SelectMany(d => d.Findings).Select(f => f.Id)).ToHashSet(StringComparer.Ordinal);
        foreach (var h in human)
        {
            Require(h.Stage is ToolboxPreset.Compute or ToolboxPreset.Compare && Enum.IsDefined(h.Presence) && Enum.IsDefined(h.Accuracy));
            Text(h.Actor); Text(h.Reason); Text(h.InputRevision); Text(h.CaptureMethod);
            Require(known.Evidence.Contains(h.EvidenceId), "missing_reference");
            Require(h.Presence != ToolboxPresence.MissingFromSource || h.Accuracy is ToolboxAccuracy.Confirmed or ToolboxAccuracy.Corrected);
        }
        Reviews(live);
        foreach (var d in decisions)
        {
            Require(d.Case == model.Case, "foreign_case"); Require(Enum.IsDefined(d.Stage) && Enum.IsDefined(d.Status));
            Text(d.Actor); Text(d.Fingerprint); Require(!d.Reasons.IsDefault && !d.ParcelDispositions.IsDefault);
            Require(d.Detected >= 0 && d.Included >= 0 && d.Excluded >= 0 && d.Unresolved >= 0 && d.CurrentAccepted >= 0);
            Findings(d.Findings); Reviews(d.Reviews);
            foreach (var p in d.ParcelDispositions)
            {
                Require(p.Case == model.Case, "foreign_case"); Require(Enum.IsDefined(p.Stage) && !p.Reasons.IsDefault);
                Require(known.Parcels.Contains(p.ParcelId), "missing_reference"); Text(p.Actor); Text(p.Fingerprint); Pin(p.Policy);
            }
        }
        void Findings(ImmutableArray<ToolboxFinding> values)
        {
            Require(!values.IsDefault && values.Select(f => f.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == values.Length);
            foreach (var f in values)
            {
                Text(f.Id); Text(f.RuleId); Text(f.Code);
                Require(Enum.IsDefined(f.Origin) && f.Severity is "info" or "warning" or "error" && f.Page is null or > 0
                    && !f.History.IsDefaultOrEmpty && !f.ParcelIds.IsDefault);
                Require(f.ParcelIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() == f.ParcelIds.Length);
                Require(f.ParcelIds.All(known.Parcels.Contains) && known.Documents.Contains(f.DocumentId), "missing_reference");
                foreach (var e in f.History)
                {
                    Require(e.Stage is ToolboxPreset.Compute or ToolboxPreset.Compare
                        && e.Status is "recorded" or "retained" or "reopened" or "resolved" or "corrected_and_rechecked");
                    Text(e.Actor); Text(e.Reason); Text(e.InputFingerprint); Text(e.RuleVersion); Text(e.PolicyVersion);
                    Require(known.Traces.TryGetValue(e.EvidenceId, out var traces)
                        && traces.Any(t => ToolboxParcelModel.Same(t.SourceId, f.DocumentId)
                            && (f.Page is null || t.Page == f.Page) && (f.Zone.Length == 0 || t.Zone == f.Zone)), "missing_reference");
                }
            }
        }
        void Reviews(ImmutableArray<ToolboxCheckReview> values)
        {
            Require(!values.IsDefault);
            foreach (var r in values)
            {
                Require(Enum.IsDefined(r.Outcome) && !r.EvidenceIds.IsDefault);
                Text(r.ResolutionFingerprint); Text(r.Actor); Text(r.Reason);
                Require(r.EvidenceIds.All(known.Evidence.Contains), "missing_reference");
                if (r.GeometryFingerprints is { } geometry) Require(geometry.Keys.All(known.Parcels.Contains), "missing_reference");
                if (r.FindingId is not null) Require(findingIds.Contains(r.FindingId), "missing_reference");
            }
        }
    }

    internal static void Lineage(ImmutableArray<ToolboxAppliedLineage> values, ImmutableArray<ToolboxParcelModel> models,
        IEnumerable<Review.Canonical.CanonicalReviewContract>? candidates = null)
    {
        var parcels = models.Skip(1).SelectMany(m => m.Parcels).Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pairs = models.SelectMany(m => m.Parcels).Select(p => (p.SourceKey.ToUpperInvariant(), p.Id.ToUpperInvariant())).ToHashSet();
        var evidence = models.SelectMany(m => m.Source.Traces).Concat((candidates ?? []).SelectMany(c => c.Traces))
            .Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var operations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in values)
        {
            var p = l.Proposal; Require(p.Case == models[0].Case, "foreign_case");
            Require(Enum.IsDefined(p.Kind) && !p.ParentIds.IsDefaultOrEmpty && !p.ChildKeys.IsDefaultOrEmpty && !l.ChildIds.IsDefault);
            Text(p.OperationId); Text(p.Actor); Text(p.Reason); Text(p.EvidenceId); Text(p.ParentRevision); Text(p.ResultRevision);
            Require(operations.Add(p.OperationId), "duplicate_identity");
            Require(evidence.Contains(p.EvidenceId), "missing_reference");
            Require(p.ParentRevision != p.ResultRevision && p.ChildKeys.Length == l.ChildIds.Length);
            Require(p.ParentIds.All(parcels.Contains), "missing_reference");
            Require(p.ParentIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() == p.ParentIds.Length, "duplicate_identity");
            Require(!l.ChildIds.Any(id => p.ParentIds.Contains(id, StringComparer.OrdinalIgnoreCase)), "inconsistent_state");
            Require(p.ChildKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count() == p.ChildKeys.Length
                && l.ChildIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() == l.ChildIds.Length);
            Require(p.Kind == ToolboxLineageKind.Split ? p.ParentIds.Length == 1 && p.ChildKeys.Length > 1 : p.ParentIds.Length > 1 && p.ChildKeys.Length == 1);
            for (var i = 0; i < l.ChildIds.Length; i++) Require(pairs.Contains((p.ChildKeys[i].ToUpperInvariant(), l.ChildIds[i].ToUpperInvariant())), "missing_reference");
            Require(p.ChildInclusions.Count == p.ChildKeys.Length && p.ChildKeys.All(p.ChildInclusions.ContainsKey)
                && p.ChildInclusions.Values.All(x => Enum.IsDefined(x) && x != ToolboxInclusion.Unresolved));
        }
    }

    internal static void Applicability(ToolboxParcelModel model, ToolboxExaminationRegister register,
        IEnumerable<Review.Canonical.CanonicalReviewContract> candidates)
    {
        var models = History(model);
        var records = register.Findings.Select(f => f.Id).Concat(register.Decisions.SelectMany(d => d.Findings).Select(f => f.Id))
            .Concat(models.SelectMany(ToolboxLineageValidator.ManualEntityIds)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Validate the existing immutable correction identifier, never a proof against new inputs.
        foreach (var review in register.HumanReviews) records.Add(ToolboxLineageValidator.CorrectionId(review));
        var keys = models.SelectMany(m => m.Parcels).Select(p => p.SourceKey)
            .Concat(candidates.SelectMany(c => c.Parcels).Select(p => p.Key)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var m in models)
            foreach (var a in m.Relations?.Applicability ?? [])
            {
                Require(records.Contains(a.RecordId), "missing_reference");
                Require(a.TargetParcelKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count() == a.TargetParcelKeys.Length, "duplicate_identity");
                Require(a.TargetParcelKeys.All(keys.Contains), "missing_reference");
                Require(a.Disposition is not (ToolboxApplicabilityDisposition.Applicable or ToolboxApplicabilityDisposition.Retained)
                    || a.TargetParcelKeys.Length > 0);
            }
    }
}
