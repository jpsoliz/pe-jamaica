namespace ParcelWorkflowAddIn.CaseFolders;

public sealed class CaseFolderCreationResult
{
    private CaseFolderCreationResult(bool success, CaseFolderLayout? layout, string? errorMessage)
    {
        Success = success;
        Layout = layout;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }

    public CaseFolderLayout? Layout { get; }

    public string? ErrorMessage { get; }

    public static CaseFolderCreationResult Created(CaseFolderLayout layout)
    {
        return new CaseFolderCreationResult(true, layout, null);
    }

    public static CaseFolderCreationResult Failed(string errorMessage)
    {
        return new CaseFolderCreationResult(false, null, errorMessage);
    }
}
