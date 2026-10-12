using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.Workflow.Review.Canonical;
using ParcelWorkflowAddIn.Workflow.Toolbox.Model;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;
using static ParcelWorkflowAddIn.Workflow.Toolbox.Persistence.ToolboxStorageJson;

namespace ParcelWorkflowAddIn.Workflow.Toolbox.Persistence;

public sealed class ToolboxWorkspaceStorageCodec(ToolboxStorageLimits? limits = null)
{
    private readonly ToolboxStorageLimits _limits = limits ?? new();
    public const string Contract = "toolbox_workspace_storage_v1";
    public static ImmutableArray<string> FingerprintContracts { get; } = new[]
    {
        "toolbox_canonical_inputs_v2", "toolbox_canonical_dependencies_v2", "toolbox_dependencies_v2",
        "toolbox_evidence_inputs_v2", "toolbox_evidence_dependencies_v1", "toolbox_finding_dependencies_v1",
        "toolbox_geometry_dependencies_v1", "toolbox_human_source_v1", "toolbox_parcel_decision_dependencies_v1",
        "toolbox_readiness_inputs_v2", "toolbox_readiness_dependencies_v1", "toolbox_reconciliation_base_v2"
    }.Order(StringComparer.Ordinal).ToImmutableArray();

    public ToolboxStorageResult<ToolboxWorkspaceStorageDocument> Capture(ToolboxStorageCaptureInput input) => Guard(() =>
    {
        Require(input.PendingProposal is null, "unsupported_capture");
        if (input.Reconciliation is { } snapshot)
            Require(ReferenceEquals(snapshot.Model, input.Model) && ReferenceEquals(snapshot.Register, input.Register), "inconsistent_state");
        var states = new Dictionary<ToolboxParcelModel, int>(ReferenceEqualityComparer.Instance);
        var ids = new Dictionary<ToolboxParcelModel, string>(ReferenceEqualityComparer.Instance);
        var models = new Dictionary<string, ToolboxStoredModel>(StringComparer.Ordinal);
        var payloads = new Dictionary<string, ToolboxStoredPayload>(StringComparer.Ordinal);
        var stack = new Stack<(ToolboxParcelModel Model, bool Exit)>(); stack.Push((input.Model, false));
        long archiveEdges = 0, capturedPayloadBytes = 0;
        while (stack.TryPop(out var item))
        {
            if (!item.Exit)
            {
                if (states.TryGetValue(item.Model, out var state)) { Require(state != 1, "cyclic_archive"); continue; }
                Require(item.Model.Case == input.Model.Case, "foreign_case");
                states[item.Model] = 1; Require(states.Count <= _limits.Models, "limit_exceeded");
                stack.Push((item.Model, true));
                if (item.Model.Relations is { } r)
                {
                    Require(!r.Archives.IsDefault);
                    archiveEdges = checked(archiveEdges + r.Archives.Length); Require(archiveEdges <= _limits.CollectionEntries, "limit_exceeded");
                    for (var i = r.Archives.Length - 1; i >= 0; i--) stack.Push((r.Archives[i], false));
                }
            }
            else
            {
                var m = item.Model;
                var source = AddCanonical(m.Source);
                ToolboxStoredRelations? relations = m.Relations is not { } r ? null : new(r.Policy, r.SourceHashes,
                    r.Boundaries, r.Dependencies, r.Archives.Select(a => ids[a]).ToImmutableArray(), r.Lineage,
                    r.Applicability, r.ReadinessBarriers, r.HumanInputBindings, r.FindingInputBindings, r.Adjacency);
                var stored = new ToolboxStoredModel("", m.Case, source, m.IdentityMap, m.Inclusions,
                    m.InclusionHistory, m.LinkedSources, relations);
                ToolboxStorageJson.Preflight(stored, _limits);
                var id = ModelId(stored); stored = stored with { SnapshotId = id };
                if (models.TryGetValue(id, out var old)) Require(Bytes(old).SequenceEqual(Bytes(stored)), "checksum_mismatch");
                else models.Add(id, stored);
                ids[m] = id; states[m] = 2;
            }
        }
        var register = new ToolboxStoredExamination(input.Register.Case, input.Register.Findings, input.Register.HumanReviews, input.Register.Decisions);
        var rootId = ids[input.Model];
        var reconciliation = input.Reconciliation is not { } b ? null : new ToolboxStoredReconciliation(b.Revision, rootId,
            b.Reruns.Select(r => new ToolboxStoredRerun(r.Case, r.BatchId, r.RunId, r.ToolVersion, r.ProviderVersion,
                r.SourceHashes, r.BaseRevision, r.BaseFingerprint, r.CandidateDigest, r.ReconciliationPolicy,
                r.ReviewPolicy, r.Status, AddCanonical(r.Candidate))).ToImmutableArray(),
            b.Lineage, b.Operations, b.AcceptedDispositions);
        var policies = (input.PolicyAssets.IsDefault ? [] : input.PolicyAssets).Select(a =>
        {
            Require(a.Bytes.Length <= _limits.PolicyBytes, "limit_exceeded");
            return new ToolboxStoredPolicyAsset(a.Pin, AddPayload("policy", a.Bytes.Span));
        }).ToImmutableArray();
        var document = new ToolboxWorkspaceStorageDocument(Contract, 1, "prototype_bundle", input.Model.Case,
            FingerprintContracts, rootId, models.Values.OrderBy(m => m.SnapshotId, StringComparer.Ordinal).ToImmutableArray(), register,
            input.CurrentCheckReviews.IsDefault ? [] : input.CurrentCheckReviews, reconciliation,
            payloads.Values.OrderBy(p => p.PayloadId, StringComparer.Ordinal).ToImmutableArray(), policies);
        var encoded = Encode(document);
        if (!encoded.Success)
        {
            var error = encoded.Diagnostics[0];
            throw new ToolboxStorageException(input.Model.Relations is null && error.Code == "missing_reference"
                ? "unsupported_capture" : error.Code, error.Path);
        }
        return document;

        string AddPayload(string kind, ReadOnlySpan<byte> bytes)
        {
            Require(bytes.Length <= (kind == "policy" ? _limits.PolicyBytes : _limits.CanonicalBytes), "limit_exceeded");
            var hash = Hash(bytes); var id = kind + "-" + hash;
            if (!payloads.ContainsKey(id))
            {
                capturedPayloadBytes = checked(capturedPayloadBytes + bytes.Length);
                Require(capturedPayloadBytes <= _limits.PayloadBytes, "limit_exceeded");
                payloads.Add(id, new(id, kind, Convert.ToBase64String(bytes), hash));
            }
            return id;
        }
        string AddCanonical(CanonicalReviewContract source)
        {
            Require(Utf8.GetByteCount(source.JsonSnapshot) <= _limits.CanonicalBytes, "limit_exceeded");
            return AddPayload("canonical", Utf8.GetBytes(source.JsonSnapshot));
        }
    });

    public ToolboxStorageResult<byte[]> Encode(ToolboxWorkspaceStorageDocument document) => Guard(() =>
    {
        ToolboxStorageJson.Preflight(document, _limits);
        var bytes = Bytes(document); Require(bytes.Length <= _limits.BundleBytes, "limit_exceeded");
        var decoded = Decode(bytes, document.Case);
        if (!decoded.Success) throw new ToolboxStorageException(decoded.Diagnostics[0].Code, decoded.Diagnostics[0].Path);
        return bytes;
    });

    public ToolboxStorageResult<ToolboxHydratedWorkspace> Decode(ReadOnlyMemory<byte> bytes, ToolboxCaseIdentity expectedCase,
        IEnumerable<ToolboxPolicyPin>? trustedPins = null)
    {
        var warnings = ImmutableArray.CreateBuilder<ToolboxStorageDiagnostic>();
        var result = Guard(() =>
        {
            Require(bytes.Length <= _limits.BundleBytes, "limit_exceeded");
            _ = Utf8.GetCharCount(bytes.Span);
            using var json = JsonDocument.Parse(bytes, new() { MaxDepth = _limits.JsonDepth });
            var root = json.RootElement;
            long entries = 0; var work = checked(Inspect(root, _limits, ref entries, true) + 3L * bytes.Length); long projectionBytes = 0;
            Require(work <= _limits.WorkUnits, "limit_exceeded");
            Shape(root, typeof(ToolboxWorkspaceStorageDocument));
            var doc = root.Deserialize<ToolboxWorkspaceStorageDocument>(Options)!;
            Require(doc.Contract == Contract, "unsupported_contract"); Require(doc.SchemaVersion == 1, "unsupported_version");
            Require(doc.StorageMode == "prototype_bundle", "unsupported_mode");
            Require(doc.Case == expectedCase, "foreign_case"); ToolboxStorageValidation.Identity(doc.Case);
            Require(doc.FingerprintContracts.SequenceEqual(FingerprintContracts), "unsupported_version");
            Require(doc.Models.Length <= _limits.Models, "limit_exceeded");
            var payloads = new Dictionary<string, (ToolboxStoredPayload Record, byte[] Bytes, long Entries)>(StringComparer.Ordinal);
            long payloadBytes = 0;
            foreach (var p in doc.Payloads)
            {
                Require(p.Kind is "canonical" or "policy");
                var max = p.Kind == "policy" ? _limits.PolicyBytes : _limits.CanonicalBytes;
                Require(p.Utf8Base64.Length <= ((long)max + 2) / 3 * 4, "limit_exceeded");
                var padding = p.Utf8Base64.EndsWith("==", StringComparison.Ordinal) ? 2 : p.Utf8Base64.EndsWith('=') ? 1 : 0;
                var projectedSize = (long)p.Utf8Base64.Length / 4 * 3 - padding;
                Require(projectedSize >= 0 && projectedSize <= max && payloadBytes + projectedSize <= _limits.PayloadBytes, "limit_exceeded");
                var decoded = Convert.FromBase64String(p.Utf8Base64);
                payloadBytes = checked(payloadBytes + decoded.Length);
                Require(decoded.Length <= max && payloadBytes <= _limits.PayloadBytes, "limit_exceeded");
                Require(Convert.ToBase64String(decoded) == p.Utf8Base64);
                _ = Utf8.GetCharCount(decoded);
                Require(p.Sha256 == Hash(decoded) && p.PayloadId == p.Kind + "-" + p.Sha256, "checksum_mismatch");
                long count = 0;
                if (p.Kind == "canonical")
                {
                    using var raw = JsonDocument.Parse(decoded, new() { MaxDepth = _limits.JsonDepth });
                    count = Inspect(raw.RootElement, _limits, ref entries, false);
                    Require(raw.RootElement.ValueKind == JsonValueKind.Object);
                }
                Require(payloads.TryAdd(p.PayloadId, (p, decoded, count)), "duplicate_identity");
            }
            var models = new Dictionary<string, ToolboxStoredModel>(StringComparer.Ordinal);
            long edges = 0;
            foreach (var m in doc.Models)
            {
                Require(m.Case == doc.Case, "foreign_case"); Require(models.TryAdd(m.SnapshotId, m), "duplicate_identity");
                Charge(m.SourcePayloadId);
                if (m.Relations is { } r) edges = checked(edges + r.Dependencies.Edges.Length);
            }
            Require(edges <= _limits.DependencyEdges, "limit_exceeded"); work = checked(work + edges);
            if (doc.Reconciliation is { } reconciliation) foreach (var r in reconciliation.Reruns) Charge(r.CandidatePayloadId);
            Require(work <= _limits.WorkUnits && projectionBytes <= _limits.ProjectionBytes, "limit_exceeded");
            var ordered = Order(models, doc.RootModelId);
            Require(ordered.Length == models.Count, "inconsistent_state"); // No hidden/unreachable history.
            var ancestors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var id in ordered)
            {
                var set = new HashSet<string>(StringComparer.Ordinal) { id };
                foreach (var archived in models[id].Relations?.ArchiveModelIds ?? [])
                {
                    work = checked(work + ancestors[archived].Count + 1);
                    Require(work <= _limits.WorkUnits, "limit_exceeded");
                    set.UnionWith(ancestors[archived]);
                }
                ancestors[id] = set;
                // Factories build historical indexes per model; charge those repeated closure visits explicitly.
                foreach (var ancestor in set)
                    work = checked(work + payloads[models[ancestor].SourcePayloadId].Entries
                        + (models[ancestor].Relations?.ArchiveModelIds.Length ?? 0));
                Require(work <= _limits.WorkUnits, "limit_exceeded");
            }
            var candidateSources = (doc.Reconciliation?.Reruns ?? []).Select(r =>
            {
                Require(r.Case == doc.Case, "foreign_case");
                return CanonicalReviewContract.Read(JsonNode.Parse(payloads[r.CandidatePayloadId].Bytes)!.AsObject());
            }).ToImmutableArray();
            var restored = new Dictionary<string, ToolboxParcelModel>(StringComparer.Ordinal);
            foreach (var id in ordered)
            {
                var m = models[id]; Require(ModelId(m) == m.SnapshotId, "checksum_mismatch");
                var raw = payloads[m.SourcePayloadId];
                var source = CanonicalReviewContract.Read(JsonNode.Parse(raw.Bytes)!.AsObject());
                var r = m.Relations;
                var relations = r is null ? null : new ToolboxModelRelations(r.Policy,
                    r.SourceHashes.WithComparers(StringComparer.OrdinalIgnoreCase), r.Boundaries, r.Dependencies,
                    r.ArchiveModelIds.Select(a => restored[a]).ToImmutableArray(), r.Lineage, r.Applicability, r.ReadinessBarriers)
                    { HumanInputBindings = r.HumanInputBindings, FindingInputBindings = r.FindingInputBindings, Adjacency = r.Adjacency };
                restored.Add(id, ToolboxParcelModel.Restore(m.Case, source, m.IdentityMap, m.Inclusions, m.InclusionHistory, m.LinkedSources, relations, candidateSources));
            }
            var model = restored[doc.RootModelId]; var e = doc.Examination;
            var register = ToolboxExaminationRegister.Restore(model, e.Case, e.Findings, e.HumanReviews, e.Decisions);
            ToolboxStorageValidation.Examination(model, e.Case, e.Findings, e.HumanReviews, e.Decisions, doc.CurrentCheckReviews);
            ToolboxReconciliationSnapshot? snapshot = null;
            if (doc.Reconciliation is { } b)
            {
                Require(b.RootModelId == doc.RootModelId && model.Relations is not null, "inconsistent_state");
                ToolboxStorageValidation.Text(b.Revision);
                Require(Bytes(b.Lineage).SequenceEqual(Bytes(model.Relations!.Lineage)), "inconsistent_state");
                ToolboxStorageValidation.Lineage(b.Lineage, ToolboxStorageValidation.History(model), candidateSources);
                Require(b.Operations.Length == b.AcceptedDispositions.Length, "inconsistent_state");
                var opIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var historyModels = ToolboxStorageValidation.History(model);
                var known = new ToolboxStorageValidation.Inventory(historyModels);
                var evidence = known.Evidence.Concat(candidateSources.SelectMany(s => s.Traces).Select(t => t.Id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var typedOldIds = historyModels.SelectMany(m => m.IdentityMap.Ids)
                    .Select(p => (p.Key[..p.Key.IndexOf(':')].ToLowerInvariant(), p.Value.ToUpperInvariant())).ToHashSet();
                var sourceKeys = historyModels.Select(m => m.Source).Concat(candidateSources)
                    .SelectMany(s => s.Parcels.Select(p => "parcel:" + p.Key).Concat(s.Rows.Select(p => "row:" + p.Key)).Concat(s.Segments.Select(p => "segment:" + p.Key)))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < b.Operations.Length; i++)
                {
                    var op = b.Operations[i]; var audit = b.AcceptedDispositions[i];
                    ToolboxStorageValidation.Text(op.OperationId); ToolboxStorageValidation.Text(op.Digest); ToolboxStorageValidation.Text(op.ResultRevision);
                    ToolboxStorageValidation.Text(audit.ParentRevision);
                    Require(opIds.Add(op.OperationId), "duplicate_identity");
                    Require(op.OperationId == audit.OperationId && op.Digest == audit.Digest && op.ResultRevision == audit.ResultRevision
                        && audit.ParentRevision != audit.ResultRevision, "inconsistent_state");
                    var oldIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var incomingKeys = new HashSet<(string, string)>(); var outputKeys = new HashSet<(string, string)>(); var globals = 0;
                    foreach (var choice in audit.Dispositions)
                    {
                        Require(choice.Kind is "parcel" or "row" or "segment" or "global" && Enum.IsDefined(choice.Choice));
                        ToolboxStorageValidation.Text(choice.Actor); ToolboxStorageValidation.Text(choice.Reason); ToolboxStorageValidation.Text(choice.EvidenceId);
                        Require(evidence.Contains(choice.EvidenceId), "missing_reference");
                        if (choice.Kind == "global") Require(++globals == 1 && choice.OldId is null && choice.IncomingKey is null && choice.OutputKey is null
                            && choice.ExplicitIdentity && choice.Choice is ToolboxValueChoice.Retain or ToolboxValueChoice.Replace);
                        else
                        {
                            if (choice.OldId is not null)
                            { Require(oldIds.Add(choice.OldId), "duplicate_identity"); Require(typedOldIds.Contains((choice.Kind, choice.OldId.ToUpperInvariant())), "missing_reference"); }
                            if (choice.IncomingKey is not null)
                            { Require(incomingKeys.Add((choice.Kind, choice.IncomingKey.ToUpperInvariant())), "duplicate_identity"); Require(sourceKeys.Contains(choice.Kind + ":" + choice.IncomingKey), "missing_reference"); }
                            if (choice.OutputKey is not null)
                            { Require(outputKeys.Add((choice.Kind, choice.OutputKey.ToUpperInvariant())), "duplicate_identity"); Require(sourceKeys.Contains(choice.Kind + ":" + choice.OutputKey), "missing_reference"); }
                            Require(choice.Choice != ToolboxValueChoice.Unresolved);
                            Require(choice.Choice is not (ToolboxValueChoice.Retain or ToolboxValueChoice.Replace)
                                || choice.OldId is not null && choice.OutputKey is not null);
                            Require(choice.Choice != ToolboxValueChoice.Replace || choice.IncomingKey is not null);
                            Require(choice.Choice != ToolboxValueChoice.New || choice.OldId is null && choice.IncomingKey is not null && choice.OutputKey is not null);
                            Require(choice.Choice != ToolboxValueChoice.Retire || choice.OldId is not null && choice.OutputKey is null && choice.IncomingKey is null);
                        }
                    }
                    Require(globals == 1, "inconsistent_state");
                }
                var reruns = b.Reruns.Select((r, index) =>
                {
                    Require(r.Case == doc.Case, "foreign_case");
                    ToolboxStorageValidation.Text(r.BatchId); ToolboxStorageValidation.Text(r.RunId); ToolboxStorageValidation.Text(r.ToolVersion);
                    ToolboxStorageValidation.Text(r.ProviderVersion); ToolboxStorageValidation.Text(r.BaseRevision); ToolboxStorageValidation.Text(r.BaseFingerprint);
                    ToolboxStorageValidation.Text(r.CandidateDigest); ToolboxStorageValidation.Text(r.ReconciliationPolicy); ToolboxStorageValidation.Pin(r.ReviewPolicy);
                    var source = candidateSources[index];
                    Require(string.Equals(source.TransactionNumber.Trim(), doc.Case.TransactionNumber, StringComparison.OrdinalIgnoreCase), "foreign_case");
                    var hashes = r.SourceHashes.WithComparers(StringComparer.OrdinalIgnoreCase);
                    Require(hashes.Count == source.Sources.Length && source.Sources.All(s => hashes.TryGetValue(s.Id, out var h) && !string.IsNullOrWhiteSpace(h)), "missing_reference");
                    return new ToolboxRerunBatch(r.Case, r.BatchId, r.RunId, r.ToolVersion, r.ProviderVersion, hashes, r.BaseRevision,
                        r.BaseFingerprint, r.CandidateDigest, r.ReconciliationPolicy, r.ReviewPolicy, r.Status, source);
                }).ToImmutableArray();
                snapshot = new(b.Revision, model, register, reruns, b.Lineage, b.Operations) { AcceptedDispositions = b.AcceptedDispositions };
                ToolboxStorageValidation.Applicability(model, register, candidateSources);
            }
            else Require(model.Relations is null, "inconsistent_state");
            var requested = (trustedPins ?? []).Take(_limits.CollectionEntries + 1).ToArray();
            Require(requested.Length <= _limits.CollectionEntries, "limit_exceeded");
            var policies = ImmutableArray.CreateBuilder<ToolboxReviewPolicy>();
            var assetPins = new HashSet<(string, string)>();
            foreach (var a in doc.PolicyAssets)
            {
                ToolboxStorageValidation.Pin(a.Pin);
                Require(assetPins.Add((a.Pin.PolicyId, a.Pin.PolicyVersion)), "duplicate_identity");
                Require(payloads.TryGetValue(a.PayloadId, out var payload) && payload.Record.Kind == "policy", "missing_reference");
                Require(string.Equals(a.Pin.Sha256, payload.Record.Sha256, StringComparison.OrdinalIgnoreCase), "checksum_mismatch");
                var trusted = requested.FirstOrDefault(p => p.PolicyId == a.Pin.PolicyId && p.PolicyVersion == a.Pin.PolicyVersion);
                if (trusted is null) warnings.Add(new("policy_untrusted", "$.policy_assets", "External policy trust was not supplied."));
                else
                {
                    Require(string.Equals(trusted.Sha256, a.Pin.Sha256, StringComparison.OrdinalIgnoreCase), "checksum_mismatch");
                    policies.Add(ToolboxReviewPolicyLoader.Load(payload.Bytes, trusted));
                }
            }
            foreach (var pin in requested) if (!assetPins.Contains((pin.PolicyId, pin.PolicyVersion)))
                warnings.Add(new("policy_unavailable", "$.policy_assets", "Requested policy bytes are unavailable."));
            if (doc.PolicyAssets.Length == 0) warnings.Add(new("policy_unavailable", "$.policy_assets", "No policy bytes supplied; inspection only."));
            return new ToolboxHydratedWorkspace(model, register, snapshot, doc.CurrentCheckReviews,
                payloads.ToImmutableDictionary(p => p.Key, p => p.Value.Bytes.ToImmutableArray(), StringComparer.Ordinal), policies.ToImmutable());

            void Charge(string id)
            {
                Require(payloads.TryGetValue(id, out var p) && p.Record.Kind == "canonical", "missing_reference");
                work = checked(work + checked(8 * p.Entries * p.Entries)); projectionBytes = checked(projectionBytes + p.Bytes.Length);
                Require(work <= _limits.WorkUnits && projectionBytes <= _limits.ProjectionBytes, "limit_exceeded");
            }
        });
        return result.Success ? result with { Diagnostics = warnings.ToImmutable() } : result;
    }

    private static ImmutableArray<string> Order(Dictionary<string, ToolboxStoredModel> models, string root)
    {
        Require(models.ContainsKey(root), "missing_reference");
        var states = new Dictionary<string, int>(StringComparer.Ordinal); var result = ImmutableArray.CreateBuilder<string>();
        var pending = new Stack<(string Id, bool Exit)>(); pending.Push((root, false));
        while (pending.TryPop(out var item))
        {
            Require(models.TryGetValue(item.Id, out var model), "missing_reference");
            if (item.Exit) { states[item.Id] = 2; result.Add(item.Id); continue; }
            if (states.TryGetValue(item.Id, out var state)) { Require(state != 1, "cyclic_archive"); continue; }
            states[item.Id] = 1; pending.Push((item.Id, true));
            var refs = model!.Relations?.ArchiveModelIds ?? [];
            for (var i = refs.Length - 1; i >= 0; i--) pending.Push((refs[i], false));
        }
        return result.ToImmutable();
    }

    internal static string ModelId(ToolboxStoredModel model)
    {
        var node = JsonSerializer.SerializeToNode(model, Options)!.AsObject(); node.Remove("snapshot_id");
        return "model-" + Hash(Bytes(node));
    }
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static ToolboxStorageResult<T> Guard<T>(Func<T> action) where T : class
    {
        try { return new(action(), []); }
        catch (ToolboxStorageException e) { return Failure<T>(e.Code, e.Path); }
        catch (JsonException e) { return Failure<T>(e.Message.Contains("depth", StringComparison.OrdinalIgnoreCase) ? "limit_exceeded" : "malformed_json", "$"); }
        catch (OverflowException) { return Failure<T>("limit_exceeded", "$"); }
        catch (ToolboxPolicyException) { return Failure<T>("invalid_value", "$.policy_assets"); }
        catch (Exception e) when (e is ArgumentException or FormatException or InvalidOperationException or System.IO.InvalidDataException)
        { return Failure<T>("invalid_value", "$"); }
    }
    private static ToolboxStorageResult<T> Failure<T>(string code, string path) where T : class =>
        new(null, [new(code, path, "The prototype bundle could not be validated; no workspace was returned.")]);
}
