using ParcelWorkflowAddIn.Preflight;

namespace ParcelWorkflowAddIn.Tests.Preflight;

internal static class PreflightRuleCatalogLoaderTests
{
    public static void MissingRulesFileFallsBackToSafeDefaults()
    {
        using var tempRoot = new TempDirectory();
        var missingPath = Path.Combine(tempRoot.Path, "missing-rules.json");

        var catalog = new PreflightRuleCatalogLoader(missingPath).Load();

        TestAssert.True(catalog.UsingSafeDefaults, "Missing rules file should fall back to safe defaults.");
        TestAssert.True(!string.IsNullOrWhiteSpace(catalog.LoadWarning), "Fallback should describe the warning.");
        TestAssert.True(catalog.Rules.Any(rule => rule.RuleId == "python_package_probe"), "Default rules should still include configurable package probe.");
        TestAssert.True(catalog.Rules.Any(rule => rule.Locked), "Default rules should preserve locked core rules.");
    }

    public static void FullCatalogFileIsAuthoritativeForMetadata()
    {
        using var tempRoot = new TempDirectory();
        var catalogPath = Path.Combine(tempRoot.Path, "PreflightRules.json");
        File.WriteAllText(catalogPath,
            """
            {
              "schema_version": "1.0.0",
              "rules": [
                {
                  "rule_id": "detected_profile_presence",
                  "category": "manifest",
                  "display_name": "Profile Present",
                  "description": "Custom profile presence text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "detected_profile_complete",
                  "category": "manifest",
                  "display_name": "Profile Complete",
                  "description": "Custom profile complete text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "required_source_roles",
                  "category": "manifest",
                  "display_name": "Required Roles",
                  "description": "Custom role text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "source_file_integrity",
                  "category": "manifest",
                  "display_name": "Source Integrity",
                  "description": "Custom source integrity text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "workflow_rule_resolution",
                  "category": "workflow_rule",
                  "display_name": "Workflow Rule",
                  "description": "Custom workflow text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "arcgis_sdk_lane",
                  "category": "arcgis_pro",
                  "display_name": "SDK Lane",
                  "description": "Custom sdk text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "workspace_access",
                  "category": "write_access",
                  "display_name": "Workspace Access",
                  "description": "Custom workspace text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "python_executable_health",
                  "category": "python",
                  "display_name": "Python Health",
                  "description": "Custom python executable text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "arcgis_unknown_version_behavior",
                  "category": "arcgis_pro",
                  "display_name": "Unknown Version",
                  "description": "Custom unknown version text.",
                  "enabled": true,
                  "severity": "warning",
                  "locked": false
                },
                {
                  "rule_id": "python_package_probe",
                  "category": "python",
                  "display_name": "Package Probe",
                  "description": "Custom package probe text.",
                  "enabled": false,
                  "severity": "configured",
                  "locked": false
                },
                {
                  "rule_id": "dwg_signature_check",
                  "category": "dwg",
                  "display_name": "DWG Signature",
                  "description": "Custom dwg signature text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "dwg_readiness_probe",
                  "category": "dwg",
                  "display_name": "DWG Readiness",
                  "description": "Custom dwg readiness text.",
                  "enabled": true,
                  "severity": "warning",
                  "locked": false
                }
              ]
            }
            """);

        var catalog = new PreflightRuleCatalogLoader(catalogPath).Load();

        TestAssert.True(!catalog.UsingSafeDefaults, "A complete catalog file should load without fallback.");
        var packageProbe = catalog.GetRule("python_package_probe");
        TestAssert.Equal("Package Probe", packageProbe.DisplayName, "Display metadata should come from the external catalog.");
        TestAssert.Equal("Custom package probe text.", packageProbe.Description, "Description should come from the external catalog.");
        TestAssert.True(!packageProbe.Enabled, "Enabled state should come from the external catalog.");
    }

    public static void PartiallyInvalidCatalogFallsBackWithWarning()
    {
        using var tempRoot = new TempDirectory();
        var catalogPath = Path.Combine(tempRoot.Path, "PreflightRules.json");
        File.WriteAllText(catalogPath,
            """
            {
              "schema_version": "1.0.0",
              "rules": [
                {
                  "rule_id": "detected_profile_presence",
                  "category": "manifest",
                  "display_name": "Profile Present",
                  "description": "Custom profile presence text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "python_package_probe",
                  "category": "python",
                  "display_name": "Package Probe",
                  "enabled": true,
                  "severity": "configured",
                  "locked": false
                }
              ]
            }
            """);

        var catalog = new PreflightRuleCatalogLoader(catalogPath).Load();

        TestAssert.True(catalog.UsingSafeDefaults, "A partially invalid catalog should fall back to safe defaults.");
        TestAssert.True(!string.IsNullOrWhiteSpace(catalog.LoadWarning), "Fallback should describe the invalid catalog.");
        TestAssert.True(catalog.LoadWarning!.Contains("partially invalid", StringComparison.OrdinalIgnoreCase), "Warning should explain the fallback reason.");
        TestAssert.True(catalog.Rules.Any(rule => rule.RuleId == "workflow_rule_resolution" && rule.DisplayName == "Workflow rule resolution"), "Fallback should restore the safe default catalog.");
    }
}
