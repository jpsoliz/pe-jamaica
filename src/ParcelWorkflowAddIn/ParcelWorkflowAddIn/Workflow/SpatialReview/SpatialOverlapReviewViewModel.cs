using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;

namespace ParcelWorkflowAddIn.Workflow.SpatialReview;

internal sealed class SpatialOverlapReviewViewModel : INotifyPropertyChanged
{
    private SpatialOverlapReviewDocument? document;
    private string contextLabel = "Overlap Review";
    private SpatialOverlapReviewRecordItem? selectedRecord;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SpatialOverlapReviewLayerResult> LayerResults { get; } = new();

    public ObservableCollection<SpatialOverlapReviewRecordItem> Records { get; } = new();

    public ObservableCollection<SpatialOverlapReviewSnapshotItem> SnapshotRefs { get; } = new();

    public ObservableCollection<SpatialOverlapReviewOwnerMatchItem> OwnerMatches { get; } = new();

    public ObservableCollection<string> Warnings { get; } = new();

    public ObservableCollection<string> Errors { get; } = new();

    public string WindowTitle => $"{contextLabel} - Overlap Review";

    public string HeaderTitle => $"{contextLabel} overlap evidence";

    public string SummaryText => document?.Summary.Message ?? "No overlap review artifact is loaded.";

    public string ScopeLabel => document?.Scope switch
    {
        SpatialOverlapReviewScopes.Compare => "Compare",
        SpatialOverlapReviewScopes.Compute => "Compute",
        _ => "Overlap Review"
    };

    public string ReviewLayerSummary => string.IsNullOrWhiteSpace(document?.ReviewLayerName)
        ? "Review geometry layer is not recorded."
        : $"Review layer: {document!.ReviewLayerName}";

    public string ReviewAreaText => document is null
        ? string.Empty
        : $"Review area: {document.ReviewAreaSquareMeters.ToString("N2", CultureInfo.InvariantCulture)} m²";

    public bool HasRecords => Records.Count > 0;

    public bool HasNoOverlapResult => document is not null && !HasRecords;

    public string NoOverlapText => "No overlaps found across configured layers.";

    public bool HasWarnings => Warnings.Count > 0;

    public bool HasErrors => Errors.Count > 0;

    public bool HasSnapshots => SnapshotRefs.Count > 0;

    public bool HasOwnerMatches => OwnerMatches.Count > 0;

    public string SnapshotStatusText => SelectedRecord is null
        ? "Select an overlap row to review linked evidence references."
        : HasSnapshots
            ? $"{SnapshotRefs.Count} evidence reference(s) are linked to {SelectedRecord.DisplayOverlapId}."
            : $"No snapshot references are linked to {SelectedRecord.DisplayOverlapId}.";

    public string OwnerMatchStatusText => SelectedRecord is null
        ? "Select an overlap row to review owner/property enrichment."
        : HasOwnerMatches
            ? $"{OwnerMatches.Count} owner/property match(es) are linked to {SelectedRecord.DisplayOverlapId}."
            : SelectedRecord.EnrichmentMessage;

    public SpatialOverlapReviewRecordItem? SelectedRecord
    {
        get => selectedRecord;
        set
        {
            if (ReferenceEquals(selectedRecord, value))
            {
                return;
            }

            selectedRecord = value;
            RefreshSnapshots();
            NotifyPropertyChanged(nameof(SelectedRecord));
            NotifyPropertyChanged(nameof(SnapshotStatusText));
            NotifyPropertyChanged(nameof(OwnerMatchStatusText));
        }
    }

    public void Load(SpatialOverlapReviewDocument reviewDocument, string reviewContextLabel)
    {
        document = reviewDocument;
        contextLabel = string.IsNullOrWhiteSpace(reviewContextLabel) ? "Overlap Review" : reviewContextLabel.Trim();

        LayerResults.Clear();
        foreach (var layer in reviewDocument.Layers)
        {
            LayerResults.Add(layer);
        }

        Records.Clear();
        foreach (var record in reviewDocument.Records)
        {
            Records.Add(new SpatialOverlapReviewRecordItem(record));
        }

        Warnings.Clear();
        foreach (var warning in reviewDocument.Warnings)
        {
            Warnings.Add(warning);
        }

        Errors.Clear();
        foreach (var error in reviewDocument.Errors)
        {
            Errors.Add(error);
        }

        SelectedRecord = Records.FirstOrDefault();
        if (SelectedRecord is null)
        {
            RefreshSnapshots();
        }

        NotifyPropertyChanged(nameof(WindowTitle));
        NotifyPropertyChanged(nameof(HeaderTitle));
        NotifyPropertyChanged(nameof(SummaryText));
        NotifyPropertyChanged(nameof(ScopeLabel));
        NotifyPropertyChanged(nameof(ReviewLayerSummary));
        NotifyPropertyChanged(nameof(ReviewAreaText));
        NotifyPropertyChanged(nameof(HasRecords));
        NotifyPropertyChanged(nameof(HasNoOverlapResult));
        NotifyPropertyChanged(nameof(HasWarnings));
        NotifyPropertyChanged(nameof(HasErrors));
        NotifyPropertyChanged(nameof(HasSnapshots));
        NotifyPropertyChanged(nameof(HasOwnerMatches));
        NotifyPropertyChanged(nameof(SnapshotStatusText));
        NotifyPropertyChanged(nameof(OwnerMatchStatusText));
    }

    private void RefreshSnapshots()
    {
        SnapshotRefs.Clear();
        OwnerMatches.Clear();
        var snapshots = document?.Snapshots ?? Array.Empty<SpatialOverlapReviewSnapshotRef>();
        if (SelectedRecord is null)
        {
            NotifyPropertyChanged(nameof(HasSnapshots));
            NotifyPropertyChanged(nameof(HasOwnerMatches));
            NotifyPropertyChanged(nameof(SnapshotStatusText));
            NotifyPropertyChanged(nameof(OwnerMatchStatusText));
            return;
        }

        var selectedOverlapGroupId = SelectedRecord.Record.OverlapGroupId;
        var selectedOverlapId = SelectedRecord.Record.OverlapId;
        foreach (var snapshot in snapshots.Where(snapshot =>
                     (!string.IsNullOrWhiteSpace(selectedOverlapId)
                      && string.Equals(snapshot.OverlapId, selectedOverlapId, StringComparison.OrdinalIgnoreCase))
                     || (!string.IsNullOrWhiteSpace(selectedOverlapGroupId)
                         && string.Equals(snapshot.OverlapGroupId, selectedOverlapGroupId, StringComparison.OrdinalIgnoreCase))))
        {
            SnapshotRefs.Add(new SpatialOverlapReviewSnapshotItem(snapshot));
        }

        var ownerMatches = SelectedRecord.Record.OwnerEnrichment?.Matches ?? Array.Empty<SpatialOverlapReviewOwnerMatch>();
        foreach (var ownerMatch in ownerMatches)
        {
            OwnerMatches.Add(new SpatialOverlapReviewOwnerMatchItem(ownerMatch));
        }

        NotifyPropertyChanged(nameof(HasSnapshots));
        NotifyPropertyChanged(nameof(HasOwnerMatches));
        NotifyPropertyChanged(nameof(SnapshotStatusText));
        NotifyPropertyChanged(nameof(OwnerMatchStatusText));
    }

    private void NotifyPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed class SpatialOverlapReviewRecordItem
{
    public SpatialOverlapReviewRecordItem(SpatialOverlapReviewRecord record)
    {
        Record = record;
    }

    public SpatialOverlapReviewRecord Record { get; }

    public string DisplayOverlapId => string.IsNullOrWhiteSpace(Record.OverlapId)
        ? "(generated)"
        : Record.OverlapId!;

    public string DisplayOverlapGroupId => string.IsNullOrWhiteSpace(Record.OverlapGroupId)
        ? "(none)"
        : Record.OverlapGroupId!;

    public string LayerDisplay => $"{Record.LayerName} ({Record.LayerRole})";

    public string IdentifierDisplay => string.Join(
        " | ",
        new[]
        {
            BuildIdentifier("PID", Record.Pid),
            BuildIdentifier("Vol/Folio", CombineVolumeFolio(Record.Volume, Record.Folio)),
            BuildIdentifier("LandVal", Record.LandValuationNumber),
            BuildIdentifier("PE", Record.PeNumber),
            BuildIdentifier("DP", Record.DpNumber),
            BuildIdentifier("R", Record.RNumber)
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public string EnrichmentStatus => string.IsNullOrWhiteSpace(Record.EnrichmentStatus)
        ? Record.OwnerEnrichment?.Status ?? "not_requested"
        : Record.EnrichmentStatus!;

    public string EnrichmentSummary
    {
        get
        {
            var matches = Record.OwnerEnrichment?.Matches ?? Array.Empty<SpatialOverlapReviewOwnerMatch>();
            if (matches.Count > 0)
            {
                var displayNames = matches
                    .Select(match => match.DisplayName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(2)
                    .ToArray();
                if (displayNames.Length > 0)
                {
                    return matches.Count > 2
                        ? $"{string.Join("; ", displayNames)} (+{matches.Count - 2} more)"
                        : string.Join("; ", displayNames);
                }
            }

            return Record.OwnerEnrichment?.Message ?? "Owner/property enrichment not requested yet.";
        }
    }

    public string EnrichmentMessage => Record.OwnerEnrichment?.Message ?? "Owner/property enrichment not requested yet.";

    public string EnrichmentDiagnostic => string.IsNullOrWhiteSpace(Record.OwnerEnrichment?.Diagnostic)
        ? "No diagnostic details were recorded for this overlap row."
        : Record.OwnerEnrichment!.Diagnostic!;

    public string OverlapAreaText => Record.OverlapAreaSquareMeters.ToString("N2", CultureInfo.InvariantCulture);

    public string OverlapPercentText => Record.OverlapPercentage.ToString("N2", CultureInfo.InvariantCulture);

    private static string? BuildIdentifier(string label, string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : $"{label}: {value}";
    }

    private static string? CombineVolumeFolio(string? volume, string? folio)
    {
        if (string.IsNullOrWhiteSpace(volume) && string.IsNullOrWhiteSpace(folio))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(volume))
        {
            return folio;
        }

        return string.IsNullOrWhiteSpace(folio) ? volume : $"{volume}/{folio}";
    }
}

internal sealed class SpatialOverlapReviewSnapshotItem
{
    public SpatialOverlapReviewSnapshotItem(SpatialOverlapReviewSnapshotRef snapshot)
    {
        Snapshot = snapshot;
    }

    public SpatialOverlapReviewSnapshotRef Snapshot { get; }

    public string Caption => Snapshot.Caption;

    public string RelativePath => string.IsNullOrWhiteSpace(Snapshot.RelativePath) ? "(not captured)" : Snapshot.RelativePath!;

    public string Status => string.IsNullOrWhiteSpace(Snapshot.Status) ? "not_captured" : Snapshot.Status;

    public string FileName => string.IsNullOrWhiteSpace(Snapshot.RelativePath)
        ? "(none)"
        : Path.GetFileName(Snapshot.RelativePath);
}

internal sealed class SpatialOverlapReviewOwnerMatchItem
{
    public SpatialOverlapReviewOwnerMatchItem(SpatialOverlapReviewOwnerMatch match)
    {
        Match = match;
    }

    public SpatialOverlapReviewOwnerMatch Match { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(Match.DisplayName) ? "(unnamed)" : Match.DisplayName!;

    public string IdentifierSummary
    {
        get
        {
            var parts = new[]
            {
                string.IsNullOrWhiteSpace(Match.ParcelId) ? null : $"PID: {Match.ParcelId}",
                string.IsNullOrWhiteSpace(Match.Volume) && string.IsNullOrWhiteSpace(Match.Folio)
                    ? null
                    : $"Vol/Folio: {Match.Volume ?? string.Empty}/{Match.Folio ?? string.Empty}",
                string.IsNullOrWhiteSpace(Match.LandValuationNumber) ? null : $"LandVal: {Match.LandValuationNumber}"
            }.Where(part => !string.IsNullOrWhiteSpace(part));
            return string.Join(" | ", parts);
        }
    }

    public string DetailSummary
    {
        get
        {
            var parts = new[]
            {
                string.IsNullOrWhiteSpace(Match.PartyRole) ? null : $"Role: {Match.PartyRole}",
                string.IsNullOrWhiteSpace(Match.PropertyType) ? null : $"Type: {Match.PropertyType}",
                string.IsNullOrWhiteSpace(Match.Tenure) ? null : $"Tenure: {Match.Tenure}",
                string.IsNullOrWhiteSpace(Match.Parish) ? null : $"Parish: {Match.Parish}",
                string.IsNullOrWhiteSpace(Match.RegisteredAt) ? null : $"Registered: {Match.RegisteredAt}"
            }.Where(part => !string.IsNullOrWhiteSpace(part));
            return string.Join(" | ", parts);
        }
    }

    public string Status => string.IsNullOrWhiteSpace(Match.Status) ? "(none)" : Match.Status!;

    public string QueryKey => string.IsNullOrWhiteSpace(Match.QueryKey) ? "(none)" : Match.QueryKey!;
}
