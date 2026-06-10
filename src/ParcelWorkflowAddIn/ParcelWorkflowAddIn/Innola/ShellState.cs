using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.Innola;

internal static class ShellState
{
    private static readonly InnolaTransactionSettings Settings = InnolaTransactionSettings.Load();

    public static InnolaSessionManager Session { get; } = new(CreateAuthService());

    public static IInnolaTransactionService Transactions { get; } = CreateTransactionService();

    public static IInnolaTransactionDetailService TransactionDetails { get; } = CreateTransactionDetailService();

    public static IInnolaTransactionLifecycleService TransactionLifecycle { get; } = CreateTransactionLifecycleService();

    public static ITransactionCompletionReadinessService CompletionReadiness { get; } = new DefaultTransactionCompletionReadinessService();

    public static WorkflowLifecycleAuditService LifecycleAudit { get; } = new();

    public static InnolaTransactionLoadService TransactionLoader { get; } = new(
        Session,
        TransactionDetails,
        new CaseFolderStore(),
        new AttachmentSourceFileWriter(),
        new SourceInputProfileDetector(),
        () => Settings.CaseFolderOutputRoot);

    public static InnolaTransactionLifecycleCoordinator LifecycleCoordinator { get; } = new(
        Session,
        TransactionLifecycle,
        CompletionReadiness,
        LifecycleAudit);

    public static string TransactionProcessStep { get; } = Settings.ProcessStep;

    private static IInnolaAuthService CreateAuthService()
    {
        return Settings.Mode.Equals("mock", StringComparison.OrdinalIgnoreCase)
            ? new MockInnolaAuthService()
            : new InnolaAuthService();
    }

    private static IInnolaTransactionService CreateTransactionService()
    {
        return Settings.Mode.Equals("mock", StringComparison.OrdinalIgnoreCase)
            ? new MockInnolaTransactionService()
            : new InnolaTransactionService();
    }

    private static IInnolaTransactionDetailService CreateTransactionDetailService()
    {
        return Settings.Mode.Equals("mock", StringComparison.OrdinalIgnoreCase)
            ? new MockInnolaTransactionDetailService()
            : new InnolaTransactionDetailService();
    }

    private static IInnolaTransactionLifecycleService CreateTransactionLifecycleService()
    {
        return Settings.Mode.Equals("mock", StringComparison.OrdinalIgnoreCase)
            ? new MockInnolaTransactionLifecycleService()
            : new InnolaTransactionLifecycleService();
    }
}
