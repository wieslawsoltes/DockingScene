using System.Text.Json;
using DockingScene.Controls;
using DockingScene.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private StandardMdiControlBar? _mdiExternalBar;
    private void EnsureMdiControlBar()
    {
        if (_mdiExternalBar is not null) return;
        _mdiExternalBar = new StandardMdiControlBar { AutomationIdPrefix = "mdi-primary", ShowOnlyWhenMaximized = false,
            Width = 340, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(_mdiExternalBar,0); _root.Children.Add(_mdiExternalBar);
        _site.VisualsUpdated += RefreshMdiControlBar;
        RefreshMdiControlBar();
    }
    private void RefreshMdiControlBar()
    {
        if (_mdiExternalBar is not null) _mdiExternalBar.Host = _site.Manager.Layout.MdiMode == MdiMode.Standard ? _site.PrimaryMdiHost : null;
    }
    private void AddMdiReviewCommands(MenuFlyout menu)
    {
        EnsureMdiControlBar();
        void Item(string text, string id, Action action, bool enabled = true)
        {
            var item = DockChrome.Identify(new MenuFlyoutItem { Text = text, FontSize = 12, MinHeight = 26, IsEnabled = enabled }, "mdi-option:" + id);
            item.Click += (_, _) => action(); menu.Items.Add(item);
        }
        bool standard = _site.Manager.Layout.MdiMode == MdiMode.Standard;
        menu.Items.Add(new MenuFlyoutSeparator());
        Item("Arrange minimized windows", "arrange", () => _site.PrimaryMdiHost?.ArrangeMinimizedWindows(), standard);
        Item("Minimize all MDI documents", "minimize-all", () =>
        {
            var manager = _site.Manager;
            var ids = _site.PrimaryMdiHost?.GetDocuments().Select(w => w.Id).ToArray() ?? [];
            Execute(manager.Transaction("Minimize all MDI documents", ids.Select(id => new Func<DockManager, DockResult>(m => m.SetMdiState(id, MdiWindowState.Minimized)))));
        }, standard && _site.CanStandardMdiWindowsMinimize);
        Item("Toggle MDI maximize buttons", "toggle-maximize", () => _site.CanStandardMdiWindowsMaximize = !_site.CanStandardMdiWindowsMaximize);
        Item("Toggle MDI minimize buttons", "toggle-minimize", () => _site.CanStandardMdiWindowsMinimize = !_site.CanStandardMdiWindowsMinimize);
        Item("Maximize active MDI document programmatically", "maximize-active", () =>
        {
            if (_site.Manager.Layout.ActiveWindowId is { } id) Execute(_site.Manager.SetMdiState(id, MdiWindowState.Maximized));
        }, standard);
        Item("Hide maximized window frames", "hide-frames", () => { if (_site.PrimaryMdiHost is { } host) host.AreMaximizedWindowFramesVisible = false; }, standard);
        Item("Show maximized window frames", "show-frames", () => { if (_site.PrimaryMdiHost is { } host) host.AreMaximizedWindowFramesVisible = true; }, standard);
    }
    private void WriteMdiDiagnostics(Utf8JsonWriter writer)
    {
        var current = _site.PrimaryMdiHost;
        writer.WriteBoolean("mdiCanMaximize", _site.CanStandardMdiWindowsMaximize);
        writer.WriteBoolean("mdiCanMinimize", _site.CanStandardMdiWindowsMinimize);
        writer.WriteString("mdiPrimaryWindowId", current?.PrimaryWindow?.Id);
        writer.WriteBoolean("mdiPrimaryRestoreEnabled", current?.RestorePrimaryWindowCommand.CanExecute(null) == true);
        writer.WriteBoolean("mdiPrimaryCloseEnabled", current?.ClosePrimaryWindowCommand.CanExecute(null) == true);
        writer.WriteNumber("mdiMinimizedLayoutBuilds", current?.MinimizedLayoutBuildCount ?? 0);
        writer.WriteNumber("mdiMinimizedLayoutReuses", current?.MinimizedLayoutReuseCount ?? 0);
        writer.WriteNumber("mdiExternalPresentationUpdates", _mdiExternalBar?.PresentationUpdateCount ?? 0);
        writer.WriteStartObject("mdiExternalButtons");
        if (_mdiExternalBar is not null) foreach (var (key, identity) in _mdiExternalBar.ButtonIdentities) writer.WriteNumber(key,identity);
        writer.WriteEndObject();
        writer.WriteStartObject("mdiFrames");
        if (_site.Manager.Layout.MdiMode == MdiMode.Standard && current is { } host)
        {
            foreach (var (id, frame) in host.Windows)
            {
                writer.WriteStartObject(id);
                writer.WriteNumber("x", frame.Bounds.X); writer.WriteNumber("y", frame.Bounds.Y);
                writer.WriteNumber("width", frame.Bounds.Width); writer.WriteNumber("height", frame.Bounds.Height);
                writer.WriteBoolean("frameVisible", frame.IsFrameVisible);
                writer.WriteBoolean("moving", frame.IsMoving); writer.WriteNumber("z", Canvas.GetZIndex(frame));
                writer.WriteStartObject("buttons"); foreach (var (key, identity) in frame.CaptionButtonIdentities) writer.WriteNumber(key, identity); writer.WriteEndObject();
                writer.WriteEndObject();
            }
        }
        writer.WriteEndObject();
    }
}
