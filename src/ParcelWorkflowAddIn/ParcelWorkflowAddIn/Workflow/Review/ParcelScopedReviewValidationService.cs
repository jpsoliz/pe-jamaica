using System.Globalization;
using System.IO;
using System.Text.Json;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Execution;

namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed class ParcelScopedReviewValidationService
{
    private readonly Func<ClosureToleranceCatalog> getClosureToleranceCatalog;

    public ParcelScopedReviewValidationService()
        : this(() => ClosureToleranceCatalog.Load())
    {
    }

    internal ParcelScopedReviewValidationService(Func<ClosureToleranceCatalog> getClosureToleranceCatalog)
    {
        this.getClosureToleranceCatalog = getClosureToleranceCatalog;
    }

    public ParcelScopedReviewValidationResult Validate(
        IReadOnlyList<ExtractionReviewRow> rows,
        string? pendingManualRowId = null,
        bool includePendingManualBarrier = true)
    {
        var issues = new List<string>();
        var parcelIssues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (rows.Count == 0)
        {
            issues.Add("No review rows are loaded.");
            return new ParcelScopedReviewValidationResult(issues, parcelIssues, Array.Empty<ParcelClosureReviewResult>());
        }

        var unresolvedCount = rows.Count(row => row.Unresolved);
        if (unresolvedCount > 0)
        {
            issues.Add($"{unresolvedCount} unresolved row(s) still need examiner confirmation.");
        }

        var missingRequiredCount = rows.Count(row =>
            string.IsNullOrWhiteSpace(row.PointIdentifier)
            || string.IsNullOrWhiteSpace(row.Easting)
            || string.IsNullOrWhiteSpace(row.Northing));
        if (missingRequiredCount > 0)
        {
            issues.Add($"{missingRequiredCount} row(s) are missing point id or coordinates.");
        }

        var invalidCoordinateCount = rows.Count(row =>
            !string.IsNullOrWhiteSpace(row.Easting)
            && !TryParseCoordinate(row.Easting, out _)
            || !string.IsNullOrWhiteSpace(row.Northing)
            && !TryParseCoordinate(row.Northing, out _));
        if (invalidCoordinateCount > 0)
        {
            issues.Add($"{invalidCoordinateCount} row(s) contain invalid numeric coordinates.");
        }

        var manualWithoutParcelCount = rows.Count(row =>
            row.IsManual
            && (string.IsNullOrWhiteSpace(row.ParcelGroupId) || string.Equals(row.ParcelGroupId.Trim(), "Parcel ?", StringComparison.OrdinalIgnoreCase)));
        if (manualWithoutParcelCount > 0)
        {
            issues.Add($"{manualWithoutParcelCount} manual row(s) are missing parcel assignment.");
        }

        foreach (var parcelGroup in rows.GroupBy(row => NormalizeParcelGroup(row.ParcelGroupId), StringComparer.OrdinalIgnoreCase))
        {
            var parcelLabel = parcelGroup.Key;
            var duplicatePointIds = parcelGroup
                .Where(row => !string.IsNullOrWhiteSpace(row.PointIdentifier))
                .GroupBy(row => row.PointIdentifier.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicatePointIds.Length > 0)
            {
                var message = $"Parcel {parcelLabel} has duplicate point id(s): {string.Join(", ", duplicatePointIds)}.";
                issues.Add(message);
                parcelIssues[parcelLabel] = AppendIssue(parcelIssues, parcelLabel, message);
            }

            var invalidSequenceRows = parcelGroup.Count(row => row.SequenceInGroup is null or <= 0);
            if (invalidSequenceRows > 0)
            {
                var message = $"Parcel {parcelLabel} has {invalidSequenceRows} row(s) with missing or invalid sequence.";
                issues.Add(message);
                parcelIssues[parcelLabel] = AppendIssue(parcelIssues, parcelLabel, message);
            }

            var duplicateSequences = parcelGroup
                .Where(row => row.SequenceInGroup.HasValue && row.SequenceInGroup.Value > 0)
                .GroupBy(row => row.SequenceInGroup!.Value)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key.ToString(CultureInfo.InvariantCulture))
                .ToArray();
            if (duplicateSequences.Length > 0)
            {
                var message = $"Parcel {parcelLabel} has duplicate sequence value(s): {string.Join(", ", duplicateSequences)}.";
                issues.Add(message);
                parcelIssues[parcelLabel] = AppendIssue(parcelIssues, parcelLabel, message);
            }
        }

        var closureResults = ComputeClosureResults(rows);
        foreach (var closureResult in closureResults)
        {
            if (closureResult.Status == ClosureValidationStatus.Passed)
            {
                continue;
            }

            var label = closureResult.ParcelGroupId;
            parcelIssues[label] = AppendIssue(parcelIssues, label, closureResult.Message);
            if (closureResult.Status == ClosureValidationStatus.Blocker)
            {
                issues.Add(closureResult.Message);
            }
        }

        if (includePendingManualBarrier && !string.IsNullOrWhiteSpace(pendingManualRowId))
        {
            issues.Add("Save or discard the in-progress manual point before approval or parcel switching.");
        }

        return new ParcelScopedReviewValidationResult(issues.Distinct(StringComparer.Ordinal).ToArray(), parcelIssues, closureResults);
    }

    private IReadOnlyList<ParcelClosureReviewResult> ComputeClosureResults(IReadOnlyList<ExtractionReviewRow> rows)
    {
        var catalog = getClosureToleranceCatalog();
        var defaultProfile = catalog.DefaultProfile;
        var sourceMode = InferSourceMode(rows);
        var results = new List<ParcelClosureReviewResult>();

        foreach (var parcelGroup in rows.GroupBy(row => NormalizeParcelGroup(row.ParcelGroupId), StringComparer.OrdinalIgnoreCase))
        {
            var orderedRows = parcelGroup
                .OrderBy(row => row.SequenceInGroup ?? int.MaxValue)
                .ThenBy(row => row.PointIdentifier, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var parcelType = InferParcelType(orderedRows, defaultProfile.ParcelType, sourceMode);
            var profile = catalog.Resolve(parcelType);
            var coordinates = orderedRows
                .Where(row => TryParseCoordinate(row.Easting, out _) && TryParseCoordinate(row.Northing, out _))
                .Select(row => new CoordinatePoint(ParseCoordinate(row.Easting), ParseCoordinate(row.Northing)))
                .ToArray();

            if (!profile.Enabled)
            {
                results.Add(new ParcelClosureReviewResult(
                    parcelGroup.Key,
                    orderedRows.FirstOrDefault()?.ParcelName ?? parcelGroup.Key,
                    parcelType,
                    ClosureValidationStatus.Passed,
                    "Closure validation profile is disabled.",
                    null,
                    null,
                    profile.RuleId,
                    profile.Title,
                    profile.MaxClosureDistanceM,
                    profile.WarningClosureDistanceM,
                    profile.MinMiscloseRatioDenominator,
                    profile.WarningMiscloseRatioDenominator));
                continue;
            }

            if (coordinates.Length < 2)
            {
                results.Add(new ParcelClosureReviewResult(
                    parcelGroup.Key,
                    orderedRows.FirstOrDefault()?.ParcelName ?? parcelGroup.Key,
                    parcelType,
                    ClosureValidationStatus.Warning,
                    $"Parcel {parcelGroup.Key} does not have enough numeric coordinate rows to compute closure.",
                    null,
                    null,
                    profile.RuleId,
                    profile.Title,
                    profile.MaxClosureDistanceM,
                    profile.WarningClosureDistanceM,
                    profile.MinMiscloseRatioDenominator,
                    profile.WarningMiscloseRatioDenominator));
                continue;
            }

            var start = coordinates[0];
            var end = coordinates[^1];
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var closureDistance = Math.Sqrt((dx * dx) + (dy * dy));
            var totalLength = 0d;
            for (var index = 1; index < coordinates.Length; index++)
            {
                var previous = coordinates[index - 1];
                var current = coordinates[index];
                totalLength += Math.Sqrt(Math.Pow(current.X - previous.X, 2) + Math.Pow(current.Y - previous.Y, 2));
            }

            double? miscloseRatio = null;
            if (closureDistance > 0d && totalLength > 0d)
            {
                miscloseRatio = totalLength / closureDistance;
            }

            var exceedsBlockDistance = profile.MaxClosureDistanceM.HasValue && closureDistance > profile.MaxClosureDistanceM.Value;
            var exceedsWarningDistance = profile.WarningClosureDistanceM.HasValue && closureDistance > profile.WarningClosureDistanceM.Value;
            var belowBlockRatio = miscloseRatio.HasValue && profile.MinMiscloseRatioDenominator.HasValue && miscloseRatio.Value < profile.MinMiscloseRatioDenominator.Value;
            var belowWarningRatio = miscloseRatio.HasValue && profile.WarningMiscloseRatioDenominator.HasValue && miscloseRatio.Value < profile.WarningMiscloseRatioDenominator.Value;

            var status = ClosureValidationStatus.Passed;
            var message = $"Parcel {parcelGroup.Key} closure is within tolerance.";

            if (profile.AllowOpenBoundary)
            {
                if (exceedsWarningDistance || belowWarningRatio)
                {
                    status = ClosureValidationStatus.Warning;
                    message = $"Parcel {parcelGroup.Key} remains outside the configured open-boundary closure tolerance.";
                }
            }
            else if (exceedsBlockDistance || belowBlockRatio)
            {
                status = profile.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase)
                    ? ClosureValidationStatus.Warning
                    : ClosureValidationStatus.Blocker;
                message = $"Parcel {parcelGroup.Key} exceeds the configured closure tolerance.";
            }
            else if (exceedsWarningDistance || belowWarningRatio)
            {
                status = ClosureValidationStatus.Warning;
                message = $"Parcel {parcelGroup.Key} exceeds the configured closure warning tolerance.";
            }

            results.Add(new ParcelClosureReviewResult(
                parcelGroup.Key,
                orderedRows.FirstOrDefault()?.ParcelName ?? parcelGroup.Key,
                parcelType,
                status,
                message,
                closureDistance,
                miscloseRatio,
                profile.RuleId,
                profile.Title,
                profile.MaxClosureDistanceM,
                profile.WarningClosureDistanceM,
                profile.MinMiscloseRatioDenominator,
                profile.WarningMiscloseRatioDenominator));
        }

        return results;
    }

    private static string AppendIssue(IDictionary<string, string> issues, string key, string message)
    {
        if (!issues.TryGetValue(key, out var existing) || string.IsNullOrWhiteSpace(existing))
        {
            return message;
        }

        return $"{existing} {message}";
    }

    private static string InferSourceMode(IReadOnlyList<ExtractionReviewRow> rows)
    {
        var rawValues = rows
            .Select(row => row.RawRow?["source_doc"]?.GetValue<string>() ?? row.RawRow?["source_mode"]?.GetValue<string>() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        return rawValues.Any(value => value.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || value.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            ? "imported_coordinates"
            : "source_documents";
    }

    private static string InferParcelType(IReadOnlyList<ExtractionReviewRow> rows, string defaultParcelType, string sourceMode)
    {
        var explicitType = rows
            .Select(row => row.RawRow?["parcel_type"]?.GetValue<string>() ?? row.RawRow?["review_parcel_type"]?.GetValue<string>() ?? string.Empty)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(explicitType))
        {
            return explicitType.Trim();
        }

        if (rows.Any(row => row.IsBoundaryBreak))
        {
            return "open_boundary";
        }

        if (string.Equals(sourceMode, "imported_coordinates", StringComparison.OrdinalIgnoreCase))
        {
            return "imported_coordinates";
        }

        return string.IsNullOrWhiteSpace(defaultParcelType) ? "standard_closed" : defaultParcelType;
    }

    private static bool TryParseCoordinate(string value, out double coordinate)
    {
        var text = value.Trim();
        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out coordinate))
        {
            return true;
        }

        return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out coordinate);
    }

    private static double ParseCoordinate(string value)
    {
        TryParseCoordinate(value, out var coordinate);
        return coordinate;
    }

    private static string NormalizeParcelGroup(string? parcelGroupId)
    {
        return string.IsNullOrWhiteSpace(parcelGroupId) ? "Unassigned" : parcelGroupId.Trim();
    }
}

public sealed record ParcelScopedReviewValidationResult(
    IReadOnlyList<string> Issues,
    IReadOnlyDictionary<string, string> ParcelIssues,
    IReadOnlyList<ParcelClosureReviewResult> ClosureResults)
{
    public bool HasBlockers => Issues.Count > 0 || ClosureResults.Any(result => result.Status == ClosureValidationStatus.Blocker);

    public string SummaryText => !HasBlockers
        ? "Review is complete for this stage."
        : Issues.Count > 0
            ? Issues[0]
            : ClosureResults.First(result => result.Status == ClosureValidationStatus.Blocker).Message;
}

public sealed record ParcelClosureReviewResult(
    string ParcelGroupId,
    string ParcelName,
    string ParcelType,
    ClosureValidationStatus Status,
    string Message,
    double? ClosureDistanceM,
    double? MiscloseRatioDenominator,
    string ProfileRuleId,
    string ProfileTitle,
    double? MaxClosureDistanceM,
    double? WarningClosureDistanceM,
    double? MinMiscloseRatioDenominator,
    double? WarningMiscloseRatioDenominator);

public enum ClosureValidationStatus
{
    Passed,
    Warning,
    Blocker
}

internal sealed record ClosureToleranceProfile(
    string RuleId,
    string Title,
    string ParcelType,
    bool Enabled,
    string Severity,
    bool AllowOpenBoundary,
    double? MaxClosureDistanceM,
    double? WarningClosureDistanceM,
    double? MinMiscloseRatioDenominator,
    double? WarningMiscloseRatioDenominator);

internal sealed class ClosureToleranceCatalog
{
    public ClosureToleranceProfile DefaultProfile { get; set; } = new(
        "closure_default_standard",
        "Standard parcel closure tolerance",
        "standard_closed",
        true,
        "blocker",
        false,
        0.3,
        0.15,
        2500,
        4000);

    public Dictionary<string, ClosureToleranceProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ClosureToleranceProfile Resolve(string parcelType)
    {
        return Profiles.TryGetValue(parcelType, out var profile) ? profile : DefaultProfile;
    }

    public static ClosureToleranceCatalog Load(string? settingsPath = null)
    {
        settingsPath ??= InnolaTransactionSettings.ResolveActiveSettingsPath();
        var executionSettings = WorkflowExecutionSettings.Load(settingsPath);
        var catalog = ParseRuleCatalog(executionSettings.ValidationRulesPath);
        ApplyOverrides(catalog, settingsPath);
        return catalog;
    }

    private static ClosureToleranceCatalog ParseRuleCatalog(string? rulesPath)
    {
        var catalog = new ClosureToleranceCatalog();
        if (string.IsNullOrWhiteSpace(rulesPath) || !File.Exists(rulesPath))
        {
            return catalog;
        }

        var lines = File.ReadAllLines(rulesPath);
        var section = string.Empty;
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var profiles = new Dictionary<string, ClosureToleranceProfile>(StringComparer.OrdinalIgnoreCase);
        var skipIndent = -1;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var indent = line.Length - line.TrimStart().Length;
            if (indent == 0)
            {
                if (string.Equals(section, "closure_tolerance_profiles", StringComparison.OrdinalIgnoreCase))
                {
                    CommitProfile(current, profiles);
                    current.Clear();
                }

                section = trimmed.TrimEnd(':');
                skipIndent = -1;
                continue;
            }

            if (skipIndent >= 0 && indent > skipIndent)
            {
                continue;
            }

            if (skipIndent >= 0 && indent <= skipIndent)
            {
                skipIndent = -1;
            }

            var working = trimmed;
            if (working.StartsWith("- ", StringComparison.Ordinal))
            {
                if (string.Equals(section, "closure_tolerance_profiles", StringComparison.OrdinalIgnoreCase))
                {
                    CommitProfile(current, profiles);
                    current.Clear();
                }

                working = working[2..].TrimStart();
            }

            var splitIndex = working.IndexOf(':');
            if (splitIndex < 0)
            {
                continue;
            }

            var key = working[..splitIndex].Trim();
            var value = working[(splitIndex + 1)..].Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(value))
            {
                skipIndent = indent;
                continue;
            }

            current[key] = value;

            if (string.Equals(section, "closure_tolerance_defaults", StringComparison.OrdinalIgnoreCase))
            {
                catalog.DefaultProfile = ToProfile(current, catalog.DefaultProfile);
            }
        }

        if (string.Equals(section, "closure_tolerance_profiles", StringComparison.OrdinalIgnoreCase))
        {
            CommitProfile(current, profiles);
        }

        catalog.Profiles = profiles;
        return catalog;
    }

    private static void ApplyOverrides(ClosureToleranceCatalog catalog, string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        if (!document.RootElement.TryGetProperty("closure_tolerance_profile_overrides", out var overrides)
            || overrides.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (overrides.TryGetProperty("default_parcel_type", out var defaultParcelType)
            && defaultParcelType.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(defaultParcelType.GetString()))
        {
            var updatedDefault = catalog.DefaultProfile with { ParcelType = defaultParcelType.GetString()!.Trim() };
            catalog.DefaultProfile = updatedDefault;
        }

        if (!overrides.TryGetProperty("profiles", out var profileOverrides)
            || profileOverrides.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in profileOverrides.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var baseProfile = catalog.Profiles.TryGetValue(property.Name, out var existing)
                ? existing
                : new ClosureToleranceProfile(property.Name, property.Name, property.Name, true, "blocker", false, null, null, null, null);
            catalog.Profiles[property.Name] = ApplyProfileOverride(baseProfile, property.Value);
        }
    }

    private static ClosureToleranceProfile ApplyProfileOverride(ClosureToleranceProfile profile, JsonElement value)
    {
        return profile with
        {
            Enabled = ReadBool(value, "enabled") ?? profile.Enabled,
            Severity = ReadString(value, "severity") ?? profile.Severity,
            AllowOpenBoundary = ReadBool(value, "allow_open_boundary") ?? profile.AllowOpenBoundary,
            MaxClosureDistanceM = ReadDouble(value, "max_closure_distance_m") ?? profile.MaxClosureDistanceM,
            WarningClosureDistanceM = ReadDouble(value, "warning_closure_distance_m") ?? profile.WarningClosureDistanceM,
            MinMiscloseRatioDenominator = ReadDouble(value, "min_misclose_ratio_denominator") ?? profile.MinMiscloseRatioDenominator,
            WarningMiscloseRatioDenominator = ReadDouble(value, "warning_misclose_ratio_denominator") ?? profile.WarningMiscloseRatioDenominator
        };
    }

    private static void CommitProfile(Dictionary<string, string> current, Dictionary<string, ClosureToleranceProfile> profiles)
    {
        if (current.Count == 0)
        {
            return;
        }

        var parcelType = GetValue(current, "parcel_type") ?? GetValue(current, "rule_id");
        if (string.IsNullOrWhiteSpace(parcelType))
        {
            return;
        }

        profiles[parcelType] = ToProfile(current, new ClosureToleranceProfile(
            GetValue(current, "rule_id") ?? parcelType,
            GetValue(current, "title") ?? parcelType,
            parcelType,
            true,
            "blocker",
            false,
            null,
            null,
            null,
            null));
    }

    private static ClosureToleranceProfile ToProfile(Dictionary<string, string> values, ClosureToleranceProfile baseProfile)
    {
        return baseProfile with
        {
            RuleId = GetValue(values, "rule_id") ?? baseProfile.RuleId,
            Title = GetValue(values, "title") ?? baseProfile.Title,
            ParcelType = GetValue(values, "parcel_type") ?? baseProfile.ParcelType,
            Enabled = TryParseBool(GetValue(values, "enabled")) ?? baseProfile.Enabled,
            Severity = GetValue(values, "severity") ?? baseProfile.Severity,
            AllowOpenBoundary = TryParseBool(GetValue(values, "allow_open_boundary")) ?? baseProfile.AllowOpenBoundary,
            MaxClosureDistanceM = TryParseDouble(GetValue(values, "max_closure_distance_m")) ?? baseProfile.MaxClosureDistanceM,
            WarningClosureDistanceM = TryParseDouble(GetValue(values, "warning_closure_distance_m")) ?? baseProfile.WarningClosureDistanceM,
            MinMiscloseRatioDenominator = TryParseDouble(GetValue(values, "min_misclose_ratio_denominator")) ?? baseProfile.MinMiscloseRatioDenominator,
            WarningMiscloseRatioDenominator = TryParseDouble(GetValue(values, "warning_misclose_ratio_denominator")) ?? baseProfile.WarningMiscloseRatioDenominator
        };
    }

    private static string? GetValue(Dictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : null;
    }

    private static bool? TryParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static double? TryParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
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

    private static double? ReadDouble(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var numeric))
        {
            return numeric;
        }

        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}

internal readonly record struct CoordinatePoint(double X, double Y);
