using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.Innola;

public static class PlaOutputDocumentSourceTypeResolver
{
    public const string GeneratedOutputRole = "pla_generated_output";

    public static IReadOnlyList<string> OrderedOutputSourceTypes { get; } = new[]
    {
        "st_plan_annex_output",
        "st_plan_annex_output2",
        "st_plan_annex_output3"
    };

    public static PlaOutputDocumentSourceTypeResolution Resolve(
        InnolaTransactionSettings settings,
        int outputDocumentCount)
    {
        if (outputDocumentCount < 0)
        {
            return PlaOutputDocumentSourceTypeResolution.Failed("PLA output document count cannot be negative.");
        }

        if (outputDocumentCount > OrderedOutputSourceTypes.Count)
        {
            return PlaOutputDocumentSourceTypeResolution.Failed(
                $"PLA Finalize supports up to {OrderedOutputSourceTypes.Count} generated output documents; found {outputDocumentCount}.");
        }

        var requested = OrderedOutputSourceTypes.Take(outputDocumentCount).ToArray();
        foreach (var sourceType in requested)
        {
            var definition = settings.ComputeAttachmentSourceTypes.FirstOrDefault(item =>
                item.SourceType.Equals(sourceType, StringComparison.OrdinalIgnoreCase));
            if (definition is null)
            {
                return PlaOutputDocumentSourceTypeResolution.Failed($"PLA output source type '{sourceType}' is not configured.");
            }

            if (!string.Equals(definition.WorkflowRole, GeneratedOutputRole, StringComparison.OrdinalIgnoreCase)
                || definition.Required
                || !definition.InternalOnly
                || !definition.SupportsExtension(".pdf"))
            {
                return PlaOutputDocumentSourceTypeResolution.Failed(
                    $"PLA output source type '{sourceType}' must be an internal, optional generated-output PDF source type.");
            }
        }

        return PlaOutputDocumentSourceTypeResolution.Succeeded(requested, "local_configuration");
    }
}

public sealed record PlaOutputDocumentSourceTypeResolution(
    bool Success,
    IReadOnlyList<string> SourceTypes,
    string? ResolutionSource,
    string? Diagnostic)
{
    public static PlaOutputDocumentSourceTypeResolution Succeeded(
        IReadOnlyList<string> sourceTypes,
        string resolutionSource)
    {
        return new PlaOutputDocumentSourceTypeResolution(
            true,
            sourceTypes,
            resolutionSource,
            null);
    }

    public static PlaOutputDocumentSourceTypeResolution Failed(string diagnostic)
    {
        return new PlaOutputDocumentSourceTypeResolution(false, Array.Empty<string>(), null, diagnostic);
    }
}
