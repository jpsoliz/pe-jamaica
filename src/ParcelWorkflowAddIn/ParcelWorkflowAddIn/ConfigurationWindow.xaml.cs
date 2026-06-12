using ArcGIS.Desktop.Framework.Controls;
using System.Windows;
using System.IO;
using System.Text.Json;
using ParcelWorkflowAddIn.Innola;

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
        SettingsPathText.Text = settingsPath;

        if (!File.Exists(settingsPath))
        {
            ModeText.Text = "mock";
            ServerText.Text = ShellState.ConfiguredServerUrl;
            ProcessStepText.Text = ShellState.TransactionProcessStep;
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
        ModeText.Text = ReadString(root, "innola_transaction_mode") ?? "mock";
        ServerText.Text = ReadString(root, "innola_server_url") ?? ShellState.ConfiguredServerUrl;
        ProcessStepText.Text = ReadString(root, "innola_process_step") ?? ShellState.TransactionProcessStep;
        CaseFolderRootText.Text = ReadString(root, "case_folder_output_root") ?? "Default";
        PythonText.Text = ReadString(root, "arcgis_python_executable") ?? "Default";
        OcrEngineText.Text = ReadString(root, "ocr_engine") ?? "local";
        OpenAiEnabledText.Text = (ReadBool(root, "openai_enabled") ?? false).ToString();
        OpenAiModelText.Text = ReadString(root, "openai_model") ?? "Not configured";
        OpenAiKeySourceText.Text = $"Environment variable {ReadString(root, "openai_api_key_environment_variable") ?? "OPENAI_API_KEY"}";
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
