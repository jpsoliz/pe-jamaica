namespace ParcelWorkflowAddIn.Enterprise.PortalAuth;

public sealed record PortalAuthRequest(
    string PortalUrl,
    string? ServiceUrl,
    string Operation,
    string? LayerRole = null);
