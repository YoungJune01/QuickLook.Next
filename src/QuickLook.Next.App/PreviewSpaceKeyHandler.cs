namespace QuickLook.Next.App;

internal static class PreviewSpaceKeyHandler
{
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const uint WM_SYSKEYDOWN = 0x0104;
    private const uint WM_SYSKEYUP = 0x0105;

    // Called only after the window hook has checked Space, modifiers and focus ownership.
    public static bool TryHandle(uint message, nint keyData, Action onSpace)
    {
        if (message is WM_KEYDOWN or WM_SYSKEYDOWN)
        {
            // Closing hides the window before key-up, which can go to Explorer or a XAML
            // child HWND. Use this message's previous-key-state bit, never a key-up latch
            // that can survive hiding/reopening. Holding the opening Space must not close it.
            bool repeat = (keyData.ToInt64() & (1L << 30)) != 0;
            if (!repeat)
                onSpace();
            return true;
        }

        return message is WM_KEYUP or WM_SYSKEYUP;
    }
}
