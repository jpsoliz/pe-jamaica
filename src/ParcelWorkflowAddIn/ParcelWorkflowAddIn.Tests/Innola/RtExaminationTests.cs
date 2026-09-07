using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.RtExamination;

namespace ParcelWorkflowAddIn.Tests.Innola;

internal static class RtExaminationTests
{
    public static void SettingsDefaultsExposeRtExaminationStage()
    {
        var settings = InnolaTransactionSettings.Default.RtExamination;

        TestAssert.True(settings.Enabled, "RT Examination should be enabled by default.");
        TestAssert.Equal("In RT Examination", settings.StageName, "Default RT stage mismatch.");
        TestAssert.Equal("RT Examination", settings.SubworkflowName, "Default RT subworkflow mismatch.");
        TestAssert.Equal("transaction_number", settings.WorkingReviewPeNumberField, "Default working_review transaction field mismatch.");
    }

    public static void StageRouterRecognizesRtExaminationIndependentlyOfTransactionType()
    {
        var route = ParcelWorkflowStageRouter.Resolve(
            "In RT Examination",
            new[] { "Compute Survey Plan" },
            new[] { "Compare Survey Plan" },
            RtExaminationSettings.Default);

        TestAssert.Equal(ParcelWorkflowStageRoute.RtExamination, route, "In RT Examination should route to the RT workspace.");
    }

    public static void PartyRolesAreConstrainedForRtReview()
    {
        var roles = RtExaminationPartyRow.AllowedRoles;

        TestAssert.True(roles.Contains("Owner"), "Owner role missing.");
        TestAssert.True(roles.Contains("Occupier"), "Occupier role missing.");
        TestAssert.False(RtExaminationPartyRow.IsAllowedRole("Applicant"), "Unexpected Applicant role should not be allowed.");
    }

    public static void SpatialUnitEditableFieldsExcludeGeometry()
    {
        TestAssert.True(RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute("landValNumber"), "landValNumber should be editable.");
        TestAssert.True(RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute("examNumber"), "examNumber should be editable.");
        TestAssert.False(RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute("geometry"), "geometry must not be editable.");
        TestAssert.False(RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute("coordinates"), "coordinates must not be editable.");
        TestAssert.False(RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute("bfsMinus"), "boundary fields must not be editable.");
    }


    public static void WindowXamlExposesReviewTabsEditableColumnsAndActions()
    {
        var source = File.ReadAllText(FindSourceFile("RtExaminationWindow.xaml"));

        foreach (var expected in new[]
        {
            "Header=\"Context\"",
            "Header=\"Neighbors / Parties\"",
            "Header=\"Spatial Units\"",
            "Header=\"Plan Check\"",
            "Header=\"Sources / Map Evidence\"",
            "LoadLinkedPeDataCommand",
            "SaveCommand",
            "Content=\"Save\"",
            "Header=\"Role\"",
            "Header=\"Address\"",
            "Header=\"LandVal No.\"",
            "Header=\"Exam No.\"",
            "Binding=\"{Binding ReviewedValue, Mode=TwoWay"
        })
        {
            TestAssert.True(source.Contains(expected, StringComparison.Ordinal), $"RT Examination window is missing expected surface: {expected}.");
        }

        TestAssert.True(
            source.Contains("ComboBox ItemsSource=\"{Binding AllowedRoles}\"", StringComparison.Ordinal),
            "RT role editing should bind the combo list from each editable party row.");
    }

    private static string FindSourceFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "ParcelWorkflowAddIn",
                "ParcelWorkflowAddIn",
                fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {fileName} from the test output directory.");
    }

    public static void DuplicatePartyRowsUseDeterministicRtKey()
    {
        var first = new RtExaminationPartyRow("Owner", "A Brown", "1 King St", "1158", "604", "7", "LV-1", "EX-1");
        var duplicate = new RtExaminationPartyRow("owner", " A Brown ", "1 King St", "1158", "604", "7", "LV-1", "EX-1");
        var different = first with { Folio = "605" };

        TestAssert.Equal(first.DeduplicationKey, duplicate.DeduplicationKey, "Equivalent RT party rows should have the same dedupe key.");
        TestAssert.True(!string.Equals(first.DeduplicationKey, different.DeduplicationKey, StringComparison.Ordinal), "Different RT party rows should not collapse.");
    }
    public static async Task SaveKeepsLinkedRtTransactionOpenUntilComplete()
    {
        var transaction = new SelectedInnolaTransaction(
            "task-rt-100000854",
            "tx-rt-100000854",
            "100000854",
            "In RT Examination",
            "parcel_workflow",
            DateTimeOffset.UtcNow,
            TransactionType: "First Registration");
        var loadService = new CapturingRtLoadService();
        var writeback = new CapturingRtWritebackService();
        var refreshed = false;
        var closeObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new RtExaminationViewModel(
            transaction,
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            loadService,
            writeback,
            _ => true,
            _ => { },
            () => refreshed = true);
        viewModel.RequestClose += (_, _) => closeObserved.TrySetResult();

        await viewModel.LoadAsync();
        TestAssert.True(!viewModel.CanSave, "Loaded RT Examination should not enable Save before changes.");
        viewModel.Observations = "Updated review";
        TestAssert.True(viewModel.CanSave, "Changed RT Examination should enable Save.");

        viewModel.SaveCommand.Execute(null);
        await Task.Delay(100);

        TestAssert.True(!closeObserved.Task.IsCompleted, "Save must keep the RT workspace open until the user chooses Complete.");
        TestAssert.True(!viewModel.CanSave, "Successful Save should disable Save until another change.");
        TestAssert.Equal("Close", viewModel.CloseActionText, "Successful Save should change Cancel to Close.");
        TestAssert.True(writeback.Request is not null, "Save should call RT writeback service.");
        TestAssert.True(!writeback.Request!.CompleteAfterSave, "Save must not complete the RT transaction.");
        TestAssert.Equal("tx-rt-100000854", writeback.Request.Transaction.TransactionId, "Save should write back to the linked/current RT transaction object.");
        TestAssert.True(!loadService.CleanupCalled, "Save must not clean up RT map layers.");
        TestAssert.True(!refreshed, "Save must not refresh Innola transactions before completion.");

        viewModel.CompleteCommand.Execute(null);
        var completed = await Task.WhenAny(closeObserved.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        TestAssert.True(ReferenceEquals(completed, closeObserved.Task), "Complete should close the RT workspace.");
        TestAssert.True(writeback.Request!.CompleteAfterSave, "Complete should be the final RT action.");
        TestAssert.True(loadService.CleanupCalled, "Successful completion should clean up RT map layers.");
        TestAssert.True(refreshed, "Successful completion should refresh Innola transactions.");
    }

    private sealed class CapturingRtLoadService : IRtExaminationLoadService
    {
        public bool CleanupCalled { get; private set; }

        public Task<RtExaminationLoadResult> LoadAsync(SelectedInnolaTransaction transaction, string caseFolderPath, CancellationToken cancellationToken = default)
        {
            var context = new RtExaminationContextDocument(
                "rt_examination_context_v1",
                DateTimeOffset.UtcNow,
                transaction.TransactionId,
                transaction.TransactionNumber,
                transaction.TaskId,
                "plan-current",
                "plan-current-uid",
                transaction.TransactionId,
                transaction.TransactionNumber,
                "100000854",
                "tx-pe-100000854",
                "100000854",
                1,
                1,
                "100000854",
                Array.Empty<string>());
            return Task.FromResult(RtExaminationLoadResult.Succeeded(
                "Loaded linked transaction data.",
                context,
                Array.Empty<RtExaminationPartyRow>(),
                Array.Empty<RtExaminationSpatialUnitAttributeViewModel>(),
                new[] { "working_review: transaction_number = 100000854" },
                new[] { "RT 100000854 - PE 100000854" }));
        }

        public Task CleanupAsync(IReadOnlyList<string> loadedMapGroups, CancellationToken cancellationToken = default)
        {
            CleanupCalled = loadedMapGroups.Count == 1 && loadedMapGroups[0] == "RT 100000854 - PE 100000854";
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingRtWritebackService : IRtExaminationWritebackService
    {
        public RtExaminationSaveRequest? Request { get; private set; }

        public Task<RtExaminationSaveResult> SaveAsync(RtExaminationSaveRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(RtExaminationSaveResult.Succeeded("RT Examination data saved and task completed."));
        }
    }
}




