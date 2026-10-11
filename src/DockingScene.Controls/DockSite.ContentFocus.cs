using DockingScene.Core;
using Microsoft.UI.Xaml;

namespace DockingScene.Controls;

public partial class DockSite
{
    private void FocusClickedTabContent()
    {
        if (_dragging || _dragIds.Length != 1) return;
        if (DockTree.GroupOf(Manager.Layout, _dragIds[0]) is { } group && _groups.TryGetValue(group.Id, out var view))
            view.Tabs.FocusSelectedContent(FocusState.Pointer);
    }
}
