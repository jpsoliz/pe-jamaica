using ArcGIS.Desktop.Framework.Controls;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Settings;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ParcelWorkflowAddIn;

public partial class ConfigurationWindow : ProWindow
{
    private readonly SettingsWorkspaceService settingsWorkspaceService = new();
    private readonly Dictionary<string, PreflightRuleEditorControls> preflightRuleEditors = new(StringComparer.OrdinalIgnoreCase);
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
        SettingsSummaryText.Text = $"Edit local parcel workflow settings using the tabs below. Active rules file: {document.PreflightRulesPath}";

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

        return document;
    }

    private void RenderPreflightRules(IEnumerable<EditablePreflightRule> rules)
    {
        PreflightRulesEditorPanel.Children.Clear();
        preflightRuleEditors.Clear();

        foreach (var rule in rules.OrderBy(rule => rule.Locked).ThenBy(rule => rule.Category).ThenBy(rule => rule.DisplayName))
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
                Text = $"{rule.DisplayName}{Environment.NewLine}{rule.Category}",
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
                Text = rule.Description,
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

    private void ApplyReviewWorkspacePresentation()
    {
        var mode = GetSelectedTag(ReviewWorkspaceModeComboBox, InnolaTransactionSettings.ReviewWorkspaceModeNormal);
        ReviewWorkspaceDescriptionTextBlock.Text = InnolaTransactionSettings.FormatReviewWorkspaceModeDescription(mode);

        var enterpriseModeSelected = string.Equals(
            mode,
            InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers,
            StringComparison.OrdinalIgnoreCase);

        EnterpriseWorkingExpander.Opacity = enterpriseModeSelected ? 1.0 : 0.58;
        EnterpriseWorkingExpander.IsExpanded = enterpriseModeSelected;
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
            warnings.Add(new SettingsWorkspaceValidationMessage("Preflight Rules", "Rule Catalog", document.PreflightRulesWarning));
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

    private sealed record PreflightRuleEditorControls(
        CheckBox? EnabledCheckBox,
        ComboBox SeverityComboBox);
}
