using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.Workflow.SpatialReview;

public sealed record SpatialOverlapReviewDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("created_at")] string CreatedAt,
    [property: JsonPropertyName("review_group_layer_name")] string? ReviewGroupLayerName,
    [property: JsonPropertyName("review_layer_name")] string? ReviewLayerName,
    [property: JsonPropertyName("review_area_square_meters")] double ReviewAreaSquareMeters,
    [property: JsonPropertyName("summary")] SpatialOverlapReviewSummary Summary,
    [property: JsonPropertyName("layers")] IReadOnlyList<SpatialOverlapReviewLayerResult> Layers,
    [property: JsonPropertyName("records")] IReadOnlyList<SpatialOverlapReviewRecord> Records,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings,
    [property: JsonPropertyName("errors")] IReadOnlyList<string> Errors,
    [property: JsonPropertyName("snapshots")] IReadOnlyList<SpatialOverlapReviewSnapshotRef>? Snapshots = null);

public sealed record SpatialOverlapReviewSummary(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("configured_layer_count")] int ConfiguredLayerCount,
    [property: JsonPropertyName("checked_layer_count")] int CheckedLayerCount,
    [property: JsonPropertyName("missing_dependency_count")] int MissingDependencyCount,
    [property: JsonPropertyName("overlap_record_count")] int OverlapRecordCount,
    [property: JsonPropertyName("no_overlap_layer_count")] int NoOverlapLayerCount);

public sealed record SpatialOverlapReviewLayerResult(
    [property: JsonPropertyName("layer_role")] string LayerRole,
    [property: JsonPropertyName("layer_name")] string LayerName,
    [property: JsonPropertyName("source_name")] string SourceName,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("result_type")] string ResultType,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("record_count")] int RecordCount,
    [property: JsonPropertyName("missing_dependency")] bool MissingDependency = false,
    [property: JsonPropertyName("map_layer_match_name")] string? MapLayerMatchName = null);

public sealed record SpatialOverlapReviewRecord(
    [property: JsonPropertyName("layer_role")] string LayerRole,
    [property: JsonPropertyName("layer_name")] string LayerName,
    [property: JsonPropertyName("source_name")] string SourceName,
    [property: JsonPropertyName("relationship")] string Relationship,
    [property: JsonPropertyName("feature_identity")] string FeatureIdentity,
    [property: JsonPropertyName("source_object_id")] string? SourceObjectId,
    [property: JsonPropertyName("parcel_id")] string? ParcelId,
    [property: JsonPropertyName("pid")] string? Pid,
    [property: JsonPropertyName("volume")] string? Volume,
    [property: JsonPropertyName("folio")] string? Folio,
    [property: JsonPropertyName("land_valuation_number")] string? LandValuationNumber,
    [property: JsonPropertyName("pe_number")] string? PeNumber,
    [property: JsonPropertyName("dp_number")] string? DpNumber,
    [property: JsonPropertyName("r_number")] string? RNumber,
    [property: JsonPropertyName("overlap_area_square_meters")] double OverlapAreaSquareMeters,
    [property: JsonPropertyName("overlap_percentage")] double OverlapPercentage,
    [property: JsonPropertyName("overlap_group_id")] string? OverlapGroupId = null,
    [property: JsonPropertyName("overlap_id")] string? OverlapId = null,
    [property: JsonPropertyName("enrichment_status")] string? EnrichmentStatus = null,
    [property: JsonPropertyName("owner_enrichment")] SpatialOverlapReviewOwnerEnrichment? OwnerEnrichment = null);

public sealed record SpatialOverlapReviewOwnerEnrichment(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("identifier_kind")] string? IdentifierKind,
    [property: JsonPropertyName("identifier_value")] string? IdentifierValue,
    [property: JsonPropertyName("query_key")] string? QueryKey,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("diagnostic")] string? Diagnostic,
    [property: JsonPropertyName("matches")] IReadOnlyList<SpatialOverlapReviewOwnerMatch> Matches);

public sealed record SpatialOverlapReviewOwnerMatch(
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("party_role")] string? PartyRole,
    [property: JsonPropertyName("parcel_id")] string? ParcelId,
    [property: JsonPropertyName("volume")] string? Volume,
    [property: JsonPropertyName("folio")] string? Folio,
    [property: JsonPropertyName("land_valuation_number")] string? LandValuationNumber,
    [property: JsonPropertyName("parish")] string? Parish,
    [property: JsonPropertyName("property_type")] string? PropertyType,
    [property: JsonPropertyName("tenure")] string? Tenure,
    [property: JsonPropertyName("registered_at")] string? RegisteredAt,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("query_key")] string? QueryKey,
    [property: JsonPropertyName("diagnostic")] string? Diagnostic);

public sealed record SpatialOverlapReviewSnapshotRef(
    [property: JsonPropertyName("overlap_group_id")] string? OverlapGroupId,
    [property: JsonPropertyName("overlap_id")] string? OverlapId,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("relative_path")] string? RelativePath,
    [property: JsonPropertyName("status")] string Status);

public static class SpatialOverlapReviewScopes
{
    public const string Compute = "compute";
    public const string Compare = "compare";
}

public static class SpatialOverlapReviewStatuses
{
    public const string Ready = "ready";
    public const string Blocked = "blocked";
}

public static class SpatialOverlapReviewEnrichmentStatuses
{
    public const string NotRequested = "not_requested";
    public const string IdentifierUnavailable = "identifier_unavailable";
    public const string Matched = "matched";
    public const string MultipleMatches = "multiple_matches";
    public const string NoOwnerMatchFound = "no_owner_match_found";
    public const string QueryFailed = "query_failed";
}
