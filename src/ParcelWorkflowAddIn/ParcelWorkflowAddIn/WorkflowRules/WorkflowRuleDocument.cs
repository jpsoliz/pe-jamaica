using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.WorkflowRules;

public sealed record WorkflowRuleDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("rules")] IReadOnlyList<WorkflowRule> Rules);

public sealed record WorkflowRule(
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("rule_version")] string RuleVersion,
    [property: JsonPropertyName("workflow_profile")] string WorkflowProfile,
    [property: JsonPropertyName("detected_profiles")] IReadOnlyList<string>? DetectedProfiles,
    [property: JsonPropertyName("transaction_types")] IReadOnlyList<string>? TransactionTypes,
    [property: JsonPropertyName("process_steps")] IReadOnlyList<string>? ProcessSteps,
    [property: JsonPropertyName("required_sources")] IReadOnlyList<WorkflowRuleRequiredSource> RequiredSources,
    [property: JsonPropertyName("script_plan")] IReadOnlyList<WorkflowRuleScriptStep> ScriptPlan);

public sealed record WorkflowRuleRequiredSource(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("extensions")] IReadOnlyList<string> Extensions);

public sealed record WorkflowRuleScriptStep(
    [property: JsonPropertyName("step_name")] string StepName,
    [property: JsonPropertyName("adapter")] string Adapter,
    [property: JsonPropertyName("script")] string Script,
    [property: JsonPropertyName("input_roles")] IReadOnlyList<string> InputRoles,
    [property: JsonPropertyName("output_artifacts")] IReadOnlyList<string> OutputArtifacts,
    [property: JsonPropertyName("parameters")] IReadOnlyDictionary<string, string> Parameters,
    [property: JsonPropertyName("timeout_seconds")] int TimeoutSeconds);
