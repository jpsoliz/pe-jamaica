using System.IO;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Workflow.SpatialReview;

public sealed class SpatialOverlapReviewSnapshotService
{
    private const int SnapshotWidth = 1600;
    private const int SnapshotHeight = 1000;
    private const int SnapshotResolution = 200;

    public async Task<SpatialOverlapReviewDocument> CaptureAndAttachAsync(
        CaseFolderLayout layout,
        SpatialOverlapReviewDocument document,
        CancellationToken cancellationToken = default)
    {
        if (document.Records.Count == 0)
        {
            return document with { Snapshots = Array.Empty<SpatialOverlapReviewSnapshotRef>() };
        }

        try
        {
            var relativePath = await ExportSnapshotAsync(layout, document.Scope, cancellationToken).ConfigureAwait(true);
            return document with
            {
                Snapshots = BuildSnapshotRefs(document, relativePath, "captured")
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return document with
            {
                Snapshots = BuildSnapshotRefs(document, null, "capture_failed"),
                Warnings = document.Warnings.Concat(new[]
                {
                    $"Overlap snapshot capture failed: {exception.Message}"
                }).ToArray()
            };
        }
    }

    private static async Task<string> ExportSnapshotAsync(
        CaseFolderLayout layout,
        string scope,
        CancellationToken cancellationToken)
    {
        var mapView = MapView.Active;
        if (mapView is null)
        {
            throw new InvalidOperationException("No active map view is available for overlap snapshot capture.");
        }

        var snapshotDirectory = Path.Combine(layout.WorkingDirectory, "overlap-snapshots");
        Directory.CreateDirectory(snapshotDirectory);
        var fileName = string.Equals(scope, SpatialOverlapReviewScopes.Compare, StringComparison.OrdinalIgnoreCase)
            ? "compare-overlap-review.png"
            : "compute-overlap-review.png";
        var outputPath = Path.Combine(snapshotDirectory, fileName);

        cancellationToken.ThrowIfCancellationRequested();
        await QueuedTask.Run(() =>
        {
            var format = new PNGFormat
            {
                Resolution = SnapshotResolution,
                Width = SnapshotWidth,
                Height = SnapshotHeight,
                OutputFileName = outputPath,
                HasTransparentBackground = false
            };

            if (!format.ValidateOutputFilePath())
            {
                throw new InvalidOperationException($"Could not validate overlap snapshot output path '{outputPath}'.");
            }

            mapView.Export(format);
        }).ConfigureAwait(true);

        return Path.GetRelativePath(layout.RootDirectory, outputPath).Replace('\\', '/');
    }

    private static IReadOnlyList<SpatialOverlapReviewSnapshotRef> BuildSnapshotRefs(
        SpatialOverlapReviewDocument document,
        string? relativePath,
        string status)
    {
        var keys = document.Records
            .Select(record => (record.OverlapGroupId, record.OverlapId))
            .Where(key => !string.IsNullOrWhiteSpace(key.OverlapGroupId) || !string.IsNullOrWhiteSpace(key.OverlapId))
            .Distinct()
            .ToArray();

        if (keys.Length == 0)
        {
            return Array.Empty<SpatialOverlapReviewSnapshotRef>();
        }

        return keys.Select(key => new SpatialOverlapReviewSnapshotRef(
            key.OverlapGroupId,
            key.OverlapId,
            BuildCaption(document.Scope, key.OverlapGroupId, key.OverlapId),
            relativePath,
            status)).ToArray();
    }

    private static string BuildCaption(string scope, string? overlapGroupId, string? overlapId)
    {
        var target = !string.IsNullOrWhiteSpace(overlapGroupId)
            ? overlapGroupId
            : !string.IsNullOrWhiteSpace(overlapId)
                ? overlapId
                : "overlap";
        return $"{scope} overlap snapshot - {target}";
    }
}
