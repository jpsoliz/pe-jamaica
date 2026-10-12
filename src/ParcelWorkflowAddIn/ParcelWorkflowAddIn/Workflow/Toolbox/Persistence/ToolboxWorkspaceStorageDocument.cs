using System.Collections.Immutable;
using ParcelWorkflowAddIn.Workflow.Toolbox.Model;
using ParcelWorkflowAddIn.Workflow.Toolbox.Policy;

namespace ParcelWorkflowAddIn.Workflow.Toolbox.Persistence;

public sealed record ToolboxStorageLimits(
    int BundleBytes = 64 * 1024 * 1024, int PayloadBytes = 32 * 1024 * 1024,
    int CanonicalBytes = 8 * 1024 * 1024, int PolicyBytes = 256 * 1024,
    int JsonDepth = 64, int Models = 512, int CollectionEntries = 100_000,
    int DependencyEdges = 100_000, int TokenCharacters = 4096,
    int TextBytes = 1024 * 1024, long WorkUnits = 100_000_000,
    long ProjectionBytes = 256 * 1024 * 1024);

public sealed record ToolboxStorageDiagnostic(string Code, string Path, string Reason);
public sealed record ToolboxStorageResult<T>(T? Value, ImmutableArray<ToolboxStorageDiagnostic> Diagnostics)
    where T : class
{
    public bool Success => Value is not null;
}

public sealed record ToolboxStoragePolicyInput(ToolboxPolicyPin Pin, ReadOnlyMemory<byte> Bytes);
public sealed record ToolboxStorageCaptureInput(ToolboxParcelModel Model, ToolboxExaminationRegister Register,
    ToolboxReconciliationSnapshot? Reconciliation = null,
    ImmutableArray<ToolboxCheckReview> CurrentCheckReviews = default,
    ImmutableArray<ToolboxStoragePolicyInput> PolicyAssets = default,
    ToolboxReconciliationProposal? PendingProposal = null);

public sealed record ToolboxWorkspaceStorageDocument(string Contract, int SchemaVersion, string StorageMode,
    ToolboxCaseIdentity Case, ImmutableArray<string> FingerprintContracts, string RootModelId,
    ImmutableArray<ToolboxStoredModel> Models, ToolboxStoredExamination Examination,
    ImmutableArray<ToolboxCheckReview> CurrentCheckReviews, ToolboxStoredReconciliation? Reconciliation,
    ImmutableArray<ToolboxStoredPayload> Payloads, ImmutableArray<ToolboxStoredPolicyAsset> PolicyAssets);

public sealed record ToolboxStoredModel(string SnapshotId, ToolboxCaseIdentity Case, string SourcePayloadId,
    ToolboxIdentityMap IdentityMap, ImmutableDictionary<string, ToolboxInclusion> Inclusions,
    ImmutableArray<ToolboxInclusionRecord> InclusionHistory, ImmutableArray<ToolboxSourceReference> LinkedSources,
    ToolboxStoredRelations? Relations);

public sealed record ToolboxStoredRelations(ToolboxPolicyPin Policy, ImmutableDictionary<string, string> SourceHashes,
    ImmutableArray<ToolboxPhysicalBoundary> Boundaries, ToolboxDependencyInventory Dependencies,
    ImmutableArray<string> ArchiveModelIds, ImmutableArray<ToolboxAppliedLineage> Lineage,
    ImmutableArray<ToolboxHistoryApplicability> Applicability, ImmutableArray<string> ReadinessBarriers,
    ImmutableDictionary<string, string> HumanInputBindings, ImmutableDictionary<string, string> FindingInputBindings,
    ToolboxAdjacencyReview? Adjacency);

public sealed record ToolboxStoredExamination(ToolboxCaseIdentity? Case, ImmutableArray<ToolboxFinding> Findings,
    ImmutableArray<ToolboxHumanReview> HumanReviews, ImmutableArray<ToolboxReadinessSnapshot> Decisions);

public sealed record ToolboxStoredReconciliation(string Revision, string RootModelId,
    ImmutableArray<ToolboxStoredRerun> Reruns, ImmutableArray<ToolboxAppliedLineage> Lineage,
    ImmutableArray<ToolboxAcceptedOperation> Operations, ImmutableArray<ToolboxAcceptedDispositions> AcceptedDispositions);

public sealed record ToolboxStoredRerun(ToolboxCaseIdentity Case, string BatchId, string RunId, string ToolVersion,
    string ProviderVersion, ImmutableDictionary<string, string> SourceHashes, string BaseRevision,
    string BaseFingerprint, string CandidateDigest, string ReconciliationPolicy, ToolboxPolicyPin ReviewPolicy,
    ToolboxRerunStatus Status, string CandidatePayloadId);

public sealed record ToolboxStoredPayload(string PayloadId, string Kind, string Utf8Base64, string Sha256);
public sealed record ToolboxStoredPolicyAsset(ToolboxPolicyPin Pin, string PayloadId);

public sealed record ToolboxHydratedWorkspace(ToolboxParcelModel Model, ToolboxExaminationRegister Register,
    ToolboxReconciliationSnapshot? Reconciliation, ImmutableArray<ToolboxCheckReview> CurrentCheckReviews,
    ImmutableDictionary<string, ImmutableArray<byte>> PayloadBytes,
    ImmutableArray<ToolboxReviewPolicy> TrustedPolicies);
