using System.ComponentModel;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Workflow.Toolbox;

public sealed class ToolboxWorkspaceSelection : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<ToolboxPreset> Presets { get; } = Array.AsReadOnly(Enum.GetValues<ToolboxPreset>());
    public ToolboxCaseContext? Current { get; private set; }
    public string? TransactionNumber => Current?.TransactionNumber;
    public string? ActualTaskName => Current?.ActualTask.TaskName;
    public ToolboxAccess LocalGeometryAccess => Current?.GeometryAccess() ?? NoCase();
    public ToolboxAccess RemoteActionAccess => Current?.RemoteActionAccess ?? NoCase();

    public ToolboxPreset? SelectedPreset
    {
        get => Current?.SelectedPreset;
        set
        {
            if (value.HasValue) TrySelectPreset(value.Value);
        }
    }

    public void Load(ToolboxCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Replace(context);
    }

    public void Clear() => Replace(null);

    public ToolboxAccess TrySelectPreset(ToolboxPreset preset)
    {
        if (Current is null) return NoCase();
        if (!Enum.IsDefined(preset)) return new(false, "Unknown Toolbox workspace.");
        if (preset != Current.SelectedPreset) Replace(Current.SelectPreset(preset));
        return new(true, "Local workspace selected; actual Innola task is unchanged.");
    }

    public ToolboxAccess SelectParcels(IEnumerable<string> parcelIds)
    {
        if (Current is null) return NoCase();
        try { Replace(Current.SelectParcels(parcelIds)); }
        catch (ArgumentException) { return new(false, "Parcel selection contains an invalid identity."); }
        return new(true, "Local parcel scope selected.");
    }

    public ToolboxAccess RefreshActualTask(string serverEnvironment, SelectedInnolaTransaction transaction)
    {
        if (Current is null) return NoCase();
        try { Replace(Current.RefreshActualTask(serverEnvironment, transaction)); }
        catch (ArgumentException) { return new(false, "Task observation does not match the current case or is invalid/stale."); }
        return new(true, "Actual task observation refreshed; no remote authorization granted.");
    }

    private static ToolboxAccess NoCase() => new(false, "Load a case before selecting a workspace or action.");

    private void Replace(ToolboxCaseContext? context)
    {
        Current = context;
        foreach (var property in new[] { nameof(Current), nameof(TransactionNumber), nameof(ActualTaskName),
            nameof(SelectedPreset), nameof(LocalGeometryAccess), nameof(RemoteActionAccess) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}
