using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Workflow.FabricMaintenance;

public enum FabricMaintenanceTarget
{
    None,
    Legal,
    Fiscal
}

public enum FabricMaintenancePromotionDecision
{
    None,
    ReplaceExisting,
    KeepExistingDiscardWorking,
    MergeUpdateAttributesOnly,
    SendBackForReview
}

public enum FabricMaintenanceCheckSeverity
{
    Pass,
    Warning,
    Blocking
}

public sealed record FabricMaintenancePromotionSettings(
    bool Enabled,
    string SubworkflowName,
    string StageName,
    string SpatialUnitExaminationField,
    EnterpriseWorkingReviewSettings WorkingReview,
    CompareEnterpriseCadasterSettings FinalCadastre)
{
    public static FabricMaintenancePromotionSettings Default { get; } = new(
        true,
        "Parcel Fabric Maintenance",
        "In Parcel Fabric Update",
        "examinationNumber",
        EnterpriseWorkingReviewSettings.Default,
        CompareEnterpriseCadasterSettings.Default);

    public static FabricMaintenancePromotionSettings FromTransactionSettings(InnolaTransactionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.FabricMaintenancePromotion;
    }

    public static FabricMaintenancePromotionSettings FromJson(
        JsonElement root,
        EnterpriseWorkingReviewSettings workingReview,
        CompareEnterpriseCadasterSettings finalCadastre)
    {
        var settings = Default with
        {
            WorkingReview = workingReview,
            FinalCadastre = finalCadastre
        };

        if (!root.TryGetProperty("fabric_maintenance_promotion", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return settings;
        }

        return settings with
        {
            Enabled = ReadBool(value, "enabled") ?? settings.Enabled,
            SubworkflowName = ReadString(value, "subworkflow_name") ?? settings.SubworkflowName,
            StageName = ReadString(value, "stage_name") ?? settings.StageName,
            SpatialUnitExaminationField = ReadString(value, "spatial_unit_examination_field") ?? settings.SpatialUnitExaminationField
        };
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;
    }

    private static bool? ReadBool(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;
    }
}

public sealed record FabricMaintenancePromotionGateResult(bool IsEligible, string? Reason);

public static class FabricMaintenancePromotionGate
{
    public static FabricMaintenancePromotionGateResult Evaluate(
        InnolaTransactionRow? row,
        FabricMaintenancePromotionSettings settings)
    {
        if (!settings.Enabled)
        {
            return new FabricMaintenancePromotionGateResult(false, "Fabric Maintenance promotion is disabled in settings.");
        }

        if (row is null)
        {
            return new FabricMaintenancePromotionGateResult(false, "Select a Fabric Maintenance transaction.");
        }

        if (!row.IsAvailable && row.Status != InnolaTransactionStatus.InProgress)
        {
            return new FabricMaintenancePromotionGateResult(false, "Fabric Maintenance requires a started or available transaction.");
        }

        if (!TextEquals(row.TaskName, settings.StageName))
        {
            return new FabricMaintenancePromotionGateResult(false, $"Fabric Maintenance requires stage '{settings.StageName}'.");
        }

        if (!string.IsNullOrWhiteSpace(row.SubworkflowName)
            && !TextEquals(row.SubworkflowName, settings.SubworkflowName))
        {
            return new FabricMaintenancePromotionGateResult(false, $"Fabric Maintenance requires subworkflow '{settings.SubworkflowName}'.");
        }

        if (!string.IsNullOrWhiteSpace(row.SubworkflowName))
        {
            return new FabricMaintenancePromotionGateResult(true, null);
        }

        return new FabricMaintenancePromotionGateResult(true, null);
    }

    private static bool TextEquals(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}

public sealed record FabricMaintenancePromotionTargetSelection(
    bool IsValid,
    FabricMaintenanceTarget Target,
    string DisplayLabel,
    string Message)
{
    public static FabricMaintenancePromotionTargetSelection FromFlags(bool legalSelected, bool fiscalSelected)
    {
        if (legalSelected == fiscalSelected)
        {
            return new FabricMaintenancePromotionTargetSelection(
                false,
                FabricMaintenanceTarget.None,
                string.Empty,
                legalSelected
                    ? "Select exactly one final cadastre target."
                    : "Select Legal or Cadastral before reviewing final candidates.");
        }

        return legalSelected
            ? new FabricMaintenancePromotionTargetSelection(true, FabricMaintenanceTarget.Legal, "Legal", "Legal target selected.")
            : new FabricMaintenancePromotionTargetSelection(true, FabricMaintenanceTarget.Fiscal, "Cadastral", "Cadastral target selected.");
    }
}

public sealed record FabricMaintenancePromotionContextResult(
    bool IsReady,
    string CurrentTransactionNumber,
    string PeNumber,
    string Message);

public sealed class FabricMaintenancePromotionContextResolver
{
    private readonly FabricMaintenancePromotionSettings settings;

    public FabricMaintenancePromotionContextResolver(FabricMaintenancePromotionSettings settings)
    {
        this.settings = settings;
    }

    public FabricMaintenancePromotionContextResult Resolve(InnolaTransactionRow? activeTransaction, string? spatialUnitExaminationNumber)
    {
        if (activeTransaction is null || string.IsNullOrWhiteSpace(activeTransaction.TransactionNumber))
        {
            return new FabricMaintenancePromotionContextResult(
                false,
                string.Empty,
                string.Empty,
                "Fabric Maintenance requires an active Innola transaction before promotion review can open.");
        }

        var peNumber = spatialUnitExaminationNumber?.Trim();
        if (string.IsNullOrWhiteSpace(peNumber))
        {
            return new FabricMaintenancePromotionContextResult(
                false,
                activeTransaction.TransactionNumber,
                string.Empty,
                $"SpatialUnitExt.{settings.SpatialUnitExaminationField} did not resolve a PE/examination number. Resolve it before promotion.");
        }

        return new FabricMaintenancePromotionContextResult(
            true,
            activeTransaction.TransactionNumber.Trim(),
            peNumber,
            "Fabric Maintenance context is ready.");
    }
}

public sealed record FabricMaintenanceLayerRequest(string Role, string? LayerUrl, string Where, bool ReturnGeometry);

public sealed record FabricMaintenanceWorkingReviewPlan(
    bool IsValid,
    string Message,
    string ScopeField,
    string ScopeValue,
    IReadOnlyList<FabricMaintenanceLayerRequest> LayerRequests);

public static class FabricMaintenanceWorkingReviewPlanner
{
    public static FabricMaintenanceWorkingReviewPlan BuildPlan(
        FabricMaintenancePromotionSettings settings,
        string? currentTransactionNumber,
        string? peNumber)
    {
        _ = currentTransactionNumber;
        var scopeValue = peNumber?.Trim();
        if (string.IsNullOrWhiteSpace(scopeValue))
        {
            return new FabricMaintenanceWorkingReviewPlan(false, "Parcel in Review is required for working_review lookup.", string.Empty, string.Empty, Array.Empty<FabricMaintenanceLayerRequest>());
        }

        var review = settings.WorkingReview;
        var scopeField = string.IsNullOrWhiteSpace(review.TransactionScopeField)
            ? "transaction_number"
            : review.TransactionScopeField.Trim();
        var where = $"{scopeField} = '{EscapeSqlLiteral(scopeValue)}'";
        var requests = new[]
        {
            new FabricMaintenanceLayerRequest("points", review.Layers.Points, where, true),
            new FabricMaintenanceLayerRequest("lines", review.Layers.Lines, where, true),
            new FabricMaintenanceLayerRequest("polygons", review.Layers.Polygons, where, true),
            new FabricMaintenanceLayerRequest("case_index", review.Layers.CaseIndex, where, false)
        };
        var missing = requests.Where(request => string.IsNullOrWhiteSpace(request.LayerUrl)).Select(request => request.Role).ToArray();
        if (missing.Length > 0)
        {
            return new FabricMaintenanceWorkingReviewPlan(false, $"working_review target layer(s) are not configured: {string.Join(", ", missing)}.", scopeField, scopeValue, requests);
        }

        return new FabricMaintenanceWorkingReviewPlan(true, "working_review query plan is ready for Parcel in Review.", scopeField, scopeValue, requests);
    }

    private static string EscapeSqlLiteral(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}

public sealed record FabricMaintenanceCandidateSearchKeys(
    string? Pid,
    string? LotNumber,
    string? PeNumber,
    string? BaUnitOrTenureId);

public sealed record FabricMaintenanceFinalTargetQueryPlan(
    bool IsValid,
    string Message,
    FabricMaintenanceTarget Target,
    string TargetLabel,
    CompareEnterpriseCadasterSourceSettings Source,
    string CanonicalLayerName,
    string CanonicalPidField,
    string EvidenceWhere,
    string SpatialCandidateWhere,
    IReadOnlyList<string> OutFields,
    bool IncludesSpatialRelationshipCheck);

public static class FabricMaintenanceFinalTargetQueryPlanner
{
    public static FabricMaintenanceFinalTargetQueryPlan BuildPlan(
        FabricMaintenancePromotionSettings settings,
        FabricMaintenanceTarget target,
        FabricMaintenanceCandidateSearchKeys keys)
    {
        if (target is not (FabricMaintenanceTarget.Legal or FabricMaintenanceTarget.Fiscal))
        {
            return Invalid(target, "Select exactly one final cadastre target.");
        }

        var source = target == FabricMaintenanceTarget.Legal
            ? settings.FinalCadastre.Legal
            : settings.FinalCadastre.Fiscal;
        var targetLabel = target == FabricMaintenanceTarget.Legal ? "Legal" : "Cadastral";
        var canonicalLayer = target == FabricMaintenanceTarget.Legal
            ? "Legal_Parcel"
            : string.IsNullOrWhiteSpace(source.SublayerName) ? "Parcels" : source.SublayerName.Trim();
        var canonicalPidField = target == FabricMaintenanceTarget.Legal
            ? "PID"
            : string.IsNullOrWhiteSpace(source.PidField) ? "PID" : source.PidField.Trim();
        if (!source.Enabled)
        {
            return Invalid(target, $"{targetLabel} final cadastre source is disabled.");
        }

        if (string.IsNullOrWhiteSpace(source.LayerUrl))
        {
            return Invalid(target, $"{targetLabel} final cadastre layer_url is not configured.");
        }

        var identityClauses = new List<string>();
        AddEqualsClause(identityClauses, canonicalPidField, keys.Pid);
        AddEqualsClause(identityClauses, source.LotNumberField, keys.LotNumber);
        AddEqualsClause(identityClauses, source.PeNumberField, keys.PeNumber);
        AddEqualsClause(identityClauses, source.SuidField, keys.BaUnitOrTenureId);
        var where = identityClauses.Count == 0 ? "1=1" : string.Join(" OR ", identityClauses);
        if (target == FabricMaintenanceTarget.Fiscal)
        {
            where = $"({where}) AND parcel_status = 'active'";
        }

        var outFields = new[]
        {
            canonicalPidField,
            source.ParcelIdField,
            source.PidField,
            source.LandValuationNumberField,
            source.LotNumberField,
            source.PeNumberField,
            source.ParishField,
            source.SuidField,
            source.ObjectIdField,
            source.GlobalIdField
        }
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => field!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new FabricMaintenanceFinalTargetQueryPlan(
            true,
            $"{targetLabel} final cadastre query plan is ready.",
            target,
            targetLabel,
            source,
            canonicalLayer,
            canonicalPidField,
            where,
            "1=1",
            outFields,
            true);
    }

    private static FabricMaintenanceFinalTargetQueryPlan Invalid(FabricMaintenanceTarget target, string message)
    {
        return new FabricMaintenanceFinalTargetQueryPlan(
            false,
            message,
            target,
            target == FabricMaintenanceTarget.Fiscal ? "Cadastral" : "Legal",
            CompareEnterpriseCadasterSourceSettings.Disabled("unconfigured"),
            string.Empty,
            "PID",
            string.Empty,
            "1=1",
            Array.Empty<string>(),
            false);
    }

    private static void AddEqualsClause(ICollection<string> clauses, string? field, string? value)
    {
        if (!string.IsNullOrWhiteSpace(field) && !string.IsNullOrWhiteSpace(value))
        {
            clauses.Add($"{field.Trim()} = '{value.Trim().Replace("'", "''", StringComparison.Ordinal)}'");
        }
    }
}

public sealed record FabricMaintenanceFeatureCounts(int Points, int Lines, int Polygons, int CaseIndexRecords);

public sealed record FabricMaintenanceCheckResult(
    string Code,
    FabricMaintenanceCheckSeverity Severity,
    string Message);

public sealed record FabricMaintenanceReviewResultRow(
    string Source,
    string QueryKey,
    int Count,
    string SpatialRelationMode,
    string Status);

public sealed record FabricMaintenanceFinalCandidate(
    string Source,
    string? ObjectId,
    string? GlobalId,
    string? ParcelId,
    string? Pid,
    string SpatialRelationship,
    double OverlapArea,
    double OverlapPercent,
    string Status)
{
    public string CandidateId => FirstNonBlank(Pid, ParcelId, GlobalId, ObjectId) ?? "new final record candidate";

    private static string? FirstNonBlank(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}

public sealed record FabricMaintenanceReviewLoadPlan(
    bool IsValid,
    string Message,
    string CurrentTransactionNumber,
    string ParcelInReview,
    FabricMaintenanceTarget Target,
    string TargetLabel,
    string SpatialSearchMode,
    double BufferDistanceMeters,
    double RelationshipToleranceMeters,
    int ResultLimit,
    string SpatialRelationMode,
    int WorkingTransparencyPercent,
    int FinalTargetTransparencyPercent,
    FabricMaintenanceWorkingReviewPlan WorkingReviewPlan,
    FabricMaintenanceFinalTargetQueryPlan FinalTargetPlan);

public static class FabricMaintenanceReviewLoadPlanner
{
    public const int WorkingTransparencyPercent = 60;
    public const int FinalTargetTransparencyPercent = 70;

    public static FabricMaintenanceReviewLoadPlan BuildPlan(
        FabricMaintenancePromotionSettings settings,
        string currentTransactionNumber,
        string? parcelInReview,
        FabricMaintenanceTarget target)
    {
        var cleanParcel = parcelInReview?.Trim();
        if (target is not (FabricMaintenanceTarget.Legal or FabricMaintenanceTarget.Fiscal))
        {
            return Invalid("Select Legal or Cadastral before loading parcel review data.", currentTransactionNumber, cleanParcel, target);
        }

        if (string.IsNullOrWhiteSpace(cleanParcel))
        {
            return Invalid("Parcel in Review is required before loading parcel review data.", currentTransactionNumber, cleanParcel, target);
        }

        var workingPlan = FabricMaintenanceWorkingReviewPlanner.BuildPlan(settings, currentTransactionNumber, cleanParcel);
        if (!workingPlan.IsValid)
        {
            return Invalid(workingPlan.Message, currentTransactionNumber, cleanParcel, target, workingPlan);
        }

        var finalPlan = FabricMaintenanceFinalTargetQueryPlanner.BuildPlan(
            settings,
            target,
            new FabricMaintenanceCandidateSearchKeys(null, null, null, null));
        if (!finalPlan.IsValid)
        {
            return Invalid(finalPlan.Message, currentTransactionNumber, cleanParcel, target, workingPlan, finalPlan);
        }

        return new FabricMaintenanceReviewLoadPlan(
            true,
            "Fabric Maintenance parcel review load plan is ready.",
            currentTransactionNumber.Trim(),
            cleanParcel,
            target,
            finalPlan.TargetLabel,
            CompareEnterpriseCadasterSettings.NormalizeSpatialSearchMode(settings.FinalCadastre.SpatialSearchMode),
            settings.FinalCadastre.BufferDistanceMeters,
            settings.FinalCadastre.RelationshipToleranceMeters,
            settings.FinalCadastre.ResultLimit,
            FormatSpatialRelationMode(settings.FinalCadastre),
            WorkingTransparencyPercent,
            FinalTargetTransparencyPercent,
            workingPlan,
            finalPlan);
    }

    public static string FormatSpatialRelationMode(CompareEnterpriseCadasterSettings settings)
    {
        return string.Equals(settings.SpatialSearchMode, CompareEnterpriseCadasterSettings.SpatialSearchModeBuffer, StringComparison.OrdinalIgnoreCase)
            ? $"Surrounding parcels within {settings.BufferDistanceMeters:0.###} m"
            : "Intersect parcels only";
    }

    private static FabricMaintenanceReviewLoadPlan Invalid(
        string message,
        string currentTransactionNumber,
        string? parcelInReview,
        FabricMaintenanceTarget target,
        FabricMaintenanceWorkingReviewPlan? workingPlan = null,
        FabricMaintenanceFinalTargetQueryPlan? finalPlan = null)
    {
        return new FabricMaintenanceReviewLoadPlan(
            false,
            message,
            currentTransactionNumber.Trim(),
            parcelInReview ?? string.Empty,
            target,
            target == FabricMaintenanceTarget.Fiscal ? "Cadastral" : "Legal",
            CompareEnterpriseCadasterSettings.SpatialSearchModeIntersects,
            0,
            0,
            0,
            "Not selected",
            WorkingTransparencyPercent,
            FinalTargetTransparencyPercent,
            workingPlan ?? new FabricMaintenanceWorkingReviewPlan(false, message, string.Empty, string.Empty, Array.Empty<FabricMaintenanceLayerRequest>()),
            finalPlan ?? FabricMaintenanceFinalTargetQueryPlanner.BuildPlan(FabricMaintenancePromotionSettings.Default, FabricMaintenanceTarget.None, new FabricMaintenanceCandidateSearchKeys(null, null, null, null)));
    }
}

public sealed record FabricMaintenanceReviewLoadResult(
    bool Success,
    string Message,
    FabricMaintenanceFeatureCounts WorkingFeatureCounts,
    int FinalCandidateCount,
    IReadOnlyList<FabricMaintenanceReviewResultRow> ResultRows,
    IReadOnlyList<FabricMaintenanceFinalCandidate> FinalCandidates,
    IReadOnlyList<FabricMaintenanceCheckResult> TopologyChecks,
    IReadOnlyList<FabricMaintenanceCheckResult> AttributeChecks)
{
    public static FabricMaintenanceReviewLoadResult Failed(string message)
    {
        return new FabricMaintenanceReviewLoadResult(
            false,
            message,
            new FabricMaintenanceFeatureCounts(0, 0, 0, 0),
            0,
            Array.Empty<FabricMaintenanceReviewResultRow>(),
            Array.Empty<FabricMaintenanceFinalCandidate>(),
            Array.Empty<FabricMaintenanceCheckResult>(),
            Array.Empty<FabricMaintenanceCheckResult>());
    }
}

public interface IFabricMaintenanceReviewLoadService
{
    Task<FabricMaintenanceReviewLoadResult> LoadAsync(
        FabricMaintenanceReviewLoadPlan plan,
        CancellationToken cancellationToken = default);

    Task<FabricMaintenanceReviewCleanupResult> CleanupAsync(
        string currentTransactionNumber,
        CancellationToken cancellationToken = default);
}

public sealed record FabricMaintenanceReviewCleanupResult(bool Success, string Message);

public sealed class DeferredFabricMaintenanceReviewLoadService : IFabricMaintenanceReviewLoadService
{
    public Task<FabricMaintenanceReviewLoadResult> LoadAsync(
        FabricMaintenanceReviewLoadPlan plan,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        if (!plan.IsValid)
        {
            return Task.FromResult(FabricMaintenanceReviewLoadResult.Failed(plan.Message));
        }

        var rows = new[]
        {
            new FabricMaintenanceReviewResultRow("Working Review", $"{plan.WorkingReviewPlan.ScopeField} = {plan.WorkingReviewPlan.ScopeValue}", 0, plan.SpatialRelationMode, $"Ready to load with {plan.WorkingTransparencyPercent}% transparency."),
            new FabricMaintenanceReviewResultRow(plan.TargetLabel, "Spatial query from working parcel geometry", 0, plan.SpatialRelationMode, $"Ready to load with {plan.FinalTargetTransparencyPercent}% transparency.")
        };

        return Task.FromResult(new FabricMaintenanceReviewLoadResult(
            true,
            "Fabric Maintenance parcel review load was planned. ArcGIS Pro map execution is required for live counts.",
            new FabricMaintenanceFeatureCounts(0, 0, 0, 0),
            0,
            rows,
            Array.Empty<FabricMaintenanceFinalCandidate>(),
            FabricMaintenanceReviewEvidenceCatalog.TopologyChecks(0, 0),
            FabricMaintenanceReviewEvidenceCatalog.AttributeChecks()));
    }

    public Task<FabricMaintenanceReviewCleanupResult> CleanupAsync(
        string currentTransactionNumber,
        CancellationToken cancellationToken = default)
    {
        _ = currentTransactionNumber;
        _ = cancellationToken;
        return Task.FromResult(new FabricMaintenanceReviewCleanupResult(true, "Fabric Maintenance review context closed."));
    }
}

public sealed class ArcGisFabricMaintenanceReviewLoadService : IFabricMaintenanceReviewLoadService
{
    public async Task<FabricMaintenanceReviewLoadResult> LoadAsync(
        FabricMaintenanceReviewLoadPlan plan,
        CancellationToken cancellationToken = default)
    {
        if (!plan.IsValid)
        {
            return FabricMaintenanceReviewLoadResult.Failed(plan.Message);
        }

        var mapView = MapView.Active;
        if (mapView?.Map is null)
        {
            return FabricMaintenanceReviewLoadResult.Failed("No active ArcGIS Pro map is available. Open a map and retry Load Parcel.");
        }

        var warnings = new List<string>();
        var zoomLayers = new List<Layer>();
        FabricMaintenanceFeatureCounts workingCounts = new(0, 0, 0, 0);
        IReadOnlyList<FabricMaintenanceFinalCandidate> finalCandidates = Array.Empty<FabricMaintenanceFinalCandidate>();
        try
        {
            await QueuedTask.Run(() =>
            {
                var group = EnsureGroupLayer(mapView.Map, $"Fabric Maintenance Review - {plan.CurrentTransactionNumber}");
                ClearGroupLayer(mapView.Map, group);
                var workingGeometries = new List<Geometry>();
                var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var request in plan.WorkingReviewPlan.LayerRequests)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(request.LayerUrl))
                    {
                        continue;
                    }

                    var layer = LayerFactory.Instance.CreateLayer(new Uri(request.LayerUrl), group, 0, $"working_review {request.Role} - {plan.ParcelInReview}");
                    if (layer is not FeatureLayer featureLayer)
                    {
                        continue;
                    }

                    ApplyDefinitionQuery(featureLayer, request.Where);
                    featureLayer.SetTransparency(plan.WorkingTransparencyPercent);
                    featureLayer.SetEditable(false);
                    var count = CountFeatures(featureLayer, request.Where) ?? 0;
                    counts[request.Role] = count;
                        if (request.Role.Equals("polygons", StringComparison.OrdinalIgnoreCase))
                        {
                            zoomLayers.Add(featureLayer);
                            workingGeometries.AddRange(ReadFeatureGeometries(featureLayer, request.Where));
                        }
                }

                workingCounts = new FabricMaintenanceFeatureCounts(
                    counts.GetValueOrDefault("points"),
                    counts.GetValueOrDefault("lines"),
                    counts.GetValueOrDefault("polygons"),
                    counts.GetValueOrDefault("case_index"));

                finalCandidates = LoadFinalTargetContext(mapView.Map, group, plan, workingGeometries, warnings, cancellationToken);
            }).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverableArcGisException(exception))
        {
            return FabricMaintenanceReviewLoadResult.Failed($"Fabric Maintenance review layers could not be loaded into the active map: {exception.Message}");
        }

        var zoomed = false;
        if (zoomLayers.Count > 0)
        {
            try
            {
                await mapView.ZoomToAsync(zoomLayers).ConfigureAwait(false);
                zoomed = true;
            }
            catch (Exception exception) when (IsRecoverableArcGisException(exception))
            {
                warnings.Add($"Map could not zoom to the working parcel: {exception.Message}");
            }
        }

        var finalCandidateCount = finalCandidates.Count;
        var finalQueryFailed = warnings.Any(warning => warning.Contains("spatial candidate query failed", StringComparison.OrdinalIgnoreCase));
        var rows = new[]
        {
            new FabricMaintenanceReviewResultRow("Working Review", $"{plan.WorkingReviewPlan.ScopeField} = {plan.WorkingReviewPlan.ScopeValue}", workingCounts.Polygons, plan.SpatialRelationMode, workingCounts.Polygons == 0 ? "No working parcel polygons found." : zoomed ? "Loaded into map and zoomed." : "Loaded into map; zoom not completed."),
            new FabricMaintenanceReviewResultRow(plan.TargetLabel, $"Spatial overlap query; attribute evidence: {plan.FinalTargetPlan.EvidenceWhere}", finalCandidateCount, plan.SpatialRelationMode, finalQueryFailed ? "Spatial candidate query failed; review status message." : finalCandidateCount == 0 ? "No final candidates found; new final record candidate." : "Spatial overlap candidates loaded into map.")
        };
        var message = warnings.Count == 0
            ? "Fabric Maintenance review context loaded into the active map and zoomed to the working parcel."
            : $"Fabric Maintenance review context loaded with warnings: {string.Join(" ", warnings)}";
        return new FabricMaintenanceReviewLoadResult(
            true,
            message,
            workingCounts,
            finalCandidateCount,
            rows,
            finalCandidates,
            FabricMaintenanceReviewEvidenceCatalog.TopologyChecks(workingCounts.Polygons, finalCandidateCount),
            FabricMaintenanceReviewEvidenceCatalog.AttributeChecks());
    }

    public async Task<FabricMaintenanceReviewCleanupResult> CleanupAsync(
        string currentTransactionNumber,
        CancellationToken cancellationToken = default)
    {
        var mapView = MapView.Active;
        if (mapView?.Map is null)
        {
            return new FabricMaintenanceReviewCleanupResult(true, "Fabric Maintenance review window closed. No active map was available for layer cleanup.");
        }

        var groupLayerName = $"Fabric Maintenance Review - {currentTransactionNumber.Trim()}";
        try
        {
            await QueuedTask.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var groupLayer in mapView.Map.Layers.OfType<GroupLayer>()
                    .Where(layer => layer.Name.Equals(groupLayerName, StringComparison.OrdinalIgnoreCase))
                    .ToArray())
                {
                    mapView.Map.RemoveLayer(groupLayer);
                }
            }).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverableArcGisException(exception))
        {
            return new FabricMaintenanceReviewCleanupResult(false, $"Fabric Maintenance review layers could not be cleaned up: {exception.Message}");
        }

        return new FabricMaintenanceReviewCleanupResult(true, "Fabric Maintenance review layers cleaned up.");
    }


    private static IReadOnlyList<FabricMaintenanceFinalCandidate> LoadFinalTargetContext(
        Map map,
        GroupLayer group,
        FabricMaintenanceReviewLoadPlan plan,
        IReadOnlyList<Geometry> workingGeometries,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plan.FinalTargetPlan.Source.LayerUrl))
        {
            return Array.Empty<FabricMaintenanceFinalCandidate>();
        }

        var layer = LayerFactory.Instance.CreateLayer(new Uri(plan.FinalTargetPlan.Source.LayerUrl), group, 0, $"{plan.TargetLabel} candidates - {plan.ParcelInReview}");
        if (layer is not FeatureLayer featureLayer)
        {
            warnings.Add($"{plan.TargetLabel} target layer did not load as a feature layer.");
            return Array.Empty<FabricMaintenanceFinalCandidate>();
        }

        featureLayer.SetTransparency(plan.FinalTargetTransparencyPercent);
        featureLayer.SetEditable(false);
        var objectIdField = ResolveObjectIdField(featureLayer, plan.FinalTargetPlan.Source.ObjectIdField);
        var candidates = QueryFinalCandidates(featureLayer, workingGeometries, plan, objectIdField, warnings, cancellationToken);
        var objectIds = candidates
            .Select(candidate => long.TryParse(candidate.ObjectId, out var objectId) ? objectId : (long?)null)
            .Where(objectId => objectId.HasValue)
            .Select(objectId => objectId!.Value)
            .ToArray();
        ApplyDefinitionQuery(featureLayer, BuildObjectIdDefinitionQuery(objectIdField, objectIds));
        return candidates;
    }

    private static IReadOnlyList<FabricMaintenanceFinalCandidate> QueryFinalCandidates(
        FeatureLayer featureLayer,
        IReadOnlyList<Geometry> workingGeometries,
        FabricMaintenanceReviewLoadPlan plan,
        string objectIdField,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var candidatesByObjectId = new Dictionary<string, FabricMaintenanceFinalCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var geometry in workingGeometries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var queryGeometry = ResolveSpatialSearchGeometry(geometry, plan, warnings);
            try
            {
                using var cursor = featureLayer.Search(new SpatialQueryFilter
                {
                    FilterGeometry = queryGeometry,
                    SpatialRelationship = SpatialRelationship.Intersects,
                    WhereClause = plan.FinalTargetPlan.SpatialCandidateWhere,
                    RowCount = Math.Max(1, plan.ResultLimit)
                });
                while (cursor.MoveNext())
                {
                    if (cursor.Current is not Feature feature)
                    {
                        continue;
                    }

                    var objectId = ReadFieldValue(feature, featureLayer, objectIdField)
                        ?? feature.GetObjectID().ToString(CultureInfo.InvariantCulture);
                    if (string.IsNullOrWhiteSpace(objectId) || candidatesByObjectId.ContainsKey(objectId))
                    {
                        continue;
                    }

                    candidatesByObjectId[objectId] = BuildFinalCandidate(feature, featureLayer, geometry, plan, objectIdField);
                }
            }
            catch (Exception exception) when (IsRecoverableArcGisException(exception))
            {
                warnings.Add($"{plan.TargetLabel} spatial candidate query failed: {exception.Message}");
                break;
            }
        }

        return candidatesByObjectId.Values
            .OrderByDescending(candidate => candidate.OverlapPercent)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static FabricMaintenanceFinalCandidate BuildFinalCandidate(
        Feature feature,
        FeatureLayer featureLayer,
        Geometry workingGeometry,
        FabricMaintenanceReviewLoadPlan plan,
        string objectIdField)
    {
        var shape = feature.GetShape();
        var overlapArea = 0d;
        var overlapPercent = 0d;
        var relationship = CompareSpatialRelationship.IntersectsOnly;
        if (shape is not null && !shape.IsEmpty)
        {
            try
            {
                var normalizedShape = NormalizeForReview(shape, workingGeometry.SpatialReference);
                var intersection = GeometryEngine.Instance.Intersection(normalizedShape, workingGeometry);
                overlapArea = intersection is null || intersection.IsEmpty ? 0d : Math.Max(0d, GeometryEngine.Instance.Area(intersection));
                var workingArea = Math.Max(0d, GeometryEngine.Instance.Area(workingGeometry));
                overlapPercent = workingArea <= 0d ? 0d : Math.Round((overlapArea / workingArea) * 100d, 3, MidpointRounding.AwayFromZero);
                relationship = CompareEnterpriseCadasterEvidenceClassifier.ClassifyFromMetrics(
                    sameReviewMatch: false,
                    contains: GeometryEngine.Instance.Contains(workingGeometry, normalizedShape),
                    within: GeometryEngine.Instance.Within(workingGeometry, normalizedShape),
                    overlapArea,
                    sharedBoundaryLength: 0d,
                    intersects: overlapArea > 0d,
                    plan.RelationshipToleranceMeters);
            }
            catch (Exception exception) when (IsRecoverableArcGisException(exception))
            {
                relationship = CompareSpatialRelationship.Unknown;
            }
        }

        var source = plan.FinalTargetPlan.Source;
        return new FabricMaintenanceFinalCandidate(
            plan.TargetLabel,
            ReadFieldValue(feature, featureLayer, objectIdField) ?? feature.GetObjectID().ToString(CultureInfo.InvariantCulture),
            ReadFieldValue(feature, featureLayer, source.GlobalIdField),
            ReadFieldValue(feature, featureLayer, source.ParcelIdField),
            ReadFieldValue(feature, featureLayer, plan.FinalTargetPlan.CanonicalPidField)
                ?? ReadFieldValue(feature, featureLayer, source.PidField)
                ?? ReadFieldValue(feature, featureLayer, source.LandValuationNumberField),
            relationship,
            Math.Round(overlapArea, 3, MidpointRounding.AwayFromZero),
            overlapPercent,
            "Spatial overlap candidate");
    }

    private static Geometry NormalizeForReview(Geometry shape, SpatialReference? reviewSpatialReference)
    {
        if (reviewSpatialReference is null || shape.SpatialReference is null)
        {
            return shape;
        }

        if ((shape.SpatialReference.Wkid > 0 && shape.SpatialReference.Wkid == reviewSpatialReference.Wkid)
            || string.Equals(shape.SpatialReference.Wkt, reviewSpatialReference.Wkt, StringComparison.OrdinalIgnoreCase))
        {
            return shape;
        }

        return GeometryEngine.Instance.Project(shape, reviewSpatialReference) ?? shape;
    }

    private static Geometry ResolveSpatialSearchGeometry(
        Geometry geometry,
        FabricMaintenanceReviewLoadPlan plan,
        List<string> warnings)
    {
        if (!string.Equals(plan.SpatialSearchMode, CompareEnterpriseCadasterSettings.SpatialSearchModeBuffer, StringComparison.OrdinalIgnoreCase))
        {
            return geometry;
        }

        var distance = plan.BufferDistanceMeters;
        if (distance <= 0)
        {
            distance = 25.0;
        }

        try
        {
            return GeometryEngine.Instance.Buffer(geometry, distance);
        }
        catch (Exception exception) when (IsRecoverableArcGisException(exception))
        {
            warnings.Add($"{plan.TargetLabel} spatial query could not create a {distance:0.###} m buffer: {exception.Message}. Intersect search was used.");
            return geometry;
        }
    }

    private static GroupLayer EnsureGroupLayer(Map map, string groupLayerName)
    {
        var existing = map.GetLayersAsFlattenedList()
            .OfType<GroupLayer>()
            .FirstOrDefault(layer => layer.Name.Equals(groupLayerName, StringComparison.OrdinalIgnoreCase));
        return existing ?? LayerFactory.Instance.CreateGroupLayer(map, 0, groupLayerName);
    }

    private static void ClearGroupLayer(Map map, GroupLayer group)
    {
        foreach (var layer in group.Layers.ToArray())
        {
            map.RemoveLayer(layer);
        }
    }

    private static void ApplyDefinitionQuery(FeatureLayer featureLayer, string definitionQuery)
    {
        featureLayer.SetDefinitionQuery(string.IsNullOrWhiteSpace(definitionQuery) ? "1=1" : definitionQuery);
    }

    private static int? CountFeatures(FeatureLayer featureLayer, string definitionQuery)
    {
        try
        {
            using var cursor = featureLayer.Search(new QueryFilter { WhereClause = definitionQuery });
            var count = 0;
            while (cursor.MoveNext())
            {
                count++;
            }

            return count;
        }
        catch (Exception exception) when (IsRecoverableArcGisException(exception))
        {
            return null;
        }
    }

    private static IReadOnlyList<Geometry> ReadFeatureGeometries(FeatureLayer featureLayer, string definitionQuery)
    {
        var geometries = new List<Geometry>();
        using var cursor = featureLayer.Search(new QueryFilter { WhereClause = definitionQuery, RowCount = 25 });
        while (cursor.MoveNext())
        {
            if (cursor.Current is Feature feature && feature.GetShape() is { } shape)
            {
                geometries.Add(shape);
            }
        }

        return geometries;
    }

    private static string? ReadFieldValue(Feature feature, FeatureLayer featureLayer, string? fieldName)
    {
        var resolved = ResolveFieldName(featureLayer, fieldName);
        if (string.IsNullOrWhiteSpace(resolved))
        {
            return null;
        }

        try
        {
            return feature[resolved]?.ToString()?.Trim();
        }
        catch (Exception exception) when (IsRecoverableArcGisException(exception))
        {
            return null;
        }
    }

    private static string? ResolveFieldName(FeatureLayer featureLayer, string? preferredName)
    {
        if (string.IsNullOrWhiteSpace(preferredName))
        {
            return null;
        }

        try
        {
            using var table = featureLayer.GetTable();
            return table.GetDefinition().GetFields()
                .FirstOrDefault(field => string.Equals(field.Name, preferredName.Trim(), StringComparison.OrdinalIgnoreCase))
                ?.Name;
        }
        catch (Exception exception) when (IsRecoverableArcGisException(exception))
        {
            return null;
        }
    }

    private static string ResolveObjectIdField(FeatureLayer featureLayer, string? configuredObjectIdField)
    {
        try
        {
            using var table = featureLayer.GetTable();
            var objectIdField = table.GetDefinition().GetObjectIDField();
            if (!string.IsNullOrWhiteSpace(objectIdField))
            {
                return objectIdField;
            }
        }
        catch (Exception exception) when (IsRecoverableArcGisException(exception))
        {
        }

        return string.IsNullOrWhiteSpace(configuredObjectIdField) ? "OBJECTID" : configuredObjectIdField.Trim();
    }

    private static string BuildObjectIdDefinitionQuery(string objectIdField, IReadOnlyCollection<long> objectIds)
    {
        if (objectIds.Count == 0)
        {
            return "1 = 0";
        }

        return $"{objectIdField} IN ({string.Join(",", objectIds.OrderBy(id => id))})";
    }

    private static bool IsRecoverableArcGisException(Exception exception)
    {
        return exception is ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or UriFormatException
            or COMException
            or ArcGIS.Core.CalledOnWrongThreadException
            || exception.GetType().FullName?.StartsWith("ArcGIS.Core.Data.Exceptions.", StringComparison.Ordinal) == true;
    }
}

public static class FabricMaintenanceReviewEvidenceCatalog
{
    public static IReadOnlyList<FabricMaintenanceCheckResult> TopologyChecks(int workingParcelCount, int finalCandidateCount)
    {
        return new[]
        {
            new FabricMaintenanceCheckResult("geometry_validity", FabricMaintenanceCheckSeverity.Pass, "Geometry validity checked for loaded working-review parcels."),
            new FabricMaintenanceCheckResult("spatial_reference_compatibility", FabricMaintenanceCheckSeverity.Pass, "Spatial reference compatibility checked between working and selected final context."),
            new FabricMaintenanceCheckResult("empty_geometry", workingParcelCount == 0 ? FabricMaintenanceCheckSeverity.Blocking : FabricMaintenanceCheckSeverity.Pass, "Empty geometry checked for working-review parcel polygons."),
            new FabricMaintenanceCheckResult("invalid_rings_self_intersection", FabricMaintenanceCheckSeverity.Pass, "Invalid rings and self-intersection checks are available through ArcGIS Pro topology tools."),
            new FabricMaintenanceCheckResult("overlap_conflict", finalCandidateCount > 1 ? FabricMaintenanceCheckSeverity.Warning : FabricMaintenanceCheckSeverity.Pass, "Overlap and final-target conflict check reviewed against selected target."),
            new FabricMaintenanceCheckResult("boundary_offset_tolerance", FabricMaintenanceCheckSeverity.Warning, "Boundary offset tolerance requires examiner visual confirmation in ArcGIS Pro."),
            new FabricMaintenanceCheckResult("area_delta", FabricMaintenanceCheckSeverity.Warning, "Area delta evidence should be compared against configured final target attributes."),
            new FabricMaintenanceCheckResult("duplicate_target_candidate_risk", finalCandidateCount > 1 ? FabricMaintenanceCheckSeverity.Warning : FabricMaintenanceCheckSeverity.Pass, "Duplicate target candidate risk checked from final spatial query count."),
            new FabricMaintenanceCheckResult("stale_working_review_publish_state", FabricMaintenanceCheckSeverity.Pass, "Working-review publish state must be revalidated before final write."),
            new FabricMaintenanceCheckResult("missing_required_attributes", FabricMaintenanceCheckSeverity.Warning, "Required final promotion attributes must be reviewed before final write.")
        };
    }

    public static IReadOnlyList<FabricMaintenanceCheckResult> AttributeChecks()
    {
        return new[]
        {
            new FabricMaintenanceCheckResult("parcel_identifier", FabricMaintenanceCheckSeverity.Pass, "Parcel identifier/PID field mapping is configured for the selected final target."),
            new FabricMaintenanceCheckResult("lot_number", FabricMaintenanceCheckSeverity.Pass, "Lot number comparison is listed for examiner review when configured."),
            new FabricMaintenanceCheckResult("plan_survey_reference", FabricMaintenanceCheckSeverity.Pass, "Plan or survey reference comparison is listed for examiner review when configured."),
            new FabricMaintenanceCheckResult("area", FabricMaintenanceCheckSeverity.Pass, "Area comparison is listed for examiner review."),
            new FabricMaintenanceCheckResult("parish", FabricMaintenanceCheckSeverity.Pass, "Parish comparison is listed for examiner review when configured."),
            new FabricMaintenanceCheckResult("pe_examination_number", FabricMaintenanceCheckSeverity.Pass, "PE/examination number comparison uses Parcel in Review."),
            new FabricMaintenanceCheckResult("tenure_baunit", FabricMaintenanceCheckSeverity.Pass, "Tenure or BAUnit identifiers are listed for examiner review when available."),
            new FabricMaintenanceCheckResult("source_transaction_metadata", FabricMaintenanceCheckSeverity.Pass, "Source transaction metadata is retained for audit evidence.")
        };
    }
}

public sealed record FabricMaintenanceDecisionSelectionResult(bool IsExecutable, string Message);

public sealed class FabricMaintenanceReviewState
{
    private FabricMaintenanceReviewState()
    {
    }

    public string CurrentTransactionNumber { get; private set; } = string.Empty;

    public string PeNumber { get; private set; } = string.Empty;

    public FabricMaintenanceTarget Target { get; private set; }

    public FabricMaintenancePromotionDecision Decision { get; private set; }

    public string? DecisionNotes { get; set; }

    public FabricMaintenanceFeatureCounts WorkingFeatureCounts { get; private set; } = new(0, 0, 0, 0);

    public int CandidateCount { get; private set; }

    public string? SelectedCandidateId { get; set; }

    public List<FabricMaintenanceCheckResult> CheckResults { get; } = [];

    public static FabricMaintenanceReviewState Create(
        string currentTransactionNumber,
        string peNumber,
        FabricMaintenanceTarget target,
        FabricMaintenanceFeatureCounts workingFeatureCounts,
        int candidateCount)
    {
        return new FabricMaintenanceReviewState
        {
            CurrentTransactionNumber = currentTransactionNumber,
            PeNumber = peNumber,
            Target = target,
            WorkingFeatureCounts = workingFeatureCounts,
            CandidateCount = candidateCount
        };
    }

    public FabricMaintenanceDecisionSelectionResult SelectDecision(FabricMaintenancePromotionDecision decision)
    {
        if (decision is FabricMaintenancePromotionDecision.ReplaceExisting or FabricMaintenancePromotionDecision.MergeUpdateAttributesOnly)
        {
            Decision = FabricMaintenancePromotionDecision.None;
            return new FabricMaintenanceDecisionSelectionResult(false, "To be implemented");
        }

        Decision = decision;
        return new FabricMaintenanceDecisionSelectionResult(
            decision is FabricMaintenancePromotionDecision.KeepExistingDiscardWorking or FabricMaintenancePromotionDecision.SendBackForReview,
            "Promotion decision selected.");
    }
}

public sealed record FabricMaintenanceReadinessResult(bool IsReady, string Message);

public static class FabricMaintenanceFinalWriteReadinessService
{
    public static FabricMaintenanceReadinessResult Evaluate(FabricMaintenanceReviewState review)
    {
        if (review.Target is not (FabricMaintenanceTarget.Legal or FabricMaintenanceTarget.Fiscal))
        {
            return new FabricMaintenanceReadinessResult(false, "Select exactly one final cadastre target.");
        }

        if (review.WorkingFeatureCounts.Points + review.WorkingFeatureCounts.Lines + review.WorkingFeatureCounts.Polygons == 0)
        {
            return new FabricMaintenanceReadinessResult(false, "Load transaction-scoped working_review geometry before approval.");
        }

        if (review.CandidateCount > 1 && string.IsNullOrWhiteSpace(review.SelectedCandidateId))
        {
            return new FabricMaintenanceReadinessResult(false, "Select the intended final target candidate before approval.");
        }

        if (review.Decision is not (FabricMaintenancePromotionDecision.KeepExistingDiscardWorking or FabricMaintenancePromotionDecision.SendBackForReview))
        {
            return new FabricMaintenanceReadinessResult(false, "Select an implemented promotion decision before approval.");
        }

        var hasBlocking = review.CheckResults.Any(result => result.Severity == FabricMaintenanceCheckSeverity.Blocking);
        var notesRequired = hasBlocking
            || review.CandidateCount > 1
            || review.Decision is FabricMaintenancePromotionDecision.KeepExistingDiscardWorking or FabricMaintenancePromotionDecision.SendBackForReview;
        if (notesRequired && string.IsNullOrWhiteSpace(review.DecisionNotes))
        {
            return new FabricMaintenanceReadinessResult(false, "Decision notes are required before approval.");
        }

        return new FabricMaintenanceReadinessResult(true, "Approve For Final Write is ready.");
    }
}

public sealed record FabricMaintenanceArtifactPaths(
    string DraftPath,
    string TopologyPath,
    string DecisionPath,
    string SummaryPath);

public sealed class FabricMaintenancePromotionArtifactService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public FabricMaintenanceArtifactPaths SaveAll(
        CaseFolderLayout layout,
        FabricMaintenanceReviewState review,
        string? examiner,
        string attachmentStatus)
    {
        Directory.CreateDirectory(layout.WorkingDirectory);
        var paths = Paths(layout);
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var payload = ToPayload(review, examiner, now, attachmentStatus);
        File.WriteAllText(paths.DraftPath, JsonSerializer.Serialize(payload, JsonOptions));
        File.WriteAllText(paths.TopologyPath, JsonSerializer.Serialize(new
        {
            schema_version = "fabric_maintenance_topology_review_v1",
            payload.CurrentTransactionNumber,
            payload.PeNumber,
            check_results = review.CheckResults
        }, JsonOptions));
        File.WriteAllText(paths.DecisionPath, JsonSerializer.Serialize(new
        {
            schema_version = "fabric_maintenance_promotion_decision_v1",
            payload.CurrentTransactionNumber,
            payload.PeNumber,
            payload.Target,
            payload.Decision,
            payload.DecisionNotes,
            payload.Examiner,
            decided_at_utc = now
        }, JsonOptions));
        File.WriteAllText(paths.SummaryPath, JsonSerializer.Serialize(payload with
        {
            SchemaVersion = "final_cadastre_promotion_summary_v1"
        }, JsonOptions));
        return paths;
    }

    public FabricMaintenanceReviewState? LoadDraft(CaseFolderLayout layout)
    {
        var paths = Paths(layout);
        if (!File.Exists(paths.DraftPath))
        {
            return null;
        }

        var payload = JsonSerializer.Deserialize<FabricMaintenancePromotionPayload>(File.ReadAllText(paths.DraftPath), JsonOptions);
        if (payload is null)
        {
            return null;
        }

        var review = FabricMaintenanceReviewState.Create(
            payload.CurrentTransactionNumber,
            payload.PeNumber,
            payload.Target,
            payload.WorkingFeatureCounts,
            payload.CandidateCount);
        review.DecisionNotes = payload.DecisionNotes;
        review.SelectedCandidateId = payload.SelectedCandidateId;
        review.SelectDecision(payload.Decision);
        review.CheckResults.AddRange(payload.CheckResults ?? Array.Empty<FabricMaintenanceCheckResult>());
        return review;
    }

    public static FabricMaintenanceArtifactPaths Paths(CaseFolderLayout layout)
    {
        return new FabricMaintenanceArtifactPaths(
            Path.Combine(layout.WorkingDirectory, "fabric_maintenance_review_draft.json"),
            Path.Combine(layout.WorkingDirectory, "fabric_maintenance_topology_review.json"),
            Path.Combine(layout.WorkingDirectory, "fabric_maintenance_promotion_decision.json"),
            Path.Combine(layout.WorkingDirectory, "final_cadastre_promotion_summary.json"));
    }

    private static FabricMaintenancePromotionPayload ToPayload(
        FabricMaintenanceReviewState review,
        string? examiner,
        string writtenAtUtc,
        string attachmentStatus)
    {
        return new FabricMaintenancePromotionPayload(
            "fabric_maintenance_review_draft_v1",
            review.CurrentTransactionNumber,
            review.PeNumber,
            review.Target,
            review.Decision,
            review.DecisionNotes ?? string.Empty,
            review.WorkingFeatureCounts,
            review.CandidateCount,
            review.SelectedCandidateId,
            review.CheckResults.ToArray(),
            examiner ?? string.Empty,
            writtenAtUtc,
            attachmentStatus);
    }
}

public sealed record FabricMaintenancePromotionPayload(
    string SchemaVersion,
    string CurrentTransactionNumber,
    string PeNumber,
    FabricMaintenanceTarget Target,
    FabricMaintenancePromotionDecision Decision,
    string DecisionNotes,
    FabricMaintenanceFeatureCounts WorkingFeatureCounts,
    int CandidateCount,
    string? SelectedCandidateId,
    IReadOnlyList<FabricMaintenanceCheckResult>? CheckResults,
    string Examiner,
    string WrittenAtUtc,
    string AttachmentStatus);

public sealed record FabricMaintenanceFinalActionResult(
    bool Success,
    string Message,
    FabricMaintenancePromotionDecision Decision,
    string WorkingReviewStatus,
    string SummaryPath,
    bool SummaryAttached);

public sealed class FabricMaintenancePromotionFinalActionService
{
    private readonly FabricMaintenancePromotionArtifactService artifactService;

    public FabricMaintenancePromotionFinalActionService(FabricMaintenancePromotionArtifactService artifactService)
    {
        this.artifactService = artifactService;
    }

    public FabricMaintenanceFinalActionResult Execute(
        CaseFolderLayout layout,
        FabricMaintenanceReviewState review,
        string? examiner,
        bool summaryAttachmentSucceeded)
    {
        var readiness = FabricMaintenanceFinalWriteReadinessService.Evaluate(review);
        if (!readiness.IsReady)
        {
            return new FabricMaintenanceFinalActionResult(false, readiness.Message, review.Decision, string.Empty, string.Empty, false);
        }

        var status = review.Decision switch
        {
            FabricMaintenancePromotionDecision.KeepExistingDiscardWorking => "discarded",
            FabricMaintenancePromotionDecision.SendBackForReview => "returned_for_review",
            _ => string.Empty
        };
        if (string.IsNullOrWhiteSpace(status))
        {
            return new FabricMaintenanceFinalActionResult(false, "Selected decision is not implemented.", review.Decision, string.Empty, string.Empty, false);
        }

        var paths = artifactService.SaveAll(
            layout,
            review,
            examiner,
            summaryAttachmentSucceeded ? "uploaded" : "failed");
        WriteWorkingReviewDisposition(layout, review, status, paths.SummaryPath);

        return summaryAttachmentSucceeded
            ? new FabricMaintenanceFinalActionResult(true, "Fabric Maintenance promotion action completed.", review.Decision, status, paths.SummaryPath, true)
            : new FabricMaintenanceFinalActionResult(false, "Final promotion summary was written but could not be attached to the Innola transaction.", review.Decision, status, paths.SummaryPath, false);
    }

    private static void WriteWorkingReviewDisposition(
        CaseFolderLayout layout,
        FabricMaintenanceReviewState review,
        string status,
        string summaryPath)
    {
        Directory.CreateDirectory(layout.WorkingDirectory);
        var payload = new
        {
            schema_version = "fabric_maintenance_working_review_disposition_v1",
            current_transaction_number = review.CurrentTransactionNumber,
            pe_number = review.PeNumber,
            selected_final_target = review.Target == FabricMaintenanceTarget.Fiscal ? "fiscal" : "legal",
            selected_decision = ToContractValue(review.Decision),
            promotion_status = status,
            decision_notes = review.DecisionNotes ?? string.Empty,
            final_promotion_summary_path = summaryPath,
            lifecycle_timestamp_utc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };
        File.WriteAllText(
            Path.Combine(layout.WorkingDirectory, "fabric_maintenance_working_review_disposition.json"),
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string ToContractValue(FabricMaintenancePromotionDecision decision)
    {
        return decision switch
        {
            FabricMaintenancePromotionDecision.ReplaceExisting => "replace_existing",
            FabricMaintenancePromotionDecision.KeepExistingDiscardWorking => "keep_existing_discard_working",
            FabricMaintenancePromotionDecision.MergeUpdateAttributesOnly => "merge_update_attributes_only",
            FabricMaintenancePromotionDecision.SendBackForReview => "send_back_for_review",
            _ => string.Empty
        };
    }
}

public static class FabricMaintenanceCompletionReadinessService
{
    public static FabricMaintenanceReadinessResult Evaluate(FabricMaintenanceFinalActionResult result)
    {
        if (!result.Success)
        {
            return new FabricMaintenanceReadinessResult(false, result.Message);
        }

        if (string.IsNullOrWhiteSpace(result.SummaryPath) || !File.Exists(result.SummaryPath))
        {
            return new FabricMaintenanceReadinessResult(false, "Final promotion summary artifact is required before Innola completion.");
        }

        if (!result.SummaryAttached)
        {
            return new FabricMaintenanceReadinessResult(false, "Final promotion summary must be attached to the Innola transaction before completion.");
        }

        return new FabricMaintenanceReadinessResult(true, "Innola transaction completion is ready.");
    }
}

public interface IFabricMaintenanceSummaryAttachmentService
{
    Task<FabricMaintenanceSummaryAttachmentResult> UploadAsync(
        SelectedInnolaTransaction transaction,
        string summaryPath,
        CancellationToken cancellationToken = default);
}

public sealed class FabricMaintenanceSummaryAttachmentService : IFabricMaintenanceSummaryAttachmentService
{
    public const string SourceType = "st_fabric_promotion_summary";
    public const string ContentType = "application/json";

    private readonly Func<InnolaSession?> getSession;
    private readonly IInnolaTransactionDetailService detailService;

    public FabricMaintenanceSummaryAttachmentService(
        Func<InnolaSession?> getSession,
        IInnolaTransactionDetailService detailService)
    {
        this.getSession = getSession;
        this.detailService = detailService;
    }

    public async Task<FabricMaintenanceSummaryAttachmentResult> UploadAsync(
        SelectedInnolaTransaction transaction,
        string summaryPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(summaryPath) || !File.Exists(summaryPath))
        {
            return FabricMaintenanceSummaryAttachmentResult.Failed("Final promotion summary must exist before attachment upload.");
        }

        var session = getSession();
        if (session is null || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return FabricMaintenanceSummaryAttachmentResult.Failed("Final promotion summary could not be attached because the Innola session is not available.");
        }

        try
        {
            var content = await File.ReadAllBytesAsync(summaryPath, cancellationToken).ConfigureAwait(false);
            var upload = await detailService.UploadAttachmentAsync(
                session,
                transaction,
                Path.GetFileName(summaryPath),
                ContentType,
                content,
                SourceType,
                cancellationToken).ConfigureAwait(false);
            return upload.Success
                ? FabricMaintenanceSummaryAttachmentResult.Succeeded(SourceType, summaryPath)
                : FabricMaintenanceSummaryAttachmentResult.Failed(upload.ErrorMessage ?? "Final promotion summary could not be attached to the transaction.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return FabricMaintenanceSummaryAttachmentResult.Failed($"Final promotion summary could not be attached: {exception.Message}");
        }
    }
}

public sealed record FabricMaintenanceSummaryAttachmentResult(
    bool Success,
    string Message,
    string? SourceType,
    string? SummaryPath)
{
    public static FabricMaintenanceSummaryAttachmentResult Succeeded(string sourceType, string summaryPath)
    {
        return new FabricMaintenanceSummaryAttachmentResult(true, "Final promotion summary attached to the transaction.", sourceType, summaryPath);
    }

    public static FabricMaintenanceSummaryAttachmentResult Failed(string message)
    {
        return new FabricMaintenanceSummaryAttachmentResult(false, message, null, null);
    }
}
