using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.Workflow.Review.Canonical;
using ParcelWorkflowAddIn.Workflow.Toolbox.Model;
using ParcelWorkflowAddIn.Workflow.Toolbox.Persistence;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static partial class ToolboxReconciliationTests
{
    private static ToolboxHydratedWorkspace Stored(ToolboxReconciliationSnapshot state, ToolboxCheckReview[]? reviews = null,
        bool compareFingerprint = true)
    {
        var codec = new ToolboxWorkspaceStorageCodec();
        var captured = codec.Capture(new(state.Model, state.Register, state, (reviews ?? []).ToImmutableArray()));
        TestAssert.True(captured.Success, "B capture: " + string.Join(",", captured.Diagnostics.Select(d => d.Code)));
        var encoded = codec.Encode(captured.Value!); TestAssert.True(encoded.Success, "B encode.");
        var decoded = codec.Decode(encoded.Value!, state.Model.Case);
        TestAssert.True(decoded.Success, "B decode: " + string.Join(",", decoded.Diagnostics.Select(d => d.Code)));
        if (compareFingerprint) TestAssert.Equal(state.Fingerprint, decoded.Value!.Reconciliation!.Fingerprint, "B reconciliation fingerprint unchanged.");
        return decoded.Value!;
    }

    public static void StorageBReadinessAndInvalidation()
    {
        var policy = Policy(); var initial = Start(3, policy); var reviews = Reviews(initial, policy); var before = Evaluate(initial, policy, reviews);
        initial = initial with { Register = initial.Register.RecordDecision(before) };
        var loaded = Stored(initial, reviews); var after = Evaluate(loaded.Reconciliation!, policy, loaded.CurrentCheckReviews.ToArray());
        TestAssert.Equal(before.Fingerprint, after.Fingerprint, "Readiness parity with identical external inputs.");
        TestAssert.Equal(before.Status, after.Status, "Readiness unchanged.");
        TestAssert.Equal(before.At, loaded.Register.Decisions[0].At, "No new decision time.");
        var json = loaded.Model.Source.ToJson(); json["rows"]![0]!["easting"] = "changed";
        var changed = loaded.Reconciliation! with { Model = loaded.Model.ReAdapt(Context(), CanonicalReviewContract.Read(json)) };
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(changed, policy, loaded.CurrentCheckReviews.ToArray()).Status, "Changed geometry invalidates old proof after hydration.");
        var hashes = loaded.Model.Relations!.SourceHashes.SetItem("plan", "new-hash");
        changed = loaded.Reconciliation! with { Model = loaded.Model.WithRelations(loaded.Model.Relations with { SourceHashes = hashes }) };
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(changed, policy, loaded.CurrentCheckReviews.ToArray()).Status, "Supporting source invalidation survives hydration.");
        TestAssert.False(Context(false).GeometryAccess().Allowed, "No hydrated geometry permission.");
    }

    public static void StorageBLineageAuditAndSharedArchives()
    {
        var policy = Policy(); var initial = Start(policy: policy);
        var split = ToolboxReconciliation.Accept(initial, LineageProposal(initial, policy, 2), Context(), policy, Authority);
        var result = ToolboxReconciliation.Accept(split, ToolboxReconciliation.Seal(Proposal(split, policy, split.Model.Source)
            with { OperationId = "second", ResultRevision = "second-revision" }), Context(), policy, Authority);
        var codec = new ToolboxWorkspaceStorageCodec(); var doc = codec.Capture(new(result.Model, result.Register, result)).Value!;
        TestAssert.Equal(3, doc.Models.Length, "Repeated ancestry stored as three unique states.");
        var loaded = Stored(result).Reconciliation!;
        TestAssert.Equal(result.AcceptedDispositions[0].AcceptedAt, loaded.AcceptedDispositions[0].AcceptedAt, "Audit UTC time survives.");
        TestAssert.Equal(result.AcceptedDispositions[0].Dispositions[0], loaded.AcceptedDispositions[0].Dispositions[0], "Audit choices survive.");
        TestAssert.True(ReferenceEquals(loaded.Model.Relations!.Archives[0], loaded.Model.Relations.Archives[1].Relations!.Archives[0]), "Shared archive reconstructed once.");
        TestAssert.Equal(initial.Model.IdentityMap.Ids["parcel:group-1"], loaded.Lineage[0].Proposal.ParentIds[0], "Retired parent identity survives.");
        var missing = doc with { Models = doc.Models.RemoveAt(0) };
        TestAssert.False(codec.Decode(ToolboxStorageJson.Bytes(missing), doc.Case).Success, "Missing historical record rejects entire bundle.");
        var root = doc.Models.Single(m => m.SnapshotId == doc.RootModelId);
        var cyclic = root with { Relations = root.Relations! with { ArchiveModelIds = [root.SnapshotId] } };
        var bad = doc with { Models = doc.Models.Replace(root, cyclic) };
        TestAssert.Equal("cyclic_archive", codec.Decode(ToolboxStorageJson.Bytes(bad), doc.Case).Diagnostics[0].Code, "Cycle rejects before content-ID checks.");
        var audit = result.AcceptedDispositions[0] with { Digest = "wrong" };
        bad = doc with { Reconciliation = doc.Reconciliation! with { AcceptedDispositions = doc.Reconciliation.AcceptedDispositions.SetItem(0, audit) } };
        TestAssert.Equal("inconsistent_state", codec.Decode(ToolboxStorageJson.Bytes(bad), doc.Case).Diagnostics[0].Code, "Audit/operation mismatch rejected.");
    }

    public static void StoragePolicyBytesAndTrust()
    {
        var policy = Policy(); var state = Start(policy: policy); var bytes = Encoding.UTF8.GetBytes(policy.JsonSnapshot);
        var codec = new ToolboxWorkspaceStorageCodec(); var doc = codec.Capture(new(state.Model, state.Register, state,
            PolicyAssets: [new(policy.Pin, bytes)])).Value!;
        TestAssert.True(doc is not null, "Pinned policy bytes captured.");
        var raw = ToolboxStorageJson.Bytes(doc);
        var untrusted = codec.Decode(raw, state.Model.Case);
        TestAssert.True(untrusted.Success && untrusted.Value!.TrustedPolicies.Length == 0, "A stored pin cannot authorize itself.");
        TestAssert.True(untrusted.Diagnostics.Any(d => d.Code == "policy_untrusted"), "Trust diagnostic.");
        var trusted = codec.Decode(raw, state.Model.Case, [policy.Pin]);
        TestAssert.True(trusted.Success && trusted.Value!.TrustedPolicies.Length == 1, "Existing loader uses external trust.");
        var payloadId = doc!.PolicyAssets[0].PayloadId;
        TestAssert.True(bytes.SequenceEqual(trusted.Value!.PayloadBytes[payloadId]), "Exact original policy bytes survive.");
        var wrong = codec.Decode(raw, state.Model.Case, [policy.Pin with { Sha256 = new string('0', 64) }]);
        TestAssert.False(wrong.Success, "External pin mismatch never yields active policy.");
        var pending = codec.Capture(new(state.Model, state.Register, state, PendingProposal: Proposal(state, policy, state.Model.Source)));
        TestAssert.Equal("unsupported_capture", pending.Diagnostics[0].Code, "Pending proposals explicitly unsupported.");
    }

    public static void StorageBlockedAndOptionalState()
    {
        var policy = Policy(); var original = Shared(policy); var boundary = original.Model.Relations!.Boundaries[0];
        foreach (var points in new[] { default(ImmutableArray<string>), ImmutableArray<string>.Empty })
        {
            var relations = original.Model.Relations with { Boundaries = [boundary with { PointIds = points, SemanticsVerified = false }],
                Dependencies = original.Model.Relations.Dependencies with { Complete = false }, ReadinessBarriers = ["retained barrier"] };
            var state = original with { Model = original.Model.WithRelations(relations) };
            var loaded = Stored(state, compareFingerprint: !points.IsDefault);
            TestAssert.Equal(points.IsDefault, loaded.Model.Relations!.Boundaries[0].PointIds.IsDefault, "Default/empty optional points remain distinct.");
            TestAssert.False(loaded.Model.Relations.Dependencies.Complete, "Incomplete coverage is representable.");
            TestAssert.False(loaded.Model.Relations.Boundaries[0].SemanticsVerified, "Unverified geometry not promoted.");
            if (!points.IsDefault) TestAssert.Equal(ToolboxReadinessStatus.Blocked, Evaluate(loaded.Reconciliation!, policy).Status, "Blocked state stays blocked.");
            else
            {
                // The delivered domain hash cannot serialize default ImmutableArray; storage must not silently normalize it.
                TestAssert.True(DefaultHashFails(state) && DefaultHashFails(loaded.Reconciliation!), "Existing domain limitation preserved for inspectable default points.");
            }
        }
        static bool DefaultHashFails(ToolboxReconciliationSnapshot state)
        { try { _ = state.Fingerprint; return false; } catch (InvalidOperationException) { return true; } }
    }

    public static void StorageDeepArchivesAndProjectionBudgets()
    {
        var policy = Policy(); var state = Start(policy: policy); var previous = state.Model; var baseModel = previous;
        for (var i = 0; i < 80; i++)
            previous = previous.WithRelations(previous.Relations! with { Archives = [previous] });
        state = state with { Model = previous };
        var codec = new ToolboxWorkspaceStorageCodec(); var captured = codec.Capture(new(state.Model, state.Register, state));
        TestAssert.True(captured.Success, "Deep flat-reference archive graph is supported.");
        TestAssert.Equal(81, captured.Value!.Models.Length, "Each unique snapshot encoded once.");
        var bytes = ToolboxStorageJson.Bytes(captured.Value);
        var single = codec.Capture(new(baseModel, state.Register, state with { Model = baseModel })).Value!;
        TestAssert.True(bytes.Length < ToolboxStorageJson.Bytes(single).Length * 82L,
            "Archive size bounded by unique records, not recursive/exponential: " + bytes.Length);
        var decoded = codec.Decode(bytes, state.Model.Case);
        TestAssert.True(decoded.Success, "Iterative archive hydration.");
        var n = decoded.Value!.Model; var depth = 0;
        while (n.Relations!.Archives.Length > 0) { depth++; n = n.Relations.Archives[0]; }
        TestAssert.Equal(80, depth, "Deep ancestry survives.");
        TestAssert.False(new ToolboxWorkspaceStorageCodec(new(Models: 80)).Decode(bytes, state.Model.Case).Success, "Unique model cap enforced.");
        var payloadBytes = Convert.FromBase64String(captured.Value.Payloads[0].Utf8Base64).Length;
        var tooSmall = codec.Decode(bytes, state.Model.Case);
        TestAssert.True(tooSmall.Success, "Baseline shared payload loads.");
        var limited = new ToolboxWorkspaceStorageCodec(new(ProjectionBytes: payloadBytes * 80L)).Decode(bytes, state.Model.Case);
        TestAssert.Equal("limit_exceeded", limited.Diagnostics[0].Code, "Every materialized shared payload charged.");
    }

    public static void StorageGraphCorruptionAndLimits()
    {
        var policy = Policy(); var state = Start(3, policy); var codec = new ToolboxWorkspaceStorageCodec();
        var doc = codec.Capture(new(state.Model, state.Register, state)).Value!;
        ToolboxWorkspaceStorageDocument Change(ToolboxDependencyInventory graph)
        {
            var model = doc.Models[0] with { Relations = doc.Models[0].Relations! with { Dependencies = graph } };
            model = model with { SnapshotId = ToolboxWorkspaceStorageCodec.ModelId(model) };
            return doc with { Models = [model], RootModelId = model.SnapshotId,
                Reconciliation = doc.Reconciliation! with { RootModelId = model.SnapshotId } };
        }
        var graph = state.Model.Relations!.Dependencies;
        foreach (var changed in new[]
        {
            graph with { Nodes = graph.Nodes.Add(graph.Nodes[0]) },
            graph with { Edges = graph.Edges.Add(new("unknown", graph.Nodes[0].Id)) },
            graph with { Edges = graph.Edges.Add(new("artifact", "case")) },
            graph with { Nodes = graph.Nodes.SetItem(0, graph.Nodes[0] with { DecisionParcelId = state.Model.Parcels[0].Id }) }
        }) TestAssert.False(codec.Decode(ToolboxStorageJson.Bytes(Change(changed)), state.Model.Case).Success, "Invalid graph never exposes partial workspace.");
        var unknownKind = JsonNode.Parse(ToolboxStorageJson.Bytes(doc))!.AsObject();
        unknownKind["models"]![0]!["relations"]!["dependencies"]!["nodes"]![0]!["kind"] = "future_kind";
        TestAssert.False(codec.Decode(Encoding.UTF8.GetBytes(unknownKind.ToJsonString()), state.Model.Case).Success, "Unknown dependency kind rejected before hashing.");
        var raw = ToolboxStorageJson.Bytes(doc);
        var denied = new ToolboxWorkspaceStorageCodec(new(DependencyEdges: graph.Edges.Length - 1)).Decode(raw, state.Model.Case);
        TestAssert.Equal("limit_exceeded", denied.Diagnostics[0].Code, "Exact graph edge budget enforced before hydration.");
        var review = Reviews(state, policy)[0] with { GeometryFingerprints = ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase) };
        var unsupported = codec.Capture(new(state.Model, state.Register, state, [review]));
        TestAssert.Equal("unsupported_capture", unsupported.Diagnostics[0].Code, "Non-profile dictionary comparer is rejected, not silently changed.");
    }

    public static void StorageFindingScopesAndHistoricalAudit()
    {
        var policy = Policy(); var initial = WithFinding(Start(policy: policy));
        var finding = initial.Register.Findings[0] with { ParcelIds = [] };
        initial = initial with { Register = new ToolboxExaminationRegister().RecordFinding(initial.Model, finding) };
        var split = ToolboxReconciliation.Accept(initial, LineageProposal(initial, policy, 2, ToolboxApplicabilityDisposition.NotApplicable), Context(), policy, Authority);
        var loaded = Stored(split);
        TestAssert.Equal(0, loaded.Model.FindingParcels(loaded.Register.Findings[0]).Length, "Latest case-wide NotApplicable retained.");
        TestAssert.Equal(finding.History[0], loaded.Register.Findings[0].History[0], "Finding event original scope/version/time untouched.");
        TestAssert.Equal(Evaluate(split, policy).Fingerprint, Evaluate(loaded.Reconciliation!, policy).Fingerprint,
            "Recorded applicability currency remains equivalent, including originally missing human checks.");
    }

    public static void StorageRejectsForgedHistoricalReferences()
    {
        var policy = Policy(); var initial = Start(policy: policy);
        var split = ToolboxReconciliation.Accept(initial, LineageProposal(initial, policy, 2), Context(), policy, Authority);
        var codec = new ToolboxWorkspaceStorageCodec(); var doc = codec.Capture(new(split.Model, split.Register, split)).Value!;
        ToolboxWorkspaceStorageDocument Root(ToolboxStoredRelations relations, ToolboxStoredReconciliation? reconciliation = null)
        {
            var old = doc.Models.Single(m => m.SnapshotId == doc.RootModelId); var changed = old with { Relations = relations };
            changed = changed with { SnapshotId = ToolboxWorkspaceStorageCodec.ModelId(changed) };
            return doc with { Models = doc.Models.Replace(old, changed), RootModelId = changed.SnapshotId,
                Reconciliation = (reconciliation ?? doc.Reconciliation!) with { RootModelId = changed.SnapshotId } };
        }
        var relation = doc.Models.Single(m => m.SnapshotId == doc.RootModelId).Relations!;
        var lineage = relation.Lineage[0];
        foreach (var bad in new[]
        {
            lineage with { Proposal = lineage.Proposal with { ParentIds = [lineage.ChildIds[0]] } },
            lineage with { Proposal = lineage.Proposal with { EvidenceId = "unknown" } },
            lineage with { ChildIds = [lineage.ChildIds[0], lineage.ChildIds[0]] }
        })
        {
            var modified = Root(relation with { Lineage = [bad] }, doc.Reconciliation! with { Lineage = [bad] });
            TestAssert.False(codec.Decode(ToolboxStorageJson.Bytes(modified), doc.Case).Success, "Forged lineage closure rejected.");
        }
        var applicability = relation.Applicability[0];
        foreach (var bad in new[]
        {
            applicability with { RecordId = "invented" }, applicability with { TargetParcelKeys = ["invented"] },
            applicability with { TargetParcelKeys = [applicability.TargetParcelKeys[0], applicability.TargetParcelKeys[0]] }
        }) TestAssert.False(codec.Decode(ToolboxStorageJson.Bytes(Root(relation with { Applicability = [bad] })), doc.Case).Success,
            "Forged or aliased applicability rejected without dropping valid history.");
        var audit = doc.Reconciliation!.AcceptedDispositions[0]; var choice = audit.Dispositions.First(c => c.Kind != "global");
        foreach (var bad in new[] { choice with { OldId = "invented" }, choice with { IncomingKey = "invented" },
            choice with { OutputKey = "invented" }, choice with { EvidenceId = "invented" } })
        {
            var altered = audit with { Dispositions = audit.Dispositions.Replace(choice, bad) };
            var modified = doc with { Reconciliation = doc.Reconciliation with { AcceptedDispositions = [altered] } };
            TestAssert.False(codec.Decode(ToolboxStorageJson.Bytes(modified), doc.Case).Success, "Accepted audit references validated.");
        }
        var emptyParent = doc with { Reconciliation = doc.Reconciliation with { AcceptedDispositions = [audit with { ParentRevision = "" }] } };
        TestAssert.False(codec.Decode(ToolboxStorageJson.Bytes(emptyParent), doc.Case).Success, "Blank audit parent rejected.");
        var duplicateOp = doc.Reconciliation.Operations[0] with { OperationId = doc.Reconciliation.Operations[0].OperationId.ToUpperInvariant() };
        var duplicateAudit = audit with { OperationId = duplicateOp.OperationId };
        var aliased = doc with { Reconciliation = doc.Reconciliation with {
            Operations = doc.Reconciliation.Operations.Add(duplicateOp), AcceptedDispositions = doc.Reconciliation.AcceptedDispositions.Add(duplicateAudit) } };
        TestAssert.False(codec.Decode(ToolboxStorageJson.Bytes(aliased), doc.Case).Success, "Replay operation aliases cannot coexist.");
    }

    public static void StorageCaptureAggregatePayloadLimit()
    {
        var policy = Policy(); var state = Start(policy: policy); var json = state.Model.Source.ToJson();
        json["new_provider_field"] = "Different snapshot retains another immutable payload.";
        var changed = state.Model.ReAdapt(Context(), CanonicalReviewContract.Read(json));
        changed = changed.WithRelations(changed.Relations! with { Archives = [state.Model] });
        state = state with { Model = changed };
        var bytes = Encoding.UTF8.GetByteCount(state.Model.Relations!.Archives[0].Source.JsonSnapshot);
        var result = new ToolboxWorkspaceStorageCodec(new(PayloadBytes: bytes)).Capture(new(state.Model, state.Register, state));
        TestAssert.Equal("limit_exceeded", result.Diagnostics[0].Code, "Unique capture payload bytes budget checked before retaining second base64 asset.");
        var codec = new ToolboxWorkspaceStorageCodec(); var doc = codec.Capture(new(state.Model, state.Register, state));
        TestAssert.True(doc.Success && doc.Value!.Payloads.Length == 2, "Both unique payloads retained within normal bounds.");
    }

    public static void StorageCandidateOnlyApplicabilityEvidence()
    {
        var policy = Policy(); var initial = Start(policy: policy); var proposal = LineageProposal(initial, policy, 2);
        var json = proposal.Batch.Candidate.ToJson(); json["source_traces"]!.AsArray().Add(new JsonObject {
            ["evidence_id"] = "candidate-only", ["source_id"] = "plan", ["page"] = 2, ["zone"] = "title" });
        var candidate = CanonicalReviewContract.Read(json);
        proposal = ToolboxReconciliation.Seal(proposal with { Batch = Batch(initial, candidate),
            Applicability = proposal.Applicability.Select(a => a with { EvidenceId = "candidate-only" }).ToImmutableArray(),
            Dispositions = proposal.Dispositions.Select(c => c with { EvidenceId = "candidate-only" }).ToImmutableArray() });
        var accepted = ToolboxReconciliation.Accept(initial, proposal, Context(), policy, Authority);
        TestAssert.False(accepted.Model.Source.HasEvidence("candidate-only"), "Global retained source intentionally lacks candidate-only evidence.");
        var loaded = Stored(accepted);
        TestAssert.True(loaded.Reconciliation!.Reruns[0].Candidate.HasEvidence("candidate-only"), "Candidate-only provenance retained.");
        TestAssert.Equal("candidate-only", loaded.Model.Relations!.Applicability[0].EvidenceId, "Applicability reference resolves to retained candidate, not refreshed current source.");
    }

    public static void StorageDirectSourceAliasAndAuditShapeGuards()
    {
        var policy = Policy(); var initial = Start(policy: policy); var json = initial.Model.Source.ToJson();
        json["source_inventory"]!.AsArray().Add(new JsonObject { ["source_id"] = "other", ["source_role"] = "title" });
        var source = CanonicalReviewContract.Read(json);
        var hashes = ImmutableDictionary<string, string>.Empty.Add("plan", "same").Add("PLAN", "same");
        var rejected = false;
        try { _ = ToolboxParcelModel.Restore(initial.Model.Case, source, initial.Model.IdentityMap, initial.Model.Inclusions,
            initial.Model.InclusionHistory, initial.Model.LinkedSources, initial.Model.Relations! with { SourceHashes = hashes }); }
        catch (ToolboxStorageException e) { rejected = e.Code is "duplicate_identity" or "missing_reference"; }
        TestAssert.True(rejected, "Direct factory cannot mask absent source with equal-valued aliases.");
        var accepted = ToolboxReconciliation.Accept(initial, Proposal(initial, policy, initial.Model.Source), Context(), policy, Authority);
        var codec = new ToolboxWorkspaceStorageCodec(); var doc = codec.Capture(new(accepted.Model, accepted.Register, accepted)).Value!;
        var audit = doc.Reconciliation!.AcceptedDispositions[0]; var row = audit.Dispositions.First(c => c.Kind == "row");
        foreach (var bad in new[] { row with { OldId = null, IncomingKey = null, OutputKey = null },
            row with { Choice = ToolboxValueChoice.Replace, IncomingKey = null }, row with { Choice = ToolboxValueChoice.Retire },
            row with { OldId = initial.Model.Parcels[0].Id } })
        {
            var invalid = doc with { Reconciliation = doc.Reconciliation with {
                AcceptedDispositions = [audit with { Dispositions = audit.Dispositions.Replace(row, bad) }] } };
            TestAssert.False(codec.Decode(ToolboxStorageJson.Bytes(invalid), doc.Case).Success, "Choice shape and kind-specific old IDs checked.");
        }
        var global = audit.Dispositions.Single(c => c.Kind == "global");
        foreach (var choice in new[] { ToolboxValueChoice.Unresolved, ToolboxValueChoice.New, ToolboxValueChoice.Retire })
        {
            var invalid = doc with { Reconciliation = doc.Reconciliation with {
                AcceptedDispositions = [audit with { Dispositions = audit.Dispositions.Replace(global, global with { Choice = choice }) }] } };
            TestAssert.False(codec.Decode(ToolboxStorageJson.Bytes(invalid), doc.Case).Success, "Global audit choices bounded.");
        }
        var duplicate = doc with { Reconciliation = doc.Reconciliation with {
            AcceptedDispositions = [audit with { Dispositions = audit.Dispositions.Add(row) }] } };
        TestAssert.False(codec.Decode(ToolboxStorageJson.Bytes(duplicate), doc.Case).Success, "Duplicated audit references rejected.");
        foreach (var choices in new[] { ImmutableArray<ToolboxCandidateDisposition>.Empty, audit.Dispositions.Remove(global),
            audit.Dispositions.Replace(global, global with { ExplicitIdentity = false }) })
        {
            var invalid = doc with { Reconciliation = doc.Reconciliation with { AcceptedDispositions = [audit with { Dispositions = choices }] } };
            TestAssert.False(codec.Decode(ToolboxStorageJson.Bytes(invalid), doc.Case).Success, "Exactly one explicit global audit is required.");
        }
    }

    public static void StorageSplitMergeRegeneratedHistory()
    {
        var policy = Policy(); var initial = Start(policy: policy);
        var split = ToolboxReconciliation.Accept(initial, LineageProposal(initial, policy, 2), Context(), policy, Authority);
        var source = CanonicalReviewContract.Read(CanonicalReviewContractTests.Sample());
        var proposal = Proposal(split, policy, source);
        var lineage = new ToolboxParcelLineage("merge-lineage", split.Model.Case, ToolboxLineageKind.Merge,
            split.Revision, "merge-revision", split.Model.Parcels.Select(p => p.Id).ToImmutableArray(), ["group-1"],
            "examiner", "Reviewed merge", "ev", ImmutableDictionary<string, ToolboxInclusion>.Empty.Add("group-1", ToolboxInclusion.Included));
        var links = split.Register.HumanReviews.Select(ToolboxLineageValidator.CorrectionId)
            .Concat(ToolboxStorageValidation.History(split.Model).SelectMany(ToolboxLineageValidator.ManualEntityIds)).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => new ToolboxHistoryApplicability(id, split.Revision, ["group-1"], ToolboxApplicabilityDisposition.Retained,
                "examiner", "Historical correction mapped to merged result", "ev")).ToImmutableArray();
        var projected = ToolboxParcelModel.Adapt(Context(), source, newImport: true); var graph = Graph(projected, policy);
        var aliases = projected.IdentityMap.Ids.ToDictionary(p => p.Value, p => "@" + p.Key);
        graph = graph with { Nodes = graph.Nodes.Select(n => n with {
            ReferenceId = aliases.TryGetValue(n.ReferenceId, out var alias) ? alias : n.ReferenceId,
            CheckBinding = n.CheckBinding is { } b && aliases.TryGetValue(b.ScopeId, out var scoped) ? b with { ScopeId = scoped } : n.CheckBinding }).ToImmutableArray() };
        proposal = ToolboxReconciliation.Seal(proposal with { OperationId = "merge-op", ResultRevision = "merge-revision",
            Dispositions = proposal.Dispositions.Select(c => c.OldId is null ? c : c with {
                Choice = ToolboxValueChoice.Retire, IncomingKey = null, OutputKey = null }).ToImmutableArray(),
            Lineage = [lineage], Applicability = links, Dependencies = graph });
        var merged = ToolboxReconciliation.Accept(split, proposal, Context(), policy, Authority);
        var json = merged.Model.Source.ToJson();
        json["parcel_groups"]![0]!["parcel_group_id"] = "regenerated";
        json["rows"]![0]!["parcel_group_id"] = "regenerated"; json["rows"]![0]!["row_id"] = "regenerated-row";
        json["segments"]![0]!["parcel_group_id"] = "regenerated"; json["segments"]![0]!["segment_id"] = "regenerated-segment";
        source = CanonicalReviewContract.Read(json); proposal = Proposal(merged, policy, source);
        var keys = new Dictionary<string, string> { ["parcel"] = "regenerated", ["row"] = "regenerated-row", ["segment"] = "regenerated-segment" };
        var choices = merged.Model.IdentityMap.Ids.Select(pair => {
            var kind = pair.Key.Split(':')[0]; return new ToolboxCandidateDisposition(kind, pair.Value, keys[kind], keys[kind],
                ToolboxValueChoice.Retain, "examiner", "Explicit regeneration", "ev", true); }).ToImmutableArray();
        proposal = ToolboxReconciliation.Seal(proposal with { OperationId = "regenerate-op", ResultRevision = "regenerated-revision",
            Dispositions = choices.Add(proposal.Dispositions.Single(c => c.Kind == "global")) });
        var regenerated = ToolboxReconciliation.Accept(merged, proposal, Context(), policy, Authority);
        var loaded = Stored(regenerated).Reconciliation!;
        TestAssert.Equal(3, loaded.AcceptedDispositions.Length, "Split, merge and regeneration audits retained.");
        TestAssert.Equal(2, loaded.Lineage.Length, "Original split and merge retained separately.");
        TestAssert.Equal(merged.Model.Parcels[0].Id, loaded.Model.Parcels[0].Id, "Regeneration retains accepted domain ID.");
        TestAssert.Equal("regenerated", loaded.Model.Parcels[0].SourceKey, "Regenerated canonical key retained.");
        TestAssert.Equal(regenerated.Model.Relations!.ReadinessBarriers.Length, loaded.Model.Relations!.ReadinessBarriers.Length,
            "Unrenewed old applicability remains a barrier, not rewritten by hydration.");
    }

    public static void StorageAllInputInvalidationParity()
    {
        var policy = Policy(); var initial = Start(3, policy); var reviews = Reviews(initial, policy);
        var loaded = Stored(initial, reviews).Reconciliation!;
        ToolboxParcelModel Tabular(ToolboxParcelModel model)
        {
            var json = model.Source.ToJson(); json["survey_metadata"] = new JsonObject { ["reviewed_note"] = "changed" };
            return model.ReAdapt(Context(), CanonicalReviewContract.Read(json));
        }
        ToolboxParcelModel Linked(ToolboxParcelModel model)
        {
            var json = model.Source.ToJson(); json["evidence_links"] = new JsonArray(new JsonObject {
                ["evidence_id"] = "ev", ["parcel_group_id"] = "group-1", ["role"] = "title" });
            return model.ReAdapt(Context(), CanonicalReviewContract.Read(json));
        }
        ToolboxParcelModel Excluded(ToolboxParcelModel model) => model.SetInclusion(new(model.Parcels[1].Id, ToolboxInclusion.Excluded,
            "examiner", Now, "Reviewed exclusion", "ev", policy.Pin.PolicyVersion), policy.Pin.PolicyVersion, true);
        foreach (var change in new Func<ToolboxParcelModel, ToolboxParcelModel>[] { Tabular, Linked, Excluded })
        {
            var before = Evaluate(initial with { Model = change(initial.Model) }, policy, reviews);
            var after = Evaluate(loaded with { Model = change(loaded.Model) }, policy, reviews);
            TestAssert.Equal(before.Fingerprint, after.Fingerprint, "Every tabular/link/inclusion consumer reacts identically after hydration.");
            TestAssert.Equal(before.Status, after.Status, "Invalidation state parity.");
        }
        var changedPolicy = JsonNode.Parse(policy.JsonSnapshot)!.AsObject(); changedPolicy["policy_version"] = "next-policy";
        var bytes = Encoding.UTF8.GetBytes(changedPolicy.ToJsonString());
        var next = ToolboxReviewPolicyLoader.Load(bytes, new(policy.Pin.PolicyId, "next-policy", ToolboxWorkspaceStorageCodec.Hash(bytes)));
        TestAssert.Equal(Evaluate(initial, next, reviews).Fingerprint, Evaluate(loaded, next, reviews).Fingerprint, "Changed policy leaves old proofs equally stale.");
        var doc = new ToolboxWorkspaceStorageCodec().Capture(new(loaded.Model, loaded.Register, loaded, reviews.ToImmutableArray()));
        TestAssert.True(doc.Success, "Hydrated state recaptures without minting history.");
    }
}
