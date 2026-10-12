using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace DockingScene.Controls;

public partial class DockComboBox
{
    // Retain original non-text events until the native popup creates containers.
    // No synthetic input, selection algorithm, timer or repeated dispatch.
    private readonly Queue<(KeyRoutedEventArgs Event, bool Down)> _openingKeys = new();
    private bool _drainingOpeningKeys;
    private long _openingGeneration;
    public int PendingOpeningKeyCount => _openingKeys.Count;
    public long DeferredOpeningKeyCount { get; private set; }

    private static bool IsOpeningKey(VirtualKey key) => key is
        VirtualKey.Home or VirtualKey.End or VirtualKey.Up or VirtualKey.Down or
        VirtualKey.PageUp or VirtualKey.PageDown or VirtualKey.Enter or VirtualKey.Escape;
    private bool HasOpeningContainer() => Items.Count == 0 ||
        ContainerFromIndex(Math.Max(0, SelectedIndex)) is Control { IsLoaded: true };

    private bool DeferOpeningKey(KeyRoutedEventArgs e, bool down)
    {
        if (_drainingOpeningKeys) return false;
        if (!IsOpeningKey(e.Key))
        {
            // A modifier/text interaction starts a different native operation;
            // never dispatch old navigation under a new modifier state.
            if (down) DiscardOpeningKeys();
            return false;
        }
        if (_openingKeys.Count == 0 && HasOpeningContainer()) return false;
        if (_openingKeys.Count >= 256)
        {
            // A broken application template must not retain unbounded input.
            DiscardOpeningKeys(); IsDropDownOpen = false; e.Handled = true; return true;
        }
        _openingKeys.Enqueue((e, down)); DeferredOpeningKeyCount++; e.Handled = true;
        if (InputDiagnosticsEnabled)
        {
            _navigationTrace.Enqueue($"deferred key={e.Key} down={down} pending={_openingKeys.Count}");
            while (_navigationTrace.Count > 24) _navigationTrace.Dequeue();
        }
        return true;
    }
    private void OnOpeningLayoutUpdated(object? sender, object e)
    {
        if (_drainingOpeningKeys || _forwarding || _openingKeys.Count == 0 || !HasOpeningContainer()) return;
        if (!IsEnabled || IsEditable || !IsLoaded || !IsDropDownOpen || XamlRoot is not { } root ||
            !ReferenceEquals(root.Content, _inputRoot)) { DiscardOpeningKeys(); return; }
        var focused = FocusManager.GetFocusedElement(root) as DependencyObject;
        bool ownsFocus = false;
        for (var node = focused; node is not null; node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, this) || ReferenceEquals(node, _inputScope) ||
                node is ComboBoxItem item && ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(item), this))
            { ownsFocus = true; break; }
        }
        if (!ownsFocus) { DiscardOpeningKeys(); return; }
        long generation = _openingGeneration;
        _drainingOpeningKeys = _forwarding = true;
        try
        {
            CompletePopupFocus();
            while (_openingKeys.Count > 0 && generation == _openingGeneration && IsDropDownOpen)
            {
                var (original, down) = _openingKeys.Dequeue();
                // Its original route completed with Handled=true. The native
                // method may now process the same event once, with real items.
                original.Handled = false;
                try
                {
                    if (down)
                    {
                        ForwardedKeyCount++; TraceNavigation("ready-before", original);
                        base.OnKeyDown(original); TraceNavigation("ready-after", original);
                    }
                    else base.OnKeyUp(original);
                }
                finally { original.Handled = true; }
            }
        }
        finally { _forwarding = _drainingOpeningKeys = false; }
    }
    private void DiscardOpeningKeys() { _openingGeneration++; _openingKeys.Clear(); }
}
