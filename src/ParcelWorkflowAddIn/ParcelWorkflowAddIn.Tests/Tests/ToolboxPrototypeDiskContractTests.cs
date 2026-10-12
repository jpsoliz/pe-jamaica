using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.Workflow.Toolbox.Model;
using ParcelWorkflowAddIn.Workflow.Toolbox.Persistence;

namespace ParcelWorkflowAddIn.Tests.Tests;

internal static class ToolboxPrototypeDiskContractTests
{
    private const string StoreId = "11111111111111111111111111111111";
    private const string OperationId = "22222222222222222222222222222222";
    private const string RevisionId = "33333333333333333333333333333333";
    private const string ParentId = "44444444444444444444444444444444";
    private const string EmptyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string AHash = "ca978112ca1bbdcafac231b39a23dc4da786eff8147c4e72b9807785afee48bb";
    private const string AbcHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    private const string ParentHash = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private const string Timestamp = "2026-10-11T12:34:56.1234567+05:30";

    // Hand-authored canonical fixtures; neither bytes nor expected hashes come from the disk encoder.
    private const string OwnerGolden = """
        {"case":{"case_id":"Case-1","environment":"Test","transaction_id":"TR-id","transaction_number":"TR-1"},"contract":"toolbox_prototype_disk_v1","schema_version":1,"storage_mode":"prototype_store","store_id":"11111111111111111111111111111111"}
        """;
    private const string IntentGolden = """
        {"actor":" Alice\n\u00E9 \u003C\u0026\u003E \u0022Q\u0022\\ ","assets":[{"byte_length":0,"sha256":"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"},{"byte_length":1,"sha256":"ca978112ca1bbdcafac231b39a23dc4da786eff8147c4e72b9807785afee48bb"}],"bundle":{"byte_length":3,"sha256":"ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"},"case":{"case_id":"Case-1","environment":"Test","transaction_id":"TR-id","transaction_number":"TR-1"},"contract":"toolbox_prototype_disk_v1","expected_parent":{"revision_digest":"dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd","revision_id":"44444444444444444444444444444444","sequence":7},"operation_id":"22222222222222222222222222222222","prepared_at":"2026-10-11T12:34:56.1234567\u002B05:30","proposed_revision_id":"33333333333333333333333333333333","schema_version":1,"store_id":"11111111111111111111111111111111"}
        """;

    private const string ShapeGolden = """
        ToolboxCaseIdentity{case_id:string,environment:string,transaction_id:string,transaction_number:string}
        ToolboxDiskAbandonment{actor:string,at:string,reason:string}
        ToolboxDiskAsset{byte_length:int64,sha256:string}
        ToolboxDiskIntent{actor:string,assets:array<ToolboxDiskAsset>?,bundle:ToolboxDiskAsset,case:ToolboxCaseIdentity,contract:string,expected_parent:ToolboxDiskParent?,operation_id:string,prepared_at:string,proposed_revision_id:string,schema_version:int32,store_id:string}
        ToolboxDiskJournal{abandonment:ToolboxDiskAbandonment?,contract:string,previous_pointer:ToolboxDiskPointer?,revision:ToolboxDiskRevision,schema_version:int32,state:string}
        ToolboxDiskOwner{case:ToolboxCaseIdentity,contract:string,schema_version:int32,storage_mode:string,store_id:string}
        ToolboxDiskParent{revision_digest:string,revision_id:string,sequence:int64}
        ToolboxDiskPointer{case:ToolboxCaseIdentity,contract:string,revision_digest:string,revision_id:string,schema_version:int32,sequence:int64,store_id:string}
        ToolboxDiskReceipt{case:ToolboxCaseIdentity,contract:string,operation_id:string,request_digest:string,revision_digest:string,revision_id:string,schema_version:int32,sequence:int64,store_id:string}
        ToolboxDiskRevision{actor:string,assets:array<ToolboxDiskAsset>?,bundle:ToolboxDiskAsset,case:ToolboxCaseIdentity,contract:string,operation_id:string,parent:ToolboxDiskParent?,prepared_at:string,request_digest:string,revision_digest:string,revision_id:string,schema_version:int32,sequence:int64,store_id:string}
        """;

    public static void ReflectedFieldTypeAndNullabilityManifestIsPinned()
    {
        var types = new[] { typeof(ToolboxCaseIdentity), typeof(ToolboxDiskAbandonment), typeof(ToolboxDiskAsset),
            typeof(ToolboxDiskIntent), typeof(ToolboxDiskJournal), typeof(ToolboxDiskOwner), typeof(ToolboxDiskParent),
            typeof(ToolboxDiskPointer), typeof(ToolboxDiskReceipt), typeof(ToolboxDiskRevision) };
        var nullability = new NullabilityInfoContext();
        var actual = string.Join("\n", types.OrderBy(t => t.Name, StringComparer.Ordinal).Select(type =>
            type.Name + "{" + string.Join(",", type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(p => System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name), StringComparer.Ordinal)
                .Select(p => System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name) + ":" + TypeShape(p.PropertyType)
                    + (Nullable.GetUnderlyingType(p.PropertyType) is not null || nullability.Create(p).ReadState == NullabilityState.Nullable ? "?" : ""))) + "}"));
        var expected = ShapeGolden.Replace("\r\n", "\n", StringComparison.Ordinal);
        TestAssert.Equal(expected, actual, "Disk v1 fields, scalar types and nullable bindings require explicit review before changes.");
        TestAssert.True(types.ToHashSet().SetEquals(ToolboxPrototypeDiskContract.Fields.Keys), "Public disk manifest admits exactly the pinned record types.");
        var expectedRows = expected.Split('\n');
        foreach (var type in types)
        {
            var row = expectedRows.Single(line => line.StartsWith(type.Name + "{", StringComparison.Ordinal));
            var fields = row[(type.Name.Length + 1)..^1].Split(',').Select(field => field.Split(':')[0]);
            TestAssert.True(fields.SequenceEqual(ToolboxPrototypeDiskContract.Fields[type]), "Public field manifest must match independent literal: " + type.Name);
        }
        TestAssert.Equal("79a4a5c4f05fa4ef50358320bc81d5fe9ac9312213528eb750ed31742544212e", IndependentHash(Utf8(actual)),
            "Disk v1 reflected manifest SHA-256 must remain pinned.");
    }

    private static string TypeShape(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int32";
        if (type == typeof(long)) return "int64";
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
            return "array<" + TypeShape(type.GenericTypeArguments[0]) + ">";
        return type.Name;
    }

    public static void IndependentGoldenBytesAndDigests()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Golden(Owner(), OwnerGolden, "7416d20e4f51489bdf63a048aebedf6a7e19a2dbc388368f116e63b2fc60de28");
            Golden(Intent(), IntentGolden, "84a8592b380d2f9689cd983c53e364d69688d12e241998b17f78fd91bc7eeb9c");
            TestAssert.Equal("84a8592b380d2f9689cd983c53e364d69688d12e241998b17f78fd91bc7eeb9c",
                ToolboxPrototypeDiskContract.RequestDigest(Intent()), "Request digest must match independent fixture.");
            var restored = ToolboxPrototypeDiskContract.Decode<ToolboxDiskIntent>(Utf8(IntentGolden));
            TestAssert.Equal(Intent().Actor, restored.Actor, "Escaping must preserve exact actor text.");
            TestAssert.Equal(Timestamp, restored.PreparedAt, "Timestamp offset and precision must survive.");
            var reordered = new JsonObject(Node(OwnerGolden).Reverse().Select(p =>
                new KeyValuePair<string, JsonNode?>(p.Key, p.Value?.DeepClone())));
            TestAssert.True(Utf8(OwnerGolden).SequenceEqual(ToolboxPrototypeDiskContract.Encode(
                ToolboxPrototypeDiskContract.Decode<ToolboxDiskOwner>(Utf8(reordered.ToJsonString())))),
                "Input property order must not change canonical bytes.");
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }

    public static void StrictRequiredDuplicateUnknownAndNullFields()
    {
        var revision = ToolboxPrototypeDiskContract.Revision(Intent());
        StrictFields(Owner());
        StrictFields(Intent(), "expected_parent", "assets");
        StrictFields(revision, "parent", "assets");
        StrictFields(ToolboxPrototypeDiskContract.Pointer(revision));
        StrictFields(Journal("prepared"), "previous_pointer", "abandonment");
        StrictFields(ToolboxPrototypeDiskContract.Receipt(revision));
        StrictFields(Intent().ExpectedParent!);
        StrictFields(Intent().Bundle);
        StrictFields(Identity());
        StrictFields(Audit());

        foreach (var path in new[] { "case", "bundle", "expected_parent" })
        {
            var root = Node(IntentGolden);
            var nested = root[path]!.AsObject();
            nested["unexpected"] = 1;
            RejectDecode<ToolboxDiskIntent>(root.ToJsonString(), "unknown_field");
        }
        RejectDecode<ToolboxDiskIntent>(IntentGolden.Replace("\"byte_length\":3", "\"byte_length\":3,\"byte_length\":3", StringComparison.Ordinal), "duplicate_field");
        RejectDecode<ToolboxDiskOwner>(OwnerGolden.Replace("\"case_id\":\"Case-1\"", "\"case_id\":\"Case-1\",\"CaseId\":\"Case-1\"", StringComparison.Ordinal), "unknown_field");
        RejectDecode<ToolboxDiskIntent>(Mutate(IntentGolden, n => n["assets"] = new JsonArray((JsonNode?)null)));
    }

    public static void MalformedJsonTypesAndIntegers()
    {
        foreach (var json in new[] { "", "{", "null", "[]", "true", "1", OwnerGolden + "{}",
            OwnerGolden.Replace("\"schema_version\":1", "\"schema_version\":1,", StringComparison.Ordinal), "/*comment*/" + OwnerGolden })
            RejectDecode<ToolboxDiskOwner>(json);
        Reject(() => ToolboxPrototypeDiskContract.Decode<ToolboxDiskOwner>(new byte[] { 0xc3, 0x28 }), "invalid_metadata");
        Reject(() => ToolboxPrototypeDiskContract.Decode<ToolboxDiskOwner>(new byte[] { 0xef, 0xbb, 0xbf }.Concat(Utf8(OwnerGolden)).ToArray()), "invalid_metadata");
        foreach (var value in new[] { "\"1\"", "true", "[]", "{}", "1.5", "2147483648", "-2147483649", "1e100" })
            RejectDecode<ToolboxDiskOwner>(Mutate(OwnerGolden, n => n["schema_version"] = JsonNode.Parse(value)));
        foreach (var value in new[] { "\"7\"", "false", "[]", "{}", "7.5", "9223372036854775808", "-9223372036854775809" })
            RejectDecode<ToolboxDiskIntent>(Mutate(IntentGolden, n => n["expected_parent"]!["sequence"] = JsonNode.Parse(value)));
        foreach (var field in new[] { "actor", "prepared_at", "store_id" })
            foreach (var value in new[] { "1", "true", "[]", "{}" })
                RejectDecode<ToolboxDiskIntent>(Mutate(IntentGolden, n => n[field] = JsonNode.Parse(value)));
        RejectDecode<ToolboxDiskIntent>(Mutate(IntentGolden, n => n["assets"] = new JsonObject()));
        RejectDecode<ToolboxDiskIntent>(Mutate(IntentGolden, n => n["bundle"]!["byte_length"] = -1));
        RejectDecode<ToolboxDiskIntent>(Mutate(IntentGolden, n => n["expected_parent"]!["sequence"] = 0));
    }

    public static void StrictVersionsEnumsIdsAndRevisionChecksums()
    {
        foreach (var version in new[] { 0, 2, -1 })
        {
            RejectDecode<ToolboxDiskOwner>(Mutate(OwnerGolden, n => n["schema_version"] = version), "unsupported_contract");
            Reject(() => ToolboxPrototypeDiskContract.Encode(Intent() with { SchemaVersion = version }), "unsupported_contract");
        }
        Reject(() => ToolboxPrototypeDiskContract.Encode(Owner() with { Contract = "toolbox_prototype_disk_v2" }), "unsupported_contract");
        Reject(() => ToolboxPrototypeDiskContract.Encode(Owner() with { StorageMode = "prototype_bundle" }));
        foreach (var id in new[] { "", "../owner.json", new string('A', 32), new string('g', 32), new string('1', 31), new string('1', 33), "11111111-1111-1111-1111-111111111111" })
            Reject(() => ToolboxPrototypeDiskContract.Encode(Owner() with { StoreId = id }), "invalid_id");
        foreach (var hash in new[] { "", new string('A', 64), new string('z', 64), new string('a', 63), new string('a', 65) })
            Reject(() => ToolboxPrototypeDiskContract.Encode(Intent() with { Bundle = new(hash, 3) }), "invalid_digest");
        foreach (var text in new[] { "", " ", new string('x', 4097) })
            Reject(() => ToolboxPrototypeDiskContract.Encode(Intent() with { Actor = text }));
        Reject(() => ToolboxPrototypeDiskContract.Encode(Intent() with { PreparedAt = "2026-10-11" }));
        Reject(() => ToolboxPrototypeDiskContract.Encode(Intent() with { Case = Identity() with { TransactionId = "" } }));
        var revision = ToolboxPrototypeDiskContract.Revision(Intent());
        Reject(() => ToolboxPrototypeDiskContract.Encode(revision with { RequestDigest = ParentHash }), "checksum_mismatch");
        Reject(() => ToolboxPrototypeDiskContract.Encode(revision with { RevisionDigest = ParentHash }), "checksum_mismatch");
        Reject(() => ToolboxPrototypeDiskContract.Encode(revision with { Sequence = revision.Sequence + 1 }));
        foreach (var changed in new[] { revision with { Contract = "foreign" }, revision with { SchemaVersion = 2 } })
        {
            var resealed = changed with { RevisionDigest = ToolboxPrototypeDiskContract.RevisionDigest(changed) };
            Reject(() => ToolboxPrototypeDiskContract.Encode(resealed), "unsupported_contract");
            RejectDecode<ToolboxDiskRevision>(Mutate(Encoding.UTF8.GetString(ToolboxPrototypeDiskContract.Encode(revision)), n =>
            {
                n["contract"] = resealed.Contract;
                n["schema_version"] = resealed.SchemaVersion;
                n["revision_digest"] = resealed.RevisionDigest;
            }), "unsupported_contract");
        }
        Reject(() => ToolboxPrototypeDiskContract.Encode(Journal("unknown")));
        Reject(() => ToolboxPrototypeDiskContract.Encode(Journal("prepared") with { Abandonment = Audit() }));
        Reject(() => ToolboxPrototypeDiskContract.Encode(Journal("abandoned") with { Abandonment = null }));
    }

    public static void EveryIntentFieldBindsDigestAndRevisionHashScope()
    {
        var intent = Intent();
        var changes = new[]
        {
            intent with { StoreId = ParentId }, intent with { OperationId = ParentId },
            intent with { ProposedRevisionId = ParentId }, intent with { Actor = intent.Actor + " " },
            intent with { PreparedAt = "2026-10-11T07:04:56.1234567+00:00" },
            intent with { Case = intent.Case with { Environment = "test" } },
            intent with { Case = intent.Case with { CaseId = "Case-2" } },
            intent with { Case = intent.Case with { TransactionId = "TR-other" } },
            intent with { Case = intent.Case with { TransactionNumber = "TR-2" } },
            intent with { ExpectedParent = null },
            intent with { ExpectedParent = intent.ExpectedParent! with { RevisionId = StoreId } },
            intent with { ExpectedParent = intent.ExpectedParent! with { RevisionDigest = AbcHash } },
            intent with { ExpectedParent = intent.ExpectedParent! with { Sequence = 8 } },
            intent with { Bundle = intent.Bundle with { Sha256 = AHash } },
            intent with { Bundle = intent.Bundle with { ByteLength = 4 } },
            intent with { Assets = intent.Assets!.Value.Reverse().ToImmutableArray() },
            intent with { Assets = [new(EmptyHash, 1), new(AHash, 1)] },
            intent with { Assets = null }, intent with { Assets = ImmutableArray<ToolboxDiskAsset>.Empty }
        };
        var digest = ToolboxPrototypeDiskContract.RequestDigest(intent);
        foreach (var change in changes)
            TestAssert.False(digest == ToolboxPrototypeDiskContract.RequestDigest(change), "Every changed request binding must change its digest.");
        var revision = ToolboxPrototypeDiskContract.Revision(intent);
        var canonical = Node(Encoding.UTF8.GetString(ToolboxPrototypeDiskContract.Encode(revision)));
        canonical.Remove("revision_digest");
        TestAssert.Equal(IndependentHash(Utf8(canonical.ToJsonString())), revision.RevisionDigest,
            "Revision hash includes all canonical fields except revision_digest.");
        TestAssert.Equal(revision.RevisionDigest, ToolboxPrototypeDiskContract.RevisionDigest(revision with { RevisionDigest = ParentHash }),
            "Self digest must be excluded.");
        TestAssert.False(revision.RevisionDigest == ToolboxPrototypeDiskContract.RevisionDigest(revision with { RequestDigest = ParentHash }),
            "Request digest must be included.");
        foreach (var state in new[] { "prepared", "sealed", "published", "committed", "abandoned" })
            TestAssert.Equal(revision.RevisionDigest, Journal(state).Revision.RevisionDigest, "Journal progress/audit must not change revision hash.");
    }

    public static void NullEmptyDefaultAndDuplicateAssets()
    {
        var absent = Intent() with { Assets = null };
        var empty = absent with { Assets = ImmutableArray<ToolboxDiskAsset>.Empty };
        TestAssert.False(ToolboxPrototypeDiskContract.Same(absent, empty), "Null and empty inventories are distinct requests.");
        TestAssert.False(ToolboxPrototypeDiskContract.RequestDigest(absent) == ToolboxPrototypeDiskContract.RequestDigest(empty), "Null and empty hashes differ.");
        TestAssert.True(ToolboxPrototypeDiskContract.Decode<ToolboxDiskIntent>(ToolboxPrototypeDiskContract.Encode(absent)).Assets is null, "Null inventory survives.");
        TestAssert.True(ToolboxPrototypeDiskContract.Decode<ToolboxDiskIntent>(ToolboxPrototypeDiskContract.Encode(empty)).Assets is { IsEmpty: true }, "Empty inventory survives.");
        Reject(() => ToolboxPrototypeDiskContract.Encode(absent with { Assets = default(ImmutableArray<ToolboxDiskAsset>) }), "limit_exceeded");
        Reject(() => ToolboxPrototypeDiskContract.Encode(absent with { Assets = [new(AHash, 1), new(AHash, 1)] }), "duplicate_asset");
        Reject(() => Prepare(assets: new ReadOnlyMemory<byte>[] { Utf8("a"), Utf8("a") }), "duplicate_asset");
        TestAssert.True(Prepare().Assets is null, "Absent prepared assets stay null.");
        TestAssert.True(Prepare(assets: Array.Empty<ReadOnlyMemory<byte>>()).Assets is { IsEmpty: true }, "Explicit empty prepared assets stay empty.");
    }

    public static void PreparedBytesAreOwnedAndSequenceOverflowIsTyped()
    {
        var bundle = Utf8("abc");
        var asset = Utf8("a");
        var prepared = Prepare(bundle, new ReadOnlyMemory<byte>[] { asset });
        bundle[0] = (byte)'z'; asset[0] = (byte)'z';
        TestAssert.True(prepared.Bundle.SequenceEqual(Utf8("abc")), "Frozen bundle must own its bytes.");
        TestAssert.True(prepared.Assets!.Value[0].SequenceEqual(Utf8("a")), "Frozen assets must own their bytes.");
        TestAssert.Equal(AbcHash, prepared.Intent.Bundle.Sha256, "Bundle inventory derives from owned bytes.");
        TestAssert.Equal(AHash, prepared.Intent.Assets!.Value[0].Sha256, "Asset inventory derives from owned bytes.");
        TestAssert.Equal(1L, ToolboxPrototypeDiskContract.Revision(Intent() with { ExpectedParent = null }).Sequence, "Initial sequence is one.");
        TestAssert.Equal(long.MaxValue, ToolboxPrototypeDiskContract.Revision(Intent() with { ExpectedParent = new(ParentId, ParentHash, long.MaxValue - 1) }).Sequence, "Maximum sequence is valid.");
        var overflow = Intent() with { ExpectedParent = new(ParentId, ParentHash, long.MaxValue) };
        Reject(() => ToolboxPrototypeDiskContract.Revision(overflow), "sequence_overflow");
        Reject(() => ToolboxPrototypeDiskContract.Prepare(StoreId, Identity(), OperationId, RevisionId,
            overflow.ExpectedParent, "actor", DateTimeOffset.ParseExact(Timestamp, "O", CultureInfo.InvariantCulture), Utf8("abc")), "sequence_overflow");
        var revision = ToolboxPrototypeDiskContract.Revision(Intent());
        Reject(() => ToolboxPrototypeDiskContract.Validate(revision with { Parent = overflow.ExpectedParent }), "sequence_overflow");
    }

    public static void MetadataDepthTextAndLimitConfigurationBounds()
    {
        var bytes = Utf8(OwnerGolden);
        _ = ToolboxPrototypeDiskContract.Decode<ToolboxDiskOwner>(bytes, new(MetadataBytes: bytes.Length));
        Reject(() => ToolboxPrototypeDiskContract.Decode<ToolboxDiskOwner>(bytes, new(MetadataBytes: bytes.Length - 1)), "limit_exceeded");
        _ = ToolboxPrototypeDiskContract.Decode<ToolboxDiskOwner>(bytes, new(JsonDepth: 2));
        Reject(() => ToolboxPrototypeDiskContract.Decode<ToolboxDiskOwner>(bytes, new(JsonDepth: 1)));
        _ = ToolboxPrototypeDiskContract.Encode(Intent() with { Actor = new string('x', 4096) });
        RejectDecode<ToolboxDiskIntent>(Mutate(IntentGolden, n => n["actor"] = new string('x', 4097)));
        new ToolboxDiskLimits().Validate();
        new ToolboxDiskLimits(Assets: 0).Validate();
        var defaults = new ToolboxDiskLimits();
        foreach (var invalid in new[]
        {
            defaults with { MetadataBytes = 0 }, defaults with { MetadataBytes = defaults.MetadataBytes + 1 },
            defaults with { Assets = -1 }, defaults with { Assets = defaults.Assets + 1 },
            defaults with { AssetBytes = 0 }, defaults with { AssetBytes = defaults.AssetBytes + 1 },
            defaults with { RevisionBytes = 0 }, defaults with { RevisionBytes = defaults.RevisionBytes + 1 },
            defaults with { Records = 0 }, defaults with { Records = defaults.Records + 1 },
            defaults with { TraversalBytes = 0 }, defaults with { TraversalBytes = defaults.TraversalBytes + 1 },
            defaults with { JsonDepth = 0 }, defaults with { JsonDepth = defaults.JsonDepth + 1 },
            defaults with { DirectoryDepth = 0 }, defaults with { DirectoryDepth = defaults.DirectoryDepth + 1 },
            defaults with { Entries = 0 }, defaults with { Entries = defaults.Entries + 1 }
        }) Reject(invalid.Validate, "invalid_limits");
    }

    public static void PreparationAndDecodeChargeLoweredInventoryBudgets()
    {
        _ = Prepare(assets: new ReadOnlyMemory<byte>[] { Utf8("a"), Utf8("b") }, limits: new(Assets: 2, AssetBytes: 1, RevisionBytes: 5));
        Reject(() => Prepare(assets: new ReadOnlyMemory<byte>[] { Utf8("a") }, limits: new(Assets: 0)), "limit_exceeded");
        Reject(() => Prepare(assets: new ReadOnlyMemory<byte>[] { Utf8("ab") }, limits: new(AssetBytes: 1)), "limit_exceeded");
        Reject(() => Prepare(limits: new(RevisionBytes: 2)), "limit_exceeded");
        Reject(() => Prepare(assets: new ReadOnlyMemory<byte>[] { Utf8("a"), Utf8("b") }, limits: new(RevisionBytes: 4)), "limit_exceeded");
        Reject(() => Prepare(limits: new(MetadataBytes: 1)), "limit_exceeded");
        _ = ToolboxPrototypeDiskContract.Decode<ToolboxDiskIntent>(Utf8(IntentGolden), new(Assets: 2, AssetBytes: 1, RevisionBytes: 4));
        Reject(() => ToolboxPrototypeDiskContract.Decode<ToolboxDiskIntent>(Utf8(IntentGolden), new(Assets: 1)), "limit_exceeded");
        Reject(() => ToolboxPrototypeDiskContract.Decode<ToolboxDiskIntent>(Utf8(IntentGolden), new(RevisionBytes: 3)), "limit_exceeded");
        var twoBytes = Intent() with { Assets = [new(AHash, 2)] };
        Reject(() => ToolboxPrototypeDiskContract.Decode<ToolboxDiskIntent>(ToolboxPrototypeDiskContract.Encode(twoBytes), new(AssetBytes: 1)), "limit_exceeded");
        var tooMany = Intent() with { Assets = Enumerable.Range(1, 257).Select(i => new ToolboxDiskAsset(i.ToString("x64", CultureInfo.InvariantCulture), 0)).ToImmutableArray() };
        Reject(() => ToolboxPrototypeDiskContract.Encode(tooMany), "limit_exceeded");
        var aggregate = Intent() with { Assets = Enumerable.Range(1, 8).Select(i => new ToolboxDiskAsset(i.ToString("x64", CultureInfo.InvariantCulture), 64L * 1024 * 1024)).ToImmutableArray() };
        Reject(() => ToolboxPrototypeDiskContract.Encode(aggregate), "limit_exceeded");
    }

    public static void JournalBindsExactOwnershipAndPreviousParent()
    {
        var journal = Journal("prepared");
        var previous = journal.PreviousPointer!;
        foreach (var changed in new[]
        {
            previous with { StoreId = ParentId }, previous with { Case = Identity() with { CaseId = "foreign" } },
            previous with { RevisionId = StoreId }, previous with { RevisionDigest = AHash }, previous with { Sequence = 6 }
        }) Reject(() => ToolboxPrototypeDiskContract.Encode(journal with { PreviousPointer = changed }), "parent_conflict");
        Reject(() => ToolboxPrototypeDiskContract.Encode(journal with { PreviousPointer = null }), "parent_conflict");
        var initial = Journal("prepared", initial: true);
        _ = ToolboxPrototypeDiskContract.Encode(initial);
        Reject(() => ToolboxPrototypeDiskContract.Encode(initial with { PreviousPointer = previous }), "parent_conflict");
        foreach (var state in new[] { "prepared", "sealed", "published", "committed", "abandoned" })
        {
            var value = Journal(state);
            TestAssert.True(ToolboxPrototypeDiskContract.Same(value, ToolboxPrototypeDiskContract.Decode<ToolboxDiskJournal>(ToolboxPrototypeDiskContract.Encode(value))), "Journal state round trip must preserve all bindings.");
        }
    }

    public static void PlannerResumeAndParentDecisionTable()
    {
        var baseline = Observation();
        var cases = new (ToolboxDiskObservation Observation, string State, string Code, bool Verified, string[] Actions)[]
        {
            (baseline, "pending", "incomplete_staging", false, ["abandon"]),
            (baseline with { StagingExists = true }, "pending", "incomplete_staging", false, ["abandon"]),
            (baseline with { StagingExists = true, StagingComplete = true }, "pending", "resume_required", false, ["seal", "publish", "abandon"]),
            (baseline with { SealedExists = true, SealedComplete = true }, "pending", "resume_required", true, ["publish", "abandon"]),
            (baseline with { SealedExists = true }, "quarantined", "partial_revision", false, []),
            (baseline with { Current = null }, "quarantined", "parent_conflict", false, []),
            (baseline with { Current = baseline.Current! with { Sequence = 8 } }, "quarantined", "parent_conflict", false, []),
            (baseline with { Current = baseline.Current! with { RevisionDigest = AHash } }, "quarantined", "parent_conflict", false, []),
            (baseline with { Current = baseline.Current! with { StoreId = ParentId } }, "quarantined", "parent_conflict", false, []),
            (baseline with { Current = baseline.Current! with { Case = Identity() with { CaseId = "foreign" } } }, "quarantined", "parent_conflict", false, []),
            (Observation(initial: true), "pending", "incomplete_staging", false, ["abandon"]),
            (Observation(initial: true) with { Current = baseline.Current }, "quarantined", "parent_conflict", false, [])
        };
        foreach (var row in cases) AssertPlan(row.Observation, row.State, row.Code, row.Verified, false, row.Actions);
    }

    public static void PlannerPublicationReceiptsHistoricalTailsAndTrust()
    {
        var revision = Journal("committed").Revision;
        var published = Observation() with { Current = ToolboxPrototypeDiskContract.Pointer(revision), OnCurrentAncestry = true,
            SealedExists = true, SealedComplete = true };
        foreach (var state in new[] { "prepared", "sealed", "published", "committed" })
            AssertPlan(published with { Journal = Journal(state) }, "pending", "recovery_required", true, false, "complete_tail");
        var committed = published with { Journal = Journal("committed"), ReceiptExists = true, ReceiptMatches = true };
        AssertPlan(committed, "verified", "committed", true, true);
        AssertPlan(committed with { PolicyTrusted = false }, "verified", "policy_inspection_required", true, false);
        AssertPlan(committed with { SealedComplete = false }, "corrupt", "published_bytes_missing", false, false);
        AssertPlan(committed with { ReceiptMatches = false }, "corrupt", "receipt_conflict", false, false);
        var successor = ToolboxPrototypeDiskContract.Revision(Intent() with { OperationId = ParentId, ProposedRevisionId = StoreId,
            ExpectedParent = ToolboxPrototypeDiskContract.Parent(committed.Current!) });
        var historical = committed with { Current = ToolboxPrototypeDiskContract.Pointer(successor) };
        AssertPlan(historical, "verified", "committed", true, true);
        AssertPlan(historical with { ReceiptExists = false, ReceiptMatches = false }, "pending", "recovery_required", true, false, "complete_tail");
        AssertPlan(historical with { Journal = Journal("published") }, "pending", "recovery_required", true, false, "complete_tail");
        AssertPlan(historical with { OnCurrentAncestry = false }, "quarantined", "publication_unproven", false, false);
        TestAssert.Equal(successor.RevisionId, historical.Current!.RevisionId, "Historical recovery planning cannot move current.");
        AssertPlan(committed with { ReceiptExists = false, ReceiptMatches = true }, "quarantined", "inconsistent_observation", false, false);
    }

    public static void PlannerAmbiguousAbandonedAndUnprovenPublication()
    {
        var baseline = Observation();
        AssertPlan(baseline with { AmbiguousArtifacts = true }, "quarantined", "ambiguous_artifacts", false, false);
        AssertPlan(baseline with { StagingExists = true, StagingComplete = true, SealedExists = true, SealedComplete = true }, "quarantined", "ambiguous_artifacts", false, false);
        AssertPlan(baseline with { StagingExists = true, SealedExists = true }, "quarantined", "ambiguous_artifacts", false, false);
        foreach (var state in new[] { "published", "committed" })
            AssertPlan(baseline with { Journal = Journal(state), SealedExists = true, SealedComplete = true }, "quarantined", "publication_unproven", false, false);
        AssertPlan(baseline with { ReceiptExists = true, ReceiptMatches = true }, "quarantined", "publication_unproven", false, false);
        AssertPlan(baseline with { ReceiptExists = true }, "corrupt", "receipt_conflict", false, false);
        var abandoned = baseline with { Journal = Journal("abandoned") };
        AssertPlan(abandoned, "verified", "operation_abandoned", false, false);
        AssertPlan(abandoned with { StagingExists = true }, "verified", "operation_abandoned", false, false);
        AssertPlan(abandoned with { OnCurrentAncestry = true }, "quarantined", "abandoned_publication", false, false);
        AssertPlan(abandoned with { ReceiptExists = true, ReceiptMatches = true }, "quarantined", "abandoned_publication", false, false);
        var bytes = ToolboxPrototypeDiskContract.Encode(abandoned.Journal);
        _ = ToolboxPrototypeRecovery.Plan(abandoned);
        _ = ToolboxPrototypeRecovery.Plan(abandoned);
        TestAssert.True(bytes.SequenceEqual(ToolboxPrototypeDiskContract.Encode(abandoned.Journal)), "Repeated planning cannot mutate abandonment audit or intent.");
    }

    public static void PlannerRejectsContradictoryPhysicalAndOwnershipEvidence()
    {
        var baseline = Observation();
        RejectPlan(baseline with { SealedComplete = true });
        RejectPlan(baseline with { StagingComplete = true });
        var published = baseline with { Current = ToolboxPrototypeDiskContract.Pointer(baseline.Journal.Revision),
            OnCurrentAncestry = true, SealedExists = true, SealedComplete = true,
            Journal = Journal("committed"), ReceiptExists = true, ReceiptMatches = true };
        RejectPlan(published with { Current = null });
        RejectPlan(published with { Current = published.Current! with { StoreId = ParentId } });
        RejectPlan(published with { Current = published.Current! with { Case = Identity() with { Environment = "foreign" } } });
        RejectPlan(published with { Current = published.Current! with { Sequence = 0 } });
        RejectPlan(published with { Current = baseline.Current });
    }

    public static void EveryJournalTransitionAndTerminalState()
    {
        var states = new[] { "prepared", "sealed", "published", "committed", "abandoned", "unknown", "" };
        var legal = new HashSet<(string, string)> { ("prepared", "sealed"), ("sealed", "published"),
            ("published", "committed"), ("prepared", "abandoned"), ("sealed", "abandoned") };
        foreach (var before in states)
            foreach (var after in states)
                TestAssert.Equal(legal.Contains((before, after)), ToolboxPrototypeRecovery.TransitionAllowed(before, after),
                    $"Journal transition {before} -> {after} must follow the explicit table.");
    }

    public static void StoreInitializationRequiresOwnerLockAndLayout()
    {
        var empty = new ToolboxDiskStoreObservation(true, true, true, false, false, false, 0, false);
        AssertStorePlan(empty, "empty", "empty", false, false);
        AssertStorePlan(empty with { PolicyTrusted = true }, "empty", "empty", false, false);
        foreach (var invalid in new[] { empty with { OwnerValid = false }, empty with { LockPresent = false },
            empty with { LayoutValid = false }, empty with { OwnerValid = false, LockPresent = false, LayoutValid = false },
            empty with { OwnerValid = false, CurrentPresent = true, CurrentVerified = true, PolicyTrusted = true } })
            AssertStorePlan(invalid, "corrupt", "initialization_incomplete", false, false);
    }

    public static void StorePendingCompetitionCurrentAndPolicyTable()
    {
        var verified = new ToolboxDiskStoreObservation(true, true, true, true, true, true, 0, false);
        AssertStorePlan(verified, "verified", "verified", true, true);
        AssertStorePlan(verified with { PolicyTrusted = false }, "verified", "policy_inspection_required", true, false);
        AssertStorePlan(verified with { CurrentVerified = false }, "corrupt", "published_bytes_missing", false, false);
        AssertStorePlan(verified with { PendingIntents = 1 }, "pending", "recovery_required", true, false);
        AssertStorePlan(verified with { ReplacementArtifacts = true }, "pending", "recovery_required", true, false);
        AssertStorePlan(verified with { PendingIntents = 1, PolicyTrusted = false }, "pending", "recovery_required", true, false);
        AssertStorePlan(verified with { CurrentPresent = false, CurrentVerified = false, PendingIntents = 1 }, "pending", "recovery_required", false, false);
        AssertStorePlan(verified with { CurrentPresent = false, CurrentVerified = false, ReplacementArtifacts = true }, "pending", "recovery_required", false, false);
        foreach (var count in new[] { -1, 2, 10_000, int.MaxValue })
            AssertStorePlan(verified with { PendingIntents = count }, "quarantined", "competing_intents", false, false);
        var impossible = ToolboxPrototypeRecovery.PlanStore(verified with { CurrentPresent = false });
        TestAssert.True(impossible.State is "quarantined" or "corrupt", "Absent current cannot simultaneously be verified.");
        TestAssert.False(impossible.Writable, "Inconsistent store evidence cannot grant writes.");
    }

    public static void AncestryWalkIsBoundedOwnedAndExact()
    {
        var chain = Chain(128);
        var current = ToolboxPrototypeDiskContract.Pointer(chain[^1]);
        var records = chain.ToDictionary(r => r.RevisionId, StringComparer.Ordinal);
        var pointerBefore = ToolboxPrototypeDiskContract.Encode(current);
        var tailBefore = ToolboxPrototypeDiskContract.Encode(chain[^1]);
        var verified = ToolboxPrototypeRecovery.VerifyAncestry(current, records, chain.Length);
        TestAssert.True(verified.SetEquals(records.Keys), "Verified ancestry contains exactly the current chain.");
        TestAssert.True(verified.KeyComparer.Equals(StringComparer.Ordinal), "Ancestry IDs use ordinal comparison.");
        TestAssert.True(pointerBefore.SequenceEqual(ToolboxPrototypeDiskContract.Encode(current)), "Traversal cannot mutate current.");
        TestAssert.True(tailBefore.SequenceEqual(ToolboxPrototypeDiskContract.Encode(chain[^1])), "Traversal cannot rewrite immutable revisions.");
        records.Clear();
        TestAssert.Equal(chain.Length, verified.Count, "Result owns its set independently of caller inventory.");
        records = chain.ToDictionary(r => r.RevisionId, StringComparer.Ordinal);
        Reject(() => ToolboxPrototypeRecovery.VerifyAncestry(current, records, chain.Length - 1), "ancestry_missing");
        foreach (var maximum in new[] { 0, -1, 10_001, int.MaxValue })
            Reject(() => ToolboxPrototypeRecovery.VerifyAncestry(current, records, maximum), "invalid_limits");
        var unrelated = ToolboxPrototypeDiskContract.Revision(Intent() with { ProposedRevisionId = ParentId, ExpectedParent = null });
        records.Add(unrelated.RevisionId, unrelated);
        TestAssert.False(ToolboxPrototypeRecovery.VerifyAncestry(current, records).Contains(unrelated.RevisionId), "Off-chain sealed records are never adopted.");
    }

    public static void AncestryMissingMismatchForeignSequenceAndCyclesFail()
    {
        var chain = Chain(3);
        var current = ToolboxPrototypeDiskContract.Pointer(chain[^1]);
        var records = chain.ToDictionary(r => r.RevisionId, StringComparer.Ordinal);
        Reject(() => ToolboxPrototypeRecovery.VerifyAncestry(current, new Dictionary<string, ToolboxDiskRevision>()), "ancestry_missing");
        var missingParent = new Dictionary<string, ToolboxDiskRevision>(records, StringComparer.Ordinal);
        missingParent.Remove(chain[0].RevisionId);
        Reject(() => ToolboxPrototypeRecovery.VerifyAncestry(current, missingParent), "ancestry_missing");
        Reject(() => ToolboxPrototypeRecovery.VerifyAncestry(current with { RevisionDigest = AHash }, records), "ancestry_conflict");
        Reject(() => ToolboxPrototypeRecovery.VerifyAncestry(current with { Sequence = current.Sequence + 1 }, records), "ancestry_conflict");
        Reject(() => ToolboxPrototypeRecovery.VerifyAncestry(current with { StoreId = ParentId }, records), "ancestry_conflict");
        foreach (var identity in new[] { Identity() with { Environment = "foreign" }, Identity() with { CaseId = "foreign" },
            Identity() with { TransactionId = "foreign" }, Identity() with { TransactionNumber = "foreign" } })
            Reject(() => ToolboxPrototypeRecovery.VerifyAncestry(current with { Case = identity }, records), "ancestry_conflict");
        var swapped = new Dictionary<string, ToolboxDiskRevision>(records, StringComparer.Ordinal) { [current.RevisionId] = chain[0] };
        Reject(() => ToolboxPrototypeRecovery.VerifyAncestry(current, swapped), "ancestry_conflict");
        var corrupt = new Dictionary<string, ToolboxDiskRevision>(records, StringComparer.Ordinal) { [current.RevisionId] = chain[^1] with { RequestDigest = AHash } };
        Reject(() => ToolboxPrototypeRecovery.VerifyAncestry(current, corrupt), "checksum_mismatch");
        var badDigestParent = ToolboxPrototypeDiskContract.Revision(Intent() with { ProposedRevisionId = ParentId,
            ExpectedParent = new(chain[0].RevisionId, AHash, 1) });
        var badSequenceParent = ToolboxPrototypeDiskContract.Revision(Intent() with { ProposedRevisionId = ParentId,
            ExpectedParent = new(chain[0].RevisionId, chain[0].RevisionDigest, 7) });
        foreach (var bad in new[] { badDigestParent, badSequenceParent })
        {
            var inventory = new Dictionary<string, ToolboxDiskRevision>(records, StringComparer.Ordinal) { [bad.RevisionId] = bad };
            Reject(() => ToolboxPrototypeRecovery.VerifyAncestry(ToolboxPrototypeDiskContract.Pointer(bad), inventory), "ancestry_conflict");
        }
        var cycle = ToolboxPrototypeDiskContract.Revision(Intent() with { ExpectedParent = new(RevisionId, ParentHash, 1) });
        Reject(() => ToolboxPrototypeRecovery.VerifyAncestry(ToolboxPrototypeDiskContract.Pointer(cycle),
            new Dictionary<string, ToolboxDiskRevision> { [cycle.RevisionId] = cycle }), "ancestry_missing");
        Reject(() => ToolboxPrototypeRecovery.VerifyAncestry(current with { Sequence = 0 }, records));
    }

    private static ToolboxDiskRevision[] Chain(int count)
    {
        var chain = new ToolboxDiskRevision[count];
        ToolboxDiskParent? parent = null;
        for (var index = 0; index < count; index++)
        {
            var revision = ToolboxPrototypeDiskContract.Revision(Intent() with { ExpectedParent = parent,
                ProposedRevisionId = (index + 1).ToString("x32", CultureInfo.InvariantCulture),
                OperationId = (index + count + 1).ToString("x32", CultureInfo.InvariantCulture) });
            chain[index] = revision;
            parent = ToolboxPrototypeDiskContract.Parent(ToolboxPrototypeDiskContract.Pointer(revision));
        }
        return chain;
    }

    private static void AssertStorePlan(ToolboxDiskStoreObservation observation, string state, string code, bool verified, bool writable)
    {
        var plan = ToolboxPrototypeRecovery.PlanStore(observation);
        TestAssert.Equal(state, plan.State, "Store classification.");
        TestAssert.Equal(code, plan.Code, "Store diagnostic.");
        TestAssert.Equal(verified, plan.TransportVerified, "Store transport evidence.");
        TestAssert.Equal(writable, plan.Writable, "Store writer authority.");
        TestAssert.True(plan.Actions.IsEmpty, "Store observation cannot execute journal operations.");
    }

    private static ToolboxCaseIdentity Identity() => new("Test", "Case-1", "TR-id", "TR-1");
    private static ToolboxDiskOwner Owner() => new(ToolboxPrototypeDiskContract.Contract, 1, Identity(), StoreId, "prototype_store");
    private static ToolboxDiskIntent Intent() => new(ToolboxPrototypeDiskContract.Contract, 1, StoreId, Identity(), OperationId,
        RevisionId, new(ParentId, ParentHash, 7), " Alice\n\u00e9 <&> \"Q\"\\ ", Timestamp, new(AbcHash, 3), [new(EmptyHash, 0), new(AHash, 1)]);
    private static ToolboxDiskAbandonment Audit() => new("reviewer", Timestamp, "explicitly abandoned");
    private static ToolboxDiskJournal Journal(string state, bool initial = false)
    {
        var intent = initial ? Intent() with { ExpectedParent = null } : Intent();
        var previous = initial ? null : new ToolboxDiskPointer(ToolboxPrototypeDiskContract.Contract, 1, StoreId, Identity(), ParentId, ParentHash, 7);
        return new(ToolboxPrototypeDiskContract.Contract, 1, ToolboxPrototypeDiskContract.Revision(intent), previous,
            state, state == "abandoned" ? Audit() : null);
    }
    private static ToolboxDiskObservation Observation(bool initial = false)
    {
        var journal = Journal("prepared", initial);
        return new(journal.PreviousPointer, journal, false, false, false, false, false, false, false, true);
    }
    private static ToolboxPreparedSave Prepare(byte[]? bundle = null, IEnumerable<ReadOnlyMemory<byte>>? assets = null, ToolboxDiskLimits? limits = null) =>
        ToolboxPrototypeDiskContract.Prepare(StoreId, Identity(), OperationId, RevisionId, null, "actor",
            DateTimeOffset.ParseExact(Timestamp, "O", CultureInfo.InvariantCulture), bundle ?? Utf8("abc"), assets, limits);
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
    private static string IndependentHash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static JsonObject Node(string json) => JsonNode.Parse(json)!.AsObject();
    private static string Mutate(string json, Action<JsonObject> mutate)
    {
        var node = Node(json); mutate(node); return node.ToJsonString();
    }
    private static void Golden<T>(T record, string literal, string digest) where T : class
    {
        var expected = Utf8(literal);
        TestAssert.Equal(digest, IndependentHash(expected), "Independent fixture digest must remain pinned.");
        var actual = ToolboxPrototypeDiskContract.Encode(record);
        TestAssert.True(expected.SequenceEqual(actual), $"{typeof(T).Name} must match canonical UTF-8 literal exactly.");
        TestAssert.Equal(digest, ToolboxPrototypeDiskContract.Hash(actual), "Encoded bytes must match pinned SHA-256.");
        TestAssert.True(actual.SequenceEqual(ToolboxPrototypeDiskContract.Encode(ToolboxPrototypeDiskContract.Decode<T>(expected))), "Golden record must round trip.");
    }
    private static void StrictFields<T>(T value, params string[] nullable) where T : class
    {
        // Case identity is a nested DTO rather than a standalone disk record.
        string json = value is ToolboxCaseIdentity
            ? System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower })
            : Encoding.UTF8.GetString(ToolboxPrototypeDiskContract.Encode(value));
        foreach (var property in Node(json))
        {
            RejectDecode<T>(Mutate(json, n => n.Remove(property.Key)), "missing_field");
            var duplicate = json[..^1] + ",\"" + property.Key + "\":" + (property.Value?.ToJsonString() ?? "null") + "}";
            RejectDecode<T>(duplicate, "duplicate_field");
            if (!nullable.Contains(property.Key)) RejectDecode<T>(Mutate(json, n => n[property.Key] = null));
        }
        RejectDecode<T>(Mutate(json, n => n["unknown"] = 1), "unknown_field");
        if (Node(json).ContainsKey("contract"))
        {
            RejectDecode<T>(Mutate(json, n => n["contract"] = "unknown_contract"), "unsupported_contract");
            RejectDecode<T>(Mutate(json, n => n["schema_version"] = 2), "unsupported_contract");
        }
    }
    private static void RejectDecode<T>(string json, string? code = null) where T : class =>
        Reject(() => ToolboxPrototypeDiskContract.Decode<T>(Utf8(json)), code);
    private static void Reject(Action action, string? code = null)
    {
        try { action(); }
        catch (ToolboxDiskException error)
        {
            TestAssert.True(error.Code.Length is > 0 and <= 128, "Diagnostic must be bounded and typed.");
            if (code is not null) TestAssert.Equal(code, error.Code, "Rejected metadata must report the expected diagnostic.");
            return;
        }
        throw new InvalidOperationException("Invalid disk evidence was accepted.");
    }
    private static void AssertPlan(ToolboxDiskObservation observation, string state, string code, bool verified, bool writable, params string[] actions)
    {
        var plan = ToolboxPrototypeRecovery.Plan(observation);
        TestAssert.Equal(state, plan.State, "Planner storage state.");
        TestAssert.Equal(code, plan.Code, "Planner diagnostic.");
        TestAssert.Equal(verified, plan.TransportVerified, "Transport verification is separate from writer authority.");
        TestAssert.Equal(writable, plan.Writable, "Planner writer gate.");
        TestAssert.True(actions.SequenceEqual(plan.Actions), "Planner must return exactly the ordered permitted actions.");
    }
    private static void RejectPlan(ToolboxDiskObservation observation)
    {
        try
        {
            var plan = ToolboxPrototypeRecovery.Plan(observation);
            TestAssert.True(plan.State is "quarantined" or "corrupt", "Contradictory evidence must fail closed.");
            TestAssert.False(plan.Writable, "Contradictory evidence cannot grant writes.");
            TestAssert.True(plan.Actions.IsEmpty, "Contradictory evidence cannot plan publication or abandonment.");
        }
        catch (ToolboxDiskException) { }
    }
}
