using DockingScene.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace DockingScene.Controls;

public partial class DockSite
{
    private void CompletePointerInteraction(PointerRoutedEventArgs e)
    {
        string? clicked = !_dragging && _dragIds.Length == 1 ? _dragIds[0] : null;
        var owner = Manager;
        try { EndInteraction(e); }
        finally { if (!IsInteractionActive) ReleaseGestureKeyboard(); }
        // Releasing capture can change focus and finish pending pointer state.
        // Restore content only after that transition, not while the canvas still
        // owns the pointer. A true drag or a changed selection must not steal focus.
        if (clicked is null || !ReferenceEquals(owner, Manager) || Manager.Layout.ActiveWindowId != clicked) return;
        if (DockTree.GroupOf(Manager.Layout, clicked) is { } group && _groups.TryGetValue(group.Id, out var view))
        {
            bool focused = view.Tabs.FocusSelectedContent(FocusState.Pointer);
            TraceInput(focused ? "Content focus restored" : "Content focus unavailable", e);
        }
    }
}
