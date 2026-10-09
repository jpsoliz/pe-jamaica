using System.IO;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Toolbox;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class ToolboxContextTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "ToolboxSyntheticCases");
    private static SelectedInnolaTransaction Transaction(string id = "transaction-a", string task = "task-compare") =>
        new(task, id, " tr1001 ", "Compare", "Compare", DateTimeOffset.Parse("2026-10-08T12:00:00Z"),
            TransactionType: "Cad Map", AssignedUser: "examiner", AssignedGroup: "survey");

    private static ToolboxCaseContext Context(bool edit = false, IEnumerable<string>? parcels = null,
        ToolboxPreset preset = ToolboxPreset.Compute) => new("sandbox", "case-a", "examiner", Transaction(),
            Root, Path.Combine(Root, "case-a"), preset, edit, parcels,
            new[] { new ToolboxSourceReference("source-a", "sandbox", "source-tr", "TR900", "revision-pe") },
            "revision-current", "revision-accepted");

    public static void ContextPreservesIdentityAndUnverifiedTask()
    {
        var context = Context();
        TestAssert.Equal(" tr1001 ", context.TransactionNumber, "Original TR must survive.");
        TestAssert.Equal("TR1001", context.NormalizedTransactionNumber, "Normalize only casing/outer whitespace.");
        TestAssert.Equal("transaction-a", context.TransactionId, "Keep server ID distinct from TR.");
        TestAssert.Equal("unverified", context.ActualTask.Resolution, "Display rows cannot prove task identity.");
        TestAssert.Equal("selected_transaction_unverified", context.ActualTask.IdProvenance, "Retain provenance.");
        TestAssert.Equal("survey", context.ActualTask.AssignedGroup, "Keep observed assignment.");
        TestAssert.Equal(Transaction().SelectedAt, context.ActualTask.ObservedAt, "Retain observation time.");
        TestAssert.True(context.Sources[0].IsReadOnly, "Linked PE source cannot be editable.");
        foreach (var task in new[] { "", "transaction-a" })
        {
            var unknown = new ToolboxCaseContext("sandbox", "case-a", "examiner", Transaction(task: task),
                Root, Path.Combine(Root, "case-a"));
            TestAssert.Equal("unverified", unknown.ActualTask.Resolution, "Missing/fallback IDs remain unverified.");
        }
    }

    public static void EveryPresetChangesLocalIntentOnly()
    {
        var original = Context(parcels: new[] { "parcel-a", "parcel-b", "parcel-c" });
        foreach (var preset in Enum.GetValues<ToolboxPreset>())
        {
            var selected = original.SelectPreset(preset);
            TestAssert.Equal(preset, selected.SelectedPreset, "Select every supported workspace.");
            TestAssert.Equal(original.ActualTask, selected.ActualTask, "Workspace is not server task.");
            TestAssert.Equal(original.CaseRoot, selected.CaseRoot, "Keep case path.");
            TestAssert.Equal(original.CurrentRevisionId, selected.CurrentRevisionId, "Keep current revision.");
            TestAssert.Equal(original.AcceptedRevisionId, selected.AcceptedRevisionId, "Keep accepted revision.");
            TestAssert.Equal(3, selected.SelectedParcelIds.Count, "Keep every parcel.");
            TestAssert.Equal(original.Sources[0], selected.Sources[0], "Keep source identity.");
            TestAssert.False(selected.RemoteActionAccess.Allowed, "Every preset denies remote effects.");
            TestAssert.True(selected.RemoteActionAccess.Reason.Length > 0, "Explain remote denial.");
        }
        TestAssert.Equal(ToolboxPreset.Compute, original.SelectedPreset, "Do not mutate prior context.");
        Reject(() => original.SelectPreset((ToolboxPreset)999));
    }

    public static void LocalGeometryAccessIsConservative()
    {
        TestAssert.False(Context().GeometryAccess().Allowed, "Compute is default-denied.");
        var editable = Context(edit: true);
        TestAssert.True(editable.GeometryAccess().Allowed, "Explicit local permission permits current Compute eligibility.");
        TestAssert.False(editable.GeometryAccess(sourceReference: true).Allowed, "References stay read-only.");
        foreach (var preset in Enum.GetValues<ToolboxPreset>().Where(value => value != ToolboxPreset.Compute))
            TestAssert.False(editable.SelectPreset(preset).GeometryAccess().Allowed, "No geometry grant outside Compute.");
        TestAssert.Equal("Compare", editable.ActualTask.TaskName, "No backward server transition.");
    }

    public static void ParcelAndSourceCollectionsAreDefensive()
    {
        var parcels = new[] { " parcel-a ", "PARCEL-A", "parcel-b", "parcel-c" };
        var context = Context(parcels: parcels);
        parcels[0] = "tampered";
        TestAssert.Equal(3, context.SelectedParcelIds.Count, "Deduplicate without dropping unrelated parcels.");
        TestAssert.Equal("parcel-a", context.SelectedParcelIds[0], "Copy input collections.");
        TestAssert.Equal(0, context.SelectParcels(Array.Empty<string>()).SelectedParcelIds.Count, "Allow case-level scope.");
        Reject(() => context.SelectParcels(new[] { " " }));
        var sources = new[] { context.Sources[0] };
        var copy = new ToolboxCaseContext("sandbox", "case-a", "examiner", Transaction(), Root,
            Path.Combine(Root, "case-a"), sources: sources);
        sources[0] = new("tampered", "other", "other", "TR2", null);
        TestAssert.Equal("source-a", copy.Sources[0].SourceId, "Copy source references.");
    }

    public static void InvalidIdentityAndPathsAreRejected()
    {
        foreach (var root in new[] { Root, Path.Combine(Root, "..", "escape"), Path.Combine(Root + "-other", "case"), "relative" })
            Reject(() => new ToolboxCaseContext("sandbox", "case-a", "examiner", Transaction(), Root, root));
        Reject(() => new ToolboxCaseContext("sandbox", "case-a", "examiner", Transaction(), Root,
            Path.Combine(Root, "case-a"), selectedPreset: (ToolboxPreset)999));
        Reject(() => new ToolboxCaseContext("sandbox", "case-a", "examiner", Transaction(), "relative", Path.Combine(Root, "case-a")));
        foreach (var value in new[] { "", " " })
        {
            Reject(() => new ToolboxCaseContext(value, "case-a", "examiner", Transaction(), Root, Path.Combine(Root, "case-a")));
            Reject(() => new ToolboxCaseContext("sandbox", value, "examiner", Transaction(), Root, Path.Combine(Root, "case-a")));
            Reject(() => new ToolboxCaseContext("sandbox", "case-a", value, Transaction(), Root, Path.Combine(Root, "case-a")));
            Reject(() => new ToolboxCaseContext("sandbox", "case-a", "examiner", Transaction(id: value), Root, Path.Combine(Root, "case-a")));
            Reject(() => new ToolboxCaseContext("sandbox", "case-a", "examiner", Transaction() with { TransactionNumber = value }, Root, Path.Combine(Root, "case-a")));
        }
    }

    public static void SelectorLoadSelectRefreshAndClearAreObservable()
    {
        var selector = new ToolboxWorkspaceSelection();
        var notifications = new List<string?>();
        selector.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        TestAssert.False(selector.TrySelectPreset(ToolboxPreset.Compute).Allowed, "No selection without case.");
        selector.Load(Context(edit: true, parcels: new[] { "parcel-a", "parcel-b" }));
        selector.SelectedPreset = ToolboxPreset.Compare;
        TestAssert.Equal(ToolboxPreset.Compare, selector.SelectedPreset, "Bindable selector updates preset.");
        TestAssert.Equal("Compare", selector.ActualTaskName, "Actual task stays visible.");
        var before = selector.Current;
        TestAssert.False(selector.TrySelectPreset((ToolboxPreset)999).Allowed, "Invalid selection fails safely.");
        TestAssert.Equal(before, selector.Current, "Invalid selection preserves current context.");
        TestAssert.False(selector.RefreshActualTask("other-environment", Transaction()).Allowed, "Wrong environment blocked.");
        TestAssert.False(selector.RefreshActualTask("sandbox", Transaction(id: "other")).Allowed, "Wrong transaction blocked.");
        TestAssert.False(selector.RefreshActualTask("sandbox", Transaction() with { TransactionNumber = "TR2000" }).Allowed, "Wrong TR blocked.");
        TestAssert.Equal(before, selector.Current, "Wrong refresh cannot replace context.");
        TestAssert.False(selector.RefreshActualTask("sandbox", Transaction() with { SelectedAt = Transaction().SelectedAt.AddMinutes(-1) }).Allowed,
            "Older observation cannot replace current task.");
        TestAssert.Equal(before, selector.Current, "Stale refresh preserves context.");
        var next = Transaction(task: "task-next") with { TaskName = "RT", SelectedAt = Transaction().SelectedAt.AddMinutes(1) };
        TestAssert.True(selector.RefreshActualTask("sandbox", next).Allowed, "Accept explicit same-case observation.");
        TestAssert.Equal("RT", selector.ActualTaskName, "Refresh actual task independently.");
        TestAssert.Equal(ToolboxPreset.Compare, selector.SelectedPreset, "Refresh preserves workspace.");
        TestAssert.Equal(2, selector.Current!.SelectedParcelIds.Count, "Refresh preserves all parcels.");
        TestAssert.True(selector.SelectParcels(new[] { "parcel-c", "parcel-d" }).Allowed, "Bindable scope service updates parcels.");
        TestAssert.Equal("parcel-c", selector.Current.SelectedParcelIds[0], "Retain new scope.");
        TestAssert.False(selector.SelectParcels(new[] { "" }).Allowed, "Invalid scope fails safely.");
        TestAssert.Equal(2, selector.Current.SelectedParcelIds.Count, "Failed scope change preserves all IDs.");
        selector.SelectedPreset = ToolboxPreset.Compute;
        TestAssert.False(selector.LocalGeometryAccess.Allowed, "Refresh clears old local authorization.");
        foreach (var property in new[] { "Current", "SelectedPreset", "TransactionNumber", "ActualTaskName", "LocalGeometryAccess", "RemoteActionAccess" })
            TestAssert.True(notifications.Contains(property), "Notify projection: " + property);
        selector.Load(new ToolboxCaseContext("sandbox", "case-b", "examiner", Transaction(id: "transaction-b"), Root, Path.Combine(Root, "case-b")));
        TestAssert.Equal(0, selector.Current!.SelectedParcelIds.Count, "Replace clears old parcels.");
        TestAssert.Equal<string?>(null, selector.Current.CurrentRevisionId, "Replace clears old revision.");
        TestAssert.False(selector.LocalGeometryAccess.Allowed, "Replace cannot inherit permission.");
        selector.Clear();
        TestAssert.Equal<ToolboxCaseContext?>(null, selector.Current, "Clear case.");
        TestAssert.Equal<string?>(null, selector.TransactionNumber, "Clear displayed TR.");
        TestAssert.False(selector.RemoteActionAccess.Allowed, "Clear stays remote-denied.");
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Expected unsafe/invalid context input to be rejected.");
    }

    public static void DriveAndUncRootPathsAcceptOnlyDescendants()
    {
        foreach (var trusted in new[] { Path.GetPathRoot(Path.GetTempPath())!, @"\\server\share\" })
        {
            var child = Path.Combine(trusted, "case-a");
            var context = new ToolboxCaseContext("sandbox", "case-a", "examiner", Transaction(), trusted, child);
            TestAssert.Equal(Path.GetFullPath(child), context.CaseRoot, "Accept a lexical child of a drive or UNC root.");
            Reject(() => new ToolboxCaseContext("sandbox", "case-a", "examiner", Transaction(), trusted, trusted));
        }
    }

    public static void RefreshUsesConstructorIdentityNormalization()
    {
        var transaction = Transaction(id: " transaction-a ");
        var context = new ToolboxCaseContext(" sandbox ", "case-a", "examiner", transaction,
            Root, Path.Combine(Root, "case-a"), selectedParcelIds: new[] { "parcel-a", "parcel-b" });
        var refreshed = context.RefreshActualTask(" SANDBOX ", transaction);
        TestAssert.Equal("transaction-a", refreshed.TransactionId, "Refresh preserves normalized case identity.");
        TestAssert.Equal(2, refreshed.SelectedParcelIds.Count, "Refresh preserves parcel scope.");
        foreach (var value in new[] { "", " " })
        {
            Reject(() => context.RefreshActualTask(value, transaction));
            Reject(() => context.RefreshActualTask("sandbox", transaction with { TransactionId = value }));
        }
    }
}
