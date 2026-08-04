using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn;

internal sealed class MapGeoreferenceOverlayService
{
    private const int Jad2001Wkid = 3448;
    private const int OverlayTransparencyPercent = 70;
    private const string OverlayLayerName = "M-Geo plan overlay";

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

        var outputPath = WriteOverlayFiles(transactionNumber, image, imagePoint1, imagePoint2, mapPoint1, mapPoint2);
        var mapView = MapView.Active;
        if (mapView?.Map is null)
        {
            return MapGeoreferenceOverlayResult.Failed("No active ArcGIS Pro map is available for the overlay.");
        }

        var groupName = BuildGroupName(transactionNumber);
        var loaded = false;
        await QueuedTask.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            mapView.Map.SetSpatialReference(SpatialReferenceBuilder.CreateSpatialReference(Jad2001Wkid));
            RemoveExistingOverlayGroup(mapView.Map, groupName);
            var group = LayerFactory.Instance.CreateGroupLayer(mapView.Map, 0, groupName);
            var created = LayerFactory.Instance.CreateLayer(new Uri(outputPath), group);
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
            return MapGeoreferenceOverlayResult.Failed("ArcGIS Pro did not return a layer for the plan overlay.");
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
            return MapGeoreferenceOverlayResult.Succeeded(
                $"Created 70% transparent M-Geo overlay, but ArcGIS Pro could not zoom to it automatically. Layer group: {groupName}.",
                outputPath,
                groupName);
        }

        return MapGeoreferenceOverlayResult.Succeeded(
            $"Created 70% transparent M-Geo overlay in '{groupName}'.",
            outputPath,
            groupName);
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

    private static string WriteOverlayFiles(
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

        File.WriteAllText(Path.ChangeExtension(imagePath, ".prj"), Jad2001Prj);
        return imagePath;
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
    string? GroupLayerName)
{
    public static MapGeoreferenceOverlayResult Succeeded(string message, string overlayPath, string groupLayerName)
    {
        return new MapGeoreferenceOverlayResult(true, message, overlayPath, groupLayerName);
    }

    public static MapGeoreferenceOverlayResult Failed(string message)
    {
        return new MapGeoreferenceOverlayResult(false, message, null, null);
    }
}

internal readonly record struct MapGeoreferenceImagePoint(double X, double Y);

internal readonly record struct MapGeoreferenceCoordinatePoint(double Easting, double Northing);
