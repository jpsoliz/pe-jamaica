using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Input;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Workflow.RtExamination;

public sealed record RtExaminationSettings(
    bool Enabled,
    string StageName,
    string? SubworkflowName,
    string? DesiredTransitionName,
    string WorkingReviewPeNumberField)
{
    public static RtExaminationSettings Default { get; } = new(
        true,
        "In RT Examination",
        "RT Examination",
        null,
        "PE_number");

    public static RtExaminationSettings FromJson(JsonElement root)
    {
        if (!root.TryGetProperty("rt_examination", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return Default;
        }

        return new RtExaminationSettings(
            ReadBool(value, "enabled") ?? Default.Enabled,
            ReadString(value, "stage_name") ?? Default.StageName,
            ReadString(value, "subworkflow_name") ?? Default.SubworkflowName,
            ReadString(value, "desired_transition_name") ?? Default.DesiredTransitionName,
            ReadString(value, "working_review_pe_number_field") ?? Default.WorkingReviewPeNumberField);
    }

    public bool MatchesStage(string? stageName)
    {
        return Enabled
            && !string.IsNullOrWhiteSpace(stageName)
            && stageName.Trim().Equals(StageName, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;
    }

    private static bool? ReadBool(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
    }
}

public sealed record RtExaminationPartyRow(
    string Role,
    string? Name,
    string? Address,
    string? Volume,
    string? Folio,
    string? Lot,
    string? LandValNumber,
    string? ExamNumber)
{
    public static IReadOnlyList<string> AllowedRoles { get; } = new[]
    {
        "Neighbor",
        "Owner",
        "Occupier",
        "Representative"
    };

    public string DeduplicationKey => string.Join("|", new[]
    {
        Normalize(Role),
        Normalize(Name),
        Normalize(Address),
        Normalize(Volume),
        Normalize(Folio),
        Normalize(Lot),
        Normalize(LandValNumber),
        Normalize(ExamNumber)
    });

    public static bool IsAllowedRole(string? role)
    {
        return AllowedRoles.Any(allowed => allowed.Equals(role?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeRole(string? role)
    {
        return AllowedRoles.FirstOrDefault(allowed => allowed.Equals(role?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Neighbor";
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    }
}

public sealed record RtExaminationSpatialUnitAttribute(string SpatialUnitId, string FieldName, string? OriginalValue, string? ReviewedValue)
{
    public bool IsDirty => !string.Equals(OriginalValue, ReviewedValue, StringComparison.Ordinal);
}

public static class RtExaminationSpatialUnitFieldPolicy
{
    private static readonly HashSet<string> BlockedExact = new(StringComparer.OrdinalIgnoreCase)
    {
        "geometry",
        "shape",
        "coordinates",
        "rings",
        "paths",
        "points",
        "point",
        "bfsMinus",
        "bfsPlus",
        "bfMinus",
        "bfPlus",
        "boundary",
        "boundaries"
    };

    private static readonly HashSet<string> SystemExact = new(StringComparer.OrdinalIgnoreCase)
    {
        "@c",
        "@id",
        "id",
        "uid",
        "link",
        "versionRev",
        "allowRead",
        "allowWrite"
    };

    public static bool IsEditableAttribute(string? fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return false;
        }

        var name = fieldName.Trim();
        if (BlockedExact.Contains(name) || SystemExact.Contains(name))
        {
            return false;
        }

        return !name.Contains("geometry", StringComparison.OrdinalIgnoreCase)
            && !name.Contains("coordinate", StringComparison.OrdinalIgnoreCase)
            && !name.Contains("bfs", StringComparison.OrdinalIgnoreCase)
            && !name.StartsWith("bf", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class RtExaminationPartyRowViewModel : INotifyPropertyChanged
{
    private string role;
    private string? name;
    private string? address;
    private string? volume;
    private string? folio;
    private string? lot;
    private string? landValNumber;
    private string? examNumber;

    public RtExaminationPartyRowViewModel(RtExaminationPartyRow row)
    {
        role = RtExaminationPartyRow.NormalizeRole(row.Role);
        name = row.Name;
        address = row.Address;
        volume = row.Volume;
        folio = row.Folio;
        lot = row.Lot;
        landValNumber = row.LandValNumber;
        examNumber = row.ExamNumber;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<string> AllowedRoles => RtExaminationPartyRow.AllowedRoles;

    public string Role
    {
        get => role;
        set
        {
            var normalized = RtExaminationPartyRow.NormalizeRole(value);
            if (string.Equals(role, normalized, StringComparison.Ordinal))
            {
                return;
            }

            role = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Role)));
        }
    }
    public string? Name { get => name; set => Set(ref name, value, nameof(Name)); }
    public string? Address { get => address; set => Set(ref address, value, nameof(Address)); }
    public string? Volume { get => volume; set => Set(ref volume, value, nameof(Volume)); }
    public string? Folio { get => folio; set => Set(ref folio, value, nameof(Folio)); }
    public string? Lot { get => lot; set => Set(ref lot, value, nameof(Lot)); }
    public string? LandValNumber { get => landValNumber; set => Set(ref landValNumber, value, nameof(LandValNumber)); }
    public string? ExamNumber { get => examNumber; set => Set(ref examNumber, value, nameof(ExamNumber)); }

    public RtExaminationPartyRow ToRow() => new(Role, Name, Address, Volume, Folio, Lot, LandValNumber, ExamNumber);

    private void Set(ref string? field, string? value, string propertyName)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class RtExaminationSpatialUnitAttributeViewModel : INotifyPropertyChanged
{
    private string? reviewedValue;

    public RtExaminationSpatialUnitAttributeViewModel(string spatialUnitId, string fieldName, string? originalValue)
    {
        SpatialUnitId = spatialUnitId;
        FieldName = fieldName;
        OriginalValue = originalValue;
        reviewedValue = originalValue;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string SpatialUnitId { get; }

    public string FieldName { get; }

    public string? OriginalValue { get; }

    public string? ReviewedValue
    {
        get => reviewedValue;
        set
        {
            if (string.Equals(reviewedValue, value, StringComparison.Ordinal))
            {
                return;
            }

            reviewedValue = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReviewedValue)));
        }
    }
}

public sealed record RtExaminationContextDocument(
    string SchemaVersion,
    DateTimeOffset WrittenAtUtc,
    string CurrentRtTransactionId,
    string CurrentRtTransactionNumber,
    string CurrentTaskId,
    string? CurrentPlanId,
    string? CurrentPlanUid,
    string? CurrentPlanTrId,
    string? CurrentPlanTrNo,
    string? PlanNumber,
    string? OriginatingPeTransactionId,
    string? OriginatingPeNumber,
    int SourceCount,
    int SpatialUnitCount,
    string WorkingReviewQueryKey,
    IReadOnlyList<string> Warnings);

public sealed record RtExaminationReviewDocument(
    string SchemaVersion,
    DateTimeOffset WrittenAtUtc,
    string TransactionNumber,
    IReadOnlyList<RtExaminationPartyRow> PartyRows,
    IReadOnlyList<RtExaminationSpatialUnitAttribute> SpatialUnitAttributes,
    string? Observations,
    string? Reviewer);

public sealed record RtExaminationLoadResult(
    bool Success,
    string Message,
    RtExaminationContextDocument? Context,
    IReadOnlyList<RtExaminationPartyRow> PartyRows,
    IReadOnlyList<RtExaminationSpatialUnitAttributeViewModel> SpatialUnitAttributes,
    IReadOnlyList<string> SourceLabels,
    IReadOnlyList<string> LoadedMapGroups)
{
    public static RtExaminationLoadResult Failed(string message) => new(false, message, null, Array.Empty<RtExaminationPartyRow>(), Array.Empty<RtExaminationSpatialUnitAttributeViewModel>(), Array.Empty<string>(), Array.Empty<string>());

    public static RtExaminationLoadResult Succeeded(
        string message,
        RtExaminationContextDocument context,
        IReadOnlyList<RtExaminationPartyRow> partyRows,
        IReadOnlyList<RtExaminationSpatialUnitAttributeViewModel> spatialUnitAttributes,
        IReadOnlyList<string> sourceLabels,
        IReadOnlyList<string>? loadedMapGroups = null) => new(true, message, context, partyRows, spatialUnitAttributes, sourceLabels, loadedMapGroups ?? Array.Empty<string>());
}

public sealed record RtExaminationSaveRequest(
    SelectedInnolaTransaction Transaction,
    string CaseFolderPath,
    IReadOnlyList<RtExaminationPartyRow> PartyRows,
    IReadOnlyList<RtExaminationSpatialUnitAttribute> SpatialUnitAttributes,
    string? Observations,
    bool CompleteAfterSave);

public sealed record RtExaminationSaveResult(bool Success, string Message, string? ErrorCategory = null)
{
    public static RtExaminationSaveResult Succeeded(string message) => new(true, message);
    public static RtExaminationSaveResult Failed(string message, string? errorCategory = null) => new(false, message, errorCategory);
}

public interface IRtExaminationLoadService
{
    Task<RtExaminationLoadResult> LoadAsync(SelectedInnolaTransaction transaction, string caseFolderPath, CancellationToken cancellationToken = default);

    Task CleanupAsync(IReadOnlyList<string> loadedMapGroups, CancellationToken cancellationToken = default);
}

public interface IRtExaminationWritebackService
{
    Task<RtExaminationSaveResult> SaveAsync(RtExaminationSaveRequest request, CancellationToken cancellationToken = default);
}

public sealed class DeferredRtExaminationLoadService : IRtExaminationLoadService
{
    public Task<RtExaminationLoadResult> LoadAsync(SelectedInnolaTransaction transaction, string caseFolderPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(RtExaminationLoadResult.Failed("RT Examination linked PE data is not loaded yet. Use Load Linked PE Data after opening the workspace."));
    }

    public Task CleanupAsync(IReadOnlyList<string> loadedMapGroups, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public sealed class RtExaminationViewModel : INotifyPropertyChanged
{
    private readonly SelectedInnolaTransaction transaction;
    private readonly string caseFolderPath;
    private readonly IRtExaminationLoadService loadService;
    private readonly IRtExaminationWritebackService writebackService;
    private readonly Func<string, bool> confirmAction;
    private readonly Action<string> showMessage;
    private readonly Action? refreshTransactions;
    private string statusText;
    private string? pePlanNumber;
    private string? originatingPeText;
    private string? observations;
    private bool isBusy;
    private bool isLoaded;
    private IReadOnlyList<string> loadedMapGroups = Array.Empty<string>();

    public RtExaminationViewModel(
        SelectedInnolaTransaction transaction,
        string caseFolderPath,
        IRtExaminationLoadService? loadService = null,
        IRtExaminationWritebackService? writebackService = null,
        Func<string, bool>? confirmAction = null,
        Action<string>? showMessage = null,
        Action? refreshTransactions = null)
    {
        this.transaction = transaction;
        this.caseFolderPath = caseFolderPath;
        this.loadService = loadService ?? new DeferredRtExaminationLoadService();
        this.writebackService = writebackService ?? new DeferredRtExaminationWritebackService();
        this.confirmAction = confirmAction ?? (_ => true);
        this.showMessage = showMessage ?? (_ => { });
        this.refreshTransactions = refreshTransactions;
        statusText = "Load linked PE data to begin RT Examination.";
        LoadLinkedPeDataCommand = new RelayCommand(async () => await LoadAsync().ConfigureAwait(true), () => !IsBusy);
        SaveCommand = new RelayCommand(async () => await SaveAsync(false).ConfigureAwait(true), () => CanSave);
        CompleteCommand = new RelayCommand(async () => await SaveAsync(true).ConfigureAwait(true), () => CanComplete);
        SuspendCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? RequestClose;

    public string TransactionNumber => transaction.TransactionNumber;
    public string TransactionType => string.IsNullOrWhiteSpace(transaction.TransactionType) ? "Transaction" : transaction.TransactionType!;
    public string StageName => transaction.TaskName;
    public string StatusText { get => statusText; private set { statusText = value; Notify(nameof(StatusText)); } }
    public string? PePlanNumber { get => pePlanNumber; private set { pePlanNumber = value; Notify(nameof(PePlanNumber)); Notify(nameof(PePlanNumberText)); } }
    public string PePlanNumberText => string.IsNullOrWhiteSpace(PePlanNumber) ? "Not loaded" : PePlanNumber!;
    public string? OriginatingPeText { get => originatingPeText; private set { originatingPeText = value; Notify(nameof(OriginatingPeText)); } }
    public string? Observations { get => observations; set { observations = value; Notify(nameof(Observations)); } }
    public bool IsBusy { get => isBusy; private set { isBusy = value; Notify(nameof(IsBusy)); RefreshCommands(); } }
    public bool IsLoaded { get => isLoaded; private set { isLoaded = value; Notify(nameof(IsLoaded)); RefreshCommands(); } }
    public bool CanSave => IsLoaded && !IsBusy;
    public bool CanComplete => IsLoaded && !IsBusy;

    public ObservableCollection<RtExaminationPartyRowViewModel> PartyRows { get; } = [];
    public ObservableCollection<RtExaminationSpatialUnitAttributeViewModel> SpatialUnitAttributes { get; } = [];
    public ObservableCollection<string> SourceLabels { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];

    public ICommand LoadLinkedPeDataCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CompleteCommand { get; }
    public ICommand SuspendCommand { get; }
    public ICommand CancelCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "Loading RT Examination linked PE data...";
        try
        {
            var result = await loadService.LoadAsync(transaction, caseFolderPath, cancellationToken).ConfigureAwait(true);
            if (!result.Success || result.Context is null)
            {
                IsLoaded = false;
                StatusText = result.Message;
                return;
            }

            PartyRows.Clear();
            foreach (var row in result.PartyRows)
            {
                PartyRows.Add(new RtExaminationPartyRowViewModel(row));
            }

            SpatialUnitAttributes.Clear();
            foreach (var item in result.SpatialUnitAttributes)
            {
                SpatialUnitAttributes.Add(item);
            }

            loadedMapGroups = result.LoadedMapGroups;

            SourceLabels.Clear();
            foreach (var label in result.SourceLabels)
            {
                SourceLabels.Add(label);
            }

            Warnings.Clear();
            foreach (var warning in result.Context.Warnings)
            {
                Warnings.Add(warning);
            }

            PePlanNumber = result.Context.PlanNumber;
            OriginatingPeText = string.IsNullOrWhiteSpace(result.Context.OriginatingPeNumber)
                ? "Not resolved"
                : $"{result.Context.OriginatingPeNumber} ({result.Context.OriginatingPeTransactionId})";
            IsLoaded = true;
            StatusText = result.Message;
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or HttpRequestException
            or IOException
            or UnauthorizedAccessException
            or TaskCanceledException)
        {
            IsLoaded = false;
            StatusText = $"RT Examination linked PE data could not be loaded. {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync(bool completeAfterSave)
    {
        if (completeAfterSave && !confirmAction("Save RT Examination data and complete this Innola task?"))
        {
            StatusText = "RT Examination completion cancelled.";
            return;
        }

        IsBusy = true;
        StatusText = completeAfterSave ? "Saving and completing RT Examination..." : "Saving RT Examination data...";
        try
        {
            var result = await writebackService.SaveAsync(
                new RtExaminationSaveRequest(
                    transaction,
                    caseFolderPath,
                    PartyRows.Select(row => row.ToRow()).ToArray(),
                    SpatialUnitAttributes.Select(item => new RtExaminationSpatialUnitAttribute(item.SpatialUnitId, item.FieldName, item.OriginalValue, item.ReviewedValue)).ToArray(),
                    Observations,
                    completeAfterSave)).ConfigureAwait(true);
            StatusText = result.Message;
            if (result.Success && completeAfterSave)
            {
                await loadService.CleanupAsync(loadedMapGroups).ConfigureAwait(true);
                loadedMapGroups = Array.Empty<string>();
                showMessage("RT Examination completed and moved to the next Innola stage.");
                refreshTransactions?.Invoke();
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or HttpRequestException
            or IOException
            or UnauthorizedAccessException
            or TaskCanceledException)
        {
            StatusText = $"RT Examination save could not be completed. {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshCommands()
    {
        foreach (var command in new[] { LoadLinkedPeDataCommand, SaveCommand, CompleteCommand })
        {
            if (command is RelayCommand relay)
            {
                relay.RaiseCanExecuteChanged();
            }
        }
    }

    private void Notify(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class DeferredRtExaminationWritebackService : IRtExaminationWritebackService
{
    public Task<RtExaminationSaveResult> SaveAsync(RtExaminationSaveRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(RtExaminationSaveResult.Failed("RT Examination writeback service is not configured."));
    }
}