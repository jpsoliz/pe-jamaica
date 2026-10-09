using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow.Review;
using ParcelWorkflowAddIn.Workflow.Toolbox;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class ToolboxBindingFixtureTests
{
    private static readonly string[] Bundles = { "single-parcel", "multi-parcel-cad-map", "manual-geometry-recovery" };

    private static JsonObject Read(string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "ToolboxBindings", name)))!.AsObject();
    private static JsonObject Catalog() => Read("binding-catalog.v1.json");
    private static JsonObject Bundle(string name) => Read(name + ".bundle.json");
    private static string Text(JsonNode node, string key) => node[key]!.GetValue<string>();
    private static JsonArray Array(JsonNode node, string key) => node[key]!.AsArray();
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Expected invalid fixture/catalog rejection.");
    }

    private static string WorkspaceRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn.sln")))
                return dir.FullName;
        throw new InvalidOperationException("Fixture source checks require the isolated checkout.");
    }

    private static JsonNode Select(JsonNode root, string selector, string scopeId)
    {
        var path = selector.Replace("{scope_id}", scopeId, StringComparison.Ordinal);
        Require(path.StartsWith('/'), "Absolute bundle JSON pointer required.");
        var current = root;
        foreach (var part in path.Split('/').Skip(1))
        {
            var token = part.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            current = (current is JsonArray list && int.TryParse(token, out var index)
                ? list[index] : current[token])
                ?? throw new InvalidDataException("Unresolved evidence selector: " + path);
        }
        return current!;
    }

    private static void ValidateCatalog(JsonObject catalog)
    {
        Require(Text(catalog, "schema_version") == "toolbox_binding_catalog_fixture_v1", "Catalog schema.");
        Require(!catalog["production_ready"]!.GetValue<bool>(), "Fixture catalog must not publish.");
        var items = Array(catalog, "items");
        Require(items.Count == 42, "All 42 items required.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items.Cast<JsonObject>())
        {
            Require(ids.Add(Text(item, "item_id")), "Duplicate item.");
            foreach (var stage in new[] { "Compute", "Compare" })
            {
                var entry = item["stages"]![stage]!;
                var status = Text(entry, "binding_status");
                var checks = Array(entry, "checks");
                Require(status is "fixture_bound" or "contract_snapshot_only" or "unresolved" or "stage_not_applicable",
                    "Unknown binding state.");
                Require(status is "unresolved" or "stage_not_applicable" ? checks.Count == 0 : checks.Count > 0,
                    "Bindings must be explicit, including unresolved entries.");
                Require((Text(entry, "requirement") == "not_applicable") == (status == "stage_not_applicable"),
                    "Unresolved binding must not erase an obligation.");
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var check in checks.Cast<JsonObject>())
                {
                    Require(seen.Add(Text(check, "check_id")), "Duplicate scoped check.");
                    Require(Text(check, "runtime_adapter_status") == "not_connected", "No application adapter activation.");
                    Require(Array(check, "reuse_sources").Count > 0, "Source mapping required.");
                    foreach (var source in Array(check, "reuse_sources").Cast<JsonObject>())
                    {
                        var relative = Text(source, "path");
                        Require(!Path.IsPathFullyQualified(relative) && !relative.Split('/').Contains(".."), "Source must be repo-relative.");
                        Require(File.Exists(Path.Combine(WorkspaceRoot(), relative)), "Mapped implementation source missing: " + relative);
                        Require(!string.IsNullOrWhiteSpace(Text(source, "symbol")), "Source symbol required.");
                    }
                }
            }
        }
    }

    // The existing loader demands a full baseline. Sentinels retain declared requirements,
    // but are never treated as concrete bindings or executed evidence.
    internal static ToolboxReviewPolicy TestPolicy(JsonObject catalog)
    {
        var items = new JsonArray();
        foreach (var item in Array(catalog, "items").Cast<JsonObject>())
        {
            var stages = new JsonObject();
            foreach (var stage in new[] { "Compute", "Compare" })
            {
                var mapping = item["stages"]![stage]!;
                var checks = new JsonArray();
                if (Text(mapping, "binding_status") == "unresolved")
                    checks.Add(new JsonObject
                    {
                        ["check_id"] = "unresolved_binding", ["scope"] = "case",
                        ["applicability"] = new JsonObject { ["op"] = "always" },
                        ["evaluator_id"] = "unresolved.binding", ["evaluator_version"] = "fixture-v1",
                        ["effect"] = "requires_disposition"
                    });
                else foreach (var check in Array(mapping, "checks").Cast<JsonObject>())
                {
                    var projected = new JsonObject();
                    foreach (var field in new[] { "check_id", "scope", "applicability", "evaluator_id", "evaluator_version", "effect" })
                        projected[field] = check[field]!.DeepClone();
                    checks.Add(projected);
                }
                stages[stage] = new JsonObject { ["requirement"] = mapping["requirement"]!.DeepClone(), ["checks"] = checks };
            }
            items.Add(new JsonObject { ["item_id"] = item["item_id"]!.DeepClone(),
                ["display_name"] = item["display_name"]!.DeepClone(), ["stages"] = stages });
        }
        var policy = new JsonObject
        {
            ["schema_version"] = ToolboxReviewPolicyLoader.SchemaVersion, ["policy_id"] = "fixture-bindings",
            ["policy_version"] = "fixture-v1", ["activation_status"] = "isolated_test",
            ["classification_baseline"] = "compute-compare-v1",
            ["supported_stages"] = new JsonArray("Compute", "Compare"), ["preset_overrides"] = new JsonArray(), ["items"] = items
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(policy);
        return ToolboxReviewPolicyLoader.Load(bytes, new("fixture-bindings", "fixture-v1",
            Convert.ToHexString(SHA256.HashData(bytes))));
    }

    internal static ToolboxPolicyScope[] Scopes(JsonObject bundle) =>
        Array(bundle, "scope_inventory").Cast<JsonObject>().Select(scope =>
            new ToolboxPolicyScope(Text(scope, "case_id"), Text(scope, "scope_kind") switch
            {
                "case" => ToolboxScopeKind.Case, "document" => ToolboxScopeKind.Document,
                "parcel" => ToolboxScopeKind.Parcel, "participant" => ToolboxScopeKind.Participant,
                "identifier_pair" => ToolboxScopeKind.IdentifierPair,
                _ => throw new InvalidDataException("Unknown fixture scope.")
            }, Text(scope, "scope_id"), Text(scope, "revision_id"), Text(scope, "input_fingerprint"),
                scope["facts"]!.AsObject().ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value)))).ToArray();

    private static void ValidateBundle(JsonObject bundle)
    {
        Require(Text(bundle, "schema_version") == "toolbox_binding_fixture_v1", "Bundle schema.");
        Require(bundle["is_synthetic"]!.GetValue<bool>() && bundle["system_evidence"]!["is_mock"]!.GetValue<bool>(), "Synthetic data only.");
        var caseId = Text(bundle["case"]!, "case_id");
        Require(bundle["case"]!["no_remote_actions"]!.GetValue<bool>(), "No remote actions.");
        Require(Text(bundle["case"]!, "fgdb_name") == "TR-" + Text(bundle["case"]!, "transaction_number") + ".gdb",
            "TR naming/leading zeros retained, no real FGDB.");
        Require(!bundle["expected_stage_outcomes"]!["case_complete"]!.GetValue<bool>()
            && bundle["expected_stage_outcomes"]!["expected_only"]!.GetValue<bool>(), "Not a completed case.");
        var scopes = Scopes(bundle);
        Require(scopes.All(s => s.CaseId == caseId && s.RevisionId == Text(bundle["case"]!, "revision_id")), "Same case/revision.");
        Require(scopes.Select(s => s.Kind + ":" + s.ScopeId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == scopes.Length,
            "Unique scope inventory.");
        var groups = new[]
        {
            (ToolboxScopeKind.Case, new[] { caseId }),
            (ToolboxScopeKind.Document, bundle["documents"]!.AsObject().Select(p => p.Key).ToArray()),
            (ToolboxScopeKind.Parcel, bundle["geometry"]!["parcels"]!.AsObject().Select(p => p.Key).ToArray()),
            (ToolboxScopeKind.Participant, bundle["participants"]!.AsObject().Select(p => p.Key).ToArray()),
            (ToolboxScopeKind.IdentifierPair, bundle["identifier_pairs"]!.AsObject().Select(p => p.Key).ToArray())
        };
        foreach (var (kind, expected) in groups)
            Require(expected.Order().SequenceEqual(scopes.Where(s => s.Kind == kind).Select(s => s.ScopeId).Order()),
                "Incomplete authoritative fixture inventory: " + kind);
        var parcels = bundle["geometry"]!["parcels"]!.AsObject();
        var participants = bundle["participants"]!.AsObject();
        Require(scopes.All(s => s.Facts["scope.parcel_count"].GetInt32() == parcels.Count), "Parcel count fact matches inventory.");
        foreach (var party in participants.Select(p => p.Value!))
            foreach (var id in Array(party, "parcel_ids"))
                Require(parcels.ContainsKey(id!.GetValue<string>()), "Participant parcel link missing.");
        foreach (var pair in bundle["identifier_pairs"]!.AsObject().Select(p => p.Value!))
        {
            Require(participants.ContainsKey(Text(pair, "participant_id")), "Pair party link missing.");
            foreach (var id in Array(pair, "parcel_ids"))
                Require(parcels.ContainsKey(id!.GetValue<string>()), "Pair parcel link missing.");
        }
        foreach (var parcel in parcels.Select(p => p.Value!))
            foreach (var link in Array(parcel, "boundary_links").Cast<JsonObject>())
            {
                var neighbor = Text(link, "neighbor_parcel_id");
                Require(parcels.ContainsKey(neighbor), "Boundary neighbor missing.");
                Require(Array(parcels[neighbor]!, "boundary_links").Any(reverse =>
                    Text(reverse!, "boundary_id") == Text(link, "boundary_id")
                    && Text(reverse!, "neighbor_parcel_id") == Text(parcel, "parcel_id")), "Shared-boundary link must be reciprocal.");
            }
        foreach (var evidence in bundle["source_evidence"]!.AsObject().Select(p => p.Value!))
        {
            Require(bundle["documents"]!.AsObject().ContainsKey(Text(evidence, "document_id")), "Evidence document missing.");
            Require(evidence["page"]!.GetValue<int>() > 0 && evidence["is_synthetic"]!.GetValue<bool>(), "Evidence page/synthetic flag.");
            Require(evidence["page"]!.GetValue<int>() <= bundle["documents"]![Text(evidence, "document_id")]!["page_count"]!.GetValue<int>(),
                "Evidence page outside source document.");
        }
        foreach (var parcel in parcels.Select(p => p.Value!))
            foreach (var record in Array(parcel, "points").Concat(Array(parcel, "segments")).Append(parcel["printed_area"]!))
                Require(bundle["source_evidence"]!.AsObject().ContainsKey(Text(record!, "source_evidence_id")),
                    "Geometry/source-area evidence link missing.");
        foreach (var group in new[] { "participants", "identifier_pairs" })
            foreach (var response in bundle["system_evidence"]![group]!.AsObject().Select(p => p.Value!))
            {
                Require(response["query"] is JsonObject && response["records"] is JsonArray,
                    "Separate query input from returned records.");
                Require(Text(response, "status") != "service_unavailable" || Array(response, "records").Count == 0,
                    "Unavailable lookup cannot invent returned records.");
            }
        foreach (var record in Array(bundle, "human_reviews").Concat(Array(bundle, "finding_history")))
            foreach (var evidenceId in Array(record!, "source_evidence_ids"))
                Require(bundle["source_evidence"]!.AsObject().ContainsKey(evidenceId!.GetValue<string>()), "Source evidence link missing.");
        foreach (var scope in scopes.Where(s => s.Kind != ToolboxScopeKind.Case))
            Require(Array(bundle, "human_reviews").Any(review => Text(review!, "scope_id") == scope.ScopeId
                && !string.IsNullOrWhiteSpace(Text(review!, "actor"))), "Consumed scope lacks human review.");
    }

    private static ExtractionReviewRow[] Rows(JsonObject bundle) =>
        bundle["geometry"]!["parcels"]!.AsObject().SelectMany(parcel =>
            Array(parcel.Value!, "points").Cast<JsonObject>().Select(point => new ExtractionReviewRow
            {
                RowId = Text(point, "row_id"), ParcelGroupId = parcel.Key, ParcelName = parcel.Key,
                PointIdentifier = Text(point, "point_id"), SequenceInGroup = point["sequence"]!.GetValue<int>(),
                Easting = Text(point, "easting"), Northing = Text(point, "northing"),
                IsManual = point["is_manual"]!.GetValue<bool>(), Unresolved = point["unresolved"]!.GetValue<bool>(),
                RowProvenance = Text(point, "row_provenance"), RawRow = point["raw_row"]!.DeepClone().AsObject()
            })).ToArray();

    private static ParcelScopedReviewValidationService Validator() => new(
        () => new ClosureToleranceCatalog(), () => new OrientationValidationCatalog(), () => new ParcelReadinessCatalog());

    private static JsonObject GeneratedCadMap(int count)
    {
        var template = Bundle("single-parcel");
        var bundle = template.DeepClone().AsObject();
        var caseId = "generated-cad-map-" + count;
        bundle["fixture_id"] = caseId;
        bundle["case"]!["case_id"] = caseId;
        foreach (var key in new[] { "participants", "identifier_pairs", "source_evidence" })
            bundle[key]!.AsObject().Clear();
        bundle["source_evidence"]!["source-north"] = template["source_evidence"]!["source-north"]!.DeepClone();
        bundle["geometry"]!["parcels"]!.AsObject().Clear();
        Array(bundle["geometry"]!, "extracted_rows").Clear();
        bundle["system_evidence"]!["participants"]!.AsObject().Clear();
        bundle["system_evidence"]!["identifier_pairs"]!.AsObject().Clear();
        Array(bundle, "scope_inventory").Clear();
        Array(bundle, "human_reviews").Clear();
        bundle["expectations"]!["parcel_count"] = count;
        bundle["expectations"]!["participant_count"] = count;
        bundle["expectations"]!["identifier_pair_count"] = count;
        bundle["expectations"]!["shared_boundary_applicability"] = "applicable";

        void AddScope(string kind, string id)
        {
            var scope = Array(template, "scope_inventory").First(s => Text(s!, "scope_kind") == kind)!.DeepClone();
            scope["case_id"] = caseId; scope["scope_id"] = id;
            scope["input_fingerprint"] = caseId + ":" + kind + ":" + id + ":v2";
            scope["facts"]!["scope.parcel_count"] = count;
            Array(bundle, "scope_inventory").Add(scope);
        }
        AddScope("case", caseId); AddScope("document", "plan-1");
        Array(bundle, "human_reviews").Add(Array(template, "human_reviews")
            .First(r => Text(r!, "scope_kind") == "document")!.DeepClone());
        for (var n = 1; n <= count; n++)
        {
            var parcelId = "parcel-" + n; var ownerId = "owner-" + n; var pairId = "pair-" + n;
            var parcel = template["geometry"]!["parcels"]!["parcel-1"]!.DeepClone();
            parcel["parcel_id"] = parcelId;
            var points = Array(parcel, "points");
            for (var j = 0; j < points.Count; j++)
            {
                var point = points[j]!;
                point["row_id"] = parcelId + "-row-" + (j + 1);
                point["point_id"] = parcelId + "-P" + (j + 1);
                point["easting"] = (680000 + (n - 1) * 10 + (j is 1 or 2 ? 10 : 0)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                point["source_evidence_id"] = "source-" + parcelId;
                point["raw_row"]!["to_point"] = parcelId + "-P" + (j + 1);
                if (j > 0) point["raw_row"]!["from_point"] = parcelId + "-P" + j;
                var extracted = point.DeepClone(); extracted["parcel_id"] = parcelId;
                Array(bundle["geometry"]!, "extracted_rows").Add(extracted);
            }
            var segments = Array(parcel, "segments");
            for (var j = 0; j < segments.Count; j++)
            {
                segments[j]!["segment_id"] = parcelId + "-segment-" + (j + 1);
                segments[j]!["from_point"] = points[j]!["point_id"]!.DeepClone();
                segments[j]!["to_point"] = points[(j + 1) % points.Count]!["point_id"]!.DeepClone();
                segments[j]!["source_evidence_id"] = "source-" + parcelId;
            }
            parcel["printed_area"]!["source_evidence_id"] = "source-" + parcelId;
            var links = Array(parcel, "boundary_links"); links.Clear();
            if (n > 1) links.Add(new JsonObject { ["boundary_id"] = "shared-" + (n - 1), ["neighbor_parcel_id"] = "parcel-" + (n - 1) });
            if (n < count) links.Add(new JsonObject { ["boundary_id"] = "shared-" + n, ["neighbor_parcel_id"] = "parcel-" + (n + 1) });
            bundle["geometry"]!["parcels"]![parcelId] = parcel;

            var owner = template["participants"]!["owner-1"]!.DeepClone();
            owner["participant_id"] = ownerId; owner["parcel_ids"] = new JsonArray(parcelId);
            owner["raw_name"] = "Synthetic Owner " + n; owner["reviewed_name"] = "Synthetic Owner " + n;
            owner["source_evidence_id"] = "source-" + ownerId; bundle["participants"]![ownerId] = owner;
            var pair = template["identifier_pairs"]!["pair-1"]!.DeepClone();
            pair["pair_id"] = pairId; pair["participant_id"] = ownerId; pair["parcel_ids"] = new JsonArray(parcelId);
            pair["volume"] = (1000 + n).ToString(System.Globalization.CultureInfo.InvariantCulture);
            pair["folio"] = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            pair["source_evidence_id"] = "source-" + ownerId; bundle["identifier_pairs"]![pairId] = pair;

            foreach (var (sourceId, sourceTemplate, zone, raw) in new[]
            {
                ("source-" + parcelId, "source-parcel-1", parcelId,
                    parcelId + ": four 10m sides, printed area 100m2; coordinates " +
                    string.Join("; ", points.Select(p => Text(p!, "easting") + "," + Text(p!, "northing")))),
                ("source-" + ownerId, "source-owner-1", parcelId + "-owner",
                    "Synthetic Owner " + n + "; volume " + (1000 + n) + "; folio " + n)
            })
            {
                var evidence = template["source_evidence"]![sourceTemplate]!.DeepClone();
                evidence["zone"] = zone; evidence["raw_text"] = raw; bundle["source_evidence"]![sourceId] = evidence;
            }
            var systemOwner = template["system_evidence"]!["participants"]!["owner-1"]!.DeepClone();
            systemOwner["query"]!["name"] = "Synthetic Owner " + n;
            systemOwner["records"]![0]!["owner_name"] = "Synthetic Owner " + n;
            bundle["system_evidence"]!["participants"]![ownerId] = systemOwner;
            var systemPair = template["system_evidence"]!["identifier_pairs"]!["pair-1"]!.DeepClone();
            systemPair["query"]!["volume"] = pair["volume"]!.DeepClone();
            systemPair["query"]!["folio"] = pair["folio"]!.DeepClone();
            systemPair["records"]![0]!["volume"] = pair["volume"]!.DeepClone();
            systemPair["records"]![0]!["folio"] = pair["folio"]!.DeepClone();
            systemPair["records"]![0]!["record_id"] = "mock-title-" + n;
            systemPair["records"]![0]!["owner_name"] = "Synthetic Owner " + n;
            bundle["system_evidence"]!["identifier_pairs"]![pairId] = systemPair;

            foreach (var (kind, id, sourceId) in new[]
            {
                ("parcel", parcelId, "source-" + parcelId),
                ("participant", ownerId, "source-" + ownerId),
                ("identifier_pair", pairId, "source-" + ownerId)
            })
            {
                AddScope(kind, id);
                var review = Array(template, "human_reviews").First(r => Text(r!, "scope_kind") == kind)!.DeepClone();
                review["scope_id"] = id; review["source_evidence_ids"] = new JsonArray(sourceId);
                Array(bundle, "human_reviews").Add(review);
            }
        }
        return bundle;
    }

    public static void CadMapsSupportVariableParcelCountsBeyondThree()
    {
        var policy = TestPolicy(Catalog());
        foreach (var count in new[] { 2, 3, 5, 10, 50, 100 })
        {
            var bundle = GeneratedCadMap(count);
            ValidateBundle(bundle);
            var compute = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute, Scopes(bundle));
            var compare = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compare, Scopes(bundle));
            TestAssert.Equal(count, compute.Count(r => r.ItemId == "M10"), "Every parcel for variable-size Cad Map.");
            TestAssert.Equal(count, compute.Count(r => r.CheckId == "closure"), "Every parcel closure binding.");
            TestAssert.Equal(count, compare.Count(r => r.ItemId == "M18"), "All owners, not three or first owner.");
            TestAssert.Equal(count, compare.Count(r => r.ItemId == "M20"), "All identifier pairs.");
            TestAssert.Equal(count, compare.Count(r => r.ItemId == "M38"), "All required legal-query scopes.");
            TestAssert.Equal(count, compute.Count(r => r.CheckId == "shared_boundaries"
                && r.Applicability == ToolboxApplicability.Applicable), "Sharing applies to N > 1.");
            var validation = Validator().Validate(Rows(bundle));
            TestAssert.False(validation.HasBlockers, "Existing geometry validation handles every generated parcel.");
            TestAssert.Equal(count, validation.ClosureResults.Count, "No geometry results truncated to three.");
            TestAssert.Equal(count, validation.OrientationResults.Count, "Every orientation result retained.");
        }
    }

    public static void CatalogAccountsForAllItemsAndUnresolvedBindings()
    {
        var catalog = Catalog(); ValidateCatalog(catalog);
        var policy = TestPolicy(catalog);
        TestAssert.Equal(42, policy.Items.Length, "Exact approved baseline.");
        var unresolved = Array(catalog, "items").Where(item =>
            new[] { "Compute", "Compare" }.Any(s => Text(item!["stages"]![s]!, "binding_status") == "unresolved")).ToArray();
        TestAssert.Equal(24, unresolved.Length, "Unfinished items explicitly unresolved.");
        TestAssert.Equal(2, Array(catalog, "items").Count(item => Text(item!["stages"]!["Compute"]!, "binding_status") == "contract_snapshot_only"),
            "Findings/completeness are contract expectations, not implemented adapters.");
        foreach (var name in Bundles)
        {
            var bundle = Bundle(name);
            TestAssert.True(unresolved.Select(i => Text(i!, "item_id")).Order().SequenceEqual(
                Array(bundle["expectations"]!, "unresolved_item_ids").Select(i => i!.GetValue<string>()).Order()),
                "Bundle never claims unmapped checklist items completed.");
        }
    }

    public static void AllBundleIdentitiesScopesAndEvidenceLinksAreValid()
    {
        foreach (var name in Bundles) ValidateBundle(Bundle(name));
    }

    public static void ScopedBindingsResolveAndSelectorsExistInEveryBundle()
    {
        var catalog = Catalog(); var policy = TestPolicy(catalog);
        foreach (var name in Bundles)
        {
            var bundle = Bundle(name); var scopes = Scopes(bundle);
            foreach (var stage in new[] { ToolboxPreset.Compute, ToolboxPreset.Compare })
            {
                var resolved = ToolboxReviewPolicyResolver.Resolve(policy, stage, scopes);
                foreach (var item in Array(catalog, "items"))
                    foreach (var check in Array(item!["stages"]![stage.ToString()]!, "checks"))
                    {
                        var results = resolved.Where(r => r.ItemId == Text(item!, "item_id") && r.CheckId == Text(check!, "check_id")).ToArray();
                        TestAssert.True(results.Length > 0 && results.All(r => r.Applicability != ToolboxApplicability.Unresolved),
                            "Concrete fixture facts resolve each targeted scope.");
                        foreach (var result in results)
                            Select(bundle, Text(check!, "evidence_selector"), result.ScopeId!);
                    }
                var unresolvedCount = Array(catalog, "items").Count(item =>
                    Text(item!["stages"]![stage.ToString()]!, "binding_status") == "unresolved");
                TestAssert.Equal(unresolvedCount, resolved.Count(r => r.CheckId == "unresolved_binding"),
                    "Sentinels retain obligations; no evidence or completion claims.");
            }
        }
    }

    public static void SingleAndCadMapChecksCoverEveryParcelOwnerAndPair()
    {
        var policy = TestPolicy(Catalog());
        foreach (var name in new[] { "single-parcel", "multi-parcel-cad-map" })
        {
            var bundle = Bundle(name); var count = bundle["expectations"]!["parcel_count"]!.GetValue<int>();
            var compute = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute, Scopes(bundle));
            var compare = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compare, Scopes(bundle));
            TestAssert.Equal(count, compute.Count(r => r.ItemId == "M10"), "Every parcel point binding.");
            TestAssert.Equal(count, compare.Count(r => r.ItemId == "M18"), "Every owner, not first-party summary.");
            TestAssert.Equal(count, compare.Count(r => r.ItemId == "M20"), "Every title pair.");
            TestAssert.Equal(count, compare.Count(r => r.ItemId == "M38"), "Every legal evidence scope.");
            TestAssert.True(compute.Where(r => r.ItemId == "M14" && r.CheckId == "duplicate_edges")
                .All(r => r.EffectiveRequirement == ToolboxReviewRequirement.Required), "Duplicate checks remain required.");
            TestAssert.True(compute.Where(r => r.CheckId == "shared_boundaries").All(r =>
                r.Applicability == (count == 1 ? ToolboxApplicability.NotApplicable : ToolboxApplicability.Applicable)),
                "Cross-parcel applicability does not suppress parent review.");
        }
    }

    public static void ExistingPointClosureAndOrientationValidatorsReplayAllBundles()
    {
        foreach (var name in Bundles)
        {
            var bundle = Bundle(name); var rows = Rows(bundle);
            var result = Validator().Validate(rows);
            var count = bundle["expectations"]!["parcel_count"]!.GetValue<int>();
            TestAssert.False(result.HasBlockers, "Synthetic reviewed rows validate through existing service.");
            TestAssert.Equal(count, result.ClosureResults.Count, "Closure for every parcel.");
            TestAssert.Equal(count, result.OrientationResults.Count, "Orientation for every parcel.");
            TestAssert.True(result.ClosureResults.All(r => r.Status == ClosureValidationStatus.Passed), "Existing closure validator passed.");
            var changed = Rows(bundle); changed[0].Easting = "invalid-coordinate";
            TestAssert.True(Validator().Validate(changed).HasBlockers, "Bad extracted coordinate remains a blocker.");
            changed = Rows(bundle); changed[1].PointIdentifier = changed[0].PointIdentifier;
            TestAssert.True(Validator().Validate(changed).Issues.Any(i => i.Contains("duplicate point id", StringComparison.OrdinalIgnoreCase)),
                "Reuse validator; no hand-rolled point validation.");
        }
    }

    public static void ManualRecoveryRetainsOriginalsAndUsesExistingEntryService()
    {
        var bundle = Bundle("manual-geometry-recovery");
        TestAssert.Equal(0, Array(bundle["geometry"]!, "extracted_rows").Count, "Zero extraction retained.");
        var document = new ExtractionReviewDocument();
        var service = new ParcelScopedManualPointService();
        foreach (var expected in Rows(bundle))
        {
            var row = service.CreateManualRow(document, expected.ParcelGroupId, expected.ParcelName, expected.ParcelGroupId);
            document.Rows.Add(row);
            TestAssert.True(Validator().Validate(document.Rows, row.RowId).HasBlockers, "Incomplete/manual pending edit cannot pass.");
            row.PointIdentifier = expected.PointIdentifier; row.Easting = expected.Easting; row.Northing = expected.Northing;
            row.RawRow = expected.RawRow.DeepClone().AsObject();
            TestAssert.True(row.IsManual && row.RowProvenance == "manual", "Existing manual provenance.");
        }
        TestAssert.False(Validator().Validate(document.Rows).HasBlockers, "Completed manual capture still fully validated.");
        var compute = ToolboxReviewPolicyResolver.Resolve(TestPolicy(Catalog()), ToolboxPreset.Compute, Scopes(bundle));
        TestAssert.Equal(ToolboxApplicability.Applicable, compute.Single(r => r.ItemId == "M17").Applicability, "Manual alternative available.");
        var party = bundle["participants"]!["owner-1"]!;
        TestAssert.Equal("Synthetic Owmer 1", Text(party, "raw_name"), "Original extraction not overwritten.");
        TestAssert.Equal("Synthetic Owner 1", Text(party, "reviewed_name"), "Compare tabular correction retained.");
        TestAssert.Equal("Compare", Text(Array(party, "review_events")[1]!, "stage"), "No geometry return needed for tabular correction.");
    }

    public static void NorthArrowReusePreservesReportOnlyFindingAndRequiredReview()
    {
        var policy = TestPolicy(Catalog());
        foreach (var name in Bundles)
        {
            var bundle = Bundle(name);
            var evidence = bundle["documents"]!["plan-1"]!["north_arrow"]!;
            var document = new ExtractionReviewDocument { MemorandumDetected = true };
            document.SurveyMetadataFields.Add(new ExtractionReviewMetadataField
            {
                Key = "north_arrow", Present = evidence["present"]!.GetValue<bool>(),
                Value = Text(evidence, "raw_text"), RawText = Text(evidence, "raw_text"), ReviewStatus = "Accepted"
            });
            var service = new PxaMemorandumReviewRuleService(System.Array.Empty<PreflightRuleDefinition>());
            var result = service.Evaluate(document).Single(r => r.RuleId == "pxa_memorandum_north_arrow_present");
            TestAssert.Equal(Text(bundle["expectations"]!, "memo_north_status"), result.Outcome, "Existing memorandum evaluator.");
            TestAssert.Equal("report_only", result.WorkflowEffect, "Missing arrow not upgraded to blocker.");
            var resolved = ToolboxReviewPolicyResolver.Resolve(policy, ToolboxPreset.Compute, Scopes(bundle));
            TestAssert.True(resolved.Where(r => r.ItemId == "M35").All(r =>
                r.EffectiveRequirement == ToolboxReviewRequirement.Required && r.Effect == ToolboxReviewEffect.ReportOnly),
                "Required human examination independent of automated effect.");
        }
    }

    public static void MockLtdOutageRetainsRequiredComparisonsAndFindingHistory()
    {
        var bundle = Bundle("multi-parcel-cad-map");
        var results = ToolboxReviewPolicyResolver.Resolve(TestPolicy(Catalog()), ToolboxPreset.Compare, Scopes(bundle));
        TestAssert.Equal("service_unavailable", Text(bundle["system_evidence"]!["identifier_pairs"]!["pair-3"]!, "status"), "Outage fixture.");
        TestAssert.Equal(ToolboxReviewRequirement.Required, results.Single(r => r.ItemId == "M38" && r.ScopeId == "pair-3").EffectiveRequirement,
            "No service-availability fact can erase required legal comparison.");
        var findings = Array(bundle, "finding_history");
        TestAssert.True(findings.Any(f => Text(f!, "stage") == "Compute" && Text(f!, "origin") == "source_defect"),
            "Earlier source findings retained.");
        TestAssert.True(findings.Any(f => Text(f!, "stage") == "Compare" && Text(f!, "status") == "pending_with_reason"),
            "Pending-with-reason is a contract snapshot, not generated stage approval.");
        TestAssert.False(bundle["expected_stage_outcomes"]!["case_complete"]!.GetValue<bool>(), "Partial fixture cannot approve a case.");
        TestAssert.Equal(0, Array(bundle["system_evidence"]!["identifier_pairs"]!["pair-3"]!, "records").Count,
            "A query key is not a returned matching record.");
    }

    public static void BrokenInventoryAndEvidenceLinksAreRejected()
    {
        var bundle = Bundle("multi-parcel-cad-map");
        var scopes = Array(bundle, "scope_inventory");
        var missing = scopes.First(s => Text(s!, "scope_kind") == "parcel");
        scopes.Remove(missing); Reject(() => ValidateBundle(bundle));
        bundle = Bundle("single-parcel"); bundle["identifier_pairs"]!["pair-1"]!["participant_id"] = "missing";
        Reject(() => ValidateBundle(bundle));
        bundle = Bundle("single-parcel"); bundle["source_evidence"]!.AsObject().Remove("source-owner-1");
        Reject(() => ValidateBundle(bundle));
        bundle = Bundle("multi-parcel-cad-map");
        Array(bundle["system_evidence"]!["identifier_pairs"]!["pair-3"]!, "records").Add(new JsonObject { ["volume"] = "1003" });
        Reject(() => ValidateBundle(bundle));
        var catalog = Catalog(); Array(catalog, "items")[1]!["item_id"] = "M01";
        Reject(() => ValidateCatalog(catalog));
    }
}
