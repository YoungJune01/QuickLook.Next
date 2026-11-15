namespace QuickLook.Next.App;

internal enum PreviewWindowLevel
{
    Standard,
    AlwaysOnTop,
}

// These options belong to one window session and are deliberately never serialized.
internal sealed class PreviewWindowOptions
{
    public bool Pinned { get; set; }
    public bool AlwaysOnTop { get; set; }
    public PreviewWindowLevel Level => AlwaysOnTop ? PreviewWindowLevel.AlwaysOnTop : PreviewWindowLevel.Standard;

    public void Reset() => (Pinned, AlwaysOnTop) = (false, false);
}

internal static class PreviewWindowPolicy
{
    public static bool ShouldCloseFromSpace(bool previewActive, bool modalDialogOpen, bool ownsForeground)
        => previewActive && !modalDialogOpen && ownsForeground;

    public static bool ShouldCloseAfterDeactivation(
        bool closeOnFocusLoss,
        bool previewVisible,
        bool windowActivated,
        bool modalDialogOpen,
        bool ownedWindowOpen)
        => closeOnFocusLoss
            && previewVisible
            && !windowActivated
            && !modalDialogOpen
            && !ownedWindowOpen;
}
