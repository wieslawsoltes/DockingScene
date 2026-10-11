using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Layout;

namespace DockingScene.PackageValidation;

public static class ControlConsumer
{
    public static DockSite Create(DockManager manager) => new()
    {
        Manager = manager,
        DisposeContentOnUnregister = false,
        DocumentTabOverflowBehavior = TabOverflowBehavior.Menu,
        ToolWindowsSingleTabLayoutBehavior = SingleTabLayoutBehavior.Hide,
        HasDocumentTabPinButtons = true,
        ContentFactory = window => new Microsoft.UI.Xaml.Controls.TextBox { AcceptsReturn = true, Text = window.Title }
    };
    public static AdvancedTabControl CreateStandalone()
    {
        var tabs = new AdvancedTabControl { OverflowBehavior = TabOverflowBehavior.ShrinkWithMenu, MinTabExtent = 60, MaxTabExtent = 200, HasTabPinButtons = true };
        tabs.Items.Add(new() { Id = "preview", Header = "Preview", IsPreview = true, ContentFactory = () => new Microsoft.UI.Xaml.Controls.TextBox() });
        tabs.CanTabsDrag = true;
        tabs.TabReordering += (_, e) => e.Cancel = e.Item.IsModified;
        tabs.TabReordered += (_, e) => System.Console.WriteLine($"Moved {e.Item.Id} from {e.OldIndex} to {e.NewIndex}");
        tabs.TabClosing += (_, e) => e.Cancel = e.Item.IsModified;
        tabs.MenuOpening += (_, e) => e.Menu.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Custom entry" });
        return tabs;
    }
}
