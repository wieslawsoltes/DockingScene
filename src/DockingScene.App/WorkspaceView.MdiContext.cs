using System.Runtime.CompilerServices;
using System.Text.Json;
using DockingScene.Controls;
using DockingScene.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private readonly Dictionary<string, WeakReference<DockingWindow>> _contextWindows = new(StringComparer.Ordinal);
    private readonly MdiContextResources _contextResources = new();
    private MdiScopeSample? _mdiScopeSample;
    private void ConfigureMdiContextSample()
    {
        _site.WindowContentReleased += (id, _) => _contextWindows.Remove(id);
        _site.WindowCreated += window =>
        {
            window.DataContext = _models?.Find(window.Id);
            _contextWindows[window.Id] = new(window);
        };
    }
    private void AddMdiContextCommands(MenuFlyout menu)
    {
        void Item(string text, string id, Action action)
        {
            var item = DockChrome.Identify(new MenuFlyoutItem { Text = text, FontSize = 12, MinHeight = 26 }, "mdi-context:" + id);
            item.Click += (_, _) => action(); menu.Items.Add(item);
        }
        DockingWindow? Primary()
        {
            string? id = _site.PrimaryMdiHost?.PrimaryWindow?.Id;
            return id is not null && _contextWindows.TryGetValue(id, out var weak) && weak.TryGetTarget(out var window) ? window : null;
        }
        Item("Show title context controls", "show", () =>
        {
            if (Primary() is { } window) { window.DataContext = _models?.Find(window.Id); window.StandardMdiTitleBarContextContentTemplate = (DataTemplate)_contextResources["EditableCaption"]; }
        });
        Item("Show title status template", "status-template", () => { if (Primary() is { } window) window.StandardMdiTitleBarContextContentTemplate = (DataTemplate)_contextResources["StatusCaption"]; });
        Item("Clear title context", "clear", () => { if (Primary() is { } window) window.StandardMdiTitleBarContextContentTemplate = null; });
        Item("Use editor visual as context", "visual-data", () => { if (Primary() is { } window) window.DataContext = window.Content; });
        Item("Open independent MDI hosts sample", "scope-sample", () =>
        {
            if (_site.Manager.Layout.Windows.ContainsKey("mdi-host-scope")) Execute(_site.Manager.Open("mdi-host-scope"));
            else Execute(_site.Manager.Register(new("mdi-host-scope", "Independent MDI hosts", WindowKind.Document)));
            Execute(_site.Manager.SetMdiMode(MdiMode.Tabbed));
        });
    }
    private void WriteMdiContextDiagnostics(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("mdiContexts");
        if (_site.Manager.Layout.MdiMode == MdiMode.Standard && _site.PrimaryMdiHost is { } host)
        foreach (var (id, frame) in host.Windows)
        {
            var presenter = frame.TitleBarContextPresenter;
            writer.WriteStartObject(id);
            writer.WriteNumber("builds", presenter.TemplateBuildCount);
            writer.WriteNumber("instance", presenter.PresentedContent is { } content ? RuntimeHelpers.GetHashCode(content) : 0);
            writer.WriteBoolean("visualDataSuppressed", presenter.Context is UIElement && (presenter.PresentedContent as FrameworkElement)?.DataContext is null);
            if (presenter.PresentedContent is DependencyObject root)
            {
                var stack = new Stack<DependencyObject>(); stack.Push(root);
                while (stack.TryPop(out var item))
                {
                    if (item is ToggleButton toggle) writer.WriteBoolean("tracked", toggle.IsChecked == true);
                    if (item is TextBox note) writer.WriteString("note", note.Text);
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(item); i++) stack.Push(VisualTreeHelper.GetChild(item, i));
                }
            }
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
        if (_mdiScopeSample is { IsLoaded: true } sample) sample.WriteDiagnostics(writer);
    }
}
