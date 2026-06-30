using System.ComponentModel;
using System.Runtime.CompilerServices;
using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn;

internal sealed class PointEditDialogViewModel : INotifyPropertyChanged
{
    private readonly IReadOnlyList<ExtractionReviewRowViewModel> existingRows;
    private readonly PointEditDraft workingDraft;
    private string validationSummary = string.Empty;

    internal PointEditDialogViewModel(PointEditDraft draft, IReadOnlyList<ExtractionReviewRowViewModel> existingRows)
    {
        workingDraft = draft;
        this.existingRows = existingRows;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DialogTitle => workingDraft.IsNewPoint ? "Add Point" : "Edit Point";

    public string DialogSummary => workingDraft.IsNewPoint
        ? "Enter the new parcel point details and save when the values are complete."
        : "Update the selected parcel point values and save when the record is ready.";

    public string ParcelLabel => string.IsNullOrWhiteSpace(workingDraft.ParcelName) ? workingDraft.ParcelGroupId : workingDraft.ParcelName;

    public string ParcelGroupLabel => workingDraft.ParcelGroupId;

    public int SequenceInGroup => workingDraft.SequenceInGroup;

    public bool IsNewPoint => workingDraft.IsNewPoint;

    public string PointIdentifier
    {
        get => workingDraft.PointIdentifier;
        set => SetValue(value, v => workingDraft.PointIdentifier = v);
    }

    public string Easting
    {
        get => workingDraft.Easting;
        set => SetValue(value, v => workingDraft.Easting = v);
    }

    public string Northing
    {
        get => workingDraft.Northing;
        set => SetValue(value, v => workingDraft.Northing = v);
    }

    public string Length
    {
        get => workingDraft.Length;
        set => SetValue(value, v => workingDraft.Length = v);
    }

    public string ExtractionStatus
    {
        get => workingDraft.ExtractionStatus;
        set => SetValue(value, v => workingDraft.ExtractionStatus = v);
    }

    public string SourceEvidence
    {
        get => workingDraft.SourceEvidence;
        set => SetValue(value, v => workingDraft.SourceEvidence = v);
    }

    public bool Unresolved
    {
        get => workingDraft.Unresolved;
        set
        {
            if (workingDraft.Unresolved == value)
            {
                return;
            }

            workingDraft.Unresolved = value;
            if (!value)
            {
                workingDraft.UnresolvedReason = string.Empty;
                OnPropertyChanged(nameof(UnresolvedReason));
            }

            ClearValidationSummary();
            OnPropertyChanged();
        }
    }

    public string UnresolvedReason
    {
        get => workingDraft.UnresolvedReason;
        set => SetValue(value, v => workingDraft.UnresolvedReason = v);
    }

    public string ReviewNotes
    {
        get => workingDraft.ReviewNotes;
        set => SetValue(value, v => workingDraft.ReviewNotes = v);
    }

    public string ValidationSummary
    {
        get => validationSummary;
        private set
        {
            if (string.Equals(validationSummary, value, StringComparison.Ordinal))
            {
                return;
            }

            validationSummary = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasValidationSummary));
        }
    }

    public bool HasValidationSummary => !string.IsNullOrWhiteSpace(ValidationSummary);

    public PointEditDraft CommittedDraft => workingDraft;

    public bool TryCommit()
    {
        var errors = workingDraft.Validate(existingRows);
        if (errors.Count == 0)
        {
            ValidationSummary = string.Empty;
            return true;
        }

        ValidationSummary = string.Join(Environment.NewLine, errors);
        return false;
    }

    private void SetValue(string value, Action<string> apply, [CallerMemberName] string? propertyName = null)
    {
        var current = propertyName switch
        {
            nameof(PointIdentifier) => workingDraft.PointIdentifier,
            nameof(Easting) => workingDraft.Easting,
            nameof(Northing) => workingDraft.Northing,
            nameof(Length) => workingDraft.Length,
            nameof(ExtractionStatus) => workingDraft.ExtractionStatus,
            nameof(SourceEvidence) => workingDraft.SourceEvidence,
            nameof(UnresolvedReason) => workingDraft.UnresolvedReason,
            nameof(ReviewNotes) => workingDraft.ReviewNotes,
            _ => string.Empty
        };

        if (string.Equals(current, value, StringComparison.Ordinal))
        {
            return;
        }

        apply(value);
        ClearValidationSummary();
        OnPropertyChanged(propertyName);
    }

    private void ClearValidationSummary()
    {
        if (HasValidationSummary)
        {
            ValidationSummary = string.Empty;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
