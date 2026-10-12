using System.Collections.Immutable;
using System.IO;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Review.Canonical;
using ParcelWorkflowAddIn.Workflow.Toolbox;
using ParcelWorkflowAddIn.Workflow.Toolbox.Model;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static partial class ToolboxReconciliationTests
{
    public static void ReviewArchivedManualApplicabilityAfterRegeneration()
    {
        var policy = Policy(); var original = Start(policy: policy);
        var manualId = ToolboxLineageValidator.ManualEntityIds(original.Model).Single();
        var split = ToolboxReconciliation.Accept(original, LineageProposal(original, policy, 2), Context(), policy, Authority);
        var previousLink = split.Model.Relations!.Applicability.Single(x => x.RecordId == manualId);
        var mergeSource = CanonicalReviewContract.Read(CanonicalReviewContractTests.Sample());
        var merge = new ToolboxParcelLineage("next-merge", split.Model.Case, ToolboxLineageKind.Merge, split.Revision,
            "merge-revision", split.Model.Parcels.Select(x => x.Id).ToImmutableArray(), ["group-1"], "examiner", "Review subsequent merge", "ev",
            ImmutableDictionary<string, ToolboxInclusion>.Empty.Add("group-1", ToolboxInclusion.Included));
        var mergeLinks = split.Register.HumanReviews.Select(ToolboxLineageValidator.CorrectionId)
            .Concat(ToolboxLineageValidator.ManualEntityIds(split.Model)).Append(manualId).Distinct()
            .Select(id => new ToolboxHistoryApplicability(id, split.Revision, ["group-1"], ToolboxApplicabilityDisposition.Retained,
                "examiner", "Preserve original provenance through subsequent merge", "ev")).ToImmutableArray();
        Reject(() => ToolboxLineageValidator.Validate(split, Batch(split, mergeSource), [merge],
            mergeLinks.Where(x => x.RecordId != manualId).ToImmutableArray(), "merge-revision", "examiner"));
        ToolboxLineageValidator.Validate(split, Batch(split, mergeSource), [merge], mergeLinks, "merge-revision", "examiner");
        var json = split.Model.Source.ToJson();
        json["parcel_groups"]![0]!["parcel_group_id"] = "regenerated";
        json["rows"]![0]!["parcel_group_id"] = "regenerated";
        json["segments"]![0]!["parcel_group_id"] = "regenerated";
        var source = CanonicalReviewContract.Read(json);
        var proposal = Proposal(split, policy, source);
        var choices = proposal.Dispositions.Where(x => !(x.Kind == "parcel" && x.OldId is null))
            .Select(x => x.Kind == "parcel" && x.OutputKey == "child-1"
                ? x with { IncomingKey = "regenerated", OutputKey = "regenerated", ExplicitIdentity = true } : x).ToImmutableArray();
        proposal = ToolboxReconciliation.Seal(proposal with { OperationId = "regenerate", ResultRevision = "regenerated-revision", Dispositions = choices });
        var changed = ToolboxReconciliation.Accept(split, proposal, Context(), policy, Authority);
        TestAssert.True(changed.Model.Relations!.ReadinessBarriers.Any(x => x.Contains(manualId)), "Missing archived remapping blocks readiness.");
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(changed, policy).Status, "Fresh checks cannot hide retired applicability keys.");
        var repair = previousLink with { OriginalRevision = changed.Revision, TargetParcelKeys = ["regenerated"], Reason = "Revalidated archived correction after key change" };
        var repairs = changed.Model.Relations.Applicability.GroupBy(x => x.RecordId).Select(x => x.Last())
            .Where(x => x.TargetParcelKeys.Contains("child-1"))
            .Select(x => x.RecordId == manualId ? repair : x with { OriginalRevision = changed.Revision, TargetParcelKeys = ["regenerated"] }).ToImmutableArray();
        var fix = ToolboxReconciliation.Seal(Proposal(changed, policy, changed.Model.Source) with {
            OperationId = "repair", ResultRevision = "repaired-revision", Applicability = repairs });
        var result = ToolboxReconciliation.Accept(changed, fix, Context(), policy, Authority);
        TestAssert.Equal(0, result.Model.Relations!.ReadinessBarriers.Length, "Same-case archived correction can be remapped.");
        TestAssert.Equal(ToolboxReadinessStatus.Ready, Evaluate(result, policy).Status, "Repaired applicability and fresh checks restore readiness.");
        TestAssert.Equal(previousLink, result.Model.Relations.Applicability.First(x => x.RecordId == manualId), "Original mapping provenance remains append-only.");
        TestAssert.Equal(repair, result.Model.Relations.Applicability.Last(x => x.RecordId == manualId), "New mapping records current audit revision.");
        TestAssert.Equal(original.Model, result.Model.Relations.Archives[0], "Original manual evidence preserved verbatim.");

        var foreignContext = new ToolboxCaseContext("synthetic", "foreign-case", "examiner",
            new SelectedInnolaTransaction("task", "transaction-a", "000000101", "Compute", "Compute", Now),
            Path.Combine(Path.GetTempPath(), "ToolboxB"), Path.Combine(Path.GetTempPath(), "ToolboxB", "foreign-case"), allowLocalGeometryEdit: true);
        var foreign = ToolboxParcelModel.Adapt(foreignContext, original.Model.Source, newImport: true);
        var contaminated = changed with { Model = changed.Model.WithRelations(changed.Model.Relations with { Archives = changed.Model.Relations.Archives.Add(foreign) }) };
        var bad = ToolboxReconciliation.Seal(Proposal(contaminated, policy, contaminated.Model.Source) with {
            OperationId = "foreign-repair", ResultRevision = "foreign-revision", Applicability = repairs });
        Reject(() => ToolboxReconciliation.Accept(contaminated, bad, Context(), policy, Authority));
    }

    public static void ReviewCaseWideFindingApplicability()
    {
        var policy = Policy(); var initial = Start(policy: policy); var originalRegister = initial.Register;
        var finding = WithFinding(initial).Register.Findings[0] with { ParcelIds = [] };
        initial = initial with { Register = initial.Register.RecordFinding(initial.Model, finding) };
        var proposal = LineageProposal(initial, policy, 2, ToolboxApplicabilityDisposition.NotApplicable);
        var result = ToolboxReconciliation.Accept(initial, proposal, Context(), policy, Authority);
        TestAssert.Equal(0, result.Model.FindingParcels(finding).Length, "Case-wide NotApplicable is honored.");
        TestAssert.Equal(ToolboxReadinessStatus.Ready, Evaluate(result, policy).Status, "Accepted NotApplicable does not leave a finding dependency barrier.");
        TestAssert.Equal(0, result.Register.Findings[0].ParcelIds.Length, "Original case-wide scope is retained.");
        TestAssert.Equal(initial.Register.Findings[0].History, result.Register.Findings[0].History, "Original finding history untouched.");

        proposal = LineageProposal(initial, policy, 2, ToolboxApplicabilityDisposition.Applicable);
        result = ToolboxReconciliation.Accept(initial, proposal, Context(), policy, Authority);
        TestAssert.Equal(result.Model.Parcels[0].Id, result.Model.FindingParcels(finding).Single(), "Explicit mapping narrows case-wide finding.");
        var retained = result.Register.Findings[0];
        result = result with { Register = result.Register.RecordFinding(result.Model, retained with {
            History = retained.History.Add(retained.History[^1] with { At = Now.AddMinutes(1), Status = "retained", Reason = "Reviewed narrower applicability" }) }) };
        var next = Evaluate(result, policy);
        var withoutFinding = Evaluate(result with { Register = originalRegister }, policy);
        var unrelated = result.Model.Parcels[1].Id;
        TestAssert.Equal(withoutFinding.ParcelDispositions.Single(x => x.ParcelId == unrelated).Fingerprint,
            next.ParcelDispositions.Single(x => x.ParcelId == unrelated).Fingerprint, "Narrowed finding does not stale unrelated parcel disposition.");
        TestAssert.Equal(ToolboxReadinessStatus.ReadyWithRecordedDefects, next.Status, "Fresh retained disposition applies to selected child.");

        proposal = LineageProposal(initial, policy, 2, ToolboxApplicabilityDisposition.Unresolved);
        result = ToolboxReconciliation.Accept(initial, proposal, Context(), policy, Authority);
        TestAssert.Equal(0, result.Model.FindingParcels(finding).Length, "Unresolved mapping never silently expands case-wide scope.");
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(result, policy).Status, "Unresolved case-wide applicability remains blocking.");
    }

    public static void ReviewAcceptedDispositionAuditAndReplay()
    {
        var policy = Policy(); var initial = Start(policy: policy); var json = initial.Model.Source.ToJson();
        json["rows"]![0]!["easting"] = "42";
        var candidate = CanonicalReviewContract.Read(json);
        var proposal = Proposal(initial, policy, candidate);
        proposal = ToolboxReconciliation.Seal(proposal with { Dispositions = proposal.Dispositions.Select(x => x.Kind == "row"
            ? x with { Choice = ToolboxValueChoice.Replace, Reason = "Examiner accepts reviewed coordinate replacement" } : x).ToImmutableArray() });
        var before = DateTimeOffset.UtcNow;
        var result = ToolboxReconciliation.Accept(initial, proposal, Context(), policy, Authority);
        var audit = result.AcceptedDispositions.Single();
        TestAssert.Equal(proposal.OperationId, audit.OperationId, "Audit identifies operation.");
        TestAssert.Equal(initial.Revision, audit.ParentRevision, "Audit identifies original revision.");
        TestAssert.Equal(result.Revision, audit.ResultRevision, "Audit identifies accepted revision.");
        TestAssert.Equal(proposal.Digest, audit.Digest, "Audit binds sealed proposal.");
        TestAssert.True(audit.AcceptedAt >= before && audit.AcceptedAt <= DateTimeOffset.UtcNow && audit.AcceptedAt.Offset == TimeSpan.Zero, "Audit records UTC acceptance time.");
        var replacement = audit.Dispositions.Single(x => x.Kind == "row");
        TestAssert.Equal(ToolboxValueChoice.Replace, replacement.Choice, "Replacement choice persists without proposal.");
        TestAssert.Equal("examiner", replacement.Actor, "Actor preserved.");
        TestAssert.Equal("ev", replacement.EvidenceId, "Evidence preserved.");
        TestAssert.Equal("Examiner accepts reviewed coordinate replacement", replacement.Reason, "Examiner reason preserved.");
        TestAssert.Equal("42", result.Model.Rows[0].Evidence.GetProperty("easting").GetString(), "Replacement applied.");
        TestAssert.Equal("10", result.Model.Relations!.Archives[0].Rows[0].Evidence.GetProperty("easting").GetString(), "Prior manual correction preserved.");
        TestAssert.True(ReferenceEquals(result, ToolboxReconciliation.Accept(result, proposal, Context(), policy, Authority)), "Replay preserves timestamp and history without duplicates.");
        TestAssert.False(result.Fingerprint == (result with { AcceptedDispositions = [audit with { AcceptedAt = audit.AcceptedAt.AddSeconds(1) }] }).Fingerprint, "Snapshot fingerprint binds immutable audit time.");
        var second = ToolboxReconciliation.Seal(Proposal(result, policy, result.Model.Source) with { OperationId = "second", ResultRevision = "second-revision" });
        var continued = ToolboxReconciliation.Accept(result, second, Context(), policy, Authority);
        TestAssert.Equal(2, continued.AcceptedDispositions.Length, "Further acceptance appends history.");
        TestAssert.Equal(audit, continued.AcceptedDispositions[0], "First acceptance audit untouched.");
    }

    public static void ReviewCanonicalEvidenceLinkFingerprints()
    {
        var policy = Policy(); var state = Start(3, policy); var original = state.Model;
        ToolboxParcelModel Links(params string[] keys)
        {
            var json = original.Source.ToJson();
            json["evidence_links"] = new JsonArray(keys.Select(key => (JsonNode)new JsonObject {
                ["evidence_id"] = "ev", ["parcel_group_id"] = key, ["role"] = "title" }).ToArray());
            return original.ReAdapt(Context(), CanonicalReviewContract.Read(json));
        }
        var added = Links("group-1"); var reassigned = Links("group-2"); var removed = Links();
        string Hash(ToolboxParcelModel m, ToolboxScopeKind kind, string id, bool geometry = false) =>
            ToolboxReadinessEvaluator.CanonicalInputFingerprint(m, kind, id, geometry);
        foreach (var kind in new[] { ToolboxScopeKind.Case, ToolboxScopeKind.Document })
        {
            var id = kind == ToolboxScopeKind.Case ? original.Case.CaseId : "plan";
            TestAssert.False(Hash(original, kind, id) == Hash(added, kind, id), "Case/document checks bind added links.");
            TestAssert.False(Hash(added, kind, id) == Hash(reassigned, kind, id), "Case/document checks bind reassigned links.");
            TestAssert.Equal(Hash(original, kind, id), Hash(removed, kind, id), "Removing all links restores canonical relationship inputs.");
        }
        for (var i = 0; i < original.Parcels.Length; i++)
        {
            var id = original.Parcels[i].Id;
            TestAssert.Equal(Hash(original, ToolboxScopeKind.Parcel, id, true), Hash(added, ToolboxScopeKind.Parcel, id, true), "Link-only edits preserve geometry canonical hashes.");
            TestAssert.Equal(original.ParcelFingerprint(id, Geometry), added.ParcelFingerprint(id, Geometry), "Physical geometry fingerprint unchanged.");
            if (i == 0) TestAssert.False(Hash(original, ToolboxScopeKind.Parcel, id) == Hash(added, ToolboxScopeKind.Parcel, id), "Target parcel nongeometry checks bind links.");
            else TestAssert.Equal(Hash(original, ToolboxScopeKind.Parcel, id), Hash(added, ToolboxScopeKind.Parcel, id), "Unrelated nongeometry checks unchanged.");
            if (i < 2) TestAssert.False(Hash(added, ToolboxScopeKind.Parcel, id) == Hash(reassigned, ToolboxScopeKind.Parcel, id), "Both old and new relationship consumers stale.");
            var segment = original.Segments[i].Id;
            TestAssert.Equal(i == 0, Hash(original, ToolboxScopeKind.Boundary, segment) != Hash(added, ToolboxScopeKind.Boundary, segment), "Boundary checks bind member-scoped links.");
        }
        var a = ToolboxParcelModel.Adapt(Context(), original.Source, original.IdentityMap);
        var aWithLink = a.ReAdapt(Context(), added.Source);
        TestAssert.Equal(Hash(a, ToolboxScopeKind.Case, a.Case.CaseId), Hash(aWithLink, ToolboxScopeKind.Case, a.Case.CaseId), "A v2 canonical hash contract preserved.");
        var sharedBoundary = Boundary(original);
        var shared = original.WithRelations(original.Relations! with { Boundaries = [sharedBoundary] });
        var sharedAdded = added.WithRelations(added.Relations! with { Boundaries = [sharedBoundary] });
        TestAssert.False(Hash(shared, ToolboxScopeKind.Boundary, sharedBoundary.Id) == Hash(sharedAdded, ToolboxScopeKind.Boundary, sharedBoundary.Id), "Physical boundary checks bind links of either member.");
        var independentLink = Links("group-3").WithRelations(original.Relations! with { Boundaries = [sharedBoundary] });
        TestAssert.Equal(Hash(shared, ToolboxScopeKind.Boundary, sharedBoundary.Id), Hash(independentLink, ToolboxScopeKind.Boundary, sharedBoundary.Id), "Unrelated parcel links do not stale shared-boundary checks.");
    }
}
