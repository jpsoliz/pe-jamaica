using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaTransactionLoadResult(
    bool Success,
    CaseFolderLayout? Layout,
    DetectedSourceInputProfile? DetectedProfile,
    string? StatusMessage,
    string? ErrorMessage)
{
    public static InnolaTransactionLoadResult Succeeded(
        CaseFolderLayout layout,
        DetectedSourceInputProfile? detectedProfile,
        string statusMessage)
    {
        return new InnolaTransactionLoadResult(true, layout, detectedProfile, statusMessage, null);
    }

    public static InnolaTransactionLoadResult Failure(string errorMessage)
    {
        return new InnolaTransactionLoadResult(false, null, null, null, errorMessage);
    }
}
