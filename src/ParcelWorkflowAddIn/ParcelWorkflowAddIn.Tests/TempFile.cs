namespace ParcelWorkflowAddIn.Tests;

internal sealed class TempFile : IDisposable
{
    public TempFile()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pe-jamaica-{Guid.NewGuid():N}.json");
    }

    public string Path { get; }

    public static TempFile FromExisting(string sourcePath)
    {
        var tempFile = new TempFile();
        File.Copy(sourcePath, tempFile.Path, overwrite: true);
        return tempFile;
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
