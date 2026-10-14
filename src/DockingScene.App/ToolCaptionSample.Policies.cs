using System.Text.Json;
using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Serialization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.App;

public sealed partial class ToolCaptionSample
{
    private string? _savedPolicyLayout;
    private void AddPolicyOptions(StackPanel toolbar)
    {
        var button = DockChrome.Button("Instance policies", "caption-sample:policies", _theme, () => { });
        toolbar.Children.Add(button);
        button.Click += (_, _) =>
        {
            var menu = new MenuFlyout();
            AddGroupLayoutMenu(menu);
            void Item(string text, string id, Action action)
            {
                var item = DockChrome.Identify(new MenuFlyoutItem { Text = text, FontSize = 12, MinHeight = 26 }, "caption-policy:" + id);
                item.Click += (_, _) => action(); menu.Items.Add(item);
            }
            void Policy(string title, string id, Func<DockWindow, bool?, DockWindow> change)
            {
                foreach (var (label, key, value) in new (string, string, bool?)[] { ("Inherit", "inherit", null), ("Show", "show", true), ("Hide", "hide", false) })
                    Item("Explorer " + title + ": " + label, id + "-" + key, () => Check(_manager.UpdateWindow("cap-a", w => change(w, value))));
            }
            Policy("title", "title", (w, value) => w with { HasTitleBar = value });
            Policy("buttons", "buttons", (w, value) => w with { HasCloseButton = value, HasOptionsButton = value, HasToggleAutoHideButton = value });
            Policy("close", "close", (w, value) => w with { HasCloseButton = value });
            Policy("options", "options", (w, value) => w with { HasOptionsButton = value });
            Policy("pin", "pin", (w, value) => w with { HasToggleAutoHideButton = value });
            Item("Toggle site title bars", "site-title", () => _site.ToolWindowsHaveTitleBars = !_site.ToolWindowsHaveTitleBars);
            Item("Join Properties to Explorer", "join", () => Check(_manager.Dock("cap-b", "cap-a", DockSide.Center)));
            Item("Toggle Explorer close / pin capabilities", "capabilities", () => Check(_manager.UpdateWindow("cap-a", w => w with { Capabilities = w.Capabilities ^ (DockCapabilities.Close | DockCapabilities.AutoHide | DockCapabilities.Dock) })));
            Item("Save policy layout", "save", () => _savedPolicyLayout = DockLayoutSerializer.Serialize(_manager.Layout));
            Item("Restore policy layout", "restore", () => { if (_savedPolicyLayout is not null) Check(DockLayoutSerializer.Restore(_manager, _savedPolicyLayout)); });
            menu.ShowAt(button);
        };
    }
    private void WritePolicyDiagnostics(Utf8JsonWriter writer)
    {
        // Read-only native state, not calls into the policy resolver or layout cache.
        writer.WriteStartObject("policies");
        writer.WriteBoolean("siteTitle", _site.ToolWindowsHaveTitleBars);
        writer.WriteBoolean("saved", _savedPolicyLayout is not null);
        writer.WriteNumber("titleHeight", _theme.TitleHeight);
        writer.WriteNumber("captures", _site.CapturedPointerCount);
        writer.WriteBoolean("keyboardCapture", _site.HasCapturedGestureKeyboardScope);
        writer.WriteStartObject("windows");
        void Nullable(string name, bool? value) { if (value.HasValue) writer.WriteBoolean(name, value.Value); else writer.WriteNull(name); }
        foreach (var (id, window) in _manager.Layout.Windows)
        {
            writer.WriteStartObject(id);
            Nullable("title", window.HasTitleBar); Nullable("close", window.HasCloseButton);
            Nullable("options", window.HasOptionsButton); Nullable("pin", window.HasToggleAutoHideButton);
            if (_editors.TryGetValue(id, out var editor)) writer.WriteString("text", editor.Text);
            writer.WriteEndObject();
        }
        writer.WriteEndObject(); writer.WriteStartObject("actions");
        var stack = new Stack<DependencyObject>(); stack.Push(_site);
        while (stack.TryPop(out var node))
        {
            if (node is Button action && AutomationProperties.GetAutomationId(action) is { } id && id.StartsWith("group:", StringComparison.Ordinal))
            {
                writer.WriteStartObject(id); writer.WriteBoolean("enabled", action.IsEnabled);
                writer.WriteBoolean("tabStop", action.IsTabStop); writer.WriteBoolean("visible", action.Visibility == Visibility.Visible); writer.WriteEndObject();
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) stack.Push(VisualTreeHelper.GetChild(node, i));
        }
        writer.WriteEndObject(); WriteGroupLayoutDiagnostics(writer); writer.WriteEndObject();
    }
}
