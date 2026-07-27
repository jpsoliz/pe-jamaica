using System.Windows;
using System.Windows.Controls;

namespace ParcelWorkflowAddIn;

public partial class SupportingDocumentsDockpane : UserControl
{
    public SupportingDocumentsDockpane()
    {
        SupportingDocumentsDiagnostics.Write("Supporting Documents lightweight dockpane constructor entered.");
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SupportingDocumentsDiagnostics.Write("Supporting Documents lightweight dockpane constructed.");
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is SupportingDocumentsDockpaneViewModel newViewModel)
        {
            SupportingDocumentsDiagnostics.Write($"Lightweight dockpane DataContext attached. Title: {newViewModel.SupportingDocumentsTabTitle}");
            newViewModel.ReloadActiveCaseFolder();
        }
        else if (e.NewValue is not null)
        {
            SupportingDocumentsDiagnostics.Write($"Unexpected lightweight dockpane DataContext type: {e.NewValue.GetType().FullName}");
        }
    }
}
