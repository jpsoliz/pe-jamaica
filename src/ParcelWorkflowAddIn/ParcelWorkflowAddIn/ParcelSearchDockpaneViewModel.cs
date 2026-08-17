using ArcGIS.Desktop.Framework.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.ParcelSearch;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Input;

namespace ParcelWorkflowAddIn;

internal sealed class ParcelSearchDockpaneViewModel : DockPane
{
    internal const string DockPaneId = "ParcelWorkflow_ParcelSearchDockpane";

    private readonly IParcelSearchMapIntegrationService mapIntegrationService;
    private readonly IParcelSearchParishOptionsProvider parishOptionsProvider;
    private readonly RelayCommand searchCommand;
    private readonly RelayCommand clearSearchCommand;
    private readonly RelayCommand zoomToResultsCommand;
    private bool searchLegal = true;
    private bool searchCadastral = true;
    private bool searchSurvey = true;
    private string selectedParish = "All";
    private string volume = string.Empty;
    private string folio = string.Empty;
    private string name = string.Empty;
    private string peNumber = string.Empty;
    private string landValuationNumber = string.Empty;
    private string dpNumber = string.Empty;
    private string rNumber = string.Empty;
    private string statusText = "Ready";
    private string diagnosticsText = string.Empty;
    private string workingGeodatabasePath = string.Empty;
    private string lastUpdatedText = "Not run";
    private int resultCount;
    private bool isSearchBusy;
    private bool hasClearableSearch;

    public ParcelSearchDockpaneViewModel()
        : this(new ParcelSearchMapIntegrationService(), new ArcGisParcelSearchParishOptionsProvider())
    {
    }

    internal ParcelSearchDockpaneViewModel(
        IParcelSearchMapIntegrationService mapIntegrationService,
        IParcelSearchParishOptionsProvider? parishOptionsProvider = null)
    {
        this.mapIntegrationService = mapIntegrationService;
        this.parishOptionsProvider = parishOptionsProvider ?? new ArcGisParcelSearchParishOptionsProvider();
        searchCommand = new RelayCommand(async () => await RunSearchAsync().ConfigureAwait(true), () => !IsSearchBusy);
        clearSearchCommand = new RelayCommand(async () => await ClearSearchAsync().ConfigureAwait(true), () => !IsSearchBusy && HasClearableSearch);
        zoomToResultsCommand = new RelayCommand(async () => await ZoomToResultsAsync().ConfigureAwait(true), () => !IsSearchBusy);
        _ = LoadParishOptionsAsync();
    }

    private static IReadOnlyList<string> DefaultParishOptions { get; } = new[]
    {
        "All",
        "Clarendon",
        "Hanover",
        "Kingston",
        "Manchester",
        "Portland",
        "Saint Andrew",
        "Saint Ann",
        "Saint Catherine",
        "Saint Elizabeth",
        "Saint James",
        "Saint Mary",
        "Saint Thomas",
        "Trelawny",
        "Westmoreland"
    };

    public ObservableCollection<string> ParishOptions { get; } = new(DefaultParishOptions);

    public bool SearchLegal
    {
        get => searchLegal;
        set => SetProperty(ref searchLegal, value, () => SearchLegal);
    }

    public bool SearchCadastral
    {
        get => searchCadastral;
        set => SetProperty(ref searchCadastral, value, () => SearchCadastral);
    }

    public bool SearchSurvey
    {
        get => searchSurvey;
        set => SetProperty(ref searchSurvey, value, () => SearchSurvey);
    }

    public string SelectedParish
    {
        get => selectedParish;
        set => SetProperty(ref selectedParish, string.IsNullOrWhiteSpace(value) ? "All" : value, () => SelectedParish);
    }

    public string Volume
    {
        get => volume;
        set => SetProperty(ref volume, value, () => Volume);
    }

    public string Folio
    {
        get => folio;
        set => SetProperty(ref folio, value, () => Folio);
    }

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value, () => Name);
    }

    public string PeNumber
    {
        get => peNumber;
        set => SetProperty(ref peNumber, value, () => PeNumber);
    }

    public string LandValuationNumber
    {
        get => landValuationNumber;
        set => SetProperty(ref landValuationNumber, value, () => LandValuationNumber);
    }

    public string DpNumber
    {
        get => dpNumber;
        set => SetProperty(ref dpNumber, value, () => DpNumber);
    }

    public string RNumber
    {
        get => rNumber;
        set => SetProperty(ref rNumber, value, () => RNumber);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value, () => StatusText);
    }

    public string DiagnosticsText
    {
        get => diagnosticsText;
        private set => SetProperty(ref diagnosticsText, value, () => DiagnosticsText);
    }

    public string WorkingGeodatabasePath
    {
        get => workingGeodatabasePath;
        private set => SetProperty(ref workingGeodatabasePath, value, () => WorkingGeodatabasePath);
    }

    public string LastUpdatedText
    {
        get => lastUpdatedText;
        private set => SetProperty(ref lastUpdatedText, value, () => LastUpdatedText);
    }

    public int ResultCount
    {
        get => resultCount;
        private set => SetProperty(ref resultCount, value, () => ResultCount);
    }

    public bool IsSearchBusy
    {
        get => isSearchBusy;
        private set
        {
            if (SetProperty(ref isSearchBusy, value, () => IsSearchBusy))
            {
                searchCommand.RaiseCanExecuteChanged();
                clearSearchCommand.RaiseCanExecuteChanged();
                zoomToResultsCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasClearableSearch
    {
        get => hasClearableSearch;
        private set
        {
            if (SetProperty(ref hasClearableSearch, value, () => HasClearableSearch))
            {
                clearSearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ResultLayerName => ParcelSearchResultLayerContract.LayerName;
    public ICommand SearchCommand => searchCommand;
    public ICommand ClearSearchCommand => clearSearchCommand;
    public ICommand ZoomToResultsCommand => zoomToResultsCommand;

    private async Task RunSearchAsync()
    {
        IsSearchBusy = true;
        try
        {
            var settings = InnolaTransactionSettings.Load();
            var plan = ParcelSearchQueryPlanner.Build(BuildCriteria(), settings.CompareEnterpriseCadaster);
            DiagnosticsText = string.Empty;
            if (!plan.ShouldExecute)
            {
                ResultCount = 0;
                HasClearableSearch = false;
                StatusText = plan.StatusMessage;
                DiagnosticsText = BuildVisibleDiagnostics(plan, null);
                WriteSearchLog(settings.CaseFolderOutputRoot, plan, null, null, StatusText);
                return;
            }

            var userName = ParcelSearchWorkspaceResolver.ResolveCurrentUserName(ShellState.Session.CurrentUser?.Username);
            var gdbPath = ParcelSearchWorkspaceResolver.ResolveWorkingGeodatabasePath(settings.CaseFolderOutputRoot, userName);
            WorkingGeodatabasePath = gdbPath;
            var result = await mapIntegrationService.UpdateResultsAsync(plan, gdbPath).ConfigureAwait(true);
            ResultCount = result.ResultCount;
            HasClearableSearch = result.Success && result.ResultCount > 0;
            LastUpdatedText = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
            StatusText = result.LimitReached
                ? $"{result.Message} Result limit reached."
                : result.Message;
            DiagnosticsText = BuildVisibleDiagnostics(plan, result);
            WriteSearchLog(settings.CaseFolderOutputRoot, plan, result, gdbPath, StatusText);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            ResultCount = 0;
            HasClearableSearch = false;
            StatusText = $"Search failed: {ParcelSearchQueryPlanner.RedactDiagnostic(exception.Message)}";
            DiagnosticsText = StatusText;
            try
            {
                WriteSearchLog(InnolaTransactionSettings.Load().CaseFolderOutputRoot, null, null, WorkingGeodatabasePath, StatusText);
            }
            catch (Exception logException) when (logException is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                _ = logException;
            }
        }
        finally
        {
            IsSearchBusy = false;
        }
    }

    private async Task ClearSearchAsync()
    {
        IsSearchBusy = true;
        try
        {
            await mapIntegrationService.ClearSearchAsync().ConfigureAwait(true);
            ResultCount = 0;
            HasClearableSearch = false;
            StatusText = "Search results and map selection cleared.";
            DiagnosticsText = string.Empty;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            StatusText = $"Clear Search failed: {ParcelSearchQueryPlanner.RedactDiagnostic(exception.Message)}";
        }
        finally
        {
            IsSearchBusy = false;
        }
    }

    private async Task ZoomToResultsAsync()
    {
        try
        {
            await mapIntegrationService.ZoomToResultsAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or NotSupportedException)
        {
            StatusText = $"Zoom to Results failed: {ParcelSearchQueryPlanner.RedactDiagnostic(exception.Message)}";
        }
    }

    private async Task LoadParishOptionsAsync()
    {
        try
        {
            var settings = InnolaTransactionSettings.Load();
            var loaded = await parishOptionsProvider
                .LoadParishOptionsAsync(settings.CompareEnterpriseCadaster.ParishSource)
                .ConfigureAwait(true);
            var options = loaded
                .Where(parish => !string.IsNullOrWhiteSpace(parish))
                .Select(parish => parish.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(parish => parish, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            if (options.Length == 0)
            {
                return;
            }

            var currentSelection = SelectedParish;
            ParishOptions.Clear();
            ParishOptions.Add("All");
            foreach (var parish in options)
            {
                if (!string.Equals(parish, "All", StringComparison.OrdinalIgnoreCase))
                {
                    ParishOptions.Add(parish);
                }
            }

            SelectedParish = ParishOptions.Any(parish => string.Equals(parish, currentSelection, StringComparison.OrdinalIgnoreCase))
                ? currentSelection
                : "All";
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException or HttpRequestException or JsonException)
        {
            _ = exception;
        }
    }

    private ParcelSearchCriteria BuildCriteria()
    {
        return new ParcelSearchCriteria
        {
            LayerScopes = BuildSelectedLayerScopes(),
            ParishNames = new[] { SelectedParish },
            Volume = Volume,
            Folio = Folio,
            Name = Name,
            PeNumber = PeNumber,
            LandValuationNumber = LandValuationNumber,
            DpNumber = DpNumber,
            RNumber = RNumber
        };
    }

    private static string BuildVisibleDiagnostics(
        ParcelSearchQueryPlan? plan,
        ParcelSearchMapUpdateResult? result)
    {
        var lines = new List<string>();
        if (plan is not null)
        {
            lines.AddRange(plan.Diagnostics
                .Where(IsVisibleSearchDiagnostic)
                .Select(ParcelSearchQueryPlanner.RedactDiagnostic));
        }

        if (result is not null)
        {
            lines.AddRange(result.Diagnostics
                .Where(IsVisibleSearchDiagnostic)
                .Select(ParcelSearchQueryPlanner.RedactDiagnostic));
        }

        return string.Join(Environment.NewLine, lines.Distinct(StringComparer.OrdinalIgnoreCase).Take(12));
    }

    private static bool IsVisibleSearchDiagnostic(string diagnostic)
    {
        return diagnostic.Contains("query where:", StringComparison.OrdinalIgnoreCase)
            || diagnostic.Contains("outFields=", StringComparison.OrdinalIgnoreCase)
            || diagnostic.Contains("FeatureServer error", StringComparison.OrdinalIgnoreCase)
            || diagnostic.Contains("JSONToFeatures", StringComparison.OrdinalIgnoreCase)
            || diagnostic.Contains("Parcel Search Results were written", StringComparison.OrdinalIgnoreCase)
            || diagnostic.Contains("is excluded because", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteSearchLog(
        string caseFolderOutputRoot,
        ParcelSearchQueryPlan? plan,
        ParcelSearchMapUpdateResult? result,
        string? workingGeodatabasePath,
        string message)
    {
        if (string.IsNullOrWhiteSpace(caseFolderOutputRoot))
        {
            return;
        }

        var logDirectory = Path.Combine(caseFolderOutputRoot, "logs");
        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, "parcel_search.log");
        var lines = new List<string>
        {
            $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {message}"
        };
        if (!string.IsNullOrWhiteSpace(workingGeodatabasePath))
        {
            lines.Add($"working_gdb={ParcelSearchQueryPlanner.RedactDiagnostic(workingGeodatabasePath)}");
        }

        if (plan is not null)
        {
            lines.Add($"sources={string.Join(",", plan.SourceRequests.Select(request => request.SourceDisplayName))}");
            lines.AddRange(plan.Diagnostics.Select(diagnostic => $"plan={ParcelSearchQueryPlanner.RedactDiagnostic(diagnostic)}"));
        }

        if (result is not null)
        {
            lines.Add($"success={result.Success}; result_count={result.ResultCount}; limit_reached={result.LimitReached}");
            lines.AddRange(result.Diagnostics.Select(diagnostic => $"result={ParcelSearchQueryPlanner.RedactDiagnostic(diagnostic)}"));
        }

        lines.Add(string.Empty);
        File.AppendAllLines(logPath, lines);
    }

    private IReadOnlyList<string> BuildSelectedLayerScopes()
    {
        var scopes = new List<string>();
        if (SearchLegal)
        {
            scopes.Add(ParcelSearchLayerScope.Legal);
        }

        if (SearchCadastral)
        {
            scopes.Add(ParcelSearchLayerScope.Cadastral);
        }

        if (SearchSurvey)
        {
            scopes.Add(ParcelSearchLayerScope.Survey);
        }

        return scopes.Count == 0
            ? new[] { "none" }
            : scopes;
    }
}
