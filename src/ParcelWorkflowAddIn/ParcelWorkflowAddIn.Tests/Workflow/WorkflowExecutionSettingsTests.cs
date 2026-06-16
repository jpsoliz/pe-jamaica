using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Execution;
using System.IO;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class WorkflowExecutionSettingsTests
{
    public static void WorkflowExecutionSettingsDefaultsToNormalTimeoutWhenConfigMissing()
    {
        using var missingFile = new TempFile();

        var settings = WorkflowExecutionSettings.Load(missingFile.Path);

        TestAssert.Equal(
            WorkflowExecutionSettings.DefaultOutputAdapterTimeoutSecondsNormal,
            settings.OutputAdapterTimeoutSeconds,
            "Missing configuration should default to the normal output timeout.");
    }

    public static void WorkflowExecutionSettingsUsesParcelFabricTimeoutWhenModeRequiresIt()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "review_workspace_mode": "parcel_fabric_local"
            }
            """);

        var settings = WorkflowExecutionSettings.Load(settingsFile.Path);

        TestAssert.Equal(
            InnolaTransactionSettings.ReviewWorkspaceModeParcelFabricLegacy,
            settings.ReviewWorkspaceMode,
            "Parcel fabric review workspace mode should normalize for execution settings.");
        TestAssert.Equal(
            WorkflowExecutionSettings.DefaultOutputAdapterTimeoutSecondsParcelFabric,
            settings.OutputAdapterTimeoutSeconds,
            "Parcel fabric mode should use the longer output timeout.");
    }

    public static void WorkflowExecutionSettingsHonorsExplicitOutputTimeoutOverride()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "review_workspace_mode": "parcel_fabric_local",
              "output_adapter_timeout_seconds": 900
            }
            """);

        var settings = WorkflowExecutionSettings.Load(settingsFile.Path);

        TestAssert.Equal(900, settings.OutputAdapterTimeoutSeconds, "Explicit output timeout should override the mode default.");
    }

    private static TempFile WriteSettingsFile(string json)
    {
        var tempFile = new TempFile();
        File.WriteAllText(tempFile.Path, json);
        return tempFile;
    }
}
