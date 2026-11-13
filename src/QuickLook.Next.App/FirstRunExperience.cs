using QuickLook.Next.Core;

namespace QuickLook.Next.App;

internal static class FirstRunExperience
{
    private static readonly string MarkerPath = Path.Combine(
        AppStorage.Current.StateDirectory,
        "welcome-shown-v1");

    public static bool ShouldShow => !File.Exists(MarkerPath);

    public static void MarkShown()
    {
        try
        {
            AppStorage.Current.EnsureDirectory(Path.GetDirectoryName(MarkerPath)!);
            File.WriteAllText(MarkerPath, DateTimeOffset.UtcNow.ToString("O"));
        }
        catch
        {
            // A failed marker only causes the welcome page to appear again.
        }
    }
}
