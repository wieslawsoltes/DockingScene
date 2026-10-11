using DockingScene.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private Button? _reviewButton;
    private bool _reviewEnabled;
    public void EnableHostReview()
    {
        if (_reviewEnabled) return;
        _reviewEnabled = true;
        // Ignore obsolete instances after unregister/manager replacement, but do
        // not remove a newer same-id editor installed by a reentrant callback.
        _site.WindowContentReleased += (id, content) =>
        {
            if (_editors.TryGetValue(id, out var editor) && (!editor.TryGetTarget(out var target) || ReferenceEquals(target, content))) _editors.Remove(id);
            if (_contentRoots.TryGetValue(id, out var root) && (!root.TryGetTarget(out var visual) || ReferenceEquals(visual, content))) _contentRoots.Remove(id);
        };
        _site.VisualsUpdated += () => { if (_reviewButton?.Parent != _toolbar) AddReviewMenu(); };
        AddReviewMenu();
    }
    private void AddReviewMenu()
    {
        Button? button = null;
        button = DockChrome.Button("Interaction", "toolbar:Interaction", _theme, () =>
        {
            var menu = new MenuFlyout();
            void Item(string text, string id, Action action, bool enabled = true)
            {
                var item = DockChrome.Identify(new MenuFlyoutItem { Text = text, FontSize = 12, MinHeight = 25, IsEnabled = enabled }, "interaction:" + id);
                item.Click += (_, _) => action(); menu.Items.Add(item);
            }
            Item("Enable drag edge scrolling", "enable-scroll", () => _tabPlayground!.CanTabsAutoScroll = true, _tabPlayground is not null);
            Item("Disable drag edge scrolling", "disable-scroll", () => _tabPlayground!.CanTabsAutoScroll = false, _tabPlayground is not null);
            Item("Focus selected editor", "focus", () =>
            {
                var view = _site.TabControls.FirstOrDefault(pair => pair.Control.SelectedId == _site.Manager.Layout.ActiveWindowId).Control;
                view?.FocusSelectedContent();
            });
            menu.ShowAt(button!);
        });
        _reviewButton = button; _toolbar.Children.Add(button);
    }
}
