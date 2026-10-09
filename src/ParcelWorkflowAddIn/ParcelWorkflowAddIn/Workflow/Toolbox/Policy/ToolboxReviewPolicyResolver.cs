using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

public static class ToolboxReviewPolicyResolver
{
    public static ImmutableArray<ToolboxResolvedReview> Resolve(ToolboxReviewPolicy policy, ToolboxPreset stage,
        IEnumerable<ToolboxPolicyScope> scopes)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(scopes);
        if (stage is not (ToolboxPreset.Compute or ToolboxPreset.Compare))
            throw new ToolboxPolicyException("No Compute/Compare policy for the selected preset.");
        var orderedScopes = scopes.ToArray();
        if (orderedScopes.Any(scope => scope is null)) throw new ArgumentException("Null scope.", nameof(scopes));
        if (orderedScopes.Select(scope => scope.CaseId).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            throw new ArgumentException("Cannot combine scopes from different cases.", nameof(scopes));
        if (orderedScopes.GroupBy(scope => (scope.Kind, scope.ScopeId.ToUpperInvariant())).Any(g => g.Count() > 1))
            throw new ArgumentException("Duplicate scoped identity.", nameof(scopes));
        orderedScopes = orderedScopes.OrderBy(scope => scope.Kind)
            .ThenBy(scope => scope.ScopeId, StringComparer.Ordinal).ToArray();
        var caseId = orderedScopes.FirstOrDefault()?.CaseId;
        var results = ImmutableArray.CreateBuilder<ToolboxResolvedReview>();
        foreach (var item in policy.Items)
        {
            var binding = item.Stages[stage];
            if (binding.Requirement == ToolboxReviewRequirement.NotApplicable)
            {
                results.Add(Result(item.ItemId, "stage_not_applicable", stage, ToolboxScopeKind.Case, null,
                    binding.Requirement, ToolboxApplicability.NotApplicable, "Not an independent check in this stage.",
                    "", "", ToolboxReviewEffect.Info, policy.Pin, caseId));
                continue;
            }
            foreach (var check in binding.Checks)
            {
                var matches = orderedScopes.Where(scope => scope.Kind == check.Scope).ToArray();
                if (matches.Length == 0)
                {
                    results.Add(Result(item.ItemId, check.CheckId, stage, check.Scope, null,
                        binding.Requirement, ToolboxApplicability.Unresolved,
                        "Required scope inventory is unavailable; not N/A.", check.EvaluatorId,
                        check.EvaluatorVersion, check.Effect, policy.Pin, caseId));
                    continue;
                }
                foreach (var scope in matches)
                {
                    var applicability = Evaluate(check.Applicability, scope.Facts);
                    var reason = applicability switch
                    {
                        ToolboxApplicability.Applicable => "Applicability predicate is true; review obligation retained.",
                        ToolboxApplicability.NotApplicable => "Applicability predicate is false for this scope.",
                        _ => "Applicability fact is unknown; not N/A."
                    };
                    results.Add(Result(item.ItemId, check.CheckId, stage, check.Scope, scope,
                        binding.Requirement, applicability, reason, check.EvaluatorId,
                        check.EvaluatorVersion, check.Effect, policy.Pin, caseId));
                }
            }
        }
        return results.ToImmutable();
    }

    private static ToolboxResolvedReview Result(string item, string check, ToolboxPreset stage,
        ToolboxScopeKind kind, ToolboxPolicyScope? scope, ToolboxReviewRequirement declared,
        ToolboxApplicability applicability, string reason, string evaluator, string evaluatorVersion,
        ToolboxReviewEffect effect, ToolboxPolicyPin pin, string? caseId)
    {
        ToolboxReviewRequirement? effective = applicability switch
        {
            ToolboxApplicability.Applicable => declared,
            ToolboxApplicability.NotApplicable => ToolboxReviewRequirement.NotApplicable,
            _ => null
        };
        var facts = scope?.Facts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var fingerprintBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            policy_id = pin.PolicyId, policy_version = pin.PolicyVersion, policy_sha256 = pin.Sha256,
            item_id = item, check_id = check, stage = stage.ToString(), scope_kind = kind.ToString(),
            case_id = caseId, scope_id = scope?.ScopeId, revision_id = scope?.RevisionId,
            input_fingerprint = scope?.InputFingerprint, applicability_facts = facts,
            applicability = applicability.ToString()
        });
        var fingerprint = Convert.ToHexString(SHA256.HashData(fingerprintBytes)).ToLowerInvariant();
        return new(item, check, stage, kind, caseId, scope?.ScopeId, declared, effective, applicability, reason,
            evaluator, evaluatorVersion, effect, pin, scope?.RevisionId, scope?.InputFingerprint, fingerprint);
    }

    private static ToolboxApplicability Evaluate(ToolboxPredicate predicate,
        ImmutableDictionary<string, JsonElement> facts)
    {
        if (predicate.Operator == "always") return ToolboxApplicability.Applicable;
        if (predicate.Operator == "never") return ToolboxApplicability.NotApplicable;
        if (predicate.Operator is "all" or "any" or "not")
        {
            var children = predicate.Children.Select(child => Evaluate(child, facts)).ToArray();
            if (predicate.Operator == "not")
                return children[0] switch
                {
                    ToolboxApplicability.Applicable => ToolboxApplicability.NotApplicable,
                    ToolboxApplicability.NotApplicable => ToolboxApplicability.Applicable,
                    _ => ToolboxApplicability.Unresolved
                };
            if (predicate.Operator == "all")
            {
                if (children.Contains(ToolboxApplicability.NotApplicable)) return ToolboxApplicability.NotApplicable;
                return children.Contains(ToolboxApplicability.Unresolved)
                    ? ToolboxApplicability.Unresolved : ToolboxApplicability.Applicable;
            }
            if (children.Contains(ToolboxApplicability.Applicable)) return ToolboxApplicability.Applicable;
            return children.Contains(ToolboxApplicability.Unresolved)
                ? ToolboxApplicability.Unresolved : ToolboxApplicability.NotApplicable;
        }
        if (!facts.TryGetValue(predicate.Fact!, out var actual)) return ToolboxApplicability.Unresolved;
        bool matches;
        if (predicate.Operator == "exists") matches = true;
        else if (predicate.Operator == "equals") matches = Equal(actual, predicate.Value!.Value);
        else if (predicate.Operator == "in")
            matches = predicate.Value!.Value.EnumerateArray().Any(value => Equal(actual, value));
        else
        {
            var comparison = actual.GetDecimal().CompareTo(predicate.Value!.Value.GetDecimal());
            matches = predicate.Operator switch
            {
                "gt" => comparison > 0, "gte" => comparison >= 0,
                "lt" => comparison < 0, "lte" => comparison <= 0,
                _ => throw new ToolboxPolicyException("Unsupported validated operator.")
            };
        }
        return matches ? ToolboxApplicability.Applicable : ToolboxApplicability.NotApplicable;
    }

    private static bool Equal(JsonElement left, JsonElement right) => left.ValueKind switch
    {
        JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.OrdinalIgnoreCase),
        JsonValueKind.Number => left.GetDecimal() == right.GetDecimal(),
        JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
        _ => false
    };
}
