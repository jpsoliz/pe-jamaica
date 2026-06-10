namespace ParcelWorkflowAddIn.Innola;

public sealed class MockInnolaTransactionLifecycleService : IInnolaTransactionLifecycleService
{
    private readonly Dictionary<string, string> owners = new(StringComparer.OrdinalIgnoreCase);

    public MockInnolaTransactionLifecycleService()
        : this(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["task-owned-by-other"] = "other.user"
        })
    {
    }

    public MockInnolaTransactionLifecycleService(IReadOnlyDictionary<string, string> initialOwners)
    {
        foreach (var owner in initialOwners)
        {
            owners[owner.Key] = owner.Value;
        }
    }

    public Task<InnolaTransactionLifecycleResult> ClaimAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
    {
        if (owners.TryGetValue(request.Transaction.TaskId, out var owner)
            && !owner.Equals(request.Session.User.Username, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(InnolaTransactionLifecycleResult.Failure(
                "Transaction is already in progress by another user.",
                "ownership_conflict"));
        }

        owners[request.Transaction.TaskId] = request.Session.User.Username;
        return Task.FromResult(InnolaTransactionLifecycleResult.Succeeded(
            "in_progress",
            request.Session.User.Username,
            request.Session.User.DisplayName,
            "Transaction is in progress."));
    }

    public Task<InnolaTransactionLifecycleResult> SaveProgressAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsOwnedByCurrentUser(request))
        {
            return Task.FromResult(InnolaTransactionLifecycleResult.Failure(
                "Only the user who started the transaction can save progress.",
                "ownership_conflict"));
        }

        return Task.FromResult(InnolaTransactionLifecycleResult.Succeeded(
            "in_progress",
            request.Session.User.Username,
            request.Session.User.DisplayName,
            "Progress saved."));
    }

    public Task<InnolaTransactionLifecycleResult> CompleteAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsOwnedByCurrentUser(request))
        {
            return Task.FromResult(InnolaTransactionLifecycleResult.Failure(
                "Only the user who started the transaction can complete it.",
                "ownership_conflict"));
        }

        owners.Remove(request.Transaction.TaskId);
        return Task.FromResult(InnolaTransactionLifecycleResult.Succeeded(
            "completed",
            request.Session.User.Username,
            request.Session.User.DisplayName,
            "Transaction completed."));
    }

    private bool IsOwnedByCurrentUser(InnolaTransactionLifecycleRequest request)
    {
        return owners.TryGetValue(request.Transaction.TaskId, out var owner)
            && owner.Equals(request.Session.User.Username, StringComparison.OrdinalIgnoreCase);
    }
}
