using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.WorkflowRules;

public sealed record WorkflowScriptPlan(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("rule_version")] string RuleVersion,
    [property: JsonPropertyName("workflow_profile")] string WorkflowProfile,
    [property: JsonPropertyName("created_at")] string CreatedAt,
    [property: JsonPropertyName("source_manifest_hash")] string SourceManifestHash,
    [property: JsonPropertyName("steps")] IReadOnlyList<WorkflowScriptStep> Steps);

public sealed record WorkflowScriptStep(
    [property: JsonPropertyName("step_name")] string StepName,
    [property: JsonPropertyName("adapter")] string Adapter,
    [property: JsonPropertyName("script")] string Script,
    [property: JsonPropertyName("input_roles")] IReadOnlyList<string> InputRoles,
    [property: JsonPropertyName("output_artifacts")] IReadOnlyList<string> OutputArtifacts,
    [property: JsonPropertyName("parameters")] IReadOnlyDictionary<string, string> Parameters,
    [property: JsonPropertyName("timeout_seconds")] int TimeoutSeconds,
    [property: JsonPropertyName("openai_enabled")] bool OpenAiEnabled,
    [property: JsonPropertyName("ocr_engine")] string OcrEngine,
    [property: JsonPropertyName("credential_profile")] string CredentialProfile);
