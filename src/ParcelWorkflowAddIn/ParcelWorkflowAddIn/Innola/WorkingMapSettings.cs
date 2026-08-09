using System.Text.Json;

namespace ParcelWorkflowAddIn.Innola;

public sealed record WorkingMapSettings(
    bool Enabled,
    string MapName,
    bool CreateIfMissing,
    bool ReuseExisting,
    bool ActivateOnTransactionLoad,
    bool CleanupTransactionGroupsOnClose,
    bool PreloadAfterLogin,
    string DefaultBasemap,
    IReadOnlyList<string> AlternateBasemaps,
    WorkingMapExtent DefaultExtent,
    bool ZoomToTransactionParish,
    WorkingMapParishLookupSettings ParishLookup,
    IReadOnlyList<WorkingMapReferenceLayerSettings> ReferenceLayers,
    string? Warning)
{
    public static WorkingMapSettings Default { get; } = new(
        true,
        "Jamaica",
        true,
        true,
        true,
        true,
        true,
        "esri_world_imagery",
        new[] { "open_basemap", "world_topographic" },
        new WorkingMapExtent("Jamaica", 3448, 580172.099, 605960.245, 845529.005, 728209.243),
        true,
        WorkingMapParishLookupSettings.Default,
        new[]
        {
            new WorkingMapReferenceLayerSettings(
                "Esri World Imagery",
                "map_service_url",
                "https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer",
                "Basemaps",
                false,
                true,
                0,
                1.0,
                "imagery",
                null,
                null),
            new WorkingMapReferenceLayerSettings(
                "Open Basemap Streets",
                "vector_tile_style_url",
                "https://www.arcgis.com/sharing/rest/content/items/643f29ef5ab94511912dd337c9e1a13b/resources/styles/root.json",
                "Basemaps",
                false,
                false,
                1,
                1.0,
                "streets",
                null,
                null),
            new WorkingMapReferenceLayerSettings(
                "World Topographic",
                "vector_tile_style_url",
                "https://cdn.arcgis.com/sharing/rest/content/items/7dc6cea0b1764a1f9af2e679f642f0f5/resources/styles/root.json",
                "Basemaps",
                false,
                false,
                2,
                1.0,
                "topographic",
                null,
                null),
            new WorkingMapReferenceLayerSettings(
                "World Hillshade",
                "map_service_url",
                "https://services.arcgisonline.com/arcgis/rest/services/Elevation/World_Hillshade/MapServer",
                "Terrain Reference",
                false,
                false,
                5,
                0.65,
                null,
                null,
                null),
            new WorkingMapReferenceLayerSettings(
                "Legal_Cadastre",
                "map_service_url",
                "https://jm-gis.innola-solutions.com/server/rest/services/Legal_Cadastre/MapServer",
                "Cadastre Reference",
                true,
                true,
                10,
                1.0,
                null,
                null,
                null),
            new WorkingMapReferenceLayerSettings(
                "Fiscal_Cadastre",
                "map_service_url",
                "https://jm-gis.innola-solutions.com/server/rest/services/Fiscal_Cadastre/MapServer",
                "Cadastre Reference",
                false,
                false,
                20,
                1.0,
                null,
                null,
                null),
            new WorkingMapReferenceLayerSettings(
                "Survey_Cadastre",
                "map_service_url",
                "https://jm-gis.innola-solutions.com/server/rest/services/Survey_Cadastre/MapServer",
                "Cadastre Reference",
                false,
                false,
                30,
                1.0,
                null,
                null,
                null)
        },
        null);

    public static WorkingMapSettings FromJson(JsonElement root)
    {
        if (!root.TryGetProperty("working_map", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return Default;
        }

        var referenceLayers = ReadReferenceLayers(value, "reference_layers", Default.ReferenceLayers);
        return new WorkingMapSettings(
            ReadBool(value, "enabled") ?? Default.Enabled,
            ReadNonEmptyString(value, "map_name") ?? Default.MapName,
            ReadBool(value, "create_if_missing") ?? Default.CreateIfMissing,
            ReadBool(value, "reuse_existing") ?? Default.ReuseExisting,
            ReadBool(value, "activate_on_transaction_load") ?? Default.ActivateOnTransactionLoad,
            ReadBool(value, "cleanup_transaction_groups_on_close") ?? Default.CleanupTransactionGroupsOnClose,
            ReadBool(value, "preload_after_login") ?? Default.PreloadAfterLogin,
            ReadNonEmptyString(value, "default_basemap") ?? Default.DefaultBasemap,
            ReadStringArray(value, "alternate_basemaps", Default.AlternateBasemaps),
            WorkingMapExtent.FromJson(value, "default_extent", Default.DefaultExtent),
            ReadBool(value, "zoom_to_transaction_parish") ?? Default.ZoomToTransactionParish,
            WorkingMapParishLookupSettings.FromJson(value, "parish_lookup", Default.ParishLookup),
            referenceLayers.Values,
            referenceLayers.Warning);
    }

    private static LayerResolution ReadReferenceLayers(
        JsonElement element,
        string name,
        IReadOnlyList<WorkingMapReferenceLayerSettings> fallback)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return new LayerResolution(fallback, null);
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return new LayerResolution(fallback, "working_map.reference_layers is not a valid list; safe defaults are being used.");
        }

        var layers = new List<WorkingMapReferenceLayerSettings>();
        var ignored = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                ignored++;
                continue;
            }

            var layer = WorkingMapReferenceLayerSettings.FromJson(item);
            if (string.IsNullOrWhiteSpace(layer.Name))
            {
                ignored++;
                continue;
            }

            layers.Add(layer);
        }

        if (layers.Count == 0)
        {
            return new LayerResolution(fallback, "working_map.reference_layers is empty or invalid; safe defaults are being used.");
        }

        return new LayerResolution(
            layers,
            ignored > 0 ? "Some working_map.reference_layers entries were invalid and were ignored." : null);
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string name, IReadOnlyList<string> fallback)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return fallback;
        }

        var values = value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return values.Length == 0 ? fallback : values;
    }

    private static bool? ReadBool(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;
    }

    private static string? ReadNonEmptyString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? EmptyToNull(value.GetString())
            : null;
    }

    private static string? EmptyToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private sealed record LayerResolution(IReadOnlyList<WorkingMapReferenceLayerSettings> Values, string? Warning);
}

public sealed record WorkingMapExtent(
    string Name,
    int Wkid,
    double XMin,
    double YMin,
    double XMax,
    double YMax)
{
    public static WorkingMapExtent FromJson(JsonElement root, string propertyName, WorkingMapExtent fallback)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return fallback;
        }

        return new WorkingMapExtent(
            ReadString(value, "name") ?? fallback.Name,
            ReadInt(value, "wkid") ?? fallback.Wkid,
            ReadDouble(value, "xmin") ?? fallback.XMin,
            ReadDouble(value, "ymin") ?? fallback.YMin,
            ReadDouble(value, "xmax") ?? fallback.XMax,
            ReadDouble(value, "ymax") ?? fallback.YMax);
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number)
                ? number
                : null;
    }

    private static double? ReadDouble(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var number)
                ? number
                : null;
    }
}

public sealed record WorkingMapParishLookupSettings(
    bool Enabled,
    string LayerName,
    string NameField,
    bool Required,
    IReadOnlyDictionary<string, WorkingMapExtent> KnownExtents)
{
    public static WorkingMapParishLookupSettings Default { get; } = new(
        true,
        "Parishes",
        "parish",
        false,
        CreateDefaultParishExtents());

    public static WorkingMapParishLookupSettings FromJson(
        JsonElement root,
        string propertyName,
        WorkingMapParishLookupSettings fallback)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return fallback;
        }

        return new WorkingMapParishLookupSettings(
            ReadBool(value, "enabled") ?? fallback.Enabled,
            ReadString(value, "layer_name") ?? fallback.LayerName,
            ReadString(value, "name_field") ?? fallback.NameField,
            ReadBool(value, "required") ?? fallback.Required,
            ReadKnownExtents(value, "known_extents", fallback.KnownExtents));
    }

    private static IReadOnlyDictionary<string, WorkingMapExtent> ReadKnownExtents(
        JsonElement element,
        string name,
        IReadOnlyDictionary<string, WorkingMapExtent> fallback)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return fallback;
        }

        var extents = new Dictionary<string, WorkingMapExtent>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            extents[NormalizeParishKey(property.Name)] = WorkingMapExtent.FromJson(value, property.Name, new WorkingMapExtent(property.Name, 3448, 0, 0, 0, 0));
        }

        return extents.Count == 0 ? fallback : extents;
    }

    public static string NormalizeParishKey(string? parish)
    {
        if (string.IsNullOrWhiteSpace(parish))
        {
            return string.Empty;
        }

        var normalized = parish.Trim()
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace("_", " ", StringComparison.Ordinal)
            .Replace("-", " ", StringComparison.Ordinal)
            .ToLowerInvariant();
        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return normalized.StartsWith("saint ", StringComparison.Ordinal)
            ? "st " + normalized["saint ".Length..]
            : normalized;
    }

    private static IReadOnlyDictionary<string, WorkingMapExtent> CreateDefaultParishExtents()
    {
        var extents = new Dictionary<string, WorkingMapExtent>(StringComparer.OrdinalIgnoreCase);
        Add(extents, "Hanover", -78.42, 18.27, -77.84, 18.57);
        Add(extents, "Westmoreland", -78.42, 17.95, -77.77, 18.35);
        Add(extents, "St. James", -77.98, 18.24, -77.62, 18.55);
        Add(extents, "Trelawny", -77.75, 18.18, -77.30, 18.55);
        Add(extents, "St. Ann", -77.58, 18.14, -76.88, 18.55);
        Add(extents, "St. Mary", -77.13, 18.18, -76.62, 18.50);
        Add(extents, "Portland", -76.75, 18.00, -76.22, 18.32);
        Add(extents, "St. Thomas", -76.65, 17.84, -76.16, 18.14);
        Add(extents, "St. Andrew", -76.92, 17.88, -76.62, 18.15);
        Add(extents, "Kingston", -76.86, 17.88, -76.70, 18.04);
        Add(extents, "St. Catherine", -77.28, 17.80, -76.72, 18.25);
        Add(extents, "Clarendon", -77.50, 17.78, -76.90, 18.25);
        Add(extents, "Manchester", -77.75, 17.82, -77.25, 18.25);
        Add(extents, "St. Elizabeth", -78.10, 17.82, -77.52, 18.25);
        return extents;
    }

    private static void Add(
        IDictionary<string, WorkingMapExtent> extents,
        string parish,
        double xmin,
        double ymin,
        double xmax,
        double ymax)
    {
        var corners = new[]
        {
            ProjectWgs84ApproxToJad2001(xmin, ymin),
            ProjectWgs84ApproxToJad2001(xmin, ymax),
            ProjectWgs84ApproxToJad2001(xmax, ymin),
            ProjectWgs84ApproxToJad2001(xmax, ymax)
        };
        var extent = new WorkingMapExtent(
            parish,
            3448,
            corners.Min(corner => corner.X),
            corners.Min(corner => corner.Y),
            corners.Max(corner => corner.X),
            corners.Max(corner => corner.Y));
        extents[NormalizeParishKey(parish)] = extent;
        if (parish.StartsWith("St.", StringComparison.OrdinalIgnoreCase))
        {
            extents[NormalizeParishKey("Saint" + parish[3..])] = extent;
        }
    }

    private static (double X, double Y) ProjectWgs84ApproxToJad2001(double longitude, double latitude)
    {
        const double semiMajorAxis = 6378137.0;
        const double inverseFlattening = 298.257222101;
        const double latitudeOfOriginDegrees = 18.0;
        const double centralMeridianDegrees = -77.0;
        const double falseEasting = 750000.0;
        const double falseNorthing = 650000.0;

        var flattening = 1.0 / inverseFlattening;
        var eccentricity = Math.Sqrt((2 * flattening) - (flattening * flattening));
        var latitudeOfOrigin = DegreesToRadians(latitudeOfOriginDegrees);
        var centralMeridian = DegreesToRadians(centralMeridianDegrees);
        var standardParallelN = Math.Sin(latitudeOfOrigin);
        var originT = LambertT(latitudeOfOrigin, eccentricity);
        var originM = LambertM(latitudeOfOrigin, eccentricity);
        var lambertF = originM / (standardParallelN * Math.Pow(originT, standardParallelN));
        var originRho = semiMajorAxis * lambertF * Math.Pow(originT, standardParallelN);

        var phi = DegreesToRadians(latitude);
        var lambda = DegreesToRadians(longitude);
        var rho = semiMajorAxis * lambertF * Math.Pow(LambertT(phi, eccentricity), standardParallelN);
        var theta = standardParallelN * (lambda - centralMeridian);

        return (
            falseEasting + (rho * Math.Sin(theta)),
            falseNorthing + originRho - (rho * Math.Cos(theta)));
    }

    private static double LambertM(double phi, double eccentricity)
    {
        var sinPhi = Math.Sin(phi);
        return Math.Cos(phi) / Math.Sqrt(1 - (eccentricity * eccentricity * sinPhi * sinPhi));
    }

    private static double LambertT(double phi, double eccentricity)
    {
        var sinPhi = Math.Sin(phi);
        var eccentricityFactor = Math.Pow((1 - (eccentricity * sinPhi)) / (1 + (eccentricity * sinPhi)), eccentricity / 2);
        return Math.Tan((Math.PI / 4) - (phi / 2)) / eccentricityFactor;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;
    }

    private static bool? ReadBool(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;
    }
}

public sealed record WorkingMapReferenceLayerSettings(
    string Name,
    string SourceType,
    string Url,
    string Group,
    bool Required,
    bool Visible,
    int Order,
    double Opacity,
    string? BasemapRole,
    double? MinScale,
    double? MaxScale)
{
    public static WorkingMapReferenceLayerSettings Default { get; } = new(
        string.Empty,
        "map_service_url",
        string.Empty,
        "Reference Layers",
        false,
        false,
        0,
        1.0,
        null,
        null,
        null);

    public static WorkingMapReferenceLayerSettings FromJson(JsonElement value)
    {
        return new WorkingMapReferenceLayerSettings(
            ReadString(value, "name") ?? Default.Name,
            ReadString(value, "source_type") ?? Default.SourceType,
            ReadString(value, "url") ?? ReadString(value, "item_path") ?? Default.Url,
            ReadString(value, "group") ?? Default.Group,
            ReadBool(value, "required") ?? Default.Required,
            ReadBool(value, "visible") ?? Default.Visible,
            ReadInt(value, "order") ?? Default.Order,
            ClampOpacity(ReadDouble(value, "opacity") ?? Default.Opacity),
            ReadString(value, "basemap_role"),
            ReadDouble(value, "min_scale"),
            ReadDouble(value, "max_scale"));
    }

    private static double ClampOpacity(double opacity)
    {
        if (opacity < 0)
        {
            return 0;
        }

        return opacity > 1 ? 1 : opacity;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;
    }

    private static bool? ReadBool(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number)
                ? number
                : null;
    }

    private static double? ReadDouble(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var number)
                ? number
                : null;
    }
}
