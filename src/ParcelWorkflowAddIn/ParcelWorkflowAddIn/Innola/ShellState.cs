using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.Innola;

internal static class ShellState
{
    private static readonly InnolaTransactionSettings Settings = InnolaTransactionSettings.Load();

    public static InnolaSessionManager Session { get; } = new(new InnolaAuthService());

    public static IInnolaTransactionService Transactions { get; } = CreateTransactionService();

    public static IInnolaTransactionDetailService TransactionDetails { get; } = CreateTransactionDetailService();

    public static InnolaTransactionLoadService TransactionLoader { get; } = new(
        Session,
        TransactionDetails,
        new CaseFolderStore(),
        new AttachmentSourceFileWriter(),
        new SourceInputProfileDetector(),
        () => Settings.CaseFolderOutputRoot);

    public static string TransactionProcessStep { get; } = Settings.ProcessStep;

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
}
