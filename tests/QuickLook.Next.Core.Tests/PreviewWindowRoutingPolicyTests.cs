using Xunit;

namespace QuickLook.Next.App;

public sealed class PreviewWindowRoutingPolicyTests
{
    private static PreviewWindowSlot Pinned(string path) => new(true, true, false, path, 1);

    [Fact]
    public void First_open_reuses_the_hidden_owner()
        => Assert.Equal(0, PreviewWindowRoutingPolicy.SelectOpen([default], "first.txt"));

    [Fact]
    public void Pinned_preview_opens_another_slot()
        => Assert.Equal(PreviewWindowRoutingPolicy.Create,
            PreviewWindowRoutingPolicy.SelectOpen([Pinned("first.txt")], "second.png"));

    [Fact]
    public void Three_pinned_windows_are_preserved_at_capacity()
        => Assert.Equal(PreviewWindowRoutingPolicy.AtCapacity,
            PreviewWindowRoutingPolicy.SelectOpen([Pinned("a"), Pinned("b"), Pinned("c")], "d"));

    [Fact]
    public void Closing_a_preview_releases_its_slot_without_allocating_a_fourth_window()
        => Assert.Equal(1,
            PreviewWindowRoutingPolicy.SelectOpen([Pinned("a"), default, Pinned("c")], "d"));

    [Fact]
    public void Reopening_a_pinned_file_selects_its_existing_window_even_at_capacity()
        => Assert.Equal(1,
            PreviewWindowRoutingPolicy.SelectOpen([Pinned("a"), Pinned("B"), Pinned("c")], "b"));

    [Fact]
    public void Explorer_space_does_not_toggle_or_reactivate_an_ordinary_background_preview()
        => Assert.Equal(PreviewWindowRoutingPolicy.Ignore,
            PreviewWindowRoutingPolicy.SelectOpen([Pinned("a"), new(true, false, false, "b", 2)], "b"));

    [Fact]
    public void Unpinning_at_capacity_allows_a_different_file_to_reuse_that_window()
        => Assert.Equal(1,
            PreviewWindowRoutingPolicy.SelectOpen([Pinned("a"), Pinned("b") with { Pinned = false }, Pinned("c")], "d"));

    [Fact]
    public void Switch_and_explorer_escape_never_target_pinned_windows()
        => Assert.Equal(PreviewWindowRoutingPolicy.Ignore,
            PreviewWindowRoutingPolicy.SelectTarget([Pinned("a"), Pinned("b") with { Foreground = true }], false));

    [Fact]
    public void Unpinning_makes_the_preview_available_for_selection_following()
        => Assert.Equal(1,
            PreviewWindowRoutingPolicy.SelectTarget([Pinned("a"), Pinned("b") with { Pinned = false }], false));

    [Fact]
    public void Multiple_unpinned_previews_follow_the_most_recently_activated_window()
        => Assert.Equal(1,
            PreviewWindowRoutingPolicy.SelectTarget([
                new(true, false, false, "a", 1), new(true, false, false, "b", 3),
                new(true, false, false, "c", 2)], false));

    [Fact]
    public void Foreground_commands_reach_only_the_foreground_pinned_preview()
        => Assert.Equal(1,
            PreviewWindowRoutingPolicy.SelectTarget([Pinned("a"), Pinned("b") with { Foreground = true }], true));

    [Fact]
    public void Hidden_windows_never_receive_commands()
        => Assert.Equal(PreviewWindowRoutingPolicy.Ignore,
            PreviewWindowRoutingPolicy.SelectTarget([default, default, default], true));

    [Fact]
    public void Closing_one_session_rejects_its_late_work_without_cancelling_another_preview()
    {
        var first = new PreviewSession();
        var second = new PreviewSession();
        PreviewSessionSnapshot old = first.Begin("a", PreviewNavigationSource.ExplorerOpen);
        PreviewSessionSnapshot other = second.Begin("b", PreviewNavigationSource.ExplorerOpen);
        first.BeginClose();
        first.Clear();
        first.CancelOperation();
        Assert.False(first.IsCurrent(old));
        Assert.True(second.IsCurrent(other));
        Assert.False(other.Token.IsCancellationRequested);
        second.CancelOperation();
    }

    [Fact]
    public void Reusing_a_closed_slot_cannot_accept_the_previous_sessions_completion()
    {
        var session = new PreviewSession();
        PreviewSessionSnapshot old = session.Begin("a", PreviewNavigationSource.ExplorerOpen);
        session.BeginClose();
        session.Clear();
        PreviewSessionSnapshot current = session.Begin("c", PreviewNavigationSource.ExplorerOpen);
        Assert.False(session.IsCurrent(old));
        Assert.True(session.IsCurrent(current));
        session.CancelOperation();
    }

    [Fact]
    public void Slot_transition_rejects_already_queued_input_even_if_another_pinned_window_remains()
    {
        var epoch = new PreviewInputEpoch();
        long queued = epoch.Capture();
        Assert.True(epoch.IsCurrent(queued));
        epoch.Invalidate();
        Assert.False(epoch.IsCurrent(queued));
        Assert.True(epoch.IsCurrent(epoch.Capture()));
    }

    [Fact]
    public void Ordinary_content_updates_do_not_invalidate_newer_explorer_selection_reads()
    {
        var state = new PreviewVisibilityState();
        Assert.True(state.Update(true, invalidateInput: false));
        Assert.False(state.Update(true, invalidateInput: false));
        Assert.True(state.Update(true, invalidateInput: true)); // One slot closed, another stays.
        Assert.False(state.Update(true, invalidateInput: false));
        Assert.True(state.Update(false, invalidateInput: false));
    }
}
