using DockingScene.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private bool _factoryFailureRetained, _recursiveFactoryRejected;
    private int _factoryCallbackCount;
    private FrameworkElement FactoryReviewActions(AdvancedTabControl tabs)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        panel.Children.Add(DockChrome.Button("Reentrant factory", "review:reentrant", _theme, () =>
        {
            _factoryCallbackCount = 0;
            var target = new AdvancedTabItem { Id = "callback-target", Header = "Callback target", Content = DockChrome.Identify(new TextBox { Text = "Selected by the factory" }, "review:target-content") };
            var source = new AdvancedTabItem { Id = "callback-source", Header = "Callback source" };
            source.ContentFactory = () => { _factoryCallbackCount++; tabs.Select(target.Id); return new TextBlock { Text = "Obsolete outer request" }; };
            tabs.Update([source, target], target.Id, _theme, Core.TabPlacement.Top, true);
            tabs.Select(source.Id);
        }));
        panel.Children.Add(DockChrome.Button("Failed factory", "review:failure", _theme, () =>
        {
            var target = new AdvancedTabItem { Id = "failure-target", Header = "Retained", Content = DockChrome.Identify(new TextBox { Text = "Retained after failed factory" }, "review:retained-content") };
            var source = new AdvancedTabItem { Id = "failure-source", Header = "Rejected", ContentFactory = () => throw new InvalidOperationException("Deliberate sample factory failure") };
            tabs.Update([source, target], target.Id, _theme, Core.TabPlacement.Top, true);
            try { tabs.Select(source.Id); _factoryFailureRetained = false; }
            catch (InvalidOperationException) { _factoryFailureRetained = tabs.SelectedId == target.Id; }
        }));
        panel.Children.Add(DockChrome.Button("Recursive factory", "review:recursive", _theme, () =>
        {
            var item = new AdvancedTabItem(); item.ContentFactory = () => { item.EnsureContent(); return null; };
            try { item.EnsureContent(); _recursiveFactoryRejected = false; }
            catch (InvalidOperationException) { _recursiveFactoryRejected = true; }
        }));
        return panel;
    }
}
