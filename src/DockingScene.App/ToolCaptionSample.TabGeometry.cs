using System.Runtime.CompilerServices;
using System.Text.Json;
using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace DockingScene.App;

public sealed partial class ToolCaptionSample
{
    private void AddGroupLayoutMenu(MenuFlyout menu)
    {
        var layout = DockChrome.Identify(new MenuFlyoutSubItem { Text = "Tab layout and content minimums", FontSize = 12 }, "caption-policy:layout");
        menu.Items.Add(layout);
        void Item(string title, string id, Action action)
        {
            var item = DockChrome.Identify(new MenuFlyoutItem { Text = title, FontSize = 12, MinHeight = 26 }, "caption-layout:" + id);
            item.Click += (_, _) => action(); layout.Items.Add(item);
        }
        Item("Side-by-side constrained tools", "horizontal", () => PrepareGroupLayout(DockSide.Right));
        Item("Stacked constrained tools", "vertical", () => PrepareGroupLayout(DockSide.Bottom));
        foreach (var placement in Enum.GetValues<TabPlacement>())
            Item("Explorer tabs: " + placement, placement.ToString(), () =>
            {
                if (DockTree.GroupOf(_manager.Layout, "cap-a") is { } group)
                    Check(_manager.SetTabPlacement(group.Id, placement));
            });
    }

    private void PrepareGroupLayout(DockSide side)
    {
        // Ordinary application commands, also usable interactively. The requested
        // 1% splitter is deliberately minimum-clamped, not a hardcoded pixel test.
        Reset();
        _site.ToolWindowsSingleTabLayoutBehavior = SingleTabLayoutBehavior.Show;
        Check(_manager.Transaction("ConstrainedTabLayout", [
            m => m.UpdateWindow("cap-a", w => w with { MinimumSize = new(120, 80) }),
            m => m.Dock("cap-b", "cap-a", side),
            m => m.SetTabPlacement(DockTree.GroupOf(m.Layout, "cap-a")!.Id, TabPlacement.Top),
            m => m.SetFloatingBounds(_toolHost, new(20, 40, 580, 340)),
            m => m.ResizeSplit(m.Layout.FloatingHosts.First(h => h.Id == _toolHost).Root.Id, .01),
            m => m.Activate("cap-a")
        ]));
    }

    private void WriteGroupLayoutDiagnostics(Utf8JsonWriter writer)
    {
        // Observe actual retained controls. Do not call Arrange/MeasureGroup or
        // warm caches here: browser assertions must observe application work only.
        writer.WriteStartObject("geometry");
        writer.WriteNumber("tabHeight", _theme.TabHeight);
        writer.WriteNumber("plans", _site.ActiveFloatingLayoutPlanCount);
        writer.WriteNumber("planBuilds", _site.FloatingLayoutPlanBuildCount);
        writer.WriteNumber("arrangements", _site.FloatingLayoutArrangeCount);
        if (XamlRoot?.Content is UIElement root && _site.FloatingWindows.TryGetValue(_toolHost, out var frame))
        {
            var origin = _site.TransformToVisual(root).TransformPoint(new Point(0, 0));
            writer.WriteNumber("frameX", origin.X + frame.Bounds.X);
            writer.WriteNumber("frameY", origin.Y + frame.Bounds.Y);
            writer.WriteBoolean("moving", frame.IsMoving);
            writer.WriteNumber("frameWidth", frame.Bounds.Width);
            writer.WriteNumber("frameHeight", frame.Bounds.Height);
        }
        writer.WriteStartObject("groups");
        var stack = new Stack<DependencyObject>(); stack.Push(_site);
        while (stack.TryPop(out var node))
        {
            if (node is DockContainer container && container.Group is { } group)
            {
                writer.WriteStartObject(group.Id);
                writer.WriteNumber("identity", RuntimeHelpers.GetHashCode(container));
                writer.WriteNumber("tabs", RuntimeHelpers.GetHashCode(container.Tabs));
                writer.WriteString("placement", container.Tabs.Placement.ToString());
                writer.WriteNumber("width", container.ActualWidth);
                writer.WriteNumber("height", container.ActualHeight);
                writer.WriteBoolean("stripVisible", container.Tabs.IsTabStripVisible);
                writer.WriteEndObject();
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) stack.Push(VisualTreeHelper.GetChild(node, i));
        }
        writer.WriteEndObject(); writer.WriteEndObject();
    }
}
