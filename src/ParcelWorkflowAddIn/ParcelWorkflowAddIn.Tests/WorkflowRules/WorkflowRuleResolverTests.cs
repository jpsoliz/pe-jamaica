using System.Text.Json;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Tests;
using ParcelWorkflowAddIn.WorkflowRules;

namespace ParcelWorkflowAddIn.Tests.WorkflowRules;

internal static class WorkflowRuleResolverTests
{
    public static void ScenarioATwoPdfSourcesResolveToTwoPdfPlan()
    {
        using var rules = DefaultRulesFile();
        var resolver = Resolver(rules.Path);
        var profile = new SourceInputProfileDetector(() => FixedNow()).Detect(new[]
        {
            Source("BELLEV029GEOLANCOMSHEET.pdf", ".pdf", null),
            Source("BELLEV029GEOLAN20230811.pdf", ".pdf", null)
        });
        var sources = new[]
        {
            Source("BELLEV029GEOLANCOMSHEET.pdf", ".pdf", SourceRole.ComputationSource),
            Source("BELLEV029GEOLAN20230811.pdf", ".pdf", SourceRole.PlanMapReference)
        };

        var result = resolver.Resolve(Context("Plan Examination", profile, sources));

        TestAssert.True(result.Success, "Scenario A two-PDF sources should resolve.");
        TestAssert.Equal("scenario_a_two_pdf_v1", result.ScriptPlan!.RuleId, "Scenario A rule id mismatch.");
        TestAssert.Equal("scenario_a_two_pdf", result.ScriptPlan.WorkflowProfile, "Scenario A workflow profile mismatch.");
        TestAssert.Equal(2, result.ScriptPlan.Steps.Count, "Scenario A should plan two script steps.");
        TestAssert.True(result.ScriptPlan.Steps.Any(step => step.InputRoles.Contains(SourceRole.ComputationSource)), "Plan should include computation input role.");
        TestAssert.True(result.ScriptPlan.Steps.Any(step => step.InputRoles.Contains(SourceRole.PlanMapReference)), "Plan should include plan/map input role.");
    }

    public static void ScenarioBSourcesResolveToDwgAwarePlan()
    {
        using var rules = DefaultRulesFile();
        var resolver = Resolver(rules.Path);
        var profile = new DetectedSourceInputProfile(
            SourceInputProfile.ScenarioB,
            SourceInputProfile.ScenarioBLabel,
            "matched",
            FixedNow().UtcDateTime.ToString("O"),
            Array.Empty<string>(),
            Array.Empty<string>());
        var sources = new[]
        {
            Source("points.csv", ".csv", SourceRole.PointsComputation),
            Source("reference.dwg", ".dwg", SourceRole.DwgReference),
            Source("plan.pdf", ".pdf", SourceRole.PlanMapReference)
        };

        var result = resolver.Resolve(Context("Plan Examination", profile, sources));

        TestAssert.True(result.Success, "Scenario B sources should resolve.");
        TestAssert.Equal("scenario_b_points_dwg_plan_v1", result.ScriptPlan!.RuleId, "Scenario B rule id mismatch.");
        TestAssert.True(result.ScriptPlan.Steps.Any(step => step.InputRoles.Contains(SourceRole.DwgReference)), "Scenario B plan should include DWG-aware step.");
    }

    public static void UnknownSourceCombinationReturnsNoMatch()
    {
        using var rules = DefaultRulesFile();
        var resolver = Resolver(rules.Path);
        var profile = new DetectedSourceInputProfile(
            SourceInputProfile.UnsupportedIntake,
            SourceInputProfile.UnsupportedIntakeLabel,
            "unsupported",
            FixedNow().UtcDateTime.ToString("O"),
            Array.Empty<string>(),
            Array.Empty<string>());

        var result = resolver.Resolve(Context("Plan Examination", profile, new[] { Source("notes.zip", ".zip", null) }));

        TestAssert.True(!result.Success, "Unsupported sources should not resolve.");
        TestAssert.True(result.ErrorMessage!.Contains("No workflow rule", StringComparison.OrdinalIgnoreCase), "No-match message should be clear.");
    }

    public static void PlanParametersDoNotPersistSecretValues()
    {
        using var rules = SecretRulesFile();
        var resolver = Resolver(rules.Path);
        var profile = new DetectedSourceInputProfile(
            SourceInputProfile.ScenarioA,
            SourceInputProfile.ScenarioALabel,
            "matched",
            FixedNow().UtcDateTime.ToString("O"),
            Array.Empty<string>(),
            Array.Empty<string>());
        var sources = new[]
        {
            Source("computation.pdf", ".pdf", SourceRole.ComputationSource),
            Source("plan.pdf", ".pdf", SourceRole.PlanMapReference)
        };

        var result = resolver.Resolve(Context("Plan Examination", profile, sources));
        var serialized = JsonSerializer.Serialize(result.ScriptPlan);

        TestAssert.True(result.Success, "Secret-filter fixture should still resolve.");
        TestAssert.True(!serialized.Contains("secret-password", StringComparison.OrdinalIgnoreCase), "Plan must not contain password-like values.");
        TestAssert.True(!serialized.Contains("Bearer token", StringComparison.OrdinalIgnoreCase), "Plan must not contain bearer token values.");
        TestAssert.True(serialized.Contains("OPENAI_API_KEY", StringComparison.Ordinal), "Plan may contain an environment variable name.");
    }

    public static void PlanParametersDoNotPersistConfiguredSecretValues()
    {
        using var rules = DefaultRulesFile();
        var resolver = Resolver(rules.Path);
        var profile = new DetectedSourceInputProfile(
            SourceInputProfile.ScenarioA,
            SourceInputProfile.ScenarioALabel,
            "matched",
            FixedNow().UtcDateTime.ToString("O"),
            Array.Empty<string>(),
            Array.Empty<string>());
        var sources = new[]
        {
            Source("computation.pdf", ".pdf", SourceRole.ComputationSource),
            Source("plan.pdf", ".pdf", SourceRole.PlanMapReference)
        };
        var context = new WorkflowRuleResolutionContext(
            "Plan Examination",
            "parcel_workflow",
            profile,
            sources,
            new WorkflowRuleSettings("openai", true, "balanced", "gpt-4.1-mini", "sk-test-secret", "Bearer token"));

        var result = resolver.Resolve(context);
        var serialized = JsonSerializer.Serialize(result.ScriptPlan);

        TestAssert.True(result.Success, "Default fixture should still resolve.");
        TestAssert.True(!serialized.Contains("sk-test-secret", StringComparison.OrdinalIgnoreCase), "Plan must not contain configured API key values.");
        TestAssert.True(!serialized.Contains("Bearer token", StringComparison.OrdinalIgnoreCase), "Plan must not contain configured bearer-token values.");
        TestAssert.Equal(WorkflowRuleSettings.Default.CredentialProfile, result.ScriptPlan!.Steps[0].CredentialProfile, "Secret-looking credential profile should be replaced with a safe default.");
    }

    private static WorkflowRuleResolver Resolver(string rulesPath)
    {
        return new WorkflowRuleResolver(new WorkflowRuleRegistry(() => rulesPath), () => FixedNow());
    }

    private static WorkflowRuleResolutionContext Context(
        string? transactionType,
        DetectedSourceInputProfile profile,
        IReadOnlyList<ManifestSourceFile> sources)
    {
        return new WorkflowRuleResolutionContext(
            transactionType,
            "parcel_workflow",
            profile,
            sources,
            new WorkflowRuleSettings("openai", true, "balanced", "gpt-4.1-mini", "OPENAI_API_KEY", "local"));
    }

    private static ManifestSourceFile Source(string fileName, string fileType, string? sourceRole)
    {
        return new ManifestSourceFile(
            $"innola-attachment:{fileName}",
            Path.Combine("D:\\cases\\100000206\\source", fileName),
            fileType,
            100,
            FixedNow().UtcDateTime.ToString("O"),
            sourceRole);
    }

    private static TempFile DefaultRulesFile()
    {
        return TempFile.FromExisting(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "Settings", "WorkflowRules.json"));
    }

    private static TempFile SecretRulesFile()
    {
        var file = new TempFile();
        File.WriteAllText(file.Path, """
            {
              "schema_version": "1.0.0",
              "rules": [
                {
                  "rule_id": "secret_filter_rule",
                  "rule_version": "1.0.0",
                  "workflow_profile": "scenario_a_two_pdf",
                  "detected_profiles": ["scenario_a"],
                  "transaction_types": ["Plan Examination"],
                  "process_steps": ["parcel_workflow"],
                  "required_sources": [
                    { "role": "computation_source", "extensions": [".pdf"] },
                    { "role": "plan_map_reference", "extensions": [".pdf"] }
                  ],
                  "script_plan": [
                    {
                      "step_name": "secret_filter",
                      "adapter": "extraction_adapter",
                      "script": "test_script",
                      "input_roles": ["computation_source"],
                      "output_artifacts": ["working/test.json"],
                      "parameters": {
                        "password": "secret-password",
                        "authorization": "Bearer token",
                        "openai_key_env": "{settings.openai_api_key_environment_variable}"
                      },
                      "timeout_seconds": 60
                    }
                  ]
                }
              ]
            }
            """);
        return file;
    }

    private static DateTimeOffset FixedNow()
    {
        return new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero);
    }
}
