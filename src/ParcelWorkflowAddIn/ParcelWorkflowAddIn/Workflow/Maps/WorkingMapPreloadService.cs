using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Workflow.Maps;

public sealed class WorkingMapPreloadService
{
    private readonly IWorkingMapPreparationService mapPreparationService;
    private readonly Func<WorkingMapSettings> loadSettings;
    private readonly SemaphoreSlim gate = new(1, 1);
    private CancellationTokenSource? currentPreload;

    public WorkingMapPreloadService(
        IWorkingMapPreparationService mapPreparationService,
        Func<WorkingMapSettings> loadSettings)
    {
        this.mapPreparationService = mapPreparationService;
        this.loadSettings = loadSettings;
    }

    public string StatusText { get; private set; } = "Working map preload has not run.";

    internal Task? CurrentPreloadTask { get; private set; }

    public void StartWorkingMapPreloadAfterLogin()
    {
        var settings = loadSettings();
        if (!settings.Enabled || !settings.PreloadAfterLogin)
        {
            StatusText = "Working map preload is disabled.";
            return;
        }

        if (currentPreload is { IsCancellationRequested: false } && CurrentPreloadTask is { IsCompleted: false })
        {
            return;
        }

        currentPreload = new CancellationTokenSource();
        CurrentPreloadTask = PreloadAsync(currentPreload.Token);
    }

    public void Cancel()
    {
        currentPreload?.Cancel();
        currentPreload?.Dispose();
        currentPreload = null;
    }

    internal async Task PreloadAsync(CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var settings = loadSettings();
            var result = await mapPreparationService
                .PrepareWorkingMapAsync(settings, CreatePreloadDetail(), cancellationToken)
                .ConfigureAwait(false);
            StatusText = result.Success
                ? $"Working map preload completed. {result.Message}"
                : $"Working map preload skipped: {result.Message}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Working map preload was cancelled.";
        }
        catch (Exception exception)
        {
            StatusText = $"Working map preload skipped: {exception.Message}";
        }
        finally
        {
            gate.Release();
        }
    }

    private static InnolaTransactionDetail CreatePreloadDetail()
    {
        return new InnolaTransactionDetail(
            "working-map-preload",
            "working-map-preload",
            "working-map-preload",
            "Working Map Preload",
            "parcel_workflow",
            "preload",
            "preload",
            null,
            null,
            null,
            null,
            Array.Empty<InnolaAttachmentMetadata>());
    }
}
