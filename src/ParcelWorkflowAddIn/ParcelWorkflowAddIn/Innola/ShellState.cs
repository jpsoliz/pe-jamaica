using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Workflow.Maps;
using ParcelWorkflowAddIn.WorkflowRules;
using System.Net.Http;

namespace ParcelWorkflowAddIn.Innola;

internal static class ShellState
{
    private static readonly InnolaTransactionSettings Settings = InnolaTransactionSettings.Load();
    private static readonly HttpClient SharedInnolaHttpClient = InnolaHttpClientFactory.Create(Settings.ClientCertificate);

    public static InnolaSessionManager Session { get; } = new(CreateAuthService());

    public static IInnolaTransactionService Transactions { get; } = CreateTransactionService();

    public static IInnolaTransactionDetailService TransactionDetails { get; } = CreateTransactionDetailService();

    public static IInnolaTransactionLifecycleService TransactionLifecycle { get; } = CreateTransactionLifecycleService();

    public static IInnolaSpatialUnitService SpatialUnits { get; } = CreateSpatialUnitService();

    public static IInnolaPlanCheckService PlanChecks { get; } = CreatePlanCheckService();

    public static ITransactionCompletionReadinessService CompletionReadiness { get; } = new DefaultTransactionCompletionReadinessService();

    public static WorkflowLifecycleAuditService LifecycleAudit { get; } = new();

    public static CaseResumePackageService ResumePackages { get; } = new();

    public static WorkingMapPreloadService WorkingMapPreloader { get; } = new(
        new ArcGisWorkingMapPreparationService(),
        () => Settings.WorkingMap);

    public static InnolaTransactionLoadService TransactionLoader { get; } = new(
        Session,
        TransactionDetails,
        new CaseFolderStore(),
        new AttachmentSourceFileWriter(),
        new SourceInputProfileDetector(),
        new WorkflowRuleResolver(),
        WorkflowRuleSettingsLoader.Load,
        ResumePackages,
        () => Settings.CaseFolderOutputRoot,
        workingMapPreparationService: new ArcGisWorkingMapPreparationService());

    public static InnolaTransactionLoadService CompareTransactionLoader { get; } = new(
        Session,
        TransactionDetails,
        new CaseFolderStore(),
        new AttachmentSourceFileWriter(),
        new SourceInputProfileDetector(),
        new WorkflowRuleResolver(),
        WorkflowRuleSettingsLoader.Load,
        ResumePackages,
        () => Settings.CaseFolderOutputRoot,
        workingMapPreparationService: NoOpWorkingMapPreparationService.Instance);

    public static InnolaTransactionLifecycleCoordinator LifecycleCoordinator { get; } = new(
        Session,
        TransactionDetails,
        TransactionLifecycle,
        SpatialUnits,
        CompletionReadiness,
        LifecycleAudit,
        ResumePackages,
        planCheckService: PlanChecks);

    public static CompareWorkspaceLoadService CompareWorkspaceLoader { get; } = new(
        Session,
        CompareTransactionLoader,
        new CompareWorkingGeometryService(
            InnolaTransactionSettings.Load,
            new DeferredCompareMapIntegrationService()),
        new CompareWorkingGeometryService(
            InnolaTransactionSettings.Load,
            new ArcGisCompareMapIntegrationService()));

    public static string TransactionProcessStep { get; } = Settings.ProcessStep;

    public static string ConfiguredServerUrl { get; } = Settings.ServerUrl;

    public static string TransactionMode { get; } = Settings.Mode;

    public static IReadOnlyList<string> SupportedTransactionTypes { get; } = Settings.SupportedTransactionTypes;

    public static string? SupportedTransactionTypesWarning { get; } = Settings.SupportedTransactionTypesWarning;

    public static IReadOnlyList<string> ComputeWorkflowStages { get; } = Settings.ComputeWorkflowStages;

    public static string? ComputeWorkflowStagesWarning { get; } = Settings.ComputeWorkflowStagesWarning;

    public static IReadOnlyList<string> CompareWorkflowStages { get; } = Settings.CompareWorkflowStages;

    public static string? CompareWorkflowStagesWarning { get; } = Settings.CompareWorkflowStagesWarning;

    public static bool CanOpenComputeWorkflow => Session.CanOpenParcelWorkflow && IsSelectedTransactionComputeWorkflow;

    public static bool IsSelectedTransactionComputeWorkflow => ParcelWorkflowStageRouter.IsComputeStage(
        Session.SelectedTransaction?.TaskName,
        ComputeWorkflowStages,
        CompareWorkflowStages);

    public static string AttachmentUploadRoute { get; } = Settings.AttachmentUploadRoute;

    public static string AttachmentUploadBindingMode { get; } = Settings.AttachmentUploadBindingMode;

    public static string AttachmentUploadMode { get; } = Settings.AttachmentUploadMode;

    public static string AttachmentUploadAuthMode { get; } = Settings.AttachmentUploadAuthMode;

    public static string ResumeAttachmentSourceType { get; } = Settings.ResumeAttachmentSourceType;

    public static string CompletedAttachmentSourceType { get; } = Settings.CompletedAttachmentSourceType;

    public static string ResumeAttachmentRegisteredType { get; } = Settings.ResumeAttachmentRegisteredType;

    public static string CompletedAttachmentRegisteredType { get; } = Settings.CompletedAttachmentRegisteredType;

    public static string? AttachmentRegisteredSpatialUnitId { get; } = Settings.AttachmentRegisteredSpatialUnitId;

    public static string ClientCertificateStatus => Settings.ClientCertificate.Enabled
        ? $"Client certificate: {Settings.ClientCertificate.SubjectName ?? Settings.ClientCertificate.Thumbprint ?? "configured"}"
        : "Client certificate: disabled";

    public static void StartWorkingMapPreloadAfterLogin()
    {
        WorkingMapPreloader.StartWorkingMapPreloadAfterLogin();
    }

    public static void OpenCompareWorkspace(string transactionNumber)
    {
        OpenCompareWorkspace(transactionNumber, null);
    }

    public static void OpenCompareWorkspace(string transactionNumber, ICompareTaskLifecycleService? taskLifecycleService)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => OpenCompareWorkspace(transactionNumber, taskLifecycleService));
            return;
        }

        try
        {
            OpenCompareWorkspaceCore(transactionNumber, taskLifecycleService);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                $"Compare workspace could not be opened for transaction {transactionNumber}. {exception.Message}",
                "Compare Workspace",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private static void OpenCompareWorkspaceCore(string transactionNumber, ICompareTaskLifecycleService? taskLifecycleService)
    {
        if (Session.SelectedTransaction is null
            || !Session.SelectedTransaction.TransactionNumber.Equals(transactionNumber, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var viewModel = new CompareWorkspaceViewModel(
            Session.SelectedTransaction,
            CompareWorkspaceLoader,
            legalCadasterQueryService: CompareCadasterQueryServiceFactory.CreateLegal(
                Settings,
                () => Session.CurrentSession,
                SharedInnolaHttpClient),
            fiscalCadasterQueryService: CompareCadasterQueryServiceFactory.CreateFiscal(Settings),
            enterpriseCadasterEvidenceService: new CompareEnterpriseCadasterEvidenceService(InnolaTransactionSettings.Load),
            taskLifecycleService: taskLifecycleService,
            reportAttachmentService: new CompareReportAttachmentService(
                () => Session.CurrentSession,
                CreateTransactionDetailService()),
            mapIntegrationService: new ArcGisCompareMapIntegrationService(),
            promptService: new MessageBoxCompareWorkspacePromptService(),
            reviewerId: Session.CurrentUser?.Username,
            reviewerDisplayName: Session.CurrentUser?.DisplayName);
        CompareWorkspaceWindow.ShowOrActivate(viewModel);
    }

    private static IInnolaAuthService CreateAuthService()
    {
        return Settings.Mode.Equals("mock", StringComparison.OrdinalIgnoreCase)
            ? new MockInnolaAuthService()
            : new InnolaAuthService(
                SharedInnolaHttpClient,
                new InnolaLoginTraceService(Settings.CaseFolderOutputRoot),
                Settings.ClientCertificate);
    }

    private static IInnolaTransactionService CreateTransactionService()
    {
        return Settings.Mode.Equals("mock", StringComparison.OrdinalIgnoreCase)
            ? new MockInnolaTransactionService()
            : new InnolaTransactionService(SharedInnolaHttpClient);
    }

    private static IInnolaTransactionDetailService CreateTransactionDetailService()
    {
        return Settings.Mode.Equals("mock", StringComparison.OrdinalIgnoreCase)
            ? new MockInnolaTransactionDetailService()
            : new InnolaTransactionDetailService(SharedInnolaHttpClient);
    }

    private static IInnolaTransactionLifecycleService CreateTransactionLifecycleService()
    {
        return Settings.Mode.Equals("mock", StringComparison.OrdinalIgnoreCase)
            ? new MockInnolaTransactionLifecycleService()
            : new InnolaTransactionLifecycleService(SharedInnolaHttpClient);
    }

    private static IInnolaSpatialUnitService CreateSpatialUnitService()
    {
        return Settings.Mode.Equals("mock", StringComparison.OrdinalIgnoreCase)
            ? new MockInnolaSpatialUnitService()
            : new InnolaSpatialUnitService(
                SharedInnolaHttpClient,
                (_, cancellationToken) => Session.RefreshCurrentSessionAsync(cancellationToken));
    }

    private static IInnolaPlanCheckService CreatePlanCheckService()
    {
        return Settings.Mode.Equals("mock", StringComparison.OrdinalIgnoreCase)
            ? new MockInnolaPlanCheckService()
            : new InnolaPlanCheckService(SharedInnolaHttpClient);
    }

    private sealed class DeferredCompareMapIntegrationService : ICompareMapIntegrationService
    {
        public Task<CompareMapIntegrationResult> AddTransactionGeometryToActiveMapAsync(
            CompareWorkingGeometryLoadPlan plan,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CompareMapIntegrationResult.MapUnavailable(
                "Compare map layers have not been loaded yet. Use Refresh Map after the workspace opens."));
        }

        public Task<CompareMapCleanupResult> RemoveTransactionGeometryFromActiveMapAsync(
            string groupLayerName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CompareMapCleanupResult.Skipped(
                "No Compare map layers were loaded during workspace startup."));
        }
    }
}
