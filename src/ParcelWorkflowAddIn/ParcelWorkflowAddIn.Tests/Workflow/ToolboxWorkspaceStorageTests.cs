using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Review.Canonical;
using ParcelWorkflowAddIn.Workflow.Toolbox;
using ParcelWorkflowAddIn.Workflow.Toolbox.Model;
using ParcelWorkflowAddIn.Workflow.Toolbox.Persistence;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class ToolboxWorkspaceStorageTests
{
    public static void ContractHasNoWriterAuthority()
    {
        var type = typeof(ToolboxParcelModel).Assembly.GetType(
            "ParcelWorkflowAddIn.Workflow.Toolbox.Persistence.ToolboxWorkspaceStorageDocument");
        TestAssert.True(type is not null, "Versioned explicit storage DTO exists.");
        TestAssert.False(type!.GetProperties().Any(p => p.Name is "Context" or "Committed" or "Authorized"),
            "A prototype bundle does not carry writer/session authority.");
        foreach (var entry in ToolboxStorageJson.Fields)
            TestAssert.True(ToolboxStorageJson.Options.GetTypeInfo(entry.Key).Properties.Select(p => p.Name)
                .Order(StringComparer.Ordinal).SequenceEqual(entry.Value), "Every record has a version-locked field manifest.");
        TestAssert.Equal(ToolboxStorageJson.FieldShapeSha256, ToolboxStorageJson.SchemaFingerprint(), "Types/nullability/enum wire values are pinned for v1.");
    }

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-11T12:00:00+02:00");
    private static ToolboxCaseContext Context() => new("synthetic", "case-a", "examiner",
        new SelectedInnolaTransaction("task", "transaction-a", "000000101", "Compute", "Compute", Now),
        Path.Combine(Path.GetTempPath(), "ToolboxStorage"), Path.Combine(Path.GetTempPath(), "ToolboxStorage", "case-a"));
    private static ToolboxParcelModel Model(int count = 1) => ToolboxParcelModel.Adapt(Context(),
        CanonicalReviewContract.Read(CanonicalReviewContractTests.Sample(count)), newImport: true);

    public static void AStateRoundTripsWithoutActions()
    {
        foreach (var count in new[] { 0, 1, 3, 10 })
        {
            var model = Model(count); var codec = new ToolboxWorkspaceStorageCodec();
            var captured = codec.Capture(new(model, new()));
            TestAssert.True(captured.Success, "Capture explicit A state.");
            var encoded = codec.Encode(captured.Value!);
            TestAssert.True(encoded.Success, "Encode supported A state.");
            var decoded = codec.Decode(encoded.Value!, model.Case);
            TestAssert.True(decoded.Success, "Hydrate supported A state.");
            TestAssert.Equal(model.InclusionFingerprint, decoded.Value!.Model.InclusionFingerprint, "Inclusion hash unchanged.");
            TestAssert.Equal(model.Source.JsonSnapshot, decoded.Value.Model.Source.JsonSnapshot, "Canonical bytes unchanged.");
            foreach (var p in model.Parcels)
                TestAssert.Equal(model.ParcelFingerprint(p.Id, "synthetic"), decoded.Value.Model.ParcelFingerprint(p.Id, "synthetic"), "Parcel proof unchanged.");
            TestAssert.True(decoded.Value.Reconciliation is null && decoded.Value.Register.Case is null, "No revision or register case minted.");
            TestAssert.True(encoded.Value!.SequenceEqual(codec.Encode(captured.Value!).Value!), "Deterministic bytes.");
        }
    }

    private static ToolboxWorkspaceStorageDocument Document(ToolboxParcelModel? model = null,
        ToolboxExaminationRegister? register = null, ImmutableArray<ToolboxCheckReview> live = default)
    {
        var result = new ToolboxWorkspaceStorageCodec().Capture(new(model ?? Model(), register ?? new(), CurrentCheckReviews: live));
        TestAssert.True(result.Success, "Capture fixture: " + string.Join(",", result.Diagnostics.Select(d => d.Code)));
        return result.Value!;
    }
    private static byte[] Raw(ToolboxWorkspaceStorageDocument doc) => ToolboxStorageJson.Bytes(doc);
    private static ToolboxWorkspaceStorageDocument Reseal(ToolboxWorkspaceStorageDocument doc, ToolboxStoredModel model)
    {
        model = model with { SnapshotId = ToolboxWorkspaceStorageCodec.ModelId(model) };
        return doc with { Models = [model], RootModelId = model.SnapshotId };
    }
    private static void Rejected(byte[] bytes, ToolboxCaseIdentity identity, string code, ToolboxStorageLimits? limits = null)
    {
        var result = new ToolboxWorkspaceStorageCodec(limits).Decode(bytes, identity);
        TestAssert.False(result.Success, "Invalid bundle must not expose a partial model.");
        TestAssert.Equal(code, result.Diagnostics[0].Code, "Typed failure.");
        TestAssert.False(result.Diagnostics[0].Reason.Contains("secret-value"), "No source-value diagnostics.");
    }

    public static void AHistoryRawValuesAndLiveReviews()
    {
        var json = CanonicalReviewContractTests.Sample(3);
        json["provider_unknown"] = new JsonObject { ["nested"] = new JsonArray("original", 3, null) };
        var model = ToolboxParcelModel.Adapt(Context(), CanonicalReviewContract.Read(json), newImport: true);
        model = model.SetInclusion(new(model.Parcels[1].Id, ToolboxInclusion.Excluded, "old-actor", Now,
            "Excluded with source evidence", "ev", "old-policy"), "old-policy", true);
        var register = new ToolboxExaminationRegister().RecordHumanReview(model, new("ev", "raw-title", "No title recorded",
            "manual", ToolboxPresence.MissingFromSource, ToolboxAccuracy.Confirmed, ToolboxPreset.Compare,
            "old-actor", Now, "Confirmed source absence", "old-input"), new("old-actor", "old-policy", true));
        var review = new ToolboxCheckReview("old-proof", ToolboxCheckOutcome.Pending, "LTD unavailable", ["ev"], "old-actor", Now,
            GeometryFingerprints: ImmutableDictionary<string, string>.Empty);
        var doc = Document(model, register, [review]); var decoded = new ToolboxWorkspaceStorageCodec().Decode(Raw(doc), model.Case);
        TestAssert.True(decoded.Success, "Historical/live input loads for inspection.");
        TestAssert.Equal(register.HumanReviews[0], decoded.Value!.Register.HumanReviews[0], "Raw/manual strings and time/offset preserved.");
        TestAssert.Equal(model.InclusionHistory[0], decoded.Value.Model.InclusionHistory[0], "Inclusion audit unchanged.");
        TestAssert.Equal(model.Source.JsonSnapshot, decoded.Value.Model.Source.JsonSnapshot, "Unknown nested provider values preserved.");
        TestAssert.Equal(ToolboxCheckOutcome.Pending, decoded.Value.CurrentCheckReviews.Single().Outcome, "Pending does not become passed.");
        TestAssert.True(decoded.Value.CurrentCheckReviews.Single().GeometryFingerprints is { Count: 0 }, "Empty geometry map is not null.");
        TestAssert.False(Context().GeometryAccess().Allowed, "Hydration never grants local edit permission.");
        TestAssert.False(Context().RemoteActionAccess.Allowed, "No remote authority.");
        TestAssert.True(doc.Examination.HumanReviews[0].At.Offset == TimeSpan.FromHours(2), "Original offset retained.");
    }

    public static void StrictShapeAndVersionFailures()
    {
        var doc = Document(); var bytes = Raw(doc);
        Rejected(Raw(doc with { Contract = "future" }), doc.Case, "unsupported_contract");
        Rejected(Raw(doc with { SchemaVersion = 2 }), doc.Case, "unsupported_version");
        Rejected(Raw(doc with { StorageMode = "committed" }), doc.Case, "unsupported_mode");
        Rejected(bytes, doc.Case with { Environment = "foreign" }, "foreign_case");
        var text = Encoding.UTF8.GetString(bytes);
        Rejected(Encoding.UTF8.GetBytes(text.Insert(1, "\"contract\":\"secret-value\",")), doc.Case, "duplicate_field");
        var node = JsonNode.Parse(bytes)!.AsObject(); node["unknown_authority"] = true;
        Rejected(Encoding.UTF8.GetBytes(node.ToJsonString()), doc.Case, "invalid_value");
        node = JsonNode.Parse(bytes)!.AsObject(); node.Remove("examination");
        Rejected(Encoding.UTF8.GetBytes(node.ToJsonString()), doc.Case, "missing_reference");
        node = JsonNode.Parse(bytes)!.AsObject(); node["models"]![0]!["inclusions"]!.AsObject()[doc.Models[0].Inclusions.Keys.Single()] = 77;
        Rejected(Encoding.UTF8.GetBytes(node.ToJsonString()), doc.Case, "invalid_value");
    }

    public static void MapAndTransactionClosureFailures()
    {
        var doc = Document(); var m = doc.Models[0];
        var missing = m with { IdentityMap = m.IdentityMap with { Ids = m.IdentityMap.Ids.Remove(m.IdentityMap.Ids.Keys.First()) } };
        Rejected(Raw(Reseal(doc, missing)), doc.Case, "missing_reference");
        var duplicate = JsonNode.Parse(Raw(doc))!.AsObject(); var ids = duplicate["models"]![0]!["identity_map"]!["ids"]!.AsObject();
        var first = ids.First(); ids[first.Key.ToUpperInvariant()] = first.Value!.DeepClone();
        Rejected(Encoding.UTF8.GetBytes(duplicate.ToJsonString()), doc.Case, "duplicate_identity");
        var same = m.IdentityMap.Ids.ToImmutableDictionary(p => p.Key, _ => "same", StringComparer.OrdinalIgnoreCase);
        Rejected(Raw(Reseal(doc, m with { IdentityMap = m.IdentityMap with { Ids = same } })), doc.Case, "duplicate_identity");
        var source = JsonNode.Parse(Convert.FromBase64String(doc.Payloads.Single().Utf8Base64))!.AsObject(); source["transaction_number"] = "different-tr";
        var raw = Encoding.UTF8.GetBytes(source.ToJsonString()); var hash = ToolboxWorkspaceStorageCodec.Hash(raw);
        var payload = new ToolboxStoredPayload("canonical-" + hash, "canonical", Convert.ToBase64String(raw), hash);
        var wrongTr = Reseal(doc with { Payloads = [payload] }, m with { SourcePayloadId = payload.PayloadId });
        Rejected(Raw(wrongTr), doc.Case, "foreign_case");
        var factoryRejected = false;
        try { _ = ToolboxParcelModel.Restore(doc.Case, CanonicalReviewContract.Read(source), m.IdentityMap, m.Inclusions, [], [], null); }
        catch (ToolboxStorageException e) { factoryRejected = e.Code == "foreign_case"; }
        TestAssert.True(factoryRejected, "Independent restoration enforces the canonical TR invariant.");
    }

    public static void PayloadIntegrityAndResourceLimits()
    {
        var doc = Document(); var bytes = Raw(doc); var codec = new ToolboxWorkspaceStorageCodec();
        Rejected(Raw(doc with { Payloads = [doc.Payloads[0] with { Sha256 = new string('0', 64) }] }), doc.Case, "checksum_mismatch");
        Rejected(Raw(doc with { Payloads = [] }), doc.Case, "missing_reference");
        Rejected(Raw(doc with { Models = doc.Models.Add(doc.Models[0]) }), doc.Case, "duplicate_identity");
        TestAssert.True(new ToolboxWorkspaceStorageCodec(new(BundleBytes: bytes.Length)).Decode(bytes, doc.Case).Success, "Exact byte budget.");
        Rejected(bytes, doc.Case, "limit_exceeded", new(BundleBytes: bytes.Length - 1));
        Rejected(bytes, doc.Case, "limit_exceeded", new(CanonicalBytes: 1));
        Rejected(bytes, doc.Case, "limit_exceeded", new(CollectionEntries: 2));
        Rejected(bytes, doc.Case, "limit_exceeded", new(WorkUnits: 1));
        Rejected(bytes, doc.Case, "limit_exceeded", new(ProjectionBytes: 1));
        Rejected(bytes, doc.Case, "limit_exceeded", new(Models: 0));
        var result = codec.Decode(bytes, doc.Case);
        var payload = result.Value!.PayloadBytes[doc.Payloads[0].PayloadId];
        bytes[0] = 0;
        TestAssert.True(payload.SequenceEqual(Convert.FromBase64String(doc.Payloads[0].Utf8Base64)), "Hydrated asset bytes are owned copies.");
    }

    public static void MissingAHistoricalEvidenceIsUnsupported()
    {
        var model = Model();
        var reg = new ToolboxExaminationRegister().RecordHumanReview(model, new("ev", "raw", "reviewed", "manual",
            ToolboxPresence.Present, ToolboxAccuracy.Confirmed, ToolboxPreset.Compute, "examiner", Now, "Observed", "r"), new("examiner", "p", true));
        var source = model.Source.ToJson(); source["source_traces"] = new JsonArray(); source["rows"]![0]!.AsObject().Remove("source_evidence_id");
        source["segments"]![0]!.AsObject().Remove("source_evidence_id"); source["evidence_links"] = new JsonArray();
        var changed = model.ReAdapt(Context(), CanonicalReviewContract.Read(source));
        var result = new ToolboxWorkspaceStorageCodec().Capture(new(changed, reg));
        TestAssert.False(result.Success, "Unavailable A history is not silently lost.");
        TestAssert.Equal("unsupported_capture", result.Diagnostics[0].Code, "Unsupported history diagnostic.");
    }

    public static void BoundsCoverCanonicalUnknownAndWrapperText()
    {
        var doc = Document(); var bytes = Raw(doc);
        Rejected(bytes, doc.Case, "limit_exceeded", new(JsonDepth: 2));
        Rejected(bytes, doc.Case, "limit_exceeded", new(TokenCharacters: 8));
        Rejected(bytes, doc.Case, "limit_exceeded", new(TextBytes: 4));
        var json = CanonicalReviewContractTests.Sample(); json["provider_unknown"] = new JsonObject { ["nested"] = new string('x', 100) };
        var model = ToolboxParcelModel.Adapt(Context(), CanonicalReviewContract.Read(json), newImport: true);
        var unknown = Document(model);
        Rejected(Raw(unknown), model.Case, "limit_exceeded", new(TextBytes: 80));
        var large = CanonicalReviewContractTests.Sample(100);
        var wide = ToolboxParcelModel.Adapt(Context(), CanonicalReviewContract.Read(large), newImport: true);
        var result = new ToolboxWorkspaceStorageCodec(new(WorkUnits: 1000)).Capture(new(wide, new()));
        TestAssert.False(result.Success, "Adversarial canonical projection rejected by work admission.");
        TestAssert.Equal("limit_exceeded", result.Diagnostics[0].Code, "Work-budget failure typed.");
        var source = JsonNode.Parse(Convert.FromBase64String(doc.Payloads[0].Utf8Base64))!.AsObject();
        var duplicate = source.ToJsonString().Insert(1, "\"transaction_number\":\"secret-value\",");
        var raw = Encoding.UTF8.GetBytes(duplicate); var hash = ToolboxWorkspaceStorageCodec.Hash(raw);
        var payload = new ToolboxStoredPayload("canonical-" + hash, "canonical", Convert.ToBase64String(raw), hash);
        var duplicateDoc = Reseal(doc with { Payloads = [payload] }, doc.Models[0] with { SourcePayloadId = payload.PayloadId });
        Rejected(Raw(duplicateDoc), doc.Case, "duplicate_field");
        var invalidUtf8 = new byte[] { 0xff }; hash = ToolboxWorkspaceStorageCodec.Hash(invalidUtf8);
        payload = new("canonical-" + hash, "canonical", Convert.ToBase64String(invalidUtf8), hash);
        Rejected(Raw(doc with { Payloads = [payload] }), doc.Case, "invalid_value");
    }

    public static void MissingNullAndCaseAliasFieldsFail()
    {
        var doc = Document(); var baseline = Raw(doc);
        foreach (var mutation in new Action<JsonObject>[]
        {
            n => n["examination"]!["findings"] = null,
            n => n["models"]![0]!["identity_map"] = null,
            n => n["case"]!["case_id"] = null,
            n => n["current_check_reviews"] = null,
            n => n["schema_version"] = "1",
            n => n["fingerprint_contracts"] = new JsonArray("future-version")
        })
        {
            var node = JsonNode.Parse(baseline)!.AsObject(); mutation(node);
            TestAssert.False(new ToolboxWorkspaceStorageCodec().Decode(Encoding.UTF8.GetBytes(node.ToJsonString()), doc.Case).Success,
                "Missing/null/incorrect known fields cannot be defaulted or coerced.");
        }
        var wrongRegister = doc with { Examination = doc.Examination with { Case = doc.Case with { CaseId = "foreign" } } };
        Rejected(Raw(wrongRegister), doc.Case, "foreign_case");
        var model = Model(); var codec = new ToolboxWorkspaceStorageCodec();
        var historic = new ToolboxHumanReview("ev", "raw", "reviewed", "manual", ToolboxPresence.Present, ToolboxAccuracy.Confirmed,
            ToolboxPreset.Compute, "examiner", Now, "Original proof", "r", "old-proof");
        var invalidRegister = new ToolboxStoredExamination(null, [], [historic], []);
        Rejected(Raw(doc with { Examination = invalidRegister }), doc.Case, "invalid_value");
        TestAssert.False(codec.Capture(new(model, new(), CurrentCheckReviews: [new("old", (ToolboxCheckOutcome)99,
            "review", ["ev"], "examiner", Now)])).Success, "Invalid captured enum rejected.");
    }

    public static void RestoreFactoriesRejectEqualValuedAliases()
    {
        var model = Model(); var key = model.IdentityMap.Ids.Keys.First();
        var aliases = model.IdentityMap.Ids.WithComparers(StringComparer.Ordinal).Add(key.ToUpperInvariant(), model.IdentityMap.Ids[key]);
        var rejected = false;
        try { _ = ToolboxParcelModel.Restore(model.Case, model.Source, model.IdentityMap with { Ids = aliases }, model.Inclusions, [], [], null); }
        catch (ToolboxStorageException e) { rejected = e.Code == "duplicate_identity"; }
        TestAssert.True(rejected, "Direct factory cannot collapse identical-valued map aliases.");
        var parcel = model.Parcels[0].Id;
        var states = model.Inclusions.WithComparers(StringComparer.Ordinal).Add(parcel.ToUpperInvariant(), model.Inclusions[parcel]);
        rejected = false;
        try { _ = ToolboxParcelModel.Restore(model.Case, model.Source, model.IdentityMap, states, [], [], null); }
        catch (ToolboxStorageException e) { rejected = e.Code == "duplicate_identity"; }
        TestAssert.True(rejected, "Direct factory cannot collapse identical-valued inclusion aliases.");
    }
}
