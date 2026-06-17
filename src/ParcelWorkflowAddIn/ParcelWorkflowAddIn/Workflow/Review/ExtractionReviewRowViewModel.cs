using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed class ExtractionReviewRowViewModel : INotifyPropertyChanged
{
    private readonly Action onRowChanged;
    private string pointIdentifier;
    private string easting;
    private string northing;
    private string length;
    private string extractionStatus;
    private string sourceEvidence;
    private bool unresolved;
    private string unresolvedReason;
    private string reviewNotes;

    public ExtractionReviewRowViewModel(ExtractionReviewRow model, Action onRowChanged)
    {
        Model = model;
        this.onRowChanged = onRowChanged;
        pointIdentifier = model.PointIdentifier;
        easting = model.Easting;
        northing = model.Northing;
        length = model.Length;
        extractionStatus = model.ExtractionStatus;
        sourceEvidence = model.SourceEvidence;
        unresolved = model.Unresolved;
        unresolvedReason = model.UnresolvedReason;
        reviewNotes = model.ReviewNotes;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ExtractionReviewRow Model { get; }

    public string RowId => Model.RowId;

    public string ParcelGroupId => string.IsNullOrWhiteSpace(Model.ParcelGroupId) ? "Parcel ?" : Model.ParcelGroupId;

    public string TraverseId => string.IsNullOrWhiteSpace(Model.TraverseId) ? "Traverse ?" : Model.TraverseId;

    public int? SequenceInGroup => Model.SequenceInGroup;

    public bool IsBoundaryBreak => Model.IsBoundaryBreak;

    public string GroupConfidence => string.IsNullOrWhiteSpace(Model.GroupConfidence) ? "unknown" : Model.GroupConfidence;

    public string PointIdentifier
    {
        get => pointIdentifier;
        set => UpdateValue(ref pointIdentifier, value, model => model.PointIdentifier = value);
    }

    public string Easting
    {
        get => easting;
        set => UpdateValue(ref easting, value, model => model.Easting = value);
    }

    public string Northing
    {
        get => northing;
        set => UpdateValue(ref northing, value, model => model.Northing = value);
    }

    public string Length
    {
        get => length;
        set => UpdateValue(ref length, value, model => model.Length = value);
    }

    public string ExtractionStatus
    {
        get => extractionStatus;
        set => UpdateValue(ref extractionStatus, value, model => model.ExtractionStatus = value);
    }

    public string SourceEvidence
    {
        get => sourceEvidence;
        set => UpdateValue(ref sourceEvidence, value, model => model.SourceEvidence = value);
    }

    public bool Unresolved
    {
        get => unresolved;
        set => UpdateValue(ref unresolved, value, model => model.Unresolved = value);
    }

    public string UnresolvedReason
    {
        get => unresolvedReason;
        set => UpdateValue(ref unresolvedReason, value, model => model.UnresolvedReason = value);
    }

    public string ReviewNotes
    {
        get => reviewNotes;
        set => UpdateValue(ref reviewNotes, value, model => model.ReviewNotes = value);
    }

    public string RowProvenance => Model.IsManual ? "Manual" : "Extracted";

    public bool IsManual => Model.IsManual;

    public bool IsEdited => Model.IsEdited;

    public string OriginalPointIdentifier => Model.OriginalValues.PointIdentifier;

    public string OriginalEasting => Model.OriginalValues.Easting;

    public string OriginalNorthing => Model.OriginalValues.Northing;

    public string OriginalStatus => Model.OriginalValues.ExtractionStatus;

    public string OriginalSourceEvidence => Model.OriginalValues.SourceEvidence;

    public bool HasMissingRequiredValues =>
        string.IsNullOrWhiteSpace(PointIdentifier)
        || string.IsNullOrWhiteSpace(Easting)
        || string.IsNullOrWhiteSpace(Northing);

    public void SyncBackToModel()
    {
        Model.PointIdentifier = pointIdentifier.Trim();
        Model.Easting = easting.Trim();
        Model.Northing = northing.Trim();
        Model.Length = length.Trim();
        Model.ExtractionStatus = extractionStatus.Trim();
        Model.SourceEvidence = sourceEvidence.Trim();
        Model.Unresolved = unresolved;
        Model.UnresolvedReason = unresolvedReason.Trim();
        Model.ReviewNotes = reviewNotes.Trim();
        Model.IsEdited = Model.IsManual
            || !string.Equals(Model.PointIdentifier, Model.OriginalValues.PointIdentifier, StringComparison.Ordinal)
            || !string.Equals(Model.Easting, Model.OriginalValues.Easting, StringComparison.Ordinal)
            || !string.Equals(Model.Northing, Model.OriginalValues.Northing, StringComparison.Ordinal)
            || !string.Equals(Model.Length, Model.OriginalValues.Length, StringComparison.Ordinal)
            || !string.Equals(Model.ExtractionStatus, Model.OriginalValues.ExtractionStatus, StringComparison.Ordinal)
            || !string.Equals(Model.SourceEvidence, Model.OriginalValues.SourceEvidence, StringComparison.Ordinal)
            || Model.Unresolved
            || !string.IsNullOrWhiteSpace(Model.ReviewNotes);
        OnPropertyChanged(nameof(IsEdited));
        OnPropertyChanged(nameof(HasMissingRequiredValues));
    }

    private void UpdateValue<T>(ref T field, T value, Action<ExtractionReviewRow> applyToModel, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        applyToModel(Model);
        SyncBackToModel();
        OnPropertyChanged(propertyName);
        onRowChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
