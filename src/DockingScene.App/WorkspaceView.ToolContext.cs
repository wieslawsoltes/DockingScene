using System.Runtime.CompilerServices;
using System.Text.Json;
using DockingScene.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private void AddToolTitleContextCommands(MenuFlyout menu)
    {
        ToolWindow? Explorer() => _contextWindows.TryGetValue("explorer", out var weak) && weak.TryGetTarget(out var window) ? window as ToolWindow : null;
        void Add(string text, string id, Action action)
        {
            var item = DockChrome.Identify(new MenuFlyoutItem { Text = text, FontSize = 12, MinHeight = 25 }, "tool-context:" + id);
            item.Click += (_, _) => action(); menu.Items.Add(item);
        }
        menu.Items.Add(new MenuFlyoutSeparator());
        Add("Show Explorer title controls", "show", () => { if (Explorer() is { } window) window.ToolWindowContainerTitleBarContextContentTemplate = (DataTemplate)_contextResources["EditableCaption"]; });
        Add("Clear Explorer title controls", "clear", () => { if (Explorer() is { } window) window.ToolWindowContainerTitleBarContextContentTemplate = null; });
        Add("Tool context before title", "near", () => _site.ToolWindowsTitleBarContextContentAlignment = ContextContentAlignment.Near);
        Add("Tool context after title", "far", () => _site.ToolWindowsTitleBarContextContentAlignment = ContextContentAlignment.Far);
    }
    private void WriteToolTitleContextDiagnostics(Utf8JsonWriter writer)
    {
        writer.WriteString("toolTitleContextAlignment", _site.ToolWindowsTitleBarContextContentAlignment.ToString());
        writer.WriteStartObject("toolTitleContexts");
        foreach (var (id, presenter) in _site.ToolTitleContexts)
        {
            writer.WriteStartObject(id);
            writer.WriteNumber("builds", presenter.TemplateBuildCount);
            writer.WriteNumber("instance", presenter.PresentedContent is { } content ? RuntimeHelpers.GetHashCode(content) : 0);
            writer.WriteString("description", _site.Manager.Layout.Windows.GetValueOrDefault(id)?.Description);
            if (presenter.PresentedContent is DependencyObject root)
            {
                var stack = new Stack<DependencyObject>(); stack.Push(root);
                while (stack.TryPop(out var node))
                {
                    if (node is TextBox note) writer.WriteString("note", note.Text);
                    if (node is ToggleButton toggle) writer.WriteBoolean("tracked", toggle.IsChecked == true);
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) stack.Push(VisualTreeHelper.GetChild(node, i));
                }
            }
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }
}
