using Xunit;

namespace QuickLook.Next.App;

public sealed class PreviewSpaceKeyHandlerTests
{
    [Theory]
    [InlineData(0x0100u)] // WM_KEYDOWN
    [InlineData(0x0104u)] // WM_SYSKEYDOWN
    public void ReopenedWindowClosesOnNextPressEvenWhenPreviousKeyUpWentElsewhere(uint down)
    {
        int closes = 0;
        for (int session = 0; session < 5; session++)
        {
            // Explorer opens the preview. Its HWND receives only the next Space down:
            // closing hides it synchronously, so key-up never returns to this handler.
            Assert.True(PreviewSpaceKeyHandler.TryHandle(down, 1, () => closes++));
            Assert.Equal(session + 1, closes);
        }
    }

    [Theory]
    [InlineData(0x0100u)]
    [InlineData(0x0104u)]
    public void HoldingOpeningSpaceAcrossFocusTransferDoesNotClosePreview(uint down)
    {
        int closes = 0;
        // The initial down was delivered to Explorer before it activated the preview.
        for (int repeat = 0; repeat < 5; repeat++)
            Assert.True(PreviewSpaceKeyHandler.TryHandle(down, new nint((1L << 30) | 1), () => closes++));
        Assert.Equal(0, closes);

        // Even if release goes elsewhere, the next physical press closes immediately.
        Assert.True(PreviewSpaceKeyHandler.TryHandle(down, 1, () => closes++));
        Assert.Equal(1, closes);
    }

    [Theory]
    [InlineData(0x0101u)] // WM_KEYUP
    [InlineData(0x0105u)] // WM_SYSKEYUP
    public void KeyUpIsConsumedWithoutClosing(uint up)
    {
        Assert.True(PreviewSpaceKeyHandler.TryHandle(up, new nint(0xC0000001L),
            () => Assert.Fail("Key-up must not close the preview.")));
    }

    [Theory]
    [InlineData(0x0102u)] // WM_CHAR
    [InlineData(0x0008u)] // WM_KILLFOCUS
    [InlineData(0x0006u)] // WM_ACTIVATE
    public void OtherMessagesPassThrough(uint message)
    {
        Assert.False(PreviewSpaceKeyHandler.TryHandle(message, 0,
            () => Assert.Fail("Unrelated messages must pass through.")));
    }
}
