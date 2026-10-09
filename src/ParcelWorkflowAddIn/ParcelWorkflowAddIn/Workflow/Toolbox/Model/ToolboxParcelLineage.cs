using System.Collections.Immutable;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

namespace ParcelWorkflowAddIn.Workflow.Toolbox.Model;

public enum ToolboxLineageKind { Split, Merge }
public sealed record ToolboxParcelLineage(string OperationId, ToolboxCaseIdentity Case, ToolboxLineageKind Kind,
    string ParentRevision, string ResultRevision, ImmutableArray<string> ParentIds, ImmutableArray<string> ChildKeys,
    string Actor, string Reason, string EvidenceId, ImmutableDictionary<string, ToolboxInclusion> ChildInclusions);
public enum ToolboxApplicabilityDisposition { Retained, Applicable, NotApplicable, Unresolved }
public sealed record ToolboxHistoryApplicability(string RecordId, string OriginalRevision,
    ImmutableArray<string> TargetParcelKeys, ToolboxApplicabilityDisposition Disposition,
    string Actor, string Reason, string EvidenceId);
public sealed record ToolboxAppliedLineage(ToolboxParcelLineage Proposal, ImmutableArray<string> ChildIds);

public sealed record ToolboxModelRelations(ToolboxPolicyPin Policy, ImmutableDictionary<string, string> SourceHashes,
    ImmutableArray<ToolboxPhysicalBoundary> Boundaries, ToolboxDependencyInventory Dependencies,
    ImmutableArray<ToolboxParcelModel> Archives, ImmutableArray<ToolboxAppliedLineage> Lineage,
    ImmutableArray<ToolboxHistoryApplicability> Applicability, ImmutableArray<string> ReadinessBarriers)
{
    public ImmutableDictionary<string, string> HumanInputBindings { get; init; } = ImmutableDictionary<string, string>.Empty;
    public ImmutableDictionary<string, string> FindingInputBindings { get; init; } = ImmutableDictionary<string, string>.Empty;
    public ToolboxAdjacencyReview? Adjacency { get; init; }
    public ImmutableArray<string> FindingParcels(ToolboxParcelModel current, ToolboxFinding finding)
    {
        if (finding.ParcelIds.Length == 0) return current.Parcels.Select(x => x.Id).ToImmutableArray();
        if (finding.ParcelIds.All(id => current.Parcels.Any(x => ToolboxParcelModel.Same(x.Id, id)))) return finding.ParcelIds;
        var link = Applicability.LastOrDefault(x => ToolboxParcelModel.Same(x.RecordId, finding.Id));
        return link is not null && link.Disposition is ToolboxApplicabilityDisposition.Applicable or ToolboxApplicabilityDisposition.Retained
            ? link.TargetParcelKeys.Where(key => current.IdentityMap.Ids.ContainsKey("parcel:" + key))
                .Select(key => current.IdentityMap.Ids["parcel:" + key]).ToImmutableArray() : [];
    }
}

internal static class ToolboxLineageValidator
{
    public static void Validate(ToolboxReconciliationSnapshot current, ToolboxRerunBatch batch,
        ImmutableArray<ToolboxParcelLineage> lineage, ImmutableArray<ToolboxHistoryApplicability> applicability,
        string resultRevision, string actor)
    {
        ToolboxParcelModel.Require(!lineage.IsDefault && !applicability.IsDefault, "Absent lineage/applicability inventory.");
        var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var children = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in lineage)
        {
            ToolboxParcelModel.Require(l.Case == current.Model.Case && Enum.IsDefined(l.Kind)
                && l.ParentRevision == current.Revision && l.ResultRevision == resultRevision && l.ParentRevision != l.ResultRevision
                && !string.IsNullOrWhiteSpace(l.OperationId) && l.Actor == actor && !string.IsNullOrWhiteSpace(l.Reason)
                && batch.Candidate.HasEvidence(l.EvidenceId) && !l.ParentIds.IsDefaultOrEmpty && !l.ChildKeys.IsDefaultOrEmpty
                && (l.Kind == ToolboxLineageKind.Split ? l.ParentIds.Length == 1 && l.ChildKeys.Length > 1
                    : l.ParentIds.Length > 1 && l.ChildKeys.Length == 1), "Invalid split/merge ancestry or many-to-many operation.");
            foreach (var id in l.ParentIds)
                ToolboxParcelModel.Require(parents.Add(id) && current.Model.Parcels.Any(x => ToolboxParcelModel.Same(x.Id, id)), "Duplicate/foreign/retired lineage parent.");
            foreach (var key in l.ChildKeys)
                ToolboxParcelModel.Require(children.Add(key) && batch.Candidate.Parcels.Any(x => ToolboxParcelModel.Same(x.Key, key))
                    && !current.Model.Parcels.Any(x => ToolboxParcelModel.Same(x.SourceKey, key))
                    && l.ChildInclusions.TryGetValue(key, out var inclusion) && Enum.IsDefined(inclusion)
                    && inclusion != ToolboxInclusion.Unresolved, "Missing inclusion or reused/cyclic result parcel key.");
            ToolboxParcelModel.Require(l.ChildInclusions.Count == l.ChildKeys.Length, "Foreign child inclusion.");
        }
        ToolboxParcelModel.Require(lineage.Select(x => x.OperationId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == lineage.Length
            && !lineage.Any(x => current.Lineage.Any(y => ToolboxParcelModel.Same(y.Proposal.OperationId, x.OperationId))), "Duplicate lineage operation.");
        ToolboxParcelModel.Require(applicability.Select(x => x.RecordId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == applicability.Length,
            "Duplicate applicability mapping.");
        var records = current.Register.Findings.Select(x => x.Id)
            .Concat(current.Register.HumanReviews.Select(CorrectionId)).Concat(ManualEntityIds(current.Model)).ToArray();
        foreach (var link in applicability)
            ToolboxParcelModel.Require(records.Contains(link.RecordId, StringComparer.OrdinalIgnoreCase)
                && link.OriginalRevision == current.Revision && Enum.IsDefined(link.Disposition)
                && link.Actor == actor && !string.IsNullOrWhiteSpace(link.Reason) && batch.Candidate.HasEvidence(link.EvidenceId)
                && !link.TargetParcelKeys.IsDefault
                && link.TargetParcelKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count() == link.TargetParcelKeys.Length
                && link.TargetParcelKeys.All(key => batch.Candidate.Parcels.Any(x => ToolboxParcelModel.Same(x.Key, key)))
                && (link.Disposition is not (ToolboxApplicabilityDisposition.Applicable or ToolboxApplicabilityDisposition.Retained)
                    || link.TargetParcelKeys.Length > 0), "Invalid finding/correction applicability audit.");
        var retired = lineage.SelectMany(x => x.ParentIds).ToArray();
        var affectedFindings = current.Register.Findings.Where(x => current.Model.FindingParcels(x).Any(id => retired.Contains(id, StringComparer.OrdinalIgnoreCase))).Select(x => x.Id);
        var required = affectedFindings.Concat(lineage.Length > 0
            ? current.Register.HumanReviews.Select(CorrectionId).Concat(ManualEntityIds(current.Model)) : []);
        ToolboxParcelModel.Require(required.All(id => applicability.Any(x => ToolboxParcelModel.Same(x.RecordId, id))), "Missing historical finding/manual correction applicability.");
    }

    public static string CorrectionId(ToolboxHumanReview review) => "human:" + ToolboxSemanticHash.Of(review);
    public static IEnumerable<string> ManualEntityIds(ToolboxParcelModel model) => model.Rows.Concat(model.Segments)
        .Where(x => x.Evidence.TryGetProperty("original_values", out _) || x.Evidence.TryGetProperty("is_manual", out var manual) && manual.ValueKind == System.Text.Json.JsonValueKind.True)
        .Select(x => "manual:" + x.Id);
}
