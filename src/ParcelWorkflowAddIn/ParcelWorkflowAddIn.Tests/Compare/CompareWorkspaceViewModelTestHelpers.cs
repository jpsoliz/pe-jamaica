using System.IO;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow;

namespace ParcelWorkflowAddIn.Tests.Compare;

internal static class CompareWorkspaceViewModelTestHelpers
{
    public static CompareWorkspaceViewModel CreateViewModel(
        ILegalCadasterQueryService? legalService = null,
        IFiscalCadasterQueryService? fiscalService = null,
        ICompareEnterpriseCadasterEvidenceService? enterpriseCadasterEvidenceService = null)
    {
        return new CompareWorkspaceViewModel(new SelectedInnolaTransaction(
            "task-1",
            "100000674",
            "TR100000674",
            "Compare Survey Plan",
            "Compare",
            DateTimeOffset.Parse("2026-07-14T00:00:00Z")),
            legalCadasterQueryService: legalService ?? new MockLegalCadasterQueryService(),
            fiscalCadasterQueryService: fiscalService,
            enterpriseCadasterEvidenceService: enterpriseCadasterEvidenceService);
    }

    public static CompareWorkspaceLoadState ReadyState(string caseFolderPath)
    {
        var plan = new CompareWorkingGeometryLoadPlan(
            true,
            "100000674",
            "TR100000674",
            null,
            "transaction_number",
            "100000674",
            "transaction_number = '100000674'",
            Array.Empty<CompareWorkingLayerRequest>(),
            null);

        return new CompareWorkspaceLoadState(
            CompareDocumentLoadState.Loaded("Documents ready.", caseFolderPath),
            CompareWorkingGeometryLoadResult.Ready(
                "Compare working layers loaded into ArcGIS Pro map group 'Compare Review - 100000674' for transaction_number '100000674' (1 polygon feature(s)). Map zoomed to the transaction layer.",
                plan,
                CompareMapIntegrationResult.Loaded(
                    "Compare working layers loaded into ArcGIS Pro map group 'Compare Review - 100000674' for transaction_number '100000674' (1 polygon feature(s)). Map zoomed to the transaction layer.",
                    Array.Empty<string>(),
                    "Compare Review - 100000674",
                    1)));
    }

    public static CompareCaseFixture CreateCaseFolderWithSource()
    {
        return CreateCaseFolderWithSources("survey-plan.pdf");
    }

    public static CompareCaseFixture CreateCaseFolderWithSources(params string[] fileNames)
    {
        var temp = new TempDirectory();
        var sourcePaths = new List<string>();
        foreach (var fileName in fileNames)
        {
            var sourcePath = Path.Combine(temp.Path, fileName);
            File.WriteAllText(sourcePath, $"fake content for {fileName}");
            sourcePaths.Add(sourcePath);
        }

        var store = new CaseFolderStore(
            () => DateTimeOffset.Parse("2026-07-14T00:00:00Z"),
            () => "run-compare");
        var created = store.CreateCase(temp.Path, "TR100000674", "tester");
        if (!created.Success || created.Layout is null)
        {
            throw new InvalidOperationException(created.ErrorMessage);
        }

        var copied = new SourceFileCopyService().CopySourceFiles(created.Layout, sourcePaths, "survey_plan_pdf");
        if (!copied.Success)
        {
            throw new InvalidOperationException(string.Join(" ", copied.Results.Select(result => result.Message)));
        }

        var manifest = ManifestSerializer.Read(created.Layout.ManifestPath);
        ManifestSerializer.Write(created.Layout.ManifestPath, manifest with
        {
            Payload = manifest.Payload with { WorkflowState = WorkflowState.OutputCreated.ToContractValue() }
        });

        return new CompareCaseFixture(temp, store, created.Layout);
    }
}

internal sealed class CompareCaseFixture : IDisposable
{
    private readonly TempDirectory tempDirectory;
    private readonly CaseFolderStore store;

    public CompareCaseFixture(TempDirectory tempDirectory, CaseFolderStore store, CaseFolderLayout layout)
    {
        this.tempDirectory = tempDirectory;
        this.store = store;
        Layout = layout;
    }

    public CaseFolderLayout Layout { get; }

    public CaseFolderReopenResult Reopen()
    {
        return store.ReopenCaseFolder(Layout.RootDirectory);
    }

    public void Dispose()
    {
        tempDirectory.Dispose();
    }
}
