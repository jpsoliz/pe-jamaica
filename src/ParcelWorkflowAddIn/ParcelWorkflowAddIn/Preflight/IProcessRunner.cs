namespace ParcelWorkflowAddIn.Preflight;

public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(
        string executablePath,
        string arguments,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string?>? environmentVariables = null,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessRunResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut);
