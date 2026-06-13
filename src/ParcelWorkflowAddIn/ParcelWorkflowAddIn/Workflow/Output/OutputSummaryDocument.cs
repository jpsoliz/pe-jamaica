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
    [property: JsonPropertyName("result_gdb_path")] string? ResultGdbPath,
    [property: JsonPropertyName("artifact_paths")] IReadOnlyList<string> ArtifactPaths,
    [property: JsonPropertyName("map_layer_paths")] IReadOnlyList<string> MapLayerPaths,
    [property: JsonPropertyName("point_feature_class_path")] string? PointFeatureClassPath,
    [property: JsonPropertyName("line_feature_class_path")] string? LineFeatureClassPath,
    [property: JsonPropertyName("polygon_feature_class_path")] string? PolygonFeatureClassPath,
    [property: JsonPropertyName("point_count")] int PointCount,
    [property: JsonPropertyName("line_count")] int LineCount,
    [property: JsonPropertyName("polygon_count")] int PolygonCount,
    [property: JsonPropertyName("template_project_path")] string? TemplateProjectPath,
    [property: JsonPropertyName("template_gdb_path")] string? TemplateGdbPath);
