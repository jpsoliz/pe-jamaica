using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using ArcGIS.Desktop.Framework.Controls;
using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn;

public partial class TransactionDocumentsWindow : ProWindow
{
    private readonly CaseFolderLayout layout;
    private readonly ObservableCollection<TransactionDocumentRow> sourceDocuments = new();
    private readonly ObservableCollection<TransactionDocumentRow> outputDocuments = new();

    public TransactionDocumentsWindow(string transactionNumber, CaseFolderLayout layout)
    {
        InitializeComponent();
        this.layout = layout;
        TransactionText.Text = transactionNumber;
        FolderText.Text = layout.RootDirectory;
        SourceDocumentsList.ItemsSource = sourceDocuments;
        OutputDocumentsList.ItemsSource = outputDocuments;
        LoadDocuments();
    }

    private void LoadDocuments()
    {
        sourceDocuments.Clear();
        outputDocuments.Clear();

        LoadDirectoryDocuments(layout.SourceDirectory, sourceDocuments, SearchOption.TopDirectoryOnly);
        LoadDirectoryDocuments(layout.OutputDirectory, outputDocuments, SearchOption.AllDirectories);

        StatusText.Text = sourceDocuments.Count == 0 && outputDocuments.Count == 0
            ? "No local transaction documents found yet."
            : $"Source: {sourceDocuments.Count}. Output: {outputDocuments.Count}.";
    }

    private void LoadDirectoryDocuments(
        string directory,
        ObservableCollection<TransactionDocumentRow> target,
        SearchOption searchOption)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*", searchOption)
                     .OrderBy(path => GetRelativePath(path), StringComparer.OrdinalIgnoreCase))
        {
            var info = new FileInfo(path);
            target.Add(new TransactionDocumentRow(
                info.Name,
                string.IsNullOrWhiteSpace(info.Extension) ? "(none)" : info.Extension,
                FormatSize(info.Length),
                GetRelativePath(info.FullName),
                info.FullName));
        }
    }

    private void OpenButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (GetSelectedDocument() is not TransactionDocumentRow row)
        {
            StatusText.Text = "Select a document to open.";
            return;
        }

        TryOpen(row.Path, "Could not open the selected document.");
    }

    private void RefreshButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        LoadDocuments();
    }

    private void FolderButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var selectedPath = GetSelectedDocument()?.Path;
        var folder = !string.IsNullOrWhiteSpace(selectedPath)
            ? Path.GetDirectoryName(selectedPath)
            : GetSelectedFolder();

        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusText.Text = "No local folder is available for this view.";
            return;
        }

        TryOpen(folder, "Could not open the selected folder.");
    }

    private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }

    private void TryOpen(string path, string failureMessage)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or IOException)
        {
            StatusText.Text = failureMessage;
        }
    }

    private TransactionDocumentRow? GetSelectedDocument()
    {
        return DocumentsTabs.SelectedItem == OutputTab
            ? OutputDocumentsList.SelectedItem as TransactionDocumentRow
            : SourceDocumentsList.SelectedItem as TransactionDocumentRow;
    }

    private string? GetSelectedFolder()
    {
        return DocumentsTabs.SelectedItem == OutputTab
            ? Directory.Exists(layout.OutputDirectory) ? layout.OutputDirectory : null
            : Directory.Exists(layout.SourceDirectory) ? layout.SourceDirectory : null;
    }

    private string GetRelativePath(string path)
    {
        try
        {
            return Path.GetRelativePath(layout.RootDirectory, path);
        }
        catch (ArgumentException)
        {
            return path;
        }
    }

    private static string FormatSize(long bytes)
    {
        return bytes < 1024
            ? $"{bytes} B"
            : $"{bytes / 1024d:0.0} KB";
    }

    private sealed record TransactionDocumentRow(
        string FileName,
        string FileType,
        string DisplaySize,
        string RelativePath,
        string Path);
}
