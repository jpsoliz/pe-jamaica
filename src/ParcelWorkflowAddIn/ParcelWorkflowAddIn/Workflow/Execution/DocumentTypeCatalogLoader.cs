using System.IO;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Workflow.Execution;

public sealed class DocumentTypeCatalogLoader
{
    private const string SupportedSchemaVersion = "2.0";
    private readonly string catalogPath;

    private static readonly IReadOnlyList<DocumentTypeDefinition> DefaultDocumentTypes = new[]
    {
        new DocumentTypeDefinition(
            "UNKNOWN_GENERIC_SOURCE_V1",
            "Unknown Generic Source",
            "unknown",
            0,
            new DocumentTypeMatchDefinition(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), 999),
            new DocumentTypeClassifierDefinition("weighted_match", new DocumentTypeClassifierWeights(20, 15, 25, 30)),
            new DocumentTypeExtractionDefinition("manual_only_source", "manual", false, false, string.Empty, Array.Empty<string>(), Array.Empty<string>()),
            new DocumentTypeSchemaDefinition(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()),
            new DocumentTypeGeometryDefinition("manual_constructed", "manual", "manual", "manual", false, false, false, "manual_only"),
            new DocumentTypeValidationDefinition("generic_minimum", Array.Empty<string>(), Array.Empty<string>(), 0, 0, Array.Empty<string>(), new[] { "low_match_confidence" }),
            new DocumentTypeReviewDefinition("point_review", "embedded_or_external", true, true, true, true),
            new DocumentTypeOutputDefinition(new[] { "normal_fgdb", "parcel_fabric", "enterprise_working_layers", "enterprise_parcel_fabric" }, "normal_fgdb")),
        new DocumentTypeDefinition(
            "SINGLE_PARCEL_SURVEY_PLAN_PDF_V1",
            "Single Parcel Survey Plan PDF",
            "survey_plan_pdf",
            220,
            new DocumentTypeMatchDefinition(
                new[] { ".pdf", ".tif", ".tiff", ".png", ".jpg", ".jpeg" },
                new[] { "SURVEY", "PLAN", "DOC_PLAN", "Cadastral", "Parcel" },
                Array.Empty<string>(),
                new[] { "JAD 2001", "Surveyed by", "Instrument", "Area", "Parish", "Grid North" },
                Array.Empty<string>(),
                20),
            new DocumentTypeClassifierDefinition("weighted_match", new DocumentTypeClassifierWeights(20, 15, 25, 30)),
            new DocumentTypeExtractionDefinition("survey_plan_ocr_vision", "single_parcel_survey_plan", false, true, "single_parcel_survey_plan_vision_v1", new[] { "manual_survey_plan_review" }, new[] { "metadata", "points", "segments", "parties", "adjacent_owners" }),
            new DocumentTypeSchemaDefinition(
                new[] { "parish", "document_area", "survey_date", "instrument", "surveyed_by", "coordinate_system", "north_arrow" },
                new[] { "parcel_name", "lot_number", "owner_names", "representatives", "adjacent_owners" },
                new[] { "point_id", "northing", "easting", "bearing_txt", "distance_txt", "source_page", "source_zone", "confidence" }),
            new DocumentTypeGeometryDefinition("single_parcel_survey_plan", "coordinate_table", "bearing_distance_segments", "single_closed_ring", false, true, true, "single_parcel_closure"),
            new DocumentTypeValidationDefinition("single_parcel_survey_plan_v1", new[] { "parish", "coordinate_system" }, new[] { "point_id", "northing", "easting" }, 1, 1, new[] { "missing_survey_plan_pdf", "missing_coordinates" }, new[] { "low_confidence_ocr", "missing_optional_metadata" }),
            new DocumentTypeReviewDefinition("point_line_review", "embedded_or_external", true, true, true, true),
            new DocumentTypeOutputDefinition(new[] { "normal_fgdb", "enterprise_working_layers" }, "normal_fgdb")),
        new DocumentTypeDefinition(
            "GEOLAND_COMPUTATION_TABLE_V2",
            "GeoLand Computation Table",
            "computation_sheet",
            200,
            new DocumentTypeMatchDefinition(
                new[] { ".pdf", ".tif", ".tiff", ".png", ".jpg", ".jpeg" },
                new[] { "GEOLAN", "GEOLAND", "COMSHEET", "COMPUTATION", "COMPUTER SHEET", "COMPUTER" },
                Array.Empty<string>(),
                new[] { "Geoland Title Limited", "PROPERTY NAME:", "From PNT Bearing Distance Northing Easting To Pnt" },
                Array.Empty<string>(),
                20),
            new DocumentTypeClassifierDefinition("weighted_match", new DocumentTypeClassifierWeights(20, 15, 25, 30)),
            new DocumentTypeExtractionDefinition("pdf_text_structured_computation", "parcel_block_rows", true, false, "survey_table_vision_v1", new[] { "openai_table_pdf", "ocr_table_pdf", "text_regex_pdf" }, new[] { "metadata", "parcel_groups", "rows" }),
            new DocumentTypeSchemaDefinition(
                new[] { "property_name", "parish", "date_of_survey", "surveyor", "block", "sheet" },
                new[] { "parcel_number", "area_square_meters" },
                new[] { "parcel_group_id", "sequence_in_group", "from_point", "bearing", "distance_m", "northing", "easting", "to_point", "is_boundary_break" }),
            new DocumentTypeGeometryDefinition("parcel_rows_with_group_breaks", "row_vertices", "from_to_pairs", "group_closed_ring", true, true, true, "do_not_chain_across_groups"),
            new DocumentTypeValidationDefinition("geoland_computation_v1", new[] { "property_name", "parish" }, new[] { "parcel_group_id", "from_point", "northing", "easting" }, 20, 1, new[] { "missing_required_fields", "invalid_coordinates", "ungrouped_rows" }, new[] { "null_distance", "null_bearing", "low_match_confidence" }),
            new DocumentTypeReviewDefinition("point_review", "embedded_or_external", true, true, true, true),
            new DocumentTypeOutputDefinition(new[] { "normal_fgdb", "parcel_fabric", "enterprise_working_layers", "enterprise_parcel_fabric" }, "normal_fgdb")),
        new DocumentTypeDefinition(
            "GENERIC_COMPUTATION_TABLE_V2",
            "Generic Computation Table",
            "computation_sheet",
            100,
            new DocumentTypeMatchDefinition(
                new[] { ".pdf", ".tif", ".tiff", ".png", ".jpg", ".jpeg" },
                new[] { "COMPUTATION", "CAD MAP", "SHEET" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                20),
            new DocumentTypeClassifierDefinition("weighted_match", new DocumentTypeClassifierWeights(20, 15, 25, 30)),
            new DocumentTypeExtractionDefinition("pdf_text_structured_computation", "parcel_block_rows", true, false, "survey_table_vision_v1", new[] { "openai_table_pdf", "ocr_table_pdf", "text_regex_pdf" }, new[] { "metadata", "parcel_groups", "rows" }),
            new DocumentTypeSchemaDefinition(
                new[] { "volume", "folio", "block", "parish", "date_checked" },
                new[] { "parcel_name" },
                new[] { "parcel_group_id", "segment_no", "type", "course", "length_m", "north", "east", "is_boundary_break" }),
            new DocumentTypeGeometryDefinition("parcel_rows_with_group_breaks", "row_vertices", "from_to_pairs", "group_closed_ring", true, true, true, "do_not_chain_across_groups"),
            new DocumentTypeValidationDefinition("generic_computation_v1", new[] { "parish" }, new[] { "parcel_group_id", "north", "east" }, 1, 1, new[] { "missing_required_fields", "invalid_coordinates" }, new[] { "low_match_confidence" }),
            new DocumentTypeReviewDefinition("point_review", "embedded_or_external", true, true, true, true),
            new DocumentTypeOutputDefinition(new[] { "normal_fgdb", "parcel_fabric", "enterprise_working_layers", "enterprise_parcel_fabric" }, "normal_fgdb")),
        new DocumentTypeDefinition(
            "STRUCTURED_POINTS_TEXT_V1",
            "Structured Points Import",
            "structured_points",
            150,
            new DocumentTypeMatchDefinition(
                new[] { ".txt", ".csv" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                1),
            new DocumentTypeClassifierDefinition("weighted_match", new DocumentTypeClassifierWeights(20, 15, 25, 30)),
            new DocumentTypeExtractionDefinition("structured_csv_points", "structured_points", false, false, string.Empty, new[] { "structured_txt_points" }, new[] { "rows" }),
            new DocumentTypeSchemaDefinition(Array.Empty<string>(), Array.Empty<string>(), new[] { "point_id", "northing", "easting" }),
            new DocumentTypeGeometryDefinition("point_list_only", "row_vertices", "from_to_pairs", "group_closed_ring", true, true, true, "do_not_chain_across_groups"),
            new DocumentTypeValidationDefinition("structured_points_v1", Array.Empty<string>(), new[] { "point_id", "northing", "easting" }, 1, 1, new[] { "missing_required_fields", "invalid_coordinates" }, Array.Empty<string>()),
            new DocumentTypeReviewDefinition("point_review", "embedded_or_external", true, true, true, true),
            new DocumentTypeOutputDefinition(new[] { "normal_fgdb", "parcel_fabric", "enterprise_working_layers", "enterprise_parcel_fabric" }, "normal_fgdb"))
    };

    public DocumentTypeCatalogLoader(string catalogPath)
    {
        this.catalogPath = catalogPath;
    }

    public DocumentTypeCatalog Load()
    {
        if (!File.Exists(catalogPath))
        {
            return new DocumentTypeCatalog(
                catalogPath,
                UsingSafeDefaults: true,
                LoadWarning: "Document-type catalog file was not found. Safe Catalog V2 defaults are active.",
                SupportedSchemaVersion,
                "UNKNOWN_GENERIC_SOURCE_V1",
                DefaultDocumentTypes);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
            var root = document.RootElement;
            var schemaVersion = ReadString(root, "schema_version");
            return string.Equals(schemaVersion, SupportedSchemaVersion, StringComparison.OrdinalIgnoreCase)
                ? LoadV2(root)
                : LoadLegacy(root);
        }
        catch (Exception exception) when (exception is JsonException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or System.Security.SecurityException)
        {
            return new DocumentTypeCatalog(
                catalogPath,
                UsingSafeDefaults: true,
                LoadWarning: $"Document-type catalog could not be loaded ({exception.GetType().Name}). Safe Catalog V2 defaults are active.",
                SupportedSchemaVersion,
                "UNKNOWN_GENERIC_SOURCE_V1",
                DefaultDocumentTypes);
        }
    }

    private DocumentTypeCatalog LoadV2(JsonElement root)
    {
        var validationIssues = new List<string>();
        var defaultDocTypeId = ReadString(root, "default_doc_type_id") ?? "UNKNOWN_GENERIC_SOURCE_V1";
        var docTypes = ReadV2DocTypes(root, validationIssues);
        if (validationIssues.Count > 0 || docTypes.Count == 0)
        {
            return new DocumentTypeCatalog(
                catalogPath,
                UsingSafeDefaults: true,
                LoadWarning: $"Document-type catalog is partially invalid. Safe Catalog V2 defaults are active. {string.Join(" ", validationIssues)}",
                SupportedSchemaVersion,
                "UNKNOWN_GENERIC_SOURCE_V1",
                DefaultDocumentTypes);
        }

        return new DocumentTypeCatalog(catalogPath, UsingSafeDefaults: false, LoadWarning: null, SupportedSchemaVersion, defaultDocTypeId, MergeRequiredDefaults(docTypes, defaultDocTypeId));
    }

    private DocumentTypeCatalog LoadLegacy(JsonElement root)
    {
        var defaultDocTypeId = ReadString(root, "default_doc_type_id") ?? "UNKNOWN_GENERIC_SOURCE_V1";
        return new DocumentTypeCatalog(
            catalogPath,
            UsingSafeDefaults: false,
            LoadWarning: "Legacy document-type catalog loaded through V2 compatibility mapping.",
            "1.x-compat",
            defaultDocTypeId,
            MergeRequiredDefaults(ReadLegacyDocTypes(root), defaultDocTypeId));
    }

    private static IReadOnlyList<DocumentTypeDefinition> ReadV2DocTypes(JsonElement root, List<string> validationIssues)
    {
        if (!root.TryGetProperty("doc_types", out var docTypesNode) || docTypesNode.ValueKind != JsonValueKind.Array)
        {
            validationIssues.Add("doc_types must be a JSON array.");
            return Array.Empty<DocumentTypeDefinition>();
        }

        var results = new List<DocumentTypeDefinition>();
        var index = 0;
        foreach (var item in docTypesNode.EnumerateArray())
        {
            index++;
            if (item.ValueKind != JsonValueKind.Object)
            {
                validationIssues.Add($"Document type entry {index} must be an object.");
                continue;
            }

            var docTypeId = ReadRequiredString(item, "doc_type_id", validationIssues, index);
            var name = ReadRequiredString(item, "name", validationIssues, index);
            if (docTypeId is null || name is null)
            {
                continue;
            }

            results.Add(new DocumentTypeDefinition(
                docTypeId,
                name,
                ReadString(item, "family") ?? "unknown",
                ReadInt(item, "priority", 0),
                ReadMatch(item, "match"),
                ReadClassifier(item, "classifier"),
                ReadExtraction(item, "extraction"),
                ReadSchema(item, "schema"),
                ReadGeometry(item, "geometry"),
                ReadValidation(item, "validation"),
                ReadReview(item, "review"),
                ReadOutput(item, "output")));
        }

        return results;
    }

    private static IReadOnlyList<DocumentTypeDefinition> ReadLegacyDocTypes(JsonElement root)
    {
        if (!root.TryGetProperty("doc_types", out var docTypesNode) || docTypesNode.ValueKind != JsonValueKind.Array)
        {
            return DefaultDocumentTypes;
        }

        var results = new List<DocumentTypeDefinition>();
        foreach (var item in docTypesNode.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var docTypeId = ReadString(item, "doc_type_id");
            var name = ReadString(item, "name");
            if (string.IsNullOrWhiteSpace(docTypeId) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var family = InferLegacyFamily(docTypeId, name);
            results.Add(new DocumentTypeDefinition(
                docTypeId,
                name,
                family,
                InferLegacyPriority(docTypeId),
                new DocumentTypeMatchDefinition(
                    ReadStringArray(item, "match", "source_ext_in"),
                    ReadStringArray(item, "match", "filename_contains_any"),
                    Array.Empty<string>(),
                    ReadStringArray(item, "match", "text_contains_any"),
                    Array.Empty<string>(),
                    20),
                new DocumentTypeClassifierDefinition("weighted_match", new DocumentTypeClassifierWeights(20, 15, 25, 30)),
                new DocumentTypeExtractionDefinition(
                    InferLegacyExtractorId(family),
                    family == "structured_points" ? "structured_points" : "parcel_block_rows",
                    string.Equals(family, "computation_sheet", StringComparison.OrdinalIgnoreCase),
                    item.TryGetProperty("openai", out _),
                    item.TryGetProperty("openai", out _) ? "survey_table_vision_v1" : string.Empty,
                    family == "structured_points" ? new[] { "structured_txt_points" } : new[] { "openai_table_pdf", "ocr_table_pdf", "text_regex_pdf" },
                    family == "structured_points" ? new[] { "rows" } : new[] { "metadata", "parcel_groups", "rows" }),
                new DocumentTypeSchemaDefinition(
                    ReadStringArray(item, "expected_schema", "metadata_fields"),
                    ReadStringArray(item, "expected_schema", "parcel_fields"),
                    ReadStringArray(item, "expected_schema", "row_fields")),
                new DocumentTypeGeometryDefinition("parcel_rows_with_group_breaks", "row_vertices", "from_to_pairs", "group_closed_ring", true, true, true, "do_not_chain_across_groups"),
                new DocumentTypeValidationDefinition(
                    $"{docTypeId.ToLowerInvariant()}_validation",
                    ReadStringArray(item, "validation", "required_metadata_fields"),
                    ReadStringArray(item, "validation", "required_row_fields"),
                    ReadInt(item, "expected_min_segment_rows", 0),
                    ReadInt(item, "expected_parcel_count", 0),
                    new[] { "missing_required_fields", "invalid_coordinates" },
                    new[] { "low_match_confidence" }),
                new DocumentTypeReviewDefinition("point_review", "embedded_or_external", true, true, true, true),
                new DocumentTypeOutputDefinition(new[] { "normal_fgdb", "parcel_fabric", "enterprise_working_layers", "enterprise_parcel_fabric" }, "normal_fgdb")));
        }

        return results.Count == 0 ? DefaultDocumentTypes : results;
    }

    private static IReadOnlyList<DocumentTypeDefinition> MergeRequiredDefaults(IReadOnlyList<DocumentTypeDefinition> definitions, string defaultDocTypeId)
    {
        var byId = definitions.ToDictionary(definition => definition.DocTypeId, StringComparer.OrdinalIgnoreCase);
        foreach (var fallback in DefaultDocumentTypes)
        {
            if (!byId.ContainsKey(fallback.DocTypeId))
            {
                byId[fallback.DocTypeId] = fallback;
            }
        }

        if (!byId.ContainsKey(defaultDocTypeId))
        {
            byId["UNKNOWN_GENERIC_SOURCE_V1"] = DefaultDocumentTypes.First(definition => definition.DocTypeId == "UNKNOWN_GENERIC_SOURCE_V1");
        }

        return byId.Values.OrderByDescending(definition => definition.Priority).ToArray();
    }

    private static DocumentTypeMatchDefinition ReadMatch(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.Object)
        {
            return new DocumentTypeMatchDefinition(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), 1);
        }

        return new DocumentTypeMatchDefinition(
            ReadStringArray(node, "source_kinds"),
            ReadStringArray(node, "filename_contains_any"),
            ReadStringArray(node, "filename_regex_any"),
            ReadStringArray(node, "text_contains_any"),
            ReadStringArray(node, "text_regex_any"),
            ReadInt(node, "score_threshold", 1));
    }

    private static DocumentTypeClassifierDefinition ReadClassifier(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.Object)
        {
            return new DocumentTypeClassifierDefinition("weighted_match", new DocumentTypeClassifierWeights(20, 15, 25, 30));
        }

        var weightsNode = node.TryGetProperty("weights", out var weights) && weights.ValueKind == JsonValueKind.Object
            ? weights
            : default;
        return new DocumentTypeClassifierDefinition(
            ReadString(node, "strategy") ?? "weighted_match",
            new DocumentTypeClassifierWeights(
                ReadInt(weightsNode, "filename_contains_any", 20),
                ReadInt(weightsNode, "filename_regex_any", 15),
                ReadInt(weightsNode, "text_contains_any", 25),
                ReadInt(weightsNode, "text_regex_any", 30)));
    }

    private static DocumentTypeExtractionDefinition ReadExtraction(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.Object)
        {
            return new DocumentTypeExtractionDefinition("manual_only_source", "manual", false, false, string.Empty, Array.Empty<string>(), Array.Empty<string>());
        }

        return new DocumentTypeExtractionDefinition(
            ReadString(node, "extractor_id") ?? "manual_only_source",
            ReadString(node, "parser_mode") ?? "manual",
            ReadBool(node, "prefers_text_layer", false),
            ReadBool(node, "ai_assisted", false),
            ReadString(node, "ai_profile") ?? string.Empty,
            ReadStringArray(node, "fallback_extractors"),
            ReadStringArray(node, "expected_outputs"));
    }

    private static DocumentTypeSchemaDefinition ReadSchema(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.Object)
        {
            return new DocumentTypeSchemaDefinition(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        }

        return new DocumentTypeSchemaDefinition(
            ReadStringArray(node, "metadata_fields"),
            ReadStringArray(node, "parcel_fields"),
            ReadStringArray(node, "row_fields"));
    }

    private static DocumentTypeGeometryDefinition ReadGeometry(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.Object)
        {
            return new DocumentTypeGeometryDefinition("manual_constructed", "manual", "manual", "manual", false, false, false, "manual_only");
        }

        return new DocumentTypeGeometryDefinition(
            ReadString(node, "geometry_mode") ?? "manual_constructed",
            ReadString(node, "point_source") ?? "manual",
            ReadString(node, "line_builder") ?? "manual",
            ReadString(node, "polygon_builder") ?? "manual",
            ReadBool(node, "supports_multi_parcel", false),
            ReadBool(node, "supports_boundary_breaks", false),
            ReadBool(node, "requires_grouping", false),
            ReadString(node, "closing_rule") ?? "manual_only");
    }

    private static DocumentTypeValidationDefinition ReadValidation(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.Object)
        {
            return new DocumentTypeValidationDefinition("generic_minimum", Array.Empty<string>(), Array.Empty<string>(), 0, 0, Array.Empty<string>(), Array.Empty<string>());
        }

        return new DocumentTypeValidationDefinition(
            ReadString(node, "validation_profile") ?? "generic_minimum",
            ReadStringArray(node, "required_metadata_fields"),
            ReadStringArray(node, "required_row_fields"),
            ReadInt(node, "minimum_expected_rows", 0),
            ReadInt(node, "minimum_expected_parcels", 0),
            ReadStringArray(node, "blockers"),
            ReadStringArray(node, "warnings"));
    }

    private static DocumentTypeReviewDefinition ReadReview(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.Object)
        {
            return new DocumentTypeReviewDefinition("point_review", "embedded_or_external", true, true, true, true);
        }

        return new DocumentTypeReviewDefinition(
            ReadString(node, "review_mode") ?? "point_review",
            ReadString(node, "source_viewer_mode") ?? "embedded_or_external",
            ReadBool(node, "allow_manual_point_add", true),
            ReadBool(node, "allow_manual_group_split", true),
            ReadBool(node, "allow_manual_group_merge", true),
            ReadBool(node, "approval_requires_zero_blockers", true));
    }

    private static DocumentTypeOutputDefinition ReadOutput(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.Object)
        {
            return new DocumentTypeOutputDefinition(new[] { "normal_fgdb" }, "normal_fgdb");
        }

        return new DocumentTypeOutputDefinition(
            ReadStringArray(node, "output_profiles"),
            ReadString(node, "default_output_profile") ?? "normal_fgdb");
    }

    private static string InferLegacyFamily(string docTypeId, string name)
    {
        if (docTypeId.Contains("COMPUTATION", StringComparison.OrdinalIgnoreCase) || name.Contains("Computation", StringComparison.OrdinalIgnoreCase))
        {
            return "computation_sheet";
        }

        if (docTypeId.Contains("TRAVERSE", StringComparison.OrdinalIgnoreCase) || name.Contains("Traverse", StringComparison.OrdinalIgnoreCase))
        {
            return "traverse_report";
        }

        if (docTypeId.Contains("TXT", StringComparison.OrdinalIgnoreCase) || docTypeId.Contains("CSV", StringComparison.OrdinalIgnoreCase))
        {
            return "structured_points";
        }

        return "unknown";
    }

    private static string InferLegacyExtractorId(string family)
    {
        return family switch
        {
            "structured_points" => "structured_csv_points",
            "computation_sheet" => "pdf_text_structured_computation",
            "traverse_report" => "pdf_text_structured_computation",
            _ => "manual_only_source"
        };
    }

    private static int InferLegacyPriority(string docTypeId)
    {
        if (docTypeId.Contains("GEOLAND", StringComparison.OrdinalIgnoreCase))
        {
            return 200;
        }

        if (docTypeId.Contains("CASE1", StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        return 50;
    }

    private static string? ReadRequiredString(JsonElement item, string propertyName, List<string> validationIssues, int index)
    {
        var value = ReadString(item, propertyName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        validationIssues.Add($"Document type entry {index} is missing required '{propertyName}'.");
        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty(propertyName, out var value)
               && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool ReadBool(JsonElement element, string propertyName, bool defaultValue)
    {
        return element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty(propertyName, out var value)
               && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : defaultValue;
    }

    private static int ReadInt(JsonElement element, string propertyName, int defaultValue)
    {
        return element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty(propertyName, out var value)
               && value.ValueKind == JsonValueKind.Number
               && value.TryGetInt32(out var result)
            ? result
            : defaultValue;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement parent, string childPropertyName, string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(childPropertyName, out var child)
            || child.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<string>();
        }

        return ReadStringArray(child, propertyName);
    }
}
