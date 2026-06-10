namespace ParcelWorkflowAddIn.Innola;

public interface IInnolaAuthService
{
    InnolaSession? CurrentSession { get; }

    Task<InnolaLoginResult> LoginAsync(string serverUrl, string username, string password, CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);
}
