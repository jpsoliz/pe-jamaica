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
        TestAssert.True(!settings.SpatialOutputAddCogoAttributes, "Missing configuration should default optional COGO attributes off.");
        TestAssert.True(!settings.SpatialOutputAddCogoLabels, "Missing configuration should default optional COGO labels off.");
        TestAssert.Equal("source_then_computed", settings.SpatialOutputCogoSourceMode, "Missing configuration should default to source-then-computed mode.");
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

    public static void WorkflowExecutionSettingsUsesParcelFabricTimeoutWhenEnterpriseParcelFabricModeRequiresIt()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "review_workspace_mode": "enterprise_parcel_fabric"
            }
            """);

        var settings = WorkflowExecutionSettings.Load(settingsFile.Path);

        TestAssert.Equal(
            InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric,
            settings.ReviewWorkspaceMode,
            "Enterprise Parcel Fabric review workspace mode should be preserved for execution settings.");
        TestAssert.Equal(
            WorkflowExecutionSettings.DefaultOutputAdapterTimeoutSecondsParcelFabric,
            settings.OutputAdapterTimeoutSeconds,
            "Enterprise Parcel Fabric mode should use the longer parcel-fabric timeout.");
    }

    public static void WorkflowExecutionSettingsLoadsOptionalNonFabricCogoToggles()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "spatial_output_add_cogo_attributes": true,
              "spatial_output_add_cogo_labels": true,
              "spatial_output_cogo_source_mode": "prefer_computed"
            }
            """);

        var settings = WorkflowExecutionSettings.Load(settingsFile.Path);

        TestAssert.True(settings.SpatialOutputAddCogoAttributes, "Optional COGO attributes flag should load from configuration.");
        TestAssert.True(settings.SpatialOutputAddCogoLabels, "Optional COGO labels flag should load from configuration.");
        TestAssert.Equal("prefer_computed", settings.SpatialOutputCogoSourceMode, "Optional COGO source mode should load from configuration.");
    }

    private static TempFile WriteSettingsFile(string json)
    {
        var tempFile = new TempFile();
        File.WriteAllText(tempFile.Path, json);
        return tempFile;
    }
}
