using ParcelWorkflowAddIn.Workflow.Execution;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class DocumentTypeCatalogLoaderTests
{
    public static void V2CatalogLoadsAndWeightedMatchPrefersSpecificComputationSheet()
    {
        using var tempRoot = new TempDirectory();
        var catalogPath = Path.Combine(tempRoot.Path, "CreateParcel_doc_types.json");
        File.WriteAllText(catalogPath,
            """
            {
              "schema_version": "2.0",
              "default_doc_type_id": "UNKNOWN_GENERIC_SOURCE_V1",
              "doc_types": [
                {
                  "doc_type_id": "GENERIC_COMPUTATION_TABLE_V2",
                  "name": "Generic Computation Table",
                  "family": "computation_sheet",
                  "priority": 100,
                  "match": {
                    "source_kinds": [".pdf"],
                    "filename_contains_any": ["SHEET"],
                    "score_threshold": 20
                  },
                  "classifier": {
                    "strategy": "weighted_match",
                    "weights": {
                      "filename_contains_any": 20,
                      "filename_regex_any": 15,
                      "text_contains_any": 25,
                      "text_regex_any": 30
                    }
                  },
                  "extraction": {
                    "extractor_id": "openai_table_pdf",
                    "parser_mode": "parcel_block_rows",
                    "ai_assisted": true,
                    "ai_profile": "survey_table_vision_v1",
                    "fallback_extractors": ["ocr_table_pdf"],
                    "expected_outputs": ["rows"]
                  },
                  "schema": { "metadata_fields": [], "parcel_fields": [], "row_fields": [] },
                  "geometry": {
                    "geometry_mode": "parcel_rows_with_group_breaks",
                    "point_source": "row_vertices",
                    "line_builder": "from_to_pairs",
                    "polygon_builder": "group_closed_ring",
                    "supports_multi_parcel": true,
                    "supports_boundary_breaks": true,
                    "requires_grouping": true,
                    "closing_rule": "do_not_chain_across_groups"
                  },
                  "validation": {
                    "validation_profile": "generic",
                    "required_metadata_fields": [],
                    "required_row_fields": [],
                    "minimum_expected_rows": 0,
                    "minimum_expected_parcels": 0,
                    "blockers": [],
                    "warnings": []
                  },
                  "review": {
                    "review_mode": "point_review",
                    "source_viewer_mode": "embedded_or_external",
                    "allow_manual_point_add": true,
                    "allow_manual_group_split": true,
                    "allow_manual_group_merge": true,
                    "approval_requires_zero_blockers": true
                  },
                  "output": {
                    "output_profiles": ["normal_fgdb"],
                    "default_output_profile": "normal_fgdb"
                  }
                },
                {
                  "doc_type_id": "GEOLAND_COMPUTATION_TABLE_V2",
                  "name": "GeoLand Computation Table",
                  "family": "computation_sheet",
                  "priority": 200,
                  "match": {
                    "source_kinds": [".pdf"],
                    "filename_contains_any": ["HEATHFIELD", "COMPUTER SHEET", "SHEET"],
                    "score_threshold": 20
                  },
                  "classifier": {
                    "strategy": "weighted_match",
                    "weights": {
                      "filename_contains_any": 20,
                      "filename_regex_any": 15,
                      "text_contains_any": 25,
                      "text_regex_any": 30
                    }
                  },
                  "extraction": {
                    "extractor_id": "openai_table_pdf",
                    "parser_mode": "parcel_block_rows",
                    "ai_assisted": true,
                    "ai_profile": "survey_table_vision_v1",
                    "fallback_extractors": ["ocr_table_pdf"],
                    "expected_outputs": ["rows"]
                  },
                  "schema": { "metadata_fields": [], "parcel_fields": [], "row_fields": [] },
                  "geometry": {
                    "geometry_mode": "parcel_rows_with_group_breaks",
                    "point_source": "row_vertices",
                    "line_builder": "from_to_pairs",
                    "polygon_builder": "group_closed_ring",
                    "supports_multi_parcel": true,
                    "supports_boundary_breaks": true,
                    "requires_grouping": true,
                    "closing_rule": "do_not_chain_across_groups"
                  },
                  "validation": {
                    "validation_profile": "geoland",
                    "required_metadata_fields": [],
                    "required_row_fields": [],
                    "minimum_expected_rows": 0,
                    "minimum_expected_parcels": 0,
                    "blockers": [],
                    "warnings": []
                  },
                  "review": {
                    "review_mode": "point_review",
                    "source_viewer_mode": "embedded_or_external",
                    "allow_manual_point_add": true,
                    "allow_manual_group_split": true,
                    "allow_manual_group_merge": true,
                    "approval_requires_zero_blockers": true
                  },
                  "output": {
                    "output_profiles": ["normal_fgdb"],
                    "default_output_profile": "normal_fgdb"
                  }
                }
              ]
            }
            """);

        var catalog = new DocumentTypeCatalogLoader(catalogPath).Load();
        var match = catalog.ResolveBestMatch(new DocumentTypeMatchCandidate("points_source", "Heathfield Block 43 Sheet 007 Computer Sheet.pdf", ".pdf"));

        TestAssert.True(!catalog.UsingSafeDefaults, "Valid V2 catalog should load directly.");
        TestAssert.Equal("GEOLAND_COMPUTATION_TABLE_V2", match.Definition.DocTypeId, "Weighted match should prefer the more specific GeoLand computation family.");
        TestAssert.Equal("openai_table_pdf", match.Definition.Extraction.ExtractorId, "Resolved route should expose extractor id.");
        TestAssert.Equal("parcel_rows_with_group_breaks", match.Definition.Geometry.GeometryMode, "Resolved route should expose geometry mode.");
        TestAssert.True(match.MatchConfidence >= 1d, "Specific weighted match should meet threshold.");
    }

    public static void LegacyCatalogLoadsThroughCompatibilityAndAddsStructuredPointsFallback()
    {
        using var tempRoot = new TempDirectory();
        var catalogPath = Path.Combine(tempRoot.Path, "CreateParcel_doc_types.json");
        File.WriteAllText(catalogPath,
            """
            {
              "default_doc_type_id": "CASE1_COMPUTATION_TABLE_V1",
              "doc_types": [
                {
                  "doc_type_id": "CASE1_COMPUTATION_TABLE_V1",
                  "name": "Case1 Computation Table",
                  "match": {
                    "source_ext_in": [".pdf"],
                    "filename_contains_any": ["COMPUTATION", "SHEET"]
                  },
                  "expected_schema": {
                    "metadata_fields": ["parish"],
                    "parcel_fields": ["parcel_name"],
                    "row_fields": ["segment_no", "north", "east"]
                  },
                  "validation": {
                    "required_metadata_fields": ["parish"],
                    "required_row_fields": ["north", "east"]
                  }
                }
              ]
            }
            """);

        var catalog = new DocumentTypeCatalogLoader(catalogPath).Load();

        TestAssert.True(!catalog.UsingSafeDefaults, "Legacy catalog should load through compatibility mapping.");
        TestAssert.True(!string.IsNullOrWhiteSpace(catalog.LoadWarning), "Legacy catalog should note compatibility mode.");
        TestAssert.True(catalog.DocumentTypes.Any(definition => definition.DocTypeId == "STRUCTURED_POINTS_TEXT_V1"), "Compatibility load should still expose structured points fallback family.");
    }

    public static void PartiallyInvalidV2CatalogFallsBackWithWarning()
    {
        using var tempRoot = new TempDirectory();
        var catalogPath = Path.Combine(tempRoot.Path, "CreateParcel_doc_types.json");
        File.WriteAllText(catalogPath,
            """
            {
              "schema_version": "2.0",
              "default_doc_type_id": "UNKNOWN_GENERIC_SOURCE_V1",
              "doc_types": [
                {
                  "doc_type_id": "BROKEN_DOC"
                }
              ]
            }
            """);

        var catalog = new DocumentTypeCatalogLoader(catalogPath).Load();
        var match = catalog.ResolveBestMatch(new DocumentTypeMatchCandidate("points_source", "unknown-file.pdf", ".pdf"));

        TestAssert.True(catalog.UsingSafeDefaults, "Partially invalid V2 catalog should fall back to safe defaults.");
        TestAssert.True(!string.IsNullOrWhiteSpace(catalog.LoadWarning), "Fallback should explain the invalid catalog.");
        TestAssert.True(catalog.LoadWarning!.Contains("partially invalid", StringComparison.OrdinalIgnoreCase), "Warning should explain the fallback reason.");
        TestAssert.Equal("UNKNOWN_GENERIC_SOURCE_V1", match.Definition.DocTypeId, "Fallback catalog should preserve safe default type.");
    }
}
