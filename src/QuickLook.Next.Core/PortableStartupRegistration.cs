using System.Security.Cryptography;
using System.Text;

namespace QuickLook.Next.Core;

/// <summary>A per-location, explicitly requested startup entry for a complete portable copy.</summary>
public sealed class PortableStartupRegistration
{
    public string ValueName { get; }
    public string Command { get; }
    public string LauncherPath { get; }
    public string Root { get; }

    public PortableStartupRegistration(string root, string systemDirectory)
    {
        if (!Path.IsPathFullyQualified(root) || !Path.IsPathFullyQualified(systemDirectory)
            || root.Contains('"') || systemDirectory.Contains('"'))
            throw new ArgumentException("Startup paths must be absolute and cannot contain quotes.");
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        LauncherPath = Path.Combine(Root, "Start.ps1");
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Root.ToUpperInvariant())));
        ValueName = "QuickLookNext.Portable." + hash;
        string powershell = Path.Combine(systemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        // -File is an argument, never interpolated into PowerShell or cmd source code.
        Command = $"\"{powershell}\" -NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{LauncherPath}\"";
    }

    public bool IsComplete => File.Exists(LauncherPath)
        && File.Exists(Path.Combine(Root, AppStorage.PortableMarkerName))
        && File.Exists(Path.Combine(Root, "App", "QuickLook.Next.App.exe"))
        && File.Exists(Path.Combine(Root, "Runtime", "dotnet.exe"));

    public bool OwnsCommand(object? value)
        => value is string command && string.Equals(command, Command, StringComparison.OrdinalIgnoreCase);
}
