using ArcGIS.Desktop.Framework.Controls;
using System.Windows;
using System.IO;
using System.Text.Json;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Preflight;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;

namespace ParcelWorkflowAddIn;

public partial class ConfigurationWindow : ProWindow
{
    public ConfigurationWindow()
    {
        InitializeComponent();
        LoadSettingsSummary();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LoadSettingsSummary()
    {
        var settingsPath = InnolaTransactionSettings.ResolveActiveSettingsPath();
        var transactionSettings = InnolaTransactionSettings.Load();
        SettingsPathText.Text = settingsPath;
        ReviewWorkspaceModeText.Text = InnolaTransactionSettings.FormatReviewWorkspaceMode(transactionSettings.ReviewWorkspaceMode);
        ReviewWorkspaceModeWarningText.Text = transactionSettings.ReviewWorkspaceModeWarning ?? string.Empty;
        ReviewWorkspaceModeWarningText.Visibility = string.IsNullOrWhiteSpace(transactionSettings.ReviewWorkspaceModeWarning)
            ? Visibility.Collapsed
            : Visibility.Visible;
        SupportedTransactionTypesText.Text = InnolaTransactionSettings.FormatSupportedTransactionTypesDisplay(transactionSettings.SupportedTransactionTypes);
        SupportedTransactionTypesWarningText.Text = transactionSettings.SupportedTransactionTypesWarning ?? string.Empty;
        SupportedTransactionTypesWarningText.Visibility = string.IsNullOrWhiteSpace(transactionSettings.SupportedTransactionTypesWarning)
            ? Visibility.Collapsed
            : Visibility.Visible;
        ComputeWorkflowStagesText.Text = InnolaTransactionSettings.FormatNamedListDisplay(transactionSettings.ComputeWorkflowStages, "No compute workflow stages configured.");
        ComputeWorkflowStagesWarningText.Text = transactionSettings.ComputeWorkflowStagesWarning ?? string.Empty;
        ComputeWorkflowStagesWarningText.Visibility = string.IsNullOrWhiteSpace(transactionSettings.ComputeWorkflowStagesWarning)
            ? Visibility.Collapsed
            : Visibility.Visible;
        var ruleCatalog = new PreflightRuleCatalogLoader().Load();
        PreflightRulesPathText.Text = ruleCatalog.SourcePath;
        PreflightRulesWarningText.Text = ruleCatalog.LoadWarning ?? string.Empty;
        PreflightRulesWarningText.Visibility = string.IsNullOrWhiteSpace(ruleCatalog.LoadWarning) ? Visibility.Collapsed : Visibility.Visible;
        RenderPreflightRules(ruleCatalog);

        if (!File.Exists(settingsPath))
        {
            ModeText.Text = transactionSettings.Mode;
            ServerText.Text = transactionSettings.ServerUrl;
            ProcessStepText.Text = transactionSettings.ProcessStep;
            CaseFolderRootText.Text = "Default";
            PythonText.Text = "Default";
            OcrEngineText.Text = "local";
            OpenAiEnabledText.Text = "False";
            OpenAiModelText.Text = "Not configured";
            OpenAiKeySourceText.Text = "Environment variable OPENAI_API_KEY";
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var root = document.RootElement;
        ModeText.Text = transactionSettings.Mode;
        ServerText.Text = transactionSettings.ServerUrl;
        ProcessStepText.Text = transactionSettings.ProcessStep;
        CaseFolderRootText.Text = ReadString(root, "case_folder_output_root") ?? "Default";
        PythonText.Text = ReadString(root, "arcgis_python_executable") ?? "Default";
        OcrEngineText.Text = ReadString(root, "ocr_engine") ?? "local";
        OpenAiEnabledText.Text = (ReadBool(root, "openai_enabled") ?? false).ToString();
        OpenAiModelText.Text = ReadString(root, "openai_model") ?? "Not configured";
        OpenAiKeySourceText.Text = $"Environment variable {ReadString(root, "openai_api_key_environment_variable") ?? "OPENAI_API_KEY"}";
    }

    private void RenderPreflightRules(PreflightRuleCatalog catalog)
    {
        PreflightRulesPanel.Children.Clear();
        foreach (var rule in catalog.Rules.OrderBy(rule => rule.Locked).ThenBy(rule => rule.Category).ThenBy(rule => rule.DisplayName))
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(216, 223, 228)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(8, 6, 8, 6)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            AddRuleCell(grid, 0, $"{rule.DisplayName}\n{rule.Category}");
            AddRuleCell(grid, 1, rule.Locked ? "Locked" : (rule.Enabled ? "Enabled" : "Disabled"));
            AddRuleCell(grid, 2, rule.Severity);
            AddRuleCell(grid, 3, rule.Description);

            border.Child = grid;
            PreflightRulesPanel.Children.Add(border);
        }
    }

    private static void AddRuleCell(Grid grid, int column, string text)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(textBlock, column);
        grid.Children.Add(textBlock);
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool? ReadBool(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;
    }
}
