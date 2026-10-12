using System.Collections.Immutable;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.Workflow.Review.Canonical;
using ParcelWorkflowAddIn.Workflow.Toolbox.Model;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static partial class ToolboxReconciliationTests
{
    public static void ReviewSecondaryBoundaryEvidenceInvalidatesBothParcels()
    {
        var policy = Policy(); var shared = Shared(policy); var json = shared.Model.Source.ToJson();
        json["source_inventory"]!.AsArray().Add(new JsonObject { ["source_id"] = "other", ["source_role"] = "st_surveyplan" });
        json["source_traces"]!.AsArray().Add(new JsonObject { ["evidence_id"] = "edge-ev", ["source_id"] = "other", ["page"] = 1,
            ["zone"] = "map", ["observed_value"] = "old" });
        var model = ToolboxParcelModel.Adapt(Context(), CanonicalReviewContract.Read(json), shared.Model.IdentityMap);
        var boundaries = ImmutableArray.Create(shared.Model.Relations!.Boundaries[0] with { EvidenceIds = ["edge-ev"] });
        var graph = Graph(model, policy, boundaries); var hashes = Hashes().Add("other", "other-v1");
        var state = ToolboxReconciliation.Start(model, shared.Register, "secondary-base", policy.Pin, hashes, boundaries, graph, Adjacency(model));
        var reviews = Reviews(state, policy); var old = Evaluate(state, policy, reviews);
        TestAssert.Equal(ToolboxReadinessStatus.Ready, old.Status, "Explicit secondary evidence graph starts ready.");
        var missing = graph with { Edges = graph.Edges.Where(e => e.ProducerId != "evidence:edge-ev" || e.ConsumerId != "boundary:physical-edge").ToImmutableArray() };
        var incomplete = state.Model.WithRelations(state.Model.Relations! with { Dependencies = missing });
        TestAssert.True(missing.CoverageIssues(incomplete).Any(x => x.StartsWith("Unregistered boundary evidence", StringComparison.Ordinal)), "Secondary evidence cannot bypass boundary coverage via case-only consumption.");
        Reject(() => ToolboxReconciliation.Accept(state, ToolboxReconciliation.Seal(Proposal(state, policy, state.Model.Source) with { Boundaries = boundaries, Dependencies = missing }), Context(), policy, Authority));
        var missingSource = graph with { Edges = graph.Edges.Where(e => e.ProducerId != "source:other").ToImmutableArray() };
        TestAssert.True(missingSource.CoverageIssues(state.Model).Any(x => x.StartsWith("Unregistered evidence source", StringComparison.Ordinal)), "Evidence must consume its actual source hash.");
        foreach (var changeTrace in new[] { false, true })
        {
            var changedJson = state.Model.Source.ToJson();
            if (changeTrace) changedJson["source_traces"]![1]!["observed_value"] = "contradictory";
            var source = CanonicalReviewContract.Read(changedJson);
            var p = Proposal(state, policy, source, hashes: changeTrace ? hashes : hashes.SetItem("other", "other-v2"), explicitIdentity: true);
            p = ToolboxReconciliation.Seal(p with { Boundaries = boundaries, Dependencies = graph,
                Dispositions = p.Dispositions.Select(c => c.Kind == "global" ? c with { Choice = ToolboxValueChoice.Replace } : c).ToImmutableArray() });
            var changed = ToolboxReconciliation.Accept(state, p, Context(), policy, Authority);
            var next = Evaluate(changed, policy, reviews);
            TestAssert.Equal(ToolboxReadinessStatus.Blocked, next.Status, "Changed secondary trace OR hash invalidates old geometry reviews.");
            TestAssert.False(next.GeometryApproved, "No stale geometry approval.");
            foreach (var member in boundaries[0].Members)
                TestAssert.False(old.ParcelDispositions.Single(x => x.ParcelId == member.ParcelId).IsCurrent(next.ParcelDispositions.Single(x => x.ParcelId == member.ParcelId)), "Every shared consumer is stale.");
            var independent = model.Parcels[2].Id;
            TestAssert.Equal(state.Model.ParcelFingerprint(independent, Geometry), changed.Model.ParcelFingerprint(independent, Geometry), "Unrelated geometry does not consume secondary boundary evidence.");
        }
    }

    public static void ReviewEvidenceLinkChangesInvalidateOnlyConsumers()
    {
        var policy = Policy(); var state = Start(3, policy); var reviews = Reviews(state, policy);
        var json = state.Model.Source.ToJson();
        json["evidence_links"] = new JsonArray(new JsonObject { ["evidence_id"] = "ev", ["parcel_group_id"] = "group-1", ["role"] = "owner-evidence" });
        var linked = state.Model.ReAdapt(Context(), CanonicalReviewContract.Read(json));
        var impact = ToolboxDependencyInventory.Impact(state.Model, linked);
        TestAssert.True(impact.AffectedIds.Contains("decision:group-1") && impact.AffectedIds.Contains("artifact"), "Link changes invalidate the linked decision and report.");
        var expectedChecks = state.Model.Relations!.Dependencies.Nodes.Where(n => n.Kind == ToolboxDependencyKind.Check
            && n.CheckBinding!.ScopeKind == ParcelWorkflowAddIn.Workflow.Toolbox.Policy.ToolboxScopeKind.Case).Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        TestAssert.True(expectedChecks.SetEquals(impact.AffectedIds.Where(id => id.StartsWith("check:", StringComparison.Ordinal))), "Impact enumerates exactly the consuming case checks, not independent geometry checks.");
        TestAssert.False(impact.AffectedIds.Contains("decision:group-2") || impact.AffectedIds.Contains("parcel:group-1"), "Other decisions and geometry do not consume an unrelated textual link.");
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(state with { Model = linked }, policy, reviews).Status, "Old nongeometry checks cannot approve changed links.");
        foreach (var parcel in state.Model.Parcels) TestAssert.Equal(state.Model.ParcelFingerprint(parcel.Id, Geometry), linked.ParcelFingerprint(parcel.Id, Geometry), "Text links preserve geometry-only fingerprints.");
        var removed = linked.ReAdapt(Context(), state.Model.Source);
        TestAssert.True(ToolboxDependencyInventory.Impact(linked, removed).AffectedIds.Contains("decision:group-1"), "Removal invalidates the old consumer.");
        json["evidence_links"]![0]!["parcel_group_id"] = "group-2";
        var reassigned = linked.ReAdapt(Context(), CanonicalReviewContract.Read(json));
        var moved = ToolboxDependencyInventory.Impact(linked, reassigned);
        TestAssert.True(moved.AffectedIds.Contains("decision:group-1") && moved.AffectedIds.Contains("decision:group-2"), "Reassignment invalidates old and new linked decisions.");
    }

    public static void ReviewEvidenceLinksRequireRegisteredDecisionConsumers()
    {
        var policy = Policy(); var state = Start(policy: policy); var json = state.Model.Source.ToJson();
        json["source_inventory"]!.AsArray().Add(new JsonObject { ["source_id"] = "other" });
        json["source_traces"]!.AsArray().Add(new JsonObject { ["evidence_id"] = "other-ev", ["source_id"] = "other", ["page"] = 1 });
        json["evidence_links"] = new JsonArray(new JsonObject { ["evidence_id"] = "other-ev", ["parcel_group_id"] = "group-1", ["role"] = "title" });
        var seed = ToolboxParcelModel.Adapt(Context(), CanonicalReviewContract.Read(json), state.Model.IdentityMap);
        var graph = Graph(seed, policy); var hashes = Hashes().Add("other", "other-hash");
        var linked = ToolboxReconciliation.Start(seed, state.Register, "linked-base", policy.Pin, hashes, [], graph, Adjacency(seed));
        TestAssert.Equal(0, graph.CoverageIssues(linked.Model).Length, "Explicit evidence-to-decision edge accounts for link consumers.");
        var multiple = graph with { Nodes = graph.Nodes.Add(new("decision-secondary", ToolboxDependencyKind.Decision,
            "logical-secondary", DecisionParcelId: seed.Parcels[0].Id)), Edges = graph.Edges.Add(new("decision-secondary", "case")) };
        var multipleModel = linked.Model.WithRelations(linked.Model.Relations! with { Dependencies = multiple });
        multiple.Validate(multipleModel);
        var incompleteIssues = multiple.CoverageIssues(multipleModel);
        TestAssert.True(incompleteIssues.Any(issue => issue.EndsWith("/decision-secondary", StringComparison.Ordinal)), "Every decision bound to a linked parcel must consume its supporting evidence.");
        var reversed = multiple with { Nodes = multiple.Nodes.Reverse().ToImmutableArray() };
        TestAssert.True(incompleteIssues.ToHashSet(StringComparer.Ordinal).SetEquals(reversed.CoverageIssues(multipleModel)), "Coverage does not depend on which decision appears first.");
        var completed = multiple with { Edges = multiple.Edges.Add(new("evidence:other-ev", "decision-secondary")) };
        TestAssert.Equal(0, completed.CoverageIssues(multipleModel).Length, "Multiple explicitly bound decisions are supported with complete consumers.");
        var oldReviews = Reviews(linked, policy); var oldDecision = Evaluate(linked, policy, oldReviews);
        TestAssert.Equal(ToolboxReadinessStatus.Ready, oldDecision.Status, "Linked-evidence baseline is ready.");
        foreach (var changeTrace in new[] { false, true })
        {
            var changedJson = linked.Model.Source.ToJson();
            if (changeTrace) changedJson["source_traces"]![1]!["observed_value"] = "contradictory-title";
            var source = CanonicalReviewContract.Read(changedJson);
            var proposal = Proposal(linked, policy, source, hashes: changeTrace ? hashes : hashes.SetItem("other", "changed-title-hash"), explicitIdentity: true);
            proposal = ToolboxReconciliation.Seal(proposal with {
                Dispositions = proposal.Dispositions.Select(c => c.Kind == "global" ? c with { Choice = ToolboxValueChoice.Replace } : c).ToImmutableArray() });
            var changed = ToolboxReconciliation.Accept(linked, proposal, Context(), policy, Authority);
            var next = Evaluate(changed, policy, oldReviews);
            TestAssert.Equal(ToolboxReadinessStatus.Blocked, next.Status, "An unchanged link ID cannot hide changed linked evidence or its source hash.");
            TestAssert.False(oldDecision.ParcelDispositions[0].IsCurrent(next.ParcelDispositions[0]), "Old linked parcel disposition consumes the changed evidence.");
            TestAssert.Equal(linked.Model.ParcelFingerprint(seed.Parcels[0].Id, Geometry), changed.Model.ParcelFingerprint(seed.Parcels[0].Id, Geometry), "Decision-only title evidence leaves geometry-only inputs unchanged.");
        }
        var missing = graph with { Edges = graph.Edges.Where(e => e.ProducerId != "evidence:other-ev" || e.ConsumerId != "decision:group-1").ToImmutableArray() };
        var bad = linked.Model.WithRelations(linked.Model.Relations! with { Dependencies = missing });
        TestAssert.True(missing.CoverageIssues(bad).Any(x => x.StartsWith("Unregistered linked evidence decision", StringComparison.Ordinal)), "Case-only evidence consumption cannot hide an unregistered linked decision.");
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(linked with { Model = bad }, policy).Status, "Unknown link coverage blocks even newly created reviews.");
    }

    public static void ReviewDependencyVersionAndBindingChangesHaveImpact()
    {
        var state = Start(); var graph = state.Model.Relations!.Dependencies;
        var changedGraph = graph with { Version = graph.Version + "-next" };
        var changed = state.Model.WithRelations(state.Model.Relations with { Dependencies = changedGraph });
        var check = graph.Nodes.First(n => n.Kind == ToolboxDependencyKind.Check);
        TestAssert.False(graph.ConsumerFingerprint(state.Model, check.Kind, check.ReferenceId) == changedGraph.ConsumerFingerprint(changed, check.Kind, check.ReferenceId), "Version changes check currency.");
        var impact = ToolboxDependencyInventory.Impact(state.Model, changed);
        TestAssert.Equal(graph.Nodes.Length, impact.ChangedIds.Length, "Version change is reported consistently for every node.");
        TestAssert.True(impact.AffectedIds.Contains("artifact") && impact.AffectedIds.Contains(check.Id), "Check and output invalidation is not omitted.");
        var altered = graph with { Nodes = graph.Nodes.Select(n => n.Id == check.Id ? n with { CheckBinding = n.CheckBinding! with { InputFingerprint = "rebound-input" } } : n).ToImmutableArray() };
        var rebound = state.Model.WithRelations(state.Model.Relations with { Dependencies = altered });
        TestAssert.True(ToolboxDependencyInventory.Impact(state.Model, rebound).ChangedIds.Contains(check.Id), "Check rebinding is significant even when actual canonical values are identical.");
        var nonGeometry = graph.Nodes.First(n => n.Kind == ToolboxDependencyKind.Check && !n.CheckBinding!.GeometryCheck);
        var mismatched = graph with { Nodes = graph.Nodes.Select(n => n.Id == nonGeometry.Id
            ? n with { CheckBinding = n.CheckBinding! with { GeometryCheck = true } } : n).ToImmutableArray() };
        var invalid = state with { Model = state.Model.WithRelations(state.Model.Relations with { Dependencies = mismatched }) };
        TestAssert.True(Evaluate(invalid, Policy()).Reasons.Any(reason => reason.Contains("Check geometry binding mismatch", StringComparison.Ordinal)), "Fresh checks cannot misclassify canonical evidence consumption as geometry-only.");
    }

    public static void ReviewInclusionImpactPreservesGeometryChecks()
    {
        var state = Start(3); var id = state.Model.Parcels[0].Id;
        var excluded = state.Model.SetInclusion(new(id, ToolboxInclusion.Excluded, "examiner", Now, "Explicit scope exclusion", "ev", "test-v1"), "test-v1", true);
        var impact = ToolboxDependencyInventory.Impact(state.Model, excluded);
        TestAssert.True(impact.AffectedIds.Contains("decision:group-1") && impact.AffectedIds.Contains("case") && impact.AffectedIds.Contains("artifact"), "Inclusion invalidates decisions, aggregates and output artifacts.");
        TestAssert.False(impact.AffectedIds.Contains("parcel:group-1") || impact.AffectedIds.Contains("decision:group-2"), "Scope-only edit leaves geometry and unrelated parcel decisions unchanged.");
        foreach (var parcel in state.Model.Parcels) TestAssert.Equal(state.Model.ParcelFingerprint(parcel.Id, Geometry), excluded.ParcelFingerprint(parcel.Id, Geometry), "No geometry invalidation from exclusion.");
        var restored = excluded.SetInclusion(new(id, ToolboxInclusion.Included, "examiner", Now.AddMinutes(1), "Reinclude after review", "ev", "test-v1"), "test-v1", true);
        TestAssert.True(ToolboxDependencyInventory.Impact(excluded, restored).AffectedIds.Contains("decision:group-1"), "Re-inclusion triggers a new decision.");
        var graph = state.Model.Relations!.Dependencies;
        var unbound = graph with { Nodes = graph.Nodes.Select(n => n.Id == "decision:group-1" ? n with { ReferenceId = "logical-decision" } : n).ToImmutableArray() };
        Reject(() => unbound.Validate(state.Model));
        var bound = unbound with { Nodes = unbound.Nodes.Select(n => n.Id == "decision:group-1" ? n with { DecisionParcelId = id } : n).ToImmutableArray() };
        var boundModel = state.Model.WithRelations(state.Model.Relations with { Dependencies = bound });
        bound.Validate(boundModel);
        var boundExcluded = boundModel.SetInclusion(new(id, ToolboxInclusion.Excluded, "examiner", Now, "Bound scope exclusion", "ev", "test-v1"), "test-v1", true);
        TestAssert.True(ToolboxDependencyInventory.Impact(boundModel, boundExcluded).AffectedIds.Contains("decision:group-1"), "Logical decisions with explicit parcel bindings consume inclusion changes.");
    }

    public static void ReviewSharedDeepDagValidationRetainsCycleGuards()
    {
        var state = Start(); var graph = state.Model.Relations!.Dependencies; var nodes = graph.Nodes.ToBuilder(); var edges = graph.Edges.ToBuilder();
        for (var layer = 0; layer < 32; layer++)
        {
            for (var side = 0; side < 2; side++) nodes.Add(new($"layer-{layer}-{side}", ToolboxDependencyKind.Artifact, $"output-{layer}-{side}"));
            if (layer == 0) continue;
            for (var left = 0; left < 2; left++) for (var right = 0; right < 2; right++) edges.Add(new($"layer-{layer - 1}-{left}", $"layer-{layer}-{right}"));
        }
        var shared = graph with { Nodes = nodes.ToImmutable(), Edges = edges.ToImmutable() };
        shared.Validate(state.Model);
        Reject(() => (shared with { Edges = shared.Edges.Add(new("layer-31-0", "layer-0-0")) }).Validate(state.Model));
        nodes = graph.Nodes.ToBuilder(); edges = graph.Edges.ToBuilder();
        for (var i = 0; i < 4096; i++)
        {
            nodes.Add(new("deep-" + i, ToolboxDependencyKind.Artifact, "deep-output-" + i));
            if (i > 0) edges.Add(new("deep-" + (i - 1), "deep-" + i));
        }
        var deep = graph with { Nodes = nodes.ToImmutable(), Edges = edges.ToImmutable() };
        deep.Validate(state.Model);
        Reject(() => (deep with { Edges = deep.Edges.Add(new("deep-4095", "deep-0")) }).Validate(state.Model));
    }
}
