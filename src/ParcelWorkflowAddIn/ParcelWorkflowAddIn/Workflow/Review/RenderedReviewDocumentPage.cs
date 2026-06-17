using System.Windows.Media.Imaging;

namespace ParcelWorkflowAddIn.Workflow.Review;

internal sealed record RenderedReviewDocumentPage(
    string SourcePath,
    int PageIndex,
    int PageCount,
    BitmapSource ImageSource,
    string DocumentKind);
