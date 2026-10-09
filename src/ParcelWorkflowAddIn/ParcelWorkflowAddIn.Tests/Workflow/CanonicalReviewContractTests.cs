using System.IO;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.Workflow.Review.Canonical;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class CanonicalReviewContractTests
{
    internal static JsonObject Sample(int count = 1)
    {
        var root = new JsonObject
        {
            ["transaction_number"] = "000000101", ["review_hash"] = "legacy-hash",
            ["unknown_provider"] = new JsonObject { ["nested"] = "preserved" },
            ["source_inventory"] = new JsonArray(new JsonObject { ["source_id"] = "plan", ["source_role"] = "st_surveyplan" }),
            ["source_traces"] = new JsonArray(new JsonObject { ["evidence_id"] = "ev", ["source_id"] = "plan", ["page"] = 1, ["zone"] = "map" }),
            ["parcel_groups"] = new JsonArray(), ["rows"] = new JsonArray(), ["segments"] = new JsonArray()
        };
        for (var i = 1; i <= count; i++)
        {
            var id = "group-" + i;
            root["parcel_groups"]!.AsArray().Add(new JsonObject { ["parcel_group_id"] = id, ["display_label"] = "Same label" });
            root["rows"]!.AsArray().Add(new JsonObject
            {
                ["row_id"] = "row-" + i, ["parcel_group_id"] = id, ["sequence_in_group"] = 1,
                ["point_id"] = "P1", ["easting"] = "10", ["northing"] = "20",
                ["original_values"] = new JsonObject { ["easting"] = "bad original" },
                ["is_manual"] = true, ["source_evidence_id"] = "ev"
            });
            root["segments"]!.AsArray().Add(new JsonObject { ["segment_id"] = "segment-" + i,
                ["parcel_group_id"] = id, ["sequence"] = 1, ["source_evidence_id"] = "ev" });
        }
        return root;
    }

    public static void LosslessImmutableAndUnknownDefaults()
    {
        var input = Sample();
        var before = input.ToJsonString();
        var contract = CanonicalReviewContract.Read(input);
        TestAssert.Equal(before, contract.ToJson().ToJsonString(), "Lossless JSON projection including legacy hash.");
        TestAssert.Equal("unreviewed", contract.Parcels[0].ValidationStatus, "No implied approval.");
        input["rows"]![0]!["easting"] = "999";
        var output = contract.ToJson();
        output["review_hash"] = "changed";
        TestAssert.Equal("10", contract.Rows[0].Data.GetProperty("easting").GetString(), "Owned row values.");
        TestAssert.Equal("legacy-hash", contract.ToJson()["review_hash"]!.GetValue<string>(), "Owned root.");
        TestAssert.Equal(1, contract.Sources.Length, "Survey sheet is not universally required.");
        var aliases = Sample();
        var trace = aliases["source_traces"]![0]!;
        trace["source_page"] = "1"; trace["source_zone"] = "map";
        TestAssert.Equal(1, CanonicalReviewContract.Read(aliases).Traces[0].Page, "Reuse canonical scalar source fields.");
        trace["source_page"] = 2;
        Reject(() => CanonicalReviewContract.Read(aliases));
    }

    public static void ArbitraryCountsAndZeroGeometryCandidates()
    {
        foreach (var count in new[] { 1, 2, 3, 5, 10, 50, 100 })
            TestAssert.Equal(count, CanonicalReviewContract.Read(Sample(count)).Parcels.Length, "No fixed parcel cap.");
        var root = Sample(3);
        root["parcel_groups"]!.AsArray().Add(new JsonObject { ["parcel_group_id"] = "manual-recovery" });
        TestAssert.Equal(4, CanonicalReviewContract.Read(root).Parcels.Length, "Retain parcel without geometry.");
    }

    public static void InvalidIdentityMembershipAndProvenanceFailAtomically()
    {
        foreach (var mutation in new Action<JsonObject>[]
        {
            x => x["rows"]![0]!["row_id"] = "",
            x => x["rows"]!.AsArray().Add(x["rows"]![0]!.DeepClone()),
            x => x["rows"]![0]!["parcel_group_id"] = "foreign",
            x => x["rows"]![0]!["sequence_in_group"] = 0,
            x => x["source_traces"]![0]!["source_id"] = "foreign",
            x => x["point_memberships"] = new JsonArray(),
            x => x["parcel_groups"] = new JsonArray(new JsonObject { ["parcel_group_id"] = "group-1" }, new JsonObject { ["parcel_group_id"] = "GROUP-1" })
        })
        {
            var root = Sample(); mutation(root);
            var before = root.ToJsonString();
            Reject(() => CanonicalReviewContract.Read(root));
            TestAssert.Equal(before, root.ToJsonString(), "Failure cannot mutate input.");
        }
    }

    public static void LegacySingleGroupingAndExplicitClosure()
    {
        var root = Sample();
        root.Remove("parcel_groups"); root["rows"]![0]!.AsObject().Remove("parcel_group_id");
        root["segments"]![0]!.AsObject().Remove("parcel_group_id");
        TestAssert.Equal("parcel-001", CanonicalReviewContract.Read(root).Parcels[0].Key, "Single legacy fallback.");
        root = Sample();
        root["point_memberships"] = new JsonArray(
            new JsonObject { ["parcel_group_id"] = "group-1", ["entity_key"] = "row-1", ["sequence"] = 1, ["direction"] = "forward", ["role"] = "boundary" },
            new JsonObject { ["parcel_group_id"] = "group-1", ["entity_key"] = "row-1", ["sequence"] = 2, ["direction"] = "reverse", ["role"] = "closure" });
        TestAssert.Equal(2, CanonicalReviewContract.Read(root).PointMemberships.Length, "Explicit closure retained.");
    }

    public static void MalformedFactInventoriesAndParcelLinksFailAtomically()
    {
        foreach (var name in new[] { "participants", "identifier_pairs" })
        {
            foreach (var links in new[] { "null", "\"group-1\"", "{}", "[null]", "[42]", "[\"\"]", "[\"foreign\"]", "[\"group-1\",\"GROUP-1\"]" })
            {
                var root = Sample();
                root[name] = new JsonObject { ["fact"] = new JsonObject { ["parcel_ids"] = JsonNode.Parse(links) } };
                var before = root.ToJsonString();
                Reject(() => CanonicalReviewContract.Read(root));
                TestAssert.Equal(before, root.ToJsonString(), "Malformed links fail before adaptation without mutation.");
            }
            foreach (var inventory in new[] { "null", "[]", "\"bad\"" })
            {
                var root = Sample(); root[name] = JsonNode.Parse(inventory);
                Reject(() => CanonicalReviewContract.Read(root));
            }
            var valid = Sample();
            valid[name] = new JsonObject { ["global"] = new JsonObject(), ["linked"] = new JsonObject { ["parcel_ids"] = new JsonArray("group-1") },
                ["empty"] = new JsonObject { ["parcel_ids"] = new JsonArray() } };
            TestAssert.Equal(1, CanonicalReviewContract.Read(valid).Parcels.Length, "Valid optional/global/empty link lists remain supported.");
        }
    }

    internal static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Expected contract rejection.");
    }
}
