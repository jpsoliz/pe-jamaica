using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Review.Canonical;
using ParcelWorkflowAddIn.Workflow.Toolbox;
using ParcelWorkflowAddIn.Workflow.Toolbox.Model;
using ParcelWorkflowAddIn.Workflow.Toolbox.Persistence;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class ToolboxPrototypeRevisionStoreTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-11T12:34:56.1234567+02:00", CultureInfo.InvariantCulture);
    private static string Id() => Guid.NewGuid().ToString("N");

    public static void RunAll()
    {
        CreateOpenAndDetachedRoundTrip();
        SuccessiveCommitsExactHistoricalRetryAndTailRecovery();
        ChangedIntentStaleParentAndReservedIds();
        PolicyTrustGatesAllWriters();
        MalformedLayoutInventoryAndAncestryCorruption();
        AbandonmentIsAuditedIdempotentAndReservesIds();
        CancellationBeforeMutationAndAfterPublication();
        EverySaveBoundaryRetainsEvidenceAndRecoversOrAbandons();
        ProcessInterruptionPreservesPublicationAndRecovery();
        InitializationNativeProcessInterruption();
        AbandonmentNativeProcessInterruption();
        InitializationAndAbandonmentBoundaryFailures();
        LockConsistentRelocationPreservesCommittedAndPendingEvidence();
        IndependentReviewRegressionsRequireExactRefusals();
    }

    public static void CreateOpenAndDetachedRoundTrip()
    {
        foreach (int count in new[] { 0, 1, 3, 10 })
        {
            var f = new Fixture(count);
            var empty = f.Unchanged(() => f.Open());
            Code("empty", empty);
            TestAssert.True(empty.Current is null && empty.Workspace is null, "Empty store has no invented revision/model.");
            Code("initialization_conflict", f.Unchanged(() => f.Store().Create("case", f.Identity, f.StoreId)));
            var prepared = f.Prepare(assets: [new byte[] { 11, 22 }, new byte[] { 33, 44, 55 }]);
            var saved = f.Save(prepared);
            Committed(prepared, saved);
            var opened = f.Unchanged(() => f.Open());
            Code("verified", opened);
            TestAssert.True(opened.TransportVerified && opened.Writable, "Real NTFS save opens verified and writable with fresh trust.");
            TestAssert.Equal(f.Model.Source.JsonSnapshot, opened.Workspace!.Model.Source.JsonSnapshot, "Unknown canonical fields retain exact snapshot.");
            TestAssert.Equal(f.Model.Parcels.Length, opened.Workspace.Model.Parcels.Length, "Variable parcel counts survive disk hydration.");
            TestAssert.Equal(f.Model.InclusionFingerprint, opened.Workspace.Model.InclusionFingerprint, "No inclusion action is replayed.");
            TestAssert.Equal(ToolboxCheckOutcome.Pending, opened.Workspace.CurrentCheckReviews.Single().Outcome, "Pending review stays pending.");
            TestAssert.Equal(f.Register.HumanReviews.Single(), opened.Workspace.Register.HumanReviews.Single(), "Raw/manual review and timestamp preserved.");
            TestAssert.True(f.Bundle.SequenceEqual(File.ReadAllBytes(f.Revision(prepared, "bundle.json"))), "Exact encoded A bytes on disk.");
            foreach (var asset in prepared.Intent.Assets!.Value)
                TestAssert.Equal(asset.Sha256, ToolboxPrototypeDiskContract.Hash(File.ReadAllBytes(f.Revision(prepared, "assets", asset.Sha256 + ".bin"))), "Ordered asset inventory addresses actual exact bytes.");
            var revision = f.Read<ToolboxDiskRevision>("revisions", prepared.Intent.ProposedRevisionId, "revision.json");
            TestAssert.Equal(prepared.Intent.PreparedAt, revision.PreparedAt, "Original offset and precision retained.");
            TestAssert.Equal("committed", f.Journal(prepared).State, "Verified terminal journal required.");
            var snapshot = opened.Workspace.Model.Source.JsonSnapshot;
            var successor = f.Prepare(saved.Current);
            Committed(successor, f.Save(successor));
            TestAssert.Equal(snapshot, opened.Workspace.Model.Source.JsonSnapshot, "Detached snapshot remains owned after successor save.");
        }
        var ownership = new Fixture();
        byte[] callerBundle = ownership.Bundle.ToArray(); byte[] callerAsset = [6, 7, 8];
        var frozen = ToolboxPrototypeDiskContract.Prepare(ownership.StoreId, ownership.Identity, Id(), Id(), null,
            "examiner", Now, callerBundle, new ReadOnlyMemory<byte>[] { callerAsset });
        Committed(frozen, ownership.Store(boundary: name =>
        {
            if (name == "before_write:journal/" + frozen.Intent.OperationId + ".json") { callerBundle[0] = 0; callerAsset[0] = 0; }
        }).Save("case", frozen, [ownership.Pin]));
        TestAssert.True(frozen.Bundle.AsSpan().SequenceEqual(File.ReadAllBytes(ownership.Revision(frozen, "bundle.json"))), "Caller mutation during save cannot change owned bundle.");
        TestAssert.True(frozen.Assets!.Value[0].AsSpan().SequenceEqual(File.ReadAllBytes(ownership.Revision(frozen, "assets", frozen.Intent.Assets!.Value[0].Sha256 + ".bin"))), "Caller mutation cannot change owned asset.");
        foreach (bool explicitEmpty in new[] { false, true })
        {
            var inventory = new Fixture(); var request = inventory.Prepare(assets: explicitEmpty ? Array.Empty<ReadOnlyMemory<byte>>() : null);
            Committed(request, inventory.Save(request));
            var revision = inventory.Read<ToolboxDiskRevision>("revisions", request.Intent.ProposedRevisionId, "revision.json");
            TestAssert.Equal(!explicitEmpty, revision.Assets is null, "Null and explicitly empty asset inventories remain distinct on disk.");
            TestAssert.Equal(explicitEmpty, Directory.Exists(inventory.Revision(request, "assets")), "Assets directory follows exact inventory nullability.");
        }
        var failedCreate = new Fixture(create: false);
        var interrupted = failedCreate.Store(boundary: name => { if (name == "after_case_create") throw new IOException("synthetic initialization interruption"); })
            .Create("case", failedCreate.Identity, failedCreate.StoreId);
        Code("initialization_incomplete", interrupted);
        Code("initialization_conflict", failedCreate.Unchanged(() => failedCreate.Store().Create("case", failedCreate.Identity, failedCreate.StoreId)));
        TestAssert.False(failedCreate.Unchanged(() => failedCreate.Open()).Writable, "Open cannot repair ownerless initialization.");
        var missingLock = new Fixture();
        File.Delete(missingLock.Path("writer.lock"));
        TestAssert.False(missingLock.Unchanged(() => missingLock.Open()).Writable, "Open refuses missing lock.");
        TestAssert.False(File.Exists(missingLock.Path("writer.lock")), "Open never recreates lock.");
    }

    public static void SuccessiveCommitsExactHistoricalRetryAndTailRecovery()
    {
        var f = new Fixture();
        var first = f.Prepare(); var receipt = f.Save(first); Committed(first, receipt);
        var second = f.Prepare(receipt.Current); var b = f.Save(second); Committed(second, b);
        var third = f.Prepare(b.Current); var c = f.Save(third); Committed(third, c);
        TestAssert.Equal(3L, c.Current!.Sequence, "Successive commits increment sequence exactly.");
        var retry = f.Unchanged(() => f.Save(first));
        Committed(first, retry);
        TestAssert.Equal(receipt.Receipt, retry.Receipt, "Retry after two successors returns exact historical receipt.");
        TestAssert.Equal(c.Current, retry.Current, "Historical retry never moves current.");
        File.Delete(f.Path("receipts", first.Intent.OperationId + ".json"));
        Code("recovery_required", f.Unchanged(() => f.Open()));
        Code("recovery_required", f.Unchanged(() => f.Save(first)));
        var pointer = File.ReadAllBytes(f.Path("current.json"));
        var recovered = f.Recover(first);
        Committed(first, recovered);
        TestAssert.Equal(receipt.Receipt, recovered.Receipt, "Explicit recovery reconstructs exact historical receipt.");
        TestAssert.True(pointer.SequenceEqual(File.ReadAllBytes(f.Path("current.json"))), "Historical tail recovery leaves current bytes unchanged.");
        TestAssert.Equal("verified", f.Open().Code, "Historical recovery resolves pending evidence.");
        TestAssert.Equal("abandon_denied", f.Unchanged(() => f.Abandon(first)).Code, "Published ancestor cannot be abandoned.");
    }

    public static void ChangedIntentStaleParentAndReservedIds()
    {
        var f = new Fixture(); var first = f.Prepare(); var a = f.Save(first); Committed(first, a);
        foreach (var changed in new[]
        {
            first with { Intent = first.Intent with { Actor = "different-examiner" } },
            first with { Intent = first.Intent with { PreparedAt = Now.AddSeconds(1).ToString("O", CultureInfo.InvariantCulture) } },
            first with { Intent = first.Intent with { ProposedRevisionId = Id() } },
            first with { Intent = first.Intent with { Assets = [] }, Assets = [] }
        }) Code("operation_conflict", f.Unchanged(() => f.Save(changed)));
        var reused = f.Prepare(a.Current, revisionId: first.Intent.ProposedRevisionId);
        Code("operation_conflict", f.Unchanged(() => f.Save(reused)));
        var second = f.Prepare(a.Current); var b = f.Save(second); Committed(second, b);
        Code("parent_conflict", f.Unchanged(() => f.Save(f.Prepare(a.Current))));
        Code("parent_conflict", f.Unchanged(() => f.Save(f.Prepare())));
        foreach (var parent in new[]
        {
            ToolboxPrototypeDiskContract.Parent(b.Current!) with { Sequence = b.Current!.Sequence + 1 },
            ToolboxPrototypeDiskContract.Parent(b.Current!) with { RevisionDigest = new string('0', 64) },
            ToolboxPrototypeDiskContract.Parent(b.Current!) with { RevisionId = Id() }
        })
        {
            var p = f.Prepare(b.Current) with { Intent = f.Prepare(b.Current).Intent with { ExpectedParent = parent } };
            Code("parent_conflict", f.Unchanged(() => f.Save(p)));
        }
        var brokenBytes = first with { Bundle = ImmutableArray.Create(new byte[] { 0 }) };
        Code("request_bytes_conflict", f.Unchanged(() => f.Save(brokenBytes)));
        ThrowsDisk("duplicate_asset", () => f.Prepare(assets: [new byte[] { 1 }, new byte[] { 1 }]));
        ThrowsDisk("sequence_overflow", () => ToolboxPrototypeDiskContract.Prepare(f.StoreId, f.Identity, Id(), Id(),
            new(Id(), new string('0', 64), long.MaxValue), "examiner", Now, f.Bundle));
    }

    public static void PolicyTrustGatesAllWriters()
    {
        var f = new Fixture(); var p = f.Prepare();
        Code("policy_inspection_required", f.Unchanged(() => f.Store().Save("case", p)));
        var wrongPin = f.Pin with { Sha256 = new string('0', 64) };
        Code("policy_inspection_required", f.Unchanged(() => f.Store().Save("case", p, [wrongPin])));
        Committed(p, f.Save(p));
        var inspect = f.Unchanged(() => f.Store().Open("case", f.Identity, f.StoreId));
        Code("policy_inspection_required", inspect);
        TestAssert.True(inspect.TransportVerified && inspect.Workspace is not null && !inspect.Writable, "Missing external trust permits only verified inspection.");
        Code("policy_inspection_required", f.Unchanged(() => f.Store().Recover("case", f.Identity, f.StoreId, p.Intent.OperationId)));
        var partial = f.Prepare(f.Open().Current);
        f.Fault(partial, "before_stage_create");
        Code("policy_inspection_required", f.Unchanged(() => f.Store().Abandon("case", f.Identity, f.StoreId, partial.Intent.OperationId,
            Fixture.Audit, originalRequest: partial)));
        Code("policy_inspection_required", f.Unchanged(() => f.Store().Abandon("case", f.Identity, f.StoreId, partial.Intent.OperationId,
            Fixture.Audit, [wrongPin], originalRequest: partial)));
        Code("operation_abandoned", f.Abandon(partial));
    }

    public static void MalformedLayoutInventoryAndAncestryCorruption()
    {
        foreach (string damage in new[] { "unknown", "extra_asset", "asset_bytes", "missing_asset", "bundle_bytes", "revision_missing", "torn_pointer", "owner_field", "duplicate_owner", "missing_lock", "ancestry" })
        {
            var f = new Fixture(); var a = f.Prepare(assets: [new byte[] { 1, 2, 3 }]); var saved = f.Save(a); Committed(a, saved);
            var current = a;
            if (damage == "ancestry") { current = f.Prepare(saved.Current); Committed(current, f.Save(current)); }
            switch (damage)
            {
                case "unknown": File.WriteAllBytes(f.Path("unrecognized.tmp"), [99]); break;
                case "extra_asset": File.WriteAllBytes(f.Revision(a, "assets", ToolboxPrototypeDiskContract.Hash(new byte[] { 99 }) + ".bin"), [99]); break;
                case "asset_bytes": File.WriteAllBytes(f.Revision(a, "assets", a.Intent.Assets!.Value[0].Sha256 + ".bin"), [9, 8, 7]); break;
                case "missing_asset": File.Delete(f.Revision(a, "assets", a.Intent.Assets!.Value[0].Sha256 + ".bin")); break;
                case "bundle_bytes": File.WriteAllBytes(f.Revision(a, "bundle.json"), [123]); break;
                case "revision_missing": File.Delete(f.Revision(a, "revision.json")); break;
                case "torn_pointer": File.WriteAllText(f.Path("current.json"), "{"); break;
                case "owner_field":
                    var owner = JsonNode.Parse(File.ReadAllBytes(f.Path("owner.json")))!.AsObject(); owner["unknown_authority"] = true;
                    File.WriteAllText(f.Path("owner.json"), owner.ToJsonString()); break;
                case "duplicate_owner":
                    File.WriteAllText(f.Path("owner.json"), Encoding.UTF8.GetString(File.ReadAllBytes(f.Path("owner.json"))).Insert(1, "\"contract\":\"secret-value\",")); break;
                case "missing_lock": File.Delete(f.Path("writer.lock")); break;
                case "ancestry": File.Delete(f.Revision(a, "revision.json")); break;
            }
            var open = f.Unchanged(() => f.Open());
            TestAssert.True(open.State is "corrupt" or "quarantined", damage + " must fail closed on actual disk.");
            TestAssert.False(open.Writable, damage + " cannot grant writable use.");
            var retry = f.Unchanged(() => f.Save(current));
            TestAssert.False(retry.Code is "committed" or "committed_late_cancellation", damage + " cannot return committed.");
            TestAssert.False(open.Code.Contains("secret-value", StringComparison.Ordinal), "Diagnostic does not echo raw metadata.");
        }
        var bounded = new Fixture(); var p = bounded.Prepare(); Committed(p, bounded.Save(p));
        foreach (var limits in new[] { new ToolboxDiskLimits(MetadataBytes: 32), new ToolboxDiskLimits(TraversalBytes: 32), new ToolboxDiskLimits(Entries: 3), new ToolboxDiskLimits(DirectoryDepth: 1) })
        {
            var result = bounded.Unchanged(() => bounded.Store(limits: limits).Open("case", bounded.Identity, bounded.StoreId, [bounded.Pin]));
            TestAssert.False(result.Writable, "Downward disk limits must deny writable observation.");
            TestAssert.True(result.Code is "limit_exceeded" or "limits_exceeded", "Bounded rejection has a typed limit diagnostic.");
        }
        var partialSealed = new Fixture(); var sealedRequest = partialSealed.Prepare();
        partialSealed.Fault(sealedRequest, "after_seal");
        File.Delete(partialSealed.Revision(sealedRequest, "bundle.json"));
        TestAssert.Equal("partial_revision", partialSealed.Unchanged(() => partialSealed.Open()).Code, "Partial sealed location is quarantined.");
        TestAssert.False(partialSealed.Unchanged(() => partialSealed.Recover(sealedRequest)).Code == "committed", "Partial sealed location cannot be resumed.");
        TestAssert.False(partialSealed.Unchanged(() => partialSealed.Abandon(sealedRequest)).Code == "operation_abandoned", "Partial sealed evidence cannot be abandoned as ordinary staging.");
    }

    public static void AbandonmentIsAuditedIdempotentAndReservesIds()
    {
        foreach (string boundary in new[] { "before_stage_create", "before_write:staging/{op}/bundle.json", "after_write:staging/{op}/bundle.json" })
        {
            var f = new Fixture(); var baseline = f.Prepare(); var committed = f.Save(baseline); Committed(baseline, committed);
            var p = f.Prepare(committed.Current, assets: [new byte[] { 7, 8 }]);
            f.Fault(p, boundary.Replace("{op}", p.Intent.OperationId));
            var oldPointer = File.ReadAllBytes(f.Path("current.json"));
            if (!File.Exists(f.Path("staging", p.Intent.OperationId, "bundle.json")))
            {
                var wrong = p with { Intent = p.Intent with { Actor = "wrong-original" } };
                Code("operation_conflict", f.Unchanged(() => f.Abandon(p, original: wrong)));
                var wrongBytes = p with { Bundle = ImmutableArray.Create(new byte[] { 0 }) };
                Code("request_bytes_conflict", f.Unchanged(() => f.Abandon(p, original: wrongBytes)));
                Code("policy_inspection_required", f.Unchanged(() => f.Store().Abandon("case", f.Identity, f.StoreId, p.Intent.OperationId, Fixture.Audit, [f.Pin])));
            }
            Code("operation_abandoned", f.Abandon(p));
            TestAssert.Equal(Fixture.Audit, f.Journal(p).Abandonment, "Exact caller abandonment audit retained.");
            TestAssert.True(oldPointer.SequenceEqual(File.ReadAllBytes(f.Path("current.json"))), "Abandonment leaves verified parent untouched.");
            Code("operation_abandoned", f.Unchanged(() => f.Abandon(p)));
            Code("abandonment_conflict", f.Unchanged(() => f.Abandon(p, Fixture.Audit with { Reason = "different reason" })));
            Code("operation_abandoned", f.Unchanged(() => f.Save(p)));
            Code("operation_conflict", f.Unchanged(() => f.Save(f.Prepare(committed.Current, revisionId: p.Intent.ProposedRevisionId))));
            Code("operation_conflict", f.Unchanged(() => f.Save(p with { Intent = p.Intent with { ProposedRevisionId = Id() } })));
            var next = f.Prepare(committed.Current); Committed(next, f.Save(next));
            TestAssert.Equal("abandoned", f.Journal(p).State, "Fresh operation retains abandoned journal and reserved IDs.");
        }
    }

    public static void CancellationBeforeMutationAndAfterPublication()
    {
        var f = new Fixture(); var p = f.Prepare();
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            Code("cancelled", f.Unchanged(() => f.Store().Save("case", p, [f.Pin], cancelled.Token)));
            TestAssert.False(File.Exists(f.Path("journal", p.Intent.OperationId + ".json")), "Early cancellation creates no operation.");
        }
        using (var cancellation = new CancellationTokenSource())
        {
            bool hit = false;
            Code("cancelled", f.Unchanged(() => f.Store(boundary: name =>
            {
                if (name == "before_write:journal/" + p.Intent.OperationId + ".json") { hit = true; cancellation.Cancel(); }
            }).Save("case", p, [f.Pin], cancellation.Token)));
            TestAssert.True(hit, "Cancellation regression reaches the callback immediately before initial journal I/O.");
            TestAssert.False(File.Exists(f.Path("journal", p.Intent.OperationId + ".json")), "Cancellation in pre-write callback creates no journal leaf.");
            TestAssert.False(Directory.Exists(f.Path("staging", p.Intent.OperationId)) || Directory.Exists(f.Revision(p)), "Pre-write cancellation creates no staging or revision evidence.");
            TestAssert.False(File.Exists(f.Path("receipts", p.Intent.OperationId + ".json")), "Pre-write cancellation creates no receipt.");
        }
        using (var cancellation = new CancellationTokenSource())
        {
            var result = f.Store(boundary: name => { if (name == "after_write:journal/" + p.Intent.OperationId + ".json") cancellation.Cancel(); })
                .Save("case", p, [f.Pin], cancellation.Token);
            Code("recovery_required", result);
            TestAssert.True(File.Exists(f.Path("journal", p.Intent.OperationId + ".json")), "Cancellation after initial journal retains intent.");
            Code("recovery_required", f.Unchanged(() => f.Open()));
            Code("operation_abandoned", f.Abandon(p));
        }
        foreach (bool failTail in new[] { false, true })
        {
            var late = new Fixture(); var request = late.Prepare(); using var cancellation = new CancellationTokenSource();
            var result = late.Store(boundary: name =>
            {
                if (name == "after_replace:current.json") cancellation.Cancel();
                if (failTail && name == "before_write:receipts/" + request.Intent.OperationId + ".json") throw new IOException("synthetic late tail failure");
            }).Save("case", request, [late.Pin], cancellation.Token);
            if (failTail)
            {
                Code("published_recovery_required", result);
                TestAssert.Equal(request.Intent.ProposedRevisionId, result.Current!.RevisionId, "Late tail failure still reports publication evidence.");
                Committed(request, late.Recover(request));
            }
            else { Code("committed_late_cancellation", result); VerifiedReceipt(request, result); Code("verified", late.Open()); }
        }
    }

    public static void EverySaveBoundaryRetainsEvidenceAndRecoversOrAbandons()
    {
        var probe = new Fixture(); var p = probe.Prepare(assets: [new byte[] { 1, 2 }, new byte[] { 3, 4 }]);
        var boundaries = new List<string>();
        Committed(p, probe.Store(boundary: boundaries.Add).Save("case", p, [probe.Pin]));
        TestAssert.True(boundaries.Contains("before_seal") && boundaries.Contains("after_replace:current.json")
            && boundaries.Contains("after_write:receipts/" + p.Intent.OperationId + ".json"), "Matrix actually observes seal, publication and receipt boundaries.");
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var point in boundaries)
        {
            int occurrence = occurrences.TryGetValue(point, out int previous) ? previous + 1 : 1;
            occurrences[point] = occurrence;
            var f = new Fixture(); var baseline = f.Prepare(); var prior = f.Save(baseline); Committed(baseline, prior);
            var request = f.Prepare(prior.Current, assets: [new byte[] { 1, 2 }, new byte[] { 3, 4 }]);
            var target = point.Replace(p.Intent.OperationId, request.Intent.OperationId).Replace(p.Intent.ProposedRevisionId, request.Intent.ProposedRevisionId);
            f.Fault(request, target, occurrence);
            var observed = f.Unchanged(() => f.Open());
            bool published = observed.Current?.RevisionId == request.Intent.ProposedRevisionId;
            if (!published) TestAssert.Equal(prior.Current, observed.Current, target + " preserves old pointer before publication.");
            var journalPath = f.Path("journal", request.Intent.OperationId + ".json");
            if (!File.Exists(journalPath))
            {
                Code("operation_missing", f.Unchanged(() => f.Recover(request)));
                TestAssert.False(Directory.Exists(f.Path("staging", request.Intent.OperationId)), "No stage without prepared journal.");
                continue;
            }
            bool complete = f.CompleteBytes(request);
            if (complete)
            {
                Committed(request, f.Recover(request));
                Code("verified", f.Open());
                Committed(request, f.Unchanged(() => f.Save(request)));
            }
            else
            {
                Code("incomplete_staging", f.Unchanged(() => f.Recover(request)));
                Code("operation_abandoned", f.Abandon(request));
                TestAssert.Equal(prior.Current, f.Open().Current, "Abandoning partial bytes preserves parent.");
            }
            TestAssert.True(f.Bundle.SequenceEqual(File.ReadAllBytes(f.Revision(baseline, "bundle.json"))), "Fault/recovery never alters baseline immutable bytes.");
        }
    }

    public static void ProcessInterruptionPreservesPublicationAndRecovery() => RunNativeProcessMatrix("save", 130);
    public static void InitializationNativeProcessInterruption() => RunNativeProcessMatrix("create", 32);
    public static void AbandonmentNativeProcessInterruption() => RunNativeProcessMatrix("abandon", 24);

    private sealed record ProcessPoint(string Name, int Occurrence, bool Published);
    private sealed record WorkerInspection(string State, string Code, bool TransportVerified, bool Writable,
        string? StoreId, ToolboxDiskPointer? Current);

    private static void RunNativeProcessMatrix(string mode, int maximumScenarios)
    {
        var probe = ProcessFixture(mode, out _, out _, out var probeRequest);
        var points = RecordProcessPoints(probe, probeRequest, mode);
        TestAssert.True(points.Count > 0 && points.Count <= maximumScenarios, "Process matrix is explicit and bounded: " + mode + "/" + points.Count);
        foreach (string hook in new[] { "before_native_write:", "after_native_write:", "before_native_flush:", "after_native_flush:", "after_native_readback:" })
            TestAssert.True(points.Any(p => p.Name.StartsWith("N:" + hook, StringComparison.Ordinal)), "Native hook must actually be exercised: " + mode + "/" + hook);
        if (mode != "create")
        {
            foreach (string hook in new[] { "replace_opened", "replace_target_closed", "replace_before_commit", "replace_after_commit" })
                TestAssert.True(points.Any(p => p.Name == "N:" + hook), "Replacement handoff hook must actually be exercised: " + mode + "/" + hook);
            foreach (string hook in new[] { "before_native_replacement_flush", "after_native_replacement_flush" })
                TestAssert.True(points.Any(p => p.Name == "N:" + hook || p.Name.StartsWith("N:" + hook + ":", StringComparison.Ordinal)), "Replacement flush hook must actually be exercised: " + mode + "/" + hook);
            foreach (string hook in new[] { "before_verify:", "after_verify:" })
                TestAssert.True(points.Any(p => p.Name.StartsWith("C:" + hook + "journal/", StringComparison.Ordinal)), "Atomic journal target verification must actually be exercised: " + mode + "/" + hook);
        }
        else
            foreach (string hook in new[] { "before_native_lock_flush", "after_native_lock_flush" })
                TestAssert.True(points.Any(p => p.Name == "N:" + hook || p.Name.StartsWith("N:" + hook + ":", StringComparison.Ordinal)), "Initialization lock flush hook must actually be exercised: " + hook);
        if (mode == "save")
        {
            foreach (string hook in new[] { "C:before_verify:current.json", "C:after_verify:current.json" })
                TestAssert.True(points.Any(p => p.Name == hook), "Atomic pointer target verification must actually be exercised: " + hook);
            string asset = probeRequest.Intent.Assets!.Value[0].Sha256;
            foreach (int offset in new[] { 65536, 131072, 180001 })
                TestAssert.True(points.Any(p => p.Name == "N:after_native_write:toolbox/prototype-store/staging/" + probeRequest.Intent.OperationId + "/assets/" + asset + ".bin:" + offset), "Large asset exercises all three actual native write blocks.");
            TestAssert.Equal(3, points.Count(p => p.Name == "C:after_replace:journal/" + probeRequest.Intent.OperationId + ".json"), "Process deaths cover sealed, published and committed journal replacements separately.");
        }
        foreach (var point in points)
        {
            var f = ProcessFixture(mode, out var baseline, out var prior, out var request);
            WriteWorkerInput(f, request);
            string target = point.Name.Replace(probeRequest.Intent.OperationId, request.Intent.OperationId)
                .Replace(probeRequest.Intent.ProposedRevisionId, request.Intent.ProposedRevisionId);
            KillAtProcessPoint(f, mode, target, point.Occurrence);
            var fresh = InspectInFreshProcess(f);
            var local = f.Unchanged(() => f.Open());
            TestAssert.Equal(Inspection(local), fresh, "Fresh-process Open and detached local observation agree: " + mode + "/" + target);
            if (mode == "create")
            {
                bool claimed = Directory.Exists(System.IO.Path.Combine(f.Root, "case"));
                if (!claimed)
                {
                    TestAssert.Equal("C:before_case_create", target, "Only pre-claim initialization interruption leaves no case.");
                    Code("created", f.Store().Create("case", f.Identity, f.StoreId));
                }
                else
                {
                    Code("initialization_conflict", f.Unchanged(() => f.Store().Create("case", f.Identity, f.StoreId)));
                    TestAssert.True(fresh.State is "empty" or "corrupt" or "quarantined", "Initialization residue cannot invent a revision or require repair.");
                    TestAssert.False(fresh.Writable, "Killed initialization never grants writable readiness.");
                    if (fresh.State == "empty") Code("empty", local);
                }
                continue;
            }
            var physicalPointer = f.Read<ToolboxDiskPointer>("current.json");
            var expectedPointer = mode == "save" && point.Published
                ? ToolboxPrototypeDiskContract.Pointer(ToolboxPrototypeDiskContract.Revision(request.Intent)) : prior!.Current;
            TestAssert.Equal(expectedPointer, physicalPointer, "Native commit boundary determines exact old/new pointer: " + mode + "/" + target);
            TestAssert.True(f.Bundle.SequenceEqual(File.ReadAllBytes(f.Revision(baseline!, "bundle.json"))), "Every native process death preserves baseline immutable bytes.");
            if (fresh.State is "corrupt" or "quarantined")
            {
                TestAssert.False(fresh.Writable, "Torn metadata remains quarantined after fresh-process observation.");
                TestAssert.False(f.Unchanged(() => f.Recover(request)).Code is "committed" or "committed_late_cancellation", "Recovery cannot guess intent from torn metadata.");
                TestAssert.False(f.Unchanged(() => f.Abandon(request)).Code == "operation_abandoned", "Abandon cannot overwrite torn metadata or ambiguous candidate.");
                continue;
            }
            TestAssert.Equal(physicalPointer, fresh.Current, "Clean inspection exposes independently verified exact publication.");
            if (mode == "abandon")
            {
                string sibling = f.Path("journal", request.Intent.OperationId + ".abandoned.next.json");
                var journal = f.Journal(request);
                if (journal.State == "abandoned") Code("operation_abandoned", f.Unchanged(() => f.Recover(request)));
                else if (File.Exists(sibling)) Code("abandon_required", f.Unchanged(() => f.Recover(request)));
                if (journal.State == "abandoned" || File.Exists(sibling))
                    Code("abandonment_conflict", f.Unchanged(() => f.Abandon(request, Fixture.Audit with { Reason = "conflicting process-death retry" })));
                string retained = TreeSnapshot(f.Path("staging", request.Intent.OperationId));
                Code("operation_abandoned", f.Abandon(request));
                TestAssert.Equal(retained, TreeSnapshot(f.Path("staging", request.Intent.OperationId)), "Process-death abandonment retry retains every payload byte.");
                Code("operation_abandoned", f.Unchanged(() => f.Abandon(request)));
            }
            else if (!File.Exists(f.Path("journal", request.Intent.OperationId + ".json")))
            {
                Code("verified", local);
                Code("operation_missing", f.Unchanged(() => f.Recover(request)));
            }
            else if (f.CompleteBytes(request))
            {
                Committed(request, f.Recover(request));
                Committed(request, f.Unchanged(() => f.Save(request)));
                TestAssert.Equal(request.Intent.ProposedRevisionId, f.Open().Current!.RevisionId, "Complete native payload recovery publishes the exact proposal.");
            }
            else
            {
                Code("incomplete_staging", f.Unchanged(() => f.Recover(request)));
                string stage = f.Path("staging", request.Intent.OperationId);
                string? retained = Directory.Exists(stage) ? TreeSnapshot(stage) : null;
                Code("operation_abandoned", f.Abandon(request));
                if (retained is not null) TestAssert.Equal(retained, TreeSnapshot(stage), "Partial native writes survive explicit abandonment untouched.");
                TestAssert.Equal(prior!.Current, f.Open().Current, "Partial native payload abandonment cannot move current.");
            }
        }
        Console.WriteLine("toolbox store " + mode + " process interruption scenarios verified: " + points.Count);
    }

    private static Fixture ProcessFixture(string mode, out ToolboxPreparedSave? baseline, out ToolboxDiskResult? prior, out ToolboxPreparedSave request)
    {
        var f = new Fixture(create: mode != "create"); baseline = null; prior = null;
        if (mode != "create") { baseline = f.Prepare(); prior = f.Save(baseline); Committed(baseline, prior); }
        byte[] large = Enumerable.Range(0, 180001).Select(i => (byte)(i % 251)).ToArray();
        request = f.Prepare(prior?.Current, assets: mode == "create" ? null : new ReadOnlyMemory<byte>[] { large });
        if (mode == "abandon") f.Fault(request, "before_seal");
        return f;
    }

    private static List<ProcessPoint> RecordProcessPoints(Fixture f, ToolboxPreparedSave request, string mode)
    {
        var points = new List<ProcessPoint>(); var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        bool publishing = false, published = false;
        void Observe(string name)
        {
            if (name == "C:before_replace:current.json") publishing = true;
            if (name == "C:after_replace:current.json" || publishing && name == "N:replace_after_commit") published = true;
            int occurrence = occurrences.TryGetValue(name, out int previous) ? previous + 1 : 1;
            occurrences[name] = occurrence; points.Add(new(name, occurrence, published));
            if (name == "C:after_replace:current.json") publishing = false;
        }
        var previousHook = Native.TestBoundary;
        Native.TestBoundary = name =>
        {
            if (name.StartsWith("before_native_", StringComparison.Ordinal) || name.StartsWith("after_native_", StringComparison.Ordinal)
                || name is "seal_opened" or "replace_opened" or "replace_target_closed" or "replace_before_commit" or "replace_after_commit") Observe("N:" + name);
        };
        try
        {
            var store = f.Store(boundary: name => Observe("C:" + name));
            var result = WorkerOperation(store, mode, request, f.Pin);
            if (mode == "save") Committed(request, result);
            else Code(mode == "create" ? "created" : "operation_abandoned", result);
        }
        finally { Native.TestBoundary = previousHook; }
        return points;
    }

    private static ToolboxDiskResult WorkerOperation(ToolboxPrototypeRevisionStore store, string mode, ToolboxPreparedSave p, ToolboxPolicyPin pin) => mode switch
    {
        "save" => store.Save("case", p, [pin]),
        "create" => store.Create("case", p.Intent.Case, p.Intent.StoreId),
        "abandon" => store.Abandon("case", p.Intent.Case, p.Intent.StoreId, p.Intent.OperationId, Fixture.Audit, [pin], originalRequest: p),
        _ => throw new InvalidOperationException("Unknown store worker mode.")
    };

    private static void WriteWorkerInput(Fixture f, ToolboxPreparedSave p, string? slot = null)
    {
        string input = System.IO.Path.Combine(f.Root, "worker-input");
        if (slot is not null) input = System.IO.Path.Combine(input, slot);
        Directory.CreateDirectory(input);
        File.WriteAllBytes(System.IO.Path.Combine(input, "intent.json"), ToolboxPrototypeDiskContract.Encode(p.Intent));
        File.WriteAllBytes(System.IO.Path.Combine(input, "bundle.json"), p.Bundle.ToArray());
        if (p.Assets is { } assets)
            for (int i = 0; i < assets.Length; i++) File.WriteAllBytes(System.IO.Path.Combine(input, p.Intent.Assets!.Value[i].Sha256 + ".bin"), assets[i].ToArray());
    }

    private static Process StartWorker(params string[] arguments)
    {
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("No harness process executable.");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Environment.CurrentDirectory
        };
        if (System.IO.Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(typeof(ToolboxPrototypeRevisionStoreTests).Assembly.Location);
        start.ArgumentList.Add("--toolbox-disk-store-worker");
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new InvalidOperationException("Could not launch task-owned store worker.");
    }

    private static void StopOwnedWorker(Process child)
    {
        if (!child.HasExited) child.Kill(entireProcessTree: true);
        TestAssert.True(child.WaitForExit(10000), "Every owned worker exits before its handles or evidence are reused.");
    }

    private static void KillAtProcessPoint(Fixture f, string mode, string target, int occurrence)
    {
        using var child = StartWorker(mode, f.Root, target, occurrence.ToString(CultureInfo.InvariantCulture));
        var stderr = child.StandardError.ReadToEndAsync(); var ready = child.StandardOutput.ReadLineAsync();
        try
        {
            TestAssert.True(ready.Wait(30000), "Worker reaches bounded critical barrier: " + mode + "/" + target);
            TestAssert.Equal("READY", ready.Result, "Worker dispatch reaches exact critical boundary: " + mode + "/" + target
                + (stderr.IsCompletedSuccessfully ? "; stderr=" + stderr.Result : ""));
            if (mode != "create") Code("busy", f.Open());
            StopOwnedWorker(child);
        }
        finally
        {
            StopOwnedWorker(child);
            TestAssert.True(ready.Wait(5000) && stderr.Wait(5000), "Task-owned pipe readers complete after worker termination.");
        }
    }

    private static WorkerInspection Inspection(ToolboxDiskResult result) => new(result.State, result.Code, result.TransportVerified, result.Writable, result.StoreId, result.Current);

    private static WorkerInspection InspectInFreshProcess(Fixture f)
    {
        string before = TreeSnapshot(f.Root);
        using var child = StartWorker("inspect", f.Root);
        var stderr = child.StandardError.ReadToEndAsync(); var output = child.StandardOutput.ReadLineAsync();
        try
        {
            TestAssert.True(output.Wait(30000) && child.WaitForExit(30000), "Fresh-process inspection finishes within bounded waits.");
            TestAssert.Equal(0, child.ExitCode, "Fresh-process inspection exits successfully.");
            TestAssert.True(output.Result is not null, "Fresh-process inspection returns serialized detached result.");
            var result = JsonSerializer.Deserialize<WorkerInspection>(output.Result!) ?? throw new InvalidOperationException("Invalid fresh-process observation.");
            TestAssert.Equal(before, TreeSnapshot(f.Root), "Fresh-process Open is strictly read-only for every native interruption.");
            return result;
        }
        finally
        {
            StopOwnedWorker(child);
            TestAssert.True(output.Wait(5000) && stderr.Wait(5000), "Fresh inspector pipe readers complete.");
        }
    }

    public static void InitializationAndAbandonmentBoundaryFailures()
    {
        var probe = new Fixture(create: false);
        var initialization = new List<string>();
        Code("created", probe.Store(boundary: initialization.Add).Create("case", probe.Identity, probe.StoreId));
        TestAssert.Equal(6, initialization.Count(n => n == "after_create_directory"), "Initialization matrix includes every required directory.");
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string point in initialization)
        {
            int occurrence = occurrences.TryGetValue(point, out int previous) ? previous + 1 : 1;
            occurrences[point] = occurrence;
            var f = new Fixture(create: false); int hits = 0; bool hit = false;
            var result = f.Store(boundary: name =>
            {
                if (name == point && ++hits == occurrence) { hit = true; throw new IOException("synthetic initialization boundary failure"); }
            }).Create("case", f.Identity, f.StoreId);
            TestAssert.True(hit, "Every initialization boundary occurrence must execute: " + point + "/" + occurrence);
            if (point == "before_case_create")
            {
                Code("io_error", result);
                TestAssert.False(Directory.Exists(System.IO.Path.Combine(f.Root, "case")), "Failure before claiming case leaves no case directory.");
                Code("created", f.Store().Create("case", f.Identity, f.StoreId));
            }
            else
            {
                Code("initialization_incomplete", result);
                TestAssert.True(Directory.Exists(System.IO.Path.Combine(f.Root, "case")), "Claimed initialization evidence remains on disk.");
                Code("initialization_conflict", f.Unchanged(() => f.Store().Create("case", f.Identity, f.StoreId)));
                var opened = f.Unchanged(() => f.Open());
                if (point == "after_write:owner.json") Code("empty", opened);
                else TestAssert.True(opened.State is "corrupt" or "quarantined", "Incomplete initialization cannot be repaired by Open: " + point);
                TestAssert.False(opened.Writable, "Interrupted initialization cannot grant writer readiness.");
            }
        }
        foreach (bool afterClaim in new[] { false, true })
        {
            var f = new Fixture(create: false); using var cancellation = new CancellationTokenSource();
            if (!afterClaim) cancellation.Cancel();
            var result = f.Store(boundary: name => { if (afterClaim && name == "after_case_create") cancellation.Cancel(); })
                .Create("case", f.Identity, f.StoreId, cancellation.Token);
            Code(afterClaim ? "initialization_incomplete" : "cancelled", result);
            TestAssert.Equal(afterClaim, Directory.Exists(System.IO.Path.Combine(f.Root, "case")), "Initialization cancellation retains exactly the claimed evidence.");
            if (afterClaim) Code("initialization_conflict", f.Unchanged(() => f.Store().Create("case", f.Identity, f.StoreId)));
            else Code("created", f.Store().Create("case", f.Identity, f.StoreId));
        }

        var abandonProbe = new Fixture(); var probeRequest = abandonProbe.Prepare();
        abandonProbe.Fault(probeRequest, "before_stage_create");
        var abandonment = new List<string>();
        Code("operation_abandoned", abandonProbe.Store(boundary: abandonment.Add).Abandon("case", abandonProbe.Identity,
            abandonProbe.StoreId, probeRequest.Intent.OperationId, Fixture.Audit, [abandonProbe.Pin], originalRequest: probeRequest));
        TestAssert.Equal(6, abandonment.Count, "Abandonment matrix observes sibling write, replacement and target verification before/after boundaries.");
        foreach (string point in abandonment)
        {
            var f = new Fixture(); var baseline = f.Prepare(); var prior = f.Save(baseline); Committed(baseline, prior);
            var p = f.Prepare(prior.Current, assets: [new byte[] { 4, 5, 6 }]); f.Fault(p, "before_assets_create");
            string retainedStage = f.Path("staging", p.Intent.OperationId);
            string stagedBytes = TreeSnapshot(retainedStage);
            string target = point.Replace(probeRequest.Intent.OperationId, p.Intent.OperationId);
            bool hit = false;
            var result = f.Store(boundary: name => { if (name == target) { hit = true; throw new IOException("synthetic abandonment boundary failure"); } })
                .Abandon("case", f.Identity, f.StoreId, p.Intent.OperationId, Fixture.Audit, [f.Pin], originalRequest: p);
            TestAssert.True(hit, "Every abandonment boundary must execute: " + target);
            TestAssert.False(result.Code == "operation_abandoned", "Interrupted abandonment requires explicit verified retry.");
            var observed = f.Unchanged(() => f.Open());
            TestAssert.Equal(prior.Current, observed.Current, "Abandonment fault preserves verified parent.");
            string sibling = f.Path("journal", p.Intent.OperationId + ".abandoned.next.json");
            bool auditRecorded = File.Exists(sibling) || f.Journal(p).State == "abandoned";
            if (auditRecorded)
                Code("abandonment_conflict", f.Unchanged(() => f.Abandon(p, Fixture.Audit with { Reason = "conflicting retained audit" })));
            Code("operation_abandoned", f.Abandon(p));
            TestAssert.False(File.Exists(sibling), "Identical audit retry consumes recognized replacement sibling.");
            TestAssert.Equal(Fixture.Audit, f.Journal(p).Abandonment, "Retry retains exact original audit.");
            Code("operation_abandoned", f.Unchanged(() => f.Abandon(p)));
            Code("abandonment_conflict", f.Unchanged(() => f.Abandon(p, Fixture.Audit with { Actor = "different-examiner" })));
            Code("abandonment_conflict", f.Unchanged(() => f.Abandon(p, Fixture.Audit with { At = Now.AddSeconds(1).ToString("O", CultureInfo.InvariantCulture) })));
            Code("operation_abandoned", f.Unchanged(() => f.Save(p)));
            var fresh = f.Prepare(prior.Current); Committed(fresh, f.Save(fresh));
            TestAssert.Equal(stagedBytes, TreeSnapshot(retainedStage), "Abandonment fault/retry and fresh save retain all partial-stage bytes without cleanup.");
            TestAssert.True(f.Bundle.SequenceEqual(File.ReadAllBytes(f.Revision(baseline, "bundle.json"))), "Abandonment fault/retry preserves baseline bytes.");
        }
    }

    public static void LockConsistentRelocationPreservesCommittedAndPendingEvidence()
    {
        foreach (bool pending in new[] { false, true })
        {
            var source = new Fixture(); var first = source.Prepare(assets: [new byte[] { 1, 3, 5 }]);
            var prior = source.Save(first); Committed(first, prior);
            var selected = first;
            if (pending)
            {
                selected = source.Prepare(prior.Current, assets: [new byte[] { 2, 4, 6 }]);
                source.Fault(selected, "before_seal");
            }
            string copied = "";
            source.Unchanged(() =>
            {
                copied = CopyUnderWriterLock(source);
                return source.Open();
            });
            var clone = new ToolboxPrototypeRevisionStore(new ToolboxContainedFileSystem(copied, true));
            string destinationStore = System.IO.Path.Combine(copied, "case", "toolbox", "prototype-store");
            TestAssert.Equal(TreeSnapshot(source.Path()), TreeSnapshot(destinationStore), "Source-locked relocation preserves every store entry and exact file byte, with independently prepared empty lock.");
            ToolboxDiskResult OpenClone() => clone.Open("case", source.Identity, source.StoreId, [source.Pin]);
            var opened = UnchangedRoot(copied, OpenClone);
            Code(pending ? "recovery_required" : "verified", opened);
            TestAssert.Equal(prior.Current, opened.Current, "Copied current retains full case/store/revision identity.");
            TestAssert.Equal(source.Model.Source.JsonSnapshot, opened.Workspace!.Model.Source.JsonSnapshot, "Relocation rebases only derived paths and preserves canonical bytes.");
            TestAssert.Equal(source.StoreId, opened.StoreId, "Destination never mints a store identity.");
            if (pending)
            {
                TestAssert.True(Directory.Exists(System.IO.Path.Combine(copied, "case", "toolbox", "prototype-store", "staging", selected.Intent.OperationId)), "Source-locked copy preserves complete pending stage.");
                Code("recovery_required", UnchangedRoot(copied, () => clone.Save("case", selected, [source.Pin])));
                Committed(selected, clone.Recover("case", source.Identity, source.StoreId, selected.Intent.OperationId, [source.Pin]));
                Code("recovery_required", source.Unchanged(() => source.Open()));
                TestAssert.Equal(prior.Current, source.Open().Current, "Destination recovery cannot move source pointer.");
                foreach (bool missingBundle in new[] { false, true })
                {
                    string partial = CopyUnderWriterLock(source);
                    string partialStage = System.IO.Path.Combine(partial, "case", "toolbox", "prototype-store", "staging", selected.Intent.OperationId);
                    string missing = missingBundle ? System.IO.Path.Combine(partialStage, "bundle.json")
                        : System.IO.Path.Combine(partialStage, "assets", selected.Intent.Assets!.Value[0].Sha256 + ".bin");
                    File.Delete(missing);
                    var partialStore = new ToolboxPrototypeRevisionStore(new ToolboxContainedFileSystem(partial, true));
                    Code("recovery_required", UnchangedRoot(partial, () => partialStore.Open("case", source.Identity, source.StoreId, [source.Pin])));
                    Code("incomplete_staging", UnchangedRoot(partial, () => partialStore.Recover("case", source.Identity, source.StoreId, selected.Intent.OperationId, [source.Pin])));
                    string residue = TreeSnapshot(partialStage);
                    Code("operation_abandoned", partialStore.Abandon("case", source.Identity, source.StoreId, selected.Intent.OperationId,
                        Fixture.Audit, [source.Pin], originalRequest: selected));
                    TestAssert.Equal(residue, TreeSnapshot(partialStage), "Abandoning incomplete pending clone preserves every surviving byte.");
                    TestAssert.Equal(prior.Current, partialStore.Open("case", source.Identity, source.StoreId, [source.Pin]).Current, "Partial clone abandonment leaves copied parent unchanged.");
                    Code("recovery_required", source.Unchanged(() => source.Open()));
                }
            }
            else Committed(first, UnchangedRoot(copied, () => clone.Save("case", first, [source.Pin])));
            // Clones have separate local locks; copying makes no coordinated-lock claim.
            using (var session = new ToolboxContainedFileSystem(source.Root, true).OpenCase("case"))
            using (session.AcquireWriter())
            {
                Code("busy", source.Open());
                Code("verified", OpenClone());
            }
            var next = source.Prepare(OpenClone().Current); Committed(next, clone.Save("case", next, [source.Pin]));
            TestAssert.Equal(prior.Current, source.Unchanged(() => source.Open()).Current, "A fresh destination commit leaves source bytes and pointer independent.");

            string lockless = CopyUnderWriterLock(source);
            string destinationLock = System.IO.Path.Combine(lockless, "case", "toolbox", "prototype-store", "writer.lock");
            File.Delete(destinationLock);
            var locklessStore = new ToolboxPrototypeRevisionStore(new ToolboxContainedFileSystem(lockless, true));
            Code("missing", UnchangedRoot(lockless, () => locklessStore.Open("case", source.Identity, source.StoreId, [source.Pin])));
            TestAssert.False(File.Exists(destinationLock), "Relocated Open must never implicitly prepare a destination lock.");

            string incomplete = CopyUnderWriterLock(source);
            string missingAsset = System.IO.Path.Combine(incomplete, "case", "toolbox", "prototype-store", "revisions", first.Intent.ProposedRevisionId, "assets", first.Intent.Assets!.Value[0].Sha256 + ".bin");
            File.Delete(missingAsset);
            var incompleteStore = new ToolboxPrototypeRevisionStore(new ToolboxContainedFileSystem(incomplete, true));
            Code("partial_revision", UnchangedRoot(incomplete, () => incompleteStore.Open("case", source.Identity, source.StoreId, [source.Pin])));
            TestAssert.False(UnchangedRoot(incomplete, () => incompleteStore.Save("case", first, [source.Pin])).Code == "committed", "Incomplete relocation cannot claim a committed retry.");

            var thirdParty = new Fixture(); var foreign = thirdParty.Prepare(); Committed(foreign, thirdParty.Save(foreign));
            string mixed = CopyUnderWriterLock(source);
            File.WriteAllBytes(System.IO.Path.Combine(mixed, "case", "toolbox", "prototype-store", "current.json"), File.ReadAllBytes(thirdParty.Path("current.json")));
            var mixedStore = new ToolboxPrototypeRevisionStore(new ToolboxContainedFileSystem(mixed, true));
            Code("owner_conflict", UnchangedRoot(mixed, () => mixedStore.Open("case", source.Identity, source.StoreId, [source.Pin])));
            TestAssert.False(UnchangedRoot(mixed, () => mixedStore.Recover("case", source.Identity, source.StoreId, selected.Intent.OperationId, [source.Pin])).Code == "committed", "Third-party mixed evidence cannot be repaired by guessing a revision.");
        }
    }

    public static void IndependentReviewRegressionsRequireExactRefusals()
    {
        var emptyAssets = new Fixture();
        var emptyRequest = emptyAssets.Prepare(assets: Array.Empty<ReadOnlyMemory<byte>>());
        emptyAssets.Fault(emptyRequest, "before_assets_create");
        TestAssert.True(File.Exists(emptyAssets.Path("staging", emptyRequest.Intent.OperationId, "bundle.json")), "Empty-inventory regression reaches complete bundle before assets directory creation.");
        TestAssert.False(Directory.Exists(emptyAssets.Path("staging", emptyRequest.Intent.OperationId, "assets")), "Explicitly empty inventory still lacks its required directory at this boundary.");
        Code("incomplete_staging", emptyAssets.Unchanged(() => emptyAssets.Recover(emptyRequest)));
        TestAssert.False(emptyAssets.CompleteBytes(emptyRequest), "Test completeness oracle also preserves null versus empty inventory semantics.");
        Code("operation_abandoned", emptyAssets.Abandon(emptyRequest));

        var abandonment = new Fixture(); var baseline = abandonment.Prepare();
        var prior = abandonment.Save(baseline); Committed(baseline, prior);
        var selected = abandonment.Prepare(prior.Current); abandonment.Fault(selected, "before_seal");
        string sibling = abandonment.Path("journal", selected.Intent.OperationId + ".abandoned.next.json");
        bool hit = false;
        var interrupted = abandonment.Store(boundary: name =>
        {
            if (name == "after_write:journal/" + selected.Intent.OperationId + ".abandoned.next.json")
            { hit = true; throw new IOException("retained abandonment candidate regression"); }
        }).Abandon("case", abandonment.Identity, abandonment.StoreId, selected.Intent.OperationId,
            Fixture.Audit, [abandonment.Pin], originalRequest: selected);
        TestAssert.True(hit && File.Exists(sibling), "Abandonment regression retains flushed recognized candidate.");
        Code("recovery_required", interrupted);
        TestAssert.Equal("prepared", abandonment.Journal(selected).State, "Retained audit candidate precedes terminal journal replacement.");
        Code("abandon_required", abandonment.Unchanged(() => abandonment.Recover(selected)));
        foreach (var audit in new[]
        {
            Fixture.Audit with { Actor = "changed-actor" },
            Fixture.Audit with { At = Now.AddSeconds(1).ToString("O", CultureInfo.InvariantCulture) },
            Fixture.Audit with { Reason = "changed-reason" }
        }) Code("abandonment_conflict", abandonment.Unchanged(() => abandonment.Abandon(selected, audit)));
        Code("operation_abandoned", abandonment.Abandon(selected));
        TestAssert.Equal(prior.Current, abandonment.Open().Current, "Abandon-required recovery refusal never publishes the staged proposal.");

        var alternate = new Fixture(create: false, policyVersion: "test-v2");
        foreach (bool complete in new[] { false, true })
        {
            var f = new Fixture(); var first = f.Prepare(); var committed = f.Save(first); Committed(first, committed);
            var extraVersion = alternate.Pin with { Sha256 = new string('0', 64) };
            var catalogOpen = f.Unchanged(() => f.Store().Open("case", f.Identity, f.StoreId, [f.Pin, extraVersion]));
            Code("verified", catalogOpen);
            TestAssert.True(catalogOpen.Writable && !catalogOpen.Diagnostics.Any(d => d.Code is "policy_unavailable" or "policy_untrusted"),
                "Undeclared catalog version cannot introduce missing-policy warnings for this revision.");
            Committed(first, f.Unchanged(() => f.Store().Save("case", first, [f.Pin, extraVersion])));
            Code("policy_inspection_required", f.Unchanged(() => f.Store().Open("case", f.Identity, f.StoreId, [alternate.Pin])));
            foreach (var duplicate in new[] { f.Pin, f.Pin with { Sha256 = new string('0', 64) } })
            {
                ToolboxPolicyPin[] invalidCatalog = [f.Pin, duplicate, alternate.Pin];
                Code("policy_trust_conflict", f.Unchanged(() => f.Store().Open("case", f.Identity, f.StoreId, invalidCatalog)));
                Code("policy_trust_conflict", f.Unchanged(() => f.Store().Save("case", first, invalidCatalog)));
                Code("policy_trust_conflict", f.Unchanged(() => f.Store().Recover("case", f.Identity, f.StoreId, first.Intent.OperationId, invalidCatalog)));
                Code("policy_trust_conflict", f.Unchanged(() => f.Store().Abandon("case", f.Identity, f.StoreId, first.Intent.OperationId,
                    Fixture.Audit, invalidCatalog, originalRequest: first)));
            }
            var proposed = ToolboxPrototypeDiskContract.Prepare(f.StoreId, f.Identity, Id(), Id(),
                ToolboxPrototypeDiskContract.Parent(committed.Current!), "examiner", Now, alternate.Bundle);
            bool faultHit = false;
            var result = f.Store(boundary: name =>
            {
                if (name == (complete ? "before_seal" : "before_stage_create"))
                { faultHit = true; throw new IOException("current policy trust regression"); }
            }).Save("case", proposed, [f.Pin, alternate.Pin]);
            TestAssert.True(faultHit, "Proposal with separately trusted policy reaches requested real disk boundary.");
            Code("recovery_required", result);
            var inspection = f.Unchanged(() => f.Store().Open("case", f.Identity, f.StoreId, [alternate.Pin]));
            TestAssert.True(inspection.TransportVerified && inspection.Workspace is not null && !inspection.Writable, "Proposal trust does not grant trust to current policy.");
            Code("policy_inspection_required", f.Unchanged(() => f.Store().Recover("case", f.Identity, f.StoreId, proposed.Intent.OperationId, [alternate.Pin])));
            Code("policy_inspection_required", f.Unchanged(() => f.Store().Abandon("case", f.Identity, f.StoreId, proposed.Intent.OperationId,
                Fixture.Audit, [alternate.Pin], originalRequest: proposed)));
            if (complete)
            {
                Committed(proposed, f.Store().Recover("case", f.Identity, f.StoreId, proposed.Intent.OperationId, [f.Pin, alternate.Pin]));
                Code("verified", f.Unchanged(() => f.Store().Open("case", f.Identity, f.StoreId, [f.Pin, alternate.Pin])));
                Code("policy_inspection_required", f.Unchanged(() => f.Store().Open("case", f.Identity, f.StoreId, [f.Pin])));
                Committed(first, f.Unchanged(() => f.Store().Save("case", first, [f.Pin, alternate.Pin])));
            }
            else Code("operation_abandoned", f.Store().Abandon("case", f.Identity, f.StoreId, proposed.Intent.OperationId,
                Fixture.Audit, [f.Pin, alternate.Pin], originalRequest: proposed));
        }

        var conflicts = new Fixture(); var pending = conflicts.Prepare(); conflicts.Fault(pending, "before_stage_create");
        Code("operation_conflict", conflicts.Unchanged(() => conflicts.Abandon(pending,
            original: pending with { Intent = pending.Intent with { Actor = "conflicting original intent" } })));
        Code("request_bytes_conflict", conflicts.Unchanged(() => conflicts.Abandon(pending,
            original: pending with { Bundle = ImmutableArray.Create(new byte[] { 0 }) })));
        Code("operation_abandoned", conflicts.Abandon(pending));

        var admission = new Fixture(); var newRequest = admission.Prepare();
        int existingEntries = Directory.EnumerateFileSystemEntries(System.IO.Path.Combine(admission.Root, "case"), "*", SearchOption.AllDirectories).Count();
        int ownerBytes = File.ReadAllBytes(admission.Path("owner.json")).Length;
        foreach (var limits in new[]
        {
            new ToolboxDiskLimits(Entries: checked(existingEntries * 2)),
            new ToolboxDiskLimits(DirectoryDepth: 2),
            new ToolboxDiskLimits(MetadataBytes: ownerBytes),
            new ToolboxDiskLimits(TraversalBytes: ownerBytes)
        })
        {
            Code("empty", admission.Unchanged(() => admission.Store(limits: limits).Open("case", admission.Identity, admission.StoreId, [admission.Pin])));
            bool mutated = false;
            var denied = admission.Unchanged(() => admission.Store(limits: limits, boundary: _ => mutated = true).Save("case", newRequest, [admission.Pin]));
            Code("limit_exceeded", denied);
            TestAssert.False(mutated, "Save reserves resulting observations and transient evidence before any mutation callback.");
            TestAssert.False(File.Exists(admission.Path("journal", newRequest.Intent.OperationId + ".json")), "Admission failure creates no operation journal.");
        }
        Committed(newRequest, admission.Save(newRequest));
    }

    public static void ShortReplacementNamesDepthAdmissionAndForwardCandidates()
    {
        var malformed = new Fixture(); var first = malformed.Prepare(); var committed = malformed.Save(first); Committed(first, committed);
        var next = malformed.Prepare(committed.Current);
        File.WriteAllBytes(malformed.Path("current.next.json"), ToolboxPrototypeDiskContract.Encode(committed.Current!));
        Code("unexpected_layout", malformed.Unchanged(() => malformed.Open()));
        Code("unexpected_layout", malformed.Unchanged(() => malformed.Save(next)));
        Code("unexpected_layout", malformed.Unchanged(() => malformed.Recover(first)));
        Code("unexpected_layout", malformed.Unchanged(() => malformed.Abandon(first)));

        var depth = new Fixture(create: false); bool reachedMutation = false;
        Code("invalid_metadata", depth.Unchanged(() => depth.Store(limits: new ToolboxDiskLimits(JsonDepth: 1),
            boundary: _ => reachedMutation = true).Create("case", depth.Identity, depth.StoreId)));
        TestAssert.False(reachedMutation, "Owner depth admission happens before the first case-creation callback.");
        TestAssert.False(Directory.Exists(System.IO.Path.Combine(depth.Root, "case")), "Depth-one Create cannot claim a case directory or initialization residue.");
        Code("created", depth.Store().Create("case", depth.Identity, depth.StoreId));

        foreach (bool entryLimit in new[] { true, false })
        {
            var admission = new Fixture(create: false);
            int ownerLength = ToolboxPrototypeDiskContract.Encode(new ToolboxDiskOwner(ToolboxPrototypeDiskContract.Contract,
                1, admission.Identity, admission.StoreId, "prototype_store")).Length;
            var limits = entryLimit ? new ToolboxDiskLimits(Entries: 15) : new ToolboxDiskLimits(TraversalBytes: ownerLength - 1);
            bool callback = false;
            Code("limit_exceeded", admission.Unchanged(() => admission.Store(limits: limits, boundary: _ => callback = true)
                .Create("case", admission.Identity, admission.StoreId)));
            TestAssert.False(callback, "Create admits the complete initial observation before any case-creation callback.");
            TestAssert.False(Directory.Exists(System.IO.Path.Combine(admission.Root, "case")), "Insufficient initial observation budget leaves no case or initialization evidence.");
            var exact = new ToolboxDiskLimits(Entries: 16, TraversalBytes: ownerLength);
            Code("created", admission.Store(limits: exact).Create("case", admission.Identity, admission.StoreId));
            Code("empty", admission.Unchanged(() => admission.Store(limits: exact).Open("case", admission.Identity, admission.StoreId, [admission.Pin])));
        }

        foreach (bool pointerCandidate in new[] { false, true })
        {
            var f = new Fixture(); var baseline = f.Prepare(); var prior = f.Save(baseline); Committed(baseline, prior);
            var p = f.Prepare(prior.Current, assets: [new byte[] { 3, 6, 9 }]);
            string boundary = pointerCandidate ? "before_replace:current.json" : "before_replace:journal/" + p.Intent.OperationId + ".json";
            f.Fault(p, boundary);
            string candidate = pointerCandidate ? f.Path("current." + p.Intent.OperationId + ".next.json")
                : f.Path("journal", p.Intent.OperationId + ".sealed.next.json");
            TestAssert.True(File.Exists(candidate), "Forward candidate regression retains a complete recognized replacement sibling.");
            Code("recovery_required", f.Unchanged(() => f.Open()));
            Code("abandon_denied", f.Unchanged(() => f.Abandon(p)));
            TestAssert.Equal(prior.Current, f.Open().Current, "Refusing abandonment with forward candidate leaves current unchanged.");
            Committed(p, f.Recover(p));
            TestAssert.False(File.Exists(candidate), "Explicit recovery consumes the matching forward replacement candidate.");
            Code("verified", f.Unchanged(() => f.Open()));
            Committed(p, f.Unchanged(() => f.Save(p)));
            TestAssert.True(f.Bundle.SequenceEqual(File.ReadAllBytes(f.Revision(baseline, "bundle.json"))), "Forward-candidate recovery leaves baseline immutable bytes unchanged.");
        }
    }

    public static void BStateRoundTripsWithoutAuthority()
    {
        var f = new Fixture(count: 1);
        var (state, policy, deniedContext, reviews) = ToolboxReconciliationTests.DiskLineageHistoryFixture();
        TestAssert.Equal(f.Identity, state.Model.Case, "B fixture has the externally supplied full disk case identity.");
        TestAssert.True(state.Model.Relations!.Boundaries.All(b => !b.PointIds.IsDefault && b.PointIds.Length > 0), "Disk B fixture uses admitted non-default physical-boundary point sets.");
        string fingerprint = state.Fingerprint;
        var codec = new ToolboxWorkspaceStorageCodec(); var policyBytes = Encoding.UTF8.GetBytes(policy.JsonSnapshot);
        var captured = codec.Capture(new(state.Model, state.Register, state, reviews, [new(policy.Pin, policyBytes)]));
        TestAssert.True(captured.Success, "Capture lineage, repeated archives and historical register for actual disk: " + string.Join(",", captured.Diagnostics.Select(d => d.Code)));
        TestAssert.Equal(3, captured.Value!.Models.Length, "B fixture contains three unique historical model states.");
        var encoded = codec.Encode(captured.Value!); TestAssert.True(encoded.Success, "Encode real supported B state.");
        var request = ToolboxPrototypeDiskContract.Prepare(f.StoreId, f.Identity, Id(), Id(), null, "examiner", Now,
            encoded.Value!, new ReadOnlyMemory<byte>[] { policyBytes });
        var saved = f.Store().Save("case", request, [policy.Pin]); Committed(request, saved);
        var opened = f.Unchanged(() => f.Store().Open("case", f.Identity, f.StoreId, [policy.Pin])); Code("verified", opened);
        var loaded = opened.Workspace!; var restored = loaded.Reconciliation!;
        TestAssert.Equal(fingerprint, restored.Fingerprint, "Disk reopen preserves exact B semantic fingerprint without replaying operations.");
        TestAssert.Equal(state.Revision, restored.Revision, "Domain reconciliation revision is preserved separately from disk revision GUID.");
        TestAssert.False(restored.Revision == opened.Current!.RevisionId, "Transport commit does not replace domain revision identity.");
        TestAssert.True(state.Operations.SequenceEqual(restored.Operations), "Accepted operation identities/digests survive disk unchanged.");
        TestAssert.Equal(state.AcceptedDispositions[0].AcceptedAt, restored.AcceptedDispositions[0].AcceptedAt, "Disk hydration never mints acceptance time.");
        TestAssert.True(state.AcceptedDispositions[0].Dispositions.SequenceEqual(restored.AcceptedDispositions[0].Dispositions), "Reviewed value dispositions retain historical choices.");
        TestAssert.True(state.Lineage[0].Proposal.ParentIds.SequenceEqual(restored.Lineage[0].Proposal.ParentIds), "Retired parent identity survives physical disk save/reopen.");
        TestAssert.True(state.Lineage[0].ChildIds.SequenceEqual(restored.Lineage[0].ChildIds), "Allocated lineage child IDs are not regenerated.");
        TestAssert.True(ReferenceEquals(loaded.Model.Relations!.Archives[0], loaded.Model.Relations.Archives[1].Relations!.Archives[0]), "Repeated historical archive is hydrated once from disk.");
        TestAssert.Equal(state.Register.HumanReviews.Single(), loaded.Register.HumanReviews.Single(), "Manual correction/raw-value audit survives disk.");
        TestAssert.True(state.Register.Findings[0].History.SequenceEqual(loaded.Register.Findings[0].History), "Source-defect event history survives disk without clearing or resolving it.");
        TestAssert.Equal(state.Register.Decisions.Single().Fingerprint, loaded.Register.Decisions.Single().Fingerprint, "Historical decision proof remains stale with its original fingerprint.");
        TestAssert.Equal(state.Register.Decisions.Single().At, loaded.Register.Decisions.Single().At, "Historical decision timestamp is not refreshed.");
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, loaded.Register.Decisions.Single().Status, "Historical blocked state is not promoted by transport commit.");
        TestAssert.True(loaded.CurrentCheckReviews.Any(r => r.Outcome == ToolboxCheckOutcome.Pending), "Pending current review remains pending after disk hydration.");
        TestAssert.False(loaded.Model.Relations.Dependencies.Complete, "Incomplete dependency coverage is not granted authority by reopen.");
        TestAssert.True(loaded.Model.Relations.ReadinessBarriers.Length > 0 && !loaded.Model.Relations.Boundaries[0].SemanticsVerified, "Unresolved applicability and geometry barriers remain on disk.");
        TestAssert.False(deniedContext.GeometryAccess().Allowed, "Hydrated B transport cannot grant local editing authority.");
        TestAssert.False(deniedContext.RemoteActionAccess.Allowed, "Hydrated B transport cannot grant remote action authority.");
        var recaptured = codec.Capture(new(loaded.Model, loaded.Register, restored, loaded.CurrentCheckReviews, [new(policy.Pin, policyBytes)]));
        TestAssert.True(recaptured.Success, "Recapture reopened B state.");
        TestAssert.True(encoded.Value!.SequenceEqual(codec.Encode(recaptured.Value!).Value!), "Complete B storage bytes, comparers, archives and policy payloads survive actual disk unchanged.");
        TestAssert.True(encoded.Value!.SequenceEqual(File.ReadAllBytes(f.Revision(request, "bundle.json"))), "Actual immutable revision contains exact B envelope bytes.");
        var successor = ToolboxPrototypeDiskContract.Prepare(f.StoreId, f.Identity, Id(), Id(), ToolboxPrototypeDiskContract.Parent(saved.Current!), "examiner", Now, encoded.Value!);
        bool sealedFault = false;
        var interrupted = f.Store(boundary: name =>
        {
            if (name == "after_seal") { sealedFault = true; throw new IOException("B history save interrupted after immutable seal"); }
        }).Save("case", successor, [policy.Pin]);
        TestAssert.True(sealedFault, "Real B successor save reaches immutable seal before interruption.");
        Code("recovery_required", interrupted);
        var pending = f.Unchanged(() => f.Store().Open("case", f.Identity, f.StoreId, [policy.Pin])); Code("recovery_required", pending);
        TestAssert.Equal(saved.Current, pending.Current, "Interrupted B successor leaves original pointer unchanged.");
        TestAssert.Equal(fingerprint, pending.Workspace!.Reconciliation!.Fingerprint, "Pending B inspection does not replay history.");
        TestAssert.True(encoded.Value!.SequenceEqual(File.ReadAllBytes(f.Revision(successor, "bundle.json"))), "Interrupted sealed B revision retains exact original bundle bytes.");
        var second = f.Store().Recover("case", f.Identity, f.StoreId, successor.Intent.OperationId, [policy.Pin]); Committed(successor, second);
        var recovered = second.Workspace!; var recoveredState = recovered.Reconciliation!;
        TestAssert.Equal(fingerprint, recoveredState.Fingerprint, "Explicit B recovery preserves the complete history fingerprint.");
        TestAssert.Equal(state.Reruns.Length, recoveredState.Reruns.Length, "Recovery adds no domain rerun.");
        TestAssert.Equal(state.Lineage.Length, recoveredState.Lineage.Length, "Recovery adds no lineage application.");
        TestAssert.Equal(state.Operations.Length, recoveredState.Operations.Length, "Recovery adds no accepted domain operation.");
        TestAssert.Equal(state.AcceptedDispositions.Length, recoveredState.AcceptedDispositions.Length, "Recovery adds no accepted disposition audit.");
        TestAssert.Equal(state.Model.Relations!.Archives.Length, recovered.Model.Relations!.Archives.Length, "Recovery adds no historical archive.");
        TestAssert.Equal(state.Register.HumanReviews.Length, recovered.Register.HumanReviews.Length, "Recovery adds no manual review.");
        TestAssert.Equal(state.Register.Findings.Length, recovered.Register.Findings.Length, "Recovery adds no finding.");
        TestAssert.Equal(state.Register.Decisions.Length, recovered.Register.Decisions.Length, "Recovery adds no examination decision.");
        TestAssert.True(state.AcceptedDispositions.Select(a => a.AcceptedAt).SequenceEqual(recoveredState.AcceptedDispositions.Select(a => a.AcceptedAt)), "Recovery retains every original acceptance timestamp.");
        TestAssert.True(state.Register.Findings[0].History.SequenceEqual(recovered.Register.Findings[0].History), "Recovery retains exact source-defect history events.");
        TestAssert.Equal(state.Register.Decisions.Single().At, recovered.Register.Decisions.Single().At, "Recovery never refreshes stale decision time.");
        var recoveredCapture = codec.Capture(new(recovered.Model, recovered.Register, recoveredState, recovered.CurrentCheckReviews, [new(policy.Pin, policyBytes)]));
        TestAssert.True(recoveredCapture.Success, "Recovered B state remains supported for storage capture.");
        TestAssert.True(encoded.Value!.SequenceEqual(codec.Encode(recoveredCapture.Value!).Value!), "B recovery preserves exact full envelope bytes including audits and pending reviews.");
        TestAssert.True(encoded.Value!.SequenceEqual(File.ReadAllBytes(f.Revision(successor, "bundle.json"))), "Recovery never rewrites immutable B payload bytes.");
        Committed(successor, f.Unchanged(() => f.Store().Recover("case", f.Identity, f.StoreId, successor.Intent.OperationId, [policy.Pin])));
        var retry = f.Unchanged(() => f.Store().Save("case", request, [policy.Pin])); Committed(request, retry);
        TestAssert.Equal(saved.Receipt, retry.Receipt, "Historical B retry returns exact original receipt after successor recovery.");
        TestAssert.Equal(second.Current, retry.Current, "Historical B retry does not move successor disk head.");
        TestAssert.Equal(fingerprint, retry.Workspace!.Reconciliation!.Fingerprint, "Successor/historical retry does not replay B lineage or actions.");
    }

    public static void CompetingProcessInitializersAndSaves()
    {
        var initialization = new Fixture(create: false); var ownerRequest = initialization.Prepare();
        WriteWorkerInput(initialization, ownerRequest, "one"); WriteWorkerInput(initialization, ownerRequest, "two");
        var creates = RunCompetingWorkers("race-create", initialization);
        TestAssert.Equal(1, creates.Count(r => r.Code == "created"), "Exactly one independent process claims a new case.");
        TestAssert.Equal(1, creates.Count(r => r.Code == "initialization_conflict"), "Competing initializer cannot adopt or overwrite winner.");
        Code("empty", initialization.Unchanged(() => initialization.Open()));
        var owner = initialization.Read<ToolboxDiskOwner>("owner.json");
        TestAssert.Equal(initialization.StoreId, owner.StoreId, "Winner preserves explicit store identity.");
        TestAssert.Equal(initialization.Identity, owner.Case, "Winner preserves full explicit case identity.");
        TestAssert.Equal(0, Directory.EnumerateFiles(initialization.Path("journal")).Count(), "Initialization race invents no save operation.");

        var saving = new Fixture(); var baseline = saving.Prepare(); var prior = saving.Save(baseline); Committed(baseline, prior);
        byte[] oldJournal = File.ReadAllBytes(saving.Path("journal", baseline.Intent.OperationId + ".json"));
        byte[] oldReceipt = File.ReadAllBytes(saving.Path("receipts", baseline.Intent.OperationId + ".json"));
        var one = saving.Prepare(prior.Current); var two = saving.Prepare(prior.Current);
        WriteWorkerInput(saving, one, "one"); WriteWorkerInput(saving, two, "two");
        var saves = RunCompetingWorkers("race-save", saving);
        TestAssert.Equal(1, saves.Count(r => r.Code == "committed"), "Exactly one independent same-parent save commits.");
        TestAssert.Equal(1, saves.Count(r => r.Code is "busy" or "parent_conflict"), "Loser is busy during winner or stale after winner.");
        int winnerIndex = saves[0].Code == "committed" ? 0 : 1;
        var winner = winnerIndex == 0 ? one : two; var loser = winnerIndex == 0 ? two : one;
        var outcome = saves[winnerIndex];
        TestAssert.True(outcome.TransportVerified && outcome.Writable, "Race winner reports committed only with verified transport.");
        TestAssert.Equal(ToolboxPrototypeDiskContract.Receipt(ToolboxPrototypeDiskContract.Revision(winner.Intent)), outcome.Receipt, "Race returns exact winner receipt.");
        TestAssert.Equal(ToolboxPrototypeDiskContract.Pointer(ToolboxPrototypeDiskContract.Revision(winner.Intent)), saving.Open().Current, "Current is exactly the single winner's revision.");
        TestAssert.Equal(outcome.Receipt, saving.Read<ToolboxDiskReceipt>("receipts", winner.Intent.OperationId + ".json"), "Winner receipt is physically present and exact.");
        TestAssert.False(File.Exists(saving.Path("journal", loser.Intent.OperationId + ".json")), "Loser creates no operation journal.");
        TestAssert.False(File.Exists(saving.Path("receipts", loser.Intent.OperationId + ".json")), "Loser creates no receipt.");
        TestAssert.False(Directory.Exists(saving.Path("staging", loser.Intent.OperationId)) || Directory.Exists(saving.Revision(loser)), "Loser creates no staging or immutable revision.");
        TestAssert.Equal(2, Directory.EnumerateFiles(saving.Path("journal")).Count(), "Only baseline and winner journals exist.");
        TestAssert.Equal(2, Directory.EnumerateFiles(saving.Path("receipts")).Count(), "Only baseline and winner receipts exist.");
        TestAssert.True(oldJournal.SequenceEqual(File.ReadAllBytes(saving.Path("journal", baseline.Intent.OperationId + ".json")))
            && oldReceipt.SequenceEqual(File.ReadAllBytes(saving.Path("receipts", baseline.Intent.OperationId + ".json")))
            && saving.Bundle.SequenceEqual(File.ReadAllBytes(saving.Revision(baseline, "bundle.json"))), "Competing processes preserve all baseline evidence exactly.");
        Committed(winner, saving.Unchanged(() => saving.Save(winner)));
    }

    private sealed record RaceOutcome(string State, string Code, bool TransportVerified, bool Writable,
        ToolboxDiskPointer? Current, ToolboxDiskReceipt? Receipt);

    private static RaceOutcome[] RunCompetingWorkers(string mode, Fixture f)
    {
        using var one = StartWorker(mode, f.Root, "one"); using var two = StartWorker(mode, f.Root, "two");
        var errorOne = one.StandardError.ReadToEndAsync(); var errorTwo = two.StandardError.ReadToEndAsync();
        var readyOne = one.StandardOutput.ReadLineAsync(); var readyTwo = two.StandardOutput.ReadLineAsync();
        System.Threading.Tasks.Task<string?>? resultOne = null, resultTwo = null;
        try
        {
            TestAssert.True(readyOne.Wait(30000) && readyTwo.Wait(30000), "Both independent workers reach common pre-operation barrier.");
            TestAssert.Equal("READY", readyOne.Result, "First independent contender is ready.");
            TestAssert.Equal("READY", readyTwo.Result, "Second independent contender is ready.");
            resultOne = one.StandardOutput.ReadLineAsync(); resultTwo = two.StandardOutput.ReadLineAsync();
            one.StandardInput.WriteLine("GO"); one.StandardInput.Flush(); two.StandardInput.WriteLine("GO"); two.StandardInput.Flush();
            TestAssert.True(resultOne.Wait(30000) && resultTwo.Wait(30000) && one.WaitForExit(30000) && two.WaitForExit(30000), "Competing workers complete within explicit bounds.");
            TestAssert.Equal(0, one.ExitCode, "First competing worker returns detached outcome.");
            TestAssert.Equal(0, two.ExitCode, "Second competing worker returns detached outcome.");
            return [JsonSerializer.Deserialize<RaceOutcome>(resultOne.Result!)!, JsonSerializer.Deserialize<RaceOutcome>(resultTwo.Result!)!];
        }
        finally
        {
            StopOwnedWorker(one); StopOwnedWorker(two);
            TestAssert.True(readyOne.Wait(5000) && readyTwo.Wait(5000) && errorOne.Wait(5000) && errorTwo.Wait(5000), "Competing worker readers finish after owned workers exit.");
            if (resultOne is not null) TestAssert.True(resultOne.Wait(5000), "First contender result reader completes.");
            if (resultTwo is not null) TestAssert.True(resultTwo.Wait(5000), "Second contender result reader completes.");
        }
    }

    public static void ForgedRevisionAliasesAndResolutionAdmissionFailBeforeMutation()
    {
        foreach (bool duplicateRevision in new[] { true, false })
        {
            var f = new Fixture(); var p = f.Prepare(); Committed(p, f.Save(p));
            var forgedIntent = p.Intent with { OperationId = duplicateRevision ? Id() : p.Intent.OperationId, Actor = "impostor-examiner" };
            var forgedRevision = ToolboxPrototypeDiskContract.Revision(forgedIntent);
            var forgedJournal = new ToolboxDiskJournal(ToolboxPrototypeDiskContract.Contract, 1, forgedRevision, null, "committed", null);
            var forgedReceipt = ToolboxPrototypeDiskContract.Receipt(forgedRevision);
            _ = ToolboxPrototypeDiskContract.Decode<ToolboxDiskJournal>(ToolboxPrototypeDiskContract.Encode(forgedJournal));
            _ = ToolboxPrototypeDiskContract.Decode<ToolboxDiskReceipt>(ToolboxPrototypeDiskContract.Encode(forgedReceipt));
            File.WriteAllBytes(f.Path("journal", forgedIntent.OperationId + ".json"), ToolboxPrototypeDiskContract.Encode(forgedJournal));
            File.WriteAllBytes(f.Path("receipts", forgedIntent.OperationId + ".json"), ToolboxPrototypeDiskContract.Encode(forgedReceipt));
            var observed = f.Unchanged(() => f.Open()); Code("operation_conflict", observed);
            TestAssert.Equal("corrupt", observed.State, "Valid self-hashed impostor metadata cannot bind a real immutable revision or grant writable use.");
            TestAssert.False(observed.Writable, "Duplicate or physically mismatched journal revision is never writable.");
            Code("operation_conflict", f.Unchanged(() => f.Save(p)));
            Code("operation_conflict", f.Unchanged(() => f.Recover(p)));
            Code("operation_conflict", f.Unchanged(() => f.Abandon(p)));
        }
        var recovery = new Fixture(); var request = recovery.Prepare(); recovery.Fault(request, "before_seal");
        TestAssert.Equal(12, Directory.EnumerateFileSystemEntries(System.IO.Path.Combine(recovery.Root, "case"), "*", SearchOption.AllDirectories).Count(), "Recovery admission fixture is complete initial staging with exactly twelve entries.");
        Code("recovery_required", recovery.Unchanged(() => recovery.Store(limits: new ToolboxDiskLimits(Entries: 26)).Open("case", recovery.Identity, recovery.StoreId, [recovery.Pin])));
        bool mutation = false;
        Code("limit_exceeded", recovery.Unchanged(() => recovery.Store(limits: new ToolboxDiskLimits(Entries: 26), boundary: _ => mutation = true)
            .Recover("case", recovery.Identity, recovery.StoreId, request.Intent.OperationId, [recovery.Pin])));
        TestAssert.False(mutation, "Recovery reserves publication, receipt and terminal evidence before seal or write.");
        TestAssert.False(File.Exists(recovery.Path("current.json")) || File.Exists(recovery.Path("receipts", request.Intent.OperationId + ".json")), "Denied resolution publishes no pointer or receipt.");
        Committed(request, recovery.Recover(request));

        var abandonment = new Fixture(); var partial = abandonment.Prepare(); abandonment.Fault(partial, "before_stage_create");
        int metadata = File.ReadAllBytes(abandonment.Path("journal", partial.Intent.OperationId + ".json")).Length;
        var longAudit = Fixture.Audit with { Reason = new string('r', 2048) };
        mutation = false;
        Code("limit_exceeded", abandonment.Unchanged(() => abandonment.Store(limits: new ToolboxDiskLimits(MetadataBytes: metadata), boundary: _ => mutation = true)
            .Abandon("case", abandonment.Identity, abandonment.StoreId, partial.Intent.OperationId, longAudit, [abandonment.Pin], originalRequest: partial)));
        TestAssert.False(mutation, "Abandonment reserves enlarged audited terminal metadata before creating a sibling.");
        Code("operation_abandoned", abandonment.Abandon(partial));
    }

    private static string CopyUnderWriterLock(Fixture source)
    {
        string root = System.IO.Path.Combine(TestRootBase(), Id()); Directory.CreateDirectory(root);
        var drive = new DriveInfo(System.IO.Path.GetPathRoot(root)!);
        TestAssert.True(drive.DriveType == DriveType.Fixed && drive.DriveFormat == "NTFS", "Relocation destination is an explicitly approved local fixed NTFS root.");
        using var sourceSession = new ToolboxContainedFileSystem(source.Root, true).OpenCase("case");
        using var sourceWriter = sourceSession.AcquireWriter();
        Code("busy", source.Open());
        using var destination = new ToolboxContainedFileSystem(root, true).OpenCase("case", createNew: true);
        foreach (var entry in sourceSession.Enumerate())
            if (entry.IsDirectory) destination.CreateDirectory(entry.RelativePath);
        using var destinationWriter = destination.AcquireWriter(createNew: true);
        TestAssert.Equal(0L, destination.Enumerate().Single(e => e.RelativePath == "toolbox/prototype-store/writer.lock").Length,
            "Destination lock is explicitly created empty, never copied from source.");
        foreach (var entry in sourceSession.Enumerate())
        {
            if (entry.IsDirectory || entry.RelativePath == "toolbox/prototype-store/writer.lock") continue;
            byte[] bytes = sourceSession.Read(entry.RelativePath, int.MaxValue);
            destination.WriteNew(entry.RelativePath, bytes);
            TestAssert.True(bytes.SequenceEqual(destination.Read(entry.RelativePath, int.MaxValue)), "Every copied ordinary file is read back under source/destination locks.");
        }
        return root;
    }

    private static ToolboxDiskResult UnchangedRoot(string root, Func<ToolboxDiskResult> action)
    {
        string before = TreeSnapshot(root); var result = action();
        TestAssert.Equal(before, TreeSnapshot(root), "Destination inspection/refusal never repairs copied evidence."); return result;
    }

    private static string TreeSnapshot(string root) => string.Join("\n", Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal).Select(path => System.IO.Path.GetRelativePath(root, path) + (Directory.Exists(path) ? "/" : ":" + ToolboxPrototypeDiskContract.Hash(File.ReadAllBytes(path)))));

    public static int RunWorker(string[] args)
    {
        string mode; string rootArgument; string? target = null; string? slot = null; int occurrence = 0;
        if (args.Length == 2 && args[0] == "inspect") { mode = "inspect"; rootArgument = args[1]; }
        else if (args.Length == 3 && (args[0] is "race-create" or "race-save") && (args[2] is "one" or "two"))
        { mode = args[0]; rootArgument = args[1]; slot = args[2]; }
        else if (args.Length == 4 && (args[0] is "save" or "create" or "abandon")
            && int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out occurrence) && occurrence is >= 1 and <= 64)
        { mode = args[0]; rootArgument = args[1]; target = args[2]; }
        else if (args.Length == 3 && int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out occurrence) && occurrence is >= 1 and <= 64)
        { mode = "save"; rootArgument = args[0]; target = "C:" + args[1]; }
        else return 2;
        string root = System.IO.Path.GetFullPath(rootArgument);
        string approved = System.IO.Path.GetFullPath(TestRootBase()).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        if (!root.StartsWith(approved, StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParseExact(root[approved.Length..], "N", out _)) return 2;
        string input = System.IO.Path.Combine(root, "worker-input");
        if (slot is not null) input = System.IO.Path.Combine(input, slot);
        var intent = ToolboxPrototypeDiskContract.Decode<ToolboxDiskIntent>(File.ReadAllBytes(System.IO.Path.Combine(input, "intent.json")));
        var policyBytes = Encoding.UTF8.GetBytes(ToolboxPolicyTests.Fixture().ToJsonString());
        var pin = new ToolboxPolicyPin("synthetic-toolbox-policy", "test-v1", ToolboxPrototypeDiskContract.Hash(policyBytes));
        if (mode == "inspect")
        {
            var result = new ToolboxPrototypeRevisionStore(new ToolboxContainedFileSystem(root, true)).Open("case", intent.Case, intent.StoreId, [pin]);
            Console.WriteLine(JsonSerializer.Serialize(Inspection(result))); Console.Out.Flush();
            return 0;
        }
        var bundle = File.ReadAllBytes(System.IO.Path.Combine(input, "bundle.json"));
        IEnumerable<ReadOnlyMemory<byte>>? assets = intent.Assets is { } inventory
            ? inventory.Select(a => (ReadOnlyMemory<byte>)File.ReadAllBytes(System.IO.Path.Combine(input, a.Sha256 + ".bin"))) : null;
        var p = ToolboxPrototypeDiskContract.Prepare(intent.StoreId, intent.Case, intent.OperationId, intent.ProposedRevisionId,
            intent.ExpectedParent, intent.Actor, DateTimeOffset.ParseExact(intent.PreparedAt, "O", CultureInfo.InvariantCulture), bundle, assets);
        TestAssert.True(ToolboxPrototypeDiskContract.Same(intent, p.Intent), "Worker reconstructs exact immutable prepared request.");
        if (mode is "race-create" or "race-save")
        {
            Console.WriteLine("READY"); Console.Out.Flush();
            if (Console.ReadLine() != "GO") return 2;
            var store = new ToolboxPrototypeRevisionStore(new ToolboxContainedFileSystem(root, true));
            var result = WorkerOperation(store, mode == "race-create" ? "create" : "save", p, pin);
            Console.WriteLine(JsonSerializer.Serialize(new RaceOutcome(result.State, result.Code, result.TransportVerified, result.Writable, result.Current, result.Receipt)));
            Console.Out.Flush(); return 0;
        }
        int hits = 0;
        void At(string name)
        {
            if (name != target || ++hits != occurrence) return;
            Console.WriteLine("READY"); Console.Out.Flush();
            // Parent terminates this owned process while the native session and writer lease remain held.
            _ = Console.ReadLine();
            throw new IOException("Worker barrier resumed instead of terminated.");
        }
        var previousHook = Native.TestBoundary;
        Native.TestBoundary = name => At("N:" + name);
        try
        {
            var store = new ToolboxPrototypeRevisionStore(new ToolboxContainedFileSystem(root, true), boundary: name => At("C:" + name));
            var result = WorkerOperation(store, mode, p, pin);
            Console.Error.WriteLine("Worker exited without termination: " + result.Code);
            return 3;
        }
        finally { Native.TestBoundary = previousHook; }
    }

    private static string TestRootBase() => Environment.GetEnvironmentVariable("TOOLBOX_DISK_TEST_ROOT")
        ?? System.IO.Path.Combine(Environment.CurrentDirectory, ".artifacts", "toolbox-disk-tests");

    private static void Code(string expected, ToolboxDiskResult result) => TestAssert.Equal(expected, result.Code, "Exact operation diagnostic; state=" + result.State);
    private static void Committed(ToolboxPreparedSave request, ToolboxDiskResult result) { Code("committed", result); VerifiedReceipt(request, result); }
    private static void VerifiedReceipt(ToolboxPreparedSave request, ToolboxDiskResult result)
    {
        TestAssert.True(result.TransportVerified && result.Writable, "Committed requires verified writable transport.");
        TestAssert.Equal(ToolboxPrototypeDiskContract.Receipt(ToolboxPrototypeDiskContract.Revision(request.Intent)), result.Receipt, "Exact original intent receipt.");
    }
    private static void ThrowsDisk(string code, Action action)
    {
        try { action(); }
        catch (ToolboxDiskException e) { TestAssert.Equal(code, e.Code, "Typed request refusal."); return; }
        throw new InvalidOperationException("Expected disk rejection: " + code);
    }

    private sealed class Fixture
    {
        public static readonly ToolboxDiskAbandonment Audit = new("examiner", Now.ToString("O", CultureInfo.InvariantCulture), "Discard interrupted synthetic save");
        public string Root { get; }
        public string StoreId { get; } = Id();
        public ToolboxParcelModel Model { get; }
        public ToolboxExaminationRegister Register { get; }
        public ToolboxCaseIdentity Identity => Model.Case;
        public ToolboxPolicyPin Pin { get; }
        public byte[] Bundle { get; }

        public Fixture(int count = 3, bool create = true, string policyVersion = "test-v1")
        {
            Root = System.IO.Path.Combine(TestRootBase(), Id());
            Directory.CreateDirectory(Root);
            var drive = new DriveInfo(System.IO.Path.GetPathRoot(Root)!);
            TestAssert.True(OperatingSystem.IsWindows() && drive.DriveType == DriveType.Fixed && drive.DriveFormat == "NTFS", "Integration tests require actual local fixed NTFS, without skips.");
            var context = new ToolboxCaseContext("synthetic", "case-a", "examiner",
                new SelectedInnolaTransaction("task", "transaction-a", "000000101", "Compute", "Compute", Now), Root, System.IO.Path.Combine(Root, "case"));
            var json = CanonicalReviewContractTests.Sample(count);
            json["provider_unknown"] = new JsonObject { ["nested"] = new JsonArray("original", 3, null) };
            Model = ToolboxParcelModel.Adapt(context, CanonicalReviewContract.Read(json), newImport: true);
            Register = new ToolboxExaminationRegister().RecordHumanReview(Model, new("ev", "raw-title", "No title recorded", "manual",
                ToolboxPresence.MissingFromSource, ToolboxAccuracy.Confirmed, ToolboxPreset.Compare, "examiner", Now, "Confirmed source absence", "old-input"), new("examiner", "old-policy", true));
            var policy = ToolboxPolicyTests.Fixture(); policy["policy_version"] = policyVersion;
            var policyBytes = Encoding.UTF8.GetBytes(policy.ToJsonString());
            Pin = new("synthetic-toolbox-policy", policyVersion, ToolboxPrototypeDiskContract.Hash(policyBytes));
            _ = ToolboxReviewPolicyLoader.Load(policyBytes, Pin);
            var codec = new ToolboxWorkspaceStorageCodec();
            var capture = codec.Capture(new(Model, Register, CurrentCheckReviews: [new("old-proof", ToolboxCheckOutcome.Pending,
                "LTD unavailable", ["ev"], "examiner", Now, GeometryFingerprints: ImmutableDictionary<string, string>.Empty)], PolicyAssets: [new(Pin, policyBytes)]));
            TestAssert.True(capture.Success, "Capture real A policy fixture: " + string.Join(",", capture.Diagnostics.Select(d => d.Code)));
            var encoded = codec.Encode(capture.Value!);
            TestAssert.True(encoded.Success, "Encode real A fixture."); Bundle = encoded.Value!;
            if (create) Code("created", Store().Create("case", Identity, StoreId));
        }

        public ToolboxPrototypeRevisionStore Store(Action<string>? boundary = null, ToolboxDiskLimits? limits = null) => new(new ToolboxContainedFileSystem(Root, true), limits, boundary);
        public ToolboxDiskResult Open() => Store().Open("case", Identity, StoreId, [Pin]);
        public ToolboxDiskResult Save(ToolboxPreparedSave p) => Store().Save("case", p, [Pin]);
        public ToolboxDiskResult Recover(ToolboxPreparedSave p) => Store().Recover("case", Identity, StoreId, p.Intent.OperationId, [Pin]);
        public ToolboxDiskResult Abandon(ToolboxPreparedSave p, ToolboxDiskAbandonment? audit = null, ToolboxPreparedSave? original = null)
            => Store().Abandon("case", Identity, StoreId, p.Intent.OperationId, audit ?? Audit, [Pin], originalRequest: original ?? p);
        public ToolboxPreparedSave Prepare(ToolboxDiskPointer? parent = null, IEnumerable<ReadOnlyMemory<byte>>? assets = null, string? revisionId = null)
            => ToolboxPrototypeDiskContract.Prepare(StoreId, Identity, Id(), revisionId ?? Id(), parent is null ? null : ToolboxPrototypeDiskContract.Parent(parent), "examiner", Now, Bundle, assets);
        public string Path(params string[] parts) => System.IO.Path.Combine(new[] { Root, "case", "toolbox", "prototype-store" }.Concat(parts).ToArray());
        public string Revision(ToolboxPreparedSave p, params string[] parts) => Path(new[] { "revisions", p.Intent.ProposedRevisionId }.Concat(parts).ToArray());
        public T Read<T>(params string[] parts) where T : class => ToolboxPrototypeDiskContract.Decode<T>(File.ReadAllBytes(Path(parts)));
        public ToolboxDiskJournal Journal(ToolboxPreparedSave p) => Read<ToolboxDiskJournal>("journal", p.Intent.OperationId + ".json");
        public ToolboxDiskResult Unchanged(Func<ToolboxDiskResult> action)
        {
            var before = Snapshot(); var result = action(); TestAssert.Equal(before, Snapshot(), "Read/refusal must leave every directory and exact file byte unchanged."); return result;
        }
        private string Snapshot() => TreeSnapshot(Root);
        public void Fault(ToolboxPreparedSave p, string target, int occurrence = 1)
        {
            bool hit = false; int hits = 0;
            var result = Store(boundary: name => { if (name == target && ++hits == occurrence) { hit = true; throw new IOException("synthetic boundary failure"); } }).Save("case", p, [Pin]);
            TestAssert.True(hit, "Requested actual persistence boundary must fire: " + target);
            TestAssert.False(result.Code is "committed" or "committed_late_cancellation", "Fault cannot falsely report committed: " + target);
        }
        public bool CompleteBytes(ToolboxPreparedSave p)
        {
            string directory = Directory.Exists(Revision(p)) ? Revision(p) : Path("staging", p.Intent.OperationId);
            if (!File.Exists(System.IO.Path.Combine(directory, "revision.json")) || !File.Exists(System.IO.Path.Combine(directory, "bundle.json"))) return false;
            if (!p.Bundle.AsSpan().SequenceEqual(File.ReadAllBytes(System.IO.Path.Combine(directory, "bundle.json")))) return false;
            if (p.Intent.Assets is not null && !Directory.Exists(System.IO.Path.Combine(directory, "assets"))) return false;
            return p.Intent.Assets is not { } assets || assets.All(a => File.Exists(System.IO.Path.Combine(directory, "assets", a.Sha256 + ".bin"))
                && ToolboxPrototypeDiskContract.Hash(File.ReadAllBytes(System.IO.Path.Combine(directory, "assets", a.Sha256 + ".bin"))) == a.Sha256);
        }
    }
}

internal static partial class ToolboxReconciliationTests
{
    internal static (ToolboxReconciliationSnapshot State, ToolboxReviewPolicy Policy, ToolboxCaseContext DeniedContext,
        ImmutableArray<ToolboxCheckReview> Reviews) DiskLineageHistoryFixture()
    {
        var policy = Policy(); var initial = WithFinding(Start(policy: policy));
        var historicalReviews = Reviews(initial, policy).Select((r, i) => i == 0
            ? r with { Outcome = ToolboxCheckOutcome.Failed, Reason = "Historical blocked geometry review" } : r).ToArray();
        var historicalDecision = Evaluate(initial, policy, historicalReviews);
        TestAssert.Equal(ToolboxReadinessStatus.Blocked, historicalDecision.Status, "B fixture starts with a real blocked historical decision.");
        initial = initial with { Register = initial.Register.RecordDecision(historicalDecision) };
        var split = ToolboxReconciliation.Accept(initial, LineageProposal(initial, policy, 2), Context(), policy, Authority);
        var result = ToolboxReconciliation.Accept(split, ToolboxReconciliation.Seal(Proposal(split, policy, split.Model.Source)
            with { OperationId = "disk-b-second", ResultRevision = "disk-b-second-revision" }), Context(), policy, Authority);
        var membership = result.Model.SegmentMemberships[0];
        var point = result.Model.Rows.First(r => r.ParcelId == membership.ParcelId);
        var boundary = new ToolboxPhysicalBoundary("disk-b-boundary", result.Model.Case, result.Revision,
            "synthetic-geometry-digest", Geometry, false, ["ev"],
            [new(membership.ParcelId, membership.EntityId, membership.Sequence, membership.Direction, "boundary")],
            ToolboxBoundaryConsistency.Unresolved, "examiner", "Retained unverified physical boundary", ToolboxReconciliation.PolicyVersion, [point.Id]);
        var dependencies = Graph(result.Model, policy, [boundary]) with { Complete = false };
        result = result with { Model = result.Model.WithRelations(result.Model.Relations! with { Boundaries = [boundary], Dependencies = dependencies }) };
        var reviews = Reviews(result, policy).Select((r, i) => i == 0 ? r with { Outcome = ToolboxCheckOutcome.Pending, Reason = "Retained pending disk review" } : r).ToImmutableArray();
        return (result, policy, Context(false), reviews);
    }
}
