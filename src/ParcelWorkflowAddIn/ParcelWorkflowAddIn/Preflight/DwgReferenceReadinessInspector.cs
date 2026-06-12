using System.IO;

namespace ParcelWorkflowAddIn.Preflight;

public interface IDwgReferenceReadinessInspector
{
    Task<DwgReferenceReadinessProbeResult> InspectAsync(string copiedPath, CancellationToken cancellationToken = default);
}

public sealed record DwgReferenceReadinessProbeResult(
    bool ProbeExecuted,
    bool Success,
    string? Message = null,
    string? Correction = null);

public sealed class NoOpDwgReferenceReadinessInspector : IDwgReferenceReadinessInspector
{
    public Task<DwgReferenceReadinessProbeResult> InspectAsync(string copiedPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DwgReferenceReadinessProbeResult(
            ProbeExecuted: false,
            Success: true,
            Message: $"No DWG CAD probe executed for '{Path.GetFileName(copiedPath)}'.",
            Correction: null));
    }
}
