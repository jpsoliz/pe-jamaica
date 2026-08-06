using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;

namespace ParcelWorkflowAddIn.Innola;

public enum InnolaApiRetryMode
{
    None,
    Safe,
    VerifyBeforeRetry
}

public sealed record InnolaApiOperation(
    string Name,
    InnolaApiRetryMode RetryMode = InnolaApiRetryMode.Safe,
    string? TransactionNumber = null,
    int? MaxAttempts = null,
    TimeSpan? RetryDelay = null)
{
    public int ResolvedMaxAttempts => MaxAttempts ?? (RetryMode == InnolaApiRetryMode.None ? 1 : 3);

    public TimeSpan ResolvedRetryDelay => RetryDelay ?? TimeSpan.FromMilliseconds(75);
}

public static class InnolaApiResilience
{
    public const string LoginRequiredMessage = "Innola connection could not be restored. Please log in again and retry.";

    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient httpClient,
        InnolaApiOperation operation,
        Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(createRequest);

        var attempts = Math.Max(1, operation.ResolvedMaxAttempts);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var request = createRequest();

            try
            {
                var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!ShouldRetry(response.StatusCode, operation, attempt, attempts))
                {
                    if (IsAuthorizationFailure(response.StatusCode))
                    {
                        Debug.WriteLine($"Innola API auth failure. Operation={operation.Name}; Attempt={attempt}; Transaction={operation.TransactionNumber ?? "(none)"}; Status={response.StatusCode}.");
                    }

                    return response;
                }

                Debug.WriteLine($"Retrying Innola request. Operation={operation.Name}; Attempt={attempt}; Transaction={operation.TransactionNumber ?? "(none)"}; Status={response.StatusCode}.");
                response.Dispose();
            }
            catch (Exception exception) when (IsRetryableException(exception, cancellationToken) && attempt < attempts && operation.RetryMode != InnolaApiRetryMode.None)
            {
                lastException = exception;
                Debug.WriteLine($"Retrying Innola request after connection failure. Operation={operation.Name}; Attempt={attempt}; Transaction={operation.TransactionNumber ?? "(none)"}; Error={exception.GetType().Name}.");
            }

            await DelayBeforeRetryAsync(operation, cancellationToken).ConfigureAwait(false);
        }

        if (lastException is not null)
        {
            throw new HttpRequestException(
                $"Innola API operation '{operation.Name}' failed after retry attempts. {lastException.GetType().Name}.",
                lastException);
        }

        using var finalRequest = createRequest();
        return await httpClient.SendAsync(finalRequest, cancellationToken).ConfigureAwait(false);
    }

    public static bool IsRetryableStatus(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
    }

    public static bool IsAuthorizationFailure(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
    }

    public static bool IsRetryableException(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception is HttpRequestException or IOException or TaskCanceledException or TimeoutException;
    }

    public static string CategoryFor(HttpStatusCode statusCode)
    {
        return IsAuthorizationFailure(statusCode)
            ? "session_expired"
            : statusCode.ToString();
    }

    public static string UserMessageFor(HttpStatusCode statusCode, string fallback)
    {
        return IsAuthorizationFailure(statusCode)
            ? LoginRequiredMessage
            : fallback;
    }

    private static bool ShouldRetry(HttpStatusCode statusCode, InnolaApiOperation operation, int attempt, int attempts)
    {
        if (attempt >= attempts || operation.RetryMode == InnolaApiRetryMode.None)
        {
            return false;
        }

        if (operation.RetryMode == InnolaApiRetryMode.VerifyBeforeRetry)
        {
            return false;
        }

        return IsRetryableStatus(statusCode);
    }

    private static async Task DelayBeforeRetryAsync(InnolaApiOperation operation, CancellationToken cancellationToken)
    {
        var delay = operation.ResolvedRetryDelay;
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }
}
