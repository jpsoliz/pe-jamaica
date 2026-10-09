using System.Collections.Immutable;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

public static class ToolboxReviewPolicyLoader
{
    public const string SchemaVersion = "toolbox_review_policy_local_v1";
    public const int MaxBytes = 256 * 1024;
    private const string ComputeBaseline = "RRRRRRRRRRRRRRRRORRRRRRRRRRRRRRRRRRRRNORRR";
    private const string CompareBaseline = "RRNORRROROOOOORRNRRRRRROROOOOONROROONRRRRR";

    public static async Task<ToolboxReviewPolicy> LoadFileAsync(string path, ToolboxPolicyPin pin,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new ToolboxPolicyException("An explicit absolute policy path is required; no settings fallback.");
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous);
        using var buffer = new MemoryStream();
        var bytes = new byte[4096];
        int count;
        while ((count = await stream.ReadAsync(bytes, cancellationToken)) != 0)
        {
            if (buffer.Length + count > MaxBytes) throw new ToolboxPolicyException("Policy exceeds size limit.");
            buffer.Write(bytes, 0, count);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Load(buffer.ToArray(), pin);
    }

    public static ToolboxReviewPolicy Load(ReadOnlyMemory<byte> bytes, ToolboxPolicyPin pin)
    {
        ArgumentNullException.ThrowIfNull(pin);
        if (bytes.Length == 0 || bytes.Length > MaxBytes) throw new ToolboxPolicyException("Invalid policy size.");
        if (string.IsNullOrWhiteSpace(pin.PolicyId) || string.IsNullOrWhiteSpace(pin.PolicyVersion)
            || pin.Sha256 is null || pin.Sha256.Length != 64 || !pin.Sha256.All(Uri.IsHexDigit))
            throw new ToolboxPolicyException("Explicit policy identity, version and SHA-256 pin required.");
        // Own the bytes before hashing and parsing so callers cannot mutate a validated snapshot.
        var snapshotBytes = bytes.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(snapshotBytes)).ToLowerInvariant();
        if (!string.Equals(hash, pin.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new ToolboxPolicyException("Policy hash does not match the trusted pin.");
        try
        {
            using var document = JsonDocument.Parse(snapshotBytes, new JsonDocumentOptions { MaxDepth = 32 });
            RejectDuplicateProperties(document.RootElement);
            var root = document.RootElement;
            Fields(root, "schema_version", "policy_id", "policy_version", "activation_status",
                "classification_baseline", "supported_stages", "preset_overrides", "items");
            if (Text(root, "schema_version") != SchemaVersion) Fail("Unsupported policy schema.");
            if (Text(root, "policy_id") != pin.PolicyId || Text(root, "policy_version") != pin.PolicyVersion)
                Fail("Policy identity/version does not match pin.");
            if (Text(root, "activation_status") != "isolated_test")
                Fail("This loader supports isolated test policies only, not draft or published policies.");
            if (Text(root, "classification_baseline") != "compute-compare-v1")
                Fail("Unsupported classification baseline.");
            var stages = Array(root, "supported_stages");
            if (stages.Length != 2 || stages.Any(s => s.ValueKind != JsonValueKind.String)
                || !stages.Select(s => s.GetString()).OrderBy(s => s, StringComparer.Ordinal)
                    .SequenceEqual(new[] { "Compare", "Compute" }))
                Fail("Exactly Compute and Compare are supported.");
            if (Array(root, "preset_overrides").Length != 0)
                Fail("Preset overrides are not supported in the local v1 contract.");
            var sourceItems = Array(root, "items");
            if (sourceItems.Length != 42) Fail("All 42 baseline items are required.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var items = ImmutableArray.CreateBuilder<ToolboxPolicyItem>();
            foreach (var source in sourceItems)
            {
                Fields(source, "item_id", "display_name", "stages");
                var id = Text(source, "item_id");
                var number = 0;
                if (id.Length != 3 || id[0] != 'M' || !int.TryParse(id.AsSpan(1), out number)
                    || number < 1 || number > 42 || id != $"M{number:00}" || !seen.Add(id))
                    Fail("Unknown/duplicate baseline item: " + id);
                var stageObject = Property(source, "stages");
                Fields(stageObject, "Compute", "Compare");
                var stageBuilder = ImmutableDictionary.CreateBuilder<ToolboxPreset, ToolboxPolicyStage>();
                foreach (var stage in new[] { ToolboxPreset.Compute, ToolboxPreset.Compare })
                {
                    var entry = Property(stageObject, stage.ToString());
                    Fields(entry, "requirement", "checks");
                    var requirement = Requirement(Text(entry, "requirement"));
                    var baseline = (stage == ToolboxPreset.Compute ? ComputeBaseline : CompareBaseline)[number - 1];
                    if (requirement != FromBaseline(baseline)) Fail("Classification differs from approved v1: " + id);
                    var checks = ImmutableArray.CreateBuilder<ToolboxPolicyCheck>();
                    var checkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var check in Array(entry, "checks"))
                    {
                        Fields(check, "check_id", "scope", "applicability", "evaluator_id", "evaluator_version", "effect");
                        var checkId = Text(check, "check_id");
                        if (!checkIds.Add(checkId)) Fail("Duplicate check ID: " + checkId);
                        var scope = Scope(Text(check, "scope"));
                        var effect = Effect(Text(check, "effect"));
                        checks.Add(new ToolboxPolicyCheck(checkId, scope,
                            Predicate(Property(check, "applicability")), Text(check, "evaluator_id"),
                            Text(check, "evaluator_version"), effect));
                    }
                    if (requirement == ToolboxReviewRequirement.NotApplicable && checks.Count != 0)
                        Fail("Stage N/A must not contain independent checks: " + id);
                    if (requirement != ToolboxReviewRequirement.NotApplicable && checks.Count == 0)
                        Fail("Applicable stage must have explicit check bindings: " + id);
                    stageBuilder.Add(stage, new ToolboxPolicyStage(requirement, checks.ToImmutable()));
                }
                items.Add(new ToolboxPolicyItem(id, Text(source, "display_name"), stageBuilder.ToImmutable()));
            }
            return new ToolboxReviewPolicy(new ToolboxPolicyPin(pin.PolicyId, pin.PolicyVersion, hash),
                new UTF8Encoding(false, true).GetString(snapshotBytes),
                items.OrderBy(item => item.ItemId, StringComparer.Ordinal).ToImmutableArray());
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            throw new ToolboxPolicyException("Invalid policy JSON/UTF-8: " + exception.Message);
        }
    }

    private static ToolboxPredicate Predicate(JsonElement node, int depth = 0)
    {
        if (depth > 8) Fail("Predicate exceeds depth limit.");
        var op = Text(node, "op");
        if (op is "always" or "never")
        {
            Fields(node, "op");
            return new(op, null, null, ImmutableArray<ToolboxPredicate>.Empty);
        }
        if (op is "all" or "any" or "not")
        {
            Fields(node, "op", "args");
            var args = Array(node, "args");
            if (args.Length == 0 || args.Length > 16 || (op == "not" && args.Length != 1))
                Fail("Invalid predicate argument count.");
            return new(op, null, null, args.Select(arg => Predicate(arg, depth + 1)).ToImmutableArray());
        }
        if (op is not ("equals" or "in" or "exists" or "gt" or "gte" or "lt" or "lte"))
            Fail("Unknown predicate operator: " + op);
        Fields(node, op == "exists" ? new[] { "op", "fact" } : new[] { "op", "fact", "value" });
        var fact = Text(node, "fact");
        if (!ToolboxPolicyFactCatalog.Types.TryGetValue(fact, out var type))
            Fail("Fact is not in the applicability catalog: " + fact);
        JsonElement? value = op == "exists" ? null : Property(node, "value").Clone();
        if (op == "in")
        {
            if (value!.Value.ValueKind != JsonValueKind.Array) Fail("in requires an array.");
            var values = value.Value.EnumerateArray().ToArray();
            if (values.Length == 0 || values.Length > 32 || values.Any(v => !ToolboxPolicyFactCatalog.Matches(type, v)))
                Fail("Invalid in values.");
        }
        else if (op != "exists")
        {
            if (!ToolboxPolicyFactCatalog.Matches(type, value!.Value)) Fail("Predicate value type mismatch.");
            if ((op is "gt" or "gte" or "lt" or "lte") && type != ToolboxFactType.Number)
                Fail("Numeric operator requires a numeric fact.");
        }
        return new(op, fact, value, ImmutableArray<ToolboxPredicate>.Empty);
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name)) Fail("Duplicate JSON property: " + property.Name);
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) RejectDuplicateProperties(child);
    }

    private static void Fields(JsonElement node, params string[] fields)
    {
        if (node.ValueKind != JsonValueKind.Object) Fail("Expected JSON object.");
        var actual = node.EnumerateObject().Select(p => p.Name).ToArray();
        if (actual.Length != fields.Length || actual.Any(p => !fields.Contains(p, StringComparer.Ordinal)))
            Fail("Missing/unknown properties; expected: " + string.Join(", ", fields));
    }

    private static JsonElement Property(JsonElement node, string name)
    {
        if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(name, out var value))
            throw new ToolboxPolicyException("Missing property: " + name);
        return value;
    }

    private static string Text(JsonElement node, string name)
    {
        var value = Property(node, name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new ToolboxPolicyException("Nonblank string required: " + name);
        var text = value.GetString()!;
        if (text != text.Trim() || text.Length > 512) Fail("Invalid string length/whitespace: " + name);
        return text;
    }

    private static JsonElement[] Array(JsonElement node, string name)
    {
        var value = Property(node, name);
        if (value.ValueKind != JsonValueKind.Array) throw new ToolboxPolicyException("Array required: " + name);
        return value.EnumerateArray().ToArray();
    }

    private static ToolboxReviewRequirement Requirement(string value) => value switch
    {
        "required" => ToolboxReviewRequirement.Required,
        "optional" => ToolboxReviewRequirement.Optional,
        "not_applicable" => ToolboxReviewRequirement.NotApplicable,
        _ => throw new ToolboxPolicyException("Unknown requirement: " + value)
    };

    private static ToolboxReviewRequirement FromBaseline(char value) => value switch
    {
        'R' => ToolboxReviewRequirement.Required, 'O' => ToolboxReviewRequirement.Optional,
        _ => ToolboxReviewRequirement.NotApplicable
    };

    private static ToolboxScopeKind Scope(string value) => value switch
    {
        "case" => ToolboxScopeKind.Case, "document" => ToolboxScopeKind.Document,
        "parcel" => ToolboxScopeKind.Parcel, "participant" => ToolboxScopeKind.Participant,
        "boundary" => ToolboxScopeKind.Boundary, "identifier_pair" => ToolboxScopeKind.IdentifierPair,
        _ => throw new ToolboxPolicyException("Unknown scope: " + value)
    };

    private static ToolboxReviewEffect Effect(string value) => value switch
    {
        "info" => ToolboxReviewEffect.Info, "report_only" => ToolboxReviewEffect.ReportOnly,
        "requires_disposition" => ToolboxReviewEffect.RequiresDisposition,
        _ => throw new ToolboxPolicyException("Unknown effect: " + value)
    };

    private static void Fail(string message) => throw new ToolboxPolicyException(message);
}
