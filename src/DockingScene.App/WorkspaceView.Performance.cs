using System.Diagnostics;
using DockingScene.Controls;
using DockingScene.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private bool _vetoTabReorder;
    private int _tabReorderRequests, _tabReorders;
    private double _lastTabWorkloadMs;
    private DockSite? _ownershipSite;
    private int _contentCreations, _contentDisposals, _contentReleases;
    private object? _trackedContent;

    private StackPanel PerformanceActions(AdvancedTabControl tabs)
    {
        _vetoTabReorder = false; _tabReorderRequests = _tabReorders = 0;
        tabs.TabReordering += (_, e) => { _tabReorderRequests++; e.Cancel = _vetoTabReorder; };
        tabs.TabReordered += (_, _) => _tabReorders++;
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        void Button(string title, string id, Action action) => panel.Children.Add(DockChrome.Button(title, "perf:" + id, _theme, action));
        Button("2,000 tabs", "large", () => LoadPerformanceTabs(tabs, 2000));
        Button("20 tabs", "small", () => LoadPerformanceTabs(tabs, 18));
        Button("Select ×200", "select", () =>
        {
            var ids = tabs.Items.Where(i => !i.IsPinned && !i.IsPreview).Select(i => i.Id).ToArray();
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 200 && ids.Length > 0; i++) tabs.Select(ids[(i * 19) % ids.Length]);
            _lastTabWorkloadMs = watch.Elapsed.TotalMilliseconds;
        });
        Button("Rename", "rename", () => { if (tabs.Items.FirstOrDefault(i => i.Id == tabs.SelectedId) is { } item) item.Header += " updated"; });
        Button("Replace item", "replace", () =>
        {
            var old = tabs.Items.First(i => i.Id == tabs.SelectedId); int index = tabs.Items.IndexOf(old);
            tabs.Items[index] = MakePerformanceTab(old.Id, old.Header, old.IsPinned, old.IsPreview);
        });
        Button("Veto reorder", "veto", () => _vetoTabReorder = !_vetoTabReorder);
        Button("Toggle drag", "drag", () => tabs.CanTabsDrag = !tabs.CanTabsDrag);
        Button("Content lifetime", "lifetime", OpenOwnershipSample);
        return panel;
    }
    private AdvancedTabItem MakePerformanceTab(string id, string title, bool pinned = false, bool preview = false) => new()
    {
        Id = id, Header = title, IsPinned = pinned, IsPreview = preview,
        HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
        ContentFactory = () => DockChrome.Identify(new TextBox { AcceptsReturn = true, Text = title + "\nRetained performance sample", BorderThickness = new(0) }, "play-editor:" + id)
    };
    private void LoadPerformanceTabs(AdvancedTabControl tabs, int count)
    {
        var watch = Stopwatch.StartNew();
        var items = new List<AdvancedTabItem> { MakePerformanceTab("play-pinned", "Pinned", true) };
        for (int i = 0; i < count; i++) items.Add(MakePerformanceTab("play-" + i, "Document " + i));
        items.Add(MakePerformanceTab("play-preview", "Preview", preview: true));
        tabs.Update(items, "play-0", _theme, TabPlacement.Top, true);
        _lastTabWorkloadMs = watch.Elapsed.TotalMilliseconds;
    }
    private void OpenOwnershipSample()
    {
        if (_site.Manager.Layout.Windows.ContainsKey("content-lifetime")) Execute(_site.Manager.Open("content-lifetime"));
        else Execute(_site.Manager.Register(new("content-lifetime", "Content lifetime", WindowKind.Document)));
    }
    private FrameworkElement OwnershipSample()
    {
        _contentCreations = _contentDisposals = _contentReleases = 0;
        var manager = new DockManager();
        var site = new DockSite { Manager = manager, Theme = _theme }; _ownershipSite = site;
        site.ContentFactory = _ =>
        {
            _contentCreations++;
            return _trackedContent = DockChrome.Identify(new TrackedEditor(() => _contentDisposals++) { AcceptsReturn = true, Text = "Close/open retains me. Unregister releases me; disposal requires opt-in." }, "lifetime:editor");
        };
        site.WindowContentReleased += (_, _) => _contentReleases++;
        var root = new Grid { Padding = new(8), RowSpacing = 8 };
        root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 }; root.Children.Add(buttons); root.Children.Add(site); Grid.SetRow(site, 1);
        void Add(string text, string id, Action action) => buttons.Children.Add(DockChrome.Button(text, "lifetime:" + id, _theme, action));
        void Register() { if (!site.Manager.Layout.Windows.ContainsKey("owned")) site.Manager.Register(new("owned", "Owned content", WindowKind.Document)).ThrowIfFailed(); }
        Add("Register", "register", Register);
        Add("Close", "close", () => Execute(site.Manager.Close("owned")));
        Add("Open", "open", () => Execute(site.Manager.Open("owned")));
        Add("Unregister", "unregister", () => Execute(site.Manager.Unregister("owned")));
        Add("Toggle disposal ownership", "ownership", () => site.DisposeContentOnUnregister = !site.DisposeContentOnUnregister);
        Add("Replace manager", "manager", () => { site.Manager = new(); Register(); });
        Add("Unregister + register", "recreate", () => { site.Manager.Unregister("owned").ThrowIfFailed(); Register(); });
        Register(); return root;
    }
    private sealed class TrackedEditor(Action dispose) : TextBox, IDisposable
    {
        void IDisposable.Dispose() => dispose();
    }
}
