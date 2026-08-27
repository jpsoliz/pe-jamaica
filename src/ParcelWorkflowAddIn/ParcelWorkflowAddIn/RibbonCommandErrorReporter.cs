using System.Windows;

namespace ParcelWorkflowAddIn;

internal static class RibbonCommandErrorReporter
{
    public static void Show(string title, Exception exception)
    {
        MessageBox.Show(
            $"{title} could not be opened.\n\n{exception.Message}",
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
