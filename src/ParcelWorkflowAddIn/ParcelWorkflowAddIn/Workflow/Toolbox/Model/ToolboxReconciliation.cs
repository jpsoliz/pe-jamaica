using System.Collections.Immutable;
using System.Text.Json;
using ParcelWorkflowAddIn.Workflow.Review.Canonical;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

namespace ParcelWorkflowAddIn.Workflow.Toolbox.Model;

public enum ToolboxRerunStatus { Succeeded, Partial, Failed, Canceled }
public enum ToolboxCandidateState { Matched, New, Missing, Ambiguous, Conflicting }
public enum ToolboxValueChoice { Unresolved, Retain, Replace, New, Retire }
public sealed record ToolboxRerunBatch(ToolboxCaseIdentity Case, string BatchId, string RunId, string ToolVersion,
    string ProviderVersion, ImmutableDictionary<string, string> SourceHashes, string BaseRevision,
    string BaseFingerprint, string CandidateDigest, string ReconciliationPolicy, ToolboxPolicyPin ReviewPolicy,
    ToolboxRerunStatus Status, CanonicalReviewContract Candidate);
public sealed record ToolboxCandidateDisposition(string Kind, string? OldId, string? IncomingKey,
    string? OutputKey, ToolboxValueChoice Choice, string Actor, string Reason, string EvidenceId, bool ExplicitIdentity);
public sealed record ToolboxCandidateMatch(string Kind, string? OldId, string? IncomingKey, ToolboxCandidateState State, string Reason);
public sealed record ToolboxReconciliationProposal(string OperationId, string ResultRevision, ToolboxRerunBatch Batch,
    CanonicalReviewContract ResolvedSource, ImmutableArray<ToolboxCandidateDisposition> Dispositions,
    ImmutableArray<ToolboxParcelLineage> Lineage, ImmutableArray<ToolboxHistoryApplicability> Applicability,
    ImmutableArray<ToolboxPhysicalBoundary> Boundaries, ToolboxDependencyInventory Dependencies, string Digest,
    ToolboxAdjacencyReview? Adjacency = null, ImmutableArray<ToolboxBoundaryRemoval> BoundaryRemovals = default);
public sealed record ToolboxAcceptedOperation(string OperationId, string Digest, string ResultRevision);
public sealed record ToolboxAcceptedDispositions(string OperationId, string Digest, string ParentRevision,
    string ResultRevision, DateTimeOffset AcceptedAt, ImmutableArray<ToolboxCandidateDisposition> Dispositions);
public sealed record ToolboxReconciliationSnapshot(string Revision, ToolboxParcelModel Model,
    ToolboxExaminationRegister Register, ImmutableArray<ToolboxRerunBatch> Reruns,
    ImmutableArray<ToolboxAppliedLineage> Lineage, ImmutableArray<ToolboxAcceptedOperation> Operations)
{
    public ImmutableArray<ToolboxAcceptedDispositions> AcceptedDispositions { get; init; } = [];
    public string Fingerprint => ToolboxSemanticHash.Of(new { version = "toolbox_reconciliation_base_v2", Revision,
        Model.Case, source = Model.Source.GlobalData, Model.IdentityMap, Model.InclusionFingerprint,
        relations = Model.Relations is { } r ? new { r.Policy, r.SourceHashes, r.Boundaries, r.Dependencies,
            r.Lineage, r.Applicability, r.ReadinessBarriers, r.HumanInputBindings, r.FindingInputBindings, r.Adjacency } : null,
        archives = Model.Relations?.Archives.Select(a => new { a.Case, a.IdentityMap, source = a.Source.GlobalData,
            a.InclusionFingerprint, hashes = a.Relations?.SourceHashes, boundaries = a.Relations?.Boundaries }),
        reruns = Reruns.Select(r => new { r.BatchId, r.RunId, r.ToolVersion, r.ProviderVersion, r.SourceHashes,
            r.BaseRevision, r.BaseFingerprint, digest = ToolboxReconciliation.CandidateDigest(r.Candidate), r.Status }),
        Lineage, Register.Findings, Register.HumanReviews, Register.Decisions, Operations, AcceptedDispositions });
}

public static class ToolboxReconciliation
{
    public const string PolicyVersion = "toolbox_d07_reconciliation_v1";
    public static string CandidateDigest(CanonicalReviewContract candidate) => ToolboxSemanticHash.Of(candidate.GlobalData);

    public static ToolboxReconciliationSnapshot Start(ToolboxParcelModel model, ToolboxExaminationRegister register,
        string revision, ToolboxPolicyPin policy, ImmutableDictionary<string, string> sourceHashes,
        ImmutableArray<ToolboxPhysicalBoundary> boundaries, ToolboxDependencyInventory dependencies, ToolboxAdjacencyReview? adjacency = null)
    {
        ToolboxParcelModel.Require(model.Relations is null && !string.IsNullOrWhiteSpace(revision) && (register.Case is null || register.Case == model.Case), "Invalid/reset initial reconciliation revision/register.");
        ValidateSources(model.Source, sourceHashes);
        ValidateAdjacency(model.Source, model.Case, adjacency);
        var relations = new ToolboxModelRelations(policy, sourceHashes, boundaries, dependencies, [], [], [], []) { Adjacency = adjacency };
        var projected = model.WithRelations(relations);
        projected = projected.WithRelations(relations with { HumanInputBindings = register.HumanReviews.GroupBy(ToolboxLineageValidator.CorrectionId)
            .ToImmutableDictionary(g => g.Key, g => ToolboxReadinessEvaluator.HumanSourceFingerprint(projected, g.Last().EvidenceId)),
            FindingInputBindings = register.Findings.ToImmutableDictionary(f => ToolboxReadinessEvaluator.FindingEventId(f),
                f => ToolboxReadinessEvaluator.FindingInputFingerprint(projected, f)) });
        _ = ToolboxBoundaryValidator.Validate(projected, boundaries, PolicyVersion);
        dependencies.Validate(projected);
        return new(revision, projected, register, [], [], []);
    }

    public static ImmutableArray<ToolboxCandidateMatch> Inspect(ToolboxReconciliationSnapshot current, ToolboxRerunBatch batch)
    {
        ValidateBatch(current, batch);
        var old = Inventory(current.Model.Source);
        var incoming = Inventory(batch.Candidate);
        var result = ImmutableArray.CreateBuilder<ToolboxCandidateMatch>();
        var sameSource = ToolboxSemanticHash.Of(current.Model.Relations!.SourceHashes) == ToolboxSemanticHash.Of(batch.SourceHashes);
        foreach (var item in old)
        {
            var found = incoming.FirstOrDefault(x => x.Kind == item.Kind && ToolboxParcelModel.Same(x.Key, item.Key));
            result.Add(new(item.Kind, current.Model.IdentityMap.Ids[item.Kind + ":" + item.Key], found?.Key,
                found is null ? ToolboxCandidateState.Missing : !sameSource ? ToolboxCandidateState.Ambiguous
                    : ValueHash(item.Data) != ValueHash(found.Data) ? ToolboxCandidateState.Conflicting : ToolboxCandidateState.Matched,
                found is null ? "Prior entity retained pending disposition." : sameSource ? "Carried source key; values still require disposition." : "Changed source requires explicit provenance mapping."));
        }
        foreach (var item in incoming.Where(x => !old.Any(y => y.Kind == x.Kind && ToolboxParcelModel.Same(x.Key, y.Key))))
            result.Add(new(item.Kind, null, item.Key, ToolboxCandidateState.New, "New suggestion; no label/coordinate matching."));
        return result.ToImmutable();
    }

    public static ToolboxReconciliationProposal Seal(ToolboxReconciliationProposal proposal)
    {
        ToolboxParcelModel.Require(!proposal.Dispositions.IsDefault && !proposal.Lineage.IsDefault && !proposal.Applicability.IsDefault
            && !proposal.Boundaries.IsDefault && !proposal.Dependencies.Nodes.IsDefault && !proposal.Dependencies.Edges.IsDefault, "Absent proposal inventories.");
        var normalized = proposal with { Boundaries = proposal.Boundaries.Select(b => b with { PointIds = b.PointIds.IsDefault ? [] : b.PointIds }).ToImmutableArray() };
        return normalized with { Digest = ProposalDigest(normalized) };
    }
    public static string ProposalDigest(ToolboxReconciliationProposal p) => ToolboxSemanticHash.Of(new
    {
        version = "toolbox_reconciliation_proposal_v1", p.OperationId, p.ResultRevision,
        batch = new { p.Batch.Case, p.Batch.BatchId, p.Batch.RunId, p.Batch.ToolVersion, p.Batch.ProviderVersion,
            p.Batch.SourceHashes, p.Batch.BaseRevision, p.Batch.BaseFingerprint, p.Batch.CandidateDigest,
            p.Batch.ReconciliationPolicy, p.Batch.ReviewPolicy, p.Batch.Status },
        candidate = p.Batch.Candidate.GlobalData, resolved = p.ResolvedSource.GlobalData,
        p.Dispositions, p.Lineage, p.Applicability, p.Boundaries, p.Dependencies, p.Adjacency,
        removals = p.BoundaryRemovals.IsDefault ? ImmutableArray<ToolboxBoundaryRemoval>.Empty : p.BoundaryRemovals
    });

    public static ToolboxReconciliationSnapshot Accept(ToolboxReconciliationSnapshot current, ToolboxReconciliationProposal proposal,
        ToolboxCaseContext context, ToolboxReviewPolicy policy, ToolboxDispositionAuthority authority,
        Func<string>? allocateId = null)
    {
        ToolboxParcelModel.Require(current.Model.Case == ToolboxCaseIdentity.From(context)
            && proposal.Batch.Case == current.Model.Case && context.GeometryAccess().Allowed
            && authority.Authorized && authority.Actor == context.OperatorId && authority.PolicyVersion == policy.Pin.PolicyVersion
            && policy.Pin == current.Model.Relations?.Policy && proposal.Batch.ReviewPolicy == policy.Pin,
            "Reconciliation requires same-case authorized local Compute; linked sources remain read-only.");
        ToolboxParcelModel.Require(!string.IsNullOrWhiteSpace(proposal.OperationId) && proposal.Digest == ProposalDigest(proposal), "Altered/unsealed reconciliation proposal.");
        var replay = current.Operations.FirstOrDefault(x => ToolboxParcelModel.Same(x.OperationId, proposal.OperationId));
        if (replay is not null)
        {
            ToolboxParcelModel.Require(replay.Digest == proposal.Digest, "Operation ID reused with another payload.");
            return current;
        }
        ValidateBatch(current, proposal.Batch);
        ValidateAdjacency(proposal.ResolvedSource, current.Model.Case, proposal.Adjacency);
        ToolboxParcelModel.Require(proposal.Adjacency is { Complete: true } && proposal.Adjacency.Actor == authority.Actor,
            "Explicit complete adjacency/no-shared-boundary review required.");
        var removals = proposal.BoundaryRemovals.IsDefault ? ImmutableArray<ToolboxBoundaryRemoval>.Empty : proposal.BoundaryRemovals;
        var removedBoundaries = current.Model.Relations!.Boundaries.Where(b => !proposal.Boundaries.Any(n => ToolboxParcelModel.Same(n.Id, b.Id))).ToArray();
        ToolboxParcelModel.Require(removals.Length == removedBoundaries.Length && removals.Select(x => x.BoundaryId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == removals.Length
            && removals.All(r => removedBoundaries.Any(b => ToolboxParcelModel.Same(b.Id, r.BoundaryId)) && r.Actor == authority.Actor
                && !string.IsNullOrWhiteSpace(r.Reason) && proposal.ResolvedSource.HasEvidence(r.EvidenceId)), "Boundary removal needs explicit disposition/evidence.");
        ToolboxParcelModel.Require(!string.IsNullOrWhiteSpace(proposal.ResultRevision) && proposal.ResultRevision != current.Revision
            && !current.Operations.Any(x => x.ResultRevision == proposal.ResultRevision), "Reused result revision.");
        ToolboxLineageValidator.Validate(current, proposal.Batch, proposal.Lineage, proposal.Applicability, proposal.ResultRevision, authority.Actor);
        var old = Inventory(current.Model.Source);
        var incoming = Inventory(proposal.Batch.Candidate);
        var output = Inventory(proposal.ResolvedSource);
        var choices = proposal.Dispositions;
        ToolboxParcelModel.Require(!choices.IsDefaultOrEmpty && choices.Count(x => x.Kind == "global") == 1
            && choices.Where(x => x.Kind == "global").All(x => x.OldId is null && x.IncomingKey is null && x.OutputKey is null)
            && choices.All(x => x.Kind is "parcel" or "row" or "segment" or "global" && Enum.IsDefined(x.Choice) && x.Choice != ToolboxValueChoice.Unresolved
            && x.Actor == authority.Actor && !string.IsNullOrWhiteSpace(x.Reason)
            && (proposal.Batch.Candidate.HasEvidence(x.EvidenceId) || current.Model.Source.HasEvidence(x.EvidenceId))), "Unresolved/unauthorized value disposition.");
        var oldIds = current.Model.IdentityMap.Ids.Values.ToArray();
        ToolboxParcelModel.Require(choices.Where(x => x.OldId is not null).Select(x => x.OldId).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            == choices.Count(x => x.OldId is not null) && oldIds.All(id => choices.Count(x => ToolboxParcelModel.Same(x.OldId ?? "", id)) == 1), "Incomplete/duplicate old inventory dispositions.");
        ToolboxParcelModel.Require(incoming.All(x => choices.Count(c => c.Kind == x.Kind && ToolboxParcelModel.Same(c.IncomingKey ?? "", x.Key)) == 1)
            && output.All(x => choices.Count(c => c.Kind == x.Kind && ToolboxParcelModel.Same(c.OutputKey ?? "", x.Key)) == 1), "Incomplete/duplicate candidate/output dispositions.");
        var map = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);
        var retired = proposal.Lineage.SelectMany(x => x.ParentIds).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var children = proposal.Lineage.SelectMany(x => x.ChildKeys).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sameSource = ToolboxSemanticHash.Of(current.Model.Relations!.SourceHashes) == ToolboxSemanticHash.Of(proposal.Batch.SourceHashes);
        foreach (var c in choices)
        {
            if (c.Kind == "global") continue;
            var previous = c.OldId is null ? null : old.SingleOrDefault(x => x.Kind == c.Kind
                && ToolboxParcelModel.Same(current.Model.IdentityMap.Ids[x.Kind + ":" + x.Key], c.OldId));
            var candidate = c.IncomingKey is null ? null : incoming.SingleOrDefault(x => x.Kind == c.Kind && ToolboxParcelModel.Same(x.Key, c.IncomingKey));
            var resolved = c.OutputKey is null ? null : output.SingleOrDefault(x => x.Kind == c.Kind && ToolboxParcelModel.Same(x.Key, c.OutputKey));
            ToolboxParcelModel.Require((c.OldId is null || previous is not null) && (c.IncomingKey is null || candidate is not null)
                && (c.OutputKey is null || resolved is not null), "Foreign disposition identity/kind.");
            if (c.Choice == ToolboxValueChoice.Retire)
            {
                ToolboxParcelModel.Require(previous is not null && candidate is null && resolved is null
                    && (retired.Contains(c.OldId!) || previous.Kind != "parcel" && retired.Contains(current.Model.IdentityMap.Ids["parcel:" + previous.ParcelKey])), "Retirement requires explicit lineage, never deletion on rerun.");
                continue;
            }
            ToolboxParcelModel.Require(resolved is not null && (c.Choice == ToolboxValueChoice.New ? previous is null && candidate is not null
                : previous is not null && (c.Choice != ToolboxValueChoice.Replace || candidate is not null)), "Invalid retained/replacement/new choice.");
            if (previous is not null && candidate is not null)
                ToolboxParcelModel.Require(c.ExplicitIdentity || sameSource && ToolboxParcelModel.Same(previous.Key, candidate.Key), "Explicit provenance identity mapping required.");
            if (previous is not null && previous.Kind == "parcel")
                ToolboxParcelModel.Require(!retired.Contains(c.OldId!) && !children.Contains(resolved!.Key), "Parent ID cannot become a split/merge result.");
            var selected = c.Choice == ToolboxValueChoice.Retain ? previous! : candidate!;
            ToolboxParcelModel.Require(ValueHash(resolved!.Data) == ValueHash(selected.Data), "Resolved entity differs from explicit retained/replacement choice.");
            if (previous is not null && previous.Kind != "parcel" && !ToolboxParcelModel.Same(previous.ParcelKey, resolved.ParcelKey)
                && !choices.Any(p => p.Kind == "parcel" && ToolboxParcelModel.Same(p.OldId ?? "", current.Model.IdentityMap.Ids["parcel:" + previous.ParcelKey])
                    && ToolboxParcelModel.Same(p.OutputKey ?? "", resolved.ParcelKey)))
                ToolboxParcelModel.Require(c.ExplicitIdentity && proposal.Lineage.Any(l => l.ParentIds.Contains(current.Model.IdentityMap.Ids["parcel:" + previous.ParcelKey], StringComparer.OrdinalIgnoreCase)
                    && l.ChildKeys.Contains(resolved.ParcelKey, StringComparer.OrdinalIgnoreCase)), "Entity reassignment requires complete explicit lineage mapping.");
            if (resolved.Kind != "parcel")
            {
                var selectedSource = c.Choice == ToolboxValueChoice.Retain ? current.Model.Source : proposal.Batch.Candidate;
                var selectedMembers = selected.Kind == "row" ? selectedSource.PointMemberships : selectedSource.SegmentMemberships;
                var outputMembers = selected.Kind == "row" ? proposal.ResolvedSource.PointMemberships : proposal.ResolvedSource.SegmentMemberships;
                ToolboxParcelModel.Require(selectedMembers.Where(m => ToolboxParcelModel.Same(m.EntityKey, selected.Key)).Select(m => (m.Sequence, m.Direction, m.Role))
                    .SequenceEqual(outputMembers.Where(m => ToolboxParcelModel.Same(m.EntityKey, resolved.Key)).Select(m => (m.Sequence, m.Direction, m.Role))), "Membership values differ from the reviewed choice.");
            }
            var id = previous is null ? (allocateId ?? (() => Guid.NewGuid().ToString("N")))() : c.OldId!;
            ToolboxParcelModel.Require(previous is not null || !oldIds.Contains(id, StringComparer.OrdinalIgnoreCase), "Fresh identity allocation reused an old ID.");
            map.Add(c.Kind + ":" + resolved.Key, id);
        }
        ToolboxParcelModel.Require(map.Count == output.Length && map.Values.All(x => !string.IsNullOrWhiteSpace(x) && x.Trim() == x)
            && map.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == map.Count
            && map.Where(x => !oldIds.Contains(x.Value, StringComparer.OrdinalIgnoreCase)).All(x => !current.Model.Relations.Archives.Any(a => a.IdentityMap.Ids.Values.Contains(x.Value, StringComparer.OrdinalIgnoreCase))), "Incomplete/reused result identities.");
        // Global tabular data stays with the examiner-owned resolved canonical snapshot, with an explicit audit choice.
        ToolboxParcelModel.Require(choices.Any(x => x.Kind == "global" && x.ExplicitIdentity), "Global document data needs an explicit retained/replacement audit.");
        var global = choices.Single(x => x.Kind == "global");
        ToolboxParcelModel.Require(global.Choice is ToolboxValueChoice.Retain or ToolboxValueChoice.Replace
            && GlobalHash(proposal.ResolvedSource) == GlobalHash(global.Choice == ToolboxValueChoice.Retain ? current.Model.Source : proposal.Batch.Candidate), "Unreviewed global data replacement.");
        var identityMap = new ToolboxIdentityMap(current.Model.Case, map.ToImmutable());
        var model = current.Model.Reconcile(context, proposal.ResolvedSource, identityMap, retired);
        string Resolve(string id)
        {
            if (!id.StartsWith("@", StringComparison.Ordinal)) return id;
            ToolboxParcelModel.Require(identityMap.Ids.ContainsKey(id[1..]), "Unknown symbolic result identity.");
            return identityMap.Ids[id[1..]];
        }
        var boundaries = proposal.Boundaries.Select(b => b with { Members = b.Members.Select(m => m with { ParcelId = Resolve(m.ParcelId), SegmentId = Resolve(m.SegmentId) }).ToImmutableArray(),
            PointIds = b.PointIds.IsDefault ? [] : b.PointIds.Select(Resolve).ToImmutableArray() }).ToImmutableArray();
        var graph = proposal.Dependencies with { Nodes = proposal.Dependencies.Nodes.Select(n =>
        {
            if (n.CheckBinding is not { } b) return n with { ReferenceId = Resolve(n.ReferenceId),
                DecisionParcelId = n.DecisionParcelId is null ? null : Resolve(n.DecisionParcelId) };
            ToolboxParcelModel.Require(n.Kind == ToolboxDependencyKind.Check, "Only check nodes may declare check bindings.");
            var bound = b with { ScopeId = Resolve(b.ScopeId) };
            var scope = new ToolboxPolicyScope(model.Case.CaseId, bound.ScopeKind, bound.ScopeId, bound.Revision, bound.InputFingerprint);
            var requirement = ToolboxReviewPolicyResolver.Resolve(policy, bound.Stage, [scope]).SingleOrDefault(r => r.ItemId == bound.ItemId
                && r.CheckId == bound.CheckId && r.ScopeKind == bound.ScopeKind && r.ScopeId == bound.ScopeId);
            ToolboxParcelModel.Require(requirement is not null, "Unknown check binding.");
            return n with { ReferenceId = requirement!.ResolutionFingerprint, CheckBinding = bound };
        }).ToImmutableArray() };
        var applied = proposal.Lineage.Select(l => new ToolboxAppliedLineage(l, l.ChildKeys.Select(key => identityMap.Ids["parcel:" + key]).ToImmutableArray())).ToImmutableArray();
        var applicability = current.Model.Relations.Applicability.AddRange(proposal.Applicability);
        var barriers = applicability.GroupBy(x => x.RecordId, StringComparer.OrdinalIgnoreCase).Select(x => x.Last())
            .Where(x => x.Disposition == ToolboxApplicabilityDisposition.Unresolved
                || x.Disposition != ToolboxApplicabilityDisposition.NotApplicable
                    && x.TargetParcelKeys.Any(key => !identityMap.Ids.ContainsKey("parcel:" + key)))
            .Select(x => "Unresolved historical applicability: " + x.RecordId).ToImmutableArray();
        var relations = new ToolboxModelRelations(policy.Pin, proposal.Batch.SourceHashes, boundaries, graph,
            current.Model.Relations.Archives.Add(current.Model), current.Lineage.AddRange(applied),
            applicability, barriers) { HumanInputBindings = current.Model.Relations.HumanInputBindings,
                FindingInputBindings = current.Model.Relations.FindingInputBindings, Adjacency = proposal.Adjacency };
        model = model.WithRelations(relations);
        _ = ToolboxBoundaryValidator.Validate(model, boundaries, PolicyVersion);
        graph.Validate(model);
        current.Model.Relations.Dependencies.Validate(current.Model);
        ToolboxParcelModel.Require(current.Model.Relations.Dependencies.CoverageIssues(current.Model).Length == 0, "Incomplete base dependency coverage.");
        ToolboxParcelModel.Require(graph.CoverageIssues(model).Length == 0, "Incomplete result dependency coverage.");
        foreach (var l in applied)
            foreach (var key in l.Proposal.ChildKeys)
                model = model.SetInclusion(new(identityMap.Ids["parcel:" + key], l.Proposal.ChildInclusions[key], authority.Actor,
                    DateTimeOffset.UtcNow, l.Proposal.Reason, l.Proposal.EvidenceId, policy.Pin.PolicyVersion), policy.Pin.PolicyVersion, true);
        return new(proposal.ResultRevision, model, current.Register, current.Reruns.Add(proposal.Batch),
            current.Lineage.AddRange(applied), current.Operations.Add(new(proposal.OperationId, proposal.Digest, proposal.ResultRevision)))
        {
            AcceptedDispositions = current.AcceptedDispositions.Add(new(proposal.OperationId, proposal.Digest,
                current.Revision, proposal.ResultRevision, DateTimeOffset.UtcNow, choices))
        };
    }

    private sealed record Candidate(string Kind, string Key, string ParcelKey, JsonElement Data);
    private static Candidate[] Inventory(CanonicalReviewContract source) => source.Parcels.Select(x => new Candidate("parcel", x.Key, x.Key, x.Data))
        .Concat(source.Rows.Select(x => new Candidate("row", x.Key, x.ParcelKey, x.Data)))
        .Concat(source.Segments.Select(x => new Candidate("segment", x.Key, x.ParcelKey, x.Data))).ToArray();
    private static string ValueHash(JsonElement data)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(data.GetRawText())!.AsObject();
        foreach (var field in new[] { "parcel_group_id", "row_id", "segment_id", "display_label", "parcel_name", "object_id", "saved_at" }) node.Remove(field);
        return ToolboxSemanticHash.Of(node);
    }
    private static string GlobalHash(CanonicalReviewContract source)
    {
        var json = source.ToJson();
        foreach (var field in new[] { "parcel_groups", "rows", "segments", "point_memberships", "segment_memberships" }) json.Remove(field);
        return ToolboxSemanticHash.Of(json);
    }
    private static void ValidateSources(CanonicalReviewContract source, ImmutableDictionary<string, string> hashes) =>
        ToolboxParcelModel.Require(hashes.Count == source.Sources.Length && hashes.All(x => !string.IsNullOrWhiteSpace(x.Value)
            && source.Sources.Any(s => ToolboxParcelModel.Same(s.Id, x.Key))), "Complete source identity/hash inventory required.");
    private static void ValidateAdjacency(CanonicalReviewContract source, ToolboxCaseIdentity identity, ToolboxAdjacencyReview? review)
    {
        if (review is null) return;
        ToolboxParcelModel.Require(review.Case == identity && !review.ParcelKeys.IsDefault && review.ParcelKeys.Length == source.Parcels.Length
            && review.ParcelKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count() == review.ParcelKeys.Length
            && review.ParcelKeys.All(key => source.Parcels.Any(p => ToolboxParcelModel.Same(p.Key, key))) && source.HasEvidence(review.EvidenceId)
            && !string.IsNullOrWhiteSpace(review.Actor) && !string.IsNullOrWhiteSpace(review.Reason)
            && review.PolicyVersion == PolicyVersion, "Invalid adjacency inventory/evidence.");
    }
    private static void ValidateBatch(ToolboxReconciliationSnapshot current, ToolboxRerunBatch b)
    {
        ToolboxParcelModel.Require(b.Case == current.Model.Case && b.BaseRevision == current.Revision && b.BaseFingerprint == current.Fingerprint
            && b.Status == ToolboxRerunStatus.Succeeded && b.ReconciliationPolicy == PolicyVersion && b.ReviewPolicy == current.Model.Relations?.Policy
            && !string.IsNullOrWhiteSpace(b.BatchId) && !string.IsNullOrWhiteSpace(b.RunId) && !string.IsNullOrWhiteSpace(b.ToolVersion)
            && !string.IsNullOrWhiteSpace(b.ProviderVersion) && b.CandidateDigest == CandidateDigest(b.Candidate)
            && b.Candidate.TransactionNumber == current.Model.Case.TransactionNumber, "Foreign/stale/incomplete rerun envelope.");
        ValidateSources(b.Candidate, b.SourceHashes);
        var failed = new[] { "failed", "partial", "canceled", "cancelled", "incomplete" };
        ToolboxParcelModel.Require(!(b.Candidate.Quality.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String
            && failed.Contains(status.GetString(), StringComparer.OrdinalIgnoreCase))
            && !b.Candidate.Sources.Any(s => failed.Contains(s.Status, StringComparer.OrdinalIgnoreCase)), "Candidate reports failed/incomplete extraction despite batch success.");
    }
}
