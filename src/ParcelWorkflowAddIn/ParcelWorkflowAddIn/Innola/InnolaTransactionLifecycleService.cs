using System.Net.Http;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaTransactionLifecycleService : IInnolaTransactionLifecycleService
{
    private readonly HttpClient httpClient;

    public InnolaTransactionLifecycleService()
        : this(new HttpClient())
    {
    }

    public InnolaTransactionLifecycleService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<InnolaTransactionLifecycleResult> ClaimAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = CreateRequest(
                HttpMethod.Post,
                request.Session,
                $"{InnolaSettings.V4RestPath}workflow/tasks/{Uri.EscapeDataString(request.Transaction.TaskId)}/start");
            using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Failure("Could not start transaction. Try again.", response.StatusCode.ToString());
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var businessFailure = TryReadSuccessEnvelope(responseBody);
            if (businessFailure is { Success: false })
            {
                return Failure(businessFailure.Message ?? "Could not start transaction. Try again.", "business_rule");
            }

            var owner = request.Session.User.Username;
            var ownerDisplay = request.Session.User.DisplayName;
            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                using var document = JsonDocument.Parse(responseBody);
                var task = ResolveTaskRoot(document.RootElement);
                if (task.HasValue)
                {
                    owner = InnolaHttp.ReadString(task.Value, "assignee", "ownerUser") ?? owner;
                    ownerDisplay = owner;
                }
            }

            return InnolaTransactionLifecycleResult.Succeeded("in_progress", owner, ownerDisplay, "Transaction is in progress.");
        }
        catch (Exception exception) when (IsExpectedAdapterFailure(exception))
        {
            return Failure("Could not start transaction. Try again.", exception.GetType().Name);
        }
    }

    public Task<InnolaTransactionLifecycleResult> SaveProgressAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(InnolaTransactionLifecycleResult.Succeeded(
            "in_progress",
            request.Session.User.Username,
            request.Session.User.DisplayName,
            "Progress saved. Transaction remains in progress."));
    }

    public async Task<InnolaTransactionLifecycleResult> CompleteAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var transition = await GetCompletionTransitionAsync(request, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(transition))
            {
                return Failure("Completion transition unavailable.", "transition_unavailable");
            }

            using var httpRequest = CreateRequest(
                HttpMethod.Post,
                request.Session,
                $"{InnolaSettings.V4RestPath}workflow/tasks/{Uri.EscapeDataString(request.Transaction.TaskId)}/complete?transition={Uri.EscapeDataString(transition)}");
            using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Failure("Could not complete transaction. Try again.", response.StatusCode.ToString());
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var businessFailure = TryReadSuccessEnvelope(responseBody);
            if (businessFailure is { Success: false })
            {
                return Failure(businessFailure.Message ?? "Could not complete transaction. Try again.", "business_rule");
            }

            return InnolaTransactionLifecycleResult.Succeeded(
                "completed",
                request.Session.User.Username,
                request.Session.User.DisplayName,
                "Transaction completed.");
        }
        catch (Exception exception) when (IsExpectedAdapterFailure(exception))
        {
            return Failure("Could not complete transaction. Try again.", exception.GetType().Name);
        }
    }

    private async Task<string?> GetCompletionTransitionAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = CreateRequest(
            HttpMethod.Get,
            request.Session,
            $"{InnolaSettings.V4RestPath}workflow/tasks/{Uri.EscapeDataString(request.Transaction.TaskId)}/transitions");
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(responseBody);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        JsonElement? fallback = null;
        foreach (var transition in document.RootElement.EnumerateArray())
        {
            if (transition.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            fallback ??= transition;
            if (transition.TryGetProperty("isDefault", out var isDefault) && isDefault.ValueKind == JsonValueKind.True)
            {
                return InnolaHttp.ReadString(transition, "transitionId", "id", "name");
            }
        }

        return fallback.HasValue ? InnolaHttp.ReadString(fallback.Value, "transitionId", "id", "name") : null;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, InnolaSession session, string relativePath)
    {
        var request = new HttpRequestMessage(method, InnolaHttp.BuildUri(session.ServerUrl, relativePath));
        InnolaHttp.ApplyAuthHeaders(request, session.AccessToken);
        return request;
    }

    private static JsonElement? ResolveTaskRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (root.TryGetProperty("task", out var task) && task.ValueKind == JsonValueKind.Object)
        {
            return task;
        }

        return root;
    }

    private static SuccessEnvelope? TryReadSuccessEnvelope(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        using var document = JsonDocument.Parse(responseBody);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("success", out var success)
            || success.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return null;
        }

        return new SuccessEnvelope(
            success.GetBoolean(),
            InnolaHttp.ReadString(document.RootElement, "message", "error", "detail"));
    }

    private static InnolaTransactionLifecycleResult Failure(string message, string errorCategory)
    {
        return InnolaTransactionLifecycleResult.Failure(
            InnolaHttp.SafeRetryMessage(message, "Transaction lifecycle action failed. Try again."),
            errorCategory);
    }

    private static bool IsExpectedAdapterFailure(Exception exception)
    {
        return exception is HttpRequestException
            or JsonException
            or TaskCanceledException
            or InvalidOperationException
            or UriFormatException;
    }

    private sealed record SuccessEnvelope(bool Success, string? Message);
}
