using System.Collections.Immutable;

namespace ParcelWorkflowAddIn.Workflow.Toolbox.Persistence;

public sealed record ToolboxDiskObservation(ToolboxDiskPointer? Current, ToolboxDiskJournal Journal,
    bool OnCurrentAncestry, bool StagingExists, bool StagingComplete, bool SealedExists, bool SealedComplete,
    bool ReceiptExists, bool ReceiptMatches, bool PolicyTrusted, bool AmbiguousArtifacts = false);
public sealed record ToolboxDiskPlan(string State, string Code, bool TransportVerified, bool Writable,
    ImmutableArray<string> Actions);
public sealed record ToolboxDiskStoreObservation(bool OwnerValid, bool LockPresent, bool LayoutValid,
    bool CurrentPresent, bool CurrentVerified, bool PolicyTrusted, int PendingIntents, bool ReplacementArtifacts);

public static class ToolboxPrototypeRecovery
{
    public static ToolboxDiskPlan PlanStore(ToolboxDiskStoreObservation o)
    {
        if (!o.OwnerValid || !o.LockPresent || !o.LayoutValid) return Result("corrupt", "initialization_incomplete");
        if (!o.CurrentPresent && o.CurrentVerified) return Result("quarantined", "inconsistent_observation");
        if (o.PendingIntents < 0 || o.PendingIntents > 1) return Result("quarantined", "competing_intents");
        if (o.CurrentPresent && !o.CurrentVerified) return Result("corrupt", "published_bytes_missing");
        if (o.PendingIntents > 0 || o.ReplacementArtifacts) return Result("pending", "recovery_required", o.CurrentVerified);
        if (!o.CurrentPresent) return Result("empty", "empty");
        return Result("verified", o.PolicyTrusted ? "verified" : "policy_inspection_required", true, o.PolicyTrusted);
    }

    public static ImmutableHashSet<string> VerifyAncestry(ToolboxDiskPointer current,
        IReadOnlyDictionary<string, ToolboxDiskRevision> revisions, int maximum = 10_000)
    {
        ToolboxPrototypeDiskContract.Validate(current);
        ToolboxPrototypeDiskContract.Require(maximum is > 0 and <= 10_000, "invalid_limits");
        var seen = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        ToolboxDiskParent? cursor = ToolboxPrototypeDiskContract.Parent(current);
        while (cursor is not null)
        {
            ToolboxPrototypeDiskContract.Require(seen.Count < maximum && seen.Add(cursor.RevisionId), "ancestry_missing");
            ToolboxPrototypeDiskContract.Require(revisions.TryGetValue(cursor.RevisionId, out var revision) && revision is not null, "ancestry_missing");
            ToolboxPrototypeDiskContract.Validate(revision!);
            ToolboxPrototypeDiskContract.Require(revision!.Case == current.Case && revision.StoreId == current.StoreId
                && revision.RevisionId == cursor.RevisionId && revision.RevisionDigest == cursor.RevisionDigest
                && revision.Sequence == cursor.Sequence, "ancestry_conflict");
            cursor = revision.Parent;
        }
        return seen.ToImmutable();
    }

    public static ToolboxDiskPlan Plan(ToolboxDiskObservation observation)
    {
        var o = observation; ToolboxPrototypeDiskContract.Validate(o.Journal);
        if (o.Current is not null) ToolboxPrototypeDiskContract.Validate(o.Current);
        if ((o.StagingComplete && !o.StagingExists) || (o.SealedComplete && !o.SealedExists)
            || (o.ReceiptMatches && !o.ReceiptExists)) return Result("quarantined", "inconsistent_observation");
        if (o.AmbiguousArtifacts || (o.StagingExists && o.SealedExists)) return Result("quarantined", "ambiguous_artifacts");
        if (o.ReceiptExists && !o.ReceiptMatches) return Result("corrupt", "receipt_conflict");
        if (o.Journal.State == "abandoned")
            return o.OnCurrentAncestry || o.ReceiptExists ? Result("quarantined", "abandoned_publication") : Result("verified", "operation_abandoned");
        if (o.OnCurrentAncestry && (o.Current is null || o.Current.Case != o.Journal.Revision.Case
            || o.Current.StoreId != o.Journal.Revision.StoreId || o.Current.Sequence < o.Journal.Revision.Sequence
            || o.Current.Sequence == o.Journal.Revision.Sequence
                && !ToolboxPrototypeDiskContract.Same(o.Current, ToolboxPrototypeDiskContract.Pointer(o.Journal.Revision))))
            return Result("quarantined", "inconsistent_observation");
        if (o.OnCurrentAncestry)
        {
            if (!o.SealedComplete) return Result("corrupt", "published_bytes_missing");
            if (o.Journal.State == "committed" && o.ReceiptMatches)
                return Result("verified", o.PolicyTrusted ? "committed" : "policy_inspection_required", true, o.PolicyTrusted);
            return Result("pending", "recovery_required", true, false, "complete_tail");
        }
        if (o.Journal.State is "published" or "committed" || o.ReceiptExists)
            return Result("quarantined", "publication_unproven");
        var parentMatches = o.Journal.PreviousPointer is null ? o.Current is null
            : o.Current is not null && ToolboxPrototypeDiskContract.Same(o.Journal.PreviousPointer, o.Current);
        if (!parentMatches) return Result("quarantined", "parent_conflict");
        if (o.SealedExists && !o.SealedComplete) return Result("quarantined", "partial_revision");
        if (o.SealedComplete) return Result("pending", "resume_required", true, false, "publish", "abandon");
        if (o.StagingComplete) return Result("pending", "resume_required", false, false, "seal", "publish", "abandon");
        return Result("pending", "incomplete_staging", false, false, "abandon");
    }

    public static bool TransitionAllowed(string before, string after) => (before, after) switch
    {
        ("prepared", "sealed") or ("sealed", "published") or ("published", "committed")
            or ("prepared", "abandoned") or ("sealed", "abandoned") => true,
        _ => false
    };

    private static ToolboxDiskPlan Result(string state, string code, bool verified = false, bool writable = false,
        params string[] actions) => new(state, code, verified, writable, actions.ToImmutableArray());
}
