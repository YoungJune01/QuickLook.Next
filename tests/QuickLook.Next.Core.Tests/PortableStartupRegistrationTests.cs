using QuickLook.Next.Core;
using Xunit;

namespace QuickLook.Next.Core.Tests;

public sealed class PortableStartupRegistrationTests
{
    [Fact]
    public void Startup_entry_belongs_to_one_location_and_never_the_installed_copy()
    {
        var first = new PortableStartupRegistration(@"C:\portable\QuickLook", @"C:\Windows\System32");
        var same = new PortableStartupRegistration(@"c:\PORTABLE\QuickLook\", @"C:\Windows\System32");
        var moved = new PortableStartupRegistration(@"D:\QuickLook", @"C:\Windows\System32");
        Assert.Equal(first.ValueName, same.ValueName);
        Assert.NotEqual(first.ValueName, moved.ValueName);
        Assert.StartsWith("QuickLookNext.Portable.", first.ValueName);
        Assert.NotEqual("QuickLookNext", first.ValueName);
        Assert.True(first.OwnsCommand(first.Command));
        Assert.False(first.OwnsCommand(moved.Command));
        Assert.False(first.OwnsCommand(first.Command + " -unexpected"));
        Assert.False(first.OwnsCommand(null));
    }

    [Fact]
    public void Launcher_path_is_a_quoted_file_argument_including_shell_metacharacters()
    {
        var registration = new PortableStartupRegistration(@"C:\测试 & space\O'Brien %PATH% $copy", @"C:\Windows\System32");
        Assert.EndsWith("-File \"" + @"C:\测试 & space\O'Brien %PATH% $copy\Start.ps1" + "\"", registration.Command);
        Assert.StartsWith("\"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\" ", registration.Command);
        Assert.DoesNotContain("-Command", registration.Command);
    }

    [Fact]
    public void Startup_requires_the_marker_launcher_app_and_bundled_runtime()
    {
        string root = Path.Combine(Path.GetTempPath(), "quicklook-startup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var registration = new PortableStartupRegistration(root, Environment.SystemDirectory);
            foreach (string file in new[] { "Start.ps1", "portable.mode", @"App\QuickLook.Next.App.exe", @"Runtime\dotnet.exe" })
            {
                Assert.False(registration.IsComplete);
                string path = Path.Combine(root, file);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "fixture");
            }
            Assert.True(registration.IsComplete);
            File.Delete(Path.Combine(root, "Start.ps1"));
            Assert.False(registration.IsComplete);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
