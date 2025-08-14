using System.Diagnostics.ContractsLight;

namespace Codex.Storage;

public record DiskObjectStorage(string Directory, bool OverwriteOnWrite = true) : IObjectStorage
{
    public static DiskObjectStorage Global { get; } = new DiskObjectStorage("");

    public string TempDirectory;
    public bool IsReadOnly { get; set; }
    public void Dispose()
    {
    }

    public void Finalize(string message)
    {
    }

    public void Initialize()
    {
        if (!string.IsNullOrEmpty(Directory) && !IsReadOnly)
        {
            TempDirectory = Path.Combine(Directory, ".tmp");
            System.IO.Directory.CreateDirectory(TempDirectory);
        }
    }

    public Stream Load(string relativePath)
    {
        var path = Path.Combine(Directory, relativePath);
        if (!File.Exists(path)) return null;

        return File.OpenRead(path);
    }

    public string Write(string relativePath, MemoryStream stream)
    {
        Contract.Assert(!IsReadOnly);

        stream.Position = 0;
        var path = Path.Combine(Directory, relativePath);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));
        var tmpPath = TempDirectory == null 
            ? path + Path.GetRandomFileName() + ".tmp"
            : Path.Combine(TempDirectory, Path.GetTempFileName());
        File.WriteAllBytes(tmpPath, stream.ToArray());

        int waitMs = 1000;
        int iterationCount = 5;
        var logger = SdkFeatures.GetGlobalLogger();
        for (int i = 1; i <= iterationCount; i++)
        {
            try
            {
                logger?.LogMessage($"Move: '{tmpPath}' -> '{path}'");
                File.Move(tmpPath, path, overwrite: OverwriteOnWrite);
                logger?.LogMessage($"Move: '{tmpPath}' -> '{path}' finished.");
                break;
            }
            catch (Exception ex) when (i != iterationCount)
            {
                logger?.LogExceptionError($"Move: '{tmpPath}' -> '{path}'", ex);
            }

            waitMs *= iterationCount;
            Thread.Sleep(waitMs);
        }

        return relativePath;
    }
}
