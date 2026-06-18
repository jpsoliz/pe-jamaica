using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow.Execution;
using ParcelWorkflowAddIn.WorkflowRules;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class CreateParcelDraftExtractionAdapterTests
{
    public static void ExtractionAdapterWritesMatchedDocTypeIntoReviewArtifactAndIni()
    {
        using var openAiKeyScope = new EnvironmentVariableScope("OPENAI_API_KEY", "test-key");
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000206");
        var catalogPath = Path.Combine(tempRoot.Path, "CreateParcel_doc_types.json");
        var sourcePath = Path.Combine(layout.SourceDirectory, "BELLEV029GEOLANCOMSHEET.pdf");
        var planPath = Path.Combine(layout.SourceDirectory, "BELLEV029GEOLAN20230811.pdf");
        File.WriteAllText(catalogPath, BuildCatalogJson(includeStructuredPoints: false));
        File.WriteAllText(sourcePath, "computation");
        File.WriteAllText(planPath, "plan");

        var reviewOutputPath = Path.Combine(layout.WorkingDirectory, "100000206_review_data.json");
        var fakeRunner = new FakeProcessRunner((_, _, _, _, _) =>
        {
            File.WriteAllText(
                reviewOutputPath,
                """
                {
                  "transaction_number": "100000206",
                  "row_count": 1,
                  "rows": [
                    {
                      "point_identifier": "338",
                      "easting": "680920.044",
                      "northing": "639209.180"
                    }
                  ],
                  "outputs": {
                    "review_json": ""
                  }
                }
                """.Replace("\"\"", $"\"{reviewOutputPath.Replace("\\", "\\\\")}\""),
                encoding: System.Text.Encoding.UTF8);

            var stdout = $$"""
            {
              "transaction_number": "100000206",
              "row_count": 1,
              "outputs": {
                "review_json": "{{reviewOutputPath.Replace("\\", "\\\\")}}"
              }
            }
            """;

            return Task.FromResult(new ProcessRunResult(0, stdout, string.Empty, false));
        });

        var adapter = new CreateParcelDraftExtractionAdapter(fakeRunner, catalogPath);
        var context = CreateContext(layout, sourcePath, planPath);

        var result = adapter.ExecuteAsync(context).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Extraction adapter should succeed.");

        var reviewArtifactPath = Path.Combine(layout.WorkingDirectory, "extraction_review_data.json");
        TestAssert.True(File.Exists(reviewArtifactPath), "Extraction review artifact should be written.");

        using var reviewDocument = JsonDocument.Parse(File.ReadAllText(reviewArtifactPath));
        var root = reviewDocument.RootElement;
        TestAssert.Equal("GEOLAND_COMPUTATION_TABLE_V2", root.GetProperty("doc_type_id").GetString(), "Matched doc type id should be persisted.");
        TestAssert.Equal("GeoLand Computation Table", root.GetProperty("doc_type_name").GetString(), "Matched doc type name should be persisted.");
        TestAssert.Equal("computation_sheet", root.GetProperty("doc_type_family").GetString(), "Matched doc type family should be persisted.");
        TestAssert.Equal("pdf_text_structured_computation", root.GetProperty("extractor_id").GetString(), "Resolved extractor id should be persisted.");
        TestAssert.Equal("openai_table_pdf", root.GetProperty("active_extractor_id").GetString(), "Active extractor should reflect the runtime fallback route.");
        TestAssert.Equal("parcel_rows_with_group_breaks", root.GetProperty("geometry_mode").GetString(), "Resolved geometry mode should be persisted.");
        TestAssert.Equal("geoland_computation_v1", root.GetProperty("validation_profile").GetString(), "Resolved validation profile should be persisted.");
        TestAssert.Equal("point_review", root.GetProperty("review_mode").GetString(), "Resolved review mode should be persisted.");
        TestAssert.True(root.GetProperty("doc_type_catalog_path").GetString()!.EndsWith("CreateParcel_doc_types.json", StringComparison.OrdinalIgnoreCase), "Catalog path should be persisted.");
        TestAssert.Equal("computation_source", root.GetProperty("primary_source_role").GetString(), "Primary source role should be persisted.");
        TestAssert.Equal("BELLEV029GEOLANCOMSHEET.pdf", root.GetProperty("primary_source_file").GetString(), "Primary source file should be persisted.");
        TestAssert.True(root.GetProperty("ai_requested").GetBoolean(), "AI request should be persisted.");
        TestAssert.Equal("openai", root.GetProperty("provider_used").GetString(), "Provider should be persisted.");
        TestAssert.Equal("pdf_text_structured_computation", root.GetProperty("extraction_method").GetString(), "Extraction method should preserve the attempted text-first route while active_extractor_id captures the runtime fallback.");

        var iniPath = Path.Combine(layout.WorkingDirectory, "CreateParcelFromFile_case.ini");
        var iniText = File.ReadAllText(iniPath);
        TestAssert.True(iniText.Contains("matched_doc_type_id = GEOLAND_COMPUTATION_TABLE_V2", StringComparison.Ordinal), "Generated ini should include matched doc type id.");
        TestAssert.True(iniText.Contains($"catalog_json = {catalogPath}", StringComparison.Ordinal), "Generated ini should include document type catalog path.");
        TestAssert.True(iniText.Contains("matched_extractor_id = pdf_text_structured_computation", StringComparison.Ordinal), "Generated ini should include configured extractor id.");
        TestAssert.True(iniText.Contains("matched_active_extractor_id = openai_table_pdf", StringComparison.Ordinal), "Generated ini should include active extractor id.");
    }

    public static void ExtractionAdapterPrefersStructuredPointsSourceOverPdfParsing()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000207");
        var catalogPath = Path.Combine(tempRoot.Path, "CreateParcel_doc_types.json");
        var csvSourcePath = Path.Combine(layout.SourceDirectory, "points.csv");
        var computationPath = Path.Combine(layout.SourceDirectory, "GenericComputationSheet.pdf");
        var planPath = Path.Combine(layout.SourceDirectory, "PlanReference.pdf");
        File.WriteAllText(catalogPath, BuildCatalogJson(includeStructuredPoints: true));
        File.WriteAllText(csvSourcePath, "point_id,easting,northing");
        File.WriteAllText(computationPath, "computation");
        File.WriteAllText(planPath, "plan");

        var reviewOutputPath = Path.Combine(layout.WorkingDirectory, "100000207_review_data.json");
        var fakeRunner = new FakeProcessRunner((_, _, _, _, _) =>
        {
            File.WriteAllText(
                reviewOutputPath,
                $$"""
                {
                  "transaction_number": "100000207",
                  "row_count": 1,
                  "rows": [
                    {
                      "point_identifier": "P1",
                      "easting": "1000.0",
                      "northing": "2000.0"
                    }
                  ],
                  "outputs": {
                    "review_json": "{{reviewOutputPath.Replace("\\", "\\\\")}}"
                  }
                }
                """);
            var stdout = $$"""
            {
              "transaction_number": "100000207",
              "row_count": 1,
              "outputs": {
                "review_json": "{{reviewOutputPath.Replace("\\", "\\\\")}}"
              }
            }
            """;
            return Task.FromResult(new ProcessRunResult(0, stdout, string.Empty, false));
        });

        var adapter = new CreateParcelDraftExtractionAdapter(fakeRunner, catalogPath);
        var context = CreateContext(
            layout,
            csvSourcePath,
            planPath,
            additionalSources: new[]
            {
                new ManifestSourceFile("innola:computation", computationPath, ".pdf", 10, "2026-06-16T00:00:00Z", "computation_source")
            },
            pointsSourceRole: "points_computation");

        var result = adapter.ExecuteAsync(context).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Structured points route should succeed.");
        var reviewArtifactPath = Path.Combine(layout.WorkingDirectory, "extraction_review_data.json");
        using var reviewDocument = JsonDocument.Parse(File.ReadAllText(reviewArtifactPath));
        var root = reviewDocument.RootElement;
        TestAssert.Equal("STRUCTURED_POINTS_TEXT_V1", root.GetProperty("doc_type_id").GetString(), "Structured points doc type should win routing.");
        TestAssert.Equal("structured_csv_points", root.GetProperty("active_extractor_id").GetString(), "Structured extractor should be active.");
        TestAssert.Equal("points_computation", root.GetProperty("primary_source_role").GetString(), "Structured file should become the primary source.");
        TestAssert.Equal("points.csv", root.GetProperty("primary_source_file").GetString(), "Structured source file should be persisted.");
        TestAssert.True(!root.GetProperty("ai_requested").GetBoolean(), "Structured source should not request AI extraction.");
        TestAssert.Equal("structured_csv_points", root.GetProperty("provider_used").GetString(), "Structured extractor should be recorded as provider used.");

        var iniPath = Path.Combine(layout.WorkingDirectory, "CreateParcelFromFile_case.ini");
        var iniText = File.ReadAllText(iniPath);
        TestAssert.True(iniText.Contains("case1_extraction_mode = structured_points", StringComparison.Ordinal), "Structured sources should switch extraction mode.");
        TestAssert.True(iniText.Contains("points_file = points.csv", StringComparison.Ordinal), "Structured source should feed points_file.");
    }

    public static void ExtractionAdapterRecordsAiFallbackWhenAiIsDisabled()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000208");
        var catalogPath = Path.Combine(tempRoot.Path, "CreateParcel_doc_types.json");
        var sourcePath = Path.Combine(layout.SourceDirectory, "BELLEV029GEOLANCOMSHEET.pdf");
        var planPath = Path.Combine(layout.SourceDirectory, "BELLEV029GEOLAN20230811.pdf");
        File.WriteAllText(catalogPath, BuildCatalogJson(includeStructuredPoints: false));
        File.WriteAllText(sourcePath, "computation");
        File.WriteAllText(planPath, "plan");

        var reviewOutputPath = Path.Combine(layout.WorkingDirectory, "100000208_review_data.json");
        var fakeRunner = new FakeProcessRunner((_, _, _, _, _) =>
        {
            File.WriteAllText(
                reviewOutputPath,
                $$"""
                {
                  "transaction_number": "100000208",
                  "row_count": 1,
                  "rows": [
                    {
                      "point_identifier": "338",
                      "easting": "680920.044",
                      "northing": "639209.180"
                    }
                  ],
                  "outputs": {
                    "review_json": "{{reviewOutputPath.Replace("\\", "\\\\")}}"
                  }
                }
                """);
            var stdout = $$"""
            {
              "transaction_number": "100000208",
              "row_count": 1,
              "outputs": {
                "review_json": "{{reviewOutputPath.Replace("\\", "\\\\")}}"
              }
            }
            """;
            return Task.FromResult(new ProcessRunResult(0, stdout, string.Empty, false));
        });

        var adapter = new CreateParcelDraftExtractionAdapter(fakeRunner, catalogPath);
        var context = CreateContext(layout, sourcePath, planPath, openAiEnabled: false);

        var result = adapter.ExecuteAsync(context).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Fallback extraction should still succeed when AI is disabled.");
        var reviewArtifactPath = Path.Combine(layout.WorkingDirectory, "extraction_review_data.json");
        using var reviewDocument = JsonDocument.Parse(File.ReadAllText(reviewArtifactPath));
        var root = reviewDocument.RootElement;
        TestAssert.True(root.GetProperty("ai_requested").GetBoolean(), "Doc type should still request AI-capable extraction.");
        TestAssert.True(!root.GetProperty("ai_available").GetBoolean(), "AI availability should be false when disabled.");
        TestAssert.True(!root.GetProperty("ai_used").GetBoolean(), "AI should not be marked used when disabled.");
        TestAssert.Equal("ocr_table_pdf", root.GetProperty("active_extractor_id").GetString(), "First fallback extractor should become active.");
        TestAssert.Equal("ocr_table_pdf", root.GetProperty("provider_used").GetString(), "Fallback provider should be persisted.");
        TestAssert.Equal("text_first_fallback_requested", root.GetProperty("fallback_reason").GetString(), "Fallback reason should reflect the configured text-first route yielding to the non-AI fallback chain.");
    }

    public static void ExtractionAdapterPreservesGroupingFieldsForGroupedGeometryModes()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000209");
        var catalogPath = Path.Combine(tempRoot.Path, "CreateParcel_doc_types.json");
        var sourcePath = Path.Combine(layout.SourceDirectory, "BELLEV029GEOLANCOMSHEET.pdf");
        var planPath = Path.Combine(layout.SourceDirectory, "BELLEV029GEOLAN20230811.pdf");
        File.WriteAllText(catalogPath, BuildCatalogJson(includeStructuredPoints: false));
        File.WriteAllText(sourcePath, "computation");
        File.WriteAllText(planPath, "plan");

        var reviewOutputPath = Path.Combine(layout.WorkingDirectory, "100000209_review_data.json");
        var fakeRunner = new FakeProcessRunner((_, _, _, _, _) =>
        {
            File.WriteAllText(
                reviewOutputPath,
                $$"""
                {
                  "transaction_number": "100000209",
                  "row_count": 2,
                  "rows": [
                    {
                      "point_identifier": "338",
                      "easting": "680920.044",
                      "northing": "639209.180"
                    },
                    {
                      "point_identifier": "339",
                      "easting": "680921.044",
                      "northing": "639210.180"
                    }
                  ],
                  "outputs": {
                    "review_json": "{{reviewOutputPath.Replace("\\", "\\\\")}}"
                  }
                }
                """);
            var stdout = $$"""
            {
              "transaction_number": "100000209",
              "row_count": 2,
              "outputs": {
                "review_json": "{{reviewOutputPath.Replace("\\", "\\\\")}}"
              }
            }
            """;
            return Task.FromResult(new ProcessRunResult(0, stdout, string.Empty, false));
        });

        var adapter = new CreateParcelDraftExtractionAdapter(fakeRunner, catalogPath);
        var context = CreateContext(layout, sourcePath, planPath);

        var result = adapter.ExecuteAsync(context).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Grouped geometry enrichment should succeed.");
        var reviewArtifactPath = Path.Combine(layout.WorkingDirectory, "extraction_review_data.json");
        using var reviewDocument = JsonDocument.Parse(File.ReadAllText(reviewArtifactPath));
        var root = reviewDocument.RootElement;
        TestAssert.Equal("inferred_single_group", root.GetProperty("grouping_status").GetString(), "Grouping status should explain inferred grouping.");
        TestAssert.True(root.GetProperty("grouping_requires_review").GetBoolean(), "Missing explicit group breaks should be flagged for review.");

        var rows = root.GetProperty("rows").EnumerateArray().ToArray();
        TestAssert.Equal("parcel-001", rows[0].GetProperty("parcel_group_id").GetString(), "First row should receive inferred group id.");
        TestAssert.Equal(1, rows[0].GetProperty("sequence_in_group").GetInt32(), "First row should receive sequence 1.");
        TestAssert.Equal("parcel-001", rows[1].GetProperty("parcel_group_id").GetString(), "Second row should remain in inferred group.");
        TestAssert.Equal(2, rows[1].GetProperty("sequence_in_group").GetInt32(), "Second row should receive sequence 2.");
    }

    public static void ExtractionAdapterBlocksUnsupportedLowConfidenceAutomationAndWritesRouteDiagnostics()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000210");
        var catalogPath = Path.Combine(tempRoot.Path, "CreateParcel_doc_types.json");
        var sourcePath = Path.Combine(layout.SourceDirectory, "mystery_document.pdf");
        File.WriteAllText(catalogPath, BuildCatalogJson(includeStructuredPoints: false));
        File.WriteAllText(sourcePath, "unknown");

        var adapter = new CreateParcelDraftExtractionAdapter(new FakeProcessRunner((_, _, _, _, _) => throw new InvalidOperationException("Should not execute.")), catalogPath);
        var context = CreateContext(layout, sourcePath, planPath: null);

        var result = adapter.ExecuteAsync(context).GetAwaiter().GetResult();

        TestAssert.True(!result.Success, "Unsupported document family should block automation.");
        TestAssert.True((result.ErrorMessage ?? string.Empty).Contains("document family", StringComparison.OrdinalIgnoreCase)
                        || (result.ErrorMessage ?? string.Empty).Contains("catalog", StringComparison.OrdinalIgnoreCase), "Failure should guide the operator toward configuration review.");
        var routeArtifactPath = Path.Combine(layout.WorkingDirectory, "extraction_route.json");
        TestAssert.True(File.Exists(routeArtifactPath), "Route diagnostics should still be written on blocked automation.");
        using var routeDocument = JsonDocument.Parse(File.ReadAllText(routeArtifactPath));
        var root = routeDocument.RootElement;
        TestAssert.True(root.GetProperty("unsafe_to_automate").GetBoolean(), "Blocked route should be marked unsafe.");
        TestAssert.Equal("UNKNOWN_GENERIC_SOURCE_V1", root.GetProperty("doc_type_id").GetString(), "Blocked route should expose the fallback document type.");
    }

    public static void ExtractionAdapterUsesTextFirstStructuredPdfWhenConfiguredAndTextProbeSucceeds()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000211");
        var catalogPath = Path.Combine(tempRoot.Path, "CreateParcel_doc_types.json");
        var sourcePath = Path.Combine(layout.SourceDirectory, "BELLEV029GEOLANCOMSHEET.pdf");
        var planPath = Path.Combine(layout.SourceDirectory, "BELLEV029GEOLAN20230811.pdf");
        File.WriteAllText(catalogPath, BuildCatalogJson(includeStructuredPoints: false));
        File.WriteAllText(sourcePath, "computation");
        File.WriteAllText(planPath, "plan");

        var reviewOutputPath = Path.Combine(layout.WorkingDirectory, "extraction_review_data.json");
        var runCalls = new List<string>();
        var fakeRunner = new FakeProcessRunner((_, arguments, _, _, _) =>
        {
            runCalls.Add(arguments);
            if (arguments.Contains("pdf_text_structured_extraction.py", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(
                    reviewOutputPath,
                    $$"""
                    {
                      "transaction_number": "100000211",
                      "row_count": 2,
                      "rows": [
                        {
                          "parcel_group_id": "parcel-001",
                          "parcel_name": "110402901",
                          "point_order": 1,
                          "segment_no": 1,
                          "point_identifier": "339",
                          "easting": "680920.044",
                          "northing": "639209.180",
                          "source_page": 1
                        },
                        {
                          "parcel_group_id": "parcel-002",
                          "parcel_name": "110402902",
                          "point_order": 1,
                          "segment_no": 1,
                          "point_identifier": "440",
                          "easting": "680930.044",
                          "northing": "639219.180",
                          "source_page": 2,
                          "is_boundary_break": true
                        }
                      ],
                      "outputs": {
                        "review_json": "{{reviewOutputPath.Replace("\\", "\\\\")}}"
                      }
                    }
                    """);

                var stdout = $$"""
                {
                  "status": "success",
                  "text_layer_available": true,
                  "parser_status": "parsed",
                  "parsed_parcel_count": 2,
                  "parsed_row_count": 2,
                  "outputs": {
                    "review_json": "{{reviewOutputPath.Replace("\\", "\\\\")}}"
                  }
                }
                """;
                return Task.FromResult(new ProcessRunResult(0, stdout, string.Empty, false));
            }

            throw new InvalidOperationException("Fallback runner should not execute when text-first parsing succeeds.");
        });

        var adapter = new CreateParcelDraftExtractionAdapter(fakeRunner, catalogPath);
        var context = CreateContext(layout, sourcePath, planPath, openAiEnabled: false);

        var result = adapter.ExecuteAsync(context).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Text-first extraction should succeed.");
        TestAssert.Equal(1, runCalls.Count, "Only the text-first helper should execute.");
        var reviewArtifactPath = Path.Combine(layout.WorkingDirectory, "extraction_review_data.json");
        using var reviewDocument = JsonDocument.Parse(File.ReadAllText(reviewArtifactPath));
        var root = reviewDocument.RootElement;
        TestAssert.Equal("pdf_text_structured_computation", root.GetProperty("active_extractor_id").GetString(), "Text-first extractor should remain active on success.");
        TestAssert.Equal("pdf_text_structured_computation", root.GetProperty("extraction_method").GetString(), "Review artifact should record the structured text method.");
        TestAssert.True(!root.GetProperty("ai_used").GetBoolean(), "Deterministic text-first extraction should not mark AI as used.");
        TestAssert.Equal("pdf_text_structured_computation", root.GetProperty("provider_used").GetString(), "Provider should reflect the deterministic structured text parser.");
        TestAssert.True(root.GetProperty("text_layer_available").GetBoolean(), "Text-layer detection should be persisted.");
        TestAssert.Equal("parsed", root.GetProperty("text_layer_probe_status").GetString(), "Probe status should indicate parsed text.");

        var rows = root.GetProperty("rows").EnumerateArray().ToArray();
        TestAssert.Equal("parcel-001", rows[0].GetProperty("parcel_group_id").GetString(), "First parcel group should be preserved.");
        TestAssert.Equal("parcel-002", rows[1].GetProperty("parcel_group_id").GetString(), "Second parcel group should be preserved.");
        TestAssert.Equal(1, rows[0].GetProperty("point_order").GetInt32(), "Point order should be preserved from the text parser.");
        TestAssert.Equal(1, rows[1].GetProperty("point_order").GetInt32(), "Point order should reset per parcel group.");
    }

    public static void ExtractionAdapterFallsBackFromTextFirstWhenNoUsableTextLayerExists()
    {
        using var openAiKeyScope = new EnvironmentVariableScope("OPENAI_API_KEY", "test-key");
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000212");
        var catalogPath = Path.Combine(tempRoot.Path, "CreateParcel_doc_types.json");
        var sourcePath = Path.Combine(layout.SourceDirectory, "BELLEV029GEOLANCOMSHEET.pdf");
        var planPath = Path.Combine(layout.SourceDirectory, "BELLEV029GEOLAN20230811.pdf");
        File.WriteAllText(catalogPath, BuildCatalogJson(includeStructuredPoints: false));
        File.WriteAllText(sourcePath, "computation");
        File.WriteAllText(planPath, "plan");

        var reviewOutputPath = Path.Combine(layout.WorkingDirectory, "100000212_review_data.json");
        var runCalls = new List<string>();
        var fakeRunner = new FakeProcessRunner((_, arguments, _, _, _) =>
        {
            runCalls.Add(arguments);
            if (arguments.Contains("pdf_text_structured_extraction.py", StringComparison.OrdinalIgnoreCase))
            {
                var stdout = """
                {
                  "status": "fallback_requested",
                  "text_layer_available": false,
                  "parser_status": "no_usable_text_layer",
                  "fallback_reason": "no_usable_text_layer",
                  "parsed_parcel_count": 0,
                  "parsed_row_count": 0
                }
                """;
                return Task.FromResult(new ProcessRunResult(0, stdout, string.Empty, false));
            }

            File.WriteAllText(
                reviewOutputPath,
                $$"""
                {
                  "transaction_number": "100000212",
                  "row_count": 1,
                  "rows": [
                    {
                      "point_identifier": "338",
                      "easting": "680920.044",
                      "northing": "639209.180"
                    }
                  ],
                  "outputs": {
                    "review_json": "{{reviewOutputPath.Replace("\\", "\\\\")}}"
                  }
                }
                """);

            var fallbackStdout = $$"""
            {
              "transaction_number": "100000212",
              "row_count": 1,
              "outputs": {
                "review_json": "{{reviewOutputPath.Replace("\\", "\\\\")}}"
              }
            }
            """;
            return Task.FromResult(new ProcessRunResult(0, fallbackStdout, string.Empty, false));
        });

        var adapter = new CreateParcelDraftExtractionAdapter(fakeRunner, catalogPath);
        var context = CreateContext(layout, sourcePath, planPath);

        var result = adapter.ExecuteAsync(context).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Fallback extraction should succeed after text-first probe fallback.");
        TestAssert.Equal(2, runCalls.Count, "Text-first probe and fallback extractor should both execute.");
        TestAssert.True(runCalls[0].Contains("pdf_text_structured_extraction.py", StringComparison.OrdinalIgnoreCase), "The first call should be the text-first helper.");

        var routeArtifactPath = Path.Combine(layout.WorkingDirectory, "extraction_route.json");
        using var routeDocument = JsonDocument.Parse(File.ReadAllText(routeArtifactPath));
        var routeRoot = routeDocument.RootElement;
        TestAssert.Equal("openai_table_pdf", routeRoot.GetProperty("active_extractor_id").GetString(), "Route diagnostics should record the runtime fallback extractor.");
        TestAssert.Equal("no_usable_text_layer", routeRoot.GetProperty("fallback_reason").GetString(), "Fallback reason should explain the probe outcome.");
        TestAssert.True(!routeRoot.GetProperty("text_layer_available").GetBoolean(), "Route diagnostics should preserve text-layer availability.");
    }

    public static void ExtractionAdapterLegacyCatalogUsesDeterministicTextRouteMetadataOnSuccess()
    {
        using var openAiKeyScope = new EnvironmentVariableScope("OPENAI_API_KEY", "test-key");
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000213");
        var catalogPath = Path.Combine(tempRoot.Path, "CreateParcel_doc_types.json");
        var sourcePath = Path.Combine(layout.SourceDirectory, "LegacyComputationSheet.pdf");
        var planPath = Path.Combine(layout.SourceDirectory, "LegacyPlan.pdf");
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
        File.WriteAllText(sourcePath, "computation");
        File.WriteAllText(planPath, "plan");

        var reviewOutputPath = Path.Combine(layout.WorkingDirectory, "extraction_review_data.json");
        var fakeRunner = new FakeProcessRunner((_, arguments, _, _, _) =>
        {
            if (!arguments.Contains("pdf_text_structured_extraction.py", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Legacy compatibility route should succeed in the text-first helper without invoking fallback extraction.");
            }

            File.WriteAllText(
                reviewOutputPath,
                $$"""
                {
                  "transaction_number": "100000213",
                  "row_count": 1,
                  "rows": [
                    {
                      "parcel_group_id": "parcel-001",
                      "parcel_name": "Legacy Parcel",
                      "point_order": 1,
                      "point_identifier": "339",
                      "easting": "680920.044",
                      "northing": "639209.180",
                      "source_page": 1
                    }
                  ],
                  "outputs": {
                    "review_json": "{{reviewOutputPath.Replace("\\", "\\\\")}}"
                  }
                }
                """);

            var stdout = $$"""
            {
              "status": "success",
              "text_layer_available": true,
              "parser_status": "parsed",
              "parsed_parcel_count": 1,
              "parsed_row_count": 1,
              "outputs": {
                "review_json": "{{reviewOutputPath.Replace("\\", "\\\\")}}"
              }
            }
            """;
            return Task.FromResult(new ProcessRunResult(0, stdout, string.Empty, false));
        });

        var adapter = new CreateParcelDraftExtractionAdapter(fakeRunner, catalogPath);
        var context = CreateContext(layout, sourcePath, planPath);

        var result = adapter.ExecuteAsync(context).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Legacy compatibility route should succeed.");
        using var reviewDocument = JsonDocument.Parse(File.ReadAllText(reviewOutputPath));
        var root = reviewDocument.RootElement;
        TestAssert.Equal("pdf_text_structured_computation", root.GetProperty("extractor_id").GetString(), "Legacy compatibility should map computation sheets to the deterministic text-first extractor.");
        TestAssert.Equal("pdf_text_structured_computation", root.GetProperty("active_extractor_id").GetString(), "Runtime metadata should preserve the deterministic active extractor.");
        TestAssert.True(root.GetProperty("ai_requested").GetBoolean(), "Legacy route should still advertise AI-capable fallback availability.");
        TestAssert.True(!root.GetProperty("ai_used").GetBoolean(), "Legacy deterministic success should not mark AI as used.");
        TestAssert.Equal("pdf_text_structured_computation", root.GetProperty("provider_used").GetString(), "Provider should reflect the deterministic parser on success.");
        TestAssert.Equal("pdf_text_structured_computation", root.GetProperty("extraction_method").GetString(), "Extraction method should reflect the text-first execution path.");
    }

    private static WorkflowScriptExecutionContext CreateContext(
        CaseFolderLayout layout,
        string sourcePath,
        string? planPath,
        IReadOnlyList<ManifestSourceFile>? additionalSources = null,
        string pointsSourceRole = "computation_source",
        bool openAiEnabled = true)
    {
        var sourceFiles = new List<ManifestSourceFile>
        {
            new("innola:computation", sourcePath, Path.GetExtension(sourcePath), 10, "2026-06-16T00:00:00Z", pointsSourceRole)
        };
        if (!string.IsNullOrWhiteSpace(planPath))
        {
            sourceFiles.Add(new ManifestSourceFile("innola:plan", planPath, Path.GetExtension(planPath), 10, "2026-06-16T00:00:00Z", "plan_map_reference"));
        }

        if (additionalSources is not null)
        {
            sourceFiles.AddRange(additionalSources);
        }

        var manifest = ManifestDocument.CreateInitial("100000206", "run-1", new DateTimeOffset(2026, 6, 16, 0, 0, 0, TimeSpan.Zero), "tester") with
        {
            Payload = new ManifestPayload(
                "preflight_passed",
                sourceFiles,
                null,
                new ManifestInnolaTransaction(
                    "txn-1",
                    "100000206",
                    "task-1",
                    "Assign Computation Task",
                    "parcel_workflow",
                    "Plan Examination",
                    null,
                    "tester",
                    "tester",
                    "Super Group",
                    null,
                    null,
                    "2026-06-16T00:00:00Z"),
                null,
                null,
                "scenario_a_two_pdf",
                "scenario_a_two_pdf_v1",
                "1.0.0",
                new WorkflowScriptPlan(
                    "1.0.0",
                    "scenario_a_two_pdf_v1",
                    "1.0.0",
                    "scenario_a_two_pdf",
                    "2026-06-16T00:00:00Z",
                    "hash-123",
                    new[]
                    {
                        new WorkflowScriptStep(
                            "extract_points_from_computation",
                            "extraction_adapter",
                            "extract_points_from_computation_pdf",
                            new[] { "computation_source" },
                            new[] { "working/extraction_points.json" },
                            new Dictionary<string, string>(),
                            300,
                            true,
                            "openai",
                            "local")
                    }))
        };

        var step = manifest.Payload.ScriptPlan!.Steps[0];
        var executionSettings = new WorkflowExecutionSettings(
            Path.Combine(layout.RootDirectory, "python.exe"),
            Path.Combine(layout.RootDirectory, "CreateParcelFromFile.py"),
            "output_adapter.py",
            "normal",
            120,
            null,
            null,
            "validation_adapter.py",
            null);

        File.WriteAllText(executionSettings.PythonExecutable, "stub");
        File.WriteAllText(executionSettings.CreateParcelScriptPath, "stub");

        return new WorkflowScriptExecutionContext(
            layout,
            manifest,
            manifest.Payload.ScriptPlan,
            step,
            new WorkflowRuleSettings("openai", openAiEnabled, "balanced", "gpt-4.1-mini", "OPENAI_API_KEY", "local"),
            executionSettings,
            new Dictionary<string, object?>());
    }

    private static CaseFolderLayout CreateLayout(string root, string transactionNumber)
    {
        var layout = CaseFolderLayout.For(root, transactionNumber);
        Directory.CreateDirectory(layout.RootDirectory);
        Directory.CreateDirectory(layout.SourceDirectory);
        Directory.CreateDirectory(layout.WorkingDirectory);
        Directory.CreateDirectory(layout.OutputDirectory);
        Directory.CreateDirectory(layout.ReportsDirectory);
        Directory.CreateDirectory(layout.LogsDirectory);
        return layout;
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        private readonly Func<string, string, TimeSpan, IReadOnlyDictionary<string, string?>?, CancellationToken, Task<ProcessRunResult>> runAsync;

        public FakeProcessRunner(Func<string, string, TimeSpan, IReadOnlyDictionary<string, string?>?, CancellationToken, Task<ProcessRunResult>> runAsync)
        {
            this.runAsync = runAsync;
        }

        public Task<ProcessRunResult> RunAsync(string executablePath, string arguments, TimeSpan timeout, IReadOnlyDictionary<string, string?>? environmentVariables = null, CancellationToken cancellationToken = default)
        {
            return runAsync(executablePath, arguments, timeout, environmentVariables, cancellationToken);
        }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string variableName;
        private readonly string? previousValue;

        public EnvironmentVariableScope(string variableName, string? value)
        {
            this.variableName = variableName;
            previousValue = Environment.GetEnvironmentVariable(variableName);
            Environment.SetEnvironmentVariable(variableName, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(variableName, previousValue);
        }
    }

    private static string BuildCatalogJson(bool includeStructuredPoints)
    {
        var structured = includeStructuredPoints
            ? """
                ,
                {
                  "doc_type_id": "STRUCTURED_POINTS_TEXT_V1",
                  "name": "Structured Points Import",
                  "family": "structured_points",
                  "priority": 250,
                  "match": {
                    "source_kinds": [".csv", ".txt"],
                    "filename_contains_any": [],
                    "score_threshold": 1
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
                    "extractor_id": "structured_csv_points",
                    "parser_mode": "structured_points",
                    "prefers_text_layer": false,
                    "ai_assisted": false,
                    "ai_profile": "",
                    "fallback_extractors": ["structured_txt_points"],
                    "expected_outputs": ["rows"]
                  },
                  "schema": {
                    "metadata_fields": [],
                    "parcel_fields": [],
                    "row_fields": ["point_id", "northing", "easting"]
                  },
                  "geometry": {
                    "geometry_mode": "point_list_only",
                    "point_source": "row_vertices",
                    "line_builder": "from_to_pairs",
                    "polygon_builder": "group_closed_ring",
                    "supports_multi_parcel": true,
                    "supports_boundary_breaks": true,
                    "requires_grouping": true,
                    "closing_rule": "do_not_chain_across_groups"
                  },
                  "validation": {
                    "validation_profile": "structured_points_v1",
                    "required_metadata_fields": [],
                    "required_row_fields": ["point_id", "northing", "easting"],
                    "minimum_expected_rows": 1,
                    "minimum_expected_parcels": 1,
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
              """
            : string.Empty;

        return $$"""
        {
          "schema_version": "2.0",
          "default_doc_type_id": "UNKNOWN_GENERIC_SOURCE_V1",
          "doc_types": [
            {
              "doc_type_id": "GEOLAND_COMPUTATION_TABLE_V2",
              "name": "GeoLand Computation Table",
              "family": "computation_sheet",
              "priority": 200,
              "match": {
                "source_kinds": [".pdf"],
                "filename_contains_any": ["GEOLAN", "COMSHEET"],
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
                "extractor_id": "pdf_text_structured_computation",
                "parser_mode": "parcel_block_rows",
                "prefers_text_layer": true,
                "ai_assisted": true,
                "ai_profile": "survey_table_vision_v1",
                "fallback_extractors": ["openai_table_pdf", "ocr_table_pdf", "text_regex_pdf"],
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
                "validation_profile": "geoland_computation_v1",
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
            }{{structured}}
          ]
        }
        """;
    }
}
