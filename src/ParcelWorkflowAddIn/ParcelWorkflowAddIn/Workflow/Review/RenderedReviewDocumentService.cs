using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace ParcelWorkflowAddIn.Workflow.Review;

internal sealed class RenderedReviewDocumentService
{
    private readonly ConcurrentDictionary<string, RenderedReviewDocumentPage> pageCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<RenderedReviewDocumentPage> RenderAsync(string sourcePath, int requestedPageIndex, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("A source path is required.", nameof(sourcePath));
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        return extension switch
        {
            ".tif" or ".tiff" => RenderRaster(sourcePath, requestedPageIndex, allowMultipleFrames: true),
            ".png" or ".jpg" or ".jpeg" => RenderRaster(sourcePath, requestedPageIndex, allowMultipleFrames: false),
            _ => throw new NotSupportedException($"Embedded rendering is not supported for '{extension}'.")
        };
    }

    public void Invalidate(string? sourcePath = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            pageCache.Clear();
            return;
        }

        foreach (var key in pageCache.Keys.Where(key => key.StartsWith($"{sourcePath}|", StringComparison.OrdinalIgnoreCase)))
        {
            pageCache.TryRemove(key, out _);
        }
    }

    private RenderedReviewDocumentPage RenderRaster(string sourcePath, int requestedPageIndex, bool allowMultipleFrames)
    {
        using var stream = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var pageCount = allowMultipleFrames ? decoder.Frames.Count : 1;
        if (pageCount == 0)
        {
            throw new FileFormatException("The image file does not contain a readable frame.");
        }

        var normalizedPageIndex = Math.Clamp(requestedPageIndex, 0, pageCount - 1);
        var cacheKey = BuildCacheKey(sourcePath, normalizedPageIndex);
        if (pageCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var frame = decoder.Frames[normalizedPageIndex];
        frame.Freeze();

        var rendered = new RenderedReviewDocumentPage(
            sourcePath,
            normalizedPageIndex,
            pageCount,
            frame,
            allowMultipleFrames ? "Raster" : "Image");

        pageCache[cacheKey] = rendered;
        return rendered;
    }

    private static string BuildCacheKey(string sourcePath, int pageIndex)
    {
        var lastWrite = File.GetLastWriteTimeUtc(sourcePath).Ticks;
        return $"{sourcePath}|{lastWrite}|{pageIndex}";
    }
}
