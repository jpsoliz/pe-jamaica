namespace ParcelWorkflowAddIn.Enterprise.PortalAuth;

public interface IPortalAuthProvider
{
    Task<PortalAuthResult> GetTokenAsync(
        PortalAuthRequest request,
        CancellationToken cancellationToken = default);
}
