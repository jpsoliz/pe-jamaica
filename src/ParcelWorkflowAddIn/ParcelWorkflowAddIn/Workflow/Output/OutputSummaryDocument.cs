using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.Workflow.Output;

public sealed record OutputSummaryDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("created_at")] string CreatedAt,
    [property: JsonPropertyName("created_by")] string? CreatedBy,
    [property: JsonPropertyName("source_manifest_hash")] string SourceManifestHash,
    [property: JsonPropertyName("payload")] OutputSummaryPayload Payload,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings,
    [property: JsonPropertyName("errors")] IReadOnlyList<string> Errors);

public sealed record OutputSummaryPayload(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("review_workspace_mode")] string ReviewWorkspaceMode,
    [property: JsonPropertyName("result_gdb_path")] string? ResultGdbPath,
    [property: JsonPropertyName("artifact_paths")] IReadOnlyList<string> ArtifactPaths,
    [property: JsonPropertyName("map_layer_paths")] IReadOnlyList<string> MapLayerPaths,
    [property: JsonPropertyName("point_feature_class_path")] string? PointFeatureClassPath,
    [property: JsonPropertyName("line_feature_class_path")] string? LineFeatureClassPath,
    [property: JsonPropertyName("polygon_feature_class_path")] string? PolygonFeatureClassPath,
    [property: JsonPropertyName("review_dataset_path")] string? ReviewDatasetPath,
    [property: JsonPropertyName("review_layer_path")] string? ReviewLayerPath,
    [property: JsonPropertyName("review_point_feature_class_path")] string? ReviewPointFeatureClassPath,
    [property: JsonPropertyName("review_line_feature_class_path")] string? ReviewLineFeatureClassPath,
    [property: JsonPropertyName("review_polygon_feature_class_path")] string? ReviewPolygonFeatureClassPath,
    [property: JsonPropertyName("parcel_fabric_mode")] string? ParcelFabricMode,
    [property: JsonPropertyName("parcel_fabric_dataset_path")] string? ParcelFabricDatasetPath,
    [property: JsonPropertyName("parcel_fabric_layer_path")] string? ParcelFabricLayerPath,
    [property: JsonPropertyName("parcel_record_name")] string? ParcelRecordName,
    [property: JsonPropertyName("parcel_record_id")] string? ParcelRecordId,
    [property: JsonPropertyName("parcel_type")] string? ParcelType,
    [property: JsonPropertyName("built_parcel_count")] int BuiltParcelCount,
    [property: JsonPropertyName("built_line_count")] int BuiltLineCount,
    [property: JsonPropertyName("built_point_count")] int BuiltPointCount,
    [property: JsonPropertyName("point_count")] int PointCount,
    [property: JsonPropertyName("line_count")] int LineCount,
    [property: JsonPropertyName("polygon_count")] int PolygonCount,
    [property: JsonPropertyName("template_project_path")] string? TemplateProjectPath,
    [property: JsonPropertyName("template_gdb_path")] string? TemplateGdbPath,
    [property: JsonPropertyName("review_result_owner")] string? ReviewResultOwner,
    [property: JsonPropertyName("enterprise_working_publish")] EnterpriseWorkingPublishSummary? EnterpriseWorkingPublish = null);

public sealed record EnterpriseWorkingPublishSummary(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("published_at")] string PublishedAt,
    [property: JsonPropertyName("published_by")] string? PublishedBy,
    [property: JsonPropertyName("transaction_scope_field")] string ScopeField,
    [property: JsonPropertyName("transaction_scope_value")] string ScopeValue,
    [property: JsonPropertyName("workflow_name")] string WorkflowName,
    [property: JsonPropertyName("workflow_stage")] string WorkflowStage,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("task_id")] string? TaskId,
    [property: JsonPropertyName("transaction_type")] string? TransactionType,
    [property: JsonPropertyName("assigned_user")] string? AssignedUser,
    [property: JsonPropertyName("assigned_group")] string? AssignedGroup,
    [property: JsonPropertyName("last_saved_utc")] string PublishedUtc,
    [property: JsonPropertyName("published_layers")] IReadOnlyList<EnterpriseWorkingPublishedLayer> PublishedLayers,
    [property: JsonPropertyName("local_only_artifacts")] IReadOnlyList<string> LocalOnlyArtifacts,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings,
    [property: JsonPropertyName("errors")] IReadOnlyList<string> Errors);

public sealed record EnterpriseWorkingPublishedLayer(
    [property: JsonPropertyName("layer_role")] string LayerRole,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("record_count")] int RecordCount,
    [property: JsonPropertyName("replaced_existing")] bool ReplacedExisting);
