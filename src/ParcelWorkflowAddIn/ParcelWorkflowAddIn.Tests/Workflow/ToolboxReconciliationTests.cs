using System.Collections.Immutable;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Review.Canonical;
using ParcelWorkflowAddIn.Workflow.Toolbox;
using ParcelWorkflowAddIn.Workflow.Toolbox.Model;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static partial class ToolboxReconciliationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-09T00:00:00Z");
    private const string Geometry = "synthetic-only-planar-v1";
    private static ToolboxCaseContext Context(bool permission = true) => new("synthetic", "case-a", "examiner",
        new SelectedInnolaTransaction("task", "transaction-a", "000000101", "Compute", "Compute", Now),
        Path.Combine(Path.GetTempPath(), "ToolboxB"), Path.Combine(Path.GetTempPath(), "ToolboxB", "case-a"), allowLocalGeometryEdit: permission);
    private static ToolboxDispositionAuthority Authority => new("examiner", "test-v1", true);
    private static ToolboxReviewPolicy Policy()
    {
        var json = ToolboxPolicyTests.Fixture();
        foreach (var stage in new[] { "Compute", "Compare" }) json["items"]![0]!["stages"]![stage]!["checks"]![0]!["scope"] = "parcel";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(json);
        return ToolboxReviewPolicyLoader.Load(bytes, new("synthetic-toolbox-policy", "test-v1", Convert.ToHexString(SHA256.HashData(bytes))));
    }
    private static ImmutableDictionary<string, string> Hashes(string value = "source-hash-v1") =>
        ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase).Add("plan", value);
    private static ToolboxAdjacencyReview Adjacency(ToolboxParcelModel model) => new(model.Case, model.Parcels.Select(x => x.SourceKey).ToImmutableArray(),
        true, "ev", "examiner", "Explicit synthetic adjacency inventory / no shared edge where declared", ToolboxReconciliation.PolicyVersion);
    private static ToolboxPolicyScope[] Scopes(ToolboxParcelModel model) =>
        new[] { new ToolboxPolicyScope(model.Case.CaseId, ToolboxScopeKind.Case, model.Case.CaseId, "review-revision", "caller-input") }
        .Concat(model.Parcels.Select(x => new ToolboxPolicyScope(model.Case.CaseId, ToolboxScopeKind.Parcel, x.Id, "review-revision", "caller-input")))
        .Concat(model.Segments.Select(x => new ToolboxPolicyScope(model.Case.CaseId, ToolboxScopeKind.Boundary, x.Id, "review-revision", "caller-input")))
        .Concat((model.Relations?.Boundaries ?? []).Select(x => new ToolboxPolicyScope(model.Case.CaseId, ToolboxScopeKind.Boundary, x.Id, "review-revision", "caller-input")))
        .Concat(model.Source.Sources.Select(x => new ToolboxPolicyScope(model.Case.CaseId, ToolboxScopeKind.Document, x.Id, "review-revision", "caller-input"))).ToArray();

    private static ToolboxDependencyInventory Graph(ToolboxParcelModel model, ToolboxReviewPolicy policy,
        ImmutableArray<ToolboxPhysicalBoundary> boundaries = default)
    {
        var nodes = new List<ToolboxDependencyNode>(); var edges = new List<ToolboxDependencyEdge>();
        void Node(string id, ToolboxDependencyKind kind, string reference) => nodes.Add(new(id, kind, reference));
        void Edge(string from, string to) => edges.Add(new(from, to));
        Node("policy", ToolboxDependencyKind.Policy, "review-policy"); Node("case", ToolboxDependencyKind.Case, model.Case.CaseId);
        Node("global-facts", ToolboxDependencyKind.GlobalFacts, "global-facts");
        var facts = new List<string>();
        foreach (var (name, kind) in new[] { ("participants", ToolboxDependencyKind.Participant), ("identifier_pairs", ToolboxDependencyKind.IdentifierPair) })
            if (model.Source.GlobalData.TryGetProperty(name, out var values))
                foreach (var fact in values.EnumerateObject()) { var id = name + ":" + fact.Name; Node(id, kind, fact.Name); facts.Add(id); }
        Node("artifact", ToolboxDependencyKind.Artifact, "declared-report"); Edge("case", "artifact");
        foreach (var source in model.Source.Sources) Node("source:" + source.Id, ToolboxDependencyKind.Source, source.Id);
        foreach (var trace in model.Source.Traces)
        { Node("evidence:" + trace.Id, ToolboxDependencyKind.Evidence, trace.Id); Edge("source:" + trace.SourceId, "evidence:" + trace.Id); Edge("evidence:" + trace.Id, "case"); }
        foreach (var parcel in model.Parcels)
        {
            Node("parcel:" + parcel.SourceKey, ToolboxDependencyKind.Parcel, parcel.Id);
            Node("decision:" + parcel.SourceKey, ToolboxDependencyKind.Decision, parcel.Id);
            Edge("parcel:" + parcel.SourceKey, "decision:" + parcel.SourceKey); Edge("decision:" + parcel.SourceKey, "case");
        }
        foreach (var entity in model.Rows.Concat(model.Segments))
        {
            var isPoint = model.Rows.Contains(entity); var name = (isPoint ? "point:" : "segment:") + entity.SourceKey;
            Node(name, isPoint ? ToolboxDependencyKind.Point : ToolboxDependencyKind.Segment, entity.Id);
            Edge(name, "parcel:" + model.Parcels.Single(x => x.Id == entity.ParcelId).SourceKey); Edge("evidence:ev", name);
        }
        foreach (var b in boundaries.IsDefault ? [] : boundaries)
        {
            Node("boundary:" + b.Id, ToolboxDependencyKind.Boundary, b.Id);
            foreach (var evidence in b.EvidenceIds) Edge("evidence:" + evidence, "boundary:" + b.Id);
            foreach (var m in b.Members) { Edge("boundary:" + b.Id, "parcel:" + model.Parcels.Single(x => x.Id == m.ParcelId).SourceKey);
                Edge("segment:" + model.Segments.Single(x => x.Id == m.SegmentId).SourceKey, "boundary:" + b.Id); }
            foreach (var id in b.PointIds.IsDefault ? [] : b.PointIds) Edge("point:" + model.Rows.Single(x => x.Id == id).SourceKey, "boundary:" + b.Id);
        }
        foreach (var link in model.Source.Links) Edge("evidence:" + link.EvidenceId, "decision:" + link.ParcelKey);
        foreach (var r in ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute, Scopes(model)).Where(x => x.EffectiveRequirement != ToolboxReviewRequirement.NotApplicable))
        {
            var id = "check:" + r.ResolutionFingerprint;
            nodes.Add(new(id, ToolboxDependencyKind.Check, r.ResolutionFingerprint,
                new(ToolboxPreset.Compute, r.ItemId, r.CheckId, r.ScopeKind, r.ScopeId!, "review-revision", "caller-input", GeometryCheck: r.ItemId == "M01"))); Edge("policy", id);
            if (r.ScopeKind == ToolboxScopeKind.Parcel)
            { var key = model.Parcels.Single(x => x.Id == r.ScopeId).SourceKey; Edge("parcel:" + key, id); Edge(id, "decision:" + key); }
            else { Edge("evidence:ev", id); Edge("global-facts", id); foreach (var fact in facts) Edge(fact, id); Edge(id, "case"); }
        }
        return new(model.Case, "synthetic-directed-v1", true, nodes.ToImmutableArray(), edges.ToImmutableArray());
    }
    private static ToolboxReconciliationSnapshot Start(int count = 1, ToolboxReviewPolicy? policy = null)
    {
        policy ??= Policy(); var model = ToolboxParcelModel.Adapt(Context(), CanonicalReviewContract.Read(CanonicalReviewContractTests.Sample(count)), newImport: true);
        var register = new ToolboxExaminationRegister().RecordHumanReview(model, new("ev", "raw", "manual-reviewed", "manual-cogo",
            ToolboxPresence.Present, ToolboxAccuracy.Corrected, ToolboxPreset.Compute, "examiner", Now, "Human validation", "review-revision"), Authority);
        return ToolboxReconciliation.Start(model, register, "base-revision", policy.Pin, Hashes(), [], Graph(model, policy), Adjacency(model));
    }
    private static ToolboxRerunBatch Batch(ToolboxReconciliationSnapshot state, CanonicalReviewContract source,
        ImmutableDictionary<string, string>? hashes = null) => new(state.Model.Case, "batch", "run", "tool-v1", "provider-v1",
            hashes ?? state.Model.Relations!.SourceHashes, state.Revision, state.Fingerprint, ToolboxReconciliation.CandidateDigest(source),
            ToolboxReconciliation.PolicyVersion, state.Model.Relations!.Policy, ToolboxRerunStatus.Succeeded, source);
    private static ToolboxReconciliationProposal Proposal(ToolboxReconciliationSnapshot state, ToolboxReviewPolicy policy,
        CanonicalReviewContract incoming, CanonicalReviewContract? output = null,
        ImmutableDictionary<string, string>? hashes = null, bool explicitIdentity = false)
    {
        output ??= incoming; var choices = new List<ToolboxCandidateDisposition>();
        foreach (var pair in state.Model.IdentityMap.Ids)
        {
            var kind = pair.Key.Split(':')[0]; var key = pair.Key[(kind.Length + 1)..];
            var exists = kind switch { "parcel" => incoming.Parcels.Any(x => x.Key == key), "row" => incoming.Rows.Any(x => x.Key == key), _ => incoming.Segments.Any(x => x.Key == key) };
            choices.Add(new(kind, pair.Value, exists ? key : null, key, ToolboxValueChoice.Retain, "examiner", "Keep reviewed values", "ev", explicitIdentity));
        }
        foreach (var (kind, key) in incoming.Parcels.Select(x => ("parcel", x.Key)).Concat(incoming.Rows.Select(x => ("row", x.Key))).Concat(incoming.Segments.Select(x => ("segment", x.Key))))
            if (!state.Model.IdentityMap.Ids.ContainsKey(kind + ":" + key)) choices.Add(new(kind, null, key, key, ToolboxValueChoice.New, "examiner", "New candidate", "ev", true));
        choices.Add(new("global", null, null, null, ToolboxValueChoice.Retain, "examiner", "Retain document data", "ev", true));
        return ToolboxReconciliation.Seal(new("operation", "result-revision", Batch(state, incoming, hashes), output, choices.ToImmutableArray(), [], [], [], state.Model.Relations!.Dependencies, "",
            Adjacency(state.Model) with { ParcelKeys = output.Parcels.Select(x => x.Key).ToImmutableArray() }));
    }
    private static ToolboxCheckReview[] Reviews(ToolboxReconciliationSnapshot state, ToolboxReviewPolicy policy) =>
        ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute, Scopes(state.Model)).Where(x => x.EffectiveRequirement != ToolboxReviewRequirement.NotApplicable)
        .Select(r => new ToolboxCheckReview(r.ResolutionFingerprint, ToolboxCheckOutcome.Passed, "Reviewed", ["ev"], "examiner", Now,
            GeometryCheck: r.ItemId == "M01", EvidenceFingerprint: ToolboxReadinessEvaluator.EvidenceFingerprint(state.Model, state.Register, ["ev"]),
            GeometryFingerprints: r.ItemId == "M01" ? ImmutableDictionary<string, string>.Empty.Add(r.ScopeId!, state.Model.ParcelFingerprint(r.ScopeId!, Geometry)) : null,
            CanonicalInputFingerprint: ToolboxReadinessEvaluator.CanonicalInputFingerprint(state.Model, r.ScopeKind, r.ScopeId, r.ItemId == "M01"),
            DependencyFingerprint: state.Model.Relations!.Dependencies.ConsumerFingerprint(state.Model, ToolboxDependencyKind.Check, r.ResolutionFingerprint))).ToArray();
    private static ToolboxReadinessSnapshot Evaluate(ToolboxReconciliationSnapshot state, ToolboxReviewPolicy policy, ToolboxCheckReview[]? reviews = null) =>
        ToolboxReadinessEvaluator.Evaluate(state.Model, Context(), policy, Scopes(state.Model), reviews ?? Reviews(state, policy), state.Register, Authority, Geometry, Now, true);
    private static void Reject(Action action) => CanonicalReviewContractTests.Reject(action);
    public static void SeparateReconciliationContractExists()
    {
        TestAssert.True(typeof(ToolboxParcelModel).Assembly.GetType(
            "ParcelWorkflowAddIn.Workflow.Toolbox.Model.ToolboxReconciliation") is not null,
            "D07 needs an explicit reconciliation path, not a relaxed ReAdapt guard.");
    }

    public static void EnvelopeAndAtomicFailures()
    {
        var policy = Policy(); var state = Start(policy: policy); var batch = Batch(state, state.Model.Source); var before = state.Fingerprint;
        foreach (var invalid in new[] { batch with { BaseRevision = "old" }, batch with { BaseFingerprint = "old" }, batch with { CandidateDigest = "bad" },
            batch with { Case = batch.Case with { CaseId = "foreign" } }, batch with { Status = ToolboxRerunStatus.Partial }, batch with { Status = ToolboxRerunStatus.Failed }, batch with { Status = ToolboxRerunStatus.Canceled },
            batch with { SourceHashes = Hashes("") }, batch with { ProviderVersion = "" }, batch with { ReconciliationPolicy = "unknown" } }) Reject(() => ToolboxReconciliation.Inspect(state, invalid));
        TestAssert.Equal(before, state.Fingerprint, "Failures preserve complete base.");
    }
    public static void VariableCountsRenameAndReplay()
    {
        foreach (var count in new[] { 1, 2, 3, 5, 10, 50, 100 })
        {
            var policy = Policy(); var state = Start(count, policy); var json = state.Model.Source.ToJson(); json["parcel_groups"]![0]!["display_label"] = "Renamed";
            var incoming = CanonicalReviewContract.Read(json);
            TestAssert.Equal(count * 3, ToolboxReconciliation.Inspect(state, Batch(state, incoming)).Count(x => x.State == ToolboxCandidateState.Matched), "Every entity matched by proven key.");
            var proposal = Proposal(state, policy, incoming); var allocations = 0;
            var result = ToolboxReconciliation.Accept(state, proposal, Context(), policy, Authority, () => { allocations++; return "unused"; });
            TestAssert.Equal(0, allocations, "Carried identities need no new allocation.");
            TestAssert.Equal(state.Model.Parcels[0].Id, result.Model.Parcels[0].Id, "Same stable parcel.");
            TestAssert.True(ReferenceEquals(result, ToolboxReconciliation.Accept(result, proposal, Context(), policy, Authority)), "Replay is a no-op.");
            Reject(() => ToolboxReconciliation.Accept(result, ToolboxReconciliation.Seal(proposal with { ResultRevision = "other" }), Context(), policy, Authority));
            Reject(() => ToolboxReconciliation.Accept(result, ToolboxReconciliation.Seal(proposal with { OperationId = "concurrent" }), Context(), policy, Authority));
            TestAssert.Equal(state.Register.HumanReviews, result.Register.HumanReviews, "No duplicate manual history.");
        }
    }
    public static void ManualConflictEmptyAndChangedSource()
    {
        var policy = Policy(); var state = Start(policy: policy); var json = state.Model.Source.ToJson(); json["rows"]![0]!["easting"] = "999";
        var candidate = CanonicalReviewContract.Read(json);
        TestAssert.True(ToolboxReconciliation.Inspect(state, Batch(state, candidate)).Any(x => x.State == ToolboxCandidateState.Conflicting), "Manual differs from suggestion.");
        Reject(() => ToolboxReconciliation.Accept(state, Proposal(state, policy, candidate), Context(), policy, Authority));
        var keep = ToolboxReconciliation.Accept(state, Proposal(state, policy, candidate, state.Model.Source), Context(), policy, Authority);
        TestAssert.Equal("10", keep.Model.Rows[0].Evidence.GetProperty("easting").GetString(), "Retain reviewed geometry.");
        TestAssert.Equal("999", keep.Reruns[0].Candidate.Rows[0].Data.GetProperty("easting").GetString(), "Suggestion retained separately.");
        var emptyJson = state.Model.Source.ToJson(); emptyJson["parcel_groups"] = new JsonArray(); emptyJson["rows"] = new JsonArray(); emptyJson["segments"] = new JsonArray();
        var recovered = ToolboxReconciliation.Accept(state, Proposal(state, policy, CanonicalReviewContract.Read(emptyJson), state.Model.Source), Context(), policy, Authority);
        TestAssert.Equal(1, recovered.Model.Parcels.Length, "Empty rerun never deletes manual parcel.");
        var oldReviews = Reviews(state, policy);
        Reject(() => ToolboxReconciliation.Accept(state, Proposal(state, policy, state.Model.Source, hashes: Hashes("new-source")), Context(), policy, Authority));
        var changed = ToolboxReconciliation.Accept(state, Proposal(state, policy, state.Model.Source, hashes: Hashes("new-source"), explicitIdentity: true), Context(), policy, Authority);
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(changed, policy, oldReviews).Status, "Retained manual review stale after source change.");
    }
    public static void PermissionDigestAndInventories()
    {
        var policy = Policy(); var state = Start(policy: policy); var p = Proposal(state, policy, state.Model.Source);
        Reject(() => ToolboxReconciliation.Accept(state, p, Context(false), policy, Authority));
        Reject(() => ToolboxReconciliation.Accept(state, p, Context().SelectPreset(ToolboxPreset.Compare), policy, Authority));
        Reject(() => ToolboxReconciliation.Accept(state, p, Context(), policy, Authority with { Authorized = false }));
        Reject(() => ToolboxReconciliation.Accept(state, p with { Digest = "forged" }, Context(), policy, Authority));
        Reject(() => ToolboxReconciliation.Accept(state, ToolboxReconciliation.Seal(p with { Dispositions = p.Dispositions.RemoveAt(0) }), Context(), policy, Authority));
        Reject(() => ToolboxReconciliation.Accept(state, ToolboxReconciliation.Seal(p with { Dispositions = p.Dispositions.Add(p.Dispositions[0]) }), Context(), policy, Authority));
        Reject(() => ToolboxReconciliation.Accept(state, ToolboxReconciliation.Seal(p with { Dispositions = p.Dispositions.SetItem(0, p.Dispositions[0] with { Choice = ToolboxValueChoice.Unresolved }) }), Context(), policy, Authority));
        TestAssert.False(Context().RemoteActionAccess.Allowed, "No remote authorization.");
        Reject(() => state.Model.ReAdapt(Context(), CanonicalReviewContract.Read(CanonicalReviewContractTests.Sample(2))));
    }
    public static void ActualGeometryStalesOnlyConsumers()
    {
        var policy = Policy(); var state = Start(3, policy); var old = Evaluate(state, policy); var reviews = Reviews(state, policy);
        TestAssert.Equal(ToolboxReadinessStatus.Ready, old.Status, "Synthetic current proofs.");
        var json = state.Model.Source.ToJson(); json["rows"]![0]!["easting"] = "changed";
        var edited = state with { Model = state.Model.ReAdapt(Context(), CanonicalReviewContract.Read(json)) };
        var next = Evaluate(edited, policy, reviews); var id = state.Model.Parcels[0].Id;
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, next.Status, "Reused caller tokens cannot hide geometry change.");
        TestAssert.False(old.ParcelDispositions.Single(x => x.ParcelId == id).IsCurrent(next.ParcelDispositions.Single(x => x.ParcelId == id)), "Changed parcel stale.");
        foreach (var p in state.Model.Parcels.Skip(1)) TestAssert.True(old.ParcelDispositions.Single(x => x.ParcelId == p.Id).IsCurrent(next.ParcelDispositions.Single(x => x.ParcelId == p.Id)), "Independent parcel current.");
        var impact = ToolboxDependencyInventory.Impact(state.Model, edited.Model);
        TestAssert.True(impact.AffectedIds.Contains("artifact"), "Declared artifact impacted, not executed.");
        TestAssert.False(impact.AffectedIds.Contains("parcel:group-3"), "Independent parcel unaffected.");
    }

    private static ToolboxPhysicalBoundary Boundary(ToolboxParcelModel model, int left = 0, int right = 1) =>
        new("physical-edge", model.Case, "boundary-revision", "reviewed-shape-v1", Geometry, true, ["ev"],
            [new(model.Parcels[left].Id, model.Segments[left].Id, 1, "forward", "boundary"),
                new(model.Parcels[right].Id, model.Segments[right].Id, 1, "reverse", "boundary")],
            ToolboxBoundaryConsistency.Confirmed, "examiner", "Explicit reviewed adjacency", ToolboxReconciliation.PolicyVersion, [model.Rows[left].Id, model.Rows[right].Id]);
    private static ToolboxReconciliationSnapshot Shared(ToolboxReviewPolicy policy)
    {
        var json = CanonicalReviewContractTests.Sample(3);
        json["segment_memberships"] = new JsonArray(Enumerable.Range(1, 3).Select(i => (JsonNode)new JsonObject
        { ["parcel_group_id"] = "group-" + i, ["entity_key"] = "segment-" + i, ["sequence"] = 1,
            ["direction"] = i == 1 ? "forward" : "reverse", ["role"] = "boundary" }).ToArray());
        var model = ToolboxParcelModel.Adapt(Context(), CanonicalReviewContract.Read(json), newImport: true);
        var register = new ToolboxExaminationRegister().RecordHumanReview(model, new("ev", "raw", "reviewed", "manual", ToolboxPresence.Present,
            ToolboxAccuracy.Confirmed, ToolboxPreset.Compute, "examiner", Now, "Reviewed", "review-revision"), Authority);
        var boundary = Boundary(model);
        return ToolboxReconciliation.Start(model, register, "base-revision", policy.Pin, Hashes(), [boundary], Graph(model, policy, [boundary]), Adjacency(model));
    }
    public static void SharedBoundaryChangesBindReadiness()
    {
        var policy = Policy(); var state = Shared(policy); var old = Evaluate(state, policy); var reviews = Reviews(state, policy);
        TestAssert.Equal(ToolboxReadinessStatus.Ready, old.Status, "Explicit synthetic shared consistency.");
        var b = state.Model.Relations!.Boundaries[0] with { GeometryDigest = "changed-shape", Revision = "boundary-v2" };
        var p = ToolboxReconciliation.Seal(Proposal(state, policy, state.Model.Source) with { Boundaries = [b] });
        var changed = ToolboxReconciliation.Accept(state, p, Context(), policy, Authority);
        var next = Evaluate(changed, policy, reviews);
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, next.Status, "Shared changed input blocks old proofs.");
        foreach (var id in state.Model.Parcels.Take(2).Select(x => x.Id))
            TestAssert.False(old.ParcelDispositions.Single(x => x.ParcelId == id).IsCurrent(next.ParcelDispositions.Single(x => x.ParcelId == id)), "Both consumers stale.");
        var independent = state.Model.Parcels[2].Id;
        TestAssert.True(old.ParcelDispositions.Single(x => x.ParcelId == independent).IsCurrent(next.ParcelDispositions.Single(x => x.ParcelId == independent)), "Independent C remains current.");
        TestAssert.True(ToolboxDependencyInventory.Impact(state.Model, changed.Model).Paths.Any(x => x.Path.Contains("boundary:physical-edge") && x.AffectedId == "artifact"), "Shared path reaches declared artifact.");
        TestAssert.Equal(3, changed.Model.Segments.Length, "Never erase independent source segments.");
        var root = state.Model.Source.ToJson(); root["rows"]![0]!["easting"] = "source-point-change";
        var pointChanged = state with { Model = state.Model.ReAdapt(Context(), CanonicalReviewContract.Read(root)) };
        var pointNext = Evaluate(pointChanged, policy, reviews); var sharedNeighbor = state.Model.Parcels[1].Id;
        TestAssert.False(old.ParcelDispositions.Single(x => x.ParcelId == sharedNeighbor).IsCurrent(pointNext.ParcelDispositions.Single(x => x.ParcelId == sharedNeighbor)),
            "A declared shared point invalidates B even when boundary token/revision are reused.");
    }
    public static void BoundaryConflictsAndUnknownCoverage()
    {
        var policy = Policy(); var state = Shared(policy); var b = state.Model.Relations!.Boundaries[0];
        foreach (var invalid in new[] { b with { Case = b.Case with { CaseId = "foreign" } }, b with { EvidenceIds = ["unknown"] },
            b with { Members = b.Members.SetItem(0, b.Members[0] with { SegmentId = "orphan" }) }, b with { Id = state.Model.Segments[0].Id } })
            Reject(() => ToolboxBoundaryValidator.Validate(state.Model, [invalid], ToolboxReconciliation.PolicyVersion));
        foreach (var unresolved in new[] { b with { SemanticsVerified = false }, b with { Consistency = ToolboxBoundaryConsistency.Unresolved },
            b with { Consistency = ToolboxBoundaryConsistency.NotApplicable }, b with { Members = b.Members.Add(new(state.Model.Parcels[2].Id, state.Model.Segments[2].Id, 1, "reverse", "boundary")) } })
        {
            TestAssert.True(ToolboxBoundaryValidator.Validate(state.Model, [unresolved], ToolboxReconciliation.PolicyVersion).Length > 0, "Typed scoped conflict.");
            var result = ToolboxReconciliation.Accept(state, ToolboxReconciliation.Seal(Proposal(state, policy, state.Model.Source) with { Boundaries = [unresolved], Dependencies = Graph(state.Model, policy, [unresolved]) }), Context(), policy, Authority);
            TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(result, policy).Status, "Conflict cannot be forwarded as valid geometry.");
        }
        var graph = state.Model.Relations.Dependencies;
        Reject(() => ToolboxReconciliation.Accept(state, ToolboxReconciliation.Seal(Proposal(state, policy, state.Model.Source) with { Boundaries = [b], Dependencies = graph with { Complete = false } }), Context(), policy, Authority));
        Reject(() => (graph with { Edges = graph.Edges.Add(new("unknown", "case")) }).Validate(state.Model));
        Reject(() => (graph with { Edges = graph.Edges.Add(new("artifact", "case")) }).Validate(state.Model));
    }
    public static void BoundaryReassignmentUsesOldNewUnion()
    {
        var policy = Policy(); var state = Shared(policy); var a = state.Model.Parcels[0].Id;
        state = state with { Model = state.Model.SetInclusion(new(a, ToolboxInclusion.Excluded, "examiner", Now, "Scope exclusion", "ev", "test-v1"), "test-v1", true) };
        var changed = Boundary(state.Model, 1, 2) with { Members = [new(state.Model.Parcels[1].Id, state.Model.Segments[1].Id, 1, "reverse", "boundary"),
            new(state.Model.Parcels[2].Id, state.Model.Segments[2].Id, 1, "reverse", "boundary")] };
        var p = ToolboxReconciliation.Seal(Proposal(state, policy, state.Model.Source) with { Boundaries = [changed], Dependencies = Graph(state.Model, policy, [changed]) });
        var result = ToolboxReconciliation.Accept(state, p, Context(), policy, Authority);
        var impact = ToolboxDependencyInventory.Impact(state.Model, result.Model);
        foreach (var key in new[] { "parcel:group-1", "parcel:group-2", "parcel:group-3" }) TestAssert.True(impact.AffectedIds.Contains(key), "Old/new union includes excluded former neighbor.");
    }

    private static ToolboxReconciliationProposal LineageProposal(ToolboxReconciliationSnapshot state, ToolboxReviewPolicy policy, int children,
        ToolboxApplicabilityDisposition findingDisposition = ToolboxApplicabilityDisposition.Unresolved)
    {
        var json = CanonicalReviewContractTests.Sample(children);
        for (var i = 0; i < children; i++)
        {
            var key = "child-" + (i + 1); json["parcel_groups"]![i]!["parcel_group_id"] = key;
            json["rows"]![i]!["parcel_group_id"] = key; json["rows"]![i]!["row_id"] = "child-row-" + (i + 1);
            json["segments"]![i]!["parcel_group_id"] = key; json["segments"]![i]!["segment_id"] = "child-segment-" + (i + 1);
        }
        var source = CanonicalReviewContract.Read(json); var p = Proposal(state, policy, source);
        p = p with { Dispositions = p.Dispositions.Select(c => c.OldId is null ? c : c with { OutputKey = null, Choice = ToolboxValueChoice.Retire }).ToImmutableArray() };
        var keys = source.Parcels.Select(x => x.Key).ToImmutableArray();
        var l = new ToolboxParcelLineage("lineage-op", state.Model.Case, children == 1 ? ToolboxLineageKind.Merge : ToolboxLineageKind.Split,
            state.Revision, p.ResultRevision, state.Model.Parcels.Select(x => x.Id).ToImmutableArray(), keys, "examiner", "Reviewed source ancestry", "ev",
            keys.ToImmutableDictionary(x => x, _ => ToolboxInclusion.Included));
        var links = state.Register.Findings.Select(x => new ToolboxHistoryApplicability(x.Id, state.Revision, [keys[0]], findingDisposition,
            "examiner", "Explicit applicability, not copied to every child", "ev"))
            .Concat(state.Register.HumanReviews.Select(h => new ToolboxHistoryApplicability(ToolboxLineageValidator.CorrectionId(h), state.Revision, [keys[0]],
                ToolboxApplicabilityDisposition.Retained, "examiner", "Retain manual history for review", "ev")))
            .Concat(ToolboxLineageValidator.ManualEntityIds(state.Model).Select(id => new ToolboxHistoryApplicability(id, state.Revision, [keys[0]],
                ToolboxApplicabilityDisposition.Retained, "examiner", "Retain original/manual entity history", "ev"))).ToImmutableArray();
        var projected = ToolboxParcelModel.Adapt(Context(), source, newImport: true); var graph = Graph(projected, policy);
        var aliases = projected.IdentityMap.Ids.ToDictionary(x => x.Value, x => "@" + x.Key);
        graph = graph with { Nodes = graph.Nodes.Select(n => n with {
            ReferenceId = aliases.TryGetValue(n.ReferenceId, out var alias) ? alias : n.ReferenceId,
            CheckBinding = n.CheckBinding is { } b && aliases.TryGetValue(b.ScopeId, out var scopeAlias) ? b with { ScopeId = scopeAlias } : n.CheckBinding }).ToImmutableArray() };
        return ToolboxReconciliation.Seal(p with { Lineage = [l], Applicability = links, Dependencies = graph });
    }
    private static ToolboxReconciliationSnapshot WithFinding(ToolboxReconciliationSnapshot state, bool resolved = false)
    {
        var finding = new ToolboxFinding("retained-finding", ToolboxFindingOrigin.SourceDefect, [state.Model.Parcels[0].Id], "plan", 1, "map", false, true,
            [new(ToolboxPreset.Compute, "examiner", Now, resolved ? "resolved" : "recorded", "Source defect", "ev", "caller-input", "test-v1", "test-v1")],
            "M01", "source-defect", "error");
        return state with { Register = state.Register.RecordFinding(state.Model, finding) };
    }
    public static void SplitMergePreserveHistoryAndRequireFreshReview()
    {
        foreach (var (parents, children) in new[] { (1, 2), (1, 5), (3, 1) })
        {
            var policy = Policy(); var state = WithFinding(Start(parents, policy)); var p = LineageProposal(state, policy, children);
            var calls = 0; var result = ToolboxReconciliation.Accept(state, p, Context(), policy, Authority, () => "fresh-id-" + ++calls);
            TestAssert.Equal(children * 3, calls, "Allocate fresh parcel/row/segment identities once.");
            TestAssert.Equal(children, result.Model.Parcels.Length, "All declared children active.");
            TestAssert.True(!result.Model.IdentityMap.Ids.Values.Intersect(state.Model.IdentityMap.Ids.Values).Any(), "No inherited identities/approvals.");
            TestAssert.Equal(state.Register.Findings[0], result.Register.Findings[0], "Original scope/history retained.");
            TestAssert.Equal(state.Register.HumanReviews, result.Register.HumanReviews, "Manual provenance untouched.");
            TestAssert.Equal(state.Model, result.Model.Relations!.Archives[0], "Historical parent snapshot retained.");
            TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(result, policy).Status, "Unresolved applicability blocks even fresh checks.");
            TestAssert.True(ReferenceEquals(result, ToolboxReconciliation.Accept(result, p, Context(), policy, Authority, () => "duplicate")), "Lineage replay no duplicate IDs/history.");
            var f = result.Register.Findings[0]; var appended = f with { History = f.History.Add(f.History[^1] with { Status = "retained", Reason = "Historical evidence retained" }) };
            TestAssert.Equal(2, result.Register.RecordFinding(result.Model, appended).Findings[0].History.Length, "Historical same-case evidence supports append-only events.");
            Reject(() => result.Register.RecordFinding(result.Model, appended with { ParcelIds = [result.Model.Parcels[0].Id] }));
        }
    }
    public static void InvalidLineageAndApplicabilityFailAtomically()
    {
        var policy = Policy(); var state = WithFinding(Start(policy: policy)); var p = LineageProposal(state, policy, 2); var l = p.Lineage[0]; var before = state.Fingerprint;
        foreach (var bad in new[] { l with { ParentIds = ["foreign"] }, l with { ParentIds = [l.ParentIds[0], l.ParentIds[0]] },
            l with { ChildKeys = [l.ChildKeys[0], l.ChildKeys[0]] }, l with { ChildKeys = ["group-1", l.ChildKeys[1]] },
            l with { ParentRevision = "stale" }, l with { ResultRevision = state.Revision }, l with { EvidenceId = "unknown" },
            l with { Kind = ToolboxLineageKind.Merge }, l with { ChildInclusions = l.ChildInclusions.SetItem(l.ChildKeys[0], ToolboxInclusion.Unresolved) } })
            Reject(() => ToolboxReconciliation.Accept(state, ToolboxReconciliation.Seal(p with { Lineage = [bad] }), Context(), policy, Authority));
        Reject(() => ToolboxReconciliation.Accept(state, ToolboxReconciliation.Seal(p with { Applicability = [] }), Context(), policy, Authority));
        Reject(() => ToolboxReconciliation.Accept(state, ToolboxReconciliation.Seal(p with { Applicability = p.Applicability.Add(p.Applicability[0]) }), Context(), policy, Authority));
        Reject(() => ToolboxReconciliation.Accept(state, p, Context(), policy, Authority, () => state.Model.Parcels[0].Id));
        TestAssert.Equal(before, state.Fingerprint, "Invalid lineage never mutates base.");
    }
    public static void NewCandidateCannotHideBehindSelection()
    {
        var policy = Policy(); var state = Start(policy: policy); var source = CanonicalReviewContract.Read(CanonicalReviewContractTests.Sample(2));
        var p = Proposal(state, policy, source); var projected = ToolboxParcelModel.Adapt(Context(), source, newImport: true);
        var aliases = projected.IdentityMap.Ids.ToDictionary(x => x.Value, x => x.Key == "parcel:group-1" ? state.Model.Parcels[0].Id
            : state.Model.IdentityMap.Ids.TryGetValue(x.Key, out var old) ? old : "@" + x.Key);
        var graph = Graph(projected, policy);
        graph = graph with { Nodes = graph.Nodes.Select(n => n with { ReferenceId = aliases.TryGetValue(n.ReferenceId, out var alias) ? alias : n.ReferenceId,
            CheckBinding = n.CheckBinding is { } b && aliases.TryGetValue(b.ScopeId, out var scopeAlias) ? b with { ScopeId = scopeAlias } : n.CheckBinding }).ToImmutableArray() };
        var result = ToolboxReconciliation.Accept(state, ToolboxReconciliation.Seal(p with { Dependencies = graph }), Context(), policy, Authority);
        TestAssert.Equal(1, result.Model.Inclusions.Count(x => x.Value == ToolboxInclusion.Unresolved), "New parcel requires explicit disposition.");
        var next = ToolboxReadinessEvaluator.Evaluate(result.Model, Context().SelectParcels([state.Model.Parcels[0].Id]), policy,
            Scopes(result.Model), Reviews(result, policy), result.Register, Authority, Geometry, Now, true);
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, next.Status, "Selected accepted subset cannot hide new parcel.");
    }
    public static void ChangedSourceNeedsFreshHumanValidation()
    {
        var policy = Policy(); var state = Start(policy: policy);
        var changed = ToolboxReconciliation.Accept(state, Proposal(state, policy, state.Model.Source, hashes: Hashes("changed"), explicitIdentity: true), Context(), policy, Authority);
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(changed, policy).Status, "Even freshly generated check hashes cannot revalidate old human evidence.");
        var h = changed.Register.HumanReviews[^1];
        changed = changed with { Register = changed.Register.RecordHumanReview(changed.Model, h with { At = Now.AddMinutes(1), Reason = "Revalidated changed source" }, Authority) };
        TestAssert.Equal(ToolboxReadinessStatus.Ready, Evaluate(changed, policy).Status, "Explicit new human validation restores readiness.");
        TestAssert.Equal(2, changed.Register.HumanReviews.Length, "Prior human event preserved.");
    }
    public static void ExplicitRegeneratedKeysAndManualReplacement()
    {
        var policy = Policy(); var state = Start(policy: policy); var root = state.Model.Source.ToJson();
        root["parcel_groups"]![0]!["parcel_group_id"] = "regenerated";
        root["rows"]![0]!["parcel_group_id"] = "regenerated"; root["rows"]![0]!["row_id"] = "regenerated-row";
        root["segments"]![0]!["parcel_group_id"] = "regenerated"; root["segments"]![0]!["segment_id"] = "regenerated-segment";
        var source = CanonicalReviewContract.Read(root);
        var keys = new Dictionary<string, string> { ["parcel"] = "regenerated", ["row"] = "regenerated-row", ["segment"] = "regenerated-segment" };
        var p = Proposal(state, policy, source);
        var choices = state.Model.IdentityMap.Ids.Select(x => { var kind = x.Key.Split(':')[0]; return new ToolboxCandidateDisposition(kind, x.Value,
            keys[kind], keys[kind], ToolboxValueChoice.Retain, "examiner", "Evidence-backed key regeneration mapping", "ev", true); }).ToImmutableArray();
        p = ToolboxReconciliation.Seal(p with { Dispositions = choices.Add(p.Dispositions.Single(x => x.Kind == "global")) });
        var result = ToolboxReconciliation.Accept(state, p, Context(), policy, Authority);
        TestAssert.Equal(state.Model.Parcels[0].Id, result.Model.Parcels[0].Id, "Explicit mapping preserves domain identity.");
        Reject(() => ToolboxReconciliation.Accept(state, ToolboxReconciliation.Seal(p with { Dispositions = p.Dispositions.Select(x => x.Kind == "global" ? x : x with { ExplicitIdentity = false }).ToImmutableArray() }), Context(), policy, Authority));
        root = state.Model.Source.ToJson(); root["rows"]![0]!["easting"] = "42";
        source = CanonicalReviewContract.Read(root); p = Proposal(state, policy, source);
        p = ToolboxReconciliation.Seal(p with { Dispositions = p.Dispositions.Select(x => x.Kind == "row" ? x with { Choice = ToolboxValueChoice.Replace, Reason = "Examiner accepts corrected coordinate" } : x).ToImmutableArray() });
        result = ToolboxReconciliation.Accept(state, p, Context(), policy, Authority);
        TestAssert.Equal("42", result.Model.Rows[0].Evidence.GetProperty("easting").GetString(), "Explicit replacement only.");
        TestAssert.Equal("10", result.Model.Relations!.Archives[0].Rows[0].Evidence.GetProperty("easting").GetString(), "Prior correction retained in historical snapshot.");
    }
    public static void BoundaryChangesStaleFindingResolution()
    {
        var policy = Policy(); var state = WithFinding(Shared(policy), resolved: true); var reviews = Reviews(state, policy);
        var r = reviews.Single(x => x.GeometryFingerprints?.ContainsKey(state.Model.Parcels[0].Id) == true);
        reviews = reviews.Select(x => x == r ? x with { FindingId = state.Register.Findings[0].Id } : x).ToArray();
        TestAssert.Equal(ToolboxReadinessStatus.Ready, Evaluate(state, policy, reviews).Status, "Current passing resolution proof.");
        var b = state.Model.Relations!.Boundaries[0] with { GeometryDigest = "changed" };
        var result = ToolboxReconciliation.Accept(state, ToolboxReconciliation.Seal(Proposal(state, policy, state.Model.Source) with { Boundaries = [b] }), Context(), policy, Authority);
        var next = Evaluate(result, policy, reviews);
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, next.Status, "Resolved event alone cannot approve changed shared geometry.");
        TestAssert.Equal("resolved", result.Register.Findings[0].History[^1].Status, "Rerun does not rewrite finding history.");
    }
    public static void TabularAndGlobalFactsDoNotStaleGeometry()
    {
        var policy = Policy(); var state = Start(3, policy); var json = state.Model.Source.ToJson();
        json["participants"] = new JsonObject { ["owner"] = new JsonObject { ["name"] = "Original", ["parcel_ids"] = new JsonArray("group-1") } };
        var model = state.Model.ReAdapt(Context(), CanonicalReviewContract.Read(json));
        model = model.WithRelations(model.Relations! with { Dependencies = Graph(model, policy) }); state = state with { Model = model };
        var reviews = Reviews(state, policy); var geometry = model.Parcels.ToDictionary(x => x.Id, x => model.ParcelFingerprint(x.Id, Geometry));
        json["participants"]!["owner"]!["name"] = "Corrected"; json["global_facts"] = new JsonObject { ["parish"] = "Changed" };
        var corrected = model.ReAdapt(Context().SelectPreset(ToolboxPreset.Compare), CanonicalReviewContract.Read(json));
        foreach (var parcel in corrected.Parcels) TestAssert.Equal(geometry[parcel.Id], corrected.ParcelFingerprint(parcel.Id, Geometry), "Tabular edits do not invalidate geometry-only inputs.");
        var impact = ToolboxDependencyInventory.Impact(model, corrected);
        TestAssert.True(impact.ChangedIds.Contains("participants:owner") && impact.ChangedIds.Contains("global-facts"), "Actual tabular/global values bound, not caller strings.");
        TestAssert.False(impact.AffectedIds.Contains("parcel:group-1"), "No declared geometry consumption of owner data.");
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(state with { Model = corrected }, policy, reviews).Status, "Consuming tabular checks stale.");
    }
    public static void AdjacencyAndRemovalAreExplicit()
    {
        var policy = Policy(); var state = Shared(policy); var p = Proposal(state, policy, state.Model.Source);
        Reject(() => ToolboxReconciliation.Accept(state, p, Context(), policy, Authority));
        Reject(() => ToolboxReconciliation.Accept(state, ToolboxReconciliation.Seal(p with { Adjacency = null }), Context(), policy, Authority));
        var removed = ToolboxReconciliation.Accept(state, ToolboxReconciliation.Seal(p with { Dependencies = Graph(state.Model, policy),
            BoundaryRemovals = [new("physical-edge", "examiner", "Reviewed edge no longer shared", "ev")] }), Context(), policy, Authority);
        var impact = ToolboxDependencyInventory.Impact(state.Model, removed.Model);
        TestAssert.True(impact.AffectedIds.Contains("parcel:group-1") && impact.AffectedIds.Contains("parcel:group-2"), "Removal still impacts former consumers.");
        var model = ToolboxParcelModel.Adapt(Context(), state.Model.Source, newImport: true);
        var unknown = ToolboxReconciliation.Start(model, new ToolboxExaminationRegister(), "initial", policy.Pin, Hashes(), [], Graph(model, policy));
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(unknown, policy).Status, "Missing adjacency review cannot invent an edge pass.");
        Reject(() => ToolboxReconciliation.Start(state.Model, state.Register, "reset", policy.Pin, Hashes(), [], state.Model.Relations!.Dependencies));
    }
    public static void DeclaredSuccessCannotHideFailedCandidate()
    {
        var state = Start();
        foreach (var status in new[] { "failed", "partial", "canceled", "incomplete" })
        {
            var json = state.Model.Source.ToJson(); json["extraction_quality_summary"] = new JsonObject { ["status"] = status };
            Reject(() => ToolboxReconciliation.Inspect(state, Batch(state, CanonicalReviewContract.Read(json))));
        }
    }
    public static void FreshChecksNeedFreshFindingDispositionAfterChange()
    {
        var policy = Policy(); var state = WithFinding(Shared(policy));
        var b = state.Model.Relations!.Boundaries[0] with { GeometryDigest = "changed-again" };
        var result = ToolboxReconciliation.Accept(state, ToolboxReconciliation.Seal(Proposal(state, policy, state.Model.Source) with { Boundaries = [b] }), Context(), policy, Authority);
        ToolboxCheckReview[] FailedReviews(ToolboxReconciliationSnapshot snapshot) => Reviews(snapshot, policy).Select(r =>
            r.GeometryFingerprints?.ContainsKey(state.Model.Parcels[0].Id) == true ? r with { Outcome = ToolboxCheckOutcome.Failed, FindingId = "retained-finding" } : r).ToArray();
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(result, policy, FailedReviews(result)).Status, "Refreshed checks cannot revive prior forwarding disposition.");
        var f = result.Register.Findings[0];
        var renewed = f with { History = f.History.Add(f.History[^1] with { Status = "retained", Reason = "Re-examined changed shared geometry; source defect still applies", At = Now.AddMinutes(1) }) };
        result = result with { Register = result.Register.RecordFinding(result.Model, renewed) };
        var next = Evaluate(result, policy, FailedReviews(result));
        TestAssert.Equal(ToolboxReadinessStatus.ReadyWithRecordedDefects, next.Status, "Authorized renewed disposition permits normal-stage forwarding.");
        TestAssert.False(next.GeometryApproved, "Forwarding does not approve defective geometry.");
        TestAssert.Equal("normal_next_stage", next.Route, "Cumulative defects follow normal workflow.");
        TestAssert.Equal(2, result.Register.Findings[0].History.Length, "Append only; no rewritten history.");
    }
    public static void BMetadataDoesNotChangeAFingerprints()
    {
        var policy = Policy(); var state = Start(policy: policy);
        var model = ToolboxParcelModel.Adapt(Context(), state.Model.Source, state.Model.IdentityMap);
        var reviews = Reviews(state, policy).Select(r => r with { EvidenceFingerprint = ToolboxReadinessEvaluator.EvidenceFingerprint(model, state.Register, r.EvidenceIds),
            GeometryFingerprints = r.GeometryCheck ? ImmutableDictionary<string, string>.Empty.Add(model.Parcels[0].Id, model.ParcelFingerprint(model.Parcels[0].Id, Geometry)) : null }).ToArray();
        var plain = state with { Model = model }; var old = Evaluate(plain, policy, reviews);
        var h = state.Register.HumanReviews[^1];
        plain = plain with { Register = plain.Register.RecordHumanReview(model, h with { SourceFingerprint = "unused-b-metadata" }, Authority) };
        var next = Evaluate(plain, policy, reviews.Select(r => r with { DependencyFingerprint = "unused-b-metadata" }).ToArray());
        TestAssert.Equal(ToolboxReadinessStatus.Ready, old.Status, "A remains independently usable.");
        TestAssert.Equal(old.Fingerprint, next.Fingerprint, "B-only metadata excluded from A v2 aggregate hashing.");
        TestAssert.Equal(old.ParcelDispositions[0].Fingerprint, next.ParcelDispositions[0].Fingerprint, "A parcel currency unchanged.");
    }
}
