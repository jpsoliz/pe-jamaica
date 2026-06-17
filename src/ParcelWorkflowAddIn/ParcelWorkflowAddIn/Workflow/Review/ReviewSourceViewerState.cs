namespace ParcelWorkflowAddIn.Workflow.Review;

internal enum ReviewSourceViewerMode
{
    None,
    Image,
    Pdf,
    PdfExternal,
    Missing,
    Unsupported,
    Error
}

internal sealed record ReviewSourceViewerState(
    ReviewSourceViewerMode Mode,
    string Title,
    string RoleLabel,
    string DisplayPath,
    string ModeLabel,
    string LoadState,
    string Guidance,
    string FallbackMessage,
    string? FullPath)
{
    public bool UsesImage => Mode == ReviewSourceViewerMode.Image;

    public bool UsesBrowser => Mode == ReviewSourceViewerMode.Pdf;

    public bool CanRenderEmbedded => UsesImage || UsesBrowser;
}
