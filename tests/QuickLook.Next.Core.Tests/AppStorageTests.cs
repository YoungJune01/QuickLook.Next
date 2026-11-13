using QuickLook.Next.Core;
using Xunit;

namespace QuickLook.Next.Core.Tests;

public sealed class AppStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "quicklook-portable-测试 & space-" + Guid.NewGuid().ToString("N"));

    public AppStorageTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("")]
    [InlineData("App")]
    [InlineData("App/ParserHost")]
    [InlineData("App/RasterHost")]
    [InlineData("ParserHost")]
    [InlineData("app/rasterhost")]
    public void Portable_marker_resolves_the_same_data_root_from_each_deployed_process(string suffix)
    {
        File.WriteAllText(Path.Combine(_root, AppStorage.PortableMarkerName), "");
        string baseDirectory = Path.Combine(_root, suffix.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(baseDirectory);
        AppStorage storage = AppStorage.Resolve(baseDirectory, "unused-profile", "unused-temp");
        Assert.Equal(_root, storage.PortableRoot);
        foreach (string directory in new[] { storage.SettingsDirectory, storage.StateDirectory,
            storage.LogDirectory, storage.PdfCacheDirectory, storage.TemporaryDirectory,
            storage.ParserWorkDirectory, storage.ShellWorkDirectory })
        {
            Assert.StartsWith(Path.Combine(_root, "Data") + Path.DirectorySeparatorChar, directory);
            storage.EnsureDirectory(directory);
            Assert.True(Directory.Exists(directory));
        }
    }

    [Fact]
    public void Nonportable_deployments_preserve_existing_profile_and_temp_locations()
    {
        string profile = Path.Combine(_root, "profile");
        string temp = Path.Combine(_root, "temp");
        AppStorage storage = AppStorage.Resolve(_root, profile, temp);
        Assert.False(storage.IsPortable);
        Assert.Equal(Path.Combine(profile, "QuickLook.Next"), storage.SettingsDirectory);
        Assert.Equal(Path.Combine(profile, "QuickLookNext"), storage.StateDirectory);
        Assert.Equal(Path.Combine(profile, "QuickLookNext", "logs"), storage.LogDirectory);
        Assert.Equal(Path.Combine(profile, "QuickLook.Next", "pdf-page-cache"), storage.PdfCacheDirectory);
        Assert.Equal(temp, storage.TemporaryDirectory);
    }

    [Fact]
    public void An_unrelated_ancestor_marker_does_not_redirect_normal_storage()
    {
        File.WriteAllText(Path.Combine(_root, AppStorage.PortableMarkerName), "");
        string unrelated = Path.Combine(_root, "unrelated", "App");
        Directory.CreateDirectory(unrelated);
        Assert.False(AppStorage.Resolve(unrelated, _root, _root).IsPortable);
    }

    [Fact]
    public void Portable_storage_rejects_escape_and_unwritable_data_without_profile_fallback()
    {
        File.WriteAllText(Path.Combine(_root, AppStorage.PortableMarkerName), "");
        string profile = Path.Combine(_root, "profile-must-not-be-created");
        AppStorage storage = AppStorage.Resolve(_root, profile, _root);
        Assert.Throws<IOException>(() => storage.EnsureDirectory(Path.Combine(_root, "..", "outside")));
        File.WriteAllText(Path.Combine(_root, "Data"), "blocking file");
        Assert.Throws<IOException>(() => storage.EnsureDirectory(storage.LogDirectory));
        Assert.False(Directory.Exists(profile));
    }

    [Fact]
    public void Application_location_must_be_absolute()
        => Assert.Throws<ArgumentException>(() => AppStorage.Resolve("relative", _root, _root));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }
}
