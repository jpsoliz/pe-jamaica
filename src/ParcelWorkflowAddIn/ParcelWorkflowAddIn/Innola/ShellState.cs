using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Intake;
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

    public static ITransactionCompletionReadinessService CompletionReadiness { get; } = new DefaultTransactionCompletionReadinessService();

    public static WorkflowLifecycleAuditService LifecycleAudit { get; } = new();

    public static CaseResumePackageService ResumePackages { get; } = new();

    public static InnolaTransactionLoadService TransactionLoader { get; } = new(
        Session,
        TransactionDetails,
        new CaseFolderStore(),
        new AttachmentSourceFileWriter(),
        new SourceInputProfileDetector(),
        new WorkflowRuleResolver(),
        WorkflowRuleSettingsLoader.Load,
        ResumePackages,
        () => Settings.CaseFolderOutputRoot);

    public static InnolaTransactionLifecycleCoordinator LifecycleCoordinator { get; } = new(
        Session,
        TransactionDetails,
        TransactionLifecycle,
        CompletionReadiness,
        LifecycleAudit,
        ResumePackages);

    public static string TransactionProcessStep { get; } = Settings.ProcessStep;

    public static string ConfiguredServerUrl { get; } = Settings.ServerUrl;

    public static string TransactionMode { get; } = Settings.Mode;

    public static string AttachmentUploadRoute { get; } = Settings.AttachmentUploadRoute;

    public static string AttachmentUploadBindingMode { get; } = Settings.AttachmentUploadBindingMode;

    public static string ResumeAttachmentSourceType { get; } = Settings.ResumeAttachmentSourceType;

    public static string CompletedAttachmentSourceType { get; } = Settings.CompletedAttachmentSourceType;

    public static string ClientCertificateStatus => Settings.ClientCertificate.Enabled
        ? $"Client certificate: {Settings.ClientCertificate.SubjectName ?? Settings.ClientCertificate.Thumbprint ?? "configured"}"
        : "Client certificate: disabled";

    private static IInnolaAuthService CreateAuthService()
    {
        return Settings.Mode.Equals("mock", StringComparison.OrdinalIgnoreCase)
            ? new MockInnolaAuthService()
            : new InnolaAuthService(SharedInnolaHttpClient);
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
}
