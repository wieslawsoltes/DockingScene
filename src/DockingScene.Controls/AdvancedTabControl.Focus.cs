using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.Controls;

public partial class AdvancedTabControl
{
    private static readonly ConditionalWeakTable<AdvancedTabItem, WeakReference<Control>> ContentFocus = new();
    private uint? _contentFocusPointer;

    private void ConfigureContentFocus()
    {
        // Clicking blank editor space still requests selected-content focus. Handle
        // that request at the content boundary as well as at a tab header. An
        // already-focused descendant keeps its own caret and selection behavior.
        _presenter.AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            _contentFocusPointer = e.GetCurrentPoint(_presenter).Properties.IsLeftButtonPressed ? e.Pointer.PointerId : null;
            if (_contentFocusPointer is not null && RestorePointerContentFocus()) e.Handled = true;
        }), true);
        _presenter.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, e) =>
        {
            if (_contentFocusPointer != e.Pointer.PointerId) return;
            _contentFocusPointer = null;
            if (RestorePointerContentFocus()) e.Handled = true;
        }), true);
        _presenter.AddHandler(PointerCanceledEvent, new PointerEventHandler((_, _) => _contentFocusPointer = null), true);
        Unloaded += (_, _) => _contentFocusPointer = null;
        _presenter.GotFocus += (_, _) =>
        {
            if (_presenter.Content is not AdvancedTabItem item || XamlRoot is not { } root) return;
            if (FocusManager.GetFocusedElement(root) is Control focused && IsDescendant(focused, item))
            { ContentFocus.Remove(item); ContentFocus.Add(item, new(focused)); }
        };
    }

    private bool RestorePointerContentFocus()
    {
        if (_presenter.Content is not AdvancedTabItem item || XamlRoot is not { } root) return false;
        // A child callback may have deliberately opened a flyout or focused a
        // different pane. Repair missing focus, never undo that explicit transfer.
        if (FocusManager.GetFocusedElement(root) is DependencyObject focused && !IsDescendant(focused, item)) return false;
        return FocusSelectedContent(FocusState.Pointer);
    }

    /// <summary>Restore the selected content's last focused control, or focus its first
    /// eligible control. Only already-created selected content participates. This is
    /// synchronous so input following a tab click cannot fall into the old document.</summary>
    public bool FocusSelectedContent(FocusState state = FocusState.Programmatic)
    {
        if (state == FocusState.Unfocused || !Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        if (_presenter.Content is not AdvancedTabItem item || item.Id != _selectedId || XamlRoot is not { } root) return false;
        if (FocusManager.GetFocusedElement(root) is DependencyObject focused && IsDescendant(focused, item)) return true;
        // Materialize the selected presenter before requesting focus. A successful
        // Focus return alone is not proof that an unarranged/reparented template
        // has acquired keyboard focus; confirm the current root's actual owner.
        item.ApplyTemplate();
        item.UpdateLayout();
        if (!ReferenceEquals(_presenter.Content, item) || item.Id != _selectedId) return false;
        if (ContentFocus.TryGetValue(item, out var remembered) && remembered.TryGetTarget(out var control) && IsDescendant(control, item) && TryFocus(control)) return true;
        if (item.Content is Control direct && TryFocus(direct)) return true;
        var pending = new Stack<DependencyObject>(); pending.Push(item);
        while (pending.TryPop(out var node))
        {
            if (node is UIElement visual && visual.Visibility != Visibility.Visible) continue;
            if (!ReferenceEquals(node, item) && node is Control candidate && TryFocus(candidate)) return true;
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--) pending.Push(VisualTreeHelper.GetChild(node, i));
        }
        return false;

        bool TryFocus(Control candidate)
        {
            if (!candidate.IsEnabled || !candidate.IsTabStop || candidate.Visibility != Visibility.Visible || candidate.XamlRoot != root) return false;
            candidate.ApplyTemplate();
            _ = candidate.Focus(state);
            if (FocusManager.GetFocusedElement(root) is DependencyObject actual && IsDescendant(actual, item)) return true;
            candidate.UpdateLayout();
            if (!ReferenceEquals(_presenter.Content, item) || item.Id != _selectedId) return false;
            _ = candidate.Focus(state);
            return FocusManager.GetFocusedElement(root) is DependencyObject final && IsDescendant(final, item);
        }
    }

    private static bool IsDescendant(DependencyObject node, DependencyObject ancestor)
    {
        for (DependencyObject? current = node; current is not null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, ancestor)) return true;
        return false;
    }
}
