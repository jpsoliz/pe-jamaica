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
                  "group": "supporting_document",
                  "category": "manifest",
                  "display_name": "Profile Present",
                  "description": "Custom profile presence text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "detected_profile_complete",
                  "group": "supporting_document",
                  "category": "manifest",
                  "display_name": "Profile Complete",
                  "description": "Custom profile complete text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "required_source_roles",
                  "group": "supporting_document",
                  "category": "manifest",
                  "display_name": "Required Roles",
                  "description": "Custom role text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "source_file_integrity",
                  "group": "structure",
                  "category": "manifest",
                  "display_name": "Source Integrity",
                  "description": "Custom source integrity text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "workflow_rule_resolution",
                  "group": "structure",
                  "category": "workflow_rule",
                  "display_name": "Workflow Rule",
                  "description": "Custom workflow text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "arcgis_sdk_lane",
                  "group": "system",
                  "category": "arcgis_pro",
                  "display_name": "SDK Lane",
                  "description": "Custom sdk text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "workspace_access",
                  "group": "system",
                  "category": "write_access",
                  "display_name": "Workspace Access",
                  "description": "Custom workspace text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "python_executable_health",
                  "group": "system",
                  "category": "python",
                  "display_name": "Python Health",
                  "description": "Custom python executable text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "arcgis_unknown_version_behavior",
                  "group": "system",
                  "category": "arcgis_pro",
                  "display_name": "Unknown Version",
                  "description": "Custom unknown version text.",
                  "enabled": true,
                  "severity": "warning",
                  "locked": false
                },
                {
                  "rule_id": "python_package_probe",
                  "group": "system",
                  "category": "python",
                  "display_name": "Package Probe",
                  "description": "Custom package probe text.",
                  "enabled": false,
                  "severity": "configured",
                  "locked": false
                },
                {
                  "rule_id": "dwg_signature_check",
                  "group": "structure",
                  "category": "dwg",
                  "display_name": "DWG Signature",
                  "description": "Custom dwg signature text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "dwg_readiness_probe",
                  "group": "structure",
                  "category": "dwg",
                  "display_name": "DWG Readiness",
                  "description": "Custom dwg readiness text.",
                  "enabled": true,
                  "severity": "warning",
                  "locked": false
                },
                {
                  "rule_id": "dwg_required_cad_layers",
                  "group": "structure",
                  "category": "dwg",
                  "display_name": "Required DWG CAD Layers",
                  "description": "Custom required CAD layer text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": false,
                  "required_cad_layers": {
                    "points": ["POINTS"],
                    "lines": ["LINES"],
                    "annotation": ["TEXT"]
                  }
                },
                {
                  "rule_id": "georeference_source_presence",
                  "group": "georeference",
                  "category": "georeference",
                  "display_name": "Georeference Source",
                  "description": "Custom georeference source text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "tabular_coordinate_columns",
                  "group": "georeference",
                  "category": "georeference",
                  "display_name": "Coordinate Columns",
                  "description": "Custom tabular coordinate text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": false
                },
                {
                  "rule_id": "jamaica_coordinate_bounds",
                  "group": "georeference",
                  "category": "georeference",
                  "display_name": "Jamaica Coordinate Bounds",
                  "description": "Custom Jamaica bounds text.",
                  "enabled": true,
                  "severity": "warning",
                  "locked": false
                },
                {
                  "rule_id": "georeference_spatial_validation_readiness",
                  "group": "georeference",
                  "category": "georeference",
                  "display_name": "Concrete Georeference Validation",
                  "description": "Custom georeference validator readiness text.",
                  "enabled": true,
                  "severity": "warning",
                  "locked": false
                },
                {
                  "rule_id": "dimension_source_presence",
                  "group": "dimension",
                  "category": "dimension",
                  "display_name": "Dimension Source",
                  "description": "Custom dimension source text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "dimension_geometry_construction_readiness",
                  "group": "dimension",
                  "category": "dimension",
                  "display_name": "Dimension Geometry Construction Readiness",
                  "description": "Custom dimension geometry readiness text.",
                  "enabled": true,
                  "severity": "warning",
                  "locked": false
                },
                {
                  "rule_id": "pxa_memorandum_detected",
                  "group": "memorandum",
                  "category": "pxa_memorandum",
                  "display_name": "Memorandum text detected",
                  "description": "Custom memorandum detection text.",
                  "enabled": true,
                  "severity": "configured",
                  "locked": false,
                  "stage_id": "data_extraction",
                  "workflow_effect": "info",
                  "evaluator_key": "pxa_memorandum_detected"
                },
                {
                  "rule_id": "pxa_memorandum_surveyed_for_names_present",
                  "group": "memorandum",
                  "category": "pxa_memorandum",
                  "display_name": "Surveyed For",
                  "description": "Custom surveyed-for text.",
                  "enabled": true,
                  "severity": "configured",
                  "locked": false,
                  "stage_id": "validate_points_and_lines",
                  "workflow_effect": "requires_disposition",
                  "evaluator_key": "pxa_memorandum_surveyed_for_names_present"
                },
                {
                  "rule_id": "pxa_memorandum_surveyed_property_name_present",
                  "group": "memorandum",
                  "category": "pxa_memorandum",
                  "display_name": "Surveyed Property",
                  "description": "Custom surveyed property text.",
                  "enabled": true,
                  "severity": "configured",
                  "locked": false,
                  "stage_id": "validate_points_and_lines",
                  "workflow_effect": "requires_disposition",
                  "evaluator_key": "pxa_memorandum_surveyed_property_name_present"
                },
                {
                  "rule_id": "pxa_memorandum_property_name_near_diagram",
                  "group": "memorandum",
                  "category": "pxa_memorandum",
                  "display_name": "Property Near Diagram",
                  "description": "Custom diagram proximity text.",
                  "enabled": true,
                  "severity": "warning",
                  "locked": false,
                  "stage_id": "validate_points_and_lines",
                  "workflow_effect": "report_only",
                  "evaluator_key": "pxa_memorandum_property_name_near_diagram"
                },
                {
                  "rule_id": "pxa_memorandum_document_area_present",
                  "group": "memorandum",
                  "category": "survey_plan_memorandum",
                  "display_name": "Area Value And Unit",
                  "description": "Custom area text.",
                  "enabled": true,
                  "severity": "configured",
                  "locked": false,
                  "stage_id": "validate_points_and_lines",
                  "workflow_effect": "requires_disposition",
                  "evaluator_key": "pxa_memorandum_document_area_present"
                },
                {
                  "rule_id": "pxa_memorandum_objections_captured",
                  "group": "memorandum",
                  "category": "survey_plan_memorandum",
                  "display_name": "Grounds Of Objections",
                  "description": "Custom objections text.",
                  "enabled": true,
                  "severity": "configured",
                  "locked": false,
                  "stage_id": "validate_points_and_lines",
                  "workflow_effect": "requires_disposition",
                  "evaluator_key": "pxa_memorandum_objections_captured"
                },
                {
                  "rule_id": "pxa_memorandum_surveyor_certification_present",
                  "group": "memorandum",
                  "category": "survey_plan_memorandum",
                  "display_name": "Surveyor Certification",
                  "description": "Custom surveyor certification text.",
                  "enabled": true,
                  "severity": "configured",
                  "locked": false,
                  "stage_id": "validate_points_and_lines",
                  "workflow_effect": "requires_disposition",
                  "evaluator_key": "pxa_memorandum_surveyor_certification_present"
                },
                {
                  "rule_id": "pxa_memorandum_instrument_group_complete",
                  "group": "memorandum",
                  "category": "pxa_memorandum",
                  "display_name": "Instrument Group",
                  "description": "Custom instrument group text.",
                  "enabled": true,
                  "severity": "configured",
                  "locked": false,
                  "stage_id": "validate_points_and_lines",
                  "workflow_effect": "requires_disposition",
                  "evaluator_key": "pxa_memorandum_instrument_group_complete"
                },
                {
                  "rule_id": "pxa_memorandum_parish_present",
                  "group": "memorandum",
                  "category": "pxa_memorandum",
                  "display_name": "Memorandum Parish",
                  "description": "Custom memorandum parish text.",
                  "enabled": true,
                  "severity": "configured",
                  "locked": false,
                  "stage_id": "validate_points_and_lines",
                  "workflow_effect": "requires_disposition",
                  "evaluator_key": "pxa_memorandum_parish_present"
                },
                {
                  "rule_id": "pxa_memorandum_north_arrow_present",
                  "group": "memorandum",
                  "category": "pxa_memorandum",
                  "display_name": "Memorandum North Arrow",
                  "description": "Custom north arrow text.",
                  "enabled": true,
                  "severity": "warning",
                  "locked": false,
                  "stage_id": "validate_points_and_lines",
                  "workflow_effect": "report_only",
                  "evaluator_key": "pxa_memorandum_north_arrow_present"
                },
                {
                  "rule_id": "pxa_memorandum_scale_bar_present",
                  "group": "memorandum",
                  "category": "pxa_memorandum",
                  "display_name": "Memorandum Scale Bar",
                  "description": "Custom scale bar text.",
                  "enabled": true,
                  "severity": "warning",
                  "locked": false,
                  "stage_id": "validate_points_and_lines",
                  "workflow_effect": "report_only",
                  "evaluator_key": "pxa_memorandum_scale_bar_present"
                },
                {
                  "rule_id": "pxa_memorandum_notice_served_on_present",
                  "group": "memorandum",
                  "category": "pxa_memorandum",
                  "display_name": "Notice Served On",
                  "description": "Custom notice text.",
                  "enabled": true,
                  "severity": "configured",
                  "locked": false,
                  "stage_id": "validate_points_and_lines",
                  "workflow_effect": "requires_disposition",
                  "evaluator_key": "pxa_memorandum_notice_served_on_present"
                },
                {
                  "rule_id": "pxa_memorandum_appearance_parties_present",
                  "group": "memorandum",
                  "category": "pxa_memorandum",
                  "display_name": "Appeared Parties",
                  "description": "Custom appearance text.",
                  "enabled": true,
                  "severity": "configured",
                  "locked": false,
                  "stage_id": "validate_points_and_lines",
                  "workflow_effect": "requires_disposition",
                  "evaluator_key": "pxa_memorandum_appearance_parties_present"
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
        TestAssert.Equal("structure_check", packageProbe.StageId, "Legacy catalog entries should infer a stage_id.");
        TestAssert.Equal("python_package_probe", packageProbe.EvaluatorKey, "Legacy catalog entries should infer the evaluator key from rule_id.");
        TestAssert.Equal("requires_disposition", packageProbe.WorkflowEffect, "Configured severity should infer a workflow effect.");
        TestAssert.True(packageProbe.ReportVisible, "Legacy catalog entries should default to report-visible.");
        var cadLayersRule = catalog.GetRule("dwg_required_cad_layers");
        TestAssert.True(cadLayersRule.RequiredCadLayers?.ContainsKey("points") == true, "Configured required CAD layer aliases should load.");
    }

    public static void UnsupportedEvaluatorKeyFallsBackToSafeDefaults()
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
                  "group": "supporting_document",
                  "category": "manifest",
                  "display_name": "Profile Present",
                  "description": "Custom profile presence text.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true,
                  "stage_id": "supporting_document_check",
                  "workflow_effect": "blocker",
                  "evaluator_key": "not_supported"
                }
              ]
            }
            """);

        var catalog = new PreflightRuleCatalogLoader(catalogPath).Load();

        TestAssert.True(catalog.UsingSafeDefaults, "Unsupported evaluator keys should trigger safe defaults.");
        TestAssert.True(catalog.LoadWarning?.Contains("unsupported evaluator_key", StringComparison.OrdinalIgnoreCase) == true, "Warning should name the unsupported evaluator.");
    }

    public static void PreferredStructureRulesPathWinsWhenPresent()
    {
        using var tempRoot = new TempDirectory();
        var settingsPath = Path.Combine(tempRoot.Path, "WorkflowSettings.json");
        var preferredPath = Path.Combine(tempRoot.Path, "StructureRules.json");
        var legacyPath = Path.Combine(tempRoot.Path, "PreflightRules.json");
        File.WriteAllText(settingsPath, "{}");
        File.WriteAllText(preferredPath, "{}");
        File.WriteAllText(legacyPath, "{}");

        var catalog = new PreflightRuleCatalogLoader(rulesPathOverride: null, settingsPathOverride: settingsPath).Load();

        TestAssert.Equal(preferredPath, catalog.SourcePath, "StructureRules.json should be preferred when both rule catalogs exist.");
        TestAssert.True(catalog.UsingSafeDefaults, "Invalid preferred file should still fall back safely.");
    }

    public static void LegacyPreflightRulesPathLoadsWhenStructureRulesMissing()
    {
        using var tempRoot = new TempDirectory();
        var settingsPath = Path.Combine(tempRoot.Path, "WorkflowSettings.json");
        var legacyPath = Path.Combine(tempRoot.Path, "PreflightRules.json");
        File.WriteAllText(settingsPath, "{}");
        File.WriteAllText(legacyPath, "{}");

        var catalog = new PreflightRuleCatalogLoader(rulesPathOverride: null, settingsPathOverride: settingsPath).Load();

        TestAssert.Equal(legacyPath, catalog.SourcePath, "PreflightRules.json should remain the fallback when StructureRules.json is absent.");
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
