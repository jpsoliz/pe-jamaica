using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;

namespace ParcelWorkflowAddIn;

internal sealed class MapGeoreferenceViewModel : INotifyPropertyChanged, IDisposable
{
    private const int Jad2001Wkid = 3448;
    private readonly MapGeoreferenceOverlayService overlayService = new();
    private readonly RelayCommand calculateDiagnosticsCommand;
    private readonly RelayCommand createOverlayCommand;
    private readonly RelayCommand pickDocumentPoint1Command;
    private readonly RelayCommand pickDocumentPoint2Command;
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
    private string diagnosticsText = "Pick two PDF/image points and enter the matching JAD2001 coordinates, then check the overlay inputs.";
    private string diagnosticsSeverity = "Info";
    private string overlayStatusText = "Capture the visible PDF page or use a supported image, pick two plan points, enter the matching JAD2001 coordinates for those points, then create the temporary overlay.";
    private string documentImagePoint1Text = "Not picked";
    private string documentImagePoint2Text = "Not picked";
    private BitmapSource? overlayPickerImageSource;
    private MapGeoreferenceImagePoint? documentImagePoint1;
    private MapGeoreferenceImagePoint? documentImagePoint2;
    private DocumentPointPickTarget activePickTarget = DocumentPointPickTarget.None;

    public MapGeoreferenceViewModel(string transactionNumber, SupportingDocumentsDockpaneViewModel documents)
    {
        TransactionNumber = string.IsNullOrWhiteSpace(transactionNumber) ? "Transaction" : transactionNumber;
        Documents = documents;
        calculateDiagnosticsCommand = new RelayCommand(CalculateDiagnostics, CanCalculateDiagnostics);
        createOverlayCommand = new RelayCommand(async () => await CreateOverlayAsync().ConfigureAwait(true), CanCreateOverlay);
        pickDocumentPoint1Command = new RelayCommand(() => BeginDocumentPointPick(DocumentPointPickTarget.Point1), CanPickDocumentPoint);
        pickDocumentPoint2Command = new RelayCommand(() => BeginDocumentPointPick(DocumentPointPickTarget.Point2), CanPickDocumentPoint);
        Documents.PropertyChanged += OnDocumentsPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => $"Map Georeference Review [TR-{TransactionNumber}]";

    public string TransactionNumber { get; }

    public SupportingDocumentsDockpaneViewModel Documents { get; }

    public ICommand CalculateDiagnosticsCommand => calculateDiagnosticsCommand;

    public ICommand CreateOverlayCommand => createOverlayCommand;

    public ICommand PickDocumentPoint1Command => pickDocumentPoint1Command;

    public ICommand PickDocumentPoint2Command => pickDocumentPoint2Command;

    public string CoordinateSystemText => $"Coordinate system: JAD2001 / EPSG:{Jad2001Wkid}";

    public IReadOnlyList<SourceFileListItem> GeoreferenceDocumentOptions =>
        Documents.SupportingDocumentOptions
            .Where(IsSupportedGeoreferenceDocument)
            .ToArray();

    public bool HasGeoreferenceDocumentOptions => GeoreferenceDocumentOptions.Count > 0;

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
            ? "Choose Pick PDF point 1 or Pick PDF point 2, then click the captured document image."
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
        OverlayStatusText = "Captured the visible PDF view. Pick the two document points, then create the 70% transparent overlay.";
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
        OverlayStatusText = "PDF point captured. Create Overlay will be available once both PDF picks and both matching JAD2001 coordinates are valid.";
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

    private bool CanCreateOverlay()
    {
        return !isCreatingOverlay
            && overlayPickerImageSource is not null
            && documentImagePoint1 is not null
            && documentImagePoint2 is not null
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
                new MapGeoreferenceCoordinatePoint(map2.Easting, map2.Northing)).ConfigureAwait(true);
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
            var result = await overlayService.TryRestorePersistedOverlayAsync(TransactionNumber).ConfigureAwait(true);
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
            OverlayStatusText = $"A saved M-Geo overlay was found, but it could not be restored: {exception.Message}";
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
            "Continue only when the active ArcGIS Pro map is JAD2001 / EPSG:3448.";
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
        calculateDiagnosticsCommand.RaiseCanExecuteChanged();
        RefreshOverlayCommandState();
    }

    private void RefreshDocumentProperties()
    {
        NotifyPropertyChanged(nameof(Documents));
        NotifyPropertyChanged(nameof(GeoreferenceDocumentOptions));
        NotifyPropertyChanged(nameof(HasGeoreferenceDocumentOptions));
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
