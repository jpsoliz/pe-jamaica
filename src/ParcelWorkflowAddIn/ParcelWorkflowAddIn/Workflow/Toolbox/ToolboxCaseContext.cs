using System.IO;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Workflow.Toolbox;

public enum ToolboxPreset { Compute, Compare, PlaA, PlaB, Rt }

public sealed record ToolboxAccess(bool Allowed, string Reason);

public sealed record ToolboxSourceReference(string SourceId, string ServerEnvironment,
    string TransactionId, string TransactionNumber, string? RevisionId)
{
    public bool IsReadOnly => true;
}

public sealed record ToolboxTaskObservation(string? TaskId, string TaskName, string ProcessStep,
    string? AssignedUser, string? AssignedGroup, DateTimeOffset ObservedAt)
{
    public string Resolution => "unverified";
    public string IdProvenance => "selected_transaction_unverified";

    internal static ToolboxTaskObservation From(SelectedInnolaTransaction transaction) =>
        new(string.IsNullOrWhiteSpace(transaction.TaskId) ? null : transaction.TaskId,
            transaction.TaskName, transaction.ProcessStep, transaction.AssignedUser,
            transaction.AssignedGroup, transaction.SelectedAt);
}

public sealed record ToolboxCaseContext
{
    public const string SchemaVersion = "toolbox_context_v1";

    public ToolboxCaseContext(string serverEnvironment, string caseId, string operatorId,
        SelectedInnolaTransaction transaction, string trustedCaseRoot, string caseRoot,
        ToolboxPreset selectedPreset = ToolboxPreset.Compute, bool allowLocalGeometryEdit = false,
        IEnumerable<string>? selectedParcelIds = null, IEnumerable<ToolboxSourceReference>? sources = null,
        string? currentRevisionId = null, string? acceptedRevisionId = null)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ServerEnvironment = Required(serverEnvironment, nameof(serverEnvironment));
        CaseId = Required(caseId, nameof(caseId));
        OperatorId = Required(operatorId, nameof(operatorId));
        TransactionId = Required(transaction.TransactionId, nameof(transaction.TransactionId));
        Required(transaction.TransactionNumber, nameof(transaction.TransactionNumber));
        TransactionNumber = transaction.TransactionNumber;
        NormalizedTransactionNumber = NormalizeTr(TransactionNumber);
        CaseRoot = ValidateRoot(trustedCaseRoot, caseRoot);
        ValidatePreset(selectedPreset);
        SelectedPreset = selectedPreset;
        ActualTask = ToolboxTaskObservation.From(transaction);
        AllowLocalGeometryEdit = allowLocalGeometryEdit;
        SelectedParcelIds = CopyParcelIds(selectedParcelIds ?? Array.Empty<string>());
        var references = (sources ?? Array.Empty<ToolboxSourceReference>()).ToArray();
        foreach (var source in references)
        {
            ArgumentNullException.ThrowIfNull(source);
            Required(source.SourceId, nameof(source.SourceId));
            Required(source.ServerEnvironment, nameof(source.ServerEnvironment));
            Required(source.TransactionId, nameof(source.TransactionId));
            Required(source.TransactionNumber, nameof(source.TransactionNumber));
        }
        Sources = Array.AsReadOnly(references);
        CurrentRevisionId = currentRevisionId;
        AcceptedRevisionId = acceptedRevisionId;
    }

    public string ServerEnvironment { get; }
    public string CaseId { get; }
    public string OperatorId { get; }
    public string TransactionId { get; }
    public string TransactionNumber { get; }
    public string NormalizedTransactionNumber { get; }
    public string CaseRoot { get; }
    public ToolboxPreset SelectedPreset { get; private init; }
    public ToolboxTaskObservation ActualTask { get; private init; }
    public bool AllowLocalGeometryEdit { get; private init; }
    public IReadOnlyList<string> SelectedParcelIds { get; private init; }
    public IReadOnlyList<ToolboxSourceReference> Sources { get; }
    public string? CurrentRevisionId { get; }
    public string? AcceptedRevisionId { get; }

    public ToolboxAccess RemoteActionAccess => new(false, "Remote actions are not supported by the local Toolbox context slice.");

    public ToolboxAccess GeometryAccess(bool sourceReference = false)
    {
        if (sourceReference) return new(false, "Source-reference geometry is read-only.");
        if (SelectedPreset != ToolboxPreset.Compute) return new(false, "Geometry changes require the Compute workspace.");
        return AllowLocalGeometryEdit
            ? new(true, "Explicit local Compute permission; this does not authorize any server action.")
            : new(false, "Local geometry-edit permission has not been granted.");
    }

    public ToolboxCaseContext SelectPreset(ToolboxPreset preset)
    {
        ValidatePreset(preset);
        return this with { SelectedPreset = preset };
    }

    public ToolboxCaseContext SelectParcels(IEnumerable<string> parcelIds)
    {
        ArgumentNullException.ThrowIfNull(parcelIds);
        return this with { SelectedParcelIds = CopyParcelIds(parcelIds) };
    }

    public ToolboxCaseContext RefreshActualTask(string serverEnvironment, SelectedInnolaTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (!string.Equals(ServerEnvironment, Required(serverEnvironment, nameof(serverEnvironment)), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(TransactionId, Required(transaction.TransactionId, nameof(transaction.TransactionId)), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(NormalizedTransactionNumber, NormalizeTr(transaction.TransactionNumber), StringComparison.Ordinal))
            throw new ArgumentException("Task observation belongs to a different case.", nameof(transaction));
        if (transaction.SelectedAt < ActualTask.ObservedAt)
            throw new ArgumentException("Task observation is older than the current observation.", nameof(transaction));
        // Cached local permission cannot survive a change of observed task/assignment.
        return this with { ActualTask = ToolboxTaskObservation.From(transaction), AllowLocalGeometryEdit = false };
    }

    internal static void ValidatePreset(ToolboxPreset preset)
    {
        if (!Enum.IsDefined(preset)) throw new ArgumentOutOfRangeException(nameof(preset));
    }

    private static IReadOnlyList<string> CopyParcelIds(IEnumerable<string> values) =>
        Array.AsReadOnly(values.Select(value => Required(value, "parcelId"))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

    private static string Required(string? value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A nonblank identity is required.", name) : value.Trim();

    private static string NormalizeTr(string? value) => Required(value, "transactionNumber").ToUpperInvariant();

    private static string ValidateRoot(string trustedRoot, string caseRoot)
    {
        Required(trustedRoot, nameof(trustedRoot));
        Required(caseRoot, nameof(caseRoot));
        if (!Path.IsPathFullyQualified(trustedRoot) || !Path.IsPathFullyQualified(caseRoot)
            || caseRoot.Split(new[] { '/', '\\' }).Any(part => part is "." or ".."))
            throw new ArgumentException("An absolute case path without traversal is required.", nameof(caseRoot));
        var trusted = Path.TrimEndingDirectorySeparator(Path.GetFullPath(trustedRoot));
        var resolved = Path.GetFullPath(caseRoot);
        var prefix = Path.EndsInDirectorySeparator(trusted) ? trusted : trusted + Path.DirectorySeparatorChar;
        if (string.Equals(Path.TrimEndingDirectorySeparator(resolved), trusted, StringComparison.OrdinalIgnoreCase)
            || !resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Case path must be inside the supplied isolated case root.", nameof(caseRoot));
        return resolved;
    }
}
