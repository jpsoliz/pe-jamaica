using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn;

internal sealed class SegmentEditDialogViewModel : INotifyPropertyChanged
{
    private readonly ExtractionReviewSegmentViewModel targetSegment;
    private readonly bool isNewSegment;
    private string sequence;
    private string fromPoint;
    private string toPoint;
    private string bearingText;
    private string distanceText;
    private bool includeInBoundary;
    private string adjacentOwner;
    private string status;
    private string reviewNotes;
    private string validationSummary = string.Empty;

    internal SegmentEditDialogViewModel(ExtractionReviewSegmentViewModel targetSegment, bool isNewSegment = false)
    {
        this.targetSegment = targetSegment;
        this.isNewSegment = isNewSegment;
        sequence = targetSegment.Sequence?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        fromPoint = targetSegment.FromPoint;
        toPoint = targetSegment.ToPoint;
        bearingText = targetSegment.BearingText;
        distanceText = targetSegment.DistanceText;
        includeInBoundary = targetSegment.IncludeInBoundary;
        adjacentOwner = targetSegment.AdjacentOwner;
        status = targetSegment.Status;
        reviewNotes = targetSegment.ReviewNotes;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DialogTitle => isNewSegment ? "Add Boundary Segment" : "Edit Boundary Segment";

    public string DialogSummary => isNewSegment
        ? "Add a reviewed boundary segment from the source plan. Save Review persists it and reruns the boundary solver."
        : "Update the reviewed boundary segment values. These values drive the PXA boundary solver and parcel preview.";

    public string SegmentLabel => $"{FromPoint} -> {ToPoint}";

    public string SourceLabel => targetSegment.SourceLabel;

    public string Confidence => targetSegment.Confidence;

    public string Sequence
    {
        get => sequence;
        set => SetValue(ref sequence, value);
    }

    public string FromPoint
    {
        get => fromPoint;
        set
        {
            SetValue(ref fromPoint, value);
            OnPropertyChanged(nameof(SegmentLabel));
        }
    }

    public string ToPoint
    {
        get => toPoint;
        set
        {
            SetValue(ref toPoint, value);
            OnPropertyChanged(nameof(SegmentLabel));
        }
    }

    public string BearingText
    {
        get => bearingText;
        set => SetValue(ref bearingText, value);
    }

    public string DistanceText
    {
        get => distanceText;
        set => SetValue(ref distanceText, value);
    }

    public bool IncludeInBoundary
    {
        get => includeInBoundary;
        set
        {
            if (includeInBoundary == value)
            {
                return;
            }

            includeInBoundary = value;
            ClearValidationSummary();
            OnPropertyChanged();
        }
    }

    public string AdjacentOwner
    {
        get => adjacentOwner;
        set => SetValue(ref adjacentOwner, value);
    }

    public string Status
    {
        get => status;
        set => SetValue(ref status, value);
    }

    public string ReviewNotes
    {
        get => reviewNotes;
        set => SetValue(ref reviewNotes, value);
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

    public bool TryCommit()
    {
        var errors = Validate();
        if (errors.Count > 0)
        {
            ValidationSummary = string.Join(Environment.NewLine, errors);
            return false;
        }

        targetSegment.Sequence = int.Parse(sequence.Trim(), CultureInfo.InvariantCulture);
        targetSegment.FromPoint = fromPoint.Trim();
        targetSegment.ToPoint = toPoint.Trim();
        targetSegment.BearingText = bearingText.Trim();
        targetSegment.DistanceText = distanceText.Trim();
        targetSegment.IncludeInBoundary = includeInBoundary;
        targetSegment.AdjacentOwner = adjacentOwner.Trim();
        targetSegment.Status = status.Trim();
        targetSegment.ReviewNotes = reviewNotes.Trim();
        ValidationSummary = string.Empty;
        return true;
    }

    private IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (!int.TryParse(sequence.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequenceNumber)
            || sequenceNumber <= 0)
        {
            errors.Add("Sequence must be a positive whole number.");
        }

        if (!includeInBoundary)
        {
            return errors;
        }

        if (string.IsNullOrWhiteSpace(fromPoint))
        {
            errors.Add("From point is required for a boundary segment.");
        }

        if (string.IsNullOrWhiteSpace(toPoint))
        {
            errors.Add("To point is required for a boundary segment.");
        }

        if (string.IsNullOrWhiteSpace(bearingText))
        {
            errors.Add("Bearing is required for a boundary segment.");
        }

        if (string.IsNullOrWhiteSpace(distanceText))
        {
            errors.Add("Distance is required for a boundary segment.");
        }

        return errors;
    }

    private void SetValue(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
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
