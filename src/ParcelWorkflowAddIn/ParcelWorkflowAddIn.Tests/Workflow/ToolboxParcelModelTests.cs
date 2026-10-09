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

internal static class ToolboxParcelModelTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-08T16:00:00Z");
    private const string GeometryToken = "synthetic-planar-crs/metres/straight/2d/precision-v1";
    private static ToolboxCaseContext Context(string caseId = "case-a", string tr = "000000101", ToolboxPreset stage = ToolboxPreset.Compute) =>
        new("synthetic", caseId, "examiner", new SelectedInnolaTransaction("task", "transaction-a", tr,
            "Compute", "Compute", Now), Path.Combine(Path.GetTempPath(), "ToolboxModel"),
            Path.Combine(Path.GetTempPath(), "ToolboxModel", caseId), stage);
    private static ToolboxParcelModel Model(int count = 1) => ToolboxParcelModel.Adapt(Context(),
        CanonicalReviewContract.Read(CanonicalReviewContractTests.Sample(count)), newImport: true);
    private static ToolboxReviewPolicy Policy(bool reportOnly = false, Action<JsonObject>? configure = null)
    {
        var json = ToolboxPolicyTests.Fixture();
        foreach (var stage in new[] { "Compute", "Compare" })
        {
            var check = json["items"]![0]!["stages"]![stage]!["checks"]![0]!;
            check["scope"] = "parcel";
            if (reportOnly) check["effect"] = "report_only";
        }
        configure?.Invoke(json);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(json);
        return ToolboxReviewPolicyLoader.Load(bytes, new("synthetic-toolbox-policy", "test-v1", Convert.ToHexString(SHA256.HashData(bytes))));
    }
    private static ToolboxPolicyScope[] Scopes(ToolboxParcelModel model, string input = "input-1", string revision = "revision-1") =>
        new[] { new ToolboxPolicyScope(model.Case.CaseId, ToolboxScopeKind.Case, model.Case.CaseId, revision, input) }
        .Concat(model.Parcels.Select(x => new ToolboxPolicyScope(model.Case.CaseId, ToolboxScopeKind.Parcel, x.Id, revision, input)))
        .Concat(model.Source.Sources.Select(x => new ToolboxPolicyScope(model.Case.CaseId, ToolboxScopeKind.Document, x.Id, revision, input)))
        .Concat(model.Segments.Select(x => new ToolboxPolicyScope(model.Case.CaseId, ToolboxScopeKind.Boundary, x.Id, revision, input)))
        .Concat(FactScopes(model, "participants", ToolboxScopeKind.Participant, revision, input))
        .Concat(FactScopes(model, "identifier_pairs", ToolboxScopeKind.IdentifierPair, revision, input)).ToArray();
    private static IEnumerable<ToolboxPolicyScope> FactScopes(ToolboxParcelModel model, string name, ToolboxScopeKind kind,
        string revision, string input) => model.Source.GlobalData.TryGetProperty(name, out var facts)
        ? facts.EnumerateObject().Select(x => new ToolboxPolicyScope(model.Case.CaseId, kind, x.Name, revision, input)).ToArray() : [];
    private static ToolboxCheckReview[] Reviews(ToolboxReviewPolicy policy, ToolboxPolicyScope[] scopes, ToolboxPreset stage = ToolboxPreset.Compute,
        ToolboxParcelModel? model = null, ToolboxExaminationRegister? register = null) =>
        ToolboxReviewPolicyResolver.Resolve(policy, stage, scopes).Where(x => x.EffectiveRequirement != ToolboxReviewRequirement.NotApplicable)
            .Select(x => new ToolboxCheckReview(x.ResolutionFingerprint, ToolboxCheckOutcome.Passed, "Human-reviewed synthetic result",
                ["ev"], "examiner", Now, GeometryCheck: x.ItemId == "M01",
                EvidenceFingerprint: model is null ? "" : ToolboxReadinessEvaluator.EvidenceFingerprint(model, register ?? Register(model, stage), ["ev"]),
                GeometryFingerprints: x.ItemId == "M01" && model is not null
                    ? ImmutableDictionary<string, string>.Empty.Add(x.ScopeId!, model.ParcelFingerprint(x.ScopeId!, GeometryToken)) : null,
                CanonicalInputFingerprint: model is null ? "" : ToolboxReadinessEvaluator.CanonicalInputFingerprint(model, x.ScopeKind, x.ScopeId, x.ItemId == "M01"))).ToArray();
    private static ToolboxExaminationRegister Register(ToolboxParcelModel model, ToolboxPreset stage = ToolboxPreset.Compute) =>
        new ToolboxExaminationRegister().RecordHumanReview(model, new("ev", "raw", "reviewed", "manual",
            ToolboxPresence.Present, ToolboxAccuracy.Confirmed, stage, "examiner", Now, "Checked source", "revision-1"), Authority());
    private static ToolboxDispositionAuthority Authority(bool authorized = true) => new("examiner", "test-v1", authorized);
    private static ToolboxReadinessSnapshot Evaluate(ToolboxParcelModel model, ToolboxReviewPolicy policy,
        ToolboxCheckReview[]? reviews = null, ToolboxExaminationRegister? register = null,
        ToolboxCaseContext? context = null, ToolboxPolicyScope[]? scopes = null, ToolboxDispositionAuthority? authority = null) =>
        ToolboxReadinessEvaluator.Evaluate(model, context ?? Context(), policy, scopes ?? Scopes(model),
            reviews ?? Reviews(policy, scopes ?? Scopes(model), (context ?? Context()).SelectedPreset, model, register),
            register ?? Register(model), authority ?? Authority(), GeometryToken, Now, geometryPolicyVerified: true);
    private static ToolboxFinding Finding(ToolboxParcelModel model, ToolboxFindingOrigin origin = ToolboxFindingOrigin.SourceDefect,
        bool corrected = false, bool geometry = true) => new("finding-1", origin, [model.Parcels[0].Id], "plan", 1, "map", corrected, geometry,
            [new(ToolboxPreset.Compute, "examiner", Now, "recorded", "Source document defect", "ev", "input-1", "test-v1", "test-v1")],
            "M01", "synthetic-source-defect", "error");

    public static void IdentityMapRenameReorderAndDefensiveSnapshots()
    {
        var model = Model(3);
        var root = model.Source.ToJson();
        root["parcel_groups"]![0]!["display_label"] = "Renamed";
        var groups = root["parcel_groups"]!.AsArray(); var first = groups[0]!.DeepClone(); groups.RemoveAt(0); groups.Add(first);
        var projected = model.ReAdapt(Context(), CanonicalReviewContract.Read(root));
        foreach (var old in model.Parcels) TestAssert.Equal(old.Id, projected.Parcels.Single(x => x.SourceKey == old.SourceKey).Id, "Stable mapped IDs.");
        TestAssert.Equal(model.ParcelFingerprint(model.Parcels[0].Id, "geometry-v1"), projected.ParcelFingerprint(model.Parcels[0].Id, "geometry-v1"), "Label/order do not change geometry.");
        root["rows"]![0]!["easting"] = "changed";
        TestAssert.Equal("10", projected.Rows[0].Evidence.GetProperty("easting").GetString(), "Immutable source projection.");
        CanonicalReviewContractTests.Reject(() => ToolboxParcelModel.Adapt(Context(), model.Source));
        CanonicalReviewContractTests.Reject(() => ToolboxParcelModel.Adapt(Context(), model.Source,
            model.IdentityMap with { Ids = model.IdentityMap.Ids.Remove(model.IdentityMap.Ids.Keys.First()) }));
        CanonicalReviewContractTests.Reject(() => ToolboxParcelModel.Adapt(Context("foreign"), model.Source, model.IdentityMap));
        CanonicalReviewContractTests.Reject(() => ToolboxParcelModel.Adapt(Context(), model.Source, newImport: true, allocateId: () => "duplicate"));
        var transaction = new SelectedInnolaTransaction("task", "transaction-a", "000000101", "Compute", "Compute", Now);
        var linkedContext = new ToolboxCaseContext("synthetic", "case-a", "examiner", transaction,
            Path.Combine(Path.GetTempPath(), "ToolboxModel"), Path.Combine(Path.GetTempPath(), "ToolboxModel", "case-a"),
            sources: [new("linked-plan", "source-env", "source-transaction", "000009999", "source-revision")]);
        var linked = ToolboxParcelModel.Adapt(linkedContext, model.Source, newImport: true);
        TestAssert.Equal("source-transaction", linked.LinkedSources[0].TransactionId, "Retain linked source identity.");
        TestAssert.Equal("source-revision", linked.LinkedSources[0].RevisionId, "Retain linked source revision.");
        TestAssert.True(linked.LinkedSources[0].IsReadOnly, "Linked source remains read-only.");
    }

    public static void SelectionInclusionAndAcceptanceStaleness()
    {
        var model = Model(3); var policy = Policy();
        TestAssert.Equal(0, model.ResolveSelection(Context()).Length, "Empty selection is case scope.");
        TestAssert.Equal(2, model.ResolveSelection(Context().SelectParcels(model.Parcels.Take(2).Select(x => x.Id))).Length, "Multi selection.");
        CanonicalReviewContractTests.Reject(() => model.ResolveSelection(Context().SelectParcels(["foreign"])));
        var old = Evaluate(model, policy);
        TestAssert.Equal(ToolboxReadinessStatus.Ready, old.Status, "Fully reviewed synthetic case.");
        CanonicalReviewContractTests.Reject(() => model.SetInclusion(new(model.Parcels[2].Id, ToolboxInclusion.Excluded,
            "examiner", Now, "reason", "ev", "test-v1"), "test-v1", false));
        var excluded = model.SetInclusion(new(model.Parcels[2].Id, ToolboxInclusion.Excluded, "examiner", Now,
            "Documented scope exclusion", "ev", "test-v1"), "test-v1", true);
        var reincluded = excluded.SetInclusion(new(model.Parcels[2].Id, ToolboxInclusion.Included, "examiner", Now.AddMinutes(1),
            "Reinclude", "ev", "test-v1"), "test-v1", true);
        TestAssert.False(old.IsCurrent(Evaluate(reincluded, policy)), "Reinclusion does not revive acceptance.");
        TestAssert.Equal(2, reincluded.InclusionHistory.Length, "Preserve inclusion history.");
        var unresolved = model.SetInclusion(new(model.Parcels[2].Id, ToolboxInclusion.Unresolved, "examiner", Now,
            "Review unresolved", "ev", "test-v1"), "test-v1", true);
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(unresolved, policy, context: Context().SelectParcels(model.Parcels.Take(2).Select(x => x.Id))).Status, "Selected two cannot hide third.");
        TestAssert.False(old.RemoteActionAllowed, "Local readiness never permits server changes.");
        var thirdReview = Reviews(policy, Scopes(model), model: model).First(x => x.GeometryFingerprints?.ContainsKey(model.Parcels[2].Id) == true);
        var incomplete = Evaluate(model, policy, Reviews(policy, Scopes(model), model: model)
            .Where(x => x.ResolutionFingerprint != thirdReview.ResolutionFingerprint).ToArray());
        TestAssert.Equal(2, incomplete.CurrentAccepted, "Track two current parcel dispositions while third blocks aggregate.");
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, incomplete.Status, "All included parcels must be reviewed.");
    }

    public static void SourceDefectForwardingDoesNotApproveGeometry()
    {
        var model = Model(); var policy = Policy(); var reviews = Reviews(policy, Scopes(model), model: model);
        var finding = Finding(model);
        var register = Register(model).RecordFinding(model, finding);
        reviews[0] = reviews[0] with { Outcome = ToolboxCheckOutcome.Failed, FindingId = finding.Id };
        var result = Evaluate(model, policy, reviews, register);
        TestAssert.Equal(ToolboxReadinessStatus.ReadyWithRecordedDefects, result.Status, "Normal next-stage forwarding.");
        TestAssert.False(result.GeometryApproved, "Defective geometry not approved.");
        TestAssert.Equal("normal_next_stage", result.Route, "No invented correction route.");
        var aliasReviews = reviews.Select(x => x with { EvidenceIds = ["EV"],
            EvidenceFingerprint = ToolboxReadinessEvaluator.EvidenceFingerprint(model, register, ["EV"]) }).ToArray();
        var aliasResult = Evaluate(model, policy, aliasReviews, register);
        TestAssert.Equal(ToolboxReadinessStatus.ReadyWithRecordedDefects, aliasResult.Status, "Evidence alias does not block authorized source-defect forwarding.");
        TestAssert.False(aliasResult.GeometryApproved, "Evidence alias never converts defective geometry to approved.");
        foreach (var origin in new[] { ToolboxFindingOrigin.ExtractionMismatch, ToolboxFindingOrigin.Mixed, ToolboxFindingOrigin.Unknown })
            TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(model, policy, reviews,
                Register(model).RecordFinding(model, Finding(model, origin))).Status, "Cannot bypass extraction/origin.");
        TestAssert.Equal(ToolboxReadinessStatus.ReadyWithRecordedDefects, Evaluate(model, policy, reviews,
            Register(model).RecordFinding(model, Finding(model, ToolboxFindingOrigin.Mixed, corrected: true))).Status, "Corrected extraction retains source defect.");
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(model, policy, reviews, register, authority: Authority(false)).Status, "Authority cannot be waived.");
        var changedScopes = Scopes(model, "input-2");
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(model, policy, Reviews(policy, changedScopes, model: model), register, scopes: changedScopes).Status, "Changed inputs stale source dispositions.");
    }

    public static void ReportOnlyPendingAndCumulativeReview()
    {
        var model = Model(); var policy = Policy(reportOnly: true); var reviews = Reviews(policy, Scopes(model), model: model);
        reviews[0] = reviews[0] with { Outcome = ToolboxCheckOutcome.Failed };
        var report = Evaluate(model, policy, reviews);
        TestAssert.Equal(ToolboxReadinessStatus.Ready, report.Status, "Report-only failure does not block forwarding.");
        TestAssert.False(report.GeometryApproved, "Report-only failure does not invent valid geometry.");
        var compare = Context(stage: ToolboxPreset.Compare); var requiredPolicy = Policy();
        var compareReviews = Reviews(requiredPolicy, Scopes(model), ToolboxPreset.Compare, model);
        compareReviews[0] = compareReviews[0] with { Outcome = ToolboxCheckOutcome.Pending, Reason = "LTD unavailable; reviewed source retained" };
        var register = Register(model, ToolboxPreset.Compare).RecordFinding(model, Finding(model, geometry: false));
        var finding = register.Findings[0];
        register = register.RecordFinding(model, finding with { History = finding.History.Add(new(ToolboxPreset.Compare,
            "examiner", Now.AddMinutes(1), "retained", "Compare retains Compute defect", "ev", "input-1", "test-v1", "test-v1")) });
        var result = Evaluate(model, requiredPolicy, compareReviews, register, compare);
        TestAssert.Equal(ToolboxReadinessStatus.Pending, result.Status, "Incomplete LTD review stays pending with reason.");
        TestAssert.Equal(2, result.Findings[0].History.Length, "Keep Compute and Compare history.");
        TestAssert.True(result.Reviews.Any(x => x.Outcome == ToolboxCheckOutcome.Passed), "Retain available comparison work.");
        CanonicalReviewContractTests.Reject(() => register.RecordFinding(model, finding));
        TestAssert.False(compare.GeometryAccess().Allowed, "Compare geometry read-only.");
    }

    public static void HumanSourceAbsenceManualCorrectionAndHistory()
    {
        var model = Model(); var register = Register(model);
        var review = new ToolboxHumanReview("ev", "raw source", "corrected tabular value", "manual",
            ToolboxPresence.Present, ToolboxAccuracy.Corrected, ToolboxPreset.Compare, "examiner", Now,
            "Correct extraction typo", "revision-1");
        var changed = register.RecordHumanReview(model, review, Authority());
        TestAssert.Equal(2, changed.HumanReviews.Length, "Retain raw/original and correction history.");
        TestAssert.Equal("raw source", changed.HumanReviews[1].RawValue, "No overwrite of original.");
        CanonicalReviewContractTests.Reject(() => register.RecordHumanReview(model, review, Authority(), geometryEvidence: true));
        CanonicalReviewContractTests.Reject(() => register.RecordHumanReview(model, review, Authority(), linkedSource: true));
        var absent = changed.RecordHumanReview(model, review with { Presence = ToolboxPresence.MissingFromSource,
            Accuracy = ToolboxAccuracy.Confirmed, Reason = "Human confirms source does not print value" }, Authority());
        TestAssert.Equal(ToolboxReadinessStatus.Ready, Evaluate(model, Policy(), register: absent).Status, "Confirmed source absence can be validly reviewed.");
        var uncertain = changed.RecordHumanReview(model, review with { Presence = ToolboxPresence.NotExtracted }, Authority());
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(model, Policy(), register: uncertain).Status, "Not extracted is not missing-from-source.");
        TestAssert.False(Evaluate(model, Policy(), register: changed).IsCurrent(Evaluate(model, Policy(), register: register)), "Evidence correction stales aggregate.");
        TestAssert.Equal(model.ParcelFingerprint(model.Parcels[0].Id, "v1"), model.ParcelFingerprint(model.Parcels[0].Id, "v1"), "Tabular correction leaves geometry fingerprint unchanged.");
    }

    public static void MissingStaleChecksAndGeometryChangesBlock()
    {
        var model = Model(3); var policy = Policy(); var reviews = Reviews(policy, Scopes(model), model: model);
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(model, policy, reviews.Skip(1).ToArray()).Status, "Missing required review.");
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(model, policy, reviews, scopes: Scopes(model, revision: "revision-2")).Status, "Old reviews stale.");
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(model, policy, reviews, scopes: Scopes(model).Where(x => x.ScopeId != model.Parcels[2].Id).ToArray()).Status, "Scope omission blocks even if others pass.");
        var root = model.Source.ToJson(); root["rows"]![0]!["easting"] = "11";
        var changed = model.ReAdapt(Context(), CanonicalReviewContract.Read(root));
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(changed, policy, reviews).Status, "Old geometry checks cannot approve changed geometry.");
        var oldDecision = Evaluate(model, policy, reviews);
        var changedDecision = Evaluate(changed, policy, reviews);
        var unaffectedId = model.Parcels[1].Id;
        TestAssert.True(oldDecision.ParcelDispositions.Single(x => x.ParcelId == unaffectedId)
            .IsCurrent(changedDecision.ParcelDispositions.Single(x => x.ParcelId == unaffectedId)), "Unrelated parcel disposition remains current.");
        TestAssert.False(Evaluate(model, policy).IsCurrent(Evaluate(changed, policy)), "Geometry change stales decision.");
        var history = Register(model).RecordDecision(Evaluate(model, policy)).RecordDecision(Evaluate(changed, policy));
        TestAssert.Equal(2, history.Decisions.Length, "Decision history retained.");
    }

    public static void WrongCaseUnverifiedPolicyAndManyParcelEvidence()
    {
        var model = Model(); var policy = Policy(); var register = Register(model);
        var foreignContext = Context("other-case");
        var foreign = ToolboxParcelModel.Adapt(foreignContext, model.Source, newImport: true);
        CanonicalReviewContractTests.Reject(() => ToolboxReadinessEvaluator.Evaluate(foreign, foreignContext,
            policy, Scopes(foreign), [], register, Authority(), GeometryToken, Now));
        var unknown = ToolboxReadinessEvaluator.Evaluate(model, Context(), policy, Scopes(model),
            Reviews(policy, Scopes(model), model: model), register, Authority(), GeometryToken, Now);
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, unknown.Status, "Unverified geometry policy cannot approve a case.");
        var root = CanonicalReviewContractTests.Sample(3);
        root["participants"] = new JsonObject { ["owner"] = new JsonObject { ["raw_name"] = "Original owner", ["reviewed_name"] = "Reviewed owner",
            ["parcel_ids"] = new JsonArray("group-1", "group-2"), ["source_evidence_id"] = "ev" } };
        root["evidence_links"] = new JsonArray(
            new JsonObject { ["evidence_id"] = "ev", ["parcel_group_id"] = "group-1", ["role"] = "owner" },
            new JsonObject { ["evidence_id"] = "ev", ["parcel_group_id"] = "group-2", ["role"] = "owner" });
        var linked = ToolboxParcelModel.Adapt(Context(), CanonicalReviewContract.Read(root), newImport: true);
        TestAssert.Equal(2, linked.ParcelsForScope(ToolboxScopeKind.Participant, "owner").Length, "One participant can link to two parcels.");
        TestAssert.Equal(2, linked.Source.Links.Length, "Shared evidence links retained without copying mutable facts.");
        root["participants"]!["owner"]!["parcel_ids"]!.AsArray().Add("foreign");
        CanonicalReviewContractTests.Reject(() => CanonicalReviewContract.Read(root));
    }

    public static void CanonicalTabularEditsInvalidateOnlyDependentChecks()
    {
        var root = CanonicalReviewContractTests.Sample(2);
        root["participants"] = new JsonObject
        {
            ["owner-1"] = new JsonObject { ["reviewed_name"] = "Owner A", ["parcel_ids"] = new JsonArray("group-1"), ["source_evidence_id"] = "ev" },
            ["owner-2"] = new JsonObject { ["reviewed_name"] = "Owner C", ["parcel_ids"] = new JsonArray("group-2"), ["source_evidence_id"] = "ev" }
        };
        root["identifier_pairs"] = new JsonObject
        {
            ["pair-1"] = new JsonObject { ["volume"] = "100", ["folio"] = "1", ["parcel_ids"] = new JsonArray("group-1"), ["source_evidence_id"] = "ev" }
        };
        root["survey_metadata"] = new JsonObject { ["parish"] = "Parish A" };
        var model = ToolboxParcelModel.Adapt(Context(), CanonicalReviewContract.Read(root), newImport: true);
        var policy = Policy(configure: json =>
        {
            foreach (var stage in new[] { "Compute", "Compare" })
            {
                json["items"]![1]!["stages"]![stage]!["checks"]![0]!["scope"] = "participant";
                if (stage == "Compute") json["items"]![2]!["stages"]![stage]!["checks"]![0]!["scope"] = "identifier_pair";
            }
        });
        var scopes = Scopes(model); var register = Register(model);
        var reviews = Reviews(policy, scopes, model: model, register: register);
        var old = Evaluate(model, policy, reviews, register, scopes: scopes);
        TestAssert.Equal(ToolboxReadinessStatus.Ready, old.Status, "Complete initial tabular review.");
        foreach (var field in new[] { "owner", "title", "metadata" })
        {
            var edited = model.Source.ToJson();
            if (field == "owner") edited["participants"]!["owner-1"]!["reviewed_name"] = "Owner B";
            if (field == "title") edited["identifier_pairs"]!["pair-1"]!["folio"] = "2";
            if (field == "metadata") edited["survey_metadata"]!["parish"] = "Parish B";
            var changed = model.ReAdapt(Context(), CanonicalReviewContract.Read(edited));
            var result = Evaluate(changed, policy, reviews, register, scopes: scopes);
            TestAssert.Equal(ToolboxReadinessStatus.Blocked, result.Status, "Old scoped tokens cannot hide canonical " + field + " edits.");
            TestAssert.False(old.IsCurrent(result), "Old aggregate is stale after canonical edit.");
            TestAssert.True(result.Reasons.Any(x => x.StartsWith("Consumed canonical values changed", StringComparison.Ordinal)), "Explicit canonical dependency diagnostic.");
            foreach (var check in ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute, scopes))
            {
                var review = reviews.FirstOrDefault(x => x.ResolutionFingerprint == check.ResolutionFingerprint);
                if (review is null) continue;
                if (review.GeometryCheck || check.ScopeId == "owner-2")
                    TestAssert.Equal(review.CanonicalInputFingerprint, ToolboxReadinessEvaluator.CanonicalInputFingerprint(changed,
                        check.ScopeKind, check.ScopeId, review.GeometryCheck), "Unrelated owner and geometry checks retain their inputs.");
            }
            TestAssert.Equal(ToolboxReadinessStatus.Ready, Evaluate(changed, policy, register: register, scopes: scopes).Status, "Fresh checks restore readiness.");
        }
        var unbound = reviews.Select(x => x with { CanonicalInputFingerprint = "" }).ToArray();
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(model, policy, unbound, register, scopes: scopes).Status, "Old unbound check format cannot be accepted.");
    }

    public static void RevokedGeometryVerificationStalesParcelDispositions()
    {
        var model = Model(3); var policy = Policy(); var register = Register(model); var scopes = Scopes(model);
        var reviews = Reviews(policy, scopes, model: model, register: register);
        var verified = Evaluate(model, policy, reviews, register);
        var revoked = ToolboxReadinessEvaluator.Evaluate(model, Context(), policy, scopes, reviews, register,
            Authority(), GeometryToken, Now, geometryPolicyVerified: false);
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, revoked.Status, "Revoked verification blocks readiness.");
        TestAssert.False(revoked.GeometryApproved, "Unverified policy never approves geometry.");
        TestAssert.False(verified.IsCurrent(revoked), "Aggregate acceptance becomes stale.");
        foreach (var prior in verified.ParcelDispositions)
            TestAssert.False(prior.IsCurrent(revoked.ParcelDispositions.Single(x => x.ParcelId == prior.ParcelId)), "Every parcel acceptance becomes stale.");
        TestAssert.Equal(ToolboxReadinessStatus.Ready, Evaluate(model, policy, reviews, register).Status, "Explicit re-verification permits reevaluation.");
    }

    public static void ResolvedFindingsNeedCurrentPassingRechecksAndKeepHistory()
    {
        var model = Model(); var policy = Policy(); var finding = Finding(model);
        var recorded = Register(model).RecordFinding(model, finding);
        var resolvedFinding = finding with { History = finding.History.Add(new(ToolboxPreset.Compute, "examiner", Now.AddMinutes(1),
            "resolved", "Corrected and rechecked source limitation", "ev", "input-1", "test-v1", "test-v1")) };
        var register = recorded.RecordFinding(model, resolvedFinding);
        var reviews = Reviews(policy, Scopes(model), model: model, register: register);
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(model, policy, reviews, register).Status, "Status text alone cannot resolve a finding.");
        reviews = reviews.Select(x => x.GeometryCheck ? x with { FindingId = finding.Id } : x).ToArray();
        var result = Evaluate(model, policy, reviews, register);
        TestAssert.Equal(ToolboxReadinessStatus.Ready, result.Status, "Resolved source defect is historical, not active.");
        TestAssert.True(result.GeometryApproved, "Current passing geometry recheck restores valid-geometry acceptance.");
        TestAssert.Equal(2, result.Findings[0].History.Length, "Retain original and resolution events.");
        TestAssert.True(result.Findings[0].GeometryDefect, "Do not erase historical defect classification.");
        TestAssert.Equal("recorded", recorded.Findings[0].History[0].Status, "Previous register remains immutable.");
        foreach (var outcome in new[] { ToolboxCheckOutcome.Failed, ToolboxCheckOutcome.Pending, ToolboxCheckOutcome.NotEvaluable })
            TestAssert.False(Evaluate(model, policy, reviews.Select(x => x.GeometryCheck ? x with { Outcome = outcome } : x).ToArray(), register).GeometryApproved,
                "Nonpassing recheck cannot approve resolution.");
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(model, policy, reviews, register, authority: Authority(false)).Status, "Unauthorized resolution stays blocked.");
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(model, policy, register: register, scopes: Scopes(model, "input-2")).Status, "Changed inputs stale resolution.");
        var edited = model.Source.ToJson(); edited["rows"]![0]!["easting"] = "11";
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(model.ReAdapt(Context(), CanonicalReviewContract.Read(edited)), policy, reviews, register).Status,
            "Old geometry proof cannot resolve changed geometry.");
        CanonicalReviewContractTests.Reject(() => register.RecordFinding(model, resolvedFinding with
        { History = resolvedFinding.History.Add(resolvedFinding.History[^1] with { Status = "invented" }) }));
        var reopened = register.RecordFinding(model, resolvedFinding with
        { History = resolvedFinding.History.Add(resolvedFinding.History[^1] with { Status = "reopened", Reason = "Source limitation found again" }) });
        TestAssert.Equal(ToolboxReadinessStatus.ReadyWithRecordedDefects, Evaluate(model, policy, reviews, reopened).Status, "Reopened defect becomes active again.");
        TestAssert.False(Evaluate(model, policy, reviews, reopened).GeometryApproved, "Reopened geometry defect not approved.");
        foreach (var origin in new[] { ToolboxFindingOrigin.Unknown, ToolboxFindingOrigin.Mixed, ToolboxFindingOrigin.ExtractionMismatch })
        {
            var unresolved = Finding(model, origin) with { History = resolvedFinding.History };
            var unsafeRegister = Register(model).RecordFinding(model, unresolved);
            TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(model, policy, reviews, unsafeRegister).Status,
                "Resolved status cannot waive unknown origin or uncorrected extraction.");
        }

        var tabularRoot = model.Source.ToJson();
        tabularRoot["participants"] = new JsonObject { ["owner"] = new JsonObject { ["parcel_ids"] = new JsonArray("group-1"),
            ["reviewed_name"] = "Corrected owner", ["source_evidence_id"] = "ev" } };
        var tabularModel = model.ReAdapt(Context(), CanonicalReviewContract.Read(tabularRoot));
        var tabularPolicy = Policy(configure: json => json["items"]![1]!["stages"]!["Compare"]!["checks"]![0]!["scope"] = "participant");
        var compare = Context(stage: ToolboxPreset.Compare);
        var tabularFinding = Finding(tabularModel, ToolboxFindingOrigin.ExtractionMismatch, corrected: true, geometry: false) with { RuleId = "M02" };
        var tabularRegister = Register(tabularModel, ToolboxPreset.Compare).RecordFinding(tabularModel, tabularFinding);
        tabularRegister = tabularRegister.RecordFinding(tabularModel, tabularFinding with
        { History = tabularFinding.History.Add(resolvedFinding.History[^1] with { Stage = ToolboxPreset.Compare }) });
        var tabularScopes = Scopes(tabularModel);
        var resolvedChecks = ToolboxReviewPolicyResolver.Resolve(tabularPolicy, ToolboxPreset.Compare, tabularScopes);
        var tabularReviews = Reviews(tabularPolicy, tabularScopes, ToolboxPreset.Compare, tabularModel, tabularRegister)
            .Select(x => resolvedChecks.Any(r => r.ItemId == "M02" && r.ResolutionFingerprint == x.ResolutionFingerprint)
                ? x with { FindingId = tabularFinding.Id } : x).ToArray();
        TestAssert.Equal(ToolboxReadinessStatus.Ready, Evaluate(tabularModel, tabularPolicy, tabularReviews, tabularRegister, compare, tabularScopes).Status,
            "Participant-scoped tabular correction can resolve in Compare without returning to Compute.");
    }

    public static void TraceOnlyChangesStaleAggregateAndPreserveHistory()
    {
        var model = Model(2); var policy = Policy(); var register = Register(model); var scopes = Scopes(model);
        var reviews = Reviews(policy, scopes, model: model, register: register);
        var old = Evaluate(model, policy, reviews, register, scopes: scopes);
        TestAssert.Equal(ToolboxReadinessStatus.Ready, old.Status, "Initially ready.");
        TestAssert.True(old.IsCurrent(Evaluate(model, policy, reviews, register, scopes: scopes)), "Unchanged inputs remain current.");
        var root = model.Source.ToJson(); root["source_traces"]![0]!["raw_text"] = "Changed source evidence only";
        var changed = model.ReAdapt(Context(), CanonicalReviewContract.Read(root));
        var current = Evaluate(changed, policy, reviews, register, scopes: scopes);
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, current.Status, "Stale trace proof blocks readiness.");
        TestAssert.False(old.Fingerprint == current.Fingerprint, "Actual consumed trace changes aggregate fingerprint.");
        TestAssert.False(old.IsCurrent(current), "Old case approval is stale even with reused caller tokens.");
        TestAssert.False(old.IsCurrent(old with { Status = ToolboxReadinessStatus.Blocked }), "Currency also binds status.");
        TestAssert.False(old.IsCurrent(old with { GeometryApproved = false }), "Currency also binds geometry approval.");
        var history = register.RecordDecision(old).RecordDecision(current);
        TestAssert.Equal(2, history.Decisions.Length, "Retain both immutable decision snapshots.");
        TestAssert.Equal(ToolboxReadinessStatus.Ready, Evaluate(changed, policy, register: register, scopes: scopes).Status,
            "Current human-reviewed check evidence recovers readiness.");
    }

    public static void ExcludedFindingResolutionValidatesEveryConsumedInput()
    {
        foreach (var geometry in new[] { true, false })
        {
            var root = CanonicalReviewContractTests.Sample(2);
            root["source_traces"]!.AsArray().Add(new JsonObject { ["evidence_id"] = "excluded-ev", ["source_id"] = "plan",
                ["page"] = 1, ["zone"] = "map", ["raw_text"] = "Original excluded evidence" });
            root["participants"] = new JsonObject { ["owner"] = new JsonObject { ["parcel_ids"] = new JsonArray("group-1"),
                ["reviewed_name"] = "Owner A", ["source_evidence_id"] = "excluded-ev" } };
            var model = ToolboxParcelModel.Adapt(Context(), CanonicalReviewContract.Read(root), newImport: true);
            var policy = Policy(configure: json => json["items"]![1]!["stages"]!["Compute"]!["checks"]![0]!["scope"] = "parcel");
            var scopes = Scopes(model); var id = model.Parcels[0].Id;
            var finding = Finding(model, geometry: geometry) with { RuleId = geometry ? "M01" : "M02",
                History = [Finding(model).History[0] with { EvidenceId = "excluded-ev" }] };
            var human = new ToolboxHumanReview("excluded-ev", "raw", "manually corrected", "manual", ToolboxPresence.Present,
                ToolboxAccuracy.Corrected, ToolboxPreset.Compute, "examiner", Now, "Reviewed correction", "revision-1");
            var register = Register(model).RecordHumanReview(model, human, Authority()).RecordFinding(model, finding);
            register = register.RecordFinding(model, finding with { History = finding.History.Add(finding.History[0] with
                { Status = "resolved", At = Now.AddMinutes(1), Reason = "Current passing recheck" }) });
            model = model.SetInclusion(new(id, ToolboxInclusion.Excluded, "examiner", Now, "Evidence-backed exclusion", "ev", "test-v1"), "test-v1", true);
            var requirement = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute, scopes)
                .Single(x => x.ItemId == finding.RuleId && x.ScopeId == id);
            var proof = Reviews(policy, scopes, model: model, register: register).Single(x => x.ResolutionFingerprint == requirement.ResolutionFingerprint)
                with { FindingId = finding.Id, EvidenceIds = ["excluded-ev"],
                    EvidenceFingerprint = ToolboxReadinessEvaluator.EvidenceFingerprint(model, register, ["excluded-ev"]) };
            ToolboxCheckReview[] WithProof(ToolboxParcelModel current, ToolboxExaminationRegister history, ToolboxCheckReview recheck) =>
                Reviews(policy, scopes, model: current, register: history)
                    .Select(x => x.ResolutionFingerprint == proof.ResolutionFingerprint ? recheck : x).ToArray();
            TestAssert.Equal(ToolboxReadinessStatus.Ready, Evaluate(model, policy, WithProof(model, register, proof), register, scopes: scopes).Status,
                "Excluded finding can retain genuinely current resolution.");
            var aliasProof = proof with { EvidenceIds = ["EXCLUDED-EV"],
                EvidenceFingerprint = ToolboxReadinessEvaluator.EvidenceFingerprint(model, register, ["EXCLUDED-EV"]) };
            TestAssert.Equal(ToolboxReadinessStatus.Ready, Evaluate(model, policy, WithProof(model, register, aliasProof), register, scopes: scopes).Status,
                "Fresh resolution evidence uses the canonical identity across aliases.");
            var aliasFinding = finding with { History = finding.History.Select(x => x with { EvidenceId = "EXCLUDED-EV" }).ToImmutableArray() };
            TestAssert.Equal(1, new ToolboxExaminationRegister().RecordFinding(model, aliasFinding).Findings.Length,
                "Scoped finding evidence accepts the same canonical identity.");
            foreach (var mutation in new[] { "trace", geometry ? "geometry" : "canonical", "human", "missing-evidence", "missing-canonical", "empty-evidence", "blank-actor", "failed", "missing-geometry" })
            {
                if (!geometry && mutation == "missing-geometry") continue;
                var edited = model.Source.ToJson(); var currentRegister = register; var currentProof = proof;
                if (mutation == "trace") edited["source_traces"]![1]!["raw_text"] = "Changed excluded trace";
                if (mutation == "geometry") edited["rows"]![0]!["easting"] = "11";
                if (mutation == "canonical") edited["participants"]!["owner"]!["reviewed_name"] = "Owner B";
                if (mutation == "human")
                {
                    currentRegister = register.RecordHumanReview(model, human with
                        { InputRevision = "revision-2", At = Now.AddMinutes(2), Reason = "New review revision" }, Authority());
                    currentProof = proof with { EvidenceFingerprint = ToolboxReadinessEvaluator.EvidenceFingerprint(model, currentRegister, ["excluded-ev"]) };
                }
                if (mutation == "missing-evidence") currentProof = proof with { EvidenceFingerprint = "" };
                if (mutation == "missing-canonical") currentProof = proof with { CanonicalInputFingerprint = "" };
                if (mutation == "empty-evidence") currentProof = proof with { EvidenceIds = [] };
                if (mutation == "blank-actor") currentProof = proof with { Actor = "" };
                if (mutation == "failed") currentProof = proof with { Outcome = ToolboxCheckOutcome.Failed };
                if (mutation == "missing-geometry") currentProof = proof with { GeometryFingerprints = null };
                var changed = model.ReAdapt(Context(), CanonicalReviewContract.Read(edited));
                var result = Evaluate(changed, policy, WithProof(changed, currentRegister, currentProof), currentRegister, scopes: scopes);
                TestAssert.Equal(ToolboxReadinessStatus.Blocked, result.Status, "Excluded resolution rejects " + mutation + " proof.");
                TestAssert.True(result.Reasons.Any(x => x.StartsWith("Finding resolution needs a current passing recheck", StringComparison.Ordinal)),
                    "Independent resolution validation, not an unrelated included check, catches " + mutation + ".");
                TestAssert.False(result.GeometryApproved, "No geometry approval from stale resolution.");
                TestAssert.Equal(2, result.Findings[0].History.Length, "Resolution history remains intact.");
                TestAssert.Equal("resolved", result.Findings[0].History[^1].Status, "Do not rewrite original resolution event.");
            }
        }
    }

    public static void MixedCaseEvidenceAliasesBindCanonicalTraceAndLatestHumanReview()
    {
        var model = Model(2); var policy = Policy(); var scopes = Scopes(model);
        var human = Register(model).HumanReviews[0] with { EvidenceId = "EV" };
        var register = new ToolboxExaminationRegister().RecordHumanReview(model, human, Authority());
        var reviews = Reviews(policy, scopes, model: model, register: register).Select(x => x with
            { EvidenceIds = ["EV"], EvidenceFingerprint = ToolboxReadinessEvaluator.EvidenceFingerprint(model, register, ["EV"]) }).ToArray();
        var old = Evaluate(model, policy, reviews, register, scopes: scopes);
        TestAssert.Equal(ToolboxReadinessStatus.Ready, old.Status, "Accepted evidence aliases remain usable.");
        TestAssert.Equal(ToolboxReadinessEvaluator.EvidenceFingerprint(model, register, ["ev"]),
            ToolboxReadinessEvaluator.EvidenceFingerprint(model, register, ["EV", "ev"]), "Alias casing/duplicates do not change consumed identity.");
        var root = model.Source.ToJson(); root["source_traces"]![0]!["raw_text"] = "Changed canonical trace";
        var changed = model.ReAdapt(Context(), CanonicalReviewContract.Read(root));
        var current = Evaluate(changed, policy, reviews, register, scopes: scopes);
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, current.Status, "Uppercase references cannot hide a changed lowercase trace.");
        TestAssert.False(old.IsCurrent(current), "Canonical trace consumption stales aggregate across aliases.");
        var corrected = register.RecordHumanReview(model, human with { EvidenceId = "ev", ReviewedValue = "New manual correction",
            At = Now.AddMinutes(1), Reason = "Later review through lowercase alias" }, Authority());
        TestAssert.False(old.IsCurrent(Evaluate(model, policy, reviews, corrected, scopes: scopes)), "Latest review under either alias is consumed.");
        TestAssert.Equal(2, corrected.HumanReviews.Length, "Keep both manual review events.");
        TestAssert.Equal(ToolboxReadinessStatus.Ready, Evaluate(model, policy, register: corrected, scopes: scopes).Status,
            "Refreshed checks consume the latest review consistently.");
    }

    public static void GlobalFactsInvalidateParcelTabularChecksButNotGeometry()
    {
        foreach (var collection in new[] { "participants", "identifier_pairs" })
        {
            var root = CanonicalReviewContractTests.Sample(2);
            root[collection] = new JsonObject
            {
                ["global"] = new JsonObject { ["reviewed_value"] = "Original", ["source_evidence_id"] = "ev" },
                ["empty"] = new JsonObject { ["reviewed_value"] = "No parcel links", ["parcel_ids"] = new JsonArray() },
                ["local"] = new JsonObject { ["reviewed_value"] = "Local", ["parcel_ids"] = new JsonArray("group-1") }
            };
            var model = ToolboxParcelModel.Adapt(Context(), CanonicalReviewContract.Read(root), newImport: true);
            var compare = Context(stage: ToolboxPreset.Compare);
            var policy = Policy(configure: json => json["items"]![1]!["stages"]!["Compare"]!["checks"]![0]!["scope"] = "parcel");
            var scopes = Scopes(model); var register = Register(model, ToolboxPreset.Compare);
            var reviews = Reviews(policy, scopes, ToolboxPreset.Compare, model, register);
            var old = Evaluate(model, policy, reviews, register, compare, scopes);
            TestAssert.Equal(ToolboxReadinessStatus.Ready, old.Status, "Initially complete Compare tabular review.");
            var kind = collection == "participants" ? ToolboxScopeKind.Participant : ToolboxScopeKind.IdentifierPair;
            TestAssert.Equal(2, model.ParcelsForScope(kind, "global").Length, "Absent links mean global applicability.");
            TestAssert.Equal(0, model.ParcelsForScope(kind, "empty").Length, "Explicit empty links do not mean global.");
            foreach (var fact in new[] { "global", "empty", "local" })
            {
                var edited = model.Source.ToJson(); edited[collection]![fact]!["reviewed_value"] = "Changed";
                var changed = model.ReAdapt(compare, CanonicalReviewContract.Read(edited));
                var current = Evaluate(changed, policy, reviews, register, compare, scopes);
                foreach (var parcel in model.Parcels)
                {
                    var shouldChange = fact == "global" || fact == "local" && parcel.SourceKey == "group-1";
                    var before = ToolboxReadinessEvaluator.CanonicalInputFingerprint(model, ToolboxScopeKind.Parcel, parcel.Id);
                    var after = ToolboxReadinessEvaluator.CanonicalInputFingerprint(changed, ToolboxScopeKind.Parcel, parcel.Id);
                    TestAssert.Equal(shouldChange, before != after, "Parcel consumes exactly global or linked " + collection + ".");
                    TestAssert.Equal(ToolboxReadinessEvaluator.CanonicalInputFingerprint(model, ToolboxScopeKind.Parcel, parcel.Id, true),
                        ToolboxReadinessEvaluator.CanonicalInputFingerprint(changed, ToolboxScopeKind.Parcel, parcel.Id, true), "Geometry-only inputs unchanged.");
                    TestAssert.Equal(model.ParcelFingerprint(parcel.Id, GeometryToken), changed.ParcelFingerprint(parcel.Id, GeometryToken), "Geometry untouched in Compare.");
                    if (shouldChange)
                        TestAssert.False(old.ParcelDispositions.Single(x => x.ParcelId == parcel.Id)
                            .IsCurrent(current.ParcelDispositions.Single(x => x.ParcelId == parcel.Id)), "Affected parcel decision is stale.");
                }
                if (fact == "global")
                {
                    TestAssert.Equal(ToolboxReadinessStatus.Blocked, current.Status, "Unlinked global edit blocks consuming old reviews.");
                    TestAssert.False(old.IsCurrent(current), "Aggregate is stale.");
                    TestAssert.Equal(ToolboxReadinessStatus.Ready, Evaluate(changed, policy, register: register, context: compare, scopes: scopes).Status,
                        "Fresh Compare checks recover without geometry editing.");
                }
            }
        }
    }

    public static void ExistingBindingBundlesAdaptWithoutClaimingCompletion()
    {
        JsonObject Read(string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "ToolboxBindings", name)))!.AsObject();
        var policy = ToolboxBindingFixtureTests.TestPolicy(Read("binding-catalog.v1.json"));
        foreach (var name in new[] { "single-parcel", "multi-parcel-cad-map", "manual-geometry-recovery" })
        {
            var bundle = Read(name + ".bundle.json");
            var root = new JsonObject { ["transaction_number"] = bundle["case"]!["transaction_number"]!.DeepClone(),
                ["source_inventory"] = new JsonArray(), ["source_traces"] = new JsonArray(),
                ["parcel_groups"] = new JsonArray(), ["rows"] = new JsonArray(), ["segments"] = new JsonArray(),
                ["participants"] = bundle["participants"]!.DeepClone(), ["identifier_pairs"] = bundle["identifier_pairs"]!.DeepClone(),
                ["fixture_evidence"] = bundle.DeepClone() };
            foreach (var (id, value) in bundle["documents"]!.AsObject())
                root["source_inventory"]!.AsArray().Add(new JsonObject { ["source_id"] = id, ["source_role"] = "st_surveyplan", ["raw_document"] = value!.DeepClone() });
            foreach (var (id, value) in bundle["source_evidence"]!.AsObject())
                root["source_traces"]!.AsArray().Add(new JsonObject { ["evidence_id"] = id, ["source_id"] = value!["document_id"]!.DeepClone(),
                    ["page"] = value["page"]!.DeepClone(), ["zone"] = value["zone"]!.DeepClone(), ["raw"] = value.DeepClone() });
            foreach (var (id, value) in bundle["geometry"]!["parcels"]!.AsObject())
            {
                root["parcel_groups"]!.AsArray().Add(new JsonObject { ["parcel_group_id"] = id });
                var index = 0;
                foreach (var row in value!["points"]!.AsArray().Cast<JsonObject>())
                {
                    var copy = (JsonObject)row.DeepClone(); copy["parcel_group_id"] = id; copy["sequence_in_group"] = ++index;
                    root["rows"]!.AsArray().Add(copy);
                }
                index = 0;
                foreach (var segment in value["segments"]!.AsArray().Cast<JsonObject>())
                {
                    var copy = (JsonObject)segment.DeepClone(); copy["parcel_group_id"] = id; copy["sequence"] = ++index;
                    root["segments"]!.AsArray().Add(copy);
                }
            }
            var canonical = CanonicalReviewContract.Read(root);
            var ids = new Queue<string>(canonical.Parcels.Select(x => x.Key).Concat(canonical.Rows.Select(x => x.Key)).Concat(canonical.Segments.Select(x => x.Key)));
            var context = Context(bundle["case"]!["case_id"]!.GetValue<string>(), canonical.TransactionNumber);
            var model = ToolboxParcelModel.Adapt(context, canonical, newImport: true, allocateId: () => ids.Dequeue());
            TestAssert.Equal(bundle["expectations"]!["parcel_count"]!.GetValue<int>(), model.Parcels.Length, "Reuse all fixture parcels.");
            var outcome = ToolboxReadinessEvaluator.Evaluate(model, context, policy, ToolboxBindingFixtureTests.Scopes(bundle),
                [], new(), new("examiner", "fixture-v1", true), "synthetic-unverified-crs", Now);
            TestAssert.Equal(ToolboxReadinessStatus.Blocked, outcome.Status, "Unresolved bindings cannot become complete.");
            TestAssert.True(outcome.Reasons.Any(x => x.Contains("Unresolved binding", StringComparison.Ordinal)), "Explicit binding gaps retained.");
        }
    }
}
