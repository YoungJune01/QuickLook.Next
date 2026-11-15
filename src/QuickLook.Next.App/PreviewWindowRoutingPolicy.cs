namespace QuickLook.Next.App;

internal readonly record struct PreviewWindowSlot(bool Active, bool Pinned, bool Foreground, string? Path, long ActivationOrder);

internal sealed class PreviewInputEpoch
{
    private long _generation;
    public long Capture() => Interlocked.Read(ref _generation);
    public bool IsCurrent(long generation) => generation == Capture();
    public void Invalidate() => Interlocked.Increment(ref _generation);
}

internal sealed class PreviewVisibilityState
{
    private bool? _published;
    public bool Update(bool active, bool invalidateInput)
    {
        if (_published == active && !invalidateInput)
            return false;
        _published = active;
        return true;
    }
}

internal static class PreviewWindowRoutingPolicy
{
    public const int MaximumWindows = 3;
    public const int Ignore = -1;
    public const int Create = -2;
    public const int AtCapacity = -3;

    public static int SelectOpen(IReadOnlyList<PreviewWindowSlot> slots, string path)
    {
        // Explorer Space must never toggle or reactivate an unfocused ordinary preview.
        int reusable = SelectTarget(slots, allowForegroundPinned: false);
        if (reusable >= 0)
            return string.Equals(slots[reusable].Path, path, StringComparison.OrdinalIgnoreCase) ? Ignore : reusable;
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].Active && string.Equals(slots[i].Path, path, StringComparison.OrdinalIgnoreCase))
                return i;
        for (int i = 0; i < slots.Count; i++)
            if (!slots[i].Active)
                return i;
        return slots.Count < MaximumWindows ? Create : AtCapacity;
    }

    public static int SelectTarget(IReadOnlyList<PreviewWindowSlot> slots, bool allowForegroundPinned)
    {
        int target = Ignore;
        for (int i = 0; i < slots.Count; i++)
        {
            PreviewWindowSlot slot = slots[i];
            if (!slot.Active)
                continue;
            if (slot.Foreground && (allowForegroundPinned || !slot.Pinned))
                return i;
            if (!slot.Pinned && (target < 0 || slot.ActivationOrder > slots[target].ActivationOrder))
                target = i;
        }
        return target;
    }
}
