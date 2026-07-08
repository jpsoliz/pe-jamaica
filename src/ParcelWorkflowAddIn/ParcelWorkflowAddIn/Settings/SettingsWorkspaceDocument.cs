using ParcelWorkflowAddIn.Preflight;

namespace ParcelWorkflowAddIn.Settings;

public sealed class SettingsWorkspaceDocument
{
    public static IReadOnlyList<string> TabNames { get; } = new[]
    {
        "General",
        "AI Toolset",
        "Innola Integration",
        "Structure Rules",
        "Spatial Workspace",
        "Enterprise Admin"
    };

    public string SettingsPath { get; init; } = string.Empty;
    public string PreflightRulesPath { get; init; } = string.Empty;
    public string? SettingsWarning { get; set; }
    public string? PreflightRulesWarning { get; set; }

    public string ArcGisProSdkLane { get; set; } = string.Empty;
    public string TargetFramework { get; set; } = string.Empty;
    public string CaseFolderOutputRoot { get; set; } = string.Empty;
    public string ArcGisPythonExecutable { get; set; } = string.Empty;
    public string OutputAdapterScriptPath { get; set; } = string.Empty;
    public string ValidationAdapterScriptPath { get; set; } = string.Empty;
    public string ValidationRulesPath { get; set; } = string.Empty;
    public string OutputTemplateProjectPath { get; set; } = string.Empty;
    public string OutputTemplateGdbPath { get; set; } = string.Empty;
    public int OutputAdapterTimeoutSeconds { get; set; }

    public string OcrEngine { get; set; } = string.Empty;
    public bool OpenAiEnabled { get; set; }
    public string OpenAiExtractionProfile { get; set; } = string.Empty;
    public string OpenAiModel { get; set; } = string.Empty;
    public string OpenAiApiKeyEnvironmentVariable { get; set; } = string.Empty;

    public string InnolaServerUrl { get; set; } = string.Empty;
    public string InnolaTransactionMode { get; set; } = string.Empty;
    public string InnolaProcessStep { get; set; } = string.Empty;
    public List<string> SupportedTransactionTypes { get; set; } = new();
    public List<string> ComputeWorkflowStages { get; set; } = new();
    public string ComputeAttachmentSourceTypesJson { get; set; } = string.Empty;
    public string ComputeTransactionTypeProfilesJson { get; set; } = string.Empty;
    public string InnolaAttachmentUploadRoute { get; set; } = string.Empty;
    public string InnolaAttachmentUploadBindingMode { get; set; } = string.Empty;
    public string InnolaAttachmentUploadMode { get; set; } = string.Empty;
    public string InnolaResumeAttachmentSourceType { get; set; } = string.Empty;
    public string InnolaCompletedAttachmentSourceType { get; set; } = string.Empty;
    public string InnolaResumeAttachmentRegisteredType { get; set; } = string.Empty;
    public string InnolaCompletedAttachmentRegisteredType { get; set; } = string.Empty;
    public string InnolaAttachmentRegisteredSpatialUnitId { get; set; } = string.Empty;
    public bool InnolaClientCertificateEnabled { get; set; }
    public string InnolaClientCertificateStoreLocation { get; set; } = string.Empty;
    public string InnolaClientCertificateStoreName { get; set; } = string.Empty;
    public string InnolaClientCertificateSubject { get; set; } = string.Empty;
    public string InnolaClientCertificateThumbprint { get; set; } = string.Empty;
    public bool InnolaAllowInvalidServerCertificate { get; set; }
    public bool InnolaCheckCertificateRevocationList { get; set; }

    public string ReviewWorkspaceMode { get; set; } = string.Empty;
    public string PdfViewerMode { get; set; } = string.Empty;
    public bool SpatialOutputAddCogoAttributes { get; set; }
    public bool SpatialOutputAddCogoLabels { get; set; }
    public string SpatialOutputCogoSourceMode { get; set; } = string.Empty;
    public string ClosureDefaultMaxClosureDistanceM { get; set; } = string.Empty;
    public string ClosureDefaultMinMiscloseRatioDenominator { get; set; } = string.Empty;
    public string ClosureDefaultWarningClosureDistanceM { get; set; } = string.Empty;
    public string ClosureDefaultWarningMiscloseRatioDenominator { get; set; } = string.Empty;
    public string ClosureToleranceProfileOverridesJson { get; set; } = string.Empty;
    public string ReadinessDefaultParcelType { get; set; } = string.Empty;
    public bool ReadinessDefaultEnabled { get; set; }
    public string ReadinessDefaultSeverity { get; set; } = string.Empty;
    public int ReadinessDefaultMinSegmentCount { get; set; }
    public bool ReadinessDefaultRequireContiguousSequence { get; set; }
    public bool ReadinessDefaultRequireReferencedPoints { get; set; }
    public bool ReadinessDefaultRequireChainConsistency { get; set; }
    public bool ReadinessDefaultDetectDuplicateEdges { get; set; }
    public bool EnterpriseWorkingEnabled { get; set; }
    public string EnterpriseWorkingServiceRoot { get; set; } = string.Empty;
    public string EnterpriseWorkingWorkspaceName { get; set; } = string.Empty;
    public string EnterpriseWorkingPublishBehavior { get; set; } = string.Empty;
    public string EnterpriseWorkingPublishTiming { get; set; } = string.Empty;
    public string EnterpriseWorkingRestoreBehavior { get; set; } = string.Empty;
    public bool EnterpriseWorkingAllowCrossMachineRestore { get; set; }
    public string EnterpriseWorkingTransactionScopeField { get; set; } = string.Empty;
    public string EnterpriseWorkingPointsLayer { get; set; } = string.Empty;
    public string EnterpriseWorkingLinesLayer { get; set; } = string.Empty;
    public string EnterpriseWorkingPolygonsLayer { get; set; } = string.Empty;
    public string EnterpriseWorkingIssuesLayer { get; set; } = string.Empty;
    public string EnterpriseWorkingCaseIndexLayer { get; set; } = string.Empty;
    public bool EnterpriseWorkingAdminProvisioningEnabled { get; set; }
    public string EnterpriseWorkingAdminProvisioningScriptPath { get; set; } = string.Empty;
    public string EnterpriseWorkingAdminPortalUrl { get; set; } = string.Empty;
    public string EnterpriseWorkingAdminSchemaVersion { get; set; } = string.Empty;
    public string EnterpriseWorkingAdminTargetFolder { get; set; } = string.Empty;
    public string EnterpriseWorkingAdminTargetServiceName { get; set; } = string.Empty;
    public bool EnterpriseWorkingAdminAllowSettingsWriteback { get; set; }
    public string EnterpriseWorkingAdminCleanupMode { get; set; } = string.Empty;
    public bool EnterpriseWorkingAdminRequireCleanupScope { get; set; }
    public string EnterpriseWorkingAdminLastValidationSummaryPath { get; set; } = string.Empty;
    public string EnterpriseWorkingAdminLastMaintenanceAuditPath { get; set; } = string.Empty;

    public bool EnterpriseParcelFabricEnabled { get; set; }
    public string EnterpriseParcelFabricServiceRoot { get; set; } = string.Empty;
    public string EnterpriseParcelFabricFabricLayerUrl { get; set; } = string.Empty;
    public string EnterpriseParcelFabricParcelLayerUrl { get; set; } = string.Empty;
    public string EnterpriseParcelFabricRecordsLayerUrl { get; set; } = string.Empty;
    public string EnterpriseParcelFabricParcelTypeName { get; set; } = string.Empty;
    public string EnterpriseParcelFabricRecordNamePattern { get; set; } = string.Empty;
    public string EnterpriseParcelFabricTransactionScopeField { get; set; } = string.Empty;
    public string EnterpriseParcelFabricTransactionIdField { get; set; } = string.Empty;
    public string EnterpriseParcelFabricReviewStateField { get; set; } = string.Empty;
    public string EnterpriseParcelFabricPublishTiming { get; set; } = string.Empty;
    public string EnterpriseParcelFabricBuildBehavior { get; set; } = string.Empty;
    public bool EnterpriseParcelFabricLoadOverlays { get; set; }
    public string EnterpriseParcelFabricOverlaySource { get; set; } = string.Empty;
    public bool EnterpriseParcelFabricAllowReplaceTransactionScope { get; set; }
    public bool EnterpriseParcelFabricRequireActiveMap { get; set; }

    public string GsiServerUrl { get; set; } = string.Empty;
    public string GsiUsername { get; set; } = string.Empty;
    public string GsiPasswordMode { get; set; } = string.Empty;
    public string GsiPasswordEnvironmentVariable { get; set; } = string.Empty;
    public string GsiPassword { get; set; } = string.Empty;

    public List<EditablePreflightRule> PreflightRules { get; set; } = new();
    public List<EditableReadinessRule> ReadinessRules { get; set; } = new();
}

public sealed class EditablePreflightRule
{
    public string RuleId { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string SectionName { get; init; } = string.Empty;
    public string RequiredCadLayerSummary { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? RequiredCadLayers { get; init; }
    public bool Enabled { get; set; }
    public string Severity { get; set; } = string.Empty;
    public bool Locked { get; init; }

    public static EditablePreflightRule FromDefinition(PreflightRuleDefinition definition)
    {
        return new EditablePreflightRule
        {
            RuleId = definition.RuleId,
            Group = definition.Group,
            Category = definition.Category,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            SectionName = ResolveSectionName(definition.Group),
            RequiredCadLayerSummary = FormatRequiredCadLayerSummary(definition.RequiredCadLayers),
            RequiredCadLayers = definition.RequiredCadLayers,
            Enabled = definition.Enabled,
            Severity = definition.Severity,
            Locked = definition.Locked
        };
    }

    private static string ResolveSectionName(string group)
    {
        return group.Trim().ToLowerInvariant() switch
        {
            "supporting_document" => "Supporting Document Rules",
            "structure" => "Structure Check Rules",
            "georeference" => "Georeference Check Rules",
            "dimension" => "Dimension Check Rules",
            "system" => "System Checks",
            _ => "Structure Check Rules"
        };
    }

    private static string FormatRequiredCadLayerSummary(IReadOnlyDictionary<string, IReadOnlyList<string>>? requiredCadLayers)
    {
        if (requiredCadLayers is null || requiredCadLayers.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("; ", requiredCadLayers
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => $"{item.Key}: {string.Join(", ", item.Value)}"));
    }
}

public sealed record SettingsWorkspaceValidationMessage(
    string TabName,
    string FieldName,
    string Message);

public sealed class EditableReadinessRule
{
    public string RuleId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string ParcelType { get; init; } = string.Empty;
    public string ScopeSummary { get; init; } = string.Empty;
    public bool IsDefaultFallback { get; init; }
    public bool Enabled { get; set; }
    public string Severity { get; set; } = string.Empty;
    public int MinSegmentCount { get; set; }
    public bool RequireContiguousSequence { get; set; }
    public bool RequireReferencedPoints { get; set; }
    public bool RequireChainConsistency { get; set; }
    public bool DetectDuplicateEdges { get; set; }
}
