using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Enterprise.PortalAuth;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.ParcelSearch;

public static class ParcelSearchLayerScope
{
    public const string All = "all";
    public const string Legal = "legal";
    public const string Cadastral = "cadastral";
    public const string Survey = "survey";
}

public sealed class ParcelSearchCriteria
{
    public string LayerScope { get; init; } = ParcelSearchLayerScope.All;
    public IReadOnlyList<string> LayerScopes { get; init; } = Array.Empty<string>();
    public string? Volume { get; init; }
    public string? Folio { get; init; }
    public string? Name { get; init; }
    public string? PeNumber { get; init; }
    public string? LandValuationNumber { get; init; }
    public string? DpNumber { get; init; }
    public string? RNumber { get; init; }
    public IReadOnlyList<string> ParishNames { get; init; } = Array.Empty<string>();

    public bool HasSearchCriteriaOrFilter =>
        HasText(Volume)
        || HasText(Folio)
        || HasText(Name)
        || HasText(PeNumber)
        || HasText(LandValuationNumber)
        || HasText(DpNumber)
        || HasText(RNumber)
        || HasSpecificParishFilter;

    public bool HasSpecificParishFilter => ParishNames.Any(parish =>
        !string.IsNullOrWhiteSpace(parish)
        && !string.Equals(parish.Trim(), "All", StringComparison.OrdinalIgnoreCase));

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed record ParcelSearchQueryPlan(
    bool ShouldExecute,
    IReadOnlyList<ParcelSearchSourceRequest> SourceRequests,
    ParcelSearchParishFilterRequest? ParishFilterRequest,
    IReadOnlyList<ParcelSearchPopupFieldSettings> PopupFields,
    IReadOnlyList<string> Diagnostics,
    string StatusMessage,
    int ResultLimit,
    int PageSize)
{
    public static ParcelSearchQueryPlan Blocked(string statusMessage, IReadOnlyList<string>? diagnostics = null)
    {
        return new ParcelSearchQueryPlan(
            false,
            Array.Empty<ParcelSearchSourceRequest>(),
            null,
            Array.Empty<ParcelSearchPopupFieldSettings>(),
            diagnostics ?? Array.Empty<string>(),
            statusMessage,
            0,
            0);
    }
}

public sealed record ParcelSearchSourceRequest(
    string SourceKind,
    string SourceLayerName,
    string SourceDisplayName,
    string LayerUrl,
    string? SublayerName,
    string WhereClause,
    IReadOnlyList<string> OutFields,
    IReadOnlyList<ParcelSearchLabelField> LabelFields,
    int ResultLimit,
    int PageSize,
    CompareEnterpriseCadasterSourceSettings FieldMap);

public sealed record ParcelSearchLabelField(
    string Label,
    IReadOnlyList<string> FieldNames,
    string Separator = "/");

public sealed record ParcelSearchParishFilterRequest(
    string LayerUrl,
    string ParishNameField,
    IReadOnlyList<string> ParishNames,
    string WhereClause);

public sealed record ParcelSearchSpatialFilter(
    string GeometryJson,
    string GeometryType,
    string? SpatialReferenceJson,
    IReadOnlyList<string> Diagnostics);

public static class ParcelSearchQueryPlanner
{
    private static readonly Regex SafeFieldName = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public static ParcelSearchQueryPlan Build(ParcelSearchCriteria criteria, CompareEnterpriseCadasterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(settings);

        if (!criteria.HasSearchCriteriaOrFilter)
        {
            return ParcelSearchQueryPlan.Blocked("Enter at least one criterion or filter before searching.");
        }

        if (!settings.Enabled)
        {
            return ParcelSearchQueryPlan.Blocked("Parcel search is disabled because compare_enterprise_cadaster is disabled.");
        }

        var diagnostics = new List<string>();
        if (!string.IsNullOrWhiteSpace(settings.Warning))
        {
            diagnostics.Add(RedactDiagnostic(settings.Warning));
        }

        var requests = new List<ParcelSearchSourceRequest>();
        foreach (var source in ResolveSources(criteria, settings))
        {
            AddSourceRequest(source, criteria, settings, settings.ResultLimit, settings.PageSize, requests, diagnostics);
        }

        if (requests.Count == 0)
        {
            var message = diagnostics.Count == 0
                ? "No enabled parcel search source is configured for the selected layer scope."
                : string.Join(" ", diagnostics);
            return ParcelSearchQueryPlan.Blocked(message, diagnostics);
        }

        return new ParcelSearchQueryPlan(
            true,
            requests,
            BuildParishFilterRequest(criteria, settings, diagnostics),
            settings.PopupFields,
            diagnostics,
            $"Parcel search query plan is ready for {requests.Count} source(s).",
            settings.ResultLimit,
            settings.PageSize);
    }

    public static string TranslateWildcardPattern(string value, bool singleCharacterWildcard)
    {
        var builder = new StringBuilder();
        foreach (var character in value.Trim())
        {
            if (character == '*')
            {
                builder.Append('%');
            }
            else if (singleCharacterWildcard && character == '?')
            {
                builder.Append('_');
            }
            else if (character == '\'')
            {
                builder.Append("''");
            }
            else if (character is '%' or '_')
            {
                builder.Append($"[{character}]");
            }
            else
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString();
    }

    public static string RedactDiagnostic(string diagnostic)
    {
        if (string.IsNullOrWhiteSpace(diagnostic))
        {
            return string.Empty;
        }

        var redacted = Regex.Replace(diagnostic, "(access[-_ ]?token|authorization|password|api[-_ ]?key)=?[^\\s&]+", "$1=[redacted]", RegexOptions.IgnoreCase);
        redacted = Regex.Replace(redacted, "(token=|apikey=|access_token=)[^\\s&]+", "$1[redacted]", RegexOptions.IgnoreCase);
        return redacted;
    }

    private static IEnumerable<ParcelSearchSourceDescriptor> ResolveSources(ParcelSearchCriteria criteria, CompareEnterpriseCadasterSettings settings)
    {
        foreach (var scope in NormalizeSelectedScopes(criteria))
        {
            if (scope == ParcelSearchLayerScope.Legal)
            {
                yield return new ParcelSearchSourceDescriptor(CompareEnterpriseCadasterSourceKind.Legal, "Legal_Cadastre", "Legal", settings.Legal);
            }
            else if (scope == ParcelSearchLayerScope.Cadastral)
            {
                yield return new ParcelSearchSourceDescriptor(CompareEnterpriseCadasterSourceKind.Fiscal, "Fiscal_Cadastre", "Cadastral", settings.Fiscal);
            }
            else if (scope == ParcelSearchLayerScope.Survey)
            {
                yield return new ParcelSearchSourceDescriptor(CompareEnterpriseCadasterSourceKind.Survey, "Survey_Cadastre", "Survey", settings.Survey);
            }
        }
    }

    private static IReadOnlyList<string> NormalizeSelectedScopes(ParcelSearchCriteria criteria)
    {
        var explicitScopes = criteria.LayerScopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim().ToLowerInvariant())
            .ToArray();

        return explicitScopes.Length == 0
            ? NormalizeSelectedScopes(criteria.LayerScope)
            : ExpandScopes(explicitScopes, fallbackToAll: false);
    }

    private static IReadOnlyList<string> NormalizeSelectedScopes(string? layerScope)
    {
        var normalized = string.IsNullOrWhiteSpace(layerScope)
            ? ParcelSearchLayerScope.All
            : layerScope.Trim().ToLowerInvariant();

        return ExpandScopes(new[] { normalized }, fallbackToAll: true);
    }

    private static IReadOnlyList<string> ExpandScopes(IReadOnlyList<string> scopes, bool fallbackToAll)
    {
        var expanded = scopes.Contains(ParcelSearchLayerScope.All, StringComparer.OrdinalIgnoreCase)
            ? new[] { ParcelSearchLayerScope.Legal, ParcelSearchLayerScope.Cadastral, ParcelSearchLayerScope.Survey }
            : scopes
                .Select(scope => string.Equals(scope, "fiscal", StringComparison.OrdinalIgnoreCase) ? ParcelSearchLayerScope.Cadastral : scope)
                .Where(scope => scope is ParcelSearchLayerScope.Legal or ParcelSearchLayerScope.Cadastral or ParcelSearchLayerScope.Survey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return fallbackToAll && expanded.Length == 0
            ? new[] { ParcelSearchLayerScope.Legal, ParcelSearchLayerScope.Cadastral, ParcelSearchLayerScope.Survey }
            : expanded;
    }

    private static void AddSourceRequest(
        ParcelSearchSourceDescriptor descriptor,
        ParcelSearchCriteria criteria,
        CompareEnterpriseCadasterSettings settings,
        int resultLimit,
        int pageSize,
        List<ParcelSearchSourceRequest> requests,
        List<string> diagnostics)
    {
        var source = descriptor.Settings;
        if (!source.Enabled)
        {
            diagnostics.Add($"{descriptor.SourceDisplayName} source is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(source.LayerUrl))
        {
            diagnostics.Add($"{descriptor.SourceDisplayName} layer_url is not configured.");
            return;
        }

        var clauses = new List<string>();
        if (!AddVolumeFolioClauses(clauses, source, criteria, descriptor.SourceDisplayName, diagnostics)
            || !AddTextClause(clauses, ResolveNameField(source), criteria.Name, "Name", descriptor.SourceDisplayName, diagnostics, singleCharacterWildcard: false)
            || !AddTextClause(clauses, source.PeNumberField, criteria.PeNumber, "PE Number", descriptor.SourceDisplayName, diagnostics, singleCharacterWildcard: true, caseInsensitive: false)
            || !AddTextClause(clauses, source.LandValuationNumberField, criteria.LandValuationNumber, "LandVal Number", descriptor.SourceDisplayName, diagnostics, singleCharacterWildcard: true, caseInsensitive: false)
            || !AddTextClause(clauses, source.DpNumberField, criteria.DpNumber, "DP Number", descriptor.SourceDisplayName, diagnostics, singleCharacterWildcard: true, caseInsensitive: false)
            || !AddTextClause(clauses, source.RNumberField, criteria.RNumber, "R Number", descriptor.SourceDisplayName, diagnostics, singleCharacterWildcard: true, caseInsensitive: false)
            || (!ShouldUseSpatialParishFilter(criteria, settings)
                && !AddParishClause(clauses, source.ParishField, criteria, descriptor.SourceDisplayName, diagnostics)))
        {
            return;
        }

        if (clauses.Count == 0 && ShouldUseSpatialParishFilter(criteria, settings))
        {
            clauses.Add("1=1");
        }

        if (clauses.Count == 0)
        {
            diagnostics.Add($"{descriptor.SourceDisplayName} has no configured field matching the requested search filters.");
            return;
        }

        requests.Add(new ParcelSearchSourceRequest(
            descriptor.SourceKind,
            descriptor.SourceLayerName,
            descriptor.SourceDisplayName,
            source.LayerUrl!,
            source.SublayerName,
            string.Join(" AND ", clauses),
            BuildOutFields(source),
            BuildLabelFields(source, criteria),
            resultLimit,
            pageSize,
            source));
    }

    private static bool ShouldUseSpatialParishFilter(ParcelSearchCriteria criteria, CompareEnterpriseCadasterSettings settings)
    {
        return criteria.HasSpecificParishFilter
            && settings.ParishSource.Enabled
            && !string.IsNullOrWhiteSpace(settings.ParishSource.LayerUrl)
            && !string.IsNullOrWhiteSpace(settings.ParishSource.ParishNameField)
            && SafeFieldName.IsMatch(settings.ParishSource.ParishNameField);
    }

    private static ParcelSearchParishFilterRequest? BuildParishFilterRequest(
        ParcelSearchCriteria criteria,
        CompareEnterpriseCadasterSettings settings,
        List<string> diagnostics)
    {
        var parishes = criteria.ParishNames
            .Where(parish => !string.IsNullOrWhiteSpace(parish))
            .Select(parish => parish.Trim())
            .Where(parish => !string.Equals(parish, "All", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (parishes.Length == 0)
        {
            return null;
        }

        var source = settings.ParishSource;
        if (!source.Enabled || string.IsNullOrWhiteSpace(source.LayerUrl))
        {
            diagnostics.Add("Parish spatial filter is not available because parish_source layer_url is not configured.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(source.ParishNameField) || !SafeFieldName.IsMatch(source.ParishNameField))
        {
            diagnostics.Add("Parish spatial filter is not available because parish_source parish_name_field is not configured.");
            return null;
        }

        var values = parishes
            .SelectMany(ExpandParishSearchValues)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(parish => $"'{TranslateWildcardPattern(parish, singleCharacterWildcard: false)}'")
            .ToArray();
        return new ParcelSearchParishFilterRequest(
            source.LayerUrl,
            source.ParishNameField,
            parishes,
            $"UPPER({source.ParishNameField}) IN ({string.Join(", ", values)})");
    }

    private static IReadOnlyList<string> ExpandParishSearchValues(string parish)
    {
        var trimmed = parish.Trim();
        if (trimmed.Length == 0)
        {
            return Array.Empty<string>();
        }

        var values = new List<string> { trimmed };
        if (trimmed.StartsWith("Saint ", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = trimmed[6..].Trim();
            if (suffix.Length > 0)
            {
                values.Add($"St {suffix}");
                values.Add($"St. {suffix}");
                values.Add($"St.{suffix}");
            }
        }
        else if (trimmed.StartsWith("St. ", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = trimmed[4..].Trim();
            if (suffix.Length > 0)
            {
                values.Add($"Saint {suffix}");
                values.Add($"St {suffix}");
                values.Add($"St.{suffix}");
            }
        }
        else if (trimmed.StartsWith("St.", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = trimmed[3..].Trim();
            if (suffix.Length > 0)
            {
                values.Add($"Saint {suffix}");
                values.Add($"St {suffix}");
                values.Add($"St. {suffix}");
            }
        }
        else if (trimmed.StartsWith("St ", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = trimmed[3..].Trim();
            if (suffix.Length > 0)
            {
                values.Add($"Saint {suffix}");
                values.Add($"St. {suffix}");
                values.Add($"St.{suffix}");
            }
        }

        return values;
    }

    private static bool AddTextClause(
        List<string> clauses,
        string? fieldName,
        string? value,
        string label,
        string sourceDisplayName,
        List<string> diagnostics,
        bool singleCharacterWildcard,
        bool caseInsensitive = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(fieldName) || !SafeFieldName.IsMatch(fieldName))
        {
            diagnostics.Add($"{sourceDisplayName} is excluded because {label} field mapping is not configured.");
            return false;
        }

        var pattern = TranslateWildcardPattern(value, singleCharacterWildcard);
        clauses.Add(caseInsensitive
            ? $"UPPER({fieldName}) LIKE '{pattern}'"
            : $"{fieldName} LIKE '{pattern}'");
        return true;
    }

    private static bool AddVolumeFolioClauses(
        List<string> clauses,
        CompareEnterpriseCadasterSourceSettings source,
        ParcelSearchCriteria criteria,
        string sourceDisplayName,
        List<string> diagnostics)
    {
        var hasVolume = !string.IsNullOrWhiteSpace(criteria.Volume);
        var hasFolio = !string.IsNullOrWhiteSpace(criteria.Folio);
        if (!hasVolume && !hasFolio)
        {
            return true;
        }

        if (hasVolume
            && hasFolio
            && !string.IsNullOrWhiteSpace(source.VolumeField)
            && !string.IsNullOrWhiteSpace(source.FolioField))
        {
            return AddTextClause(clauses, source.VolumeField, criteria.Volume, "Volume", sourceDisplayName, diagnostics, singleCharacterWildcard: true, caseInsensitive: false)
                && AddTextClause(clauses, source.FolioField, criteria.Folio, "Folio", sourceDisplayName, diagnostics, singleCharacterWildcard: true, caseInsensitive: false);
        }

        if (!string.IsNullOrWhiteSpace(source.CombinedVolumeFolioField))
        {
            if (!SafeFieldName.IsMatch(source.CombinedVolumeFolioField))
            {
                diagnostics.Add($"{sourceDisplayName} is excluded because combined Volume/Folio field mapping is invalid.");
                return false;
            }

            var combined = BuildCombinedVolumeFolioPattern(criteria.Volume, criteria.Folio, source.CombinedVolumeFolioSeparator);
            clauses.Add($"{source.CombinedVolumeFolioField} LIKE '{TranslateWildcardPattern(combined, singleCharacterWildcard: true)}'");
            return true;
        }

        return AddTextClause(clauses, source.VolumeField, criteria.Volume, "Volume", sourceDisplayName, diagnostics, singleCharacterWildcard: true, caseInsensitive: false)
            && AddTextClause(clauses, source.FolioField, criteria.Folio, "Folio", sourceDisplayName, diagnostics, singleCharacterWildcard: true, caseInsensitive: false);
    }

    private static string BuildCombinedVolumeFolioPattern(string? volume, string? folio, string separator)
    {
        var sep = string.IsNullOrEmpty(separator) ? "/" : separator;
        var left = string.IsNullOrWhiteSpace(volume) ? "*" : volume.Trim();
        var right = string.IsNullOrWhiteSpace(folio) ? "*" : folio.Trim();
        return $"{left}{sep}{right}";
    }

    private static bool AddParishClause(
        List<string> clauses,
        string? fieldName,
        ParcelSearchCriteria criteria,
        string sourceDisplayName,
        List<string> diagnostics)
    {
        var parishes = criteria.ParishNames
            .Where(parish => !string.IsNullOrWhiteSpace(parish))
            .Select(parish => parish.Trim())
            .Where(parish => !string.Equals(parish, "All", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (parishes.Length == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(fieldName) || !SafeFieldName.IsMatch(fieldName))
        {
            diagnostics.Add($"{sourceDisplayName} is excluded because Parish field mapping is not configured.");
            return false;
        }

        var values = parishes
            .SelectMany(ExpandParishSearchValues)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(parish => $"'{TranslateWildcardPattern(parish, singleCharacterWildcard: false)}'")
            .ToArray();
        clauses.Add($"UPPER({fieldName}) IN ({string.Join(", ", values)})");
        return true;
    }

    private static string? ResolveNameField(CompareEnterpriseCadasterSourceSettings source)
    {
        return FirstNonBlank(source.OwnerField, source.OccupantField, source.TaxpayerField);
    }

    private static string? FirstNonBlank(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static IReadOnlyList<string> BuildOutFields(CompareEnterpriseCadasterSourceSettings source)
    {
        return source.EvidenceFields()
            .Concat(new[]
            {
                source.PeNumberField,
                source.LotNumberField,
                source.CombinedVolumeFolioField,
                source.DpNumberField,
                source.RNumberField
            })
            .Where(field => !string.IsNullOrWhiteSpace(field) && SafeFieldName.IsMatch(field) && !IsGlobalIdFieldName(field))
            .Select(field => field!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<ParcelSearchLabelField> BuildLabelFields(
        CompareEnterpriseCadasterSourceSettings source,
        ParcelSearchCriteria criteria)
    {
        var fields = new List<ParcelSearchLabelField>();
        if (!string.IsNullOrWhiteSpace(criteria.Volume) || !string.IsNullOrWhiteSpace(criteria.Folio))
        {
            if (IsSafeConfiguredField(source.VolumeField) && IsSafeConfiguredField(source.FolioField))
            {
                fields.Add(new ParcelSearchLabelField("Vol/Folio", new[] { source.VolumeField!, source.FolioField! }));
            }
            else if (IsSafeConfiguredField(source.CombinedVolumeFolioField))
            {
                fields.Add(new ParcelSearchLabelField("Vol/Folio", new[] { source.CombinedVolumeFolioField! }, string.Empty));
            }
        }

        AddLabelField(fields, "Name", ResolveNameField(source), criteria.Name);
        AddLabelField(fields, "PE No.", source.PeNumberField, criteria.PeNumber);
        AddLabelField(fields, "LandVal No.", source.LandValuationNumberField, criteria.LandValuationNumber);
        AddLabelField(fields, "DP No.", source.DpNumberField, criteria.DpNumber);
        AddLabelField(fields, "R No.", source.RNumberField, criteria.RNumber);
        return DeduplicateLabelFields(fields);
    }

    private static void AddLabelField(
        List<ParcelSearchLabelField> fields,
        string label,
        string? fieldName,
        string? criteriaValue)
    {
        if (!string.IsNullOrWhiteSpace(criteriaValue) && IsSafeConfiguredField(fieldName))
        {
            fields.Add(new ParcelSearchLabelField(label, new[] { fieldName! }, string.Empty));
        }
    }

    internal static IReadOnlyList<ParcelSearchLabelField> DeduplicateLabelFields(IEnumerable<ParcelSearchLabelField> fields)
    {
        var result = new List<ParcelSearchLabelField>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            var fieldNames = field.FieldNames
                .Where(IsSafeConfiguredField)
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (fieldNames.Length == 0)
            {
                continue;
            }

            var key = $"{field.Label}\u001f{field.Separator}\u001f{string.Join("\u001e", fieldNames)}";
            if (seen.Add(key))
            {
                result.Add(field with { FieldNames = fieldNames });
            }
        }

        return result;
    }

    private static bool IsSafeConfiguredField([NotNullWhen(true)] string? fieldName)
    {
        return !string.IsNullOrWhiteSpace(fieldName) && SafeFieldName.IsMatch(fieldName);
    }

    private static bool IsGlobalIdFieldName([NotNullWhen(true)] string? fieldName)
    {
        return !string.IsNullOrWhiteSpace(fieldName)
            && fieldName.Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Equals("globalid", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ParcelSearchSourceDescriptor(
        string SourceKind,
        string SourceLayerName,
        string SourceDisplayName,
        CompareEnterpriseCadasterSourceSettings Settings);
}

public static class ParcelSearchWorkspaceResolver
{
    public static string ResolveWorkingGeodatabasePath(string caseFolderOutputRoot, string? userName = null)
    {
        var root = string.IsNullOrWhiteSpace(caseFolderOutputRoot)
            ? InnolaTransactionSettings.Default.CaseFolderOutputRoot
            : Environment.ExpandEnvironmentVariables(caseFolderOutputRoot.Trim());
        var safeUser = SanitizeUserName(userName);
        return Path.Combine(root, $"GDB_{safeUser}_working.gdb");
    }

    public static string ResolveCurrentUserName(string? innolaUserName)
    {
        if (!string.IsNullOrWhiteSpace(innolaUserName))
        {
            return innolaUserName.Trim();
        }

        var identityName = WindowsIdentity.GetCurrent()?.Name;
        if (!string.IsNullOrWhiteSpace(identityName))
        {
            var separator = identityName.LastIndexOf('\\');
            return separator >= 0 ? identityName[(separator + 1)..] : identityName;
        }

        return Environment.UserName;
    }

    private static string SanitizeUserName(string? userName)
    {
        var value = string.IsNullOrWhiteSpace(userName) ? ResolveCurrentUserName(null) : userName.Trim();
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) || char.IsWhiteSpace(character) ? '_' : character);
        }

        var sanitized = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "user" : sanitized;
    }
}

public static class ParcelSearchResultLayerContract
{
    public const string LayerName = "Parcel Search Results";
    public const string FeatureClassName = "Parcel_Search_Results";
    public const string SearchLabelField = "search_label";
    public const string SourceDisplayField = "source_display_name";
    public const string LegalChildLayerName = "Legal";
    public const string CadastralChildLayerName = "Cadastral";
    public const string SurveyChildLayerName = "Survey";
    public const string OtherChildLayerName = "Other";

    public static IReadOnlyList<string> ChildLayerNames { get; } = new ReadOnlyCollection<string>(new[]
    {
        LegalChildLayerName,
        CadastralChildLayerName,
        SurveyChildLayerName,
        OtherChildLayerName
    });

    public static IReadOnlyList<string> MetadataFields { get; } = new ReadOnlyCollection<string>(new[]
    {
        "source_layer",
        SourceDisplayField,
        "search_run_id",
        "search_timestamp",
        SearchLabelField,
        "source_object_id",
        "source_global_id",
        "parcel_id",
        "pid",
        "volume",
        "folio",
        "name",
        "pe_number",
        "landval_number",
        "parish"
    });

    public static string BuildSourceDefinitionQuery(string sourceDisplayName)
    {
        return $"{SourceDisplayField} = '{EscapeSqlLiteral(sourceDisplayName)}'";
    }

    public static string BuildOtherDefinitionQuery()
    {
        var knownSources = new[]
        {
            LegalChildLayerName,
            CadastralChildLayerName,
            SurveyChildLayerName
        }
            .Select(source => $"'{EscapeSqlLiteral(source)}'");
        return $"{SourceDisplayField} NOT IN ({string.Join(", ", knownSources)})";
    }

    private static string EscapeSqlLiteral(string value)
    {
        return value.Replace("'", "''");
    }
}

public interface IParcelSearchMapIntegrationService
{
    Task<ParcelSearchMapUpdateResult> UpdateResultsAsync(
        ParcelSearchQueryPlan plan,
        string workingGeodatabasePath,
        CancellationToken cancellationToken = default);

    Task ClearSearchAsync(CancellationToken cancellationToken = default);

    Task ZoomToResultsAsync(CancellationToken cancellationToken = default);
}

public interface IParcelSearchFeatureQueryClient
{
    Task<ParcelSearchSpatialFilter?> ResolveParishSpatialFilterAsync(
        ParcelSearchParishFilterRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParcelSearchFeatureSet>> QueryAsync(
        ParcelSearchSourceRequest request,
        ParcelSearchSpatialFilter? spatialFilter = null,
        CancellationToken cancellationToken = default);
}

public interface IParcelSearchParishOptionsProvider
{
    Task<IReadOnlyList<string>> LoadParishOptionsAsync(
        ParcelSearchParishSourceSettings parishSource,
        CancellationToken cancellationToken = default);
}

public interface IParcelSearchResultMaterializer
{
    Task<ParcelSearchMaterializationResult> MaterializeAsync(
        ParcelSearchQueryPlan plan,
        IReadOnlyList<ParcelSearchFeatureSet> featureSets,
        string workingGeodatabasePath,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);

    Task ZoomToResultsAsync(CancellationToken cancellationToken = default);
}

public sealed record ParcelSearchFeatureSet(
    ParcelSearchSourceRequest SourceRequest,
    string Json,
    int FeatureCount,
    bool LimitReached,
    IReadOnlyList<string> Diagnostics);

public sealed record ParcelSearchMaterializationResult(
    bool Success,
    int ResultCount,
    bool LimitReached,
    string Message,
    IReadOnlyList<string> Diagnostics);

public sealed record ParcelSearchMapUpdateResult(
    bool Success,
    int ResultCount,
    bool LimitReached,
    string Message,
    IReadOnlyList<string> Diagnostics)
{
    public static ParcelSearchMapUpdateResult Ready(int resultCount, bool limitReached, string message, IReadOnlyList<string>? diagnostics = null)
    {
        return new ParcelSearchMapUpdateResult(true, resultCount, limitReached, message, diagnostics ?? Array.Empty<string>());
    }

    public static ParcelSearchMapUpdateResult Failed(string message, IReadOnlyList<string>? diagnostics = null)
    {
        return new ParcelSearchMapUpdateResult(false, 0, false, message, diagnostics ?? Array.Empty<string>());
    }
}

public sealed class ParcelSearchMapIntegrationService : IParcelSearchMapIntegrationService
{
    private readonly IParcelSearchFeatureQueryClient queryClient;
    private readonly IParcelSearchResultMaterializer resultMaterializer;
    private readonly Func<bool> hasActiveMap;

    public ParcelSearchMapIntegrationService()
        : this(new ArcGisFeatureServerParcelSearchClient(), new ArcGisParcelSearchResultMaterializer(), () => MapView.Active?.Map is not null)
    {
    }

    internal ParcelSearchMapIntegrationService(
        IParcelSearchFeatureQueryClient queryClient,
        IParcelSearchResultMaterializer resultMaterializer,
        Func<bool>? hasActiveMap = null)
    {
        this.queryClient = queryClient;
        this.resultMaterializer = resultMaterializer;
        this.hasActiveMap = hasActiveMap ?? (() => true);
    }

    public Task<ParcelSearchMapUpdateResult> UpdateResultsAsync(
        ParcelSearchQueryPlan plan,
        string workingGeodatabasePath,
        CancellationToken cancellationToken = default)
    {
        return UpdateResultsCoreAsync(plan, workingGeodatabasePath, cancellationToken);
    }

    private async Task<ParcelSearchMapUpdateResult> UpdateResultsCoreAsync(
        ParcelSearchQueryPlan plan,
        string workingGeodatabasePath,
        CancellationToken cancellationToken)
    {
        if (!hasActiveMap())
        {
            return ParcelSearchMapUpdateResult.Failed("Open or activate an ArcGIS Pro map before running Parcel Search.");
        }

        var featureSets = new List<ParcelSearchFeatureSet>();
        var diagnostics = plan.Diagnostics.Select(ParcelSearchQueryPlanner.RedactDiagnostic).ToList();
        var spatialFilter = await ResolveSpatialFilterAsync(plan, diagnostics, cancellationToken).ConfigureAwait(false);
        if (plan.ParishFilterRequest is not null && spatialFilter is null)
        {
            var clearResult = await resultMaterializer
                .MaterializeAsync(plan, Array.Empty<ParcelSearchFeatureSet>(), workingGeodatabasePath, cancellationToken)
                .ConfigureAwait(false);
            diagnostics.AddRange(clearResult.Diagnostics.Select(ParcelSearchQueryPlanner.RedactDiagnostic));
            return ParcelSearchMapUpdateResult.Failed(
                "Parish filter could not be applied; search was not run.",
                diagnostics);
        }

        var failedSourceCount = 0;
        foreach (var request in plan.SourceRequests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            diagnostics.Add(BuildSourceQueryDiagnostic(request, spatialFilter));
            try
            {
                var results = await queryClient.QueryAsync(request, spatialFilter, cancellationToken).ConfigureAwait(false);
                featureSets.AddRange(results);
            }
            catch (Exception exception) when (exception is HttpRequestException
                or TaskCanceledException
                or JsonException
                or InvalidOperationException
                or ArgumentException
                or UriFormatException)
            {
                failedSourceCount++;
                diagnostics.Add($"{request.SourceDisplayName} query failed: {ParcelSearchQueryPlanner.RedactDiagnostic(exception.Message)}");
            }
        }

        if (failedSourceCount == plan.SourceRequests.Count && featureSets.Count == 0)
        {
            return ParcelSearchMapUpdateResult.Failed(
                "Parcel search failed for all selected sources.",
                diagnostics);
        }

        var materialization = await resultMaterializer
            .MaterializeAsync(plan, featureSets, workingGeodatabasePath, cancellationToken)
            .ConfigureAwait(false);
        diagnostics.AddRange(materialization.Diagnostics.Select(ParcelSearchQueryPlanner.RedactDiagnostic));
        return materialization.Success
            ? ParcelSearchMapUpdateResult.Ready(
                materialization.ResultCount,
                materialization.LimitReached || featureSets.Any(result => result.LimitReached),
                materialization.Message,
                diagnostics)
            : ParcelSearchMapUpdateResult.Failed(materialization.Message, diagnostics);
    }

    private static string BuildSourceQueryDiagnostic(ParcelSearchSourceRequest request, ParcelSearchSpatialFilter? spatialFilter)
    {
        return spatialFilter is null
            ? $"{request.SourceDisplayName} query where: {request.WhereClause}; outFields=*"
            : $"{request.SourceDisplayName} query where: {request.WhereClause}; outFields=*; spatialRel=esriSpatialRelIntersects";
    }

    private async Task<ParcelSearchSpatialFilter?> ResolveSpatialFilterAsync(
        ParcelSearchQueryPlan plan,
        List<string> diagnostics,
        CancellationToken cancellationToken)
    {
        if (plan.ParishFilterRequest is null)
        {
            return null;
        }

        try
        {
            var filter = await queryClient
                .ResolveParishSpatialFilterAsync(plan.ParishFilterRequest, cancellationToken)
                .ConfigureAwait(false);
            if (filter is null)
            {
                diagnostics.Add("Parish spatial filter returned no matching parish geometry.");
                return null;
            }

            diagnostics.AddRange(filter.Diagnostics.Select(ParcelSearchQueryPlanner.RedactDiagnostic));
            return filter;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException
            or JsonException
            or InvalidOperationException
            or ArgumentException
            or UriFormatException)
        {
            diagnostics.Add($"Parish spatial filter failed: {ParcelSearchQueryPlanner.RedactDiagnostic(exception.Message)}");
            return null;
        }
    }

    public async Task ClearSearchAsync(CancellationToken cancellationToken = default)
    {
        await resultMaterializer.ClearAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ZoomToResultsAsync(CancellationToken cancellationToken = default)
    {
        await resultMaterializer.ZoomToResultsAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ArcGisFeatureServerParcelSearchClient : IParcelSearchFeatureQueryClient
{
    private readonly HttpClient httpClient;
    private readonly IPortalAuthProvider portalAuthProvider;

    public ArcGisFeatureServerParcelSearchClient()
        : this(new HttpClient(), CompositePortalAuthProvider.CreateDefault())
    {
    }

    internal ArcGisFeatureServerParcelSearchClient(HttpClient httpClient, IPortalAuthProvider portalAuthProvider)
    {
        this.httpClient = httpClient;
        this.portalAuthProvider = portalAuthProvider;
    }

    public async Task<IReadOnlyList<ParcelSearchFeatureSet>> QueryAsync(
        ParcelSearchSourceRequest request,
        ParcelSearchSpatialFilter? spatialFilter = null,
        CancellationToken cancellationToken = default)
    {
        var token = await TryResolveTokenAsync(request, cancellationToken).ConfigureAwait(false);
        var results = new List<ParcelSearchFeatureSet>();
        var offset = 0;
        var limit = Math.Max(1, request.ResultLimit);
        var pageSize = Math.Max(1, request.PageSize);
        while (offset < limit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(pageSize, limit - offset);
            var parameters = BuildQueryParameters(request, offset, count, token, spatialFilter);
            using var response = spatialFilter is null
                ? await httpClient.GetAsync(BuildQueryUri(request.LayerUrl, parameters), cancellationToken).ConfigureAwait(false)
                : await httpClient.PostAsync(
                    $"{request.LayerUrl.TrimEnd('/')}/query",
                    new FormUrlEncodedContent(parameters
                        .Where(parameter => parameter.Value is not null)
                        .Select(parameter => new KeyValuePair<string, string>(parameter.Key, parameter.Value!))),
                    cancellationToken).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"{request.SourceDisplayName} returned HTTP {(int)response.StatusCode}.");
            }

            var parsed = ParseFeatureSetJson(request, json);
            results.Add(parsed with
            {
                Diagnostics = parsed.Diagnostics
                    .Concat(new[]
                    {
                        spatialFilter is null
                            ? $"{request.SourceDisplayName} query where: {request.WhereClause}; outFields=*"
                            : $"{request.SourceDisplayName} query where: {request.WhereClause}; outFields=*; spatialRel=esriSpatialRelIntersects"
                    })
                    .ToArray()
            });
            if (parsed.FeatureCount == 0 || !parsed.LimitReached || results.Sum(item => item.FeatureCount) >= limit)
            {
                break;
            }

            offset += parsed.FeatureCount;
        }

        return results;
    }

    public async Task<ParcelSearchSpatialFilter?> ResolveParishSpatialFilterAsync(
        ParcelSearchParishFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = await TryResolveTokenAsync(request.LayerUrl, "Parish List", cancellationToken).ConfigureAwait(false);
        var builder = new UriBuilder($"{request.LayerUrl.TrimEnd('/')}/query");
        var parameters = new Dictionary<string, string?>
        {
            ["f"] = "json",
            ["where"] = request.WhereClause,
            ["outFields"] = request.ParishNameField,
            ["returnGeometry"] = "true",
            ["resultRecordCount"] = "100"
        };
        if (!string.IsNullOrWhiteSpace(token))
        {
            parameters["token"] = token;
        }

        builder.Query = BuildQueryString(parameters);
        using var response = await httpClient.GetAsync(builder.Uri, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Parish source returned HTTP {(int)response.StatusCode}.");
        }

        return ParseParishSpatialFilter(request, json);
    }

    private async Task<string?> TryResolveTokenAsync(ParcelSearchSourceRequest request, CancellationToken cancellationToken)
    {
        return await TryResolveTokenAsync(request.LayerUrl, request.SourceDisplayName, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> TryResolveTokenAsync(string layerUrl, string layerRole, CancellationToken cancellationToken)
    {
        try
        {
            var portalUrl = ResolvePortalUrlForLayer(layerUrl);
            var result = await portalAuthProvider.GetTokenAsync(
                new PortalAuthRequest(portalUrl, layerUrl, "parcel search", layerRole),
                cancellationToken).ConfigureAwait(false);
            return result.Success ? result.Token : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or UriFormatException)
        {
            return null;
        }
    }

    private static Dictionary<string, string?> BuildQueryParameters(
        ParcelSearchSourceRequest request,
        int offset,
        int count,
        string? token,
        ParcelSearchSpatialFilter? spatialFilter)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["f"] = "json",
            ["where"] = request.WhereClause,
            ["outFields"] = "*",
            ["returnGeometry"] = "true",
            ["resultOffset"] = offset.ToString(CultureInfo.InvariantCulture),
            ["resultRecordCount"] = count.ToString(CultureInfo.InvariantCulture)
        };
        if (spatialFilter is not null)
        {
            parameters["geometry"] = spatialFilter.GeometryJson;
            parameters["geometryType"] = spatialFilter.GeometryType;
            parameters["spatialRel"] = "esriSpatialRelIntersects";
            if (!string.IsNullOrWhiteSpace(spatialFilter.SpatialReferenceJson))
            {
                parameters["inSR"] = spatialFilter.SpatialReferenceJson;
            }
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            parameters["token"] = token;
        }

        return parameters;
    }

    private static Uri BuildQueryUri(string layerUrl, Dictionary<string, string?> parameters)
    {
        var builder = new UriBuilder($"{layerUrl.TrimEnd('/')}/query");
        builder.Query = BuildQueryString(parameters);
        return builder.Uri;
    }

    private static string BuildQueryString(Dictionary<string, string?> parameters)
    {
        return string.Join("&", parameters.Select(parameter =>
            $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value ?? string.Empty)}"));
    }

    private static ParcelSearchFeatureSet ParseFeatureSetJson(ParcelSearchSourceRequest request, string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("error", out var error))
        {
            var message = error.TryGetProperty("message", out var messageValue)
                ? messageValue.GetString()
                : "FeatureServer returned an error.";
            throw new InvalidOperationException($"{request.SourceDisplayName} FeatureServer error: {BuildFeatureServerErrorMessage(error, message)}");
        }

        var count = document.RootElement.TryGetProperty("features", out var features) && features.ValueKind == JsonValueKind.Array
            ? features.GetArrayLength()
            : 0;
        var limitReached = document.RootElement.TryGetProperty("exceededTransferLimit", out var exceeded)
            && exceeded.ValueKind is JsonValueKind.True;
        return new ParcelSearchFeatureSet(request, json, count, limitReached, Array.Empty<string>());
    }

    private static ParcelSearchSpatialFilter? ParseParishSpatialFilter(ParcelSearchParishFilterRequest request, string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("error", out var error))
        {
            var message = error.TryGetProperty("message", out var messageValue)
                ? messageValue.GetString()
                : "FeatureServer returned an error.";
            throw new InvalidOperationException($"Parish source FeatureServer error: {BuildFeatureServerErrorMessage(error, message)}");
        }

        if (!document.RootElement.TryGetProperty("features", out var features)
            || features.ValueKind != JsonValueKind.Array
            || features.GetArrayLength() == 0)
        {
            return null;
        }

        var geometries = new List<JsonElement>();
        foreach (var feature in features.EnumerateArray())
        {
            if (feature.TryGetProperty("geometry", out var geometry))
            {
                geometries.Add(geometry.Clone());
            }
        }

        if (geometries.Count == 0)
        {
            return null;
        }

        var spatialReferenceJson = document.RootElement.TryGetProperty("spatialReference", out var sr)
            ? sr.GetRawText()
            : TryGetGeometrySpatialReferenceJson(geometries);
        var geometryJson = BuildMultipartPolygonJson(geometries, spatialReferenceJson);
        return new ParcelSearchSpatialFilter(
            geometryJson,
            "esriGeometryPolygon",
            spatialReferenceJson,
            new[] { $"Parish spatial filter where: {request.WhereClause}" });
    }

    private static string? TryGetGeometrySpatialReferenceJson(IEnumerable<JsonElement> geometries)
    {
        foreach (var geometry in geometries)
        {
            if (geometry.TryGetProperty("spatialReference", out var spatialReference))
            {
                return spatialReference.GetRawText();
            }
        }

        return null;
    }

    private static string BuildMultipartPolygonJson(IReadOnlyList<JsonElement> geometries, string? spatialReferenceJson)
    {
        var rings = new JsonArray();
        foreach (var geometry in geometries)
        {
            if (!geometry.TryGetProperty("rings", out var geometryRings) || geometryRings.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var ring in geometryRings.EnumerateArray())
            {
                rings.Add(JsonNode.Parse(ring.GetRawText()));
            }
        }

        if (rings.Count == 0)
        {
            return geometries[0].GetRawText();
        }

        var root = new JsonObject
        {
            ["rings"] = rings
        };
        if (!string.IsNullOrWhiteSpace(spatialReferenceJson))
        {
            root["spatialReference"] = JsonNode.Parse(spatialReferenceJson);
        }

        return root.ToJsonString();
    }

    private static string BuildFeatureServerErrorMessage(JsonElement error, string? message)
    {
        var parts = new List<string>();
        if (error.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number)
        {
            parts.Add($"code {code.GetInt32()}");
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            parts.Add(message!);
        }

        if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
        {
            var detailValues = details
                .EnumerateArray()
                .Select(detail => detail.GetString())
                .Where(detail => !string.IsNullOrWhiteSpace(detail))
                .ToArray();
            if (detailValues.Length > 0)
            {
                parts.Add(string.Join("; ", detailValues));
            }
        }

        return parts.Count == 0
            ? "FeatureServer returned an error."
            : string.Join(": ", parts);
    }

    internal static string ResolvePortalUrlForLayer(string layerUrl)
    {
        if (!Uri.TryCreate(layerUrl, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        return $"{uri.Scheme}://{uri.Host}/portal";
    }
}

public sealed class ArcGisParcelSearchParishOptionsProvider : IParcelSearchParishOptionsProvider
{
    private readonly HttpClient httpClient;
    private readonly IPortalAuthProvider portalAuthProvider;

    public ArcGisParcelSearchParishOptionsProvider()
        : this(new HttpClient(), CompositePortalAuthProvider.CreateDefault())
    {
    }

    internal ArcGisParcelSearchParishOptionsProvider(HttpClient httpClient, IPortalAuthProvider portalAuthProvider)
    {
        this.httpClient = httpClient;
        this.portalAuthProvider = portalAuthProvider;
    }

    public async Task<IReadOnlyList<string>> LoadParishOptionsAsync(
        ParcelSearchParishSourceSettings parishSource,
        CancellationToken cancellationToken = default)
    {
        if (!parishSource.Enabled
            || string.IsNullOrWhiteSpace(parishSource.LayerUrl)
            || string.IsNullOrWhiteSpace(parishSource.ParishNameField))
        {
            return Array.Empty<string>();
        }

        var token = await TryResolveTokenAsync(parishSource.LayerUrl, cancellationToken).ConfigureAwait(false);
        var parameters = new Dictionary<string, string?>
        {
            ["f"] = "json",
            ["where"] = "1=1",
            ["outFields"] = parishSource.ParishNameField,
            ["returnGeometry"] = "false",
            ["returnDistinctValues"] = "true",
            ["orderByFields"] = parishSource.ParishNameField,
            ["resultRecordCount"] = "100"
        };
        if (!string.IsNullOrWhiteSpace(token))
        {
            parameters["token"] = token;
        }

        var builder = new UriBuilder($"{parishSource.LayerUrl.TrimEnd('/')}/query")
        {
            Query = BuildQueryString(parameters)
        };
        using var response = await httpClient.GetAsync(builder.Uri, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<string>();
        }

        return ParseParishOptions(json, parishSource.ParishNameField);
    }

    private async Task<string?> TryResolveTokenAsync(string layerUrl, CancellationToken cancellationToken)
    {
        try
        {
            var portalUrl = ArcGisFeatureServerParcelSearchClient.ResolvePortalUrlForLayer(layerUrl);
            var result = await portalAuthProvider.GetTokenAsync(
                new PortalAuthRequest(portalUrl, layerUrl, "parcel search", "Parish List"),
                cancellationToken).ConfigureAwait(false);
            return result.Success ? result.Token : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or UriFormatException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ParseParishOptions(string json, string parishNameField)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return features
            .EnumerateArray()
            .Select(feature => feature.TryGetProperty("attributes", out var attributes)
                && attributes.TryGetProperty(parishNameField, out var value)
                    ? value.GetString()
                    : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static string BuildQueryString(Dictionary<string, string?> parameters)
    {
        return string.Join("&", parameters.Select(parameter =>
            $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value ?? string.Empty)}"));
    }
}

public sealed class ArcGisParcelSearchResultMaterializer : IParcelSearchResultMaterializer
{
    private static readonly Regex SafeFieldName = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private string? lastResultFeatureClassPath;

    public async Task<ParcelSearchMaterializationResult> MaterializeAsync(
        ParcelSearchQueryPlan plan,
        IReadOnlyList<ParcelSearchFeatureSet> featureSets,
        string workingGeodatabasePath,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<string>();
        var sourceSets = featureSets.Where(set => set.FeatureCount > 0).ToArray();
        var resultFeatureClassPath = Path.Combine(workingGeodatabasePath, ParcelSearchResultLayerContract.FeatureClassName);
        try
        {
            await EnsureWorkingGeodatabaseAsync(workingGeodatabasePath, cancellationToken).ConfigureAwait(false);
            if (sourceSets.Length == 0)
            {
                await ClearRowsIfPresentAsync(resultFeatureClassPath).ConfigureAwait(false);
                await ClearActiveMapSelectionAsync(cancellationToken).ConfigureAwait(false);
                lastResultFeatureClassPath = resultFeatureClassPath;
                return new ParcelSearchMaterializationResult(
                    true,
                    0,
                    false,
                    $"No parcels found. Cleared {ParcelSearchResultLayerContract.LayerName}.",
                    diagnostics);
            }

            var tempFeatureClasses = new List<string>();
            var tempRoot = Path.Combine(Path.GetTempPath(), $"parcel-search-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);
            try
            {
                await RemoveResultLayerFromMapAsync(cancellationToken).ConfigureAwait(false);
                await DeleteDatasetIfPresentAsync(resultFeatureClassPath).ConfigureAwait(false);
                for (var index = 0; index < sourceSets.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var set = sourceSets[index];
                    var jsonPath = Path.Combine(tempRoot, $"source-{index + 1}.json");
                    await File.WriteAllTextAsync(jsonPath, NormalizeFeatureSetJsonForMaterialization(set), cancellationToken).ConfigureAwait(false);
                    var tempFeatureClass = Path.Combine(
                        workingGeodatabasePath,
                        $"Parcel_Search_Temp_{index + 1}_{Guid.NewGuid():N}".Substring(0, 31));
                    await ExecuteRequiredToolAsync(
                        "conversion.JSONToFeatures",
                        Geoprocessing.MakeValueArray(jsonPath, tempFeatureClass),
                        cancellationToken).ConfigureAwait(false);
                    await StampMetadataAsync(tempFeatureClass, set.SourceRequest, diagnostics, cancellationToken).ConfigureAwait(false);
                    tempFeatureClasses.Add(tempFeatureClass);
                }

                if (tempFeatureClasses.Count == 1)
                {
                    await ExecuteRequiredToolAsync(
                        "management.CopyFeatures",
                        Geoprocessing.MakeValueArray(tempFeatureClasses[0], resultFeatureClassPath),
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await ExecuteRequiredToolAsync(
                        "management.Merge",
                        Geoprocessing.MakeValueArray(string.Join(";", tempFeatureClasses), resultFeatureClassPath),
                        cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                foreach (var tempFeatureClass in tempFeatureClasses)
                {
                    await DeleteDatasetIfPresentAsync(tempFeatureClass).ConfigureAwait(false);
                }

                TryDeleteDirectory(tempRoot);
            }

            lastResultFeatureClassPath = resultFeatureClassPath;
            try
            {
                await AddResultLayerToMapAsync(resultFeatureClassPath, plan.PopupFields, sourceSets, diagnostics, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or ArgumentException
                or NotSupportedException
                or UriFormatException
                or ArcGIS.Core.CalledOnWrongThreadException)
            {
                diagnostics.Add($"Parcel Search Results were written, but map layer refresh was skipped: {exception.Message}");
            }

            return new ParcelSearchMaterializationResult(
                true,
                sourceSets.Sum(set => set.FeatureCount),
                sourceSets.Any(set => set.LimitReached),
                $"Loaded {sourceSets.Sum(set => set.FeatureCount)} parcel search result feature(s) into {ParcelSearchResultLayerContract.LayerName}.",
                diagnostics);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or NotSupportedException
            or UriFormatException
            or ArcGIS.Core.CalledOnWrongThreadException)
        {
            return new ParcelSearchMaterializationResult(
                false,
                0,
                false,
                $"Parcel Search Results could not be written: {ParcelSearchQueryPlanner.RedactDiagnostic(exception.Message)}",
                diagnostics);
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(lastResultFeatureClassPath))
        {
            await ClearRowsIfPresentAsync(lastResultFeatureClassPath).ConfigureAwait(false);
        }

        await ClearActiveMapSelectionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ZoomToResultsAsync(CancellationToken cancellationToken = default)
    {
        var mapView = MapView.Active;
        if (mapView?.Map is null)
        {
            return;
        }

        var resultLayers = await QueuedTask.Run(() =>
            FindParcelSearchResultZoomLayers(mapView.Map), TaskCreationOptions.None).ConfigureAwait(false);
        if (resultLayers.Length > 0)
        {
            await mapView.ZoomToAsync(resultLayers).ConfigureAwait(false);
        }
    }

    private static async Task EnsureWorkingGeodatabaseAsync(string workingGeodatabasePath, CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(workingGeodatabasePath);
        if (string.IsNullOrWhiteSpace(parent))
        {
            throw new InvalidOperationException("Working geodatabase parent folder could not be resolved.");
        }

        Directory.CreateDirectory(parent);
        if (Directory.Exists(workingGeodatabasePath) && !Directory.EnumerateFileSystemEntries(workingGeodatabasePath).Any())
        {
            Directory.Delete(workingGeodatabasePath);
        }

        if (Directory.Exists(workingGeodatabasePath))
        {
            return;
        }

        await ExecuteRequiredToolAsync(
            "management.CreateFileGDB",
            Geoprocessing.MakeValueArray(parent, Path.GetFileName(workingGeodatabasePath)),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task StampMetadataAsync(
        string featureClassPath,
        ParcelSearchSourceRequest request,
        ICollection<string> diagnostics,
        CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString("N");
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        await EnsureTextFieldAsync(featureClassPath, "source_layer", 64, cancellationToken).ConfigureAwait(false);
        await EnsureTextFieldAsync(featureClassPath, "source_display_name", 64, cancellationToken).ConfigureAwait(false);
        await EnsureTextFieldAsync(featureClassPath, "search_run_id", 64, cancellationToken).ConfigureAwait(false);
        await EnsureTextFieldAsync(featureClassPath, "search_timestamp", 32, cancellationToken).ConfigureAwait(false);
        await EnsureTextFieldAsync(featureClassPath, ParcelSearchResultLayerContract.SearchLabelField, 512, cancellationToken).ConfigureAwait(false);
        await EnsureTextFieldAsync(featureClassPath, "source_object_id", 128, cancellationToken).ConfigureAwait(false);
        await EnsureTextFieldAsync(featureClassPath, "source_global_id", 128, cancellationToken).ConfigureAwait(false);
        await EnsureTextFieldAsync(featureClassPath, "parcel_id", 128, cancellationToken).ConfigureAwait(false);
        await EnsureTextFieldAsync(featureClassPath, "pid", 128, cancellationToken).ConfigureAwait(false);
        await EnsureTextFieldAsync(featureClassPath, "volume", 128, cancellationToken).ConfigureAwait(false);
        await EnsureTextFieldAsync(featureClassPath, "folio", 128, cancellationToken).ConfigureAwait(false);
        await EnsureTextFieldAsync(featureClassPath, "name", 256, cancellationToken).ConfigureAwait(false);
        await EnsureTextFieldAsync(featureClassPath, "pe_number", 128, cancellationToken).ConfigureAwait(false);
        await EnsureTextFieldAsync(featureClassPath, "landval_number", 128, cancellationToken).ConfigureAwait(false);
        await EnsureTextFieldAsync(featureClassPath, "parish", 128, cancellationToken).ConfigureAwait(false);
        await CalculateTextFieldAsync(featureClassPath, "source_layer", request.SourceLayerName, cancellationToken).ConfigureAwait(false);
        await CalculateTextFieldAsync(featureClassPath, "source_display_name", request.SourceDisplayName, cancellationToken).ConfigureAwait(false);
        await CalculateTextFieldAsync(featureClassPath, "search_run_id", runId, cancellationToken).ConfigureAwait(false);
        await CalculateTextFieldAsync(featureClassPath, "search_timestamp", timestamp, cancellationToken).ConfigureAwait(false);
        var availableFields = await LoadFieldNamesAsync(featureClassPath, cancellationToken).ConfigureAwait(false);
        await CalculateTextFromSourceFieldAsync(featureClassPath, "source_object_id", request.FieldMap.ObjectIdField, availableFields, cancellationToken).ConfigureAwait(false);
        await CalculateTextFromSourceFieldAsync(featureClassPath, "source_global_id", request.FieldMap.GlobalIdField, availableFields, cancellationToken).ConfigureAwait(false);
        await CalculateTextFromSourceFieldAsync(featureClassPath, "parcel_id", request.FieldMap.ParcelIdField, availableFields, cancellationToken).ConfigureAwait(false);
        await CalculateTextFromSourceFieldAsync(featureClassPath, "pid", request.FieldMap.PidField, availableFields, cancellationToken).ConfigureAwait(false);
        await CalculateTextFromSourceFieldAsync(featureClassPath, "volume", request.FieldMap.VolumeField, availableFields, cancellationToken).ConfigureAwait(false);
        await CalculateTextFromSourceFieldAsync(featureClassPath, "folio", request.FieldMap.FolioField, availableFields, cancellationToken).ConfigureAwait(false);
        await CalculateTextFromSourceFieldAsync(featureClassPath, "name", ResolveDisplayNameField(request.FieldMap), availableFields, cancellationToken).ConfigureAwait(false);
        await CalculateTextFromSourceFieldAsync(featureClassPath, "pe_number", request.FieldMap.PeNumberField, availableFields, cancellationToken).ConfigureAwait(false);
        await CalculateTextFromSourceFieldAsync(featureClassPath, "landval_number", request.FieldMap.LandValuationNumberField, availableFields, cancellationToken).ConfigureAwait(false);
        await CalculateTextFromSourceFieldAsync(featureClassPath, "parish", request.FieldMap.ParishField, availableFields, cancellationToken).ConfigureAwait(false);
        var labelDiagnostics = await CalculateSearchLabelFieldAsync(featureClassPath, request, availableFields, cancellationToken).ConfigureAwait(false);
        foreach (var diagnostic in labelDiagnostics)
        {
            diagnostics.Add(diagnostic);
        }
    }

    private static string NormalizeFeatureSetJsonForMaterialization(ParcelSearchFeatureSet featureSet)
    {
        var root = JsonNode.Parse(featureSet.Json) as JsonObject;
        if (root?["features"] is not JsonArray features)
        {
            return featureSet.Json;
        }

        RemoveGlobalIdFields(root, features);

        var requiredFields = BuildRequiredMaterializationFields(featureSet.SourceRequest);
        if (requiredFields.Length == 0)
        {
            return root.ToJsonString();
        }

        foreach (var feature in features.OfType<JsonObject>())
        {
            if (feature["attributes"] is not JsonObject attributes)
            {
                attributes = new JsonObject();
                feature["attributes"] = attributes;
            }

            foreach (var field in requiredFields)
            {
                if (!attributes.ContainsKey(field))
                {
                    attributes[field] = string.Empty;
                }
            }
        }

        return root.ToJsonString();
    }

    private static string[] BuildRequiredMaterializationFields(ParcelSearchSourceRequest request)
    {
        return request.LabelFields
            .SelectMany(field => field.FieldNames)
            .Concat(new[] { request.FieldMap.ObjectIdField, request.FieldMap.LandValuationNumberField })
            .Where(IsSafeConfiguredField)
            .Where(field => !IsGlobalIdFieldName(field))
            .Select(field => field!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsGlobalIdFieldName([NotNullWhen(true)] string? fieldName)
    {
        return !string.IsNullOrWhiteSpace(fieldName)
            && fieldName.Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Equals("globalid", StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveGlobalIdFields(JsonObject root, JsonArray features)
    {
        var globalIdNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root["fields"] is JsonArray fields)
        {
            foreach (var field in fields.OfType<JsonObject>().ToArray())
            {
                var name = TryGetJsonString(field["name"]);
                var type = TryGetJsonString(field["type"]);
                if (IsGlobalIdFieldName(name) || string.Equals(type, "esriFieldTypeGlobalID", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        globalIdNames.Add(name);
                    }

                    fields.Remove(field);
                }
            }
        }

        if (globalIdNames.Count == 0)
        {
            globalIdNames.Add("globalid");
            globalIdNames.Add("GlobalID");
            globalIdNames.Add("GLOBALID");
        }

        foreach (var feature in features.OfType<JsonObject>())
        {
            if (feature["attributes"] is not JsonObject attributes)
            {
                continue;
            }

            foreach (var fieldName in globalIdNames)
            {
                attributes.Remove(fieldName);
            }
        }
    }

    private static string? TryGetJsonString(JsonNode? node)
    {
        return node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;
    }

    private static bool IsSafeConfiguredField([NotNullWhen(true)] string? fieldName)
    {
        return !string.IsNullOrWhiteSpace(fieldName) && SafeFieldName.IsMatch(fieldName);
    }

    private static Task AddTextFieldAsync(string featureClassPath, string fieldName, int length, CancellationToken cancellationToken)
    {
        return ExecuteRequiredToolAsync(
            "management.AddField",
            Geoprocessing.MakeValueArray(featureClassPath, fieldName, "TEXT", null, null, length),
            cancellationToken);
    }

    private static async Task EnsureTextFieldAsync(string featureClassPath, string fieldName, int length, CancellationToken cancellationToken)
    {
        var availableFields = await LoadFieldNamesAsync(featureClassPath, cancellationToken).ConfigureAwait(false);
        if (availableFields.Contains(fieldName))
        {
            return;
        }

        await AddTextFieldAsync(featureClassPath, fieldName, length, cancellationToken).ConfigureAwait(false);
    }

    private static Task CalculateTextFieldAsync(string featureClassPath, string fieldName, string value, CancellationToken cancellationToken)
    {
        var expression = $"'{value.Replace("'", "\\'")}'";
        return ExecuteRequiredToolAsync(
            "management.CalculateField",
            Geoprocessing.MakeValueArray(featureClassPath, fieldName, expression, "PYTHON3"),
            cancellationToken);
    }

    private static Task CalculateTextFromSourceFieldAsync(
        string featureClassPath,
        string targetFieldName,
        string? sourceFieldName,
        IReadOnlySet<string> availableFields,
        CancellationToken cancellationToken)
    {
        var actualSourceFieldName = ResolveAvailableFieldName(sourceFieldName, availableFields);
        if (string.IsNullOrWhiteSpace(actualSourceFieldName))
        {
            return CalculateTextFieldAsync(featureClassPath, targetFieldName, string.Empty, cancellationToken);
        }

        return ExecuteRequiredToolAsync(
            "management.CalculateField",
            Geoprocessing.MakeValueArray(
                featureClassPath,
                targetFieldName,
                $"clean(!{actualSourceFieldName}!)",
                "PYTHON3",
                BuildCleanTextCodeBlock()),
            cancellationToken);
    }

    private static string? ResolveDisplayNameField(CompareEnterpriseCadasterSourceSettings source)
    {
        return FirstNonBlank(source.OwnerField, source.OccupantField, source.TaxpayerField);
    }

    private static string? FirstNonBlank(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string BuildCleanTextCodeBlock()
    {
        return """
            def clean(value):
                if value is None:
                    return ''
                text = str(value).strip()
                if text.lower() in ('', '<null>', 'null', 'none'):
                    return ''
                return text
            """;
    }

    private static async Task<IReadOnlyList<string>> CalculateSearchLabelFieldAsync(
        string featureClassPath,
        ParcelSearchSourceRequest request,
        IReadOnlySet<string> availableFields,
        CancellationToken cancellationToken)
    {
        if (request.LabelFields.Count == 0)
        {
            await CalculateTextFieldAsync(featureClassPath, ParcelSearchResultLayerContract.SearchLabelField, string.Empty, cancellationToken).ConfigureAwait(false);
            return new[]
            {
                BuildSearchLabelDiagnostic(request.SourceDisplayName, request.LabelFields, Array.Empty<ParcelSearchLabelField>(), string.Empty)
            };
        }

        var labelFields = ParcelSearchQueryPlanner.DeduplicateLabelFields(request.LabelFields
            .Select(field => field with
            {
                FieldNames = field.FieldNames
                    .Select(name => ResolveAvailableFieldName(name, availableFields))
                    .Where(IsSafeConfiguredField)
                    .Select(name => name!)
                    .ToArray()
            })
            .Where(field => field.FieldNames.Count > 0))
            .ToArray();
        if (labelFields.Length == 0)
        {
            await CalculateTextFieldAsync(featureClassPath, ParcelSearchResultLayerContract.SearchLabelField, string.Empty, cancellationToken).ConfigureAwait(false);
            return new[]
            {
                BuildSearchLabelDiagnostic(request.SourceDisplayName, request.LabelFields, labelFields, string.Empty)
            };
        }

        var fieldTokens = labelFields
            .SelectMany(field => field.FieldNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(field => $"!{field}!")
            .ToArray();
        var expression = $"build_label({string.Join(", ", fieldTokens)})";
        await ExecuteRequiredToolAsync(
            "management.CalculateField",
            Geoprocessing.MakeValueArray(
                featureClassPath,
                ParcelSearchResultLayerContract.SearchLabelField,
                expression,
                "PYTHON3",
                BuildSearchLabelCodeBlock(labelFields)),
            cancellationToken).ConfigureAwait(false);
        var sampleLabel = await ReadFirstTextValueAsync(featureClassPath, ParcelSearchResultLayerContract.SearchLabelField, cancellationToken).ConfigureAwait(false);
        return new[]
        {
            BuildSearchLabelDiagnostic(request.SourceDisplayName, request.LabelFields, labelFields, sampleLabel)
        };
    }

    internal static string? ResolveAvailableFieldName(string? configuredFieldName, IReadOnlySet<string> availableFields)
    {
        if (!IsSafeConfiguredField(configuredFieldName))
        {
            return null;
        }

        return availableFields.FirstOrDefault(field => string.Equals(field, configuredFieldName, StringComparison.OrdinalIgnoreCase));
    }

    internal static IReadOnlyList<ParcelSearchLabelField> ResolveAvailableLabelFields(
        IEnumerable<ParcelSearchLabelField> configuredLabelFields,
        IReadOnlySet<string> availableFields)
    {
        return ParcelSearchQueryPlanner.DeduplicateLabelFields(configuredLabelFields
            .Select(field => field with
            {
                FieldNames = field.FieldNames
                    .Select(name => ResolveAvailableFieldName(name, availableFields))
                    .Where(IsSafeConfiguredField)
                    .Select(name => name!)
                    .ToArray()
            })
            .Where(field => field.FieldNames.Count > 0));
    }

    internal static string BuildSearchLabelDiagnostic(
        string sourceDisplayName,
        IReadOnlyList<ParcelSearchLabelField> configuredLabelFields,
        IReadOnlyList<ParcelSearchLabelField> actualLabelFields,
        string? sampleSearchLabel)
    {
        static string FormatFields(IEnumerable<ParcelSearchLabelField> fields)
        {
            var values = fields
                .Select(field => $"{field.Label}=[{string.Join("|", field.FieldNames)}]")
                .ToArray();
            return values.Length == 0 ? "<none>" : string.Join("; ", values);
        }

        return $"Parcel Search Results label diagnostics: source_display_name={sourceDisplayName}; configured_label_fields={FormatFields(configuredLabelFields)}; actual_label_fields={FormatFields(actualLabelFields)}; sample_search_label={ParcelSearchQueryPlanner.RedactDiagnostic(sampleSearchLabel ?? string.Empty)}";
    }

    private static Task<IReadOnlySet<string>> LoadFieldNamesAsync(string featureClassPath, CancellationToken cancellationToken)
    {
        return QueuedTask.Run<IReadOnlySet<string>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var geodatabasePath = Path.GetDirectoryName(featureClassPath);
            var datasetName = Path.GetFileName(featureClassPath);
            if (string.IsNullOrWhiteSpace(geodatabasePath) || string.IsNullOrWhiteSpace(datasetName))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            using var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(geodatabasePath)));
            using var featureClass = geodatabase.OpenDataset<FeatureClass>(datasetName);
            return featureClass.GetDefinition()
                .GetFields()
                .Select(field => field.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }, TaskCreationOptions.None);
    }

    private static Task<string> ReadFirstTextValueAsync(string featureClassPath, string fieldName, CancellationToken cancellationToken)
    {
        return QueuedTask.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var geodatabasePath = Path.GetDirectoryName(featureClassPath);
            var datasetName = Path.GetFileName(featureClassPath);
            if (string.IsNullOrWhiteSpace(geodatabasePath) || string.IsNullOrWhiteSpace(datasetName))
            {
                return string.Empty;
            }

            using var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(geodatabasePath)));
            using var featureClass = geodatabase.OpenDataset<FeatureClass>(datasetName);
            using var cursor = featureClass.Search(new QueryFilter { WhereClause = $"{fieldName} IS NOT NULL" }, false);
            while (cursor.MoveNext())
            {
                using var row = cursor.Current;
                var value = row[fieldName]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }, TaskCreationOptions.None);
    }

    private static string BuildSearchLabelCodeBlock(IReadOnlyList<ParcelSearchLabelField> labelFields)
    {
        var orderedFields = labelFields
            .SelectMany(field => field.FieldNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var indexes = orderedFields
            .Select((field, index) => new { field, index })
            .ToDictionary(item => item.field, item => item.index, StringComparer.OrdinalIgnoreCase);
        var builder = new StringBuilder();
        builder.AppendLine("def _clean(value):");
        builder.AppendLine("    if value is None:");
        builder.AppendLine("        return ''");
        builder.AppendLine("    text = str(value).strip()");
        builder.AppendLine("    if text.lower() in ('', '<null>', 'null', 'none'):");
        builder.AppendLine("        return ''");
        builder.AppendLine("    return text");
        builder.AppendLine();
        builder.AppendLine($"def build_label({string.Join(", ", orderedFields.Select((_, index) => $"v{index}"))}):");
        builder.AppendLine("    parts = []");
        for (var index = 0; index < labelFields.Count; index++)
        {
            var labelField = labelFields[index];
            var valueExpressions = labelField.FieldNames
                .Select(field => $"_clean(v{indexes[field]})")
                .ToArray();
            builder.AppendLine($"    p{index}_values = [{string.Join(", ", valueExpressions)}]");
            builder.AppendLine($"    p{index}_values = [value for value in p{index}_values if value]");
            builder.AppendLine($"    if p{index}_values:");
            var separator = string.IsNullOrEmpty(labelField.Separator) ? " " : labelField.Separator;
            builder.AppendLine($"        parts.append('{EscapePythonSingleQuoted(labelField.Label)}: ' + '{EscapePythonSingleQuoted(separator)}'.join(p{index}_values))");
        }

        builder.AppendLine("    return '\\n'.join(parts)");
        return builder.ToString();
    }

    private static string EscapePythonSingleQuoted(string value)
    {
        return value.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    private static async Task AddResultLayerToMapAsync(
        string resultFeatureClassPath,
        IReadOnlyList<ParcelSearchPopupFieldSettings> popupFields,
        IReadOnlyList<ParcelSearchFeatureSet> sourceSets,
        List<string> diagnostics,
        CancellationToken cancellationToken)
    {
        var mapView = MapView.Active;
        if (mapView?.Map is null)
        {
            diagnostics.Add("No active map was available to display Parcel Search Results.");
            return;
        }

        var layers = new List<Layer>();
        await QueuedTask.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            RemoveResultLayer(mapView.Map);
            var resultGroup = LayerFactory.Instance.CreateGroupLayer(
                mapView.Map,
                0,
                ParcelSearchResultLayerContract.LayerName);
            var featureClassUri = new Uri(resultFeatureClassPath);
            foreach (var sourceLayer in BuildResultSourceLayerDefinitions())
            {
                var layer = LayerFactory.Instance.CreateLayer(featureClassUri, resultGroup);
                layer.SetName(sourceLayer.Name);
                if (layer is FeatureLayer featureLayer)
                {
                    featureLayer.SetDefinitionQuery(sourceLayer.DefinitionQuery);
                    ApplyResultLayerStyle(featureLayer, sourceLayer, diagnostics);
                    ApplyResultLayerPopupProfile(featureLayer, popupFields, diagnostics);
                    ApplyResultLayerLabels(featureLayer, diagnostics);
                    SelectResultFeatures(
                        featureLayer,
                        layers.Count == 0 ? SelectionCombinationMethod.New : SelectionCombinationMethod.Add,
                        diagnostics);
                }

                layers.Add(layer);
            }

            SelectSourceFeatures(mapView.Map, sourceSets, diagnostics);
        }, TaskCreationOptions.None).ConfigureAwait(false);

        try
        {
            await mapView.ZoomToAsync(layers).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            diagnostics.Add($"Parcel Search Results were loaded, but zoom failed: {exception.Message}");
        }
    }

    private static void RemoveResultLayer(Map map)
    {
        foreach (var layer in map.Layers
                     .Where(layer => string.Equals(layer.Name, ParcelSearchResultLayerContract.LayerName, StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            map.RemoveLayer(layer);
        }
    }

    private static Layer[] FindParcelSearchResultZoomLayers(Map map)
    {
        var resultGroup = map.Layers
            .OfType<GroupLayer>()
            .FirstOrDefault(layer => string.Equals(layer.Name, ParcelSearchResultLayerContract.LayerName, StringComparison.OrdinalIgnoreCase));
        if (resultGroup is not null)
        {
            var childLayers = FlattenLayers(resultGroup.Layers)
                .Where(layer => layer is FeatureLayer)
                .ToArray();
            return childLayers.Length > 0 ? childLayers : new Layer[] { resultGroup };
        }

        return map.Layers
            .OfType<FeatureLayer>()
            .Where(layer => string.Equals(layer.Name, ParcelSearchResultLayerContract.LayerName, StringComparison.OrdinalIgnoreCase))
            .Cast<Layer>()
            .ToArray();
    }

    private static async Task RemoveResultLayerFromMapAsync(CancellationToken cancellationToken)
    {
        var mapView = MapView.Active;
        if (mapView?.Map is null)
        {
            return;
        }

        await QueuedTask.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            RemoveResultLayer(mapView.Map);
        }, TaskCreationOptions.None).ConfigureAwait(false);
    }

    private static IReadOnlyList<ParcelSearchResultSourceLayerDefinition> BuildResultSourceLayerDefinitions()
    {
        return new[]
        {
            new ParcelSearchResultSourceLayerDefinition(
                ParcelSearchResultLayerContract.LegalChildLayerName,
                ParcelSearchResultLayerContract.BuildSourceDefinitionQuery(ParcelSearchResultLayerContract.LegalChildLayerName),
                37,
                99,
                235),
            new ParcelSearchResultSourceLayerDefinition(
                ParcelSearchResultLayerContract.CadastralChildLayerName,
                ParcelSearchResultLayerContract.BuildSourceDefinitionQuery(ParcelSearchResultLayerContract.CadastralChildLayerName),
                217,
                119,
                6),
            new ParcelSearchResultSourceLayerDefinition(
                ParcelSearchResultLayerContract.SurveyChildLayerName,
                ParcelSearchResultLayerContract.BuildSourceDefinitionQuery(ParcelSearchResultLayerContract.SurveyChildLayerName),
                5,
                150,
                105),
            new ParcelSearchResultSourceLayerDefinition(
                ParcelSearchResultLayerContract.OtherChildLayerName,
                ParcelSearchResultLayerContract.BuildOtherDefinitionQuery(),
                107,
                114,
                128)
        };
    }

    private static void ApplyResultLayerStyle(
        FeatureLayer featureLayer,
        ParcelSearchResultSourceLayerDefinition sourceLayer,
        ICollection<string> diagnostics)
    {
        try
        {
            featureLayer.SetRenderer(new CIMSimpleRenderer
            {
                Symbol = BuildPolygonSymbol(sourceLayer.Red, sourceLayer.Green, sourceLayer.Blue)
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or ArgumentException)
        {
            diagnostics.Add($"Parcel Search Results styling was skipped: {exception.Message}");
        }
    }

    private static CIMSymbolReference BuildPolygonSymbol(byte red, byte green, byte blue)
    {
        var fill = ColorFactory.Instance.CreateRGBColor(red, green, blue, 30);
        var outline = SymbolFactory.Instance.ConstructStroke(
            ColorFactory.Instance.CreateRGBColor(red, green, blue, 95),
            0.4,
            SimpleLineStyle.Solid);
        return SymbolFactory.Instance.ConstructPolygonSymbol(fill, SimpleFillStyle.Solid, outline).MakeSymbolReference();
    }

    private sealed record ParcelSearchResultSourceLayerDefinition(
        string Name,
        string DefinitionQuery,
        byte Red,
        byte Green,
        byte Blue);

    private static void ApplyResultLayerPopupProfile(
        FeatureLayer featureLayer,
        IReadOnlyList<ParcelSearchPopupFieldSettings> popupFields,
        ICollection<string> diagnostics)
    {
        try
        {
            var fieldDescriptions = featureLayer.GetFieldDescriptions();
            var curatedFields = CreateResultPopupFieldProfile(popupFields);
            var reorderedFields = new List<FieldDescription>();
            foreach (var field in fieldDescriptions)
            {
                if (string.IsNullOrWhiteSpace(field.Name))
                {
                    continue;
                }

                if (curatedFields.TryGetValue(field.Name, out var profile))
                {
                    field.IsVisible = profile.Visible;
                    field.Alias = profile.Alias;
                }
                else
                {
                    field.IsVisible = false;
                }
            }

            foreach (var popupField in popupFields.Where(field => field.Visible))
            {
                var field = fieldDescriptions.FirstOrDefault(item =>
                    string.Equals(item.Name, popupField.FieldName, StringComparison.OrdinalIgnoreCase));
                if (field is not null && !reorderedFields.Contains(field))
                {
                    reorderedFields.Add(field);
                }
            }

            reorderedFields.AddRange(fieldDescriptions.Where(field => !reorderedFields.Contains(field)));
            featureLayer.SetFieldDescriptions(reorderedFields);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or ArgumentException)
        {
            diagnostics.Add($"Parcel Search Results popup customization was skipped: {exception.Message}");
        }
    }

    private static IReadOnlyDictionary<string, ResultPopupFieldProfile> CreateResultPopupFieldProfile(
        IReadOnlyList<ParcelSearchPopupFieldSettings> popupFields)
    {
        var configured = popupFields.Count == 0
            ? ParcelSearchPopupFieldSettings.Defaults
            : popupFields;
        return configured
            .GroupBy(field => field.FieldName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new ResultPopupFieldProfile(group.First().Alias, group.First().Visible),
                StringComparer.OrdinalIgnoreCase);
    }

    private sealed record ResultPopupFieldProfile(string Alias, bool Visible);

    private static void ApplyResultLayerLabels(FeatureLayer featureLayer, ICollection<string> diagnostics)
    {
        try
        {
            if (featureLayer.GetDefinition() is not CIMFeatureLayer definition)
            {
                diagnostics.Add("Parcel Search Results labels were skipped because layer definition was unavailable.");
                return;
            }

            var labelClass = definition.LabelClasses?.FirstOrDefault() ?? new CIMLabelClass();
            labelClass.Name = "Search Criteria";
            labelClass.ExpressionEngine = LabelExpressionEngine.Arcade;
            labelClass.Expression = $"$feature.{ParcelSearchResultLayerContract.SearchLabelField}";
            labelClass.Visibility = true;
            labelClass.TextSymbol = BuildResultLabelSymbol();
            definition.LabelClasses = new[] { labelClass };
            definition.LabelVisibility = true;
            featureLayer.SetDefinition(definition);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or ArgumentException)
        {
            diagnostics.Add($"Parcel Search Results labels were skipped: {exception.Message}");
        }
    }

    private static CIMSymbolReference BuildResultLabelSymbol()
    {
        var textSymbol = SymbolFactory.Instance.ConstructTextSymbol(
            ColorFactory.Instance.CreateRGBColor(17, 24, 39, 100),
            9.0,
            "Arial",
            "Regular");
        textSymbol.HorizontalAlignment = HorizontalAlignment.Center;
        textSymbol.VerticalAlignment = VerticalAlignment.Center;
        return textSymbol.MakeSymbolReference();
    }

    private static void SelectResultFeatures(
        FeatureLayer featureLayer,
        SelectionCombinationMethod selectionMethod,
        ICollection<string> diagnostics)
    {
        try
        {
            featureLayer.Select(new QueryFilter { WhereClause = "1=1" }, selectionMethod);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or ArgumentException)
        {
            diagnostics.Add($"Parcel Search Results selection was skipped: {exception.Message}");
        }
    }

    private static void SelectSourceFeatures(
        Map map,
        IReadOnlyList<ParcelSearchFeatureSet> sourceSets,
        ICollection<string> diagnostics)
    {
        foreach (var featureSet in sourceSets)
        {
            var selection = BuildSourceSelection(featureSet);
            if (selection is null)
            {
                continue;
            }

            var matchingLayers = FlattenLayers(map.Layers)
                .OfType<FeatureLayer>()
                .Where(layer => IsMatchingSourceLayer(layer, featureSet.SourceRequest))
                .ToArray();
            foreach (var layer in matchingLayers)
            {
                try
                {
                    layer.Select(new QueryFilter { WhereClause = selection.WhereClause }, SelectionCombinationMethod.Add);
                }
                catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or ArgumentException)
                {
                    diagnostics.Add($"{featureSet.SourceRequest.SourceDisplayName} source layer selection was skipped: {exception.Message}");
                }
            }
        }
    }

    private static SourceSelectionClause? BuildSourceSelection(ParcelSearchFeatureSet featureSet)
    {
        var objectIdField = featureSet.SourceRequest.FieldMap.ObjectIdField;
        if (IsSafeConfiguredField(objectIdField))
        {
            var objectIds = ReadAttributeValues(featureSet.Json, objectIdField)
                .Where(value => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(999)
                .ToArray();
            if (objectIds.Length > 0)
            {
                return new SourceSelectionClause($"{objectIdField} IN ({string.Join(",", objectIds)})");
            }
        }

        var globalIdField = featureSet.SourceRequest.FieldMap.GlobalIdField;
        if (IsSafeConfiguredField(globalIdField))
        {
            var globalIds = ReadAttributeValues(featureSet.Json, globalIdField)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(999)
                .Select(value => $"'{value.Replace("'", "''")}'")
                .ToArray();
            if (globalIds.Length > 0)
            {
                return new SourceSelectionClause($"{globalIdField} IN ({string.Join(",", globalIds)})");
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ReadAttributeValues(string featureSetJson, string fieldName)
    {
        using var document = JsonDocument.Parse(featureSetJson);
        if (!document.RootElement.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return features
            .EnumerateArray()
            .Select(feature => feature.TryGetProperty("attributes", out var attributes)
                && attributes.TryGetProperty(fieldName, out var value)
                    ? value.ToString()
                    : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();
    }

    private static bool IsMatchingSourceLayer(FeatureLayer layer, ParcelSearchSourceRequest request)
    {
        var names = new[]
        {
            request.SourceLayerName,
            request.SourceDisplayName,
            request.FieldMap.SourceName,
            request.FieldMap.SublayerName,
            request.FieldMap.DisplayName
        };
        return names.Any(name => !string.IsNullOrWhiteSpace(name)
            && string.Equals(layer.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<Layer> FlattenLayers(IEnumerable<Layer> layers)
    {
        foreach (var layer in layers)
        {
            yield return layer;
            if (layer is CompositeLayer compositeLayer)
            {
                foreach (var childLayer in FlattenLayers(compositeLayer.Layers))
                {
                    yield return childLayer;
                }
            }
        }
    }

    private sealed record SourceSelectionClause(string WhereClause);

    private static async Task ClearActiveMapSelectionAsync(CancellationToken cancellationToken)
    {
        var mapView = MapView.Active;
        if (mapView?.Map is null)
        {
            return;
        }

        await QueuedTask.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            mapView.Map.ClearSelection();
        }, TaskCreationOptions.None).ConfigureAwait(false);
    }

    private static async Task DeleteDatasetIfPresentAsync(string datasetPath)
    {
        var result = await Geoprocessing.ExecuteToolAsync(
            "management.Delete",
            Geoprocessing.MakeValueArray(datasetPath),
            flags: GPExecuteToolFlags.None).ConfigureAwait(false);
        _ = result;
    }

    private static async Task ClearRowsIfPresentAsync(string datasetPath)
    {
        var result = await Geoprocessing.ExecuteToolAsync(
            "management.DeleteRows",
            Geoprocessing.MakeValueArray(datasetPath),
            flags: GPExecuteToolFlags.None).ConfigureAwait(false);
        _ = result;
    }

    private static async Task ExecuteRequiredToolAsync(
        string toolName,
        IReadOnlyList<string> values,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await Geoprocessing.ExecuteToolAsync(
            toolName,
            values,
            flags: GPExecuteToolFlags.None).ConfigureAwait(false);
        if (result.IsFailed)
        {
            throw new InvalidOperationException($"{toolName} failed: {BuildGeoprocessingMessage(result)}");
        }
    }

    private static string BuildGeoprocessingMessage(IGPResult result)
    {
        var message = string.Join(
            " ",
            result.Messages
                .Select(item => item.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text)));
        return string.IsNullOrWhiteSpace(message)
            ? "ArcGIS geoprocessing did not return a detailed error."
            : message;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
