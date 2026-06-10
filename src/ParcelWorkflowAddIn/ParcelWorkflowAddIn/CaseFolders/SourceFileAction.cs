namespace ParcelWorkflowAddIn.CaseFolders;

public enum SourceFileAction
{
    Open,
    Reveal,
    RouteToMap
}

public static class SourceFileActionExtensions
{
    public static string ToContractValue(this SourceFileAction action)
    {
        return action switch
        {
            SourceFileAction.Open => "open",
            SourceFileAction.Reveal => "reveal",
            SourceFileAction.RouteToMap => "route_to_map",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown source file action.")
        };
    }
}
