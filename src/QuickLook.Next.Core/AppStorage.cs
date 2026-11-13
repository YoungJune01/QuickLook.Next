namespace QuickLook.Next.Core;

/// <summary>Deployment-owned storage paths. Portable mode never falls back to the user profile.</summary>
public sealed class AppStorage
{
    public const string PortableMarkerName = "portable.mode";
    public static AppStorage Current { get; } = Resolve(
        AppContext.BaseDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Path.GetTempPath());

    public string? PortableRoot { get; }
    public bool IsPortable => PortableRoot is not null;
    public string SettingsDirectory { get; }
    public string StateDirectory { get; }
    public string LogDirectory { get; }
    public string PdfCacheDirectory { get; }
    public string TemporaryDirectory { get; }
    public string ParserWorkDirectory { get; }
    public string ShellWorkDirectory { get; }

    private AppStorage(string? portableRoot, string localData, string temporaryDirectory)
    {
        PortableRoot = portableRoot;
        string? data = portableRoot is null ? null : Path.Combine(portableRoot, "Data");
        SettingsDirectory = data is null ? Path.Combine(localData, "QuickLook.Next") : Path.Combine(data, "Settings");
        StateDirectory = data is null ? Path.Combine(localData, "QuickLookNext") : Path.Combine(data, "State");
        LogDirectory = data is null ? Path.Combine(localData, "QuickLookNext", "logs") : Path.Combine(data, "Logs");
        PdfCacheDirectory = data is null ? Path.Combine(localData, "QuickLook.Next", "pdf-page-cache") : Path.Combine(data, "Cache", "PDF");
        TemporaryDirectory = data is null ? temporaryDirectory : Path.Combine(data, "Temp");
        ParserWorkDirectory = data is null ? Path.Combine(localData, "QuickLookNext", "ParserHost", "work") : Path.Combine(data, "Work", "ParserHost");
        ShellWorkDirectory = data is null ? Path.Combine(localData, "QuickLookNext", "ShellBroker", "work") : Path.Combine(data, "Work", "ShellBroker");
    }

    public static AppStorage Resolve(string baseDirectory, string localData, string temporaryDirectory)
    {
        if (!Path.IsPathFullyQualified(baseDirectory))
            throw new ArgumentException("Application directory must be absolute.", nameof(baseDirectory));
        var directory = new DirectoryInfo(Path.GetFullPath(baseDirectory));
        // Only the documented App/ParserHost/RasterHost layout may inherit a parent's marker.
        // An unrelated ancestor marker must not redirect a normal installation's storage.
        for (int depth = 0; depth < 3; depth++)
        {
            if (File.Exists(Path.Combine(directory.FullName, PortableMarkerName)))
                return new AppStorage(directory.FullName, localData, temporaryDirectory);
            if ((!directory.Name.Equals("App", StringComparison.OrdinalIgnoreCase)
                && !directory.Name.Equals("ParserHost", StringComparison.OrdinalIgnoreCase)
                && !directory.Name.Equals("RasterHost", StringComparison.OrdinalIgnoreCase)) || directory.Parent is null)
                break;
            directory = directory.Parent;
        }
        return new AppStorage(null, localData, temporaryDirectory);
    }

    public void EnsureDirectory(string path)
    {
        if (PortableRoot is not null)
        {
            string prefix = Path.TrimEndingDirectorySeparator(PortableRoot) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Portable storage must remain within its deployment directory.");
            string current = PortableRoot;
            foreach (string component in fullPath[prefix.Length..].Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, component);
                if (Directory.Exists(current) || File.Exists(current))
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Portable storage cannot use a reparse point.");
                }
                Directory.CreateDirectory(current);
            }
            return;
        }
        Directory.CreateDirectory(path);
    }

    public void ConfigureProcessEnvironment()
    {
        if (!IsPortable)
            return;
        EnsureDirectory(TemporaryDirectory);
        // Fail before reading settings or starting hosts if Data was redirected externally.
        foreach (string directory in new[] { SettingsDirectory, StateDirectory, LogDirectory,
            PdfCacheDirectory, ParserWorkDirectory, ShellWorkDirectory })
            EnsureDirectory(directory);
        using (new FileStream(Path.Combine(TemporaryDirectory, Guid.NewGuid().ToString("N")),
            FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
        ConfigureTemporaryDirectory(TemporaryDirectory);
    }

    public static void ConfigureTemporaryDirectory(string directory)
    {
        if (!Path.IsPathFullyQualified(directory))
            throw new ArgumentException("Temporary directory must be absolute.", nameof(directory));
        Directory.CreateDirectory(directory);
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Temporary directory cannot be a reparse point.");
        Environment.SetEnvironmentVariable("TEMP", directory);
        Environment.SetEnvironmentVariable("TMP", directory);
    }
}
