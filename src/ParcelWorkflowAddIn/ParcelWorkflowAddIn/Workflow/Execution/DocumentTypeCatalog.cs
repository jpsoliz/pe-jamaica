using System.Text.RegularExpressions;

namespace ParcelWorkflowAddIn.Workflow.Execution;

public sealed record DocumentTypeCatalog(
    string CatalogPath,
    bool UsingSafeDefaults,
    string? LoadWarning,
    string SchemaVersion,
    string DefaultDocTypeId,
    IReadOnlyList<DocumentTypeDefinition> DocumentTypes)
{
    public DocumentTypeDefinition ResolveDefaultDefinition()
    {
        return DocumentTypes.FirstOrDefault(definition =>
                   string.Equals(definition.DocTypeId, DefaultDocTypeId, StringComparison.OrdinalIgnoreCase))
               ?? DocumentTypes.First(definition => !string.IsNullOrWhiteSpace(definition.DocTypeId));
    }

    public DocumentTypeMatchResult ResolveBestMatch(params DocumentTypeMatchCandidate[] candidates)
    {
        DocumentTypeScoredMatch? bestMatch = null;
        foreach (var candidate in candidates.Where(candidate => !string.IsNullOrWhiteSpace(candidate.Name)))
        {
            foreach (var definition in DocumentTypes)
            {
                var score = Score(definition, candidate);
                if (score.TotalScore <= 0)
                {
                    continue;
                }

                if (bestMatch is null
                    || score.TotalScore > bestMatch.TotalScore
                    || (score.TotalScore == bestMatch.TotalScore && definition.Priority > bestMatch.Definition.Priority))
                {
                    bestMatch = new DocumentTypeScoredMatch(definition, candidate, score.TotalScore, score.ScoreThreshold);
                }
            }
        }

        if (bestMatch is not null && bestMatch.TotalScore >= bestMatch.ScoreThreshold)
        {
            return new DocumentTypeMatchResult(
                bestMatch.Definition,
                MatchMode: $"{bestMatch.Candidate.Role}_weighted_match",
                MatchConfidence: ComputeConfidence(bestMatch.TotalScore, bestMatch.ScoreThreshold),
                LowConfidence: false,
                CandidateRole: bestMatch.Candidate.Role,
                CandidateName: bestMatch.Candidate.Name,
                MatchScore: bestMatch.TotalScore,
                ScoreThreshold: bestMatch.ScoreThreshold,
                CatalogPath: CatalogPath);
        }

        var defaultDefinition = ResolveDefaultDefinition();
        return new DocumentTypeMatchResult(
            defaultDefinition,
            MatchMode: bestMatch is null ? "default_doc_type_no_match" : "default_doc_type_low_confidence",
            MatchConfidence: bestMatch is null ? 0d : ComputeConfidence(bestMatch.TotalScore, bestMatch.ScoreThreshold),
            LowConfidence: bestMatch is not null,
            CandidateRole: bestMatch?.Candidate.Role ?? "unknown",
            CandidateName: bestMatch?.Candidate.Name ?? string.Empty,
            MatchScore: bestMatch?.TotalScore ?? 0,
            ScoreThreshold: bestMatch?.ScoreThreshold ?? defaultDefinition.Match.ScoreThreshold,
            CatalogPath: CatalogPath);
    }

    private static DocumentTypeScore Score(DocumentTypeDefinition definition, DocumentTypeMatchCandidate candidate)
    {
        if (definition.Match.SourceKinds.Count > 0
            && !definition.Match.SourceKinds.Any(kind => kind.Equals(candidate.Extension, StringComparison.OrdinalIgnoreCase)))
        {
            return DocumentTypeScore.Zero(definition.Match.ScoreThreshold);
        }

        var weights = definition.Classifier.Weights;
        var filenameContainsHits = CountContainsMatches(candidate.Name, definition.Match.FilenameContainsAny);
        var filenameRegexHits = CountRegexMatches(candidate.Name, definition.Match.FilenameRegexAny);
        var textContainsHits = string.IsNullOrWhiteSpace(candidate.TextContent)
            ? 0
            : CountContainsMatches(candidate.TextContent, definition.Match.TextContainsAny);
        var textRegexHits = string.IsNullOrWhiteSpace(candidate.TextContent)
            ? 0
            : CountRegexMatches(candidate.TextContent, definition.Match.TextRegexAny);

        var total = (filenameContainsHits * weights.FilenameContainsAny)
                    + (filenameRegexHits * weights.FilenameRegexAny)
                    + (textContainsHits * weights.TextContainsAny)
                    + (textRegexHits * weights.TextRegexAny);
        if (total <= 0
            && definition.Match.SourceKinds.Count > 0
            && definition.Match.FilenameContainsAny.Count == 0
            && definition.Match.FilenameRegexAny.Count == 0
            && definition.Match.TextContainsAny.Count == 0
            && definition.Match.TextRegexAny.Count == 0)
        {
            total = Math.Max(1, definition.Match.ScoreThreshold);
        }

        return new DocumentTypeScore(total, Math.Max(1, definition.Match.ScoreThreshold));
    }

    private static int CountContainsMatches(string text, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return 0;
        }

        return tokens.Count(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountRegexMatches(string text, IReadOnlyList<string> patterns)
    {
        var count = 0;
        foreach (var pattern in patterns)
        {
            try
            {
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    count++;
                }
            }
            catch (ArgumentException)
            {
                // Validation handles malformed regex patterns during load.
            }
        }

        return count;
    }

    private static double ComputeConfidence(int score, int threshold)
    {
        if (threshold <= 0)
        {
            return score > 0 ? 1d : 0d;
        }

        return Math.Round(Math.Min(1d, (double)score / threshold), 3, MidpointRounding.AwayFromZero);
    }

    private sealed record DocumentTypeScoredMatch(
        DocumentTypeDefinition Definition,
        DocumentTypeMatchCandidate Candidate,
        int TotalScore,
        int ScoreThreshold);

    private readonly record struct DocumentTypeScore(int TotalScore, int ScoreThreshold)
    {
        public static DocumentTypeScore Zero(int scoreThreshold) => new(0, Math.Max(1, scoreThreshold));
    }
}

public sealed record DocumentTypeDefinition(
    string DocTypeId,
    string Name,
    string Family,
    int Priority,
    DocumentTypeMatchDefinition Match,
    DocumentTypeClassifierDefinition Classifier,
    DocumentTypeExtractionDefinition Extraction,
    DocumentTypeSchemaDefinition Schema,
    DocumentTypeGeometryDefinition Geometry,
    DocumentTypeValidationDefinition Validation,
    DocumentTypeReviewDefinition Review,
    DocumentTypeOutputDefinition Output);

public sealed record DocumentTypeMatchDefinition(
    IReadOnlyList<string> SourceKinds,
    IReadOnlyList<string> FilenameContainsAny,
    IReadOnlyList<string> FilenameRegexAny,
    IReadOnlyList<string> TextContainsAny,
    IReadOnlyList<string> TextRegexAny,
    int ScoreThreshold);

public sealed record DocumentTypeClassifierDefinition(
    string Strategy,
    DocumentTypeClassifierWeights Weights);

public sealed record DocumentTypeClassifierWeights(
    int FilenameContainsAny,
    int FilenameRegexAny,
    int TextContainsAny,
    int TextRegexAny);

public sealed record DocumentTypeExtractionDefinition(
    string ExtractorId,
    string ParserMode,
    bool AiAssisted,
    string AiProfile,
    IReadOnlyList<string> FallbackExtractors,
    IReadOnlyList<string> ExpectedOutputs);

public sealed record DocumentTypeSchemaDefinition(
    IReadOnlyList<string> MetadataFields,
    IReadOnlyList<string> ParcelFields,
    IReadOnlyList<string> RowFields);

public sealed record DocumentTypeGeometryDefinition(
    string GeometryMode,
    string PointSource,
    string LineBuilder,
    string PolygonBuilder,
    bool SupportsMultiParcel,
    bool SupportsBoundaryBreaks,
    bool RequiresGrouping,
    string ClosingRule);

public sealed record DocumentTypeValidationDefinition(
    string ValidationProfile,
    IReadOnlyList<string> RequiredMetadataFields,
    IReadOnlyList<string> RequiredRowFields,
    int MinimumExpectedRows,
    int MinimumExpectedParcels,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings);

public sealed record DocumentTypeReviewDefinition(
    string ReviewMode,
    string SourceViewerMode,
    bool AllowManualPointAdd,
    bool AllowManualGroupSplit,
    bool AllowManualGroupMerge,
    bool ApprovalRequiresZeroBlockers);

public sealed record DocumentTypeOutputDefinition(
    IReadOnlyList<string> OutputProfiles,
    string DefaultOutputProfile);

public sealed record DocumentTypeMatchCandidate(
    string Role,
    string Name,
    string Extension,
    string? TextContent = null);

public sealed record DocumentTypeMatchResult(
    DocumentTypeDefinition Definition,
    string MatchMode,
    double MatchConfidence,
    bool LowConfidence,
    string CandidateRole,
    string CandidateName,
    int MatchScore,
    int ScoreThreshold,
    string CatalogPath);
