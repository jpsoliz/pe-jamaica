using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using System.IO;

namespace ParcelWorkflowAddIn.Intake;

public sealed class SourceInputProfileDetector
{
    private static readonly HashSet<string> ImageDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".tif",
        ".tiff",
        ".png",
        ".jpg",
        ".jpeg"
    };

    private static readonly HashSet<string> PointsComputationExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".txt",
        ".csv"
    };

    private readonly Func<DateTimeOffset> getUtcNow;

    public SourceInputProfileDetector()
        : this(() => DateTimeOffset.UtcNow)
    {
    }

    public SourceInputProfileDetector(Func<DateTimeOffset> getUtcNow)
    {
        this.getUtcNow = getUtcNow;
    }

    public DetectedSourceInputProfile Detect(IReadOnlyList<ManifestSourceFile> sourceFiles)
    {
        var classified = sourceFiles.Select(Classify).ToArray();
        var hasComputation = HasRoleWithExtension(classified, SourceRole.ComputationSheet, ImageDocumentExtensions);
        var hasPoints = HasRoleWithExtension(classified, SourceRole.CoordinateTextSource, PointsComputationExtensions);
        var hasDwg = HasRoleWithExtension(classified, SourceRole.DwgSource, new[] { ".dwg" });
        var hasPlan = HasRoleWithExtension(classified, SourceRole.PlanMapReference, ImageDocumentExtensions);
        var hasSurveyPlanPdf = HasRoleWithExtension(classified, SourceRole.SurveyPlanPdf, ImageDocumentExtensions);

        if (hasSurveyPlanPdf)
        {
            return Profile(SourceInputProfile.PxaSurveyPlan, SourceInputProfile.PxaSurveyPlanLabel, "matched", Array.Empty<string>(), Array.Empty<string>());
        }

        if (hasComputation && hasPlan && hasPoints)
        {
            return Profile(SourceInputProfile.ScenarioB, SourceInputProfile.ScenarioBLabel, "matched", Array.Empty<string>(), Array.Empty<string>());
        }

        if (hasComputation && hasPlan)
        {
            return Profile(SourceInputProfile.ScenarioA, SourceInputProfile.ScenarioALabel, "matched", Array.Empty<string>(), Array.Empty<string>());
        }

        if (sourceFiles.Count == 0)
        {
            return Profile(
                SourceInputProfile.IncompleteIntake,
                SourceInputProfile.IncompleteIntakeLabel,
                "incomplete",
                ComputeAttachmentSourceTypeCatalog.RequiredWorkflowRoles,
                new[] { "Missing: source files." });
        }

        if (!hasComputation && !hasPoints && !hasDwg && !hasPlan
            && classified.All(source => SourceRole.Matches(source.Role, SourceRole.UnsupportedSource)))
        {
            return Profile(
                SourceInputProfile.UnsupportedIntake,
                SourceInputProfile.UnsupportedIntakeLabel,
                "unsupported",
                Array.Empty<string>(),
                new[] { "Unsupported intake: no recognized source combination." });
        }

        var missingRoles = DetermineMissingRoles(hasComputation, hasPoints, hasPlan);
        var issues = missingRoles.Select(role => $"Missing: {ToDisplayRole(role)}.").ToArray();
        if (classified.Any(source => SourceRole.Matches(source.Role, SourceRole.AmbiguousDocument)))
        {
            issues = issues.Concat(new[] { "Ambiguous source role: review document filenames or roles." }).ToArray();
        }

        return Profile(SourceInputProfile.IncompleteIntake, SourceInputProfile.IncompleteIntakeLabel, "incomplete", missingRoles, issues);
    }

    private DetectedSourceInputProfile Profile(string code, string label, string status, IReadOnlyList<string> missingRoles, IReadOnlyList<string> issues)
    {
        return new DetectedSourceInputProfile(
            code,
            label,
            status,
            getUtcNow().UtcDateTime.ToString("O"),
            missingRoles,
            issues);
    }

    private static ClassifiedSourceFile Classify(ManifestSourceFile sourceFile)
    {
        var explicitRole = sourceFile.SourceRole;
        if (!string.IsNullOrWhiteSpace(explicitRole))
        {
            return new ClassifiedSourceFile(sourceFile, SourceRole.Normalize(explicitRole) ?? explicitRole);
        }

        var extension = sourceFile.FileType;
        var fileName = Path.GetFileNameWithoutExtension(sourceFile.CopiedPath);
        if (extension.Equals(".dwg", StringComparison.OrdinalIgnoreCase))
        {
            return new ClassifiedSourceFile(sourceFile, SourceRole.DwgSource);
        }

        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) || extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return new ClassifiedSourceFile(sourceFile, SourceRole.CoordinateTextSource);
        }

        if (ImageDocumentExtensions.Contains(extension) && ContainsAny(fileName, "computation", "comput", "comsheet", "comp sheet", "calculation", "coord", "coordinate", "point"))
        {
            return new ClassifiedSourceFile(sourceFile, SourceRole.ComputationSheet);
        }

        if (ImageDocumentExtensions.Contains(extension) && ContainsAny(fileName, "plan", "map", "geolan", "geo lan", "survey plan"))
        {
            return new ClassifiedSourceFile(sourceFile, SourceRole.PlanMapReference);
        }

        if (ImageDocumentExtensions.Contains(extension))
        {
            return new ClassifiedSourceFile(sourceFile, SourceRole.AmbiguousDocument);
        }

        return new ClassifiedSourceFile(sourceFile, SourceRole.UnsupportedSource);
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasRoleWithExtension(IEnumerable<ClassifiedSourceFile> sources, string role, IEnumerable<string> extensions)
    {
        var allowed = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        return sources.Any(source => SourceRole.Matches(source.Role, role)
            && allowed.Contains(source.SourceFile.FileType));
    }

    private static IReadOnlyList<string> DetermineMissingRoles(bool hasComputation, bool hasPoints, bool hasPlan)
    {
        if (hasPoints)
        {
            var missing = new List<string>();
            if (!hasComputation)
            {
                missing.Add(SourceRole.ComputationSheet);
            }

            if (!hasPlan)
            {
                missing.Add(SourceRole.PlanMapReference);
            }

            return missing;
        }

        var scenarioAMissing = new List<string>();
        if (!hasComputation)
        {
            scenarioAMissing.Add(SourceRole.ComputationSheet);
        }

        if (!hasPlan)
        {
            scenarioAMissing.Add(SourceRole.PlanMapReference);
        }

        return scenarioAMissing;
    }

    private static string ToDisplayRole(string role)
    {
        return role switch
        {
            SourceRole.ComputationSheet => "survey sheet",
            SourceRole.CoordinateTextSource => "structured points source",
            SourceRole.DwgSource => "AutoCAD source",
            SourceRole.PlanMapReference => "plan/map reference",
            _ => role
        };
    }

    private sealed record ClassifiedSourceFile(ManifestSourceFile SourceFile, string Role);
}
