using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn;

internal sealed class MapGeoreferenceOverlayService
{
    private const int Jad2001Wkid = 3448;
    private const int OverlayTransparencyPercent = 70;
    private const string OverlayLayerName = "M-Geo plan overlay";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<MapGeoreferenceOverlayResult> CreateOverlayAsync(
        string transactionNumber,
        BitmapSource image,
        MapGeoreferenceImagePoint imagePoint1,
        MapGeoreferenceImagePoint imagePoint2,
        MapGeoreferenceCoordinatePoint mapPoint1,
        MapGeoreferenceCoordinatePoint mapPoint2,
        CancellationToken cancellationToken = default)
    {
        if (image.PixelWidth <= 0 || image.PixelHeight <= 0)
        {
            return MapGeoreferenceOverlayResult.Failed("The captured plan image is empty.");
        }

        var pixelDx = imagePoint2.X - imagePoint1.X;
        var pixelDy = imagePoint2.Y - imagePoint1.Y;
        var pixelDistance = Math.Sqrt((pixelDx * pixelDx) + (pixelDy * pixelDy));
        if (pixelDistance <= 0.0001)
        {
            return MapGeoreferenceOverlayResult.Failed("Pick two different plan points before creating the overlay.");
        }

        var mapDx = mapPoint2.Easting - mapPoint1.Easting;
        var mapDy = mapPoint2.Northing - mapPoint1.Northing;
        var mapDistance = Math.Sqrt((mapDx * mapDx) + (mapDy * mapDy));
        if (mapDistance <= 0.0001)
        {
            return MapGeoreferenceOverlayResult.Failed("Map/control points must be two different JAD2001 locations.");
        }

        var artifact = WriteOverlayFiles(transactionNumber, image, imagePoint1, imagePoint2, mapPoint1, mapPoint2);
        var loadResult = await AddOverlayLayerToActiveMapAsync(
            transactionNumber,
            artifact.ImagePath,
            "ArcGIS Pro did not return a layer for the plan overlay.",
            cancellationToken).ConfigureAwait(false);

        if (!loadResult.Success)
        {
            return MapGeoreferenceOverlayResult.Failed(loadResult.Message);
        }

        var persistenceResult = await TryPersistOverlayRasterAsync(transactionNumber, artifact, cancellationToken).ConfigureAwait(false);
        var persistedMessage = persistenceResult.Success
            ? $" Saved georeferenced image to {persistenceResult.RasterDatasetPath}."
            : $" The map overlay is available, but it was not saved into the output geodatabase: {persistenceResult.Message}";

        return MapGeoreferenceOverlayResult.Succeeded(
            $"{loadResult.Message}{persistedMessage}",
            artifact.ImagePath,
            loadResult.GroupLayerName ?? BuildGroupName(transactionNumber),
            persistenceResult.RasterDatasetPath);
    }

    public async Task<MapGeoreferenceOverlayResult> TryRestorePersistedOverlayAsync(
        string transactionNumber,
        CancellationToken cancellationToken = default)
    {
        string? outputGeodatabaseRestoreFailure = null;
        try
        {
            var outputGeodatabaseResult = await TryRestoreLocalOverlayFromOutputGeodatabaseAsync(
                transactionNumber,
                cancellationToken).ConfigureAwait(false);
            if (outputGeodatabaseResult.Success)
            {
                return outputGeodatabaseResult;
            }

            outputGeodatabaseRestoreFailure = outputGeodatabaseResult.Message;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or ArcGIS.Core.CalledOnWrongThreadException)
        {
            outputGeodatabaseRestoreFailure = exception.Message;
        }

        var artifact = LoadOverlayArtifact(transactionNumber);
        if (artifact is null)
        {
            return MapGeoreferenceOverlayResult.Failed("No saved M-Geo overlay was found for this transaction.");
        }

        var imageAvailable = !string.IsNullOrWhiteSpace(artifact.ImagePath) && File.Exists(artifact.ImagePath);
        var preferredPath = imageAvailable
            ? artifact.ImagePath
            : (!string.IsNullOrWhiteSpace(artifact.RasterDatasetPath)
                && Directory.Exists(Path.GetDirectoryName(artifact.RasterDatasetPath) ?? string.Empty)
                ? artifact.RasterDatasetPath
                : artifact.ImagePath);

        if (string.IsNullOrWhiteSpace(preferredPath)
            || (!preferredPath.Contains(".gdb", StringComparison.OrdinalIgnoreCase) && !File.Exists(preferredPath)))
        {
            return MapGeoreferenceOverlayResult.Failed("A saved M-Geo overlay record exists, but the saved raster/image path is no longer available.");
        }

        var loadResult = await AddOverlayLayerToActiveMapAsync(
            transactionNumber,
            preferredPath,
            "The saved M-Geo overlay could not be loaded into the active map.",
            cancellationToken).ConfigureAwait(false);

        return loadResult.Success
            ? MapGeoreferenceOverlayResult.Succeeded(
                string.IsNullOrWhiteSpace(outputGeodatabaseRestoreFailure)
                    ? $"Restored saved 70% transparent M-Geo overlay from {preferredPath}."
                    : $"Restored saved 70% transparent M-Geo overlay from {preferredPath}. Output geodatabase raster restore was skipped: {outputGeodatabaseRestoreFailure}",
                artifact.ImagePath,
                loadResult.GroupLayerName ?? BuildGroupName(transactionNumber),
                artifact.RasterDatasetPath)
            : MapGeoreferenceOverlayResult.Failed(loadResult.Message);
    }

    public async Task<MapGeoreferenceOverlayResult> TryRestoreLocalOverlayFromOutputGeodatabaseAsync(
        string transactionNumber,
        CancellationToken cancellationToken = default)
    {
        var caseRoot = ShellState.Session.LoadedCaseFolderPath;
        if (string.IsNullOrWhiteSpace(caseRoot))
        {
            return MapGeoreferenceOverlayResult.Failed("No loaded case folder is available for M-Geo overlay restore.");
        }

        var plan = MapGeoreferenceOverlayArtifactPlan.Create(caseRoot, transactionNumber);
        if (!Directory.Exists(plan.OutputGeodatabasePath))
        {
            return MapGeoreferenceOverlayResult.Failed($"No saved M-Geo overlay geodatabase was found at {plan.OutputGeodatabasePath}.");
        }

        var loadResult = await AddOverlayLayerToActiveMapAsync(
            transactionNumber,
            plan.RasterDatasetPath,
            $"The saved M-Geo overlay '{plan.RasterDatasetName}' was not found or could not be loaded from {plan.OutputGeodatabasePath}.",
            cancellationToken).ConfigureAwait(false);

        return loadResult.Success
            ? MapGeoreferenceOverlayResult.Succeeded(
                $"Restored saved 70% transparent M-Geo overlay from {plan.RasterDatasetPath}.",
                plan.RasterDatasetPath,
                loadResult.GroupLayerName ?? BuildGroupName(transactionNumber),
                plan.RasterDatasetPath)
            : MapGeoreferenceOverlayResult.Failed(loadResult.Message);
    }

    public async Task RemoveOverlayAsync(string? transactionNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transactionNumber))
        {
            return;
        }

        var mapView = MapView.Active;
        if (mapView?.Map is null)
        {
            return;
        }

        var groupName = BuildGroupName(transactionNumber);
        await QueuedTask.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            RemoveExistingOverlayGroup(mapView.Map, groupName);
        }).ConfigureAwait(false);
    }

    private static MapGeoreferenceOverlayArtifactDocument WriteOverlayFiles(
        string transactionNumber,
        BitmapSource image,
        MapGeoreferenceImagePoint imagePoint1,
        MapGeoreferenceImagePoint imagePoint2,
        MapGeoreferenceCoordinatePoint mapPoint1,
        MapGeoreferenceCoordinatePoint mapPoint2)
    {
        var root = ShellState.Session.LoadedCaseFolderPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(Path.GetTempPath(), "SidwellCo", "ParcelWorkflowCases", transactionNumber);
        }

        var overlayDirectory = Path.Combine(root, "working", "mgeo_overlay");
        Directory.CreateDirectory(overlayDirectory);

        var safeTransaction = string.Concat(transactionNumber.Where(char.IsLetterOrDigit));
        if (string.IsNullOrWhiteSpace(safeTransaction))
        {
            safeTransaction = "transaction";
        }

        var imagePath = Path.Combine(overlayDirectory, $"mgeo_overlay_{safeTransaction}.png");
        using (var stream = File.Create(imagePath))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);
        }

        var (a, b, d, e, c, f) = BuildWorldFile(imagePoint1, imagePoint2, mapPoint1, mapPoint2);
        var worldPath = Path.ChangeExtension(imagePath, ".pgw");
        File.WriteAllLines(
            worldPath,
            new[]
            {
                FormatWorldFileValue(a),
                FormatWorldFileValue(d),
                FormatWorldFileValue(b),
                FormatWorldFileValue(e),
                FormatWorldFileValue(c),
                FormatWorldFileValue(f)
            });

        var projectionPath = Path.ChangeExtension(imagePath, ".prj");
        File.WriteAllText(projectionPath, Jad2001Prj);

        var plan = MapGeoreferenceOverlayArtifactPlan.Create(root, transactionNumber);
        var artifact = new MapGeoreferenceOverlayArtifactDocument(
            transactionNumber,
            imagePath,
            worldPath,
            projectionPath,
            plan.OutputGeodatabasePath,
            plan.RasterDatasetName,
            plan.RasterDatasetPath,
            DateTimeOffset.UtcNow,
            image.PixelWidth,
            image.PixelHeight,
            imagePoint1,
            imagePoint2,
            mapPoint1,
            mapPoint2);
        SaveOverlayArtifact(artifact, root);
        return artifact;
    }

    private static async Task<MapGeoreferenceOverlayLayerLoadResult> AddOverlayLayerToActiveMapAsync(
        string transactionNumber,
        string overlayPath,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        await EnsureFileRasterProjectionAsync(overlayPath, cancellationToken).ConfigureAwait(false);

        var mapView = MapView.Active;
        if (mapView?.Map is null)
        {
            return MapGeoreferenceOverlayLayerLoadResult.Failed("No active ArcGIS Pro map is available for the overlay.");
        }

        var groupName = BuildGroupName(transactionNumber);
        var loaded = false;
        await QueuedTask.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            mapView.Map.SetSpatialReference(SpatialReferenceBuilder.CreateSpatialReference(Jad2001Wkid));
            RemoveExistingOverlayGroup(mapView.Map, groupName);
            var group = LayerFactory.Instance.CreateGroupLayer(mapView.Map, 0, groupName);
            var created = LayerFactory.Instance.CreateLayer(new Uri(overlayPath), group);
            if (created is null)
            {
                return;
            }

            created.SetName(OverlayLayerName);
            created.SetVisibility(true);
            created.SetTransparency(OverlayTransparencyPercent);
            group.SetVisibility(true);
            loaded = true;
        }).ConfigureAwait(false);

        if (!loaded)
        {
            return MapGeoreferenceOverlayLayerLoadResult.Failed(failureMessage);
        }

        try
        {
            var layers = await QueuedTask.Run(() =>
                FlattenLayers(mapView.Map.Layers)
                    .Where(layer => string.Equals(layer.Name, OverlayLayerName, StringComparison.OrdinalIgnoreCase))
                    .ToArray()).ConfigureAwait(false);
            if (layers.Length > 0)
            {
                await mapView.ZoomToAsync(layers).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            return MapGeoreferenceOverlayLayerLoadResult.Succeeded(
                $"Created 70% transparent M-Geo overlay, but ArcGIS Pro could not zoom to it automatically. Layer group: {groupName}.",
                groupName);
        }

        return MapGeoreferenceOverlayLayerLoadResult.Succeeded(
            $"Created 70% transparent M-Geo overlay in '{groupName}'.",
            groupName);
    }

    private static async Task EnsureFileRasterProjectionAsync(string overlayPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(overlayPath)
            || overlayPath.Contains(".gdb", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(overlayPath))
        {
            return;
        }

        var extension = Path.GetExtension(overlayPath);
        if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var projectionPath = Path.ChangeExtension(overlayPath, ".prj");
        if (!File.Exists(projectionPath))
        {
            File.WriteAllText(projectionPath, Jad2001Prj);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = await Geoprocessing.ExecuteToolAsync(
            "management.DefineProjection",
            Geoprocessing.MakeValueArray(
                overlayPath,
                SpatialReferenceBuilder.CreateSpatialReference(Jad2001Wkid)),
            flags: GPExecuteToolFlags.None).ConfigureAwait(false);
        if (result.IsFailed)
        {
            throw new InvalidOperationException(BuildGeoprocessingMessage(result));
        }
    }

    private static async Task<MapGeoreferenceOverlayPersistenceResult> TryPersistOverlayRasterAsync(
        string transactionNumber,
        MapGeoreferenceOverlayArtifactDocument artifact,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(artifact.OutputGeodatabasePath)
            || string.IsNullOrWhiteSpace(artifact.RasterDatasetPath))
        {
            return MapGeoreferenceOverlayPersistenceResult.Failed("No transaction output geodatabase path could be resolved.");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(artifact.OutputGeodatabasePath) ?? artifact.OutputGeodatabasePath);
            if (!Directory.Exists(artifact.OutputGeodatabasePath))
            {
                var createResult = await Geoprocessing.ExecuteToolAsync(
                    "management.CreateFileGDB",
                    Geoprocessing.MakeValueArray(
                        Path.GetDirectoryName(artifact.OutputGeodatabasePath),
                        Path.GetFileName(artifact.OutputGeodatabasePath)),
                    flags: GPExecuteToolFlags.None).ConfigureAwait(false);
                if (createResult.IsFailed)
                {
                    return MapGeoreferenceOverlayPersistenceResult.Failed(BuildGeoprocessingMessage(createResult));
                }
            }

            await Geoprocessing.ExecuteToolAsync(
                "management.Delete",
                Geoprocessing.MakeValueArray(artifact.RasterDatasetPath),
                flags: GPExecuteToolFlags.None).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            var copyResult = await Geoprocessing.ExecuteToolAsync(
                "management.CopyRaster",
                Geoprocessing.MakeValueArray(artifact.ImagePath, artifact.RasterDatasetPath),
                flags: GPExecuteToolFlags.None).ConfigureAwait(false);
            if (copyResult.IsFailed)
            {
                return MapGeoreferenceOverlayPersistenceResult.Failed(BuildGeoprocessingMessage(copyResult));
            }

            SaveOverlayArtifact(artifact, ShellState.Session.LoadedCaseFolderPath);
            return MapGeoreferenceOverlayPersistenceResult.Succeeded(artifact.RasterDatasetPath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or NotSupportedException
            or ArgumentException
            or OperationCanceledException)
        {
            return MapGeoreferenceOverlayPersistenceResult.Failed(exception.Message);
        }
    }

    private static string BuildGeoprocessingMessage(IGPResult result)
    {
        var message = string.Join(
            " ",
            result.Messages
                .Select(item => item.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text)));
        return string.IsNullOrWhiteSpace(message)
            ? "ArcGIS geoprocessing did not return a detailed error."
            : message;
    }

    private static MapGeoreferenceOverlayArtifactDocument? LoadOverlayArtifact(string transactionNumber)
    {
        var caseRoot = ShellState.Session.LoadedCaseFolderPath;
        if (string.IsNullOrWhiteSpace(caseRoot))
        {
            return null;
        }

        var artifactPath = MapGeoreferenceOverlayArtifactPlan.BuildMetadataPath(caseRoot);
        if (!File.Exists(artifactPath))
        {
            return null;
        }

        try
        {
            var artifact = JsonSerializer.Deserialize<MapGeoreferenceOverlayArtifactDocument>(
                File.ReadAllText(artifactPath),
                JsonOptions);
            return string.Equals(artifact?.TransactionNumber, transactionNumber, StringComparison.OrdinalIgnoreCase)
                ? artifact
                : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private static void SaveOverlayArtifact(MapGeoreferenceOverlayArtifactDocument artifact, string? caseRoot)
    {
        if (string.IsNullOrWhiteSpace(caseRoot))
        {
            return;
        }

        var artifactPath = MapGeoreferenceOverlayArtifactPlan.BuildMetadataPath(caseRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(artifactPath) ?? caseRoot);
        File.WriteAllText(artifactPath, JsonSerializer.Serialize(artifact, JsonOptions));
    }

    private static (double A, double B, double D, double E, double C, double F) BuildWorldFile(
        MapGeoreferenceImagePoint imagePoint1,
        MapGeoreferenceImagePoint imagePoint2,
        MapGeoreferenceCoordinatePoint mapPoint1,
        MapGeoreferenceCoordinatePoint mapPoint2)
    {
        var sourceDx = imagePoint2.X - imagePoint1.X;
        var sourceDyUp = -(imagePoint2.Y - imagePoint1.Y);
        var targetDx = mapPoint2.Easting - mapPoint1.Easting;
        var targetDy = mapPoint2.Northing - mapPoint1.Northing;
        var sourceLength = Math.Sqrt((sourceDx * sourceDx) + (sourceDyUp * sourceDyUp));
        var targetLength = Math.Sqrt((targetDx * targetDx) + (targetDy * targetDy));
        var scale = targetLength / sourceLength;
        var sourceAngle = Math.Atan2(sourceDyUp, sourceDx);
        var targetAngle = Math.Atan2(targetDy, targetDx);
        var rotation = targetAngle - sourceAngle;
        var cos = Math.Cos(rotation);
        var sin = Math.Sin(rotation);

        var a = scale * cos;
        var b = scale * sin;
        var d = scale * sin;
        var e = -scale * cos;
        var c = mapPoint1.Easting - (a * imagePoint1.X) - (b * imagePoint1.Y);
        var f = mapPoint1.Northing - (d * imagePoint1.X) - (e * imagePoint1.Y);
        return (a, b, d, e, c, f);
    }

    private static string FormatWorldFileValue(double value)
    {
        return value.ToString("0.###############", CultureInfo.InvariantCulture);
    }

    private static void RemoveExistingOverlayGroup(Map map, string groupName)
    {
        foreach (var layer in map.Layers.ToArray())
        {
            if (string.Equals(layer.Name, groupName, StringComparison.OrdinalIgnoreCase))
            {
                map.RemoveLayer(layer);
            }
        }
    }

    private static IEnumerable<Layer> FlattenLayers(IEnumerable<Layer> layers)
    {
        foreach (var layer in layers)
        {
            yield return layer;
            if (layer is CompositeLayer compositeLayer)
            {
                foreach (var child in FlattenLayers(compositeLayer.Layers))
                {
                    yield return child;
                }
            }
        }
    }

    private static string BuildGroupName(string transactionNumber)
    {
        var trimmed = string.IsNullOrWhiteSpace(transactionNumber) ? "Unknown" : transactionNumber.Trim();
        return $"TR {trimmed} - M-Geo Overlay";
    }

    private const string Jad2001Prj =
        "PROJCS[\"JAD_2001_Jamaica_Grid\",GEOGCS[\"GCS_JAD_2001\",DATUM[\"D_JAD_2001\",SPHEROID[\"Clarke_1880_RGS\",6378249.145,293.465]],PRIMEM[\"Greenwich\",0.0],UNIT[\"Degree\",0.0174532925199433]],PROJECTION[\"Lambert_Conformal_Conic\"],PARAMETER[\"False_Easting\",750000.0],PARAMETER[\"False_Northing\",650000.0],PARAMETER[\"Central_Meridian\",-77.0],PARAMETER[\"Standard_Parallel_1\",18.0],PARAMETER[\"Latitude_Of_Origin\",18.0],UNIT[\"Meter\",1.0],AUTHORITY[\"EPSG\",3448]]";
}

internal sealed record MapGeoreferenceOverlayResult(
    bool Success,
    string Message,
    string? OverlayPath,
    string? GroupLayerName,
    string? RasterDatasetPath = null)
{
    public static MapGeoreferenceOverlayResult Succeeded(string message, string overlayPath, string groupLayerName, string? rasterDatasetPath = null)
    {
        return new MapGeoreferenceOverlayResult(true, message, overlayPath, groupLayerName, rasterDatasetPath);
    }

    public static MapGeoreferenceOverlayResult Failed(string message)
    {
        return new MapGeoreferenceOverlayResult(false, message, null, null, null);
    }
}

internal readonly record struct MapGeoreferenceImagePoint(double X, double Y);

internal readonly record struct MapGeoreferenceCoordinatePoint(double Easting, double Northing);

internal sealed record MapGeoreferenceOverlayLayerLoadResult(
    bool Success,
    string Message,
    string? GroupLayerName)
{
    public static MapGeoreferenceOverlayLayerLoadResult Succeeded(string message, string groupLayerName)
    {
        return new MapGeoreferenceOverlayLayerLoadResult(true, message, groupLayerName);
    }

    public static MapGeoreferenceOverlayLayerLoadResult Failed(string message)
    {
        return new MapGeoreferenceOverlayLayerLoadResult(false, message, null);
    }
}

internal sealed record MapGeoreferenceOverlayPersistenceResult(
    bool Success,
    string Message,
    string? RasterDatasetPath)
{
    public static MapGeoreferenceOverlayPersistenceResult Succeeded(string rasterDatasetPath)
    {
        return new MapGeoreferenceOverlayPersistenceResult(true, string.Empty, rasterDatasetPath);
    }

    public static MapGeoreferenceOverlayPersistenceResult Failed(string message)
    {
        return new MapGeoreferenceOverlayPersistenceResult(false, message, null);
    }
}

internal sealed record MapGeoreferenceOverlayArtifactDocument(
    string TransactionNumber,
    string ImagePath,
    string WorldFilePath,
    string ProjectionFilePath,
    string OutputGeodatabasePath,
    string RasterDatasetName,
    string RasterDatasetPath,
    DateTimeOffset CreatedAtUtc,
    int ImagePixelWidth,
    int ImagePixelHeight,
    MapGeoreferenceImagePoint ImagePoint1,
    MapGeoreferenceImagePoint ImagePoint2,
    MapGeoreferenceCoordinatePoint MapPoint1,
    MapGeoreferenceCoordinatePoint MapPoint2);

internal sealed record MapGeoreferenceOverlayArtifactPlan(
    string CaseRoot,
    string OutputGeodatabasePath,
    string RasterDatasetName,
    string RasterDatasetPath,
    string MetadataPath)
{
    public static MapGeoreferenceOverlayArtifactPlan Create(string caseRoot, string transactionNumber)
    {
        var safeTransaction = string.Concat(transactionNumber.Where(char.IsLetterOrDigit));
        if (string.IsNullOrWhiteSpace(safeTransaction))
        {
            safeTransaction = "transaction";
        }

        var outputDirectory = Path.Combine(caseRoot, "output");
        var outputGeodatabasePath = Path.Combine(outputDirectory, $"{safeTransaction}_parcel_output.gdb");
        var rasterDatasetName = $"mgeo_overlay_{safeTransaction}";
        var rasterDatasetPath = Path.Combine(outputGeodatabasePath, rasterDatasetName);
        return new MapGeoreferenceOverlayArtifactPlan(
            caseRoot,
            outputGeodatabasePath,
            rasterDatasetName,
            rasterDatasetPath,
            BuildMetadataPath(caseRoot));
    }

    public static string BuildMetadataPath(string caseRoot)
    {
        return Path.Combine(caseRoot, "working", "mgeo_overlay", "mgeo_overlay_artifact.json");
    }
}
