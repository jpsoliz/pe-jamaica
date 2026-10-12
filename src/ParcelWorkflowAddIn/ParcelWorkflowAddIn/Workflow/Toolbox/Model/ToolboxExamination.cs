using System.Collections.Immutable;
using System.Text.Json;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

namespace ParcelWorkflowAddIn.Workflow.Toolbox.Model;

public enum ToolboxFindingOrigin { ExtractionMismatch, SourceDefect, Mixed, Unknown }
public enum ToolboxCheckOutcome { Passed, Failed, NotEvaluable, Pending }
public enum ToolboxPresence { Present, MissingFromSource, NotExtracted, Uncertain, NotReviewed }
public enum ToolboxAccuracy { Confirmed, Corrected, Uncertain, NotReviewed }
public enum ToolboxReadinessStatus { Blocked, Pending, Ready, ReadyWithRecordedDefects }
public sealed record ToolboxFindingEvent(ToolboxPreset Stage, string Actor, DateTimeOffset At, string Status,
    string Reason, string EvidenceId, string InputFingerprint, string RuleVersion, string PolicyVersion, string DependencyFingerprint = "");
public sealed record ToolboxFinding(string Id, ToolboxFindingOrigin Origin, ImmutableArray<string> ParcelIds,
    string DocumentId, int? Page, string Zone, bool ExtractionCorrected, bool GeometryDefect,
    ImmutableArray<ToolboxFindingEvent> History, string RuleId, string Code, string Severity);
public sealed record ToolboxHumanReview(string EvidenceId, string RawValue, string ReviewedValue,
    string CaptureMethod, ToolboxPresence Presence, ToolboxAccuracy Accuracy, ToolboxPreset Stage,
    string Actor, DateTimeOffset At, string Reason, string InputRevision, string SourceFingerprint = "");
public sealed record ToolboxCheckReview(string ResolutionFingerprint, ToolboxCheckOutcome Outcome,
    string Reason, ImmutableArray<string> EvidenceIds, string Actor, DateTimeOffset At,
    string? FindingId = null, bool GeometryCheck = false, string EvidenceFingerprint = "",
    ImmutableDictionary<string, string>? GeometryFingerprints = null, string CanonicalInputFingerprint = "",
    string DependencyFingerprint = "");
public sealed record ToolboxDispositionAuthority(string Actor, string PolicyVersion, bool Authorized);
public sealed record ToolboxParcelDisposition(ToolboxCaseIdentity Case, ToolboxPreset Stage, ToolboxPolicyPin Policy,
    string ParcelId, string Fingerprint, bool Ready, string Actor, DateTimeOffset At, ImmutableArray<string> Reasons)
{
    public bool IsCurrent(ToolboxParcelDisposition current) => Case == current.Case && Stage == current.Stage
        && ParcelId == current.ParcelId && Fingerprint == current.Fingerprint && Ready == current.Ready;
}
public sealed record ToolboxReadinessSnapshot(ToolboxCaseIdentity Case, ToolboxPreset Stage,
    ToolboxReadinessStatus Status, bool GeometryApproved, string Fingerprint, string Actor, DateTimeOffset At,
    int Detected, int Included, int Excluded, int Unresolved, int CurrentAccepted, ImmutableArray<string> Reasons,
    ImmutableArray<ToolboxFinding> Findings, ImmutableArray<ToolboxCheckReview> Reviews,
    ImmutableArray<ToolboxParcelDisposition> ParcelDispositions)
{
    public bool RemoteActionAllowed => false;
    public string Route => "normal_next_stage";
    public bool IsCurrent(ToolboxReadinessSnapshot current) => Case == current.Case && Stage == current.Stage
        && Fingerprint == current.Fingerprint && Actor == current.Actor
        && Status == current.Status && GeometryApproved == current.GeometryApproved;
}

public sealed class ToolboxExaminationRegister
{
    public ToolboxCaseIdentity? Case { get; }
    public ImmutableArray<ToolboxFinding> Findings { get; }
    public ImmutableArray<ToolboxHumanReview> HumanReviews { get; }
    public ImmutableArray<ToolboxReadinessSnapshot> Decisions { get; }
    public ToolboxExaminationRegister() : this(null, [], [], []) { }
    private ToolboxExaminationRegister(ToolboxCaseIdentity? identity, ImmutableArray<ToolboxFinding> findings,
        ImmutableArray<ToolboxHumanReview> humanReviews, ImmutableArray<ToolboxReadinessSnapshot> decisions)
    { Case = identity; Findings = findings; HumanReviews = humanReviews; Decisions = decisions; }

    internal static ToolboxExaminationRegister Restore(ToolboxParcelModel model, ToolboxCaseIdentity? identity,
        ImmutableArray<ToolboxFinding> findings, ImmutableArray<ToolboxHumanReview> humanReviews,
        ImmutableArray<ToolboxReadinessSnapshot> decisions)
    {
        Persistence.ToolboxStorageValidation.Examination(model, identity, findings, humanReviews, decisions, []);
        return new(identity, findings, humanReviews, decisions);
    }

    public ToolboxExaminationRegister RecordFinding(ToolboxParcelModel model, ToolboxFinding finding)
    {
        ToolboxParcelModel.Require(Case is null || Case == model.Case, "Foreign finding register.");
        ToolboxParcelModel.Require(!finding.History.IsDefaultOrEmpty && !finding.ParcelIds.IsDefault, "Missing finding history/scope.");
        var existing = Findings.FirstOrDefault(x => ToolboxParcelModel.Same(x.Id, finding.Id));
        if (model.Relations is not null)
        {
            var fingerprint = ToolboxReadinessEvaluator.FindingInputFingerprint(model, finding);
            finding = finding with { History = finding.History.Select((e, i) => i >= (existing?.History.Length ?? 0)
                ? e with { DependencyFingerprint = fingerprint } : e).ToImmutableArray() };
        }
        var evidenceModels = new[] { model }.Concat(existing is null ? [] : model.Relations?.Archives ?? []).ToArray();
        ToolboxParcelModel.Require(evidenceModels.All(m => m.Case == model.Case), "Foreign historical finding registry.");
        ToolboxParcelModel.Require(!string.IsNullOrWhiteSpace(finding.Id) && Enum.IsDefined(finding.Origin)
            && !string.IsNullOrWhiteSpace(finding.RuleId) && !string.IsNullOrWhiteSpace(finding.Code)
            && finding.Severity is "info" or "warning" or "error"
            && !finding.History.IsDefaultOrEmpty && !finding.ParcelIds.IsDefault
            && finding.ParcelIds.All(id => evidenceModels.Any(m => m.Parcels.Any(x => ToolboxParcelModel.Same(x.Id, id))))
            && finding.ParcelIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() == finding.ParcelIds.Length
            && evidenceModels.Any(m => m.Source.Sources.Any(x => ToolboxParcelModel.Same(x.Id, finding.DocumentId)))
            && finding.Page is null or > 0, "Invalid finding identity or scope.");
        foreach (var entry in finding.History)
            ToolboxParcelModel.Require(evidenceModels.Any(m => ValidEvent(m, entry)
                && m.Source.Traces.Any(x => ToolboxParcelModel.Same(x.Id, entry.EvidenceId) && ToolboxParcelModel.Same(x.SourceId, finding.DocumentId)
                    && (finding.Page is null || x.Page == finding.Page) && (finding.Zone.Length == 0 || x.Zone == finding.Zone))),
                "Finding event requires scoped evidence, actor, reason, version and revision.");
        if (existing is not null)
            ToolboxParcelModel.Require(existing.Origin == finding.Origin && existing.DocumentId == finding.DocumentId
                && existing.Page == finding.Page && existing.Zone == finding.Zone && existing.GeometryDefect == finding.GeometryDefect
                && existing.RuleId == finding.RuleId && existing.Code == finding.Code && existing.Severity == finding.Severity
                && existing.ParcelIds.SequenceEqual(finding.ParcelIds)
                && finding.History.Length > existing.History.Length
                && finding.History.Take(existing.History.Length).SequenceEqual(existing.History), "Finding history cannot be erased or reassigned.");
        return new(model.Case, Findings.Where(x => !ToolboxParcelModel.Same(x.Id, finding.Id)).Append(finding).ToImmutableArray(), HumanReviews, Decisions);
    }

    public ToolboxExaminationRegister RecordHumanReview(ToolboxParcelModel model, ToolboxHumanReview review,
        ToolboxDispositionAuthority authority, bool geometryEvidence = false, bool linkedSource = false)
    {
        ToolboxParcelModel.Require(Case is null || Case == model.Case, "Foreign human review register.");
        ToolboxParcelModel.Require(authority.Authorized && authority.Actor == review.Actor
            && !string.IsNullOrWhiteSpace(authority.PolicyVersion) && model.Source.HasEvidence(review.EvidenceId)
            && !linkedSource && !(geometryEvidence && review.Stage != ToolboxPreset.Compute)
            && review.Stage is ToolboxPreset.Compute or ToolboxPreset.Compare
            && Enum.IsDefined(review.Presence) && Enum.IsDefined(review.Accuracy)
            && !string.IsNullOrWhiteSpace(review.Actor) && !string.IsNullOrWhiteSpace(review.Reason)
            && !string.IsNullOrWhiteSpace(review.InputRevision) && !string.IsNullOrWhiteSpace(review.CaptureMethod), "Invalid human review or edit scope.");
        ToolboxParcelModel.Require(review.Presence != ToolboxPresence.MissingFromSource
            || review.Accuracy is ToolboxAccuracy.Confirmed or ToolboxAccuracy.Corrected, "Source absence needs human confirmation.");
        if (model.Relations is not null) review = review with { SourceFingerprint = ToolboxReadinessEvaluator.HumanSourceFingerprint(model, review.EvidenceId) };
        return new(model.Case, Findings, HumanReviews.Add(review), Decisions);
    }

    public ToolboxExaminationRegister RecordDecision(ToolboxReadinessSnapshot decision)
    {
        ToolboxParcelModel.Require(Case is null || Case == decision.Case, "Foreign decision register.");
        return new(decision.Case, Findings, HumanReviews, Decisions.Add(decision));
    }

    internal static bool ValidEvent(ToolboxParcelModel model, ToolboxFindingEvent entry) =>
        entry.Stage is ToolboxPreset.Compute or ToolboxPreset.Compare && model.Source.HasEvidence(entry.EvidenceId)
        && !string.IsNullOrWhiteSpace(entry.Actor) && !string.IsNullOrWhiteSpace(entry.Reason)
        && entry.Status is "recorded" or "retained" or "reopened" or "resolved" or "corrected_and_rechecked"
        && !string.IsNullOrWhiteSpace(entry.InputFingerprint)
        && !string.IsNullOrWhiteSpace(entry.RuleVersion) && !string.IsNullOrWhiteSpace(entry.PolicyVersion);
}

public static class ToolboxReadinessEvaluator
{
    internal static string FindingEventId(ToolboxFinding finding) => ToolboxSemanticHash.Of(new { finding.Id, latest = finding.History[^1] });
    internal static string FindingInputFingerprint(ToolboxParcelModel model, ToolboxFinding finding)
    {
        var parcels = model.Relations is null && finding.ParcelIds.Length == 0
            ? model.Parcels.Select(x => x.Id).ToImmutableArray() : model.FindingParcels(finding);
        return ToolboxSemanticHash.Of(new { version = "toolbox_finding_dependencies_v1", model.Case, policy = model.Relations?.Policy,
            inputs = parcels.Order(StringComparer.Ordinal).Select(id => new { id,
                geometry = finding.GeometryDefect ? model.Relations?.Dependencies.ConsumerFingerprint(model, ToolboxDependencyKind.Parcel, id) : null,
                tabular = finding.GeometryDefect ? null : CanonicalInputFingerprint(model, ToolboxScopeKind.Parcel, id) }),
            evidence = HumanSourceFingerprint(model, finding.History.IsDefaultOrEmpty ? "" : finding.History[^1].EvidenceId) });
    }
    private static bool FindingInputsCurrent(ToolboxParcelModel model, ToolboxFinding finding)
    {
        if (model.Relations is not { } relations) return true;
        var stored = finding.History[^1].DependencyFingerprint;
        if (stored.Length == 0) stored = relations.FindingInputBindings.GetValueOrDefault(FindingEventId(finding)) ?? "";
        return stored == FindingInputFingerprint(model, finding);
    }
    public static string HumanSourceFingerprint(ToolboxParcelModel model, string evidenceId)
    {
        var trace = model.Source.Traces.FirstOrDefault(x => ToolboxParcelModel.Same(x.Id, evidenceId));
        return ToolboxSemanticHash.Of(new { version = "toolbox_human_source_v1", trace = trace?.Data,
            hash = model.Relations?.SourceHashes.FirstOrDefault(x => ToolboxParcelModel.Same(x.Key, trace?.SourceId ?? "")).Value });
    }
    public static string CanonicalInputFingerprint(ToolboxParcelModel model, ToolboxScopeKind kind, string? scopeId,
        bool geometryCheck = false)
    {
        // Geometry dependencies are bound separately; tabular changes must not stale geometry-only checks.
        object? inputs = geometryCheck ? null : kind switch
        {
            ToolboxScopeKind.Participant => Fact("participants", scopeId),
            ToolboxScopeKind.IdentifierPair => Fact("identifier_pairs", scopeId),
            ToolboxScopeKind.Parcel => ParcelFacts(),
            ToolboxScopeKind.Boundary => (object?)model.Segments.FirstOrDefault(x => ToolboxParcelModel.Same(x.Id, scopeId ?? ""))?.Evidence
                ?? model.Relations?.Boundaries.FirstOrDefault(x => ToolboxParcelModel.Same(x.Id, scopeId ?? "")),
            _ => new[] { "survey_metadata", "document_sections", "parties", "representatives", "adjacent_owners",
                "volume_folios", "participants", "identifier_pairs", "global_facts" }
                .Where(name => model.Source.GlobalData.TryGetProperty(name, out _))
                .ToDictionary(name => name, name => model.Source.GlobalData.GetProperty(name), StringComparer.Ordinal)
        };
        var baseline = ToolboxSemanticHash.Of(new { version = "toolbox_canonical_inputs_v2", kind, scopeId, geometryCheck, inputs });
        if (model.Relations is null || geometryCheck) return baseline;
        var parcelIds = model.ParcelsForScope(kind, scopeId);
        var parcelKeys = model.Parcels.Where(x => parcelIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase))
            .Select(x => x.SourceKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var links = model.Source.Links.Where(x => kind is ToolboxScopeKind.Case or ToolboxScopeKind.Document
                || parcelKeys.Contains(x.ParcelKey))
            .OrderBy(x => x.EvidenceId, StringComparer.Ordinal).ThenBy(x => x.ParcelKey, StringComparer.Ordinal)
            .ThenBy(x => x.Role, StringComparer.Ordinal).ToArray();
        if (links.Length == 0) return baseline;
        return ToolboxSemanticHash.Of(new { version = "toolbox_canonical_dependencies_v2", baseline,
            links = links.Select(link => new { link.EvidenceId, link.ParcelKey, link.Role, source = HumanSourceFingerprint(model, link.EvidenceId) }) });

        JsonElement? Fact(string name, string? id) => model.Source.GlobalData.TryGetProperty(name, out var facts)
            && facts.ValueKind == JsonValueKind.Object
            ? facts.EnumerateObject().Where(x => ToolboxParcelModel.Same(x.Name, id ?? "")).Select(x => (JsonElement?)x.Value).FirstOrDefault() : null;
        object? ParcelFacts()
        {
            var parcel = model.Parcels.FirstOrDefault(x => ToolboxParcelModel.Same(x.Id, scopeId ?? ""));
            if (parcel is null) return null;
            var candidate = model.Source.Parcels.Single(x => ToolboxParcelModel.Same(x.Key, parcel.SourceKey)).Data;
            var data = System.Text.Json.Nodes.JsonNode.Parse(candidate.GetRawText())!.AsObject();
            data.Remove("display_label");
            return new { candidate = data, participants = Linked("participants", parcel.SourceKey), pairs = Linked("identifier_pairs", parcel.SourceKey) };
        }
        object Linked(string name, string parcelKey) => model.Source.GlobalData.TryGetProperty(name, out var facts)
            ? facts.EnumerateObject().Where(x => !x.Value.TryGetProperty("parcel_ids", out var links)
                || links.EnumerateArray().Any(id => ToolboxParcelModel.Same(id.GetString()!, parcelKey)))
                .OrderBy(x => x.Name, StringComparer.Ordinal).ToDictionary(x => x.Name, x => x.Value, StringComparer.Ordinal)
            : new Dictionary<string, JsonElement>();
    }

    public static string EvidenceFingerprint(ToolboxParcelModel model, ToolboxExaminationRegister register,
        IEnumerable<string> evidenceIds)
    {
        var ids = evidenceIds.Select(id => model.Source.Traces.FirstOrDefault(x => ToolboxParcelModel.Same(x.Id, id))?.Id ?? id)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var baseline = ToolboxSemanticHash.Of(new
        {
            version = "toolbox_evidence_inputs_v2",
            inputs = ids.Select(id => new
                {
                    id, source = model.Source.Traces.FirstOrDefault(x => ToolboxParcelModel.Same(x.Id, id))?.Data,
                    human = register.HumanReviews.LastOrDefault(x => ToolboxParcelModel.Same(x.EvidenceId, id)) is { } h
                        ? new { h.RawValue, h.ReviewedValue, h.CaptureMethod, h.Presence, h.Accuracy, h.Actor, h.InputRevision } : null
                })
        });
        return model.Relations is null ? baseline : ToolboxSemanticHash.Of(new { version = "toolbox_evidence_dependencies_v1", baseline,
            inputs = ids.Select(id => new { id, source = HumanSourceFingerprint(model, id),
                humanSource = register.HumanReviews.LastOrDefault(x => ToolboxParcelModel.Same(x.EvidenceId, id))?.SourceFingerprint }) });
    }

    public static ToolboxReadinessSnapshot Evaluate(ToolboxParcelModel model, ToolboxCaseContext context,
        ToolboxReviewPolicy policy, IEnumerable<ToolboxPolicyScope> scopeInventory,
        IEnumerable<ToolboxCheckReview> checkReviews, ToolboxExaminationRegister register,
        ToolboxDispositionAuthority authority, string geometryPolicyToken, DateTimeOffset now, bool geometryPolicyVerified = false)
    {
        ToolboxParcelModel.Require(model.Case == ToolboxCaseIdentity.From(context), "Foreign readiness context.");
        ToolboxParcelModel.Require(register.Case is null || register.Case == model.Case, "Foreign examination register.");
        var scopes = scopeInventory.ToImmutableArray();
        var reviews = checkReviews.ToImmutableArray();
        var requirements = ToolboxReviewPolicyResolver.Resolve(policy, context.SelectedPreset, scopes);
        var reasons = ImmutableArray.CreateBuilder<string>();
        var pending = false;
        var activeSourceDefect = false;
        var geometryApproved = true;
        var included = model.Parcels.Where(x => model.Inclusions[x.Id] == ToolboxInclusion.Included).ToArray();
        var parcelReasons = included.ToDictionary(x => x.Id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        void Issue(string reason, ToolboxResolvedReview? requirement = null, ToolboxFinding? finding = null, ImmutableArray<string>? parcelIds = null)
        {
            reasons.Add(reason);
            var affected = parcelIds ?? (requirement is not null ? model.ParcelsForScope(requirement.ScopeKind, requirement.ScopeId)
                : finding is not null && (model.Relations is not null || finding.ParcelIds.Length > 0)
                    ? model.FindingParcels(finding) : included.Select(x => x.Id).ToImmutableArray());
            foreach (var id in affected) if (parcelReasons.TryGetValue(id, out var issues)) issues.Add(reason);
        }
        var unresolved = model.Inclusions.Count(x => x.Value == ToolboxInclusion.Unresolved);
        if (included.Length == 0 || unresolved > 0) Issue("A nonempty included set and dispositions for all candidates are required.");
        if (!authority.Authorized || string.IsNullOrWhiteSpace(authority.Actor) || authority.Actor != context.OperatorId
            || authority.PolicyVersion != policy.Pin.PolicyVersion) Issue("Current local disposition authority is missing.");
        if (string.IsNullOrWhiteSpace(geometryPolicyToken)) Issue("Geometry policy fingerprint inputs are missing.");
        if (!geometryPolicyVerified) Issue("Geometry policy tokens are unverified.");
        if (model.Relations is { } relations)
        {
            if (relations.Adjacency is not { Complete: true }) Issue("Adjacency/no-shared-boundary evidence is incomplete.");
            relations.Dependencies.Validate(model);
            foreach (var issue in relations.Dependencies.CoverageIssues(model)) Issue(issue);
            foreach (var issue in relations.ReadinessBarriers) Issue(issue);
            if (relations.Policy != policy.Pin) Issue("Reconciliation review policy is stale.");
            foreach (var conflict in ToolboxBoundaryValidator.Validate(model, relations.Boundaries, ToolboxReconciliation.PolicyVersion))
                Issue("Boundary conflict: " + conflict.BoundaryId + "/" + conflict.Code, parcelIds: conflict.ParcelIds);
        }
        if (scopes.Any(x => !ToolboxParcelModel.Same(x.CaseId, model.Case.CaseId) || !model.KnowsScope(x.Kind, x.ScopeId)))
            Issue("Scope inventory contains foreign or unknown identities.");
        var expectedScopes = new[] { (ToolboxScopeKind.Case, model.Case.CaseId) }
            .Concat(model.Parcels.Select(x => (ToolboxScopeKind.Parcel, x.Id)))
            .Concat(model.Source.Sources.Select(x => (ToolboxScopeKind.Document, x.Id)))
            .Concat(model.Segments.Select(x => (ToolboxScopeKind.Boundary, x.Id)))
            .Concat((model.Relations?.Boundaries ?? []).Select(x => (ToolboxScopeKind.Boundary, x.Id)))
            .Concat(GlobalScopes(model, "participants", ToolboxScopeKind.Participant))
            .Concat(GlobalScopes(model, "identifier_pairs", ToolboxScopeKind.IdentifierPair));
        foreach (var (kind, id) in expectedScopes)
            if (!scopes.Any(x => x.Kind == kind && ToolboxParcelModel.Same(x.ScopeId, id))) Issue("Missing authoritative scope: " + kind + "/" + id);
        if (reviews.Select(x => x.ResolutionFingerprint).Distinct(StringComparer.Ordinal).Count() != reviews.Length)
            Issue("Duplicate check review.");
        if (reviews.Any(x => !requirements.Any(r => r.ResolutionFingerprint == x.ResolutionFingerprint)))
            Issue("Stale or foreign review result.");

        foreach (var required in requirements)
        {
            var label = required.ItemId + "/" + required.CheckId + "/" + required.ScopeId;
            if (required.ScopeId is not null && required.ScopeKind is ToolboxScopeKind.Parcel or ToolboxScopeKind.Boundary or ToolboxScopeKind.Participant or ToolboxScopeKind.IdentifierPair
                && model.ParcelsForScope(required.ScopeKind, required.ScopeId) is { Length: > 0 } affected
                && affected.All(id => model.Inclusions[id] == ToolboxInclusion.Excluded)) continue;
            if (required.Applicability == ToolboxApplicability.Unresolved || required.EvaluatorId == "unresolved.binding")
            { Issue("Unresolved binding/applicability: " + label, required); continue; }
            if (required.EffectiveRequirement == ToolboxReviewRequirement.NotApplicable) continue;
            var review = reviews.FirstOrDefault(x => x.ResolutionFingerprint == required.ResolutionFingerprint);
            if (review is null)
            {
                if (required.EffectiveRequirement == ToolboxReviewRequirement.Required) Issue("Review missing: " + label, required);
                continue;
            }
            var inputIssues = ReviewInputIssues(model, register, required, review, geometryPolicyToken, geometryPolicyVerified).ToArray();
            foreach (var issue in inputIssues) Issue(issue + ": " + label, required);
            if (inputIssues.Length > 0)
            {
                if (review.GeometryCheck) geometryApproved = false;
                continue;
            }
            if (review.Outcome == ToolboxCheckOutcome.Pending)
            {
                if (required.EffectiveRequirement == ToolboxReviewRequirement.Required) { pending = true; Issue("Pending: " + label + ": " + review.Reason, required); }
                geometryApproved &= !review.GeometryCheck;
                continue;
            }
            if (review.GeometryCheck && review.Outcome != ToolboxCheckOutcome.Passed) geometryApproved = false;
            if (review.Outcome != ToolboxCheckOutcome.Passed && required.Effect == ToolboxReviewEffect.RequiresDisposition)
            {
                var finding = register.Findings.FirstOrDefault(x => x.Id == review.FindingId);
                if (finding is null || !review.EvidenceIds.Any(id => ToolboxParcelModel.Same(id, finding.History[^1].EvidenceId))
                    || !CanForward(model, finding, required, authority)) Issue("Unresolved disposition: " + label, required);
            }
        }
        foreach (var finding in model.Source.Findings)
            if (!register.Findings.Any(x => x.Id == finding.Id)) Issue("Canonical finding not dispositioned: " + finding.Id);
        foreach (var finding in register.Findings)
        {
            var latest = finding.History[^1];
            var historical = finding.ParcelIds.Any(id => !model.Parcels.Any(x => ToolboxParcelModel.Same(x.Id, id)));
            var link = model.Relations?.Applicability.LastOrDefault(x => ToolboxParcelModel.Same(x.RecordId, finding.Id));
            if (link?.Disposition == ToolboxApplicabilityDisposition.NotApplicable) continue;
            if (historical || link is not null)
            {
                if (link is null || link.Disposition == ToolboxApplicabilityDisposition.Unresolved || model.FindingParcels(finding).Length == 0)
                    Issue("Historical finding applicability unresolved: " + finding.Id, finding: finding);
            }
            if (!FindingInputsCurrent(model, finding)) Issue("Finding disposition dependencies changed: " + finding.Id, finding: finding);
            if (finding.Origin == ToolboxFindingOrigin.Unknown
                || finding.Origin is ToolboxFindingOrigin.ExtractionMismatch or ToolboxFindingOrigin.Mixed && !finding.ExtractionCorrected)
                Issue("Extraction correction/origin unresolved: " + finding.Id, finding: finding);
            var resolved = latest.Status == "resolved" && HasCurrentResolution(model, register, finding, requirements, reviews,
                authority, geometryPolicyToken, geometryPolicyVerified);
            if (latest.Status == "resolved" && !resolved)
                Issue("Finding resolution needs a current passing recheck: " + finding.Id, finding: finding);
            if (finding.Origin is ToolboxFindingOrigin.SourceDefect or ToolboxFindingOrigin.Mixed || latest.Status == "resolved")
            {
                if (!authority.Authorized || latest.Actor != authority.Actor || latest.PolicyVersion != policy.Pin.PolicyVersion
                    || !requirements.Any(x => x.ItemId == finding.RuleId && x.EvaluatorVersion == latest.RuleVersion)
                    || !scopes.Any(x => x.InputFingerprint == latest.InputFingerprint
                        && (x.Kind == ToolboxScopeKind.Document && x.ScopeId == finding.DocumentId
                            || model.ParcelsForScope(x.Kind, x.ScopeId).Any(id => model.Relations is null && finding.ParcelIds.Length == 0
                                || model.FindingParcels(finding).Contains(id)))))
                    Issue("Stale or unauthorized finding disposition: " + finding.Id, finding: finding);
                if (!resolved && finding.Origin is ToolboxFindingOrigin.SourceDefect or ToolboxFindingOrigin.Mixed)
                {
                    activeSourceDefect = true;
                    if (finding.GeometryDefect) geometryApproved = false;
                }
            }
        }
        // A successful extraction or a legacy hash is not a geometry approval.
        foreach (var parcel in included)
            if (!model.Rows.Any(x => x.ParcelId == parcel.Id) || !requirements.Any(r => r.ScopeKind == ToolboxScopeKind.Parcel && ToolboxParcelModel.Same(r.ScopeId ?? "", parcel.Id)
                && reviews.Any(x => x.ResolutionFingerprint == r.ResolutionFingerprint && x.GeometryCheck && x.Outcome == ToolboxCheckOutcome.Passed)))
                geometryApproved = false;
        var status = reasons.Count > 0 ? (pending && reasons.All(x => x.StartsWith("Pending:", StringComparison.Ordinal))
            ? ToolboxReadinessStatus.Pending : ToolboxReadinessStatus.Blocked)
            : activeSourceDefect
                ? ToolboxReadinessStatus.ReadyWithRecordedDefects : ToolboxReadinessStatus.Ready;
        geometryApproved &= status is ToolboxReadinessStatus.Ready or ToolboxReadinessStatus.ReadyWithRecordedDefects;
        var fingerprintInputs = new
        {
            version = "toolbox_readiness_inputs_v2", status, geometryApproved,
            model.Case, context.SelectedPreset, model.InclusionFingerprint, policy.Pin, authority, geometryPolicyToken, geometryPolicyVerified,
            linkedSources = model.LinkedSources.OrderBy(x => x.SourceId, StringComparer.Ordinal),
            geometry = included.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => new { x.Id,
                fingerprint = string.IsNullOrWhiteSpace(geometryPolicyToken) ? "missing" : model.ParcelFingerprint(x.Id, geometryPolicyToken) }),
            scopes = scopes.OrderBy(x => x.Kind).ThenBy(x => x.ScopeId, StringComparer.Ordinal).Select(x => new { x.Kind, x.ScopeId, x.RevisionId, x.InputFingerprint, x.Facts }),
            requirements = requirements.Select(x => x.ResolutionFingerprint).OrderBy(x => x, StringComparer.Ordinal),
            canonicalInputs = CurrentCanonicalInputs(requirements),
            evidence = EvidenceFingerprint(model, register, reviews.Where(x => !x.EvidenceIds.IsDefaultOrEmpty).SelectMany(x => x.EvidenceIds)),
            reviews = ReviewRecords(reviews.OrderBy(x => x.ResolutionFingerprint, StringComparer.Ordinal)),
            findings = ReviewRecords(register.Findings.OrderBy(x => x.Id, StringComparer.Ordinal)),
            human = ReviewRecords(register.HumanReviews.GroupBy(x => x.EvidenceId).Select(x => x.Last()).OrderBy(x => x.EvidenceId, StringComparer.Ordinal))
        };
        var fingerprint = model.Relations is null ? ToolboxSemanticHash.Of(fingerprintInputs) : ToolboxSemanticHash.Of(new
        { version = "toolbox_readiness_dependencies_v1", fingerprintInputs, model.Relations.Dependencies,
            model.Relations.Boundaries, model.Relations.Applicability, model.Relations.HumanInputBindings, model.Relations.FindingInputBindings, model.Relations.Adjacency });
        return new(model.Case, context.SelectedPreset, status, geometryApproved,
            fingerprint, authority.Actor, now, model.Parcels.Length, included.Length,
            model.Inclusions.Count(x => x.Value == ToolboxInclusion.Excluded), unresolved,
            parcelReasons.Count(x => x.Value.Count == 0), reasons.ToImmutable(), register.Findings, reviews,
            included.Select(x => new ToolboxParcelDisposition(model.Case, context.SelectedPreset, policy.Pin, x.Id,
                ParcelDecisionFingerprint(x.Id), parcelReasons[x.Id].Count == 0, authority.Actor, now,
                parcelReasons[x.Id].ToImmutableArray())).ToImmutableArray());

        string ParcelDecisionFingerprint(string id)
        {
            var relevant = requirements.Where(x => model.ParcelsForScope(x.ScopeKind, x.ScopeId).Contains(id)).ToArray();
            var reviewInputs = reviews.Where(x => relevant.Any(r => r.ResolutionFingerprint == x.ResolutionFingerprint))
                .OrderBy(x => x.ResolutionFingerprint, StringComparer.Ordinal).ToArray();
            var evidence = reviewInputs.Where(x => !x.EvidenceIds.IsDefaultOrEmpty).SelectMany(x => x.EvidenceIds).Distinct(StringComparer.Ordinal).ToArray();
            var baseline = ToolboxSemanticHash.Of(new
            {
                model.Case, context.SelectedPreset, id, policy.Pin, authority, geometryPolicyVerified,
                geometry = string.IsNullOrWhiteSpace(geometryPolicyToken) ? "missing" : model.ParcelFingerprint(id, geometryPolicyToken),
                inclusion = model.InclusionHistory.Where(x => x.ParcelId == id),
                required = relevant.Select(x => x.ResolutionFingerprint).OrderBy(x => x, StringComparer.Ordinal),
                canonicalInputs = CurrentCanonicalInputs(relevant),
                reviewInputs = ReviewRecords(reviewInputs), evidence = EvidenceFingerprint(model, register, evidence),
                findings = ReviewRecords(register.Findings.Where(x => model.Relations is null && x.ParcelIds.Length == 0
                    || model.FindingParcels(x).Contains(id)).OrderBy(x => x.Id, StringComparer.Ordinal))
            });
            if (model.Relations is null) return baseline;
            var decisions = model.Relations.Dependencies.Nodes.Where(n => n.Kind == ToolboxDependencyKind.Decision
                    && ToolboxParcelModel.Same(n.DecisionParcelId ?? n.ReferenceId, id)).OrderBy(n => n.Id, StringComparer.Ordinal)
                .Select(n => model.Relations.Dependencies.ConsumerFingerprint(model, n.Kind, n.ReferenceId)).ToArray();
            return ToolboxSemanticHash.Of(new { version = "toolbox_parcel_decision_dependencies_v1", baseline, decisions });
        }
        object CurrentCanonicalInputs(IEnumerable<ToolboxResolvedReview> resolved) => resolved
            .Where(x => x.EffectiveRequirement != ToolboxReviewRequirement.NotApplicable)
            .OrderBy(x => x.ResolutionFingerprint, StringComparer.Ordinal).Select(x => new
            {
                x.ResolutionFingerprint, input = CanonicalInputFingerprint(model, x.ScopeKind, x.ScopeId,
                    reviews.FirstOrDefault(r => r.ResolutionFingerprint == x.ResolutionFingerprint)?.GeometryCheck ?? false)
            }).ToArray();
        object ReviewRecords(object value)
        {
            if (model.Relations is not null) return value;
            // B-only fields must not silently change A's v2 fingerprint contract.
            var node = JsonSerializer.SerializeToNode(value)!;
            void Remove(System.Text.Json.Nodes.JsonNode? current)
            {
                if (current is System.Text.Json.Nodes.JsonObject obj)
                {
                    obj.Remove("SourceFingerprint"); obj.Remove("DependencyFingerprint");
                    foreach (var child in obj.ToArray()) Remove(child.Value);
                }
                else if (current is System.Text.Json.Nodes.JsonArray array) foreach (var child in array) Remove(child);
            }
            Remove(node); return node;
        }
    }

    private static IEnumerable<(ToolboxScopeKind, string)> GlobalScopes(ToolboxParcelModel model, string name, ToolboxScopeKind kind) =>
        model.Source.GlobalData.TryGetProperty(name, out var obj) && obj.ValueKind == JsonValueKind.Object
            ? obj.EnumerateObject().Select(x => (kind, x.Name)).ToArray() : [];
    private static bool CanForward(ToolboxParcelModel model, ToolboxFinding finding, ToolboxResolvedReview required, ToolboxDispositionAuthority authority)
    {
        var last = finding.History[^1];
        return FindingInputsCurrent(model, finding) && last.Status != "resolved" && authority.Authorized && (finding.Origin == ToolboxFindingOrigin.SourceDefect
            || finding.Origin == ToolboxFindingOrigin.Mixed && finding.ExtractionCorrected)
            && last.Actor == authority.Actor && last.PolicyVersion == required.PolicyPin.PolicyVersion
            && last.RuleVersion == required.EvaluatorVersion && finding.RuleId == required.ItemId
            && last.InputFingerprint == required.InputFingerprint
            && (required.ScopeKind != ToolboxScopeKind.Parcel || model.Relations is null && finding.ParcelIds.Length == 0
                || model.FindingParcels(finding).Contains(required.ScopeId!));
    }

    private static IEnumerable<string> ReviewInputIssues(ToolboxParcelModel model, ToolboxExaminationRegister register,
        ToolboxResolvedReview required, ToolboxCheckReview review, string geometryPolicyToken, bool geometryPolicyVerified)
    {
        if (!Enum.IsDefined(review.Outcome) || string.IsNullOrWhiteSpace(review.Actor) || string.IsNullOrWhiteSpace(review.Reason)
            || review.EvidenceIds.IsDefaultOrEmpty || review.EvidenceIds.Any(id => string.IsNullOrWhiteSpace(id) || !model.Source.HasEvidence(id)))
        {
            yield return "Review evidence incomplete";
            yield break;
        }
        if (string.IsNullOrWhiteSpace(review.EvidenceFingerprint)
            || review.EvidenceFingerprint != EvidenceFingerprint(model, register, review.EvidenceIds))
            yield return "Consumed evidence changed";
        if (string.IsNullOrWhiteSpace(review.CanonicalInputFingerprint)
            || review.CanonicalInputFingerprint != CanonicalInputFingerprint(model, required.ScopeKind, required.ScopeId, review.GeometryCheck))
            yield return "Consumed canonical values changed or unbound";
        if (model.Relations is { } relations)
        {
            var dependency = relations.Dependencies.ConsumerFingerprint(model, ToolboxDependencyKind.Check, required.ResolutionFingerprint);
            if (dependency == "unknown" || review.DependencyFingerprint != dependency)
                yield return "Consumed dependency inputs changed or unregistered";
            var binding = relations.Dependencies.Nodes.FirstOrDefault(n => n.Kind == ToolboxDependencyKind.Check
                && ToolboxParcelModel.Same(n.ReferenceId, required.ResolutionFingerprint))?.CheckBinding;
            if (binding is null || binding.GeometryCheck != review.GeometryCheck)
                yield return "Check geometry binding mismatch";
        }
        if (review.GeometryCheck && (!geometryPolicyVerified || string.IsNullOrWhiteSpace(geometryPolicyToken)))
            yield return "Geometry policy inputs missing or unverified";
        if (review.GeometryCheck && (review.GeometryFingerprints is null || review.GeometryFingerprints.Count == 0))
            yield return "Geometry check has no dependency fingerprint";
        if (review.GeometryCheck && required.ScopeKind == ToolboxScopeKind.Parcel
            && review.GeometryFingerprints?.ContainsKey(required.ScopeId!) != true)
            yield return "Geometry check does not bind the examined parcel";
        foreach (var input in review.GeometryFingerprints ?? ImmutableDictionary<string, string>.Empty)
            if (!model.Parcels.Any(x => ToolboxParcelModel.Same(x.Id, input.Key)) || string.IsNullOrWhiteSpace(geometryPolicyToken)
                || input.Value != model.ParcelFingerprint(input.Key, geometryPolicyToken))
                yield return "Geometry check input changed";
        foreach (var evidenceId in review.EvidenceIds)
        {
            var human = register.HumanReviews.LastOrDefault(x => ToolboxParcelModel.Same(x.EvidenceId, evidenceId));
            if (human is null || human.InputRevision != required.RevisionId
                || human.Presence is ToolboxPresence.NotExtracted or ToolboxPresence.Uncertain or ToolboxPresence.NotReviewed
                || human.Accuracy is ToolboxAccuracy.Uncertain or ToolboxAccuracy.NotReviewed)
                yield return "Human presence/accuracy review missing or stale: " + evidenceId;
            if (human is not null && model.Relations is { } b)
            {
                var bound = human.SourceFingerprint.Length > 0 ? human.SourceFingerprint
                    : b.HumanInputBindings.GetValueOrDefault(ToolboxLineageValidator.CorrectionId(human));
                if (bound != HumanSourceFingerprint(model, evidenceId)) yield return "Human source validation changed: " + evidenceId;
            }
        }
    }

    private static bool HasCurrentResolution(ToolboxParcelModel model, ToolboxExaminationRegister register,
        ToolboxFinding finding, ImmutableArray<ToolboxResolvedReview> requirements,
        ImmutableArray<ToolboxCheckReview> reviews, ToolboxDispositionAuthority authority,
        string geometryPolicyToken, bool geometryPolicyVerified)
    {
        var last = finding.History[^1];
        var checks = requirements.Where(x => x.ItemId == finding.RuleId && x.EvaluatorVersion == last.RuleVersion
            && x.PolicyPin.PolicyVersion == last.PolicyVersion && x.InputFingerprint == last.InputFingerprint
            && x.Applicability == ToolboxApplicability.Applicable && x.Stage == last.Stage
            && x.EffectiveRequirement != ToolboxReviewRequirement.NotApplicable && x.EvaluatorId != "unresolved.binding").ToArray();
        bool Passed(ToolboxResolvedReview check) => reviews.Any(x => x.ResolutionFingerprint == check.ResolutionFingerprint
            && x.FindingId == finding.Id && x.Outcome == ToolboxCheckOutcome.Passed && !x.EvidenceIds.IsDefaultOrEmpty
            && x.EvidenceIds.Any(id => ToolboxParcelModel.Same(id, last.EvidenceId)) && (!finding.GeometryDefect || x.GeometryCheck)
            && !ReviewInputIssues(model, register, check, x, geometryPolicyToken, geometryPolicyVerified).Any());
        return FindingInputsCurrent(model, finding) && authority.Authorized && last.Actor == authority.Actor && last.PolicyVersion == authority.PolicyVersion
            && finding.Origin != ToolboxFindingOrigin.Unknown
            && (finding.Origin is not (ToolboxFindingOrigin.ExtractionMismatch or ToolboxFindingOrigin.Mixed) || finding.ExtractionCorrected)
            && checks.Length > 0 && (finding.ParcelIds.Length == 0
                && model.Relations?.Applicability.Any(x => ToolboxParcelModel.Same(x.RecordId, finding.Id)) != true
                ? checks.Any(x => x.ScopeKind is ToolboxScopeKind.Case or ToolboxScopeKind.Document && Passed(x))
                : model.FindingParcels(finding).Length > 0 && model.FindingParcels(finding).All(id => checks.Any(x => model.ParcelsForScope(x.ScopeKind, x.ScopeId).Contains(id) && Passed(x))));
    }
}
