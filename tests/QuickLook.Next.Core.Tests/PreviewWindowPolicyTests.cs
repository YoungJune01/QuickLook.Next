using Xunit;

namespace QuickLook.Next.App;

public sealed class PreviewWindowPolicyTests
{
    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)] // Explorer, desktop, another app or owned settings.
    [InlineData(true, true, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, true, false, false)]
    public void Space_close_requires_current_foreground_ownership(
        bool previewActive, bool modalDialogOpen, bool ownsForeground, bool expected)
        => Assert.Equal(expected,
            PreviewWindowPolicy.ShouldCloseFromSpace(previewActive, modalDialogOpen, ownsForeground));

    [Fact]
    public void Topmost_and_pin_are_independent_per_window_and_default_off()
    {
        var first = new PreviewWindowOptions { Pinned = true, AlwaysOnTop = true };
        var second = new PreviewWindowOptions();
        Assert.Equal(PreviewWindowLevel.AlwaysOnTop, first.Level);
        Assert.Equal(PreviewWindowLevel.Standard, second.Level);
        Assert.False(second.Pinned);
        first.Pinned = false;
        Assert.True(first.AlwaysOnTop);
    }

    [Fact]
    public void Closing_and_reusing_a_window_clears_its_temporary_options_only()
    {
        var first = new PreviewWindowOptions { Pinned = true, AlwaysOnTop = true };
        var second = new PreviewWindowOptions { Pinned = true, AlwaysOnTop = true };
        first.Reset();
        Assert.False(first.Pinned);
        Assert.False(first.AlwaysOnTop);
        Assert.Equal(PreviewWindowLevel.Standard, first.Level);
        Assert.True(second.Pinned);
        Assert.True(second.AlwaysOnTop);
    }

    [Fact]
    public void Focus_loss_closes_only_a_visible_inactive_preview_when_enabled()
        => Assert.True(
            PreviewWindowPolicy.ShouldCloseAfterDeactivation(
                closeOnFocusLoss: true,
                previewVisible: true,
                windowActivated: false,
                modalDialogOpen: false,
                ownedWindowOpen: false));

    [Theory]
    [InlineData(false, true, false, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, false, false, true)]
    public void Focus_loss_close_is_suppressed_for_nonterminal_activation_states(
        bool closeOnFocusLoss,
        bool previewVisible,
        bool windowActivated,
        bool modalDialogOpen,
        bool ownedWindowOpen)
        => Assert.False(
            PreviewWindowPolicy.ShouldCloseAfterDeactivation(
                closeOnFocusLoss,
                previewVisible,
                windowActivated,
                modalDialogOpen,
                ownedWindowOpen));
}
