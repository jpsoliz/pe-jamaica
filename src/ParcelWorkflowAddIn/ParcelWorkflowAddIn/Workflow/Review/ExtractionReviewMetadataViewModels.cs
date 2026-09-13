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
    private string lotNumber;
    private string address;
    private string landValuationNumber;
    private string examinationNumber;
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
        lotNumber = model.LotNumber;
        address = model.Address;
        landValuationNumber = model.LandValuationNumber;
        examinationNumber = model.ExaminationNumber;
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

    public string LotNumber
    {
        get => lotNumber;
        set => UpdateValue(ref lotNumber, value, model => model.LotNumber = value?.Trim() ?? string.Empty);
    }

    public string Address
    {
        get => address;
        set => UpdateValue(ref address, value, model => model.Address = value?.Trim() ?? string.Empty);
    }

    public string LandValuationNumber
    {
        get => landValuationNumber;
        set => UpdateValue(ref landValuationNumber, value, model => model.LandValuationNumber = value?.Trim() ?? string.Empty);
    }

    public string ExaminationNumber
    {
        get => examinationNumber;
        set => UpdateValue(ref examinationNumber, value, model => model.ExaminationNumber = value?.Trim() ?? string.Empty);
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
        Model.LotNumber = lotNumber.Trim();
        Model.Address = address.Trim();
        Model.LandValuationNumber = landValuationNumber.Trim();
        Model.ExaminationNumber = examinationNumber.Trim();
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
    private string lotNumber;
    private string address;
    private string landValuationNumber;
    private string examinationNumber;
    private string volume;
    private string folio;
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
        lotNumber = model.LotNumber;
        address = model.Address;
        landValuationNumber = model.LandValuationNumber;
        examinationNumber = model.ExaminationNumber;
        volume = model.Volume;
        folio = model.Folio;
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

    public string LotNumber
    {
        get => lotNumber;
        set => UpdateValue(ref lotNumber, value, model => model.LotNumber = value?.Trim() ?? string.Empty);
    }

    public string Address
    {
        get => address;
        set => UpdateValue(ref address, value, model => model.Address = value?.Trim() ?? string.Empty);
    }

    public string LandValuationNumber
    {
        get => landValuationNumber;
        set => UpdateValue(ref landValuationNumber, value, model => model.LandValuationNumber = value?.Trim() ?? string.Empty);
    }

    public string ExaminationNumber
    {
        get => examinationNumber;
        set => UpdateValue(ref examinationNumber, value, model => model.ExaminationNumber = value?.Trim() ?? string.Empty);
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
        Model.LotNumber = lotNumber.Trim();
        Model.Address = address.Trim();
        Model.LandValuationNumber = landValuationNumber.Trim();
        Model.ExaminationNumber = examinationNumber.Trim();
        Model.Volume = volume.Trim();
        Model.Folio = folio.Trim();
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

public sealed class ExtractionReviewParticipantViewModel : INotifyPropertyChanged
{
    private readonly ExtractionReviewAdjacentOwnerViewModel? adjacentOwner;
    private readonly ExtractionReviewNamedPartyViewModel? namedParty;

    public ExtractionReviewParticipantViewModel(ExtractionReviewAdjacentOwnerViewModel adjacentOwner)
    {
        this.adjacentOwner = adjacentOwner;
        adjacentOwner.PropertyChanged += OnSourcePropertyChanged;
    }

    public ExtractionReviewParticipantViewModel(ExtractionReviewNamedPartyViewModel namedParty)
    {
        this.namedParty = namedParty;
        namedParty.PropertyChanged += OnSourcePropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => adjacentOwner?.Name ?? namedParty?.Name ?? string.Empty;
        set
        {
            if (adjacentOwner is not null)
            {
                adjacentOwner.Name = value;
            }
            else if (namedParty is not null)
            {
                namedParty.Name = value;
            }
        }
    }

    public string Role
    {
        get => adjacentOwner?.Role ?? namedParty?.Role ?? string.Empty;
        set
        {
            if (adjacentOwner is not null)
            {
                adjacentOwner.Role = value;
            }
            else if (namedParty is not null)
            {
                namedParty.Role = value;
            }
        }
    }

    public string LotNumber
    {
        get => adjacentOwner?.LotNumber ?? namedParty?.LotNumber ?? string.Empty;
        set
        {
            if (adjacentOwner is not null)
            {
                adjacentOwner.LotNumber = value;
            }
            else if (namedParty is not null)
            {
                namedParty.LotNumber = value;
            }
        }
    }

    public string Address
    {
        get => adjacentOwner?.Address ?? namedParty?.Address ?? string.Empty;
        set
        {
            if (adjacentOwner is not null)
            {
                adjacentOwner.Address = value;
            }
            else if (namedParty is not null)
            {
                namedParty.Address = value;
            }
        }
    }

    public string LandValuationNumber
    {
        get => adjacentOwner?.LandValuationNumber ?? namedParty?.LandValuationNumber ?? string.Empty;
        set
        {
            if (adjacentOwner is not null)
            {
                adjacentOwner.LandValuationNumber = value;
            }
            else if (namedParty is not null)
            {
                namedParty.LandValuationNumber = value;
            }
        }
    }

    public string ExaminationNumber
    {
        get => adjacentOwner?.ExaminationNumber ?? namedParty?.ExaminationNumber ?? string.Empty;
        set
        {
            if (adjacentOwner is not null)
            {
                adjacentOwner.ExaminationNumber = value;
            }
            else if (namedParty is not null)
            {
                namedParty.ExaminationNumber = value;
            }
        }
    }

    public string Volume
    {
        get => adjacentOwner?.Volume ?? namedParty?.Volume ?? string.Empty;
        set
        {
            if (adjacentOwner is not null)
            {
                adjacentOwner.Volume = value;
            }
            else if (namedParty is not null)
            {
                namedParty.Volume = value;
            }
        }
    }

    public string Folio
    {
        get => adjacentOwner?.Folio ?? namedParty?.Folio ?? string.Empty;
        set
        {
            if (adjacentOwner is not null)
            {
                adjacentOwner.Folio = value;
            }
            else if (namedParty is not null)
            {
                namedParty.Folio = value;
            }
        }
    }

    public string SourceLabel => adjacentOwner?.SegmentLabel ?? namedParty?.SourceLabel ?? string.Empty;

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, e);
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

public sealed class ExtractionReviewMemorandumGroupViewModel : INotifyPropertyChanged
{
    public ExtractionReviewMemorandumGroupViewModel(ExtractionReviewMemorandumGroup model, Action onRuleChanged)
    {
        Model = model;
        Rules = model.Rules
            .Select(rule => new ExtractionReviewMemorandumRuleResultViewModel(rule, () =>
            {
                OnPropertyChanged(nameof(Summary));
                onRuleChanged();
            }))
            .ToArray();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ExtractionReviewMemorandumGroup Model { get; }

    public string DisplayName => Model.DisplayName;

    public string Summary
    {
        get
        {
            var needsReview = Rules.Count(rule => rule.IsUnresolvedDisposition);
            return needsReview == 1
                ? "1 needs review"
                : $"{needsReview} needs review";
        }
    }

    public IReadOnlyList<ExtractionReviewMemorandumRuleResultViewModel> Rules { get; }

    public void RefreshSummary()
    {
        OnPropertyChanged(nameof(Summary));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ExtractionReviewMemorandumRuleResultViewModel : INotifyPropertyChanged
{
    private readonly Action onRuleChanged;
    private string reviewerStatus;
    private string message;

    public ExtractionReviewMemorandumRuleResultViewModel(ExtractionReviewMemorandumRuleResult model, Action onRuleChanged)
    {
        Model = model;
        this.onRuleChanged = onRuleChanged;
        reviewerStatus = model.ReviewerStatus;
        message = model.Message;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ExtractionReviewMemorandumRuleResult Model { get; }

    public string Label => Model.Label;

    public IReadOnlyList<string> ReviewerStatusOptions { get; } =
    [
        "Accepted",
        "Corrected",
        "Skipped",
        "Not available"
    ];

    public bool IsUnresolvedDisposition =>
        string.Equals(reviewerStatus, "Needs Review", StringComparison.OrdinalIgnoreCase)
        || string.Equals(reviewerStatus, "Failed", StringComparison.OrdinalIgnoreCase);

    public string ReviewerStatus
    {
        get => reviewerStatus;
        set => SetReviewerStatus(value, notifyReviewChanged: true);
    }

    public void SetReviewerStatusForBulkApply(string value)
    {
        SetReviewerStatus(value, notifyReviewChanged: false);
    }

    private void SetReviewerStatus(string value, bool notifyReviewChanged)
    {
        var next = value?.Trim() ?? string.Empty;
        if (string.Equals(reviewerStatus, next, StringComparison.Ordinal))
        {
            return;
        }

        reviewerStatus = next;
        Model.ReviewerStatus = next;
        OnPropertyChanged(nameof(ReviewerStatus));
        OnPropertyChanged(nameof(IsUnresolvedDisposition));
        if (notifyReviewChanged)
        {
            onRuleChanged();
        }
    }

    public string WorkflowEffect => Model.WorkflowEffect;

    public string EvidenceValue => Model.EvidenceValue;

    public string EvidenceState => Model.EvidenceState;

    public string Message
    {
        get => message;
        set
        {
            var next = value?.Trim() ?? string.Empty;
            if (string.Equals(message, next, StringComparison.Ordinal))
            {
                return;
            }

            message = next;
            Model.Message = next;
            OnPropertyChanged();
            onRuleChanged();
        }
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
