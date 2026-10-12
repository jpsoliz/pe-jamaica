using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using ParcelWorkflowAddIn.Workflow.Toolbox.Model;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

namespace ParcelWorkflowAddIn.Workflow.Toolbox.Persistence;

public sealed record ToolboxDiskResult(string State, string Code, bool TransportVerified = false,
    bool Writable = false, string? StoreId = null, ToolboxDiskPointer? Current = null,
    ToolboxDiskReceipt? Receipt = null, ToolboxHydratedWorkspace? Workspace = null,
    ImmutableArray<ToolboxStorageDiagnostic> Diagnostics = default);

/// <summary>Detached prototype transport only. No domain actions, host activation or operational writes.</summary>
public sealed class ToolboxPrototypeRevisionStore
{
    private const string Root = "toolbox/prototype-store/";
    private readonly ToolboxContainedFileSystem files;
    private readonly ToolboxDiskLimits limits;
    private readonly ToolboxWorkspaceStorageCodec codec = new();
    private readonly Action<string>? boundary;
    private readonly AsyncLocal<Budget?> requestBudget = new();

    public ToolboxPrototypeRevisionStore(ToolboxContainedFileSystem files, ToolboxDiskLimits? limits = null,
        Action<string>? boundary = null)
    { this.files = files; this.limits = limits ?? new(); this.limits.Validate(); this.boundary = boundary; }

    public ToolboxDiskResult Create(string casePath, ToolboxCaseIdentity identity, string storeId,
        CancellationToken cancellation = default)
    {
        bool claimed = false;
        try
        {
            var owner = new ToolboxDiskOwner(ToolboxPrototypeDiskContract.Contract, 1, identity, storeId, "prototype_store");
            ToolboxPrototypeDiskContract.Validate(owner);
            var ownerBytes = ToolboxPrototypeDiskContract.Encode(owner);
            _ = ToolboxPrototypeDiskContract.Decode<ToolboxDiskOwner>(ownerBytes, limits);
            Require(limits.Entries >= 16 && limits.TraversalBytes >= ownerBytes.Length, "limit_exceeded");
            cancellation.ThrowIfCancellationRequested(); Hit("before_case_create");
            cancellation.ThrowIfCancellationRequested();
            using var session = files.OpenCase(casePath, createNew: true); claimed = true; Hit("after_case_create");
            foreach (var dir in new[] { "toolbox", "toolbox/prototype-store", Root + "journal", Root + "staging", Root + "revisions", Root + "receipts" })
                Mutate("create_directory", () => session.CreateDirectory(dir), cancellation);
            cancellation.ThrowIfCancellationRequested(); Hit("before_lock_create");
            using var writer = session.AcquireWriter(createNew: true); Hit("after_lock_create");
            Write(session, Root + "owner.json", owner, cancellation);
            return new("empty", "created", StoreId: storeId);
        }
        catch (OperationCanceledException) { return new(claimed ? "pending" : "empty", claimed ? "initialization_incomplete" : "cancelled"); }
        catch (Exception e) when (Known(e))
        { return claimed ? new("corrupt", "initialization_incomplete") : Failure(e, initialization: true); }
    }

    public ToolboxDiskResult Open(string casePath, ToolboxCaseIdentity identity, string storeId,
        IEnumerable<ToolboxPolicyPin>? trustedPins = null) => Run(casePath, identity, storeId, trustedPins,
            (session, view, pins) => view.Result);

    public ToolboxDiskResult Save(string casePath, ToolboxPreparedSave prepared,
        IEnumerable<ToolboxPolicyPin>? trustedPins = null, CancellationToken cancellation = default)
    {
        try
        {
            // A caller can construct this public record directly: validate its owned bytes again before any I/O.
            ValidatePrepared(prepared); cancellation.ThrowIfCancellationRequested();
            var pins = Pins(trustedPins);
            var hydrated = Hydrate(prepared.Bundle.AsMemory(), prepared.Intent.Case, pins);
            Require(hydrated.Success, "invalid_bundle");
            if (!Trusted(hydrated)) return new("verified", "policy_inspection_required", Workspace: hydrated.Value, Diagnostics: hydrated.Diagnostics);
            return Run(casePath, prepared.Intent.Case, prepared.Intent.StoreId, pins, (session, view, trust) =>
            {
                var intent = prepared.Intent;
                if (view.Journals.TryGetValue(intent.OperationId, out var existing))
                {
                    if (!ToolboxPrototypeDiskContract.Same(intent, ToolboxPrototypeDiskContract.Intent(existing.Revision)))
                        return view.Result with { Code = "operation_conflict", Writable = false };
                    var plan = Operation(session, view, existing, trust);
                    if (existing.State == "abandoned") return view.Result with { Code = "operation_abandoned", Writable = false };
                    if (plan.Code != "committed" && plan.Code != "policy_inspection_required")
                        return view.Result with { Code = plan.State == "pending" ? "recovery_required" : plan.Code, Writable = false };
                    return view.Result with { Code = "committed", Receipt = view.Receipts[intent.OperationId] };
                }
                if (view.Revisions.Values.Any(r => r.RevisionId == intent.ProposedRevisionId || r.OperationId == intent.OperationId)
                    || view.Journals.Values.Any(j => j.Revision.RevisionId == intent.ProposedRevisionId))
                    return view.Result with { Code = "operation_conflict", Writable = false };
                if (view.Result.State is not ("empty" or "verified") || view.ReplacementExists)
                    return view.Result with { Code = view.Result.State == "unsupported" ? "unsupported" : "recovery_required", Writable = false };
                if (view.Current is not null && !view.Result.Writable)
                    return view.Result with { Code = "policy_inspection_required", Writable = false };
                var parent = view.Current is null ? null : ToolboxPrototypeDiskContract.Parent(view.Current);
                if (parent != intent.ExpectedParent) return view.Result with { Code = "parent_conflict", Writable = false };
                CheckNewLimits(view);
                var revision = ToolboxPrototypeDiskContract.Revision(intent);
                var journal = new ToolboxDiskJournal(ToolboxPrototypeDiskContract.Contract, 1, revision, view.Current, "prepared", null);
                ReserveSave(view, journal);
                session.ValidateReplacementCapability(Root + "owner.json");
                bool mutated = false, published = false;
                try
                {
                    cancellation.ThrowIfCancellationRequested(); mutated = true;
                    Write(session, JournalPath(intent.OperationId), journal, cancellation);
                    string stage = Stage(intent.OperationId);
                    Mutate("stage_create", () => session.CreateDirectory(stage), cancellation);
                    Write(session, stage + "/revision.json", revision, cancellation);
                    WriteBytes(session, stage + "/bundle.json", prepared.Bundle.AsSpan(), cancellation);
                    if (prepared.Assets is { } assets)
                    {
                        Mutate("assets_create", () => session.CreateDirectory(stage + "/assets"), cancellation);
                        for (int i = 0; i < assets.Length; i++)
                            WriteBytes(session, stage + "/assets/" + revision.Assets!.Value[i].Sha256 + ".bin", assets[i].AsSpan(), cancellation);
                    }
                    Require(Complete(session, stage, revision, trust, new Budget(limits)), "incomplete_staging");
                    Mutate("seal", () => session.SealDirectory(stage, RevisionPath(revision.RevisionId)), cancellation);
                    journal = Advance(session, journal, "sealed", cancellation);
                    Publish(session, revision, cancellation); published = true;
                    // Publication is irreversible. A late cancellation must not report that nothing was saved.
                    journal = Advance(session, journal, "published", CancellationToken.None);
                    Tail(session, journal);
                    var result = Inspect(session, intent.Case, intent.StoreId, trust).Result;
                    return result with { Code = cancellation.IsCancellationRequested ? "committed_late_cancellation" : "committed", Receipt = ToolboxPrototypeDiskContract.Receipt(revision) };
                }
                catch (Exception e) when (Known(e) || e is OperationCanceledException)
                {
                    if (!mutated) return view.Result with { Code = "cancelled" };
                    if (!session.Exists(JournalPath(intent.OperationId)))
                        return view.Result with { Code = e is OperationCanceledException ? "cancelled" : Failure(e).Code };
                    var after = SafeInspect(session, intent.Case, intent.StoreId, trust);
                    return after with { Writable = false, Code = published || after.Current?.RevisionId == revision.RevisionId ? "published_recovery_required" : after.State is "corrupt" or "quarantined" ? after.Code : "recovery_required" };
                }
            });
        }
        catch (OperationCanceledException) { return new("empty", "cancelled"); }
        catch (Exception e) when (Known(e)) { return Failure(e); }
    }

    public ToolboxDiskResult Recover(string casePath, ToolboxCaseIdentity identity, string storeId, string operationId,
        IEnumerable<ToolboxPolicyPin>? trustedPins = null, CancellationToken cancellation = default)
        => Run(casePath, identity, storeId, trustedPins, (session, view, pins) =>
        {
            ToolboxPrototypeDiskContract.Id(operationId);
            if (!view.Journals.TryGetValue(operationId, out var journal)) return view.Result with { Code = "operation_missing", Writable = false };
            var plan = Operation(session, view, journal, pins);
            if (plan.State is "corrupt" or "quarantined" || plan.Code == "operation_abandoned")
                return view.Result with { State = plan.State, Code = plan.Code, Writable = false };
            if (view.Pending.Any(id => id != operationId)) return view.Result with { Code = "competing_intents", Writable = false };
            if (view.ReplacementOperations.Any(id => id != operationId)) return view.Result with { Code = "ambiguous_artifacts", Writable = false };
            if (view.PendingAbandonments.ContainsKey(operationId)) return view.Result with { Code = "abandon_required", Writable = false };
            if (view.Hydrated is not null && !Trusted(view.Hydrated)) return view.Result with { Code = "policy_inspection_required", Writable = false };
            var revision = journal.Revision;
            var location = view.Chain.Contains(revision.RevisionId) || session.Exists(RevisionPath(revision.RevisionId))
                ? RevisionPath(revision.RevisionId) : Stage(operationId);
            if (!Complete(session, location, revision, pins, view.Budget)) return view.Result with { Code = "incomplete_staging", Writable = false };
            if (!Trusted(Hydrate(session.Read(location + "/bundle.json", new ToolboxStorageLimits().BundleBytes), identity, pins)))
                return view.Result with { Code = "policy_inspection_required", Writable = false };
            session.ValidateReplacementCapability(Root + "owner.json");
            cancellation.ThrowIfCancellationRequested();
            if (plan.Code == "committed" || plan.Code == "policy_inspection_required")
                return view.Result with { Receipt = view.Receipts[operationId], Code = "committed" };
            ReserveResolution(view, journal with { State = "committed" }, includePublication: !view.Chain.Contains(revision.RevisionId));
            if (!view.Chain.Contains(revision.RevisionId))
            {
                if (plan.Actions.Contains("seal"))
                    Mutate("seal", () => session.SealDirectory(Stage(operationId), RevisionPath(revision.RevisionId)), cancellation);
                if (journal.State == "prepared") journal = Advance(session, journal, "sealed", cancellation);
                Publish(session, revision, cancellation);
            }
            // A historical tail completion never changes current.
            if (journal.State == "prepared") journal = Advance(session, journal, "sealed", CancellationToken.None);
            if (journal.State == "sealed") journal = Advance(session, journal, "published", CancellationToken.None);
            Tail(session, journal);
            var after = Inspect(session, identity, storeId, pins).Result;
            return after with { State = "recovered", Code = "committed", Receipt = ToolboxPrototypeDiskContract.Receipt(revision) };
        });

    public ToolboxDiskResult Abandon(string casePath, ToolboxCaseIdentity identity, string storeId, string operationId,
        ToolboxDiskAbandonment audit, IEnumerable<ToolboxPolicyPin>? trustedPins = null, CancellationToken cancellation = default,
        ToolboxPreparedSave? originalRequest = null)
        => Run(casePath, identity, storeId, trustedPins, (session, view, pins) =>
        {
            ToolboxPrototypeDiskContract.Id(operationId); ToolboxPrototypeDiskContract.Validate(audit);
            if (!view.Journals.TryGetValue(operationId, out var journal)) return view.Result with { Code = "operation_missing", Writable = false };
            if (journal.State == "abandoned") return view.Result with { Code = journal.Abandonment == audit ? "operation_abandoned" : "abandonment_conflict" };
            if (view.PendingAbandonments.TryGetValue(operationId, out var pendingAudit) && pendingAudit != audit)
                return view.Result with { Code = "abandonment_conflict", Writable = false };
            var plan = Operation(session, view, journal, pins);
            if (!plan.Actions.Contains("abandon") || view.Chain.Contains(journal.Revision.RevisionId)
                || view.Pending.Any(id => id != operationId) || view.ReplacementOperations.Any(id => id != operationId))
                return view.Result with { Code = "abandon_denied", Writable = false };
            if (view.Siblings.Any(s => s != JournalSibling(operationId, "abandoned")))
                return view.Result with { Code = "abandon_denied", Writable = false };
            if (view.Hydrated is not null && !Trusted(view.Hydrated)) return view.Result with { Code = "policy_inspection_required", Writable = false };
            // Partial staging may not contain a bundle; external pins still must match the validated proposed bundle.
            var location = session.Exists(RevisionPath(journal.Revision.RevisionId)) ? RevisionPath(journal.Revision.RevisionId) : Stage(operationId);
            byte[] bytes;
            if (originalRequest is not null)
            {
                ValidatePrepared(originalRequest);
                Require(ToolboxPrototypeDiskContract.Same(originalRequest.Intent, ToolboxPrototypeDiskContract.Intent(journal.Revision)), "operation_conflict");
                bytes = originalRequest.Bundle.ToArray();
            }
            else if (session.Exists(location + "/bundle.json")) bytes = session.Read(location + "/bundle.json", new ToolboxStorageLimits().BundleBytes);
            else return view.Result with { Code = "policy_inspection_required", Writable = false };
            if (bytes.LongLength != journal.Revision.Bundle.ByteLength || ToolboxPrototypeDiskContract.Hash(bytes) != journal.Revision.Bundle.Sha256
                || !Trusted(Hydrate(bytes, identity, pins))) return view.Result with { Code = "policy_inspection_required", Writable = false };
            session.ValidateReplacementCapability(Root + "owner.json");
            ReserveResolution(view, journal with { State = "abandoned", Abandonment = audit }, includePublication: false);
            Atomic(session, JournalPath(operationId), JournalSibling(operationId, "abandoned"), journal with { State = "abandoned", Abandonment = audit }, cancellation);
            return Inspect(session, identity, storeId, pins).Result with { Code = "operation_abandoned" };
        });

    private ToolboxDiskResult Run(string casePath, ToolboxCaseIdentity identity, string storeId,
        IEnumerable<ToolboxPolicyPin>? trustedPins,
        Func<ToolboxFileSession, View, ToolboxPolicyPin[], ToolboxDiskResult> operation)
    {
        var previousBudget = requestBudget.Value;
        requestBudget.Value = new Budget(limits);
        try
        {
            var pins = Pins(trustedPins);
            using var session = files.OpenCase(casePath);
            using var writer = session.AcquireWriter();
            var view = Inspect(session, identity, storeId, pins);
            try { return operation(session, view, pins); }
            catch (Exception e) when (Known(e) || e is OperationCanceledException)
            {
                if (!requestBudget.Value!.MutationStarted)
                    return view.Result with { Writable = false, State = e is ToolboxFileSystemException { Code: "unsupported" } ? "unsupported"
                        : e is ToolboxFileSystemException { Code: "busy" } ? "busy" : view.Result.State,
                        Code = e is OperationCanceledException ? "cancelled" : Failure(e).Code };
                var after = SafeInspect(session, identity, storeId, pins);
                if (after.State is "corrupt" or "quarantined") return after;
                if (e is ToolboxFileSystemException { Code: "unsupported" }) return after with { State = "unsupported", Code = "unsupported", Writable = false };
                if (e is ToolboxFileSystemException { Code: "busy" }) return after with { State = "busy", Code = "busy", Writable = false };
                return after with { Writable = false, Code = e is ToolboxDiskException disk ? disk.Code : after.State == "pending" ? "recovery_required" : e is OperationCanceledException ? "cancelled" : "operation_outcome_requires_inspection" };
            }
        }
        catch (OperationCanceledException) { return new("pending", "recovery_required"); }
        catch (Exception e) when (Known(e)) { return Failure(e); }
        finally { requestBudget.Value = previousBudget; }
    }

    private View Inspect(ToolboxFileSession session, ToolboxCaseIdentity identity, string storeId, ToolboxPolicyPin[] pins)
    {
        var budget = requestBudget.Value ?? new Budget(limits);
        var owner = Read<ToolboxDiskOwner>(session, Root + "owner.json", budget);
        Require(owner.Case == identity && owner.StoreId == storeId, "owner_conflict");
        var view = new View(owner, budget); var directories = new HashSet<string>(StringComparer.Ordinal);
        int stages = 0;
        foreach (var entry in session.Enumerate())
        {
            budget.Entry();
            view.PhysicalEntries++;
            if (!entry.IsDirectory && entry.RelativePath.EndsWith(".json", StringComparison.Ordinal)
                && !entry.RelativePath.EndsWith("/bundle.json", StringComparison.Ordinal))
                view.PhysicalMetadataBytes = checked(view.PhysicalMetadataBytes + entry.Length);
            Require(entry.RelativePath.Split('/').Length - 2 <= limits.DirectoryDepth, "limit_exceeded");
            if (entry.RelativePath == "toolbox") { Require(entry.IsDirectory, "unexpected_layout"); continue; }
            Require(entry.RelativePath.StartsWith(Root, StringComparison.Ordinal) || entry.RelativePath == Root.TrimEnd('/'), "unexpected_layout");
            var path = entry.RelativePath.Length >= Root.Length ? entry.RelativePath[Root.Length..] : "";
            var parts = path.Split('/');
            if (entry.IsDirectory)
            {
                bool allowed = path == "" || parts.Length == 1 && parts[0] is "journal" or "receipts" or "staging" or "revisions"
                    || parts.Length is 2 or 3 && parts[0] is "staging" or "revisions" && IsId(parts[1]) && (parts.Length == 2 || parts[2] == "assets");
                Require(allowed, "unexpected_layout"); directories.Add(path);
                if (parts.Length == 2 && parts[0] == "staging") { Require(++stages <= limits.Records, "limit_exceeded"); view.Stages.Add(parts[1]); }
                if (parts.Length == 2 && parts[0] == "revisions")
                { Require(view.RevisionDirectories.Count < limits.Records, "limit_exceeded"); view.RevisionDirectories.Add(parts[1]); }
                continue;
            }
            Require(entry.Length >= 0, "invalid_metadata");
            if (path == "writer.lock") { Require(entry.Length == 0, "invalid_lock"); continue; }
            if (path == "owner.json") continue;
            if (path == "current.json") { view.Current = Read<ToolboxDiskPointer>(session, entry.RelativePath, budget); continue; }
            if (parts.Length == 2 && parts[0] is "journal" or "receipts" && parts[1].EndsWith(".json", StringComparison.Ordinal))
            {
                var name = parts[1][..^5];
                if (parts[0] == "journal" && name.Contains('.'))
                { view.Siblings.Add(entry.RelativePath); continue; }
                ToolboxPrototypeDiskContract.Id(name);
                if (parts[0] == "journal")
                {
                    Require(view.Journals.Count < limits.Records, "limit_exceeded");
                    var journal = Read<ToolboxDiskJournal>(session, entry.RelativePath, budget);
                    Require(journal.Revision.OperationId == name, "operation_conflict"); view.Journals.Add(name, journal);
                }
                else
                {
                    Require(view.Receipts.Count < limits.Records, "limit_exceeded");
                    var receipt = Read<ToolboxDiskReceipt>(session, entry.RelativePath, budget);
                    Require(receipt.OperationId == name, "receipt_conflict"); view.Receipts.Add(name, receipt);
                }
                continue;
            }
            if (parts.Length == 1 && path.StartsWith("current.", StringComparison.Ordinal) && path.EndsWith(".next.json", StringComparison.Ordinal))
            { view.Siblings.Add(entry.RelativePath); continue; }
            if (parts.Length is 3 or 4 && parts[0] is "revisions" or "staging" && IsId(parts[1]))
            {
                if (parts.Length == 3 && parts[2] is "revision.json" or "bundle.json")
                {
                    if (parts[2] == "revision.json")
                    {
                        var revision = Read<ToolboxDiskRevision>(session, entry.RelativePath, budget); Binding(revision, owner);
                        Require(parts[1] == (parts[0] == "staging" ? revision.OperationId : revision.RevisionId), "operation_conflict");
                        if (parts[0] == "revisions") { Require(view.Revisions.Count < limits.Records, "limit_exceeded"); view.Revisions.Add(revision.RevisionId, revision); }
                        else view.StagedRevisions.Add(revision.OperationId, revision);
                    }
                    else Require(entry.Length <= new ToolboxStorageLimits().BundleBytes, "limit_exceeded");
                    continue;
                }
                if (parts.Length == 4 && parts[2] == "assets" && parts[3].EndsWith(".bin", StringComparison.Ordinal))
                { ToolboxPrototypeDiskContract.Digest(parts[3][..^4]); Require(entry.Length <= limits.AssetBytes, "limit_exceeded"); continue; }
            }
            throw new ToolboxDiskException("unexpected_layout");
        }
        foreach (string d in new[] { "", "journal", "receipts", "staging", "revisions" }) Require(directories.Contains(d), "initialization_incomplete");
        var proposedRevisionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var journal in view.Journals.Values)
        {
            Binding(journal.Revision, owner);
            Require(proposedRevisionIds.Add(journal.Revision.RevisionId), "operation_conflict");
        }
        foreach (var receipt in view.Receipts.Values)
        { Require(receipt.StoreId == storeId && receipt.Case == identity && view.Journals.TryGetValue(receipt.OperationId, out var j)
                && ToolboxPrototypeDiskContract.Same(receipt, ToolboxPrototypeDiskContract.Receipt(j.Revision)), "receipt_conflict"); }
        foreach (var revision in view.Revisions.Values)
        { Require(view.Journals.TryGetValue(revision.OperationId, out var j) && ToolboxPrototypeDiskContract.Same(j.Revision, revision), "operation_conflict"); }
        foreach (string stage in view.Stages) Require(view.Journals.ContainsKey(stage), "unexpected_layout");
        foreach (var staged in view.StagedRevisions.Values)
            Require(view.Journals.TryGetValue(staged.OperationId, out var journal)
                && ToolboxPrototypeDiskContract.Same(staged, journal.Revision), "operation_conflict");
        foreach (string revision in view.RevisionDirectories) Require(view.Revisions.ContainsKey(revision), "partial_revision");
        CheckInventories(session, view);
        if (view.Current is not null)
        {
            Require(view.Current.StoreId == storeId && view.Current.Case == identity, "owner_conflict");
            view.Chain.UnionWith(ToolboxPrototypeRecovery.VerifyAncestry(view.Current, view.Revisions, limits.Records));
            var currentRevision = view.Revisions[view.Current.RevisionId];
            Require(Complete(session, RevisionPath(currentRevision.RevisionId), currentRevision, pins, budget), "published_bytes_missing");
            view.Hydrated = Hydrate(session.Read(RevisionPath(currentRevision.RevisionId) + "/bundle.json", new ToolboxStorageLimits().BundleBytes), identity, pins);
        }
        ValidateSiblings(session, view);
        foreach (var journal in view.Journals.Values)
        {
            bool onChain = view.Chain.Contains(journal.Revision.RevisionId);
            if (onChain)
            {
                Require(ToolboxPrototypeDiskContract.Same(journal.Revision, view.Revisions[journal.Revision.RevisionId]), "operation_conflict");
                Require(!view.Stages.Contains(journal.Revision.OperationId) && journal.State != "abandoned", "ambiguous_artifacts");
                if (journal.State != "committed" || !view.Receipts.ContainsKey(journal.Revision.OperationId)) view.Pending.Add(journal.Revision.OperationId);
            }
            else if (journal.State is "published" or "committed" || view.Receipts.ContainsKey(journal.Revision.OperationId))
                throw new ToolboxDiskException("publication_unproven");
            else if (journal.State != "abandoned")
            {
                Require(journal.PreviousPointer == view.Current, "parent_conflict"); view.Pending.Add(journal.Revision.OperationId);
            }
            if (view.Stages.Contains(journal.Revision.OperationId) && view.Revisions.ContainsKey(journal.Revision.RevisionId))
                throw new ToolboxDiskException("ambiguous_artifacts");
        }
        Require(view.Pending.Count <= 1, "competing_intents");
        bool trusted = view.Hydrated is not null && Trusted(view.Hydrated);
        var plan = ToolboxPrototypeRecovery.PlanStore(new(true, true, true, view.Current is not null,
            view.Current is not null, trusted, view.Pending.Count, view.ReplacementExists));
        if (plan.Writable)
        {
            try { session.ValidateReplacementCapability(Root + "owner.json"); }
            catch (ToolboxFileSystemException e) when (e.Code == "unsupported")
            { plan = plan with { State = "unsupported", Code = "unsupported", Writable = false }; }
        }
        view.Result = new(plan.State, plan.Code, plan.TransportVerified, plan.Writable, storeId, view.Current,
            Workspace: view.Hydrated?.Value, Diagnostics: view.Hydrated?.Diagnostics ?? []);
        return view;
    }

    private ToolboxDiskPlan Operation(ToolboxFileSession session, View view, ToolboxDiskJournal journal, ToolboxPolicyPin[] pins)
    {
        var revision = journal.Revision; string sealedPath = RevisionPath(revision.RevisionId), stagingPath = Stage(revision.OperationId);
        bool sealedExists = session.Exists(sealedPath), stagingExists = session.Exists(stagingPath);
        bool sealedComplete = sealedExists && Complete(session, sealedPath, revision, pins, view.Budget);
        bool stageComplete = stagingExists && Complete(session, stagingPath, revision, pins, view.Budget);
        bool receiptExists = view.Receipts.TryGetValue(revision.OperationId, out var receipt);
        return ToolboxPrototypeRecovery.Plan(new(view.Current, journal, view.Chain.Contains(revision.RevisionId),
            stagingExists, stageComplete, sealedExists, sealedComplete, receiptExists,
            receiptExists && ToolboxPrototypeDiskContract.Same(receipt!, ToolboxPrototypeDiskContract.Receipt(revision)),
            view.Hydrated is not null && Trusted(view.Hydrated)));
    }

    private void CheckInventories(ToolboxFileSession session, View view)
    {
        // Keep one bounded counter per operation/revision, never materialize the payload tree.
        var counts = new Dictionary<string, InventoryCount>(StringComparer.Ordinal);
        foreach (var entry in session.Enumerate())
        {
            view.Budget.Entry();
            if (!entry.RelativePath.StartsWith(Root, StringComparison.Ordinal)) continue;
            var parts = entry.RelativePath[Root.Length..].Split('/');
            if (parts.Length < 2 || parts[0] is not ("revisions" or "staging")) continue;
            bool sealedLocation = parts[0] == "revisions";
            ToolboxDiskRevision revision = sealedLocation ? view.Revisions[parts[1]] : view.Journals[parts[1]].Revision;
            long total = revision.Bundle.ByteLength;
            if (revision.Assets is { } assets)
            {
                Require(assets.Length <= limits.Assets, "limit_exceeded");
                foreach (var asset in assets) { Require(asset.ByteLength <= limits.AssetBytes, "limit_exceeded"); total = checked(total + asset.ByteLength); }
            }
            Require(revision.Bundle.ByteLength <= new ToolboxStorageLimits().BundleBytes && total <= limits.RevisionBytes, "limit_exceeded");
            string key = parts[0] + "/" + parts[1];
            if (!counts.TryGetValue(key, out var count)) counts.Add(key, count = new());
            if (parts.Length == 2) continue;
            if (entry.IsDirectory)
            { Require(revision.Assets is not null, "unexpected_layout"); count.AssetsDirectory = true; continue; }
            if (parts.Length == 3 && parts[2] == "revision.json") { count.Metadata = true; continue; }
            if (parts.Length == 3 && parts[2] == "bundle.json")
            { count.Bundle = entry.Length == revision.Bundle.ByteLength; continue; }
            var hash = parts[3][..^4];
            var expected = revision.Assets?.FirstOrDefault(a => a.Sha256 == hash);
            Require(expected is not null, "unexpected_layout");
            if (entry.Length != expected!.ByteLength) count.LengthMismatch = true;
            count.Assets++;
        }
        foreach (var pair in counts)
        {
            if (!pair.Key.StartsWith("revisions/", StringComparison.Ordinal)) continue;
            var revision = view.Revisions[pair.Key[10..]]; var count = pair.Value;
            Require(count.Metadata && count.Bundle && !count.LengthMismatch
                && count.Assets == (revision.Assets?.Length ?? 0)
                && (revision.Assets is null || count.AssetsDirectory), "partial_revision");
        }
    }

    private bool Complete(ToolboxFileSession session, string location, ToolboxDiskRevision revision, ToolboxPolicyPin[] pins, Budget budget)
    {
        if (!session.Exists(location + "/revision.json") || !session.Exists(location + "/bundle.json")) return false;
        Require(ToolboxPrototypeDiskContract.Same(Read<ToolboxDiskRevision>(session, location + "/revision.json", budget), revision), "operation_conflict");
        if (revision.Bundle.ByteLength > new ToolboxStorageLimits().BundleBytes) throw new ToolboxDiskException("limit_exceeded");
        var bytes = session.Read(location + "/bundle.json", new ToolboxStorageLimits().BundleBytes);
        if (bytes.LongLength != revision.Bundle.ByteLength || ToolboxPrototypeDiskContract.Hash(bytes) != revision.Bundle.Sha256) return false;
        var hydrated = Hydrate(bytes, revision.Case, pins); Require(hydrated.Success, "invalid_bundle");
        long total = bytes.LongLength;
        if (revision.Assets is { } assets)
        {
            if (!session.Exists(location + "/assets")) return false;
            Require(assets.Length <= limits.Assets, "limit_exceeded");
            foreach (var asset in assets)
            {
                total = checked(total + asset.ByteLength); Require(total <= limits.RevisionBytes && asset.ByteLength <= limits.AssetBytes, "limit_exceeded");
                var path = location + "/assets/" + asset.Sha256 + ".bin";
                if (!session.Exists(path)) return false;
                try { session.Verify(path, asset.ByteLength, asset.Sha256); }
                catch (ToolboxFileSystemException e) when (e.Code == "corrupt") { return false; }
            }
        }
        Require(total <= limits.RevisionBytes, "limit_exceeded");
        return true;
    }

    private void ValidateSiblings(ToolboxFileSession session, View view)
    {
        foreach (string sibling in view.Siblings)
        {
            var name = sibling[(sibling.LastIndexOf('/') + 1)..]; string operation;
            if (name.StartsWith("current.", StringComparison.Ordinal))
            {
                Require(name.Length == 50, "unexpected_layout");
                operation = name[8..^10]; ToolboxPrototypeDiskContract.Id(operation);
                Require(view.Journals.TryGetValue(operation, out var journal), "ambiguous_artifacts");
                var pointer = Read<ToolboxDiskPointer>(session, sibling, view.Budget);
                Require(ToolboxPrototypeDiskContract.Same(pointer, ToolboxPrototypeDiskContract.Pointer(journal!.Revision)), "ambiguous_artifacts");
            }
            else
            {
                var parts = name.Split('.'); Require(parts.Length == 4 && parts[2] == "next" && parts[3] == "json", "unexpected_layout");
                operation = parts[0]; ToolboxPrototypeDiskContract.Id(operation);
                Require(view.Journals.TryGetValue(operation, out var original), "ambiguous_artifacts");
                var candidate = Read<ToolboxDiskJournal>(session, sibling, view.Budget);
                Require(candidate.State == parts[1] && ToolboxPrototypeDiskContract.Same(original!.Revision, candidate.Revision)
                    && candidate.PreviousPointer == original.PreviousPointer
                    && ToolboxPrototypeRecovery.TransitionAllowed(original.State, candidate.State), "ambiguous_artifacts");
                if (candidate.State == "abandoned") view.PendingAbandonments.Add(operation, candidate.Abandonment!);
            }
            Require(view.ReplacementOperations.Add(operation) || view.Siblings.Count(s => s.Contains(operation, StringComparison.Ordinal)) <= 2, "ambiguous_artifacts");
        }
    }

    private void Publish(ToolboxFileSession session, ToolboxDiskRevision revision, CancellationToken token)
    { Atomic(session, Root + "current.json", Root + "current." + revision.OperationId + ".next.json", ToolboxPrototypeDiskContract.Pointer(revision), token); }
    private ToolboxDiskJournal Advance(ToolboxFileSession session, ToolboxDiskJournal journal, string state, CancellationToken token)
    {
        Require(ToolboxPrototypeRecovery.TransitionAllowed(journal.State, state), "invalid_transition");
        var next = journal with { State = state };
        Atomic(session, JournalPath(journal.Revision.OperationId), JournalSibling(journal.Revision.OperationId, state), next, token);
        return next;
    }
    private void Tail(ToolboxFileSession session, ToolboxDiskJournal journal)
    {
        var receipt = ToolboxPrototypeDiskContract.Receipt(journal.Revision); string path = Root + "receipts/" + receipt.OperationId + ".json";
        if (session.Exists(path)) Require(ToolboxPrototypeDiskContract.Same(Read<ToolboxDiskReceipt>(session, path, new Budget(limits)), receipt), "receipt_conflict");
        else Write(session, path, receipt, CancellationToken.None);
        if (journal.State != "committed") _ = Advance(session, journal, "committed", CancellationToken.None);
    }
    private void Atomic<T>(ToolboxFileSession session, string target, string sibling, T value, CancellationToken token) where T : class
    {
        if (session.Exists(sibling)) Require(ToolboxPrototypeDiskContract.Same(Read<T>(session, sibling, new Budget(limits)), value), "ambiguous_artifacts");
        else Write(session, sibling, value, token);
        Mutate("replace:" + target[Root.Length..], () => session.Replace(sibling, target), token);
        Hit("before_verify:" + target[Root.Length..]);
        Require(ToolboxPrototypeDiskContract.Same(Read<T>(session, target, new Budget(limits)), value), "replacement_unverified");
        Hit("after_verify:" + target[Root.Length..]);
    }
    private void Write<T>(ToolboxFileSession session, string path, T value, CancellationToken token) where T : class
    {
        var bytes = ToolboxPrototypeDiskContract.Encode(value); Require(bytes.Length <= limits.MetadataBytes, "limit_exceeded");
        _ = ToolboxPrototypeDiskContract.Decode<T>(bytes, limits);
        requestBudget.Value?.Add(bytes.Length);
        WriteBytes(session, path, bytes, token);
    }
    private void WriteBytes(ToolboxFileSession session, string path, ReadOnlySpan<byte> bytes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Hit("before_write:" + path[Root.Length..]);
        token.ThrowIfCancellationRequested();
        if (requestBudget.Value is { } budget) budget.MutationStarted = true;
        session.WriteNew(path, bytes); Hit("after_write:" + path[Root.Length..]);
    }
    private void Mutate(string name, Action operation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Hit("before_" + name);
        token.ThrowIfCancellationRequested();
        if (requestBudget.Value is { } budget) budget.MutationStarted = true;
        operation(); Hit("after_" + name);
    }
    private void Hit(string name) => boundary?.Invoke(name);
    private T Read<T>(ToolboxFileSession session, string path, Budget budget) where T : class
    {
        budget = requestBudget.Value ?? budget;
        var bytes = session.Read(path, Math.Min(limits.MetadataBytes, budget.Remaining)); budget.Add(bytes.Length);
        return ToolboxPrototypeDiskContract.Decode<T>(bytes, limits);
    }
    private ToolboxStorageResult<ToolboxHydratedWorkspace> Hydrate(ReadOnlyMemory<byte> bytes, ToolboxCaseIdentity identity, ToolboxPolicyPin[] pins)
    {
        var inspection = codec.Decode(bytes, identity);
        if (!inspection.Success || pins.Length == 0) return inspection;
        // The external catalog spans current/proposed policies; resolve only this validated bundle's declarations.
        var catalog = pins.ToDictionary(p => (p.PolicyId, p.PolicyVersion));
        using var document = JsonDocument.Parse(bytes, new() { MaxDepth = new ToolboxStorageLimits().JsonDepth });
        var selected = new List<ToolboxPolicyPin>();
        foreach (var asset in document.RootElement.GetProperty("policy_assets").EnumerateArray())
        {
            var pin = asset.GetProperty("pin");
            if (catalog.TryGetValue((pin.GetProperty("policy_id").GetString()!, pin.GetProperty("policy_version").GetString()!), out var trusted))
                selected.Add(trusted);
        }
        var resolved = codec.Decode(bytes, identity, selected);
        // External trust failure must not relabel structurally valid, hash-verified transport as byte corruption.
        return resolved.Success ? resolved : inspection with { Diagnostics = inspection.Diagnostics.Add(
            new("policy_untrusted", "$.policy_assets", "External policy resolution did not validate; inspection only.")) };
    }
    private static bool Trusted(ToolboxStorageResult<ToolboxHydratedWorkspace> result) => result.Success
        && result.Value!.TrustedPolicies.Length > 0 && !result.Diagnostics.Any(d => d.Code is "policy_untrusted" or "policy_unavailable");
    private ToolboxPolicyPin[] Pins(IEnumerable<ToolboxPolicyPin>? pins)
    {
        var result = (pins ?? []).Take(257).ToArray(); Require(result.Length <= 256, "limit_exceeded");
        Require(result.All(p => p is not null) && result.Select(p => (p.PolicyId, p.PolicyVersion)).Distinct().Count() == result.Length,
            "policy_trust_conflict");
        return result;
    }
    private void ValidatePrepared(ToolboxPreparedSave save)
    {
        ToolboxPrototypeDiskContract.Validate(save.Intent);
        Require(!save.Bundle.IsDefault && save.Bundle.Length <= new ToolboxStorageLimits().BundleBytes
            && save.Intent.Bundle.ByteLength == save.Bundle.Length && save.Intent.Bundle.Sha256 == ToolboxPrototypeDiskContract.Hash(save.Bundle.AsSpan()), "request_bytes_conflict");
        Require((save.Assets is null) == (save.Intent.Assets is null), "request_bytes_conflict");
        long total = save.Bundle.Length;
        if (save.Assets is { } assets)
        {
            Require(!assets.IsDefault && assets.Length <= limits.Assets && assets.Length == save.Intent.Assets!.Value.Length, "request_bytes_conflict");
            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i]; var inventory = save.Intent.Assets!.Value[i];
                Require(!asset.IsDefault && asset.Length <= limits.AssetBytes && asset.Length == inventory.ByteLength
                    && ToolboxPrototypeDiskContract.Hash(asset.AsSpan()) == inventory.Sha256, "request_bytes_conflict");
                total = checked(total + asset.Length);
            }
        }
        Require(total <= limits.RevisionBytes, "limit_exceeded"); _ = ToolboxPrototypeDiskContract.Revision(save.Intent);
    }
    private void CheckNewLimits(View view)
    { Require(view.Journals.Count < limits.Records && view.Revisions.Count < limits.Records && view.Receipts.Count < limits.Records && view.Stages.Count < limits.Records, "limit_exceeded"); }
    private void ReserveSave(View view, ToolboxDiskJournal journal)
    {
        var revision = journal.Revision;
        // Admission reserves both the resulting observation and transient sibling/staging evidence.
        long newEntries = 6L + (view.Current is null ? 1 : 0)
            + (revision.Assets is { } assets ? 1L + assets.Length : 0);
        Require(limits.DirectoryDepth >= (revision.Assets is null ? 3 : 4), "limit_exceeded");
        Require(2L * (view.PhysicalEntries + newEntries) + view.Budget.Entries <= limits.Entries, "limit_exceeded");
        long largestJournal = ToolboxPrototypeDiskContract.Encode(journal with { State = "committed" }).Length;
        long newMetadata = largestJournal + ToolboxPrototypeDiskContract.Encode(revision).Length
            + ToolboxPrototypeDiskContract.Encode(ToolboxPrototypeDiskContract.Pointer(revision)).Length
            + ToolboxPrototypeDiskContract.Encode(ToolboxPrototypeDiskContract.Receipt(revision)).Length;
        Require(3L * (view.PhysicalMetadataBytes + newMetadata) + view.Budget.Used + 8L * largestJournal <= limits.TraversalBytes,
            "limit_exceeded");
        Require(largestJournal <= limits.MetadataBytes && ToolboxPrototypeDiskContract.Encode(revision).Length <= limits.MetadataBytes,
            "limit_exceeded");
        _ = ToolboxPrototypeDiskContract.Decode<ToolboxDiskJournal>(ToolboxPrototypeDiskContract.Encode(journal), limits);
        _ = ToolboxPrototypeDiskContract.Decode<ToolboxDiskRevision>(ToolboxPrototypeDiskContract.Encode(revision), limits);
        _ = ToolboxPrototypeDiskContract.Decode<ToolboxDiskPointer>(ToolboxPrototypeDiskContract.Encode(ToolboxPrototypeDiskContract.Pointer(revision)), limits);
        _ = ToolboxPrototypeDiskContract.Decode<ToolboxDiskReceipt>(ToolboxPrototypeDiskContract.Encode(ToolboxPrototypeDiskContract.Receipt(revision)), limits);
    }
    private void ReserveResolution(View view, ToolboxDiskJournal terminal, bool includePublication)
    {
        var revision = terminal.Revision;
        long additionalEntries = 1;
        long additionalMetadata = ToolboxPrototypeDiskContract.Encode(terminal).Length;
        if (terminal.State == "committed" && !view.Receipts.ContainsKey(revision.OperationId))
        { additionalEntries++; additionalMetadata += ToolboxPrototypeDiskContract.Encode(ToolboxPrototypeDiskContract.Receipt(revision)).Length; }
        if (includePublication && view.Current is null)
        { additionalEntries++; additionalMetadata += ToolboxPrototypeDiskContract.Encode(ToolboxPrototypeDiskContract.Pointer(revision)).Length; }
        Require(2L * (view.PhysicalEntries + additionalEntries) + view.Budget.Entries <= limits.Entries, "limit_exceeded");
        Require(3L * (view.PhysicalMetadataBytes + additionalMetadata) + view.Budget.Used + 8L * additionalMetadata <= limits.TraversalBytes,
            "limit_exceeded");
        _ = ToolboxPrototypeDiskContract.Decode<ToolboxDiskJournal>(ToolboxPrototypeDiskContract.Encode(terminal), limits);
        if (terminal.State == "committed")
        {
            _ = ToolboxPrototypeDiskContract.Decode<ToolboxDiskReceipt>(ToolboxPrototypeDiskContract.Encode(ToolboxPrototypeDiskContract.Receipt(revision)), limits);
            _ = ToolboxPrototypeDiskContract.Decode<ToolboxDiskPointer>(ToolboxPrototypeDiskContract.Encode(ToolboxPrototypeDiskContract.Pointer(revision)), limits);
        }
    }
    private static void Binding(ToolboxDiskRevision revision, ToolboxDiskOwner owner)
    { Require(revision.Case == owner.Case && revision.StoreId == owner.StoreId, "owner_conflict"); }
    private ToolboxDiskResult SafeInspect(ToolboxFileSession session, ToolboxCaseIdentity identity, string storeId, ToolboxPolicyPin[] pins)
    { try { return Inspect(session, identity, storeId, pins).Result; } catch (Exception e) when (Known(e)) { return Failure(e); } }
    private static bool IsId(string text) => text.Length == 32 && text.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Stage(string operation) => Root + "staging/" + operation;
    private static string RevisionPath(string revision) => Root + "revisions/" + revision;
    private static string JournalPath(string operation) => Root + "journal/" + operation + ".json";
    private static string JournalSibling(string operation, string state) => Root + "journal/" + operation + "." + state + ".next.json";
    private static void Require(bool condition, string code) => ToolboxPrototypeDiskContract.Require(condition, code);
    private static bool Known(Exception e) => e is ToolboxDiskException or ToolboxFileSystemException or IOException or UnauthorizedAccessException or OverflowException;
    private static ToolboxDiskResult Failure(Exception exception, bool initialization = false)
    {
        string code = exception is ToolboxDiskException disk ? disk.Code : exception is ToolboxFileSystemException fs ? fs.Code : "io_error";
        if (initialization && code == "exists") return new("quarantined", "initialization_conflict");
        return new(code == "busy" ? "busy" : code == "unsupported" ? "unsupported" : code is "unsafe_path" or "ambiguous_artifacts" or "competing_intents" or "publication_unproven" or "parent_conflict" or "unexpected_layout" or "partial_revision" ? "quarantined" : "corrupt", code);
    }
    private sealed class Budget(ToolboxDiskLimits limits)
    {
        private long used;
        private int entries;
        public bool MutationStarted;
        public int Remaining => (int)Math.Min(int.MaxValue, limits.TraversalBytes - used);
        public int Entries => entries;
        public long Used => used;
        public void Add(int length) { used = checked(used + length); Require(used <= limits.TraversalBytes, "limit_exceeded"); }
        public void Entry() { Require(++entries <= limits.Entries, "limit_exceeded"); }
    }
    private sealed class View(ToolboxDiskOwner owner, Budget budget)
    {
        public ToolboxDiskOwner Owner { get; } = owner;
        public Budget Budget { get; } = budget;
        public ToolboxDiskPointer? Current;
        public ToolboxStorageResult<ToolboxHydratedWorkspace>? Hydrated;
        public ToolboxDiskResult Result = new("corrupt", "unobserved");
        public Dictionary<string, ToolboxDiskJournal> Journals { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, ToolboxDiskReceipt> Receipts { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, ToolboxDiskRevision> Revisions { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, ToolboxDiskRevision> StagedRevisions { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Chain { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Stages { get; } = new(StringComparer.Ordinal);
        public HashSet<string> RevisionDirectories { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Pending { get; } = new(StringComparer.Ordinal);
        public List<string> Siblings { get; } = [];
        public HashSet<string> ReplacementOperations { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, ToolboxDiskAbandonment> PendingAbandonments { get; } = new(StringComparer.Ordinal);
        public int PhysicalEntries;
        public long PhysicalMetadataBytes;
        public bool ReplacementExists => Siblings.Count != 0;
    }
    private sealed class InventoryCount
    { public bool Metadata; public bool Bundle; public bool AssetsDirectory; public bool LengthMismatch; public int Assets; }
}
