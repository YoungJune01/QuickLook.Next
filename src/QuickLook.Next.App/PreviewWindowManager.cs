using System.Diagnostics;
using QuickLook.Next.Core;

namespace QuickLook.Next.App;

// UI-thread owner of the bounded window pool and the single native input producer.
internal sealed class PreviewWindowManager
{
    private readonly List<MainWindow> _windows = [];
    private readonly NativeBridge _native = new();
    private readonly PreviewInputEpoch _inputEpoch = new();
    private readonly PreviewVisibilityState _visibility = new();
    private long _activationOrder;
    private bool _exiting;
    private bool? _backgroundEfficiency;

    public MainWindow Owner { get; }
    public bool SettingsOpen => Owner.HasSettingsWindow;

    public PreviewWindowManager()
    {
        Owner = CreateWindow(ownsGlobalServices: true);
    }

    private MainWindow CreateWindow(bool ownsGlobalServices = false)
    {
        if (_windows.Count >= PreviewWindowRoutingPolicy.MaximumWindows)
            throw new InvalidOperationException("Preview window limit exceeded.");
        var window = new MainWindow(this, _native, ownsGlobalServices);
        _windows.Add(window);
        return window;
    }

    public void Dispatch(NativeIntent intent)
    {
        long receivedAt = Stopwatch.GetTimestamp();
        long epoch = _inputEpoch.Capture();
        Owner.DispatcherQueue.TryEnqueue(() =>
        {
            if (!_inputEpoch.IsCurrent(epoch))
                return;
            try { Route(intent, receivedAt); }
            catch (Exception ex) { DiagLog.Write("App", "preview routing failed: " + ex); }
        });
    }

    private void Route(NativeIntent intent, long receivedAt)
    {
        if (_exiting)
            return;
        PreviewWindowSlot[] slots = _windows.Select(window => window.RoutingSlot).ToArray();
        if (intent.Intent == PreviewIntent.Open && intent.PrimaryPath is { } path)
        {
            int target = PreviewWindowRoutingPolicy.SelectOpen(slots, path);
            if (target == PreviewWindowRoutingPolicy.Ignore)
                return;
            if (target == PreviewWindowRoutingPolicy.AtCapacity)
            {
                foreach (MainWindow pinnedWindow in _windows)
                    pinnedWindow.ShowPreviewLimitMessage();
                return;
            }
            if (target >= 0 && slots[target].Active)
            {
                if (slots[target].Pinned)
                    _windows[target].ActivateExistingPreview();
                else
                    _windows[target].ReceiveNativeIntent(new NativeIntent(PreviewIntent.Switch, intent.Paths), receivedAt);
                return;
            }
            MainWindow window;
            if (target == PreviewWindowRoutingPolicy.Create)
            {
                window = CreateWindow();
                // Per-window startup is synchronous; only the owner starts global services.
                window.StartPreviewServices();
            }
            else
            {
                window = _windows[target];
            }
            window.ReceiveNativeIntent(intent, receivedAt);
            return;
        }

        bool foregroundCommand = intent.Intent is PreviewIntent.Reload or PreviewIntent.Fullscreen
            or PreviewIntent.ZoomIn or PreviewIntent.ZoomOut;
        int selected = PreviewWindowRoutingPolicy.SelectTarget(slots, foregroundCommand);
        if (selected >= 0)
            _windows[selected].ReceiveNativeIntent(intent, receivedAt);
    }

    public void Activated(MainWindow window)
        => window.ActivationOrder = ++_activationOrder;

    public void RefreshVisibility(bool invalidateInput = false)
    {
        bool anyActive = _windows.Any(window => window.RoutingSlot.Active);
        if (_visibility.Update(anyActive, invalidateInput))
        {
            // Close/pin changes cancel queued input and in-flight selection reads. Ordinary
            // content updates must not cancel newer Explorer selection work in another slot.
            _native.SetPreviewVisible(anyActive);
            _inputEpoch.Invalidate();
        }
        if (_windows.Count < PreviewWindowRoutingPolicy.MaximumWindows
            || _windows.Any(window => !window.RoutingSlot.Active || !window.RoutingSlot.Pinned))
        {
            foreach (MainWindow window in _windows)
                window.DismissPreviewLimitMessage();
        }
        bool background = !anyActive;
        if (_backgroundEfficiency != background)
        {
            _backgroundEfficiency = background;
            ProcessPowerMode.SetCurrentBackgroundEfficiency(background, "App");
        }
    }

    public void SettingsChanged()
    {
        foreach (MainWindow window in _windows)
            window.ApplySettings();
    }

    public void Exit()
    {
        if (_exiting)
            return;
        _exiting = true;
        _native.Stop();
        foreach (MainWindow window in _windows)
        {
            try { window.StopPreviewServices(); }
            catch (Exception ex) { DiagLog.Write("App", "window shutdown failed: " + ex); }
        }
        Microsoft.UI.Xaml.Application.Current.Exit();
    }
}
