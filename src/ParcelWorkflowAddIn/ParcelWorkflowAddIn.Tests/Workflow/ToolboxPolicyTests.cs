using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.Workflow.Toolbox;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class ToolboxPolicyTests
{
    private const string Compute = "RRRRRRRRRRRRRRRRORRRRRRRRRRRRRRRRRRRRNORRR";
    private const string Compare = "RRNORRROROOOOORRNRRRRRROROOOOONROROONRRRRR";

    internal static JsonObject Fixture()
    {
        var items = new JsonArray();
        for (var i = 0; i < 42; i++)
            items.Add(new JsonObject
            {
                ["item_id"] = $"M{i + 1:00}", ["display_name"] = $"Synthetic item {i + 1}",
                ["stages"] = new JsonObject
                {
                    ["Compute"] = Stage(Compute[i]), ["Compare"] = Stage(Compare[i])
                }
            });
        return new JsonObject
        {
            ["schema_version"] = ToolboxReviewPolicyLoader.SchemaVersion,
            ["policy_id"] = "synthetic-toolbox-policy", ["policy_version"] = "test-v1",
            ["activation_status"] = "isolated_test", ["classification_baseline"] = "compute-compare-v1",
            ["supported_stages"] = new JsonArray("Compute", "Compare"),
            ["preset_overrides"] = new JsonArray(), ["items"] = items
        };
    }

    private static JsonObject Stage(char requirement) => new()
    {
        ["requirement"] = requirement switch { 'R' => "required", 'O' => "optional", _ => "not_applicable" },
        ["checks"] = requirement == 'N' ? new JsonArray() : new JsonArray(new JsonObject
        {
            ["check_id"] = "review", ["scope"] = "case",
            ["applicability"] = new JsonObject { ["op"] = "always" },
            ["evaluator_id"] = "synthetic-manual-review", ["evaluator_version"] = "test-v1",
            ["effect"] = "requires_disposition"
        })
    };

    private static JsonObject Binding(JsonObject fixture, int index = 0, string stage = "Compute") =>
        (JsonObject)fixture["items"]![index]!["stages"]![stage]!["checks"]![0]!;

    private static JsonObject Predicate(string op, string fact, object? value = null)
    {
        var node = new JsonObject { ["op"] = op, ["fact"] = fact };
        if (op != "exists") node["value"] = JsonSerializer.SerializeToNode(value);
        return node;
    }

    private static JsonObject Compound(string op, params JsonObject[] args) =>
        new() { ["op"] = op, ["args"] = new JsonArray(args.Select(a => (JsonNode?)a).ToArray()) };

    private static byte[] Bytes(JsonObject fixture) => Encoding.UTF8.GetBytes(fixture.ToJsonString());
    private static ToolboxPolicyPin Pin(byte[] bytes, string version = "test-v1") =>
        new("synthetic-toolbox-policy", version, Convert.ToHexString(SHA256.HashData(bytes)));
    private static ToolboxReviewPolicy Load(JsonObject? fixture = null)
    {
        var bytes = Bytes(fixture ?? Fixture());
        return ToolboxReviewPolicyLoader.Load(bytes, Pin(bytes));
    }

    private static ToolboxPolicyScope Scope(string id = "case-a", ToolboxScopeKind kind = ToolboxScopeKind.Case,
        IReadOnlyDictionary<string, JsonElement>? facts = null, string revision = "revision-1", string input = "inputs-1") =>
        new(kind == ToolboxScopeKind.Case ? id : "case-a", kind, id, revision, input, facts);

    private static Dictionary<string, JsonElement> Facts(params (string Key, object Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value));

    private static ToolboxResolvedReview First(JsonObject fixture, Dictionary<string, JsonElement>? facts = null) =>
        ToolboxReviewPolicyResolver.Resolve(Load(fixture), ToolboxPreset.Compute, new[] { Scope(facts: facts) })[0];

    private static void Reject(Action action)
    {
        try { action(); }
        catch (ToolboxPolicyException) { return; }
        throw new InvalidOperationException("Expected policy rejection.");
    }

    private static void RejectArgument(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Expected argument rejection.");
    }

    public static void ValidBaselineAndSnapshotsAreImmutable()
    {
        var bytes = Bytes(Fixture());
        var policy = ToolboxReviewPolicyLoader.Load(bytes, Pin(bytes));
        var original = policy.JsonSnapshot;
        bytes[0] = 0;
        TestAssert.Equal(original, policy.JsonSnapshot, "Own validated snapshot.");
        TestAssert.Equal(42, policy.Items.Length, "Every baseline item.");
        TestAssert.Equal(39, policy.Items.Count(i => i.Stages[ToolboxPreset.Compute].Requirement == ToolboxReviewRequirement.Required), "Compute required.");
        TestAssert.Equal(22, policy.Items.Count(i => i.Stages[ToolboxPreset.Compare].Requirement == ToolboxReviewRequirement.Required), "Compare required.");
        TestAssert.Equal(Pin(Encoding.UTF8.GetBytes(original)).Sha256.ToLowerInvariant(), policy.Pin.Sha256, "Exact byte pin.");
    }

    public static void InvalidJsonAndDuplicatePropertiesAreRejected()
    {
        foreach (var json in new[] { "{", "{\"schema_version\":\"a\",\"schema_version\":\"b\"}", "[]" })
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            Reject(() => ToolboxReviewPolicyLoader.Load(bytes, Pin(bytes)));
        }
        var fixture = Fixture();
        fixture["unexpected"] = true;
        Reject(() => Load(fixture));
        fixture = Fixture();
        Binding(fixture)["unexpected"] = true;
        Reject(() => Load(fixture));
        Reject(() => ToolboxReviewPolicyLoader.Load(new byte[ToolboxReviewPolicyLoader.MaxBytes + 1], Pin(new byte[1])));
    }

    public static void SchemaActivationAndPinAreStrict()
    {
        foreach (var pair in new[] { ("schema_version", "unknown"), ("activation_status", "published"),
            ("activation_status", "draft_not_activatable"), ("classification_baseline", "other") })
        {
            var fixture = Fixture(); fixture[pair.Item1] = pair.Item2;
            Reject(() => Load(fixture));
        }
        var bytes = Bytes(Fixture());
        Reject(() => ToolboxReviewPolicyLoader.Load(bytes, Pin(bytes) with { Sha256 = new string('0', 64) }));
        Reject(() => ToolboxReviewPolicyLoader.Load(bytes, Pin(bytes, "different-version")));
        Reject(() => ToolboxReviewPolicyLoader.Load(bytes, Pin(bytes) with { PolicyId = "other" }));
        Reject(() => ToolboxReviewPolicyLoader.Load(bytes, Pin(bytes) with { Sha256 = "invalid" }));
    }

    public static void MissingDuplicateAndChangedBaselineItemsAreRejected()
    {
        var fixture = Fixture(); ((JsonArray)fixture["items"]!).RemoveAt(41); Reject(() => Load(fixture));
        fixture = Fixture(); fixture["items"]![1]!["item_id"] = "M01"; Reject(() => Load(fixture));
        fixture = Fixture(); fixture["items"]![0]!["item_id"] = "m01"; Reject(() => Load(fixture));
        fixture = Fixture(); fixture["items"]![0]!["stages"]!["Compute"]!["requirement"] = "optional"; Reject(() => Load(fixture));
        fixture = Fixture(); ((JsonObject)fixture["items"]![0]!["stages"]!).Remove("Compare"); Reject(() => Load(fixture));
        fixture = Fixture(); fixture["supported_stages"] = new JsonArray("Compute", "Compute"); Reject(() => Load(fixture));
    }

    public static void OverridesAndIncompleteBindingsAreRejected()
    {
        var fixture = Fixture(); ((JsonArray)fixture["preset_overrides"]!).Add(new JsonObject()); Reject(() => Load(fixture));
        fixture = Fixture(); fixture["items"]![0]!["stages"]!["Compute"]!["checks"] = new JsonArray(); Reject(() => Load(fixture));
        fixture = Fixture(); Binding(fixture)["scope"] = "unknown"; Reject(() => Load(fixture));
        fixture = Fixture(); Binding(fixture)["effect"] = "allow_handoff"; Reject(() => Load(fixture));
        fixture = Fixture(); Binding(fixture)["evaluator_version"] = ""; Reject(() => Load(fixture));
        fixture = Fixture();
        var checks = (JsonArray)fixture["items"]![0]!["stages"]!["Compute"]!["checks"]!;
        checks.Add(checks[0]!.DeepClone()); Reject(() => Load(fixture));
        fixture = Fixture(); fixture["items"]![2]!["stages"]!["Compare"]!["checks"] = new JsonArray(Binding(fixture).DeepClone());
        Reject(() => Load(fixture));
    }

    public static void PredicateTypesAllowlistAndBoundsAreStrict()
    {
        var predicates = new[]
        {
            Predicate("equals", "ltd.service_available", false),
            Predicate("equals", "document.memorandum_applicable", "yes"),
            Predicate("gt", "route.mode", "manual"),
            Predicate("exec", "route.mode", "manual"),
            Predicate("in", "scope.parcel_count", Array.Empty<int>()),
            Compound("not", new JsonObject { ["op"] = "always" }, new JsonObject { ["op"] = "always" }),
            Compound("all")
        };
        foreach (var predicate in predicates)
        {
            var fixture = Fixture(); Binding(fixture)["applicability"] = predicate; Reject(() => Load(fixture));
        }
        JsonObject deep = new() { ["op"] = "always" };
        for (var i = 0; i < 10; i++) deep = Compound("not", deep);
        var nested = Fixture(); Binding(nested)["applicability"] = deep; Reject(() => Load(nested));
    }

    public static void ScopeFactsAreTypedAndDefensive()
    {
        var facts = Facts(("route.mode", "manual"));
        var scope = Scope(facts: facts);
        facts["route.mode"] = JsonSerializer.SerializeToElement("automated");
        TestAssert.Equal("manual", scope.Facts["route.mode"].GetString(), "Own facts.");
        RejectArgument(() => Scope(facts: Facts(("source.owner_present", false))));
        RejectArgument(() => Scope(facts: Facts(("scope.parcel_count", "three"))));
        RejectArgument(() => Scope(facts: Facts(("scope.parcel_count", -1))));
        RejectArgument(() => Scope(facts: Facts(("scope.parcel_count", 1.5))));
        RejectArgument(() => Scope(id: " "));
        RejectArgument(() => Scope(revision: ""));
        RejectArgument(() => Scope(input: ""));
        RejectArgument(() => Scope(kind: (ToolboxScopeKind)999));
    }

    public static void UnknownNeverBecomesNotApplicable()
    {
        var fixture = Fixture();
        Binding(fixture)["applicability"] = Predicate("equals", "document.memorandum_applicable", true);
        var unknown = First(fixture);
        TestAssert.Equal(ToolboxApplicability.Unresolved, unknown.Applicability, "Missing applicability fact unknown.");
        TestAssert.Equal<ToolboxReviewRequirement?>(null, unknown.EffectiveRequirement, "No fake N/A.");
        var absent = First(fixture, Facts(("document.memorandum_applicable", false)));
        TestAssert.Equal(ToolboxApplicability.NotApplicable, absent.Applicability, "Explicit inapplicability.");
        TestAssert.Equal(ToolboxReviewRequirement.NotApplicable, absent.EffectiveRequirement, "N/A with reason.");
        var present = First(fixture, Facts(("document.memorandum_applicable", true)));
        TestAssert.Equal(ToolboxReviewRequirement.Required, present.EffectiveRequirement, "Required review retained.");
        TestAssert.True(present.Reason.Length > 0 && absent.Reason.Length > 0, "Explain resolution.");
    }

    public static void LogicalOperatorsUseThreeValuedSemantics()
    {
        var unknown = Predicate("equals", "document.memorandum_applicable", true);
        foreach (var entry in new[]
        {
            (Compound("all", unknown.DeepClone().AsObject(), new JsonObject { ["op"] = "never" }), ToolboxApplicability.NotApplicable),
            (Compound("any", unknown.DeepClone().AsObject(), new JsonObject { ["op"] = "always" }), ToolboxApplicability.Applicable),
            (Compound("not", unknown.DeepClone().AsObject()), ToolboxApplicability.Unresolved),
            (Compound("all", unknown.DeepClone().AsObject(), new JsonObject { ["op"] = "always" }), ToolboxApplicability.Unresolved),
            (Compound("any", unknown.DeepClone().AsObject(), new JsonObject { ["op"] = "never" }), ToolboxApplicability.Unresolved)
        })
        {
            var fixture = Fixture(); Binding(fixture)["applicability"] = entry.Item1;
            TestAssert.Equal(entry.Item2, First(fixture).Applicability, "Three-valued result.");
        }
    }

    public static void ScalarOperatorsAreDeterministic()
    {
        foreach (var entry in new[]
        {
            (Predicate("equals", "route.mode", "MANUAL"), Facts(("route.mode", "manual")), true),
            (Predicate("in", "geometry.recipe", new[] { "points", "segments" }), Facts(("geometry.recipe", "segments")), true),
            (Predicate("gt", "scope.parcel_count", 1), Facts(("scope.parcel_count", 3)), true),
            (Predicate("gte", "scope.parcel_count", 3), Facts(("scope.parcel_count", 3)), true),
            (Predicate("lt", "scope.parcel_count", 3), Facts(("scope.parcel_count", 3)), false),
            (Predicate("lte", "scope.parcel_count", 3), Facts(("scope.parcel_count", 3)), true),
            (Predicate("exists", "document.memorandum_applicable"), Facts(("document.memorandum_applicable", false)), true)
        })
        {
            var fixture = Fixture(); Binding(fixture)["applicability"] = entry.Item1;
            TestAssert.Equal(entry.Item3 ? ToolboxApplicability.Applicable : ToolboxApplicability.NotApplicable,
                First(fixture, entry.Item2).Applicability, "Typed operator.");
        }
        var missing = Fixture(); Binding(missing)["applicability"] = Predicate("exists", "route.mode");
        TestAssert.Equal(ToolboxApplicability.Unresolved, First(missing).Applicability, "exists does not infer false from unavailable fact.");
    }

    public static void EveryParcelParticipantAndPairIsResolved()
    {
        var fixture = Fixture();
        Binding(fixture, 8)["scope"] = "parcel";
        Binding(fixture, 17)["scope"] = "participant";
        Binding(fixture, 19)["scope"] = "identifier_pair";
        var scopes = new[]
        {
            Scope(), Scope("parcel-a", ToolboxScopeKind.Parcel), Scope("parcel-b", ToolboxScopeKind.Parcel),
            Scope("parcel-c", ToolboxScopeKind.Parcel), Scope("owner-a", ToolboxScopeKind.Participant),
            Scope("owner-b", ToolboxScopeKind.Participant), Scope("pair-a", ToolboxScopeKind.IdentifierPair),
            Scope("pair-b", ToolboxScopeKind.IdentifierPair)
        };
        var policy = Load(fixture);
        var result = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute, scopes);
        TestAssert.Equal(3, result.Count(r => r.ItemId == "M09"), "All three parcels.");
        TestAssert.Equal(2, result.Count(r => r.ItemId == "M18"), "All owners.");
        TestAssert.Equal(2, result.Count(r => r.ItemId == "M20"), "All pairs.");
        var reversed = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute, scopes.Reverse());
        TestAssert.True(result.SequenceEqual(reversed), "Input enumeration order does not change results.");
    }

    public static void MissingScopesAndDuplicatesCannotSilentlyComplete()
    {
        var fixture = Fixture(); Binding(fixture, 8)["scope"] = "parcel";
        var policy = Load(fixture);
        var result = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute, new[] { Scope() });
        var parcel = result.Single(r => r.ItemId == "M09");
        TestAssert.Equal(ToolboxApplicability.Unresolved, parcel.Applicability, "No parcel inventory remains unresolved.");
        TestAssert.Equal<string?>(null, parcel.ScopeId, "Do not fabricate scope.");
        RejectArgument(() => ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute,
            new[] { Scope("case-a"), Scope("CASE-A") }));
        var empty = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute, Array.Empty<ToolboxPolicyScope>());
        TestAssert.True(empty.Any(r => r.Applicability == ToolboxApplicability.Unresolved), "Empty inventory is not success.");
    }

    public static void RequiredReviewAndReportOnlyEffectRemainSeparate()
    {
        var fixture = Fixture(); Binding(fixture, 34)["effect"] = "report_only";
        var policy = Load(fixture);
        var result = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute, new[] { Scope() });
        var north = result.Single(r => r.ItemId == "M35");
        TestAssert.Equal(ToolboxReviewRequirement.Required, north.EffectiveRequirement, "Required review, not required source presence.");
        TestAssert.Equal(ToolboxReviewEffect.ReportOnly, north.Effect, "Do not promote effect to blocker.");
        TestAssert.Equal(ToolboxReviewRequirement.Optional, result.Single(r => r.ItemId == "M17").EffectiveRequirement, "Manual tool remains alternative.");
        // Outcome/authority and unavailable LTD data are not applicability facts or resolver decisions.
        var compare = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compare, new[] { Scope() });
        TestAssert.Equal(ToolboxReviewRequirement.Required, compare.Single(r => r.ItemId == "M38").EffectiveRequirement, "Required legal comparison cannot disappear.");
        TestAssert.Equal(ToolboxReviewRequirement.Required, compare.Single(r => r.ItemId == "M41").EffectiveRequirement, "Inherited findings reviewed.");
    }

    public static void CompareAndUnsupportedStagesStayIsolated()
    {
        var policy = Load();
        foreach (var stage in new[] { ToolboxPreset.PlaA, ToolboxPreset.PlaB, ToolboxPreset.Rt, (ToolboxPreset)999 })
            Reject(() => ToolboxReviewPolicyResolver.Resolve(policy, stage, new[] { Scope() }));
        var result = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compare, new[] { Scope() });
        TestAssert.Equal(ToolboxReviewRequirement.NotApplicable, result.Single(r => r.ItemId == "M17").EffectiveRequirement, "No manual COGO in Compare.");
        TestAssert.Equal(ToolboxReviewRequirement.NotApplicable, result.Single(r => r.ItemId == "M31").EffectiveRequirement, "No repeat detection.");
        TestAssert.Equal(ToolboxReviewRequirement.Optional, result.Single(r => r.ItemId == "M12").EffectiveRequirement, "Closure recheck optional.");
        TestAssert.Equal(42, result.Length, "All baseline items represented.");
    }

    public static void FingerprintsBindPolicyScopeFactsAndRevision()
    {
        var policy = Load();
        ToolboxResolvedReview Resolve(ToolboxReviewPolicy p, ToolboxPolicyScope scope) =>
            ToolboxReviewPolicyResolver.Resolve(p, ToolboxPreset.Compute, new[] { scope })[0];
        var first = Resolve(policy, Scope(facts: Facts(("route.mode", "manual"), ("scope.parcel_count", 3))));
        var reordered = Resolve(policy, Scope(facts: Facts(("scope.parcel_count", 3), ("route.mode", "manual"))));
        TestAssert.Equal(first.ResolutionFingerprint, reordered.ResolutionFingerprint, "Canonical fact order.");
        foreach (var scope in new[] { Scope(revision: "revision-2"), Scope(input: "inputs-2"), Scope("case-b"),
            Scope(facts: Facts(("route.mode", "automated"))) })
            TestAssert.False(first.ResolutionFingerprint == Resolve(policy, scope).ResolutionFingerprint, "Changed input stales resolution.");
        var changed = Fixture(); changed["policy_version"] = "test-v2";
        var bytes = Bytes(changed);
        var next = ToolboxReviewPolicyLoader.Load(bytes, Pin(bytes, "test-v2"));
        TestAssert.False(first.ResolutionFingerprint == Resolve(next, Scope()).ResolutionFingerprint, "Changed policy stales resolution.");
        TestAssert.Equal("test-v1", policy.Pin.PolicyVersion, "Old immutable policy remains unchanged.");
    }

    public static void ExplicitFileLoadNeverFallsBack()
    {
        var bytes = Bytes(Fixture()); var pin = Pin(bytes);
        Reject(() => ToolboxReviewPolicyLoader.LoadFileAsync("relative.json", pin).GetAwaiter().GetResult());
        var root = Path.Combine(Path.GetTempPath(), "toolbox-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "policy.json");
            try
            {
                ToolboxReviewPolicyLoader.LoadFileAsync(path, pin).GetAwaiter().GetResult();
                throw new InvalidOperationException("Missing file must not fall back.");
            }
            catch (FileNotFoundException) { }
            File.WriteAllBytes(path, bytes);
            var before = File.ReadAllBytes(path);
            var policy = ToolboxReviewPolicyLoader.LoadFileAsync(path, pin).GetAwaiter().GetResult();
            TestAssert.Equal(42, policy.Items.Length, "Load explicit file.");
            TestAssert.True(before.SequenceEqual(File.ReadAllBytes(path)), "Loader never writes settings.");
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            try
            {
                ToolboxReviewPolicyLoader.LoadFileAsync(path, pin, canceled.Token).GetAwaiter().GetResult();
                throw new InvalidOperationException("Canceled load must stop.");
            }
            catch (OperationCanceledException) { }
        }
        finally { Directory.Delete(root, true); }
    }

    public static void MixedCaseScopesAndInvalidCaseIdentityAreRejected()
    {
        RejectArgument(() => new ToolboxPolicyScope("case-a", ToolboxScopeKind.Case, "case-b", "r1", "i1"));
        RejectArgument(() => new ToolboxPolicyScope("", ToolboxScopeKind.Parcel, "parcel-a", "r1", "i1"));
        var other = new ToolboxPolicyScope("case-b", ToolboxScopeKind.Parcel, "parcel-a", "r1", "i1");
        RejectArgument(() => ToolboxReviewPolicyResolver.Resolve(Load(), ToolboxPreset.Compute,
            new[] { Scope(), other }));
        var result = ToolboxReviewPolicyResolver.Resolve(Load(), ToolboxPreset.Compute, new[] { Scope() });
        TestAssert.Equal("case-a", result[0].CaseId, "Resolved provenance binds the case.");
    }

    public static void SharedBoundarySubchecksRespectSingleAndMultiParcelScope()
    {
        var fixture = Fixture();
        var duplicate = Binding(fixture, 13);
        duplicate["check_id"] = "duplicate_edge";
        duplicate["scope"] = "parcel";
        var shared = duplicate.DeepClone().AsObject();
        shared["check_id"] = "shared_edge";
        shared["applicability"] = Predicate("gt", "scope.parcel_count", 1);
        ((JsonArray)fixture["items"]![13]!["stages"]!["Compute"]!["checks"]!).Add(shared);
        var policy = Load(fixture);
        var single = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute,
            new[] { Scope(), Scope("parcel-a", ToolboxScopeKind.Parcel, Facts(("scope.parcel_count", 1))) });
        TestAssert.Equal(ToolboxReviewRequirement.Required,
            single.Single(r => r.ItemId == "M14" && r.CheckId == "duplicate_edge").EffectiveRequirement,
            "Single-parcel duplicate review remains required.");
        TestAssert.Equal(ToolboxReviewRequirement.NotApplicable,
            single.Single(r => r.ItemId == "M14" && r.CheckId == "shared_edge").EffectiveRequirement,
            "Single-parcel cross-parcel subcheck N/A, not parent item.");
        var multi = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute, new[]
        {
            Scope(), Scope("parcel-a", ToolboxScopeKind.Parcel, Facts(("scope.parcel_count", 3))),
            Scope("parcel-b", ToolboxScopeKind.Parcel, Facts(("scope.parcel_count", 3))),
            Scope("parcel-c", ToolboxScopeKind.Parcel, Facts(("scope.parcel_count", 3)))
        });
        TestAssert.Equal(6, multi.Count(r => r.ItemId == "M14"), "Both checks for all three parcels.");
        TestAssert.True(multi.Where(r => r.ItemId == "M14").All(r => r.EffectiveRequirement == ToolboxReviewRequirement.Required),
            "All applicable subchecks required.");
    }

    public static void ManualAndPdfRoutesDoNotRequireUnrelatedAutomatedChecks()
    {
        var fixture = Fixture();
        Binding(fixture, 2)["applicability"] = Predicate("equals", "route.mode", "automated");
        Binding(fixture, 3)["applicability"] = Predicate("equals", "document.dwg_in_use", true);
        var policy = Load(fixture);
        var manual = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute,
            new[] { Scope(facts: Facts(("route.mode", "manual"), ("document.dwg_in_use", false))) });
        TestAssert.Equal(ToolboxReviewRequirement.NotApplicable, manual.Single(r => r.ItemId == "M03").EffectiveRequirement,
            "Manual route does not need script-plan resolution.");
        TestAssert.Equal(ToolboxReviewRequirement.NotApplicable, manual.Single(r => r.ItemId == "M04").EffectiveRequirement,
            "PDF-only route does not need CAD layers.");
        TestAssert.Equal(ToolboxReviewRequirement.Required, manual.Single(r => r.ItemId == "M40").EffectiveRequirement,
            "Human validation remains required.");
        var automated = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute,
            new[] { Scope(facts: Facts(("route.mode", "automated"), ("document.dwg_in_use", true))) });
        TestAssert.Equal(ToolboxReviewRequirement.Required, automated.Single(r => r.ItemId == "M03").EffectiveRequirement,
            "Automated plan resolution required.");
        TestAssert.Equal(ToolboxReviewRequirement.Required, automated.Single(r => r.ItemId == "M04").EffectiveRequirement,
            "Declared DWG use required.");
    }
}
