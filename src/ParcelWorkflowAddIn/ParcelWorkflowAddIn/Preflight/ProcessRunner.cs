using System.Diagnostics;

namespace ParcelWorkflowAddIn.Preflight;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(
        string executablePath,
        string arguments,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string?>? environmentVariables = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (environmentVariables is not null)
        {
            foreach (var entry in environmentVariables)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                startInfo.Environment[entry.Key] = entry.Value ?? string.Empty;
            }
        }

        using var process = new Process { StartInfo = startInfo };

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
            catch (Exception)
            {
            }

            try
            {
                await Task.WhenAny(process.WaitForExitAsync(cancellationToken), Task.Delay(TimeSpan.FromMilliseconds(500))).ConfigureAwait(false);
            }
            catch
            {
            }

            return new ProcessRunResult(
                -1,
                await outputTask.ConfigureAwait(false),
                await errorTask.ConfigureAwait(false),
                TimedOut: true);
        }

        return new ProcessRunResult(
            process.ExitCode,
            await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false),
            TimedOut: false);
    }
}
