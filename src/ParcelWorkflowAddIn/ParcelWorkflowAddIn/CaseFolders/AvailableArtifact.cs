namespace ParcelWorkflowAddIn.CaseFolders;

public sealed record AvailableArtifact(
    string ArtifactName,
    string Path,
    string? ArtifactType = null,
    bool IsInternalGenerated = false);
