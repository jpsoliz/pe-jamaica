namespace ParcelWorkflowAddIn.Preflight;

public sealed record ProcessingEnvironmentPreflightResult(
    IReadOnlyList<PreflightCheck> Blockers,
    IReadOnlyList<PreflightCheck> Warnings,
    IReadOnlyList<PreflightCheck> PassedChecks)
{
    public static ProcessingEnvironmentPreflightResult Empty { get; } = new(
        Array.Empty<PreflightCheck>(),
        Array.Empty<PreflightCheck>(),
        Array.Empty<PreflightCheck>());
}
