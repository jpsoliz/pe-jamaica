using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed class ExtractionReviewMetadataFieldViewModel : INotifyPropertyChanged
{
    private readonly Action onMetadataChanged;
    private string value;
    private string rawText;
    private string reviewStatus;
    private string reviewNotes;
    private bool? present;

    public ExtractionReviewMetadataFieldViewModel(ExtractionReviewMetadataField model, Action onMetadataChanged)
    {
        Model = model;
        this.onMetadataChanged = onMetadataChanged;
        value = model.Value;
        rawText = model.RawText;
        reviewStatus = model.ReviewStatus;
        reviewNotes = model.ReviewNotes;
        present = model.Present;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ExtractionReviewMetadataField Model { get; }

    public string Key => Model.Key;

    public string Label => string.IsNullOrWhiteSpace(Model.Label) ? Model.Key : Model.Label;

    public string Confidence => string.IsNullOrWhiteSpace(Model.Confidence) ? "unknown" : Model.Confidence;

    public string SourceLabel
    {
        get
        {
            var parts = new[]
            {
                string.IsNullOrWhiteSpace(Model.SourcePage) ? null : $"page {Model.SourcePage}",
                string.IsNullOrWhiteSpace(Model.SourceZone) ? null : Model.SourceZone
            }.Where(part => !string.IsNullOrWhiteSpace(part));
            return string.Join(" - ", parts);
        }
    }

    public string Value
    {
        get => value;
        set => UpdateValue(ref this.value, value, model => model.Value = value?.Trim() ?? string.Empty);
    }

    public string RawText
    {
        get => rawText;
        set => UpdateValue(ref rawText, value, model => model.RawText = value?.Trim() ?? string.Empty);
    }

    public bool? Present
    {
        get => present;
        set => UpdateValue(ref present, value, model => model.Present = value);
    }

    public string ReviewStatus
    {
        get => reviewStatus;
        set => UpdateValue(ref reviewStatus, value, model => model.ReviewStatus = value?.Trim() ?? string.Empty);
    }

    public string ReviewNotes
    {
        get => reviewNotes;
        set => UpdateValue(ref reviewNotes, value, model => model.ReviewNotes = value?.Trim() ?? string.Empty);
    }

    public bool IsEdited => Model.IsEdited;

    public void SyncBackToModel()
    {
        Model.Value = value.Trim();
        Model.RawText = rawText.Trim();
        Model.Present = present;
        Model.ReviewStatus = reviewStatus.Trim();
        Model.ReviewNotes = reviewNotes.Trim();
        OnPropertyChanged(nameof(IsEdited));
    }

    private void UpdateValue<T>(ref T field, T next, Action<ExtractionReviewMetadataField> applyToModel, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, next))
        {
            return;
        }

        field = next;
        applyToModel(Model);
        SyncBackToModel();
        OnPropertyChanged(propertyName);
        onMetadataChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ExtractionReviewAdjacentOwnerViewModel : INotifyPropertyChanged
{
    private readonly Action onOwnerChanged;
    private string name;
    private string role;
    private string relatedSegmentFrom;
    private string relatedSegmentTo;
    private string volume;
    private string folio;
    private string reviewStatus;
    private string reviewNotes;

    public ExtractionReviewAdjacentOwnerViewModel(ExtractionReviewAdjacentOwner model, Action onOwnerChanged)
    {
        Model = model;
        this.onOwnerChanged = onOwnerChanged;
        name = model.Name;
        role = model.Role;
        relatedSegmentFrom = model.RelatedSegmentFrom;
        relatedSegmentTo = model.RelatedSegmentTo;
        volume = model.Volume;
        folio = model.Folio;
        reviewStatus = model.ReviewStatus;
        reviewNotes = model.ReviewNotes;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ExtractionReviewAdjacentOwner Model { get; }

    public string Name
    {
        get => name;
        set => UpdateValue(ref name, value, model => model.Name = value?.Trim() ?? string.Empty);
    }

    public string Role
    {
        get => role;
        set => UpdateValue(ref role, value, model => model.Role = value?.Trim() ?? string.Empty);
    }

    public string RelatedSegmentFrom
    {
        get => relatedSegmentFrom;
        set => UpdateValue(ref relatedSegmentFrom, value, model => model.RelatedSegmentFrom = value?.Trim() ?? string.Empty);
    }

    public string RelatedSegmentTo
    {
        get => relatedSegmentTo;
        set => UpdateValue(ref relatedSegmentTo, value, model => model.RelatedSegmentTo = value?.Trim() ?? string.Empty);
    }

    public string Volume
    {
        get => volume;
        set => UpdateValue(ref volume, value, model => model.Volume = value?.Trim() ?? string.Empty);
    }

    public string Folio
    {
        get => folio;
        set => UpdateValue(ref folio, value, model => model.Folio = value?.Trim() ?? string.Empty);
    }

    public string ReviewStatus
    {
        get => reviewStatus;
        set => UpdateValue(ref reviewStatus, value, model => model.ReviewStatus = value?.Trim() ?? string.Empty);
    }

    public string ReviewNotes
    {
        get => reviewNotes;
        set => UpdateValue(ref reviewNotes, value, model => model.ReviewNotes = value?.Trim() ?? string.Empty);
    }

    public string SegmentLabel => string.IsNullOrWhiteSpace(RelatedSegmentFrom) && string.IsNullOrWhiteSpace(RelatedSegmentTo)
        ? string.Empty
        : $"{RelatedSegmentFrom}->{RelatedSegmentTo}";

    public void SyncBackToModel()
    {
        Model.Name = name.Trim();
        Model.Role = role.Trim();
        Model.RelatedSegmentFrom = relatedSegmentFrom.Trim();
        Model.RelatedSegmentTo = relatedSegmentTo.Trim();
        Model.Volume = volume.Trim();
        Model.Folio = folio.Trim();
        Model.ReviewStatus = reviewStatus.Trim();
        Model.ReviewNotes = reviewNotes.Trim();
        OnPropertyChanged(nameof(SegmentLabel));
    }

    private void UpdateValue<T>(ref T field, T next, Action<ExtractionReviewAdjacentOwner> applyToModel, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, next))
        {
            return;
        }

        field = next;
        applyToModel(Model);
        SyncBackToModel();
        OnPropertyChanged(propertyName);
        onOwnerChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ExtractionReviewNamedPartyViewModel : INotifyPropertyChanged
{
    private readonly Action onPartyChanged;
    private string name;
    private string role;
    private string sourceGroup;
    private string reviewStatus;
    private string reviewNotes;

    public ExtractionReviewNamedPartyViewModel(ExtractionReviewNamedParty model, string sourceGroup, Action onPartyChanged)
    {
        Model = model;
        this.sourceGroup = sourceGroup;
        this.onPartyChanged = onPartyChanged;
        name = model.Name;
        role = model.Role;
        reviewStatus = model.ReviewStatus;
        reviewNotes = model.ReviewNotes;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ExtractionReviewNamedParty Model { get; }

    public string SourceGroup
    {
        get => sourceGroup;
        set
        {
            if (sourceGroup == value)
            {
                return;
            }

            sourceGroup = value?.Trim() ?? string.Empty;
            OnPropertyChanged();
            onPartyChanged();
        }
    }

    public string Name
    {
        get => name;
        set => UpdateValue(ref name, value, model => model.Name = value?.Trim() ?? string.Empty);
    }

    public string Role
    {
        get => role;
        set => UpdateValue(ref role, value, model => model.Role = value?.Trim() ?? string.Empty);
    }

    public string SourceLabel
    {
        get
        {
            var parts = new[]
            {
                string.IsNullOrWhiteSpace(Model.SourcePage) ? null : $"page {Model.SourcePage}",
                string.IsNullOrWhiteSpace(Model.SourceZone) ? null : Model.SourceZone
            }.Where(part => !string.IsNullOrWhiteSpace(part));
            return string.Join(" - ", parts);
        }
    }

    public string ReviewStatus
    {
        get => reviewStatus;
        set => UpdateValue(ref reviewStatus, value, model => model.ReviewStatus = value?.Trim() ?? string.Empty);
    }

    public string ReviewNotes
    {
        get => reviewNotes;
        set => UpdateValue(ref reviewNotes, value, model => model.ReviewNotes = value?.Trim() ?? string.Empty);
    }

    public void SyncBackToModel()
    {
        Model.Name = name.Trim();
        Model.Role = role.Trim();
        Model.ReviewStatus = reviewStatus.Trim();
        Model.ReviewNotes = reviewNotes.Trim();
    }

    private void UpdateValue<T>(ref T field, T next, Action<ExtractionReviewNamedParty> applyToModel, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, next))
        {
            return;
        }

        field = next;
        applyToModel(Model);
        SyncBackToModel();
        OnPropertyChanged(propertyName);
        onPartyChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ExtractionReviewVolumeFolioViewModel : INotifyPropertyChanged
{
    private readonly Action onVolumeFolioChanged;
    private string volume;
    private string folio;
    private string rawText;
    private string reviewStatus;
    private string reviewNotes;

    public ExtractionReviewVolumeFolioViewModel(ExtractionReviewVolumeFolio model, Action onVolumeFolioChanged)
    {
        Model = model;
        this.onVolumeFolioChanged = onVolumeFolioChanged;
        volume = model.Volume;
        folio = model.Folio;
        rawText = model.RawText;
        reviewStatus = model.ReviewStatus;
        reviewNotes = model.ReviewNotes;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ExtractionReviewVolumeFolio Model { get; }

    public string Volume
    {
        get => volume;
        set => UpdateValue(ref volume, value, model => model.Volume = value?.Trim() ?? string.Empty);
    }

    public string Folio
    {
        get => folio;
        set => UpdateValue(ref folio, value, model => model.Folio = value?.Trim() ?? string.Empty);
    }

    public string RawText
    {
        get => rawText;
        set => UpdateValue(ref rawText, value, model => model.RawText = value?.Trim() ?? string.Empty);
    }

    public string SourceLabel
    {
        get
        {
            var parts = new[]
            {
                string.IsNullOrWhiteSpace(Model.SourcePage) ? null : $"page {Model.SourcePage}",
                string.IsNullOrWhiteSpace(Model.SourceZone) ? null : Model.SourceZone
            }.Where(part => !string.IsNullOrWhiteSpace(part));
            return string.Join(" - ", parts);
        }
    }

    public string ReviewStatus
    {
        get => reviewStatus;
        set => UpdateValue(ref reviewStatus, value, model => model.ReviewStatus = value?.Trim() ?? string.Empty);
    }

    public string ReviewNotes
    {
        get => reviewNotes;
        set => UpdateValue(ref reviewNotes, value, model => model.ReviewNotes = value?.Trim() ?? string.Empty);
    }

    public void SyncBackToModel()
    {
        Model.Volume = volume.Trim();
        Model.Folio = folio.Trim();
        Model.RawText = rawText.Trim();
        Model.ReviewStatus = reviewStatus.Trim();
        Model.ReviewNotes = reviewNotes.Trim();
    }

    private void UpdateValue<T>(ref T field, T next, Action<ExtractionReviewVolumeFolio> applyToModel, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, next))
        {
            return;
        }

        field = next;
        applyToModel(Model);
        SyncBackToModel();
        OnPropertyChanged(propertyName);
        onVolumeFolioChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
