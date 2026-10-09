using System.Collections.Immutable;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

public enum ToolboxReviewRequirement { Required, Optional, NotApplicable }
public enum ToolboxApplicability { Applicable, NotApplicable, Unresolved }
public enum ToolboxFactType { Boolean, String, Number }
public enum ToolboxScopeKind { Case, Document, Parcel, Participant, Boundary, IdentifierPair }
public enum ToolboxReviewEffect { Info, ReportOnly, RequiresDisposition }

public sealed record ToolboxPolicyPin(string PolicyId, string PolicyVersion, string Sha256);

public sealed record ToolboxPredicate
{
    internal ToolboxPredicate(string op, string? fact, JsonElement? value, ImmutableArray<ToolboxPredicate> children)
    {
        Operator = op; Fact = fact; Value = value; Children = children;
    }

    public string Operator { get; }
    public string? Fact { get; }
    public JsonElement? Value { get; }
    public ImmutableArray<ToolboxPredicate> Children { get; }
}

public sealed record ToolboxPolicyCheck
{
    internal ToolboxPolicyCheck(string checkId, ToolboxScopeKind scope, ToolboxPredicate applicability,
        string evaluatorId, string evaluatorVersion, ToolboxReviewEffect effect)
    {
        CheckId = checkId; Scope = scope; Applicability = applicability;
        EvaluatorId = evaluatorId; EvaluatorVersion = evaluatorVersion; Effect = effect;
    }

    public string CheckId { get; }
    public ToolboxScopeKind Scope { get; }
    public ToolboxPredicate Applicability { get; }
    public string EvaluatorId { get; }
    public string EvaluatorVersion { get; }
    public ToolboxReviewEffect Effect { get; }
}

public sealed record ToolboxPolicyStage(ToolboxReviewRequirement Requirement, ImmutableArray<ToolboxPolicyCheck> Checks);
public sealed record ToolboxPolicyItem(string ItemId, string DisplayName,
    ImmutableDictionary<ToolboxPreset, ToolboxPolicyStage> Stages);

public sealed class ToolboxReviewPolicy
{
    internal ToolboxReviewPolicy(ToolboxPolicyPin pin, string snapshot, ImmutableArray<ToolboxPolicyItem> items)
    {
        Pin = pin; JsonSnapshot = snapshot; Items = items;
    }

    public ToolboxPolicyPin Pin { get; }
    public string JsonSnapshot { get; }
    public ImmutableArray<ToolboxPolicyItem> Items { get; }
    public string BaselineId => "compute-compare-v1";
}

public sealed class ToolboxPolicyException : Exception
{
    public ToolboxPolicyException(string message) : base(message) { }
}

// Only applicability facts belong here; missing values/service availability are deliberately excluded.
public static class ToolboxPolicyFactCatalog
{
    public static ImmutableDictionary<string, ToolboxFactType> Types { get; } =
        new Dictionary<string, ToolboxFactType>(StringComparer.Ordinal)
        {
            ["route.mode"] = ToolboxFactType.String,
            ["document.memorandum_applicable"] = ToolboxFactType.Boolean,
            ["document.dwg_in_use"] = ToolboxFactType.Boolean,
            ["document.computation_sheet_applicable"] = ToolboxFactType.Boolean,
            ["geometry.recipe"] = ToolboxFactType.String,
            ["geometry.closed_ring_applicable"] = ToolboxFactType.Boolean,
            ["scope.parcel_count"] = ToolboxFactType.Number,
            ["comparison.address_applicable"] = ToolboxFactType.Boolean,
            ["comparison.landval_applicable"] = ToolboxFactType.Boolean,
            ["comparison.property_name_applicable"] = ToolboxFactType.Boolean
        }.ToImmutableDictionary(StringComparer.Ordinal);

    internal static bool Matches(ToolboxFactType type, JsonElement value) => type switch
    {
        ToolboxFactType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        ToolboxFactType.String => value.ValueKind == JsonValueKind.String,
        ToolboxFactType.Number => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
        _ => false
    };
}

public sealed class ToolboxPolicyScope
{
    public ToolboxPolicyScope(string caseId, ToolboxScopeKind kind, string scopeId, string revisionId, string inputFingerprint,
        IReadOnlyDictionary<string, JsonElement>? facts = null)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentException("Unknown scope kind.", nameof(kind));
        Kind = kind;
        CaseId = Required(caseId);
        ScopeId = Required(scopeId); RevisionId = Required(revisionId); InputFingerprint = Required(inputFingerprint);
        if (kind == ToolboxScopeKind.Case && !string.Equals(CaseId, ScopeId, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Case scope must use its case identity.", nameof(scopeId));
        var builder = ImmutableDictionary.CreateBuilder<string, JsonElement>(StringComparer.Ordinal);
        foreach (var pair in facts ?? ImmutableDictionary<string, JsonElement>.Empty)
        {
            if (!ToolboxPolicyFactCatalog.Types.TryGetValue(pair.Key, out var type)
                || !ToolboxPolicyFactCatalog.Matches(type, pair.Value))
                throw new ArgumentException("Unknown fact or invalid fact type: " + pair.Key, nameof(facts));
            if (pair.Key == "scope.parcel_count"
                && (pair.Value.GetDecimal() < 0 || decimal.Truncate(pair.Value.GetDecimal()) != pair.Value.GetDecimal()))
                throw new ArgumentException("Parcel count must be a nonnegative integer.", nameof(facts));
            builder.Add(pair.Key, pair.Value.Clone());
        }
        Facts = builder.ToImmutable();
    }

    public ToolboxScopeKind Kind { get; }
    public string CaseId { get; }
    public string ScopeId { get; }
    public string RevisionId { get; }
    public string InputFingerprint { get; }
    public ImmutableDictionary<string, JsonElement> Facts { get; }

    private static string Required(string value) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException("A nonblank scope/revision/fingerprint is required.") : value.Trim();
}

public sealed record ToolboxResolvedReview(
    string ItemId, string CheckId, ToolboxPreset Stage, ToolboxScopeKind ScopeKind, string? CaseId, string? ScopeId,
    ToolboxReviewRequirement DeclaredRequirement, ToolboxReviewRequirement? EffectiveRequirement,
    ToolboxApplicability Applicability, string Reason, string EvaluatorId, string EvaluatorVersion,
    ToolboxReviewEffect Effect, ToolboxPolicyPin PolicyPin, string? RevisionId, string? InputFingerprint,
    string ResolutionFingerprint);
