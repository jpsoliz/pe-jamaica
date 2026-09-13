using System.Windows;
using ParcelWorkflowAddIn.Compare;

namespace ParcelWorkflowAddIn;

public partial class CompareTitleSourceSelectionWindow : Window
{
    private CompareTitleSourceSelectionWindow(IReadOnlyList<CompareTitleSourceRecord> sources)
    {
        InitializeComponent();
        SourcesList.ItemsSource = sources;
        SourcesList.SelectedIndex = sources.Count > 0 ? 0 : -1;
    }

    public CompareTitleSourceRecord? SelectedSource { get; private set; }

    public static CompareTitleSourceRecord? ShowDialogFor(IReadOnlyList<CompareTitleSourceRecord> sources)
    {
        if (sources.Count == 0)
        {
            return null;
        }

        var window = new CompareTitleSourceSelectionWindow(sources);
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(item => item.IsActive);
        if (owner is not null && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }

        return window.ShowDialog() == true ? window.SelectedSource : null;
    }

    private void DownloadClick(object sender, RoutedEventArgs e)
    {
        SelectedSource = SourcesList.SelectedItem as CompareTitleSourceRecord;
        DialogResult = SelectedSource is not null;
    }
}
