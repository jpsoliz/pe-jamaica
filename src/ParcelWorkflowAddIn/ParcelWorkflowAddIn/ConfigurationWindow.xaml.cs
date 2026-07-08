using ArcGIS.Desktop.Framework.Controls;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Settings;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ParcelWorkflowAddIn;

public partial class ConfigurationWindow : ProWindow
{
    private readonly SettingsWorkspaceService settingsWorkspaceService = new();
    private readonly Dictionary<string, PreflightRuleEditorControls> preflightRuleEditors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ReadinessRuleEditorControls> readinessRuleEditors = new(StringComparer.OrdinalIgnoreCase);
    private SettingsWorkspaceDocument? currentDocument;

    public ConfigurationWindow()
    {
        InitializeComponent();
        ReloadWorkspace();
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        ReloadWorkspace("Settings reloaded from disk.");
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var document = BuildDocumentFromUi();
            var validationMessages = settingsWorkspaceService.Validate(document);
            if (validationMessages.Count > 0)
            {
                ShowValidationMessages(validationMessages);
                return;
            }

            settingsWorkspaceService.Save(document);
            ReloadWorkspace("Settings saved. Some changes apply on the next transaction or after ArcGIS Pro restart.");
        }
        catch (Exception exception)
        {
            ValidationSummaryTextBlock.Text = $"Could not save settings. {exception.Message}";
            ValidationSummaryTextBlock.Visibility = Visibility.Visible;
            FooterStatusTextBlock.Text = "Settings were not saved.";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ReviewWorkspaceModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyReviewWorkspacePresentation();
    }

    private void GsiPasswordModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyGsiPasswordModePresentation();
    }

    private void OpenAiExtractionProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyOpenAiExtractionProfilePresentation();
    }

    private async void ValidateEnterpriseWorkingAdminButton_Click(object sender, RoutedEventArgs e)
    {
        await RunEnterpriseWorkingAdminOperationAsync("validate").ConfigureAwait(true);
    }

    private async void ProvisionEnterpriseWorkingAdminButton_Click(object sender, RoutedEventArgs e)
    {
        await RunEnterpriseWorkingAdminOperationAsync("provision").ConfigureAwait(true);
    }

    private async void CleanupEnterpriseWorkingAdminButton_Click(object sender, RoutedEventArgs e)
    {
        await RunEnterpriseWorkingAdminOperationAsync("cleanup").ConfigureAwait(true);
    }

    private async Task RunEnterpriseWorkingAdminOperationAsync(string mode)
    {
        var document = BuildDocumentFromUi();
        var messages = settingsWorkspaceService.Validate(document)
            .Where(message => IsBlockingEnterpriseAdminMessage(mode, message))
            .ToList();
        if (messages.Count > 0)
        {
            EnterpriseAdminStatusTextBlock.Text = string.Join(Environment.NewLine, messages.Select(message => $"{message.FieldName}: {message.Message}"));
            return;
        }

        if (string.IsNullOrWhiteSpace(document.EnterpriseWorkingAdminProvisioningScriptPath) || !File.Exists(document.EnterpriseWorkingAdminProvisioningScriptPath))
        {
            EnterpriseAdminStatusTextBlock.Text = "Provisioning script path is required before running Enterprise admin operations.";
            return;
        }

        if (string.IsNullOrWhiteSpace(document.EnterpriseWorkingAdminPortalUrl))
        {
            EnterpriseAdminStatusTextBlock.Text = "Portal URL is required before running Enterprise admin operations.";
            return;
        }

        if (string.IsNullOrWhiteSpace(document.EnterpriseWorkingServiceRoot))
        {
            EnterpriseAdminStatusTextBlock.Text = "Enterprise working service root is required before running Enterprise admin operations.";
            return;
        }

        var cleanupScope = EnterpriseAdminCleanupScopeTextBox.Text.Trim();
        if (string.Equals(mode, "cleanup", StringComparison.OrdinalIgnoreCase)
            && document.EnterpriseWorkingAdminRequireCleanupScope
            && string.IsNullOrWhiteSpace(cleanupScope))
        {
            EnterpriseAdminStatusTextBlock.Text = "Cleanup requires an explicit transaction number or test scope. No delete-all operation is available from Settings.";
            return;
        }

        var outputJson = ResolveEnterpriseAdminOutputPath(document, mode);
        var auditJson = ResolveEnterpriseAdminAuditPath(document);
        Directory.CreateDirectory(Path.GetDirectoryName(outputJson) ?? ".");
        if (string.Equals(mode, "cleanup", StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(auditJson) ?? ".");
        }

        EnterpriseAdminStatusTextBlock.Text = $"Running Enterprise admin {mode} with configured ArcGIS Python. Credentials are read from runtime environment variables only.";

        try
        {
            var result = await RunEnterpriseAdminScriptAsync(document, mode, cleanupScope, outputJson, auditJson).ConfigureAwait(true);
            var payload = File.Exists(outputJson)
                ? JsonNode.Parse(File.ReadAllText(outputJson))?.AsObject()
                : null;
            if (payload is not null)
            {
                ApplyEnterpriseAdminResult(mode, document, payload, outputJson, auditJson);
            }

            var statusText = payload is null
                ? string.Join(Environment.NewLine, new[] { result.StandardOutput, result.StandardError }.Where(text => !string.IsNullOrWhiteSpace(text))).Trim()
                : BuildEnterpriseAdminResultSummary(payload);
            if (result.ExitCode != 0)
            {
                EnterpriseAdminStatusTextBlock.Text = $"Enterprise admin {mode} failed.{Environment.NewLine}{statusText}";
                return;
            }

            EnterpriseAdminStatusTextBlock.Text = statusText;
            EnterpriseAdminRuntimeSummaryTextBlock.Text = BuildEnterpriseAdminRuntimeSummary(BuildDocumentFromUi());
        }
        catch (Exception exception)
        {
            EnterpriseAdminStatusTextBlock.Text = $"Enterprise admin {mode} could not run. {exception.Message}";
        }
    }

    private static async Task<EnterpriseAdminProcessResult> RunEnterpriseAdminScriptAsync(
        SettingsWorkspaceDocument document,
        string mode,
        string cleanupScope,
        string outputJson,
        string auditJson)
    {
        var pythonExecutable = string.IsNullOrWhiteSpace(document.ArcGisPythonExecutable)
            ? "python"
            : document.ArcGisPythonExecutable;
        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add(document.EnterpriseWorkingAdminProvisioningScriptPath);
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add(mode);
        startInfo.ArgumentList.Add("--no-dry-run");
        AddArgument(startInfo, "--portal-url", document.EnterpriseWorkingAdminPortalUrl);
        AddArgument(startInfo, "--service-root", document.EnterpriseWorkingServiceRoot);
        AddArgument(startInfo, "--workspace-name", document.EnterpriseWorkingWorkspaceName);
        AddArgument(startInfo, "--target-folder", document.EnterpriseWorkingAdminTargetFolder);
        AddArgument(startInfo, "--target-service-name", document.EnterpriseWorkingAdminTargetServiceName);
        AddArgument(startInfo, "--schema-version", document.EnterpriseWorkingAdminSchemaVersion);
        AddArgument(startInfo, "--points-layer", document.EnterpriseWorkingPointsLayer);
        AddArgument(startInfo, "--lines-layer", document.EnterpriseWorkingLinesLayer);
        AddArgument(startInfo, "--polygons-layer", document.EnterpriseWorkingPolygonsLayer);
        AddArgument(startInfo, "--case-index-layer", document.EnterpriseWorkingCaseIndexLayer);
        AddArgument(startInfo, "--issues-layer", document.EnterpriseWorkingIssuesLayer);
        AddArgument(startInfo, "--cleanup-scope-field", document.EnterpriseWorkingTransactionScopeField);
        AddArgument(startInfo, "--cleanup-scope-value", cleanupScope);
        AddArgument(startInfo, "--cleanup-mode", document.EnterpriseWorkingAdminCleanupMode);
        startInfo.ArgumentList.Add("--require-cleanup-scope");
        AddArgument(startInfo, "--output-json", outputJson);
        AddArgument(startInfo, "--audit-json", auditJson);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start Enterprise admin provisioning script.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        return new EnterpriseAdminProcessResult(
            process.ExitCode,
            await stdoutTask.ConfigureAwait(false),
            await stderrTask.ConfigureAwait(false));
    }

    private void ApplyEnterpriseAdminResult(
        string mode,
        SettingsWorkspaceDocument document,
        JsonObject payload,
        string outputJson,
        string auditJson)
    {
        EnterpriseAdminLastValidationSummaryPathTextBox.Text = outputJson;
        if (string.Equals(mode, "cleanup", StringComparison.OrdinalIgnoreCase))
        {
            EnterpriseAdminLastMaintenanceAuditPathTextBox.Text = auditJson;
        }

        var status = ReadJsonString(payload, "status");
        if (!string.Equals(status, "provisioned", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "provision_ready", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (document.EnterpriseWorkingAdminAllowSettingsWriteback && payload["layers"] is JsonObject layers)
        {
            EnterpriseWorkingPointsLayerTextBox.Text = ReadJsonString(layers, "points") ?? EnterpriseWorkingPointsLayerTextBox.Text;
            EnterpriseWorkingLinesLayerTextBox.Text = ReadJsonString(layers, "lines") ?? EnterpriseWorkingLinesLayerTextBox.Text;
            EnterpriseWorkingPolygonsLayerTextBox.Text = ReadJsonString(layers, "polygons") ?? EnterpriseWorkingPolygonsLayerTextBox.Text;
            EnterpriseWorkingCaseIndexLayerTextBox.Text = ReadJsonString(layers, "case_index") ?? EnterpriseWorkingCaseIndexLayerTextBox.Text;
            EnterpriseWorkingIssuesLayerTextBox.Text = ReadJsonString(layers, "issues") ?? EnterpriseWorkingIssuesLayerTextBox.Text;
            settingsWorkspaceService.Save(BuildDocumentFromUi());
        }
    }

    private static string BuildEnterpriseAdminResultSummary(JsonObject payload)
    {
        var lines = new List<string>
        {
            $"Status: {ReadJsonString(payload, "status") ?? "(unknown)"}"
        };
        if (payload["messages"] is JsonArray messages)
        {
            lines.AddRange(messages
                .Select(message => message?.GetValue<string>())
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message!));
        }

        if (payload["validation"] is JsonObject validation)
        {
            AddJsonArrayMessages(lines, "Errors", validation["errors"] as JsonArray);
            AddJsonArrayMessages(lines, "Warnings", validation["warnings"] as JsonArray);
        }

        if (payload["cleanup"] is JsonObject cleanup && cleanup["affected_counts"] is JsonObject counts)
        {
            lines.Add("Cleanup counts: " + string.Join(", ", counts.Select(item => $"{item.Key}={item.Value}")));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AddJsonArrayMessages(ICollection<string> lines, string label, JsonArray? messages)
    {
        if (messages is null || messages.Count == 0)
        {
            return;
        }

        lines.Add($"{label}:");
        foreach (var message in messages)
        {
            lines.Add($"- {message?.GetValue<string>()}");
        }
    }

    private static string ResolveEnterpriseAdminOutputPath(SettingsWorkspaceDocument document, string mode)
    {
        if (!string.IsNullOrWhiteSpace(document.EnterpriseWorkingAdminLastValidationSummaryPath))
        {
            return document.EnterpriseWorkingAdminLastValidationSummaryPath;
        }

        return Path.Combine(ResolveSettingsDirectory(document), $"enterprise_working_admin_{mode}.json");
    }

    private static string ResolveEnterpriseAdminAuditPath(SettingsWorkspaceDocument document)
    {
        if (!string.IsNullOrWhiteSpace(document.EnterpriseWorkingAdminLastMaintenanceAuditPath))
        {
            return document.EnterpriseWorkingAdminLastMaintenanceAuditPath;
        }

        return Path.Combine(ResolveSettingsDirectory(document), "enterprise_working_admin_audit.json");
    }

    private static string ResolveSettingsDirectory(SettingsWorkspaceDocument document)
    {
        return Path.GetDirectoryName(document.SettingsPath)
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }

    private static void AddArgument(ProcessStartInfo startInfo, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        startInfo.ArgumentList.Add(name);
        startInfo.ArgumentList.Add(value);
    }

    private static bool IsBlockingEnterpriseAdminMessage(string mode, SettingsWorkspaceValidationMessage message)
    {
        if (string.Equals(message.TabName, "Enterprise Admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(message.TabName, "Spatial Workspace", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(mode, "provision", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(message.FieldName, "Enterprise Working Review", StringComparison.OrdinalIgnoreCase)
                || string.Equals(message.FieldName, "Enterprise Targets", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return string.Equals(message.FieldName, "Enterprise Working Review", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message.FieldName, "Enterprise Targets", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message.FieldName, "ArcGIS Enterprise Server", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadJsonString(JsonObject node, string propertyName)
    {
        return node.TryGetPropertyValue(propertyName, out var value) && value is not null
            ? value.GetValue<string>()
            : null;
    }

    private void ReloadWorkspace(string? footerMessage = null)
    {
        currentDocument = settingsWorkspaceService.Load();
        PopulateUi(currentDocument);
        ShowWorkspaceWarnings(currentDocument);
        FooterStatusTextBlock.Text = footerMessage ?? "Changes to runtime configuration usually apply on the next transaction or after restart, depending on the setting.";
    }

    private void PopulateUi(SettingsWorkspaceDocument document)
    {
        SettingsPathTextBox.Text = document.SettingsPath;
        SettingsSummaryText.Text = $"Edit local parcel workflow settings using the tabs below. Active structure rules file: {document.PreflightRulesPath}";

        CaseFolderRootTextBox.Text = document.CaseFolderOutputRoot;
        PythonExecutableTextBox.Text = document.ArcGisPythonExecutable;
        OutputAdapterScriptPathTextBox.Text = document.OutputAdapterScriptPath;
        ValidationAdapterScriptPathTextBox.Text = document.ValidationAdapterScriptPath;
        ValidationRulesPathTextBox.Text = document.ValidationRulesPath;
        OutputTemplateProjectPathTextBox.Text = document.OutputTemplateProjectPath;
        OutputTemplateGdbPathTextBox.Text = document.OutputTemplateGdbPath;
        OutputAdapterTimeoutTextBox.Text = document.OutputAdapterTimeoutSeconds.ToString();
        SdkLaneSummaryTextBox.Text = $"{document.ArcGisProSdkLane} / {document.TargetFramework}";

        SetSelectedTag(OcrEngineComboBox, document.OcrEngine);
        OpenAiEnabledCheckBox.IsChecked = document.OpenAiEnabled;
        SetSelectedTag(OpenAiExtractionProfileComboBox, document.OpenAiExtractionProfile);
        OpenAiModelTextBox.Text = document.OpenAiModel;
        OpenAiApiKeyEnvironmentVariableTextBox.Text = document.OpenAiApiKeyEnvironmentVariable;

        InnolaServerUrlTextBox.Text = document.InnolaServerUrl;
        SetSelectedTag(InnolaModeComboBox, document.InnolaTransactionMode);
        InnolaProcessStepTextBox.Text = document.InnolaProcessStep;
        SupportedTransactionTypesTextBox.Text = string.Join(Environment.NewLine, document.SupportedTransactionTypes);
        ComputeWorkflowStagesTextBox.Text = string.Join(Environment.NewLine, document.ComputeWorkflowStages);
        ComputeAttachmentSourceTypesTextBox.Text = document.ComputeAttachmentSourceTypesJson;
        ComputeTransactionTypeProfilesTextBox.Text = document.ComputeTransactionTypeProfilesJson;
        InnolaAttachmentUploadRouteTextBox.Text = document.InnolaAttachmentUploadRoute;
        InnolaAttachmentUploadBindingModeTextBox.Text = document.InnolaAttachmentUploadBindingMode;
        InnolaAttachmentUploadModeTextBox.Text = document.InnolaAttachmentUploadMode;
        InnolaResumeAttachmentSourceTypeTextBox.Text = document.InnolaResumeAttachmentSourceType;
        InnolaCompletedAttachmentSourceTypeTextBox.Text = document.InnolaCompletedAttachmentSourceType;
        InnolaResumeAttachmentRegisteredTypeTextBox.Text = document.InnolaResumeAttachmentRegisteredType;
        InnolaCompletedAttachmentRegisteredTypeTextBox.Text = document.InnolaCompletedAttachmentRegisteredType;
        InnolaAttachmentRegisteredSpatialUnitIdTextBox.Text = document.InnolaAttachmentRegisteredSpatialUnitId;
        InnolaClientCertificateEnabledCheckBox.IsChecked = document.InnolaClientCertificateEnabled;
        InnolaClientCertificateStoreLocationTextBox.Text = document.InnolaClientCertificateStoreLocation;
        InnolaClientCertificateStoreNameTextBox.Text = document.InnolaClientCertificateStoreName;
        InnolaClientCertificateSubjectTextBox.Text = document.InnolaClientCertificateSubject;
        InnolaClientCertificateThumbprintTextBox.Text = document.InnolaClientCertificateThumbprint;
        InnolaAllowInvalidServerCertificateCheckBox.IsChecked = document.InnolaAllowInvalidServerCertificate;
        InnolaCheckCertificateRevocationListCheckBox.IsChecked = document.InnolaCheckCertificateRevocationList;

        PreflightRulesPathTextBox.Text = document.PreflightRulesPath;
        RenderPreflightRules(document.PreflightRules);

        SetSelectedTag(ReviewWorkspaceModeComboBox, document.ReviewWorkspaceMode);
        SetSelectedTag(PdfViewerModeComboBox, document.PdfViewerMode);
        SpatialOutputAddCogoAttributesCheckBox.IsChecked = document.SpatialOutputAddCogoAttributes;
        SpatialOutputAddCogoLabelsCheckBox.IsChecked = document.SpatialOutputAddCogoLabels;
        SetSelectedTag(SpatialOutputCogoSourceModeComboBox, document.SpatialOutputCogoSourceMode);
        ClosureDefaultMaxClosureDistanceTextBox.Text = document.ClosureDefaultMaxClosureDistanceM;
        ClosureDefaultWarningClosureDistanceTextBox.Text = document.ClosureDefaultWarningClosureDistanceM;
        ClosureDefaultMinMiscloseRatioTextBox.Text = document.ClosureDefaultMinMiscloseRatioDenominator;
        ClosureDefaultWarningMiscloseRatioTextBox.Text = document.ClosureDefaultWarningMiscloseRatioDenominator;
        ClosureToleranceProfileOverridesTextBox.Text = document.ClosureToleranceProfileOverridesJson;
        ReadinessDefaultParcelTypeTextBox.Text = document.ReadinessDefaultParcelType;
        ReadinessDefaultEnabledCheckBox.IsChecked = document.ReadinessDefaultEnabled;
        SetSelectedTag(ReadinessDefaultSeverityComboBox, document.ReadinessDefaultSeverity);
        ReadinessDefaultMinSegmentCountTextBox.Text = document.ReadinessDefaultMinSegmentCount.ToString();
        ReadinessDefaultRequireContiguousSequenceCheckBox.IsChecked = document.ReadinessDefaultRequireContiguousSequence;
        ReadinessDefaultRequireReferencedPointsCheckBox.IsChecked = document.ReadinessDefaultRequireReferencedPoints;
        ReadinessDefaultRequireChainConsistencyCheckBox.IsChecked = document.ReadinessDefaultRequireChainConsistency;
        ReadinessDefaultDetectDuplicateEdgesCheckBox.IsChecked = document.ReadinessDefaultDetectDuplicateEdges;
        RenderReadinessRules(document.ReadinessRules);
        EnterpriseWorkingEnabledCheckBox.IsChecked = document.EnterpriseWorkingEnabled;
        EnterpriseWorkingServiceRootTextBox.Text = document.EnterpriseWorkingServiceRoot;
        EnterpriseWorkingWorkspaceNameTextBox.Text = document.EnterpriseWorkingWorkspaceName;
        SetSelectedTag(EnterpriseWorkingPublishBehaviorComboBox, document.EnterpriseWorkingPublishBehavior);
        SetSelectedTag(EnterpriseWorkingPublishTimingComboBox, document.EnterpriseWorkingPublishTiming);
        SetSelectedTag(EnterpriseWorkingRestoreBehaviorComboBox, document.EnterpriseWorkingRestoreBehavior);
        EnterpriseWorkingAllowCrossMachineRestoreCheckBox.IsChecked = document.EnterpriseWorkingAllowCrossMachineRestore;
        EnterpriseWorkingTransactionScopeFieldTextBox.Text = document.EnterpriseWorkingTransactionScopeField;
        EnterpriseWorkingPointsLayerTextBox.Text = document.EnterpriseWorkingPointsLayer;
        EnterpriseWorkingLinesLayerTextBox.Text = document.EnterpriseWorkingLinesLayer;
        EnterpriseWorkingPolygonsLayerTextBox.Text = document.EnterpriseWorkingPolygonsLayer;
        EnterpriseWorkingIssuesLayerTextBox.Text = document.EnterpriseWorkingIssuesLayer;
        EnterpriseWorkingCaseIndexLayerTextBox.Text = document.EnterpriseWorkingCaseIndexLayer;
        EnterpriseAdminRuntimeSummaryTextBlock.Text = BuildEnterpriseAdminRuntimeSummary(document);
        EnterpriseAdminProvisioningEnabledCheckBox.IsChecked = document.EnterpriseWorkingAdminProvisioningEnabled;
        EnterpriseAdminProvisioningScriptPathTextBox.Text = document.EnterpriseWorkingAdminProvisioningScriptPath;
        EnterpriseAdminPortalUrlTextBox.Text = document.EnterpriseWorkingAdminPortalUrl;
        EnterpriseAdminSchemaVersionTextBox.Text = document.EnterpriseWorkingAdminSchemaVersion;
        EnterpriseAdminTargetFolderTextBox.Text = document.EnterpriseWorkingAdminTargetFolder;
        EnterpriseAdminTargetServiceNameTextBox.Text = document.EnterpriseWorkingAdminTargetServiceName;
        EnterpriseAdminAllowSettingsWritebackCheckBox.IsChecked = document.EnterpriseWorkingAdminAllowSettingsWriteback;
        SetSelectedTag(EnterpriseAdminCleanupModeComboBox, document.EnterpriseWorkingAdminCleanupMode);
        EnterpriseAdminRequireCleanupScopeCheckBox.IsChecked = document.EnterpriseWorkingAdminRequireCleanupScope;
        EnterpriseAdminLastValidationSummaryPathTextBox.Text = document.EnterpriseWorkingAdminLastValidationSummaryPath;
        EnterpriseAdminLastMaintenanceAuditPathTextBox.Text = document.EnterpriseWorkingAdminLastMaintenanceAuditPath;
        EnterpriseAdminStatusTextBlock.Text = "Admin operations require explicit portal credentials at runtime. Credentials are not saved here.";
        EnterpriseParcelFabricEnabledCheckBox.IsChecked = document.EnterpriseParcelFabricEnabled;
        EnterpriseParcelFabricServiceRootTextBox.Text = document.EnterpriseParcelFabricServiceRoot;
        EnterpriseParcelFabricFabricLayerUrlTextBox.Text = document.EnterpriseParcelFabricFabricLayerUrl;
        EnterpriseParcelFabricParcelLayerUrlTextBox.Text = document.EnterpriseParcelFabricParcelLayerUrl;
        EnterpriseParcelFabricRecordsLayerUrlTextBox.Text = document.EnterpriseParcelFabricRecordsLayerUrl;
        EnterpriseParcelFabricParcelTypeNameTextBox.Text = document.EnterpriseParcelFabricParcelTypeName;
        EnterpriseParcelFabricRecordNamePatternTextBox.Text = document.EnterpriseParcelFabricRecordNamePattern;
        EnterpriseParcelFabricTransactionScopeFieldTextBox.Text = document.EnterpriseParcelFabricTransactionScopeField;
        EnterpriseParcelFabricTransactionIdFieldTextBox.Text = document.EnterpriseParcelFabricTransactionIdField;
        EnterpriseParcelFabricReviewStateFieldTextBox.Text = document.EnterpriseParcelFabricReviewStateField;
        SetSelectedTag(EnterpriseParcelFabricPublishTimingComboBox, document.EnterpriseParcelFabricPublishTiming);
        SetSelectedTag(EnterpriseParcelFabricBuildBehaviorComboBox, document.EnterpriseParcelFabricBuildBehavior);
        EnterpriseParcelFabricLoadOverlaysCheckBox.IsChecked = document.EnterpriseParcelFabricLoadOverlays;
        SetSelectedTag(EnterpriseParcelFabricOverlaySourceComboBox, document.EnterpriseParcelFabricOverlaySource);
        EnterpriseParcelFabricAllowReplaceTransactionScopeCheckBox.IsChecked = document.EnterpriseParcelFabricAllowReplaceTransactionScope;
        EnterpriseParcelFabricRequireActiveMapCheckBox.IsChecked = document.EnterpriseParcelFabricRequireActiveMap;

        GsiServerUrlTextBox.Text = document.GsiServerUrl;
        GsiUsernameTextBox.Text = document.GsiUsername;
        SetSelectedTag(GsiPasswordModeComboBox, document.GsiPasswordMode);
        GsiPasswordEnvironmentVariableTextBox.Text = document.GsiPasswordEnvironmentVariable;
        GsiPasswordBox.Password = document.GsiPassword;

        ApplyReviewWorkspacePresentation();
        ApplyGsiPasswordModePresentation();
        ApplyOpenAiExtractionProfilePresentation();
    }

    private SettingsWorkspaceDocument BuildDocumentFromUi()
    {
        var document = currentDocument ?? settingsWorkspaceService.Load();

        document.CaseFolderOutputRoot = CaseFolderRootTextBox.Text.Trim();
        document.ArcGisPythonExecutable = PythonExecutableTextBox.Text.Trim();
        document.OutputAdapterScriptPath = OutputAdapterScriptPathTextBox.Text.Trim();
        document.ValidationAdapterScriptPath = ValidationAdapterScriptPathTextBox.Text.Trim();
        document.ValidationRulesPath = ValidationRulesPathTextBox.Text.Trim();
        document.OutputTemplateProjectPath = OutputTemplateProjectPathTextBox.Text.Trim();
        document.OutputTemplateGdbPath = OutputTemplateGdbPathTextBox.Text.Trim();
        document.OutputAdapterTimeoutSeconds = ParsePositiveInt(OutputAdapterTimeoutTextBox.Text, document.OutputAdapterTimeoutSeconds);

        document.OcrEngine = GetSelectedTag(OcrEngineComboBox, "local");
        document.OpenAiEnabled = OpenAiEnabledCheckBox.IsChecked == true;
        document.OpenAiExtractionProfile = GetSelectedTag(OpenAiExtractionProfileComboBox, SettingsWorkspaceService.OpenAiExtractionProfileCustom);
        document.OpenAiModel = OpenAiModelTextBox.Text.Trim();
        document.OpenAiApiKeyEnvironmentVariable = OpenAiApiKeyEnvironmentVariableTextBox.Text.Trim();

        document.InnolaServerUrl = InnolaServerUrlTextBox.Text.Trim();
        document.InnolaTransactionMode = GetSelectedTag(InnolaModeComboBox, "mock");
        document.InnolaProcessStep = InnolaProcessStepTextBox.Text.Trim();
        document.SupportedTransactionTypes = SplitLines(SupportedTransactionTypesTextBox.Text);
        document.ComputeWorkflowStages = SplitLines(ComputeWorkflowStagesTextBox.Text);
        document.ComputeAttachmentSourceTypesJson = ComputeAttachmentSourceTypesTextBox.Text.Trim();
        document.ComputeTransactionTypeProfilesJson = ComputeTransactionTypeProfilesTextBox.Text.Trim();
        document.InnolaAttachmentUploadRoute = InnolaAttachmentUploadRouteTextBox.Text.Trim();
        document.InnolaAttachmentUploadBindingMode = InnolaAttachmentUploadBindingModeTextBox.Text.Trim();
        document.InnolaAttachmentUploadMode = InnolaAttachmentUploadModeTextBox.Text.Trim();
        document.InnolaResumeAttachmentSourceType = InnolaResumeAttachmentSourceTypeTextBox.Text.Trim();
        document.InnolaCompletedAttachmentSourceType = InnolaCompletedAttachmentSourceTypeTextBox.Text.Trim();
        document.InnolaResumeAttachmentRegisteredType = InnolaResumeAttachmentRegisteredTypeTextBox.Text.Trim();
        document.InnolaCompletedAttachmentRegisteredType = InnolaCompletedAttachmentRegisteredTypeTextBox.Text.Trim();
        document.InnolaAttachmentRegisteredSpatialUnitId = InnolaAttachmentRegisteredSpatialUnitIdTextBox.Text.Trim();
        document.InnolaClientCertificateEnabled = InnolaClientCertificateEnabledCheckBox.IsChecked == true;
        document.InnolaClientCertificateStoreLocation = InnolaClientCertificateStoreLocationTextBox.Text.Trim();
        document.InnolaClientCertificateStoreName = InnolaClientCertificateStoreNameTextBox.Text.Trim();
        document.InnolaClientCertificateSubject = InnolaClientCertificateSubjectTextBox.Text.Trim();
        document.InnolaClientCertificateThumbprint = InnolaClientCertificateThumbprintTextBox.Text.Trim();
        document.InnolaAllowInvalidServerCertificate = InnolaAllowInvalidServerCertificateCheckBox.IsChecked == true;
        document.InnolaCheckCertificateRevocationList = InnolaCheckCertificateRevocationListCheckBox.IsChecked == true;

        document.ReviewWorkspaceMode = GetSelectedTag(ReviewWorkspaceModeComboBox, InnolaTransactionSettings.ReviewWorkspaceModeNormal);
        document.PdfViewerMode = GetSelectedTag(PdfViewerModeComboBox, InnolaTransactionSettings.PdfViewerModeEmbeddedBrowser);
        document.SpatialOutputAddCogoAttributes = SpatialOutputAddCogoAttributesCheckBox.IsChecked == true;
        document.SpatialOutputAddCogoLabels = SpatialOutputAddCogoLabelsCheckBox.IsChecked == true;
        document.SpatialOutputCogoSourceMode = GetSelectedTag(SpatialOutputCogoSourceModeComboBox, SettingsWorkspaceService.SpatialOutputCogoSourceModeSourceThenComputed);
        document.ClosureDefaultMaxClosureDistanceM = ClosureDefaultMaxClosureDistanceTextBox.Text.Trim();
        document.ClosureDefaultWarningClosureDistanceM = ClosureDefaultWarningClosureDistanceTextBox.Text.Trim();
        document.ClosureDefaultMinMiscloseRatioDenominator = ClosureDefaultMinMiscloseRatioTextBox.Text.Trim();
        document.ClosureDefaultWarningMiscloseRatioDenominator = ClosureDefaultWarningMiscloseRatioTextBox.Text.Trim();
        document.ClosureToleranceProfileOverridesJson = ClosureToleranceProfileOverridesTextBox.Text.Trim();
        document.ReadinessDefaultParcelType = ReadinessDefaultParcelTypeTextBox.Text.Trim();
        document.ReadinessDefaultEnabled = ReadinessDefaultEnabledCheckBox.IsChecked == true;
        document.ReadinessDefaultSeverity = GetSelectedTag(ReadinessDefaultSeverityComboBox, "blocker");
        document.ReadinessDefaultMinSegmentCount = ParsePositiveInt(ReadinessDefaultMinSegmentCountTextBox.Text, document.ReadinessDefaultMinSegmentCount);
        document.ReadinessDefaultRequireContiguousSequence = ReadinessDefaultRequireContiguousSequenceCheckBox.IsChecked == true;
        document.ReadinessDefaultRequireReferencedPoints = ReadinessDefaultRequireReferencedPointsCheckBox.IsChecked == true;
        document.ReadinessDefaultRequireChainConsistency = ReadinessDefaultRequireChainConsistencyCheckBox.IsChecked == true;
        document.ReadinessDefaultDetectDuplicateEdges = ReadinessDefaultDetectDuplicateEdgesCheckBox.IsChecked == true;
        document.EnterpriseWorkingEnabled = EnterpriseWorkingEnabledCheckBox.IsChecked == true;
        document.EnterpriseWorkingServiceRoot = EnterpriseWorkingServiceRootTextBox.Text.Trim();
        document.EnterpriseWorkingWorkspaceName = EnterpriseWorkingWorkspaceNameTextBox.Text.Trim();
        document.EnterpriseWorkingPublishBehavior = GetSelectedTag(EnterpriseWorkingPublishBehaviorComboBox, EnterpriseWorkingReviewSettings.PublishBehaviorReplaceTransactionScope);
        document.EnterpriseWorkingPublishTiming = GetSelectedTag(EnterpriseWorkingPublishTimingComboBox, EnterpriseWorkingReviewSettings.PublishTimingOnComplete);
        document.EnterpriseWorkingRestoreBehavior = GetSelectedTag(EnterpriseWorkingRestoreBehaviorComboBox, EnterpriseWorkingReviewSettings.RestoreBehaviorPreferLocalThenEnterprise);
        document.EnterpriseWorkingAllowCrossMachineRestore = EnterpriseWorkingAllowCrossMachineRestoreCheckBox.IsChecked == true;
        document.EnterpriseWorkingTransactionScopeField = EnterpriseWorkingTransactionScopeFieldTextBox.Text.Trim();
        document.EnterpriseWorkingPointsLayer = EnterpriseWorkingPointsLayerTextBox.Text.Trim();
        document.EnterpriseWorkingLinesLayer = EnterpriseWorkingLinesLayerTextBox.Text.Trim();
        document.EnterpriseWorkingPolygonsLayer = EnterpriseWorkingPolygonsLayerTextBox.Text.Trim();
        document.EnterpriseWorkingIssuesLayer = EnterpriseWorkingIssuesLayerTextBox.Text.Trim();
        document.EnterpriseWorkingCaseIndexLayer = EnterpriseWorkingCaseIndexLayerTextBox.Text.Trim();
        document.EnterpriseWorkingAdminProvisioningEnabled = EnterpriseAdminProvisioningEnabledCheckBox.IsChecked == true;
        document.EnterpriseWorkingAdminProvisioningScriptPath = EnterpriseAdminProvisioningScriptPathTextBox.Text.Trim();
        document.EnterpriseWorkingAdminPortalUrl = EnterpriseAdminPortalUrlTextBox.Text.Trim();
        document.EnterpriseWorkingAdminSchemaVersion = EnterpriseAdminSchemaVersionTextBox.Text.Trim();
        document.EnterpriseWorkingAdminTargetFolder = EnterpriseAdminTargetFolderTextBox.Text.Trim();
        document.EnterpriseWorkingAdminTargetServiceName = EnterpriseAdminTargetServiceNameTextBox.Text.Trim();
        document.EnterpriseWorkingAdminAllowSettingsWriteback = EnterpriseAdminAllowSettingsWritebackCheckBox.IsChecked == true;
        document.EnterpriseWorkingAdminCleanupMode = GetSelectedTag(EnterpriseAdminCleanupModeComboBox, SettingsWorkspaceService.EnterpriseWorkingAdminCleanupModeDeactivate);
        document.EnterpriseWorkingAdminRequireCleanupScope = EnterpriseAdminRequireCleanupScopeCheckBox.IsChecked == true;
        document.EnterpriseWorkingAdminLastValidationSummaryPath = EnterpriseAdminLastValidationSummaryPathTextBox.Text.Trim();
        document.EnterpriseWorkingAdminLastMaintenanceAuditPath = EnterpriseAdminLastMaintenanceAuditPathTextBox.Text.Trim();
        document.EnterpriseParcelFabricEnabled = EnterpriseParcelFabricEnabledCheckBox.IsChecked == true;
        document.EnterpriseParcelFabricServiceRoot = EnterpriseParcelFabricServiceRootTextBox.Text.Trim();
        document.EnterpriseParcelFabricFabricLayerUrl = EnterpriseParcelFabricFabricLayerUrlTextBox.Text.Trim();
        document.EnterpriseParcelFabricParcelLayerUrl = EnterpriseParcelFabricParcelLayerUrlTextBox.Text.Trim();
        document.EnterpriseParcelFabricRecordsLayerUrl = EnterpriseParcelFabricRecordsLayerUrlTextBox.Text.Trim();
        document.EnterpriseParcelFabricParcelTypeName = EnterpriseParcelFabricParcelTypeNameTextBox.Text.Trim();
        document.EnterpriseParcelFabricRecordNamePattern = EnterpriseParcelFabricRecordNamePatternTextBox.Text.Trim();
        document.EnterpriseParcelFabricTransactionScopeField = EnterpriseParcelFabricTransactionScopeFieldTextBox.Text.Trim();
        document.EnterpriseParcelFabricTransactionIdField = EnterpriseParcelFabricTransactionIdFieldTextBox.Text.Trim();
        document.EnterpriseParcelFabricReviewStateField = EnterpriseParcelFabricReviewStateFieldTextBox.Text.Trim();
        document.EnterpriseParcelFabricPublishTiming = GetSelectedTag(EnterpriseParcelFabricPublishTimingComboBox, EnterpriseParcelFabricReviewSettings.PublishTimingOnOutputs);
        document.EnterpriseParcelFabricBuildBehavior = GetSelectedTag(EnterpriseParcelFabricBuildBehaviorComboBox, EnterpriseParcelFabricReviewSettings.BuildBehaviorBuildAfterCopy);
        document.EnterpriseParcelFabricLoadOverlays = EnterpriseParcelFabricLoadOverlaysCheckBox.IsChecked == true;
        document.EnterpriseParcelFabricOverlaySource = GetSelectedTag(EnterpriseParcelFabricOverlaySourceComboBox, EnterpriseParcelFabricReviewSettings.OverlaySourceLocalCaseOutputs);
        document.EnterpriseParcelFabricAllowReplaceTransactionScope = EnterpriseParcelFabricAllowReplaceTransactionScopeCheckBox.IsChecked == true;
        document.EnterpriseParcelFabricRequireActiveMap = EnterpriseParcelFabricRequireActiveMapCheckBox.IsChecked == true;

        document.GsiServerUrl = GsiServerUrlTextBox.Text.Trim();
        document.GsiUsername = GsiUsernameTextBox.Text.Trim();
        document.GsiPasswordMode = GetSelectedTag(GsiPasswordModeComboBox, SettingsWorkspaceService.GsiPasswordModeEnvironmentVariable);
        document.GsiPasswordEnvironmentVariable = GsiPasswordEnvironmentVariableTextBox.Text.Trim();
        document.GsiPassword = GsiPasswordBox.Password;

        foreach (var rule in document.PreflightRules)
        {
            if (!preflightRuleEditors.TryGetValue(rule.RuleId, out var editor))
            {
                continue;
            }

            if (!rule.Locked && editor.EnabledCheckBox is not null)
            {
                rule.Enabled = editor.EnabledCheckBox.IsChecked == true;
            }

            rule.Severity = GetSelectedTag(editor.SeverityComboBox, rule.Severity);
        }

        foreach (var rule in document.ReadinessRules)
        {
            if (!readinessRuleEditors.TryGetValue(rule.RuleId, out var editor))
            {
                continue;
            }

            rule.Enabled = editor.EnabledCheckBox.IsChecked == true;
            rule.Severity = GetSelectedTag(editor.SeverityComboBox, rule.Severity);
            if (editor.MinSegmentCountTextBox is not null)
            {
                rule.MinSegmentCount = ParsePositiveInt(editor.MinSegmentCountTextBox.Text, rule.MinSegmentCount);
            }

            if (editor.BehaviorCheckBox is not null)
            {
                var behaviorValue = editor.BehaviorCheckBox.IsChecked == true;
                switch (rule.Category)
                {
                    case "boundary_completeness":
                        rule.RequireContiguousSequence = behaviorValue;
                        break;
                    case "line_without_point_support":
                        rule.RequireReferencedPoints = behaviorValue;
                        break;
                    case "orphan_line_detection":
                        rule.RequireChainConsistency = behaviorValue;
                        break;
                    case "shared_edge_consistency":
                        rule.DetectDuplicateEdges = behaviorValue;
                        break;
                }
            }
        }

        return document;
    }

    private void RenderPreflightRules(IEnumerable<EditablePreflightRule> rules)
    {
        PreflightRulesEditorPanel.Children.Clear();
        preflightRuleEditors.Clear();

        foreach (var rule in rules.OrderBy(rule => rule.SectionName).ThenBy(rule => rule.Locked).ThenBy(rule => rule.Group).ThenBy(rule => rule.Category).ThenBy(rule => rule.DisplayName))
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(216, 223, 228)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var titleBlock = new TextBlock
            {
                Text = $"{rule.DisplayName}{Environment.NewLine}{rule.Group} / {rule.Category}",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(titleBlock, 0);
            grid.Children.Add(titleBlock);

            CheckBox? enabledCheckBox = null;
            if (rule.Locked)
            {
                var lockedText = new TextBlock
                {
                    Text = "Locked",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(lockedText, 1);
                grid.Children.Add(lockedText);
            }
            else
            {
                enabledCheckBox = new CheckBox
                {
                    Content = "Enabled",
                    IsChecked = rule.Enabled,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(enabledCheckBox, 1);
                grid.Children.Add(enabledCheckBox);
            }

            var severityComboBox = new ComboBox
            {
                Margin = new Thickness(0, 0, 8, 0),
                IsEnabled = !rule.Locked,
                VerticalAlignment = VerticalAlignment.Center
            };
            severityComboBox.Items.Add(new ComboBoxItem { Tag = "warning", Content = "Warning" });
            severityComboBox.Items.Add(new ComboBoxItem { Tag = "blocker", Content = "Blocker" });
            severityComboBox.Items.Add(new ComboBoxItem { Tag = "configured", Content = "Configured" });
            SetSelectedTag(severityComboBox, rule.Severity);
            Grid.SetColumn(severityComboBox, 2);
            grid.Children.Add(severityComboBox);

            var descriptionBlock = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(rule.RequiredCadLayerSummary)
                    ? rule.Description
                    : $"{rule.Description}{Environment.NewLine}CAD aliases: {rule.RequiredCadLayerSummary}",
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(descriptionBlock, 3);
            grid.Children.Add(descriptionBlock);

            border.Child = grid;
            PreflightRulesEditorPanel.Children.Add(border);
            preflightRuleEditors[rule.RuleId] = new PreflightRuleEditorControls(enabledCheckBox, severityComboBox);
        }
    }

    private void RenderReadinessRules(IEnumerable<EditableReadinessRule> rules)
    {
        ReadinessRulesEditorPanel.Children.Clear();
        readinessRuleEditors.Clear();

        foreach (var rule in rules
                     .OrderBy(rule => rule.IsDefaultFallback ? 0 : 1)
                     .ThenBy(rule => rule.ParcelType, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(rule => rule.Category, StringComparer.OrdinalIgnoreCase))
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(216, 223, 228)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(8)
            };

            var stack = new StackPanel();

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var titleBlock = new TextBlock
            {
                Text = $"{rule.Title}{Environment.NewLine}{rule.ScopeSummary}",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(titleBlock, 0);
            header.Children.Add(titleBlock);

            var enabledCheckBox = new CheckBox
            {
                Content = "Enabled",
                IsChecked = rule.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(enabledCheckBox, 1);
            header.Children.Add(enabledCheckBox);

            var severityComboBox = new ComboBox
            {
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            severityComboBox.Items.Add(new ComboBoxItem { Tag = "blocker", Content = "Blocker" });
            severityComboBox.Items.Add(new ComboBoxItem { Tag = "warning", Content = "Warning" });
            severityComboBox.Items.Add(new ComboBoxItem { Tag = "info", Content = "Info" });
            SetSelectedTag(severityComboBox, rule.Severity);
            Grid.SetColumn(severityComboBox, 2);
            header.Children.Add(severityComboBox);

            var categoryBlock = new TextBlock
            {
                Text = $"Category: {rule.Category.Replace('_', ' ')}",
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(categoryBlock, 3);
            header.Children.Add(categoryBlock);

            stack.Children.Add(header);

            var detailGrid = new Grid
            {
                Margin = new Thickness(0, 8, 0, 0)
            };
            detailGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            detailGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBox? minSegmentCountTextBox = null;
            CheckBox? behaviorCheckBox = null;

            if (string.Equals(rule.Category, "minimum_segment_count", StringComparison.OrdinalIgnoreCase))
            {
                detailGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var label = new TextBlock
                {
                    Text = "Minimum segment count",
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 6, 8, 6)
                };
                minSegmentCountTextBox = new TextBox
                {
                    Text = rule.MinSegmentCount.ToString(),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                Grid.SetColumn(label, 0);
                Grid.SetColumn(minSegmentCountTextBox, 1);
                detailGrid.Children.Add(label);
                detailGrid.Children.Add(minSegmentCountTextBox);
            }
            else
            {
                detailGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                behaviorCheckBox = new CheckBox
                {
                    Content = GetReadinessBehaviorLabel(rule.Category),
                    IsChecked = GetReadinessBehaviorValue(rule),
                    Margin = new Thickness(0, 2, 0, 4)
                };
                Grid.SetColumnSpan(behaviorCheckBox, 2);
                detailGrid.Children.Add(behaviorCheckBox);
            }

            stack.Children.Add(detailGrid);
            border.Child = stack;
            ReadinessRulesEditorPanel.Children.Add(border);
            readinessRuleEditors[rule.RuleId] = new ReadinessRuleEditorControls(enabledCheckBox, severityComboBox, minSegmentCountTextBox, behaviorCheckBox);
        }
    }

    private void ApplyReviewWorkspacePresentation()
    {
        var mode = GetSelectedTag(ReviewWorkspaceModeComboBox, InnolaTransactionSettings.ReviewWorkspaceModeNormal);
        ReviewWorkspaceDescriptionTextBlock.Text = InnolaTransactionSettings.FormatReviewWorkspaceModeDescription(mode);

        var enterpriseModeSelected = string.Equals(
            mode,
            InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers,
            StringComparison.OrdinalIgnoreCase);
        var enterpriseParcelFabricSelected = string.Equals(
            mode,
            InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric,
            StringComparison.OrdinalIgnoreCase);

        EnterpriseWorkingExpander.Opacity = enterpriseModeSelected ? 1.0 : 0.58;
        EnterpriseWorkingExpander.IsExpanded = enterpriseModeSelected;
        EnterpriseParcelFabricExpander.Opacity = enterpriseParcelFabricSelected ? 1.0 : 0.58;
        EnterpriseParcelFabricExpander.IsExpanded = enterpriseParcelFabricSelected;
    }

    private void ApplyOpenAiExtractionProfilePresentation()
    {
        var profile = GetSelectedTag(OpenAiExtractionProfileComboBox, SettingsWorkspaceService.OpenAiExtractionProfileCustom);
        var recommendedModel = profile switch
        {
            SettingsWorkspaceService.OpenAiExtractionProfileBalanced => "gpt-4.1-mini",
            SettingsWorkspaceService.OpenAiExtractionProfileHighAccuracy => "gpt-4.1",
            _ => null
        };

        OpenAiModelTextBox.IsEnabled = string.Equals(profile, SettingsWorkspaceService.OpenAiExtractionProfileCustom, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(recommendedModel) && !OpenAiModelTextBox.IsKeyboardFocusWithin)
        {
            OpenAiModelTextBox.Text = recommendedModel;
        }
    }

    private void ApplyGsiPasswordModePresentation()
    {
        var mode = GetSelectedTag(GsiPasswordModeComboBox, SettingsWorkspaceService.GsiPasswordModeEnvironmentVariable);
        var useEnvironmentVariable = string.Equals(mode, SettingsWorkspaceService.GsiPasswordModeEnvironmentVariable, StringComparison.OrdinalIgnoreCase);
        GsiPasswordEnvironmentVariableTextBox.IsEnabled = useEnvironmentVariable;
        GsiPasswordBox.IsEnabled = !useEnvironmentVariable;
    }

    private void ShowWorkspaceWarnings(SettingsWorkspaceDocument document)
    {
        var warnings = new List<SettingsWorkspaceValidationMessage>();
        if (!string.IsNullOrWhiteSpace(document.SettingsWarning))
        {
            warnings.Add(new SettingsWorkspaceValidationMessage("Settings", "Summary", document.SettingsWarning));
        }

        if (!string.IsNullOrWhiteSpace(document.PreflightRulesWarning))
        {
            warnings.Add(new SettingsWorkspaceValidationMessage("Structure Rules", "Rule Catalog", document.PreflightRulesWarning));
        }

        if (warnings.Count == 0)
        {
            ValidationSummaryTextBlock.Visibility = Visibility.Collapsed;
            ValidationSummaryTextBlock.Text = string.Empty;
            return;
        }

        ValidationSummaryTextBlock.Text = string.Join(
            Environment.NewLine,
            warnings.Select(message => $"• {message.TabName} - {message.FieldName}: {message.Message}"));
        ValidationSummaryTextBlock.Visibility = Visibility.Visible;
    }

    private void ShowValidationMessages(IReadOnlyList<SettingsWorkspaceValidationMessage> messages)
    {
        ValidationSummaryTextBlock.Text = string.Join(
            Environment.NewLine,
            messages.Select(message => $"• {message.TabName} - {message.FieldName}: {message.Message}"));
        ValidationSummaryTextBlock.Visibility = Visibility.Visible;
        FooterStatusTextBlock.Text = "Settings were not saved because validation found issues.";

        var firstTab = messages[0].TabName;
        foreach (var item in SettingsTabControl.Items.OfType<TabItem>())
        {
            if (string.Equals(item.Header?.ToString(), firstTab, StringComparison.OrdinalIgnoreCase))
            {
                SettingsTabControl.SelectedItem = item;
                break;
            }
        }
    }

    private static string BuildEnterpriseAdminRuntimeSummary(SettingsWorkspaceDocument document)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                $"Service root: {BlankIfEmpty(document.EnterpriseWorkingServiceRoot)}",
                $"Admin portal URL: {BlankIfEmpty(document.EnterpriseWorkingAdminPortalUrl)}",
                $"Workspace: {BlankIfEmpty(document.EnterpriseWorkingWorkspaceName)}",
                $"Publish behavior: {BlankIfEmpty(document.EnterpriseWorkingPublishBehavior)}",
                $"Transaction scope field: {BlankIfEmpty(document.EnterpriseWorkingTransactionScopeField)}",
                $"Points: {BlankIfEmpty(document.EnterpriseWorkingPointsLayer)}",
                $"Lines: {BlankIfEmpty(document.EnterpriseWorkingLinesLayer)}",
                $"Polygons: {BlankIfEmpty(document.EnterpriseWorkingPolygonsLayer)}",
                $"Case index: {BlankIfEmpty(document.EnterpriseWorkingCaseIndexLayer)}",
                $"Issues: {BlankIfEmpty(document.EnterpriseWorkingIssuesLayer)}"
            });
    }

    private static string BlankIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(not configured)" : value.Trim();
    }

    private static int ParsePositiveInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static List<string> SplitLines(string value)
    {
        return value
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void SetSelectedTag(ComboBox comboBox, string? tag)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
    }

    private static string GetSelectedTag(ComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem is ComboBoxItem item && item.Tag is not null
            ? item.Tag.ToString() ?? fallback
            : fallback;
    }

    private static string GetReadinessBehaviorLabel(string category)
    {
        return category switch
        {
            "boundary_completeness" => "Require contiguous parcel sequence",
            "line_without_point_support" => "Require all referenced points",
            "orphan_line_detection" => "Require clean parcel chain continuity",
            "shared_edge_consistency" => "Detect duplicate / shared-edge conflicts",
            _ => "Enable rule behavior"
        };
    }

    private static bool GetReadinessBehaviorValue(EditableReadinessRule rule)
    {
        return rule.Category switch
        {
            "boundary_completeness" => rule.RequireContiguousSequence,
            "line_without_point_support" => rule.RequireReferencedPoints,
            "orphan_line_detection" => rule.RequireChainConsistency,
            "shared_edge_consistency" => rule.DetectDuplicateEdges,
            _ => true
        };
    }

    private sealed record PreflightRuleEditorControls(
        CheckBox? EnabledCheckBox,
        ComboBox SeverityComboBox);

    private sealed record ReadinessRuleEditorControls(
        CheckBox EnabledCheckBox,
        ComboBox SeverityComboBox,
        TextBox? MinSegmentCountTextBox,
        CheckBox? BehaviorCheckBox);

    private sealed record EnterpriseAdminProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
