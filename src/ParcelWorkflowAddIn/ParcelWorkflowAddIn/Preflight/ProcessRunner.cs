using System.Diagnostics;

namespace ParcelWorkflowAddIn.Preflight;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(string executablePath, string arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var finishedTask = process.WaitForExitAsync(cancellationToken);
        var completed = await Task.WhenAny(finishedTask, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
        if (completed != finishedTask)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            return new ProcessRunResult(-1, string.Empty, string.Empty, TimedOut: true);
        }

        return new ProcessRunResult(
            process.ExitCode,
            await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false),
            TimedOut: false);
    }
}
