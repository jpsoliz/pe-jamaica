using System.IO;
using System.Text;
using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.Innola;

public sealed class MockInnolaTransactionDetailService : IInnolaTransactionDetailService
{
    private readonly Dictionary<string, InnolaTransactionDetail> details;
    private readonly Dictionary<string, byte[]> contentByAttachmentId;

    public MockInnolaTransactionDetailService()
        : this(CreateDefaultDetails(), CreateDefaultContent())
    {
    }

    public MockInnolaTransactionDetailService(
        IEnumerable<InnolaTransactionDetail> details,
        IReadOnlyDictionary<string, byte[]> contentByAttachmentId)
    {
        this.details = details.ToDictionary(detail => detail.TaskId, StringComparer.OrdinalIgnoreCase);
        this.contentByAttachmentId = new Dictionary<string, byte[]>(contentByAttachmentId, StringComparer.OrdinalIgnoreCase);
    }

    public Task<InnolaTransactionDetailResult> GetTransactionDetailAsync(
        InnolaSession session,
        SelectedInnolaTransaction selectedTransaction,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return Task.FromResult(InnolaTransactionDetailResult.Failure("Could not load transaction. Try again.", "unauthorized"));
        }

        if (!details.TryGetValue(selectedTransaction.TaskId, out var detail))
        {
            return Task.FromResult(InnolaTransactionDetailResult.Failure("Transaction details were not found.", "not_found"));
        }

        return Task.FromResult(InnolaTransactionDetailResult.Succeeded(detail));
    }

    public Task<InnolaAttachmentContentResult> GetAttachmentContentAsync(
        InnolaSession session,
        InnolaTransactionDetail detail,
        InnolaAttachmentMetadata attachment,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return Task.FromResult(InnolaAttachmentContentResult.Failure("Could not load attachment. Try again.", "unauthorized"));
        }

        return contentByAttachmentId.TryGetValue(attachment.AttachmentId, out var content)
            ? Task.FromResult(InnolaAttachmentContentResult.Succeeded(content))
            : Task.FromResult(InnolaAttachmentContentResult.Failure("Attachment content was not found.", "not_found"));
    }

    public Task<InnolaAttachmentUploadResult> UploadAttachmentAsync(
        InnolaSession session,
        SelectedInnolaTransaction selectedTransaction,
        string fileName,
        string contentType,
        byte[] content,
        string sourceType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return Task.FromResult(InnolaAttachmentUploadResult.Failure("Could not upload attachment. Try again.", "unauthorized"));
        }

        if (!details.TryGetValue(selectedTransaction.TaskId, out var detail))
        {
            return Task.FromResult(InnolaAttachmentUploadResult.Failure("Transaction details were not found.", "not_found"));
        }

        var updatedAttachments = detail.Attachments
            .Where(attachment => !attachment.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var removed in detail.Attachments.Where(attachment => attachment.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
        {
            contentByAttachmentId.Remove(removed.AttachmentId);
        }

        var attachmentId = $"resume-{Guid.NewGuid():N}";
        contentByAttachmentId[attachmentId] = content;
        updatedAttachments.Add(new InnolaAttachmentMetadata(
            attachmentId,
            fileName,
            Path.GetExtension(fileName).ToLowerInvariant(),
            contentType,
            null,
            sourceType,
            content.LongLength,
            null,
            $"mock-attachment:{attachmentId}",
            false));
        details[selectedTransaction.TaskId] = detail with { Attachments = updatedAttachments };
        return Task.FromResult(InnolaAttachmentUploadResult.Succeeded());
    }

    private static IReadOnlyList<InnolaTransactionDetail> CreateDefaultDetails()
    {
        return new[]
        {
            Detail("task-100000004", "100000004", "TR100000004", "Computation Check", "tester", "survey"),
            Detail("task-100000005", "100000005", "TR100000005", "Prepare Rejection Letter", "TR - Integration User", "registration"),
            Detail("task-100000009", "100000009", "TR100000009", "QC of Registration Cases", null, "qc")
        };
    }

    private static InnolaTransactionDetail Detail(
        string taskId,
        string transactionId,
        string transactionNumber,
        string taskName,
        string? assignedUser,
        string? assignedGroup)
    {
        return new InnolaTransactionDetail(
            transactionId,
            transactionNumber,
            taskId,
            taskName,
            "parcel_workflow",
            "parcel_workflow",
            "scenario_b",
            assignedUser,
            assignedGroup,
            null,
            "available",
            new[]
            {
                Attachment("att-computation", "computation.pdf", ".pdf", "application/pdf", SourceRole.ComputationSource, "computation", 64, "sha256:mock-computation"),
                Attachment("att-plan", "plan_map.pdf", ".pdf", "application/pdf", SourceRole.PlanMapReference, "plan", 64, "sha256:mock-plan")
            });
    }

    private static InnolaAttachmentMetadata Attachment(
        string id,
        string fileName,
        string extension,
        string mimeType,
        string role,
        string category,
        long size,
        string checksum)
    {
        return new InnolaAttachmentMetadata(
            id,
            fileName,
            extension,
            mimeType,
            role,
            category,
            size,
            checksum,
            $"mock-attachment:{id}",
            true);
    }

    private static IReadOnlyDictionary<string, byte[]> CreateDefaultContent()
    {
        return new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["att-computation"] = Encoding.UTF8.GetBytes("%PDF-1.4\n% mock computation\n"),
            ["att-plan"] = Encoding.UTF8.GetBytes("%PDF-1.4\n% mock plan map\n")
        };
    }
}
