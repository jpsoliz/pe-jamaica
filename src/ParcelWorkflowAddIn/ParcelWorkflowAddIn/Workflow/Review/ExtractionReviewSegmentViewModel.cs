using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed class ExtractionReviewSegmentViewModel : INotifyPropertyChanged
{
    private readonly Action onSegmentChanged;
    private int? sequence;
    private string fromPoint;
    private string toPoint;
    private string bearingText;
    private string distanceText;
    private bool includeInBoundary;
    private string status;
    private string reviewNotes;
    private string adjacentOwner;

    public ExtractionReviewSegmentViewModel(ExtractionReviewSegment model, Action onSegmentChanged)
    {
        Model = model;
        this.onSegmentChanged = onSegmentChanged;
        sequence = model.ReviewSequence ?? model.Sequence;
        fromPoint = model.EffectiveFromPoint;
        toPoint = model.EffectiveToPoint;
        bearingText = model.EffectiveBearingText;
        distanceText = string.IsNullOrWhiteSpace(model.EffectiveDistanceText)
            ? model.EffectiveLengthText
            : model.EffectiveDistanceText;
        includeInBoundary = model.EffectiveIncludeInBoundary;
        status = string.IsNullOrWhiteSpace(model.ReviewStatus) ? model.Status : model.ReviewStatus;
        reviewNotes = model.ReviewNotes;
        adjacentOwner = model.AdjacentOwner;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ExtractionReviewSegment Model { get; }

    public string SegmentId => string.IsNullOrWhiteSpace(Model.SegmentId)
        ? $"segment-{Sequence?.ToString() ?? "?"}"
        : Model.SegmentId;

    public int? Sequence
    {
        get => sequence;
        set => UpdateValue(ref sequence, value, model => model.ReviewSequence = value);
    }

    public string FromPoint
    {
        get => fromPoint;
        set => UpdateValue(ref fromPoint, value, model => model.ReviewFromPoint = value?.Trim() ?? string.Empty);
    }

    public string ToPoint
    {
        get => toPoint;
        set => UpdateValue(ref toPoint, value, model => model.ReviewToPoint = value?.Trim() ?? string.Empty);
    }

    public string BearingText
    {
        get => bearingText;
        set => UpdateValue(ref bearingText, value, model => model.ReviewBearingText = value?.Trim() ?? string.Empty);
    }

    public string DistanceText
    {
        get => distanceText;
        set
        {
            UpdateValue(ref distanceText, value, model =>
            {
                model.ReviewDistanceText = value?.Trim() ?? string.Empty;
                model.ReviewLengthText = value?.Trim() ?? string.Empty;
            });
        }
    }

    public bool IncludeInBoundary
    {
        get => includeInBoundary;
        set => UpdateValue(ref includeInBoundary, value, model => model.ReviewIncludeInBoundary = value);
    }

    public string Confidence => string.IsNullOrWhiteSpace(Model.Confidence) ? "unknown" : Model.Confidence;

    public string Status
    {
        get => status;
        set => UpdateValue(ref status, value, model => model.ReviewStatus = value?.Trim() ?? string.Empty);
    }

    public string ReviewNotes
    {
        get => reviewNotes;
        set => UpdateValue(ref reviewNotes, value, model => model.ReviewNotes = value?.Trim() ?? string.Empty);
    }

    public string AdjacentOwner
    {
        get => adjacentOwner;
        set => UpdateValue(ref adjacentOwner, value, model => model.AdjacentOwner = value?.Trim() ?? string.Empty);
    }

    public string SourceLabel
    {
        get
        {
            var parts = new[]
            {
                string.IsNullOrWhiteSpace(Model.SourcePage) ? null : $"page {Model.SourcePage}",
                string.IsNullOrWhiteSpace(Model.SourceZone) ? null : Model.SourceZone,
                string.IsNullOrWhiteSpace(Model.SourceEvidence) ? null : Model.SourceEvidence
            }.Where(part => !string.IsNullOrWhiteSpace(part));
            return string.Join(" - ", parts);
        }
    }

    public bool IsEdited => Model.IsEdited;

    public void SyncBackToModel()
    {
        Model.ReviewSequence = sequence;
        Model.ReviewFromPoint = fromPoint.Trim();
        Model.ReviewToPoint = toPoint.Trim();
        Model.ReviewBearingText = bearingText.Trim();
        Model.ReviewDistanceText = distanceText.Trim();
        Model.ReviewLengthText = distanceText.Trim();
        Model.ReviewIncludeInBoundary = includeInBoundary;
        Model.ReviewStatus = status.Trim();
        Model.ReviewNotes = reviewNotes.Trim();
        Model.AdjacentOwner = adjacentOwner.Trim();
        Model.IsEdited = IsChanged();
        OnPropertyChanged(nameof(IsEdited));
        OnPropertyChanged(nameof(SegmentId));
    }

    private bool IsChanged()
    {
        return Model.ReviewSequence != Model.OriginalValues.Sequence
            || !string.Equals(Model.ReviewFromPoint, Model.OriginalValues.FromPoint, StringComparison.Ordinal)
            || !string.Equals(Model.ReviewToPoint, Model.OriginalValues.ToPoint, StringComparison.Ordinal)
            || !string.Equals(Model.ReviewBearingText, Model.OriginalValues.BearingText, StringComparison.Ordinal)
            || !string.Equals(Model.ReviewDistanceText, Model.OriginalValues.DistanceText, StringComparison.Ordinal)
            || !string.Equals(Model.ReviewLengthText, Model.OriginalValues.LengthText, StringComparison.Ordinal)
            || Model.ReviewIncludeInBoundary != Model.OriginalValues.IncludeInBoundary
            || !string.IsNullOrWhiteSpace(Model.ReviewStatus)
            || !string.IsNullOrWhiteSpace(Model.ReviewNotes)
            || !string.IsNullOrWhiteSpace(Model.AdjacentOwner);
    }

    private void UpdateValue<T>(ref T field, T value, Action<ExtractionReviewSegment> applyToModel, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        applyToModel(Model);
        SyncBackToModel();
        OnPropertyChanged(propertyName);
        onSegmentChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
