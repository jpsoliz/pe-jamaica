using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;

namespace ParcelWorkflowAddIn;

internal sealed class MapGeoreferenceViewModel : INotifyPropertyChanged, IDisposable
{
    private const int Jad2001Wkid = 3448;
    private readonly MapGeoreferenceOverlayService overlayService = new();
    private readonly RelayCommand calculateDiagnosticsCommand;
    private readonly RelayCommand createOverlayCommand;
    private readonly RelayCommand pickDocumentPoint1Command;
    private readonly RelayCommand pickDocumentPoint2Command;
    private readonly RelayCommand pickMapPoint1Command;
    private readonly RelayCommand pickMapPoint2Command;
    private readonly RelayCommand clearPoint1Command;
    private readonly RelayCommand clearPoint2Command;
    private readonly RelayCommand clearAllPointsCommand;
    private readonly RelayCommand removeOverlayCommand;
    private bool disposed;
    private bool isCreatingOverlay;
    private string documentPoint1Easting = string.Empty;
    private string documentPoint1Northing = string.Empty;
    private string documentPoint2Easting = string.Empty;
    private string documentPoint2Northing = string.Empty;
    private string mapPoint1Easting = string.Empty;
    private string mapPoint1Northing = string.Empty;
    private string mapPoint2Easting = string.Empty;
    private string mapPoint2Northing = string.Empty;
    private string selectedSourcePageNumber = "1";
    private int overlayTransparencyPercent = 70;
    private string diagnosticsText = "Pick two PDF/image points and enter the matching JAD2001 coordinates, then check the overlay inputs.";
    private string diagnosticsSeverity = "Info";
    private string overlayStatusText = "Capture the visible PDF page or use a supported image, pick two plan points, enter the matching JAD2001 coordinates for those points, then create the temporary overlay.";
    private string documentImagePoint1Text = "Not picked";
    private string documentImagePoint2Text = "Not picked";
    private BitmapSource? overlayPickerImageSource;
    private MapGeoreferenceImagePoint? documentImagePoint1;
    private MapGeoreferenceImagePoint? documentImagePoint2;
    private DocumentPointPickTarget activePickTarget = DocumentPointPickTarget.None;

    public MapGeoreferenceViewModel(
        string transactionNumber,
        SupportingDocumentsDockpaneViewModel documents,
        MapGeoreferenceWorkflowMode mode = MapGeoreferenceWorkflowMode.CoordinateControl)
    {
        TransactionNumber = string.IsNullOrWhiteSpace(transactionNumber) ? "Transaction" : transactionNumber;
        Documents = documents;
        Mode = mode;
        diagnosticsText = InitialDiagnosticsText;
        overlayStatusText = InitialOverlayStatusText;
        calculateDiagnosticsCommand = new RelayCommand(CalculateDiagnostics, CanCalculateDiagnostics);
        createOverlayCommand = new RelayCommand(async () => await CreateOverlayAsync().ConfigureAwait(true), CanCreateOverlay);
        pickDocumentPoint1Command = new RelayCommand(() => BeginDocumentPointPick(DocumentPointPickTarget.Point1), CanPickDocumentPoint);
        pickDocumentPoint2Command = new RelayCommand(() => BeginDocumentPointPick(DocumentPointPickTarget.Point2), CanPickDocumentPoint);
        pickMapPoint1Command = new RelayCommand(async () => await BeginMapPointPickAsync(MapPointPickTarget.Point1).ConfigureAwait(true), CanPickMapPoint);
        pickMapPoint2Command = new RelayCommand(async () => await BeginMapPointPickAsync(MapPointPickTarget.Point2).ConfigureAwait(true), CanPickMapPoint);
        clearPoint1Command = new RelayCommand(() => ClearControlPair(MapPointPickTarget.Point1), CanClearPoint1);
        clearPoint2Command = new RelayCommand(() => ClearControlPair(MapPointPickTarget.Point2), CanClearPoint2);
        clearAllPointsCommand = new RelayCommand(ClearAllControlPairs, CanClearAnyPoint);
        removeOverlayCommand = new RelayCommand(async () => await RemoveComparisonOverlayAsync().ConfigureAwait(true), CanRemoveOverlay);
        Documents.PropertyChanged += OnDocumentsPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MapGeoreferenceWorkflowMode Mode { get; }

    public bool IsImageComparisonMode => Mode == MapGeoreferenceWorkflowMode.ImageComparison;

    public string Title => IsImageComparisonMode
        ? $"Title Plan [TR-{TransactionNumber}]"
        : $"Map Georeference Review [TR-{TransactionNumber}]";

    public string WorkflowHeading => IsImageComparisonMode ? "Title Plan" : "M-Geo";

    public string PointPickingHeading => IsImageComparisonMode ? "Control points" : "PDF points";

    public string CapturePdfButtonText => IsImageComparisonMode ? "Capture page" : "Capture PDF";

    public string PickPoint1ButtonText => IsImageComparisonMode ? "Plan 1" : "PDF 1";

    public string PickPoint2ButtonText => IsImageComparisonMode ? "Plan 2" : "PDF 2";

    public string CoordinateEntryHeading => IsImageComparisonMode
        ? "Map coordinates"
        : "JAD2001 coordinates for picked PDF points";

    public string CheckInputsButtonText => IsImageComparisonMode ? "Preview" : "Check";

    public string CreateOverlayButtonText => IsImageComparisonMode ? "Create Overlay" : "Create Overlay";

    public string SourcePageLabel => IsImageComparisonMode ? "Page" : "PDF page";

    private string InitialDiagnosticsText => IsImageComparisonMode
        ? "Step 1: confirm the PDF page is visible. Step 2: capture the page. Step 3: pick two plan points and the same two points on the map."
        : "Pick two PDF/image points and enter the matching JAD2001 coordinates, then check the overlay inputs.";

    private string InitialOverlayStatusText => IsImageComparisonMode
        ? "The left panel first shows the selected PDF page. Capture it, pick two plan points, pick the same two ArcGIS Pro map points, then create the comparison overlay."
        : "Capture the visible PDF page or use a supported image, pick two plan points, enter the matching JAD2001 coordinates for those points, then create the temporary overlay.";

    public string TransactionNumber { get; }

    public SupportingDocumentsDockpaneViewModel Documents { get; }

    public ICommand CalculateDiagnosticsCommand => calculateDiagnosticsCommand;

    public ICommand CreateOverlayCommand => createOverlayCommand;

    public ICommand PickDocumentPoint1Command => pickDocumentPoint1Command;

    public ICommand PickDocumentPoint2Command => pickDocumentPoint2Command;

    public ICommand PickMapPoint1Command => pickMapPoint1Command;

    public ICommand PickMapPoint2Command => pickMapPoint2Command;

    public ICommand ClearPoint1Command => clearPoint1Command;

    public ICommand ClearPoint2Command => clearPoint2Command;

    public ICommand ClearAllPointsCommand => clearAllPointsCommand;

    public ICommand RemoveOverlayCommand => removeOverlayCommand;

    public string CoordinateSystemText => $"Coordinate system: JAD2001 / EPSG:{Jad2001Wkid}";

    public string SelectedSourcePageNumber
    {
        get => selectedSourcePageNumber;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "1" : value.Trim();
            if (string.Equals(selectedSourcePageNumber, normalized, StringComparison.Ordinal))
            {
                return;
            }

            selectedSourcePageNumber = normalized;
            NotifyPropertyChanged(nameof(SelectedSourcePageNumber));
            NotifyPropertyChanged(nameof(SelectedPdfPageNumber));
            RefreshOverlayCommandState();
        }
    }

    public int? SelectedPdfPageNumber => SelectedSupportingDocumentIsPdf ? TryReadSelectedPageNumber() : null;

    public int OverlayTransparencyPercent
    {
        get => overlayTransparencyPercent;
        set
        {
            var normalized = Math.Clamp(value, 0, 90);
            if (overlayTransparencyPercent == normalized)
            {
                return;
            }

            overlayTransparencyPercent = normalized;
            NotifyPropertyChanged(nameof(OverlayTransparencyPercent));
            NotifyPropertyChanged(nameof(OverlayTransparencyText));
            RefreshOverlayCommandState();
        }
    }

    public string OverlayTransparencyText => $"{OverlayTransparencyPercent}% transparency";

    public IReadOnlyList<SourceFileListItem> GeoreferenceDocumentOptions =>
        Documents.SupportingDocumentOptions
            .Where(IsSupportedGeoreferenceDocument)
            .ToArray();

    public bool HasGeoreferenceDocumentOptions => GeoreferenceDocumentOptions.Count > 0;

    private bool SelectedSupportingDocumentIsPdf => Documents.SelectedSupportingDocument is { } item
        && ResolveExtension(item.SourceFile) == ".pdf";

    public string OverlayStatusText
    {
        get => overlayStatusText;
        private set
        {
            if (!string.Equals(overlayStatusText, value, StringComparison.Ordinal))
            {
                overlayStatusText = value;
                NotifyPropertyChanged(nameof(OverlayStatusText));
            }
        }
    }

    public ImageSource? OverlayPickerImageSource => overlayPickerImageSource;

    public bool OverlayPickerImageAvailable => overlayPickerImageSource is not null;

    public string DocumentImagePoint1Text
    {
        get => documentImagePoint1Text;
        private set
        {
            if (!string.Equals(documentImagePoint1Text, value, StringComparison.Ordinal))
            {
                documentImagePoint1Text = value;
                NotifyPropertyChanged(nameof(DocumentImagePoint1Text));
            }
        }
    }

    public string DocumentImagePoint2Text
    {
        get => documentImagePoint2Text;
        private set
        {
            if (!string.Equals(documentImagePoint2Text, value, StringComparison.Ordinal))
            {
                documentImagePoint2Text = value;
                NotifyPropertyChanged(nameof(DocumentImagePoint2Text));
            }
        }
    }

    public string ActiveDocumentPickInstruction => activePickTarget switch
    {
        DocumentPointPickTarget.Point1 => "Click the first matching point on the captured document image.",
        DocumentPointPickTarget.Point2 => "Click the second matching point on the captured document image.",
        _ => OverlayPickerImageAvailable
            ? "Choose a plan point button, then click the captured image. Use the matching map point button to capture the same location in ArcGIS Pro."
            : IsImageComparisonMode
                ? "When the PDF page is visible, press Capture selected PDF page to enable point picking."
                : "Capture the PDF view first, or choose a supported image document."
    };

    public string DocumentPoint1Easting
    {
        get => documentPoint1Easting;
        set => SetCoordinateField(ref documentPoint1Easting, value);
    }

    public string DocumentPoint1Northing
    {
        get => documentPoint1Northing;
        set => SetCoordinateField(ref documentPoint1Northing, value);
    }

    public string DocumentPoint2Easting
    {
        get => documentPoint2Easting;
        set => SetCoordinateField(ref documentPoint2Easting, value);
    }

    public string DocumentPoint2Northing
    {
        get => documentPoint2Northing;
        set => SetCoordinateField(ref documentPoint2Northing, value);
    }

    public string MapPoint1Easting
    {
        get => mapPoint1Easting;
        set => SetCoordinateField(ref mapPoint1Easting, value);
    }

    public string MapPoint1Northing
    {
        get => mapPoint1Northing;
        set => SetCoordinateField(ref mapPoint1Northing, value);
    }

    public string MapPoint2Easting
    {
        get => mapPoint2Easting;
        set => SetCoordinateField(ref mapPoint2Easting, value);
    }

    public string MapPoint2Northing
    {
        get => mapPoint2Northing;
        set => SetCoordinateField(ref mapPoint2Northing, value);
    }

    public string MapPoint1Text => TryReadPoint(mapPoint1Easting, mapPoint1Northing, out var point)
        ? FormatMapPoint(point)
        : "Map point 1 not picked";

    public string MapPoint2Text => TryReadPoint(mapPoint2Easting, mapPoint2Northing, out var point)
        ? FormatMapPoint(point)
        : "Map point 2 not picked";

    public string DiagnosticsText
    {
        get => diagnosticsText;
        private set
        {
            if (!string.Equals(diagnosticsText, value, StringComparison.Ordinal))
            {
                diagnosticsText = value;
                NotifyPropertyChanged(nameof(DiagnosticsText));
            }
        }
    }

    public string DiagnosticsSeverity
    {
        get => diagnosticsSeverity;
        private set
        {
            if (!string.Equals(diagnosticsSeverity, value, StringComparison.Ordinal))
            {
                diagnosticsSeverity = value;
                NotifyPropertyChanged(nameof(DiagnosticsSeverity));
            }
        }
    }

    public bool DiagnosticsHasWarning => string.Equals(DiagnosticsSeverity, "Warning", StringComparison.OrdinalIgnoreCase);

    public bool DiagnosticsHasError => string.Equals(DiagnosticsSeverity, "Error", StringComparison.OrdinalIgnoreCase);

    internal void Reload()
    {
        Documents.ReloadActiveCaseFolder();
        EnsureSupportedDocumentSelection();
        TryUseSelectedImageAsPickerSource();
        RefreshDocumentProperties();
        _ = RestorePersistedOverlayAsync();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Documents.PropertyChanged -= OnDocumentsPropertyChanged;
    }

    internal void MarkRenderFailure(string message)
    {
        Documents.MarkSupportingDocumentRenderFailure(message);
    }

    internal void MarkRenderAttempt(string message)
    {
        Documents.MarkSupportingDocumentRenderAttempt(message);
    }

    internal void MarkRenderReady(string message)
    {
        Documents.MarkSupportingDocumentRenderReady(message);
    }

    internal void SetCapturedPdfImage(BitmapSource image)
    {
        SetOverlayPickerImage(image);
        OverlayStatusText = IsImageComparisonMode
            ? "Captured the selected PDF page. Pick two plan points and their matching ArcGIS Pro map points."
            : "Captured the visible PDF view. Pick the two document points, then create the transparent overlay.";
    }

    internal bool TryApplyDocumentImagePick(double x, double y)
    {
        if (overlayPickerImageSource is null || activePickTarget == DocumentPointPickTarget.None)
        {
            return false;
        }

        var point = new MapGeoreferenceImagePoint(x, y);
        if (activePickTarget == DocumentPointPickTarget.Point1)
        {
            documentImagePoint1 = point;
            DocumentImagePoint1Text = FormatImagePoint(point);
        }
        else
        {
            documentImagePoint2 = point;
            DocumentImagePoint2Text = FormatImagePoint(point);
        }

        activePickTarget = DocumentPointPickTarget.None;
        OverlayStatusText = IsImageComparisonMode
            ? "Plan point captured. Pick the matching ArcGIS Pro map point for the same location."
            : "PDF point captured. Create Overlay will be available once both PDF picks and both matching JAD2001 coordinates are valid.";
        calculateDiagnosticsCommand.RaiseCanExecuteChanged();
        RefreshOverlayCommandState();
        NotifyPropertyChanged(nameof(ActiveDocumentPickInstruction));
        return true;
    }

    private bool CanCalculateDiagnostics()
    {
        return documentImagePoint1 is not null
            && documentImagePoint2 is not null
            && TryRead(mapPoint1Easting, out _)
            && TryRead(mapPoint1Northing, out _)
            && TryRead(mapPoint2Easting, out _)
            && TryRead(mapPoint2Northing, out _);
    }

    private bool CanPickDocumentPoint()
    {
        return overlayPickerImageSource is not null && !isCreatingOverlay;
    }

    private bool CanPickMapPoint()
    {
        return IsImageComparisonMode && !isCreatingOverlay;
    }

    private bool CanClearPoint1()
    {
        return documentImagePoint1 is not null || TryReadPoint(mapPoint1Easting, mapPoint1Northing, out _);
    }

    private bool CanClearPoint2()
    {
        return documentImagePoint2 is not null || TryReadPoint(mapPoint2Easting, mapPoint2Northing, out _);
    }

    private bool CanClearAnyPoint()
    {
        return CanClearPoint1() || CanClearPoint2();
    }

    private bool CanRemoveOverlay()
    {
        return IsImageComparisonMode && !isCreatingOverlay;
    }

    private bool CanCreateOverlay()
    {
        return !isCreatingOverlay
            && overlayPickerImageSource is not null
            && documentImagePoint1 is not null
            && documentImagePoint2 is not null
            && (!IsImageComparisonMode || !SelectedSupportingDocumentIsPdf || SelectedPdfPageNumber.HasValue)
            && TryReadPoint(mapPoint1Easting, mapPoint1Northing, out _)
            && TryReadPoint(mapPoint2Easting, mapPoint2Northing, out _);
    }

    private async Task CreateOverlayAsync()
    {
        if (overlayPickerImageSource is null
            || documentImagePoint1 is not { } picked1
            || documentImagePoint2 is not { } picked2
            || !TryReadPoint(mapPoint1Easting, mapPoint1Northing, out var map1)
            || !TryReadPoint(mapPoint2Easting, mapPoint2Northing, out var map2))
        {
            OverlayStatusText = "Pick two PDF points and enter two valid matching JAD2001 coordinates before creating the overlay.";
            return;
        }

        if (IsImageComparisonMode && SelectedSupportingDocumentIsPdf && SelectedPdfPageNumber is null)
        {
            OverlayStatusText = "Enter a valid PDF page number before creating the title-plan comparison overlay.";
            return;
        }

        isCreatingOverlay = true;
        RefreshOverlayCommandState();
        OverlayStatusText = "Creating temporary 70% transparent map overlay...";
        try
        {
            var result = await overlayService.CreateOverlayAsync(
                TransactionNumber,
                overlayPickerImageSource,
                picked1,
                picked2,
                new MapGeoreferenceCoordinatePoint(map1.Easting, map1.Northing),
                new MapGeoreferenceCoordinatePoint(map2.Easting, map2.Northing),
                Mode == MapGeoreferenceWorkflowMode.ImageComparison
                    ? MapGeoreferenceOverlayKind.TitlePlanComparison
                    : MapGeoreferenceOverlayKind.MGeo,
                Documents.SelectedSupportingDocument?.SourceFile.CopiedPath
                    ?? Documents.SelectedSupportingDocument?.SourceRelativePath,
                SelectedSupportingDocumentIsPdf ? TryReadSelectedPageNumber() : null,
                OverlayTransparencyPercent).ConfigureAwait(true);
            OverlayStatusText = result.Message;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or ArcGIS.Core.CalledOnWrongThreadException)
        {
            OverlayStatusText = $"Could not create the map overlay: {exception.Message}";
        }
        finally
        {
            isCreatingOverlay = false;
            RefreshOverlayCommandState();
        }
    }

    private async Task RestorePersistedOverlayAsync()
    {
        try
        {
            var result = await overlayService.TryRestorePersistedOverlayAsync(
                TransactionNumber,
                Mode == MapGeoreferenceWorkflowMode.ImageComparison
                    ? MapGeoreferenceOverlayKind.TitlePlanComparison
                    : MapGeoreferenceOverlayKind.MGeo).ConfigureAwait(true);
            if (result.Success)
            {
                OverlayStatusText = result.Message;
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or ArcGIS.Core.CalledOnWrongThreadException)
        {
            var label = IsImageComparisonMode ? "title-plan comparison" : "M-Geo";
            OverlayStatusText = $"A saved {label} overlay was found, but it could not be restored: {exception.Message}";
        }
    }

    private void CalculateDiagnostics()
    {
        if (documentImagePoint1 is not { } picked1
            || documentImagePoint2 is not { } picked2
            || !TryReadPoint(mapPoint1Easting, mapPoint1Northing, out var map1)
            || !TryReadPoint(mapPoint2Easting, mapPoint2Northing, out var map2))
        {
            DiagnosticsSeverity = "Error";
            DiagnosticsText = "Pick two PDF/image points and enter valid matching JAD2001 coordinates.";
            RefreshDiagnosticsFlags();
            return;
        }

        var pixelDistance = Distance(picked1, picked2);
        var mapDistance = Distance(map1, map2);
        if (pixelDistance <= 0.0001 || mapDistance <= 0.0001)
        {
            DiagnosticsSeverity = "Error";
            DiagnosticsText = "The two PDF/image points and the two JAD2001 points must be different locations.";
            RefreshDiagnosticsFlags();
            return;
        }

        var metersPerPixel = mapDistance / pixelDistance;
        var imageAngle = Math.Atan2(-(picked2.Y - picked1.Y), picked2.X - picked1.X) * 180.0 / Math.PI;
        var mapAngle = Math.Atan2(map2.Northing - map1.Northing, map2.Easting - map1.Easting) * 180.0 / Math.PI;
        var rotation = NormalizeDegrees(mapAngle - imageAngle);

        DiagnosticsSeverity = "Info";
        DiagnosticsText =
            $"Overlay inputs ready. Picked PDF/image distance: {pixelDistance:0.#} px. " +
            $"JAD2001 distance: {mapDistance:0.###} m. Scale: {metersPerPixel:0.######} m/px. " +
            $"Rotation: {rotation:0.###} degrees. Create Overlay will place the captured image using these two point pairs. " +
            $"Transparency: {OverlayTransparencyPercent}%. Continue only when the active ArcGIS Pro map is JAD2001 / EPSG:3448.";
        RefreshDiagnosticsFlags();
    }

    private void SetCoordinateField(ref string field, string value)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        NotifyPropertyChanged(string.Empty);
        NotifyPropertyChanged(nameof(MapPoint1Text));
        NotifyPropertyChanged(nameof(MapPoint2Text));
        calculateDiagnosticsCommand.RaiseCanExecuteChanged();
        RefreshOverlayCommandState();
    }

    private void RefreshDocumentProperties()
    {
        NotifyPropertyChanged(nameof(Documents));
        NotifyPropertyChanged(nameof(GeoreferenceDocumentOptions));
        NotifyPropertyChanged(nameof(HasGeoreferenceDocumentOptions));
        NotifyPropertyChanged(nameof(SelectedPdfPageNumber));
        NotifyPropertyChanged(nameof(OverlayTransparencyPercent));
        NotifyPropertyChanged(nameof(OverlayTransparencyText));
        NotifyPropertyChanged(nameof(OverlayPickerImageSource));
        NotifyPropertyChanged(nameof(OverlayPickerImageAvailable));
        NotifyPropertyChanged(nameof(ActiveDocumentPickInstruction));
        calculateDiagnosticsCommand.RaiseCanExecuteChanged();
        RefreshOverlayCommandState();
    }

    private void OnDocumentsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SupportingDocumentsDockpaneViewModel.SupportingDocumentOptions)
            or nameof(SupportingDocumentsDockpaneViewModel.SelectedSupportingDocument)
            or nameof(SupportingDocumentsDockpaneViewModel.HasSupportingDocumentOptions)
            or nameof(SupportingDocumentsDockpaneViewModel.SupportingDocumentListSummary)
            or nameof(SupportingDocumentsDockpaneViewModel.SupportingDocumentViewerBrowserUri)
            or nameof(SupportingDocumentsDockpaneViewModel.SupportingDocumentViewerNavigationKey)
            or nameof(SupportingDocumentsDockpaneViewModel.SupportingDocumentViewerImageSource))
        {
            EnsureSupportedDocumentSelection();
            TryUseSelectedImageAsPickerSource();
        }

        RefreshDocumentProperties();
    }

    private void EnsureSupportedDocumentSelection()
    {
        var options = GeoreferenceDocumentOptions;
        if (options.Count == 0)
        {
            return;
        }

        if (Documents.SelectedSupportingDocument is null
            || !IsSupportedGeoreferenceDocument(Documents.SelectedSupportingDocument))
        {
            Documents.SelectedSupportingDocument = options[0];
        }
    }

    private void RefreshDiagnosticsFlags()
    {
        NotifyPropertyChanged(nameof(DiagnosticsHasWarning));
        NotifyPropertyChanged(nameof(DiagnosticsHasError));
    }

    private void BeginDocumentPointPick(DocumentPointPickTarget target)
    {
        if (overlayPickerImageSource is null)
        {
            OverlayStatusText = "Capture the visible PDF page, or choose a supported image document, before picking document points.";
            return;
        }

        activePickTarget = target;
        OverlayStatusText = target == DocumentPointPickTarget.Point1
            ? "Pick the first document point on the captured plan image."
            : "Pick the second document point on the captured plan image.";
        NotifyPropertyChanged(nameof(ActiveDocumentPickInstruction));
    }

    private async Task BeginMapPointPickAsync(MapPointPickTarget target)
    {
        if (!IsImageComparisonMode)
        {
            return;
        }

        TitlePlanMapPointTool.Arm(this, target);
        OverlayStatusText = target == MapPointPickTarget.Point1
            ? "Click the matching location for point 1 in the active ArcGIS Pro map."
            : "Click the matching location for point 2 in the active ArcGIS Pro map.";
        try
        {
            await FrameworkApplication.SetCurrentToolAsync(TitlePlanMapPointTool.ToolId).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            TitlePlanMapPointTool.ClearArmedTarget(this);
            OverlayStatusText = $"Could not activate the map point picker: {exception.Message}";
        }
    }

    internal void ApplyCapturedMapPoint(MapPointPickTarget target, double easting, double northing)
    {
        if (target == MapPointPickTarget.Point1)
        {
            MapPoint1Easting = FormatCoordinate(easting);
            MapPoint1Northing = FormatCoordinate(northing);
        }
        else
        {
            MapPoint2Easting = FormatCoordinate(easting);
            MapPoint2Northing = FormatCoordinate(northing);
        }

        OverlayStatusText = target == MapPointPickTarget.Point1
            ? "Map point 1 captured from ArcGIS Pro. Capture point 2 on the plan and map."
            : "Map point 2 captured from ArcGIS Pro. Preview placement or create the comparison overlay.";
        NotifyPropertyChanged(nameof(MapPoint1Text));
        NotifyPropertyChanged(nameof(MapPoint2Text));
        calculateDiagnosticsCommand.RaiseCanExecuteChanged();
        RefreshOverlayCommandState();
    }

    internal void MarkMapPointPickFailure(string message)
    {
        OverlayStatusText = message;
    }

    private void ClearControlPair(MapPointPickTarget target)
    {
        if (target == MapPointPickTarget.Point1)
        {
            documentImagePoint1 = null;
            DocumentImagePoint1Text = "Not picked";
            MapPoint1Easting = string.Empty;
            MapPoint1Northing = string.Empty;
        }
        else
        {
            documentImagePoint2 = null;
            DocumentImagePoint2Text = "Not picked";
            MapPoint2Easting = string.Empty;
            MapPoint2Northing = string.Empty;
        }

        activePickTarget = DocumentPointPickTarget.None;
        OverlayStatusText = "Control point cleared. Re-pick the plan point and matching map point before creating the overlay.";
        NotifyPropertyChanged(nameof(ActiveDocumentPickInstruction));
        RefreshOverlayCommandState();
    }

    private void ClearAllControlPairs()
    {
        ClearControlPair(MapPointPickTarget.Point1);
        ClearControlPair(MapPointPickTarget.Point2);
        OverlayStatusText = "All control points cleared. Pick two plan/map point pairs to place the title-plan image.";
    }

    private async Task RemoveComparisonOverlayAsync()
    {
        if (!IsImageComparisonMode)
        {
            return;
        }

        try
        {
            await overlayService.RemoveOverlayAsync(
                TransactionNumber,
                kind: MapGeoreferenceOverlayKind.TitlePlanComparison).ConfigureAwait(true);
            OverlayStatusText = "Removed the title-plan comparison overlay from the active map. You can adjust points and create it again.";
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or ArcGIS.Core.CalledOnWrongThreadException)
        {
            OverlayStatusText = $"Could not remove the title-plan comparison overlay: {exception.Message}";
        }
    }

    private void TryUseSelectedImageAsPickerSource()
    {
        if (Documents.SupportingDocumentViewerImageSource is BitmapSource bitmap)
        {
            SetOverlayPickerImage(bitmap);
            OverlayStatusText = "Image document is ready for point picking. Pick two document points, then create the overlay.";
        }
    }

    private void SetOverlayPickerImage(BitmapSource image)
    {
        if (image.CanFreeze && !image.IsFrozen)
        {
            image.Freeze();
        }

        overlayPickerImageSource = image;
        documentImagePoint1 = null;
        documentImagePoint2 = null;
        activePickTarget = DocumentPointPickTarget.None;
        DocumentImagePoint1Text = "Not picked";
        DocumentImagePoint2Text = "Not picked";
        NotifyPropertyChanged(nameof(OverlayPickerImageSource));
        NotifyPropertyChanged(nameof(OverlayPickerImageAvailable));
        NotifyPropertyChanged(nameof(ActiveDocumentPickInstruction));
        RefreshOverlayCommandState();
    }

    private void RefreshOverlayCommandState()
    {
        createOverlayCommand.RaiseCanExecuteChanged();
        pickDocumentPoint1Command.RaiseCanExecuteChanged();
        pickDocumentPoint2Command.RaiseCanExecuteChanged();
        pickMapPoint1Command.RaiseCanExecuteChanged();
        pickMapPoint2Command.RaiseCanExecuteChanged();
        clearPoint1Command.RaiseCanExecuteChanged();
        clearPoint2Command.RaiseCanExecuteChanged();
        clearAllPointsCommand.RaiseCanExecuteChanged();
        removeOverlayCommand.RaiseCanExecuteChanged();
    }

    private static bool TryReadPoint(string easting, string northing, out JadPoint point)
    {
        if (TryRead(easting, out var e) && TryRead(northing, out var n))
        {
            point = new JadPoint(e, n);
            return true;
        }

        point = default;
        return false;
    }

    private static bool TryRead(string text, out double value)
    {
        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value)
            || double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value))
        {
            return double.IsFinite(value);
        }

        value = default;
        return false;
    }

    private int? TryReadSelectedPageNumber()
    {
        return int.TryParse(SelectedSourcePageNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var page)
            && page > 0
            ? page
            : null;
    }

    private static double Distance(JadPoint a, JadPoint b)
    {
        var de = b.Easting - a.Easting;
        var dn = b.Northing - a.Northing;
        return Math.Sqrt((de * de) + (dn * dn));
    }

    private static double Distance(MapGeoreferenceImagePoint a, MapGeoreferenceImagePoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static double BearingDegrees(JadPoint a, JadPoint b)
    {
        return Math.Atan2(b.Easting - a.Easting, b.Northing - a.Northing) * 180.0 / Math.PI;
    }

    private static string FormatImagePoint(MapGeoreferenceImagePoint point)
    {
        return $"X {point.X:0.#}, Y {point.Y:0.#}";
    }

    private static string FormatMapPoint(JadPoint point)
    {
        return $"E {point.Easting:0.###}, N {point.Northing:0.###}";
    }

    private static string FormatCoordinate(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static double NormalizeDegrees(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0.0;
        }

        value %= 360.0;
        if (value <= -180.0)
        {
            value += 360.0;
        }

        if (value > 180.0)
        {
            value -= 360.0;
        }

        return value;
    }

    private static bool IsSupportedGeoreferenceDocument(SourceFileListItem item)
    {
        var extension = ResolveExtension(item.SourceFile);
        return extension is ".pdf" or ".png" or ".jpg" or ".jpeg" or ".tif" or ".tiff";
    }

    private static string ResolveExtension(CaseFolders.SourceFileCopyResult sourceFile)
    {
        if (!string.IsNullOrWhiteSpace(sourceFile.FileType))
        {
            var fileType = sourceFile.FileType.Trim();
            return fileType.StartsWith(".", StringComparison.Ordinal)
                ? fileType.ToLowerInvariant()
                : $".{fileType.ToLowerInvariant()}";
        }

        return Path.GetExtension(sourceFile.FileName).ToLowerInvariant();
    }

    private void NotifyPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private readonly record struct JadPoint(double Easting, double Northing);

    private enum DocumentPointPickTarget
    {
        None,
        Point1,
        Point2
    }
}

internal enum MapPointPickTarget
{
    Point1,
    Point2
}

internal enum MapGeoreferenceWorkflowMode
{
    CoordinateControl,
    ImageComparison
}
