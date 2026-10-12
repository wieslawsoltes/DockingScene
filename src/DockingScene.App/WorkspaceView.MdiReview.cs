using System.Text.Json;
using DockingScene.Controls;
using DockingScene.Core;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private void AddMdiReviewCommands(MenuFlyout menu)
    {
        void Item(string text, string id, Action action, bool enabled = true)
        {
            var item = DockChrome.Identify(new MenuFlyoutItem { Text = text, FontSize = 12, MinHeight = 26, IsEnabled = enabled }, "mdi-option:" + id);
            item.Click += (_, _) => action(); menu.Items.Add(item);
        }
        bool standard = _site.Manager.Layout.MdiMode == MdiMode.Standard;
        menu.Items.Add(new MenuFlyoutSeparator());
        Item("Hide maximized MDI frames", "hide-frames", () => _site.AreStandardMdiMaximizedWindowFramesVisible = false);
        Item("Show maximized MDI frames", "show-frames", () => _site.AreStandardMdiMaximizedWindowFramesVisible = true);
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
    }
    private void WriteMdiDiagnostics(Utf8JsonWriter writer)
    {
        writer.WriteBoolean("mdiCanMaximize", _site.CanStandardMdiWindowsMaximize);
        writer.WriteBoolean("mdiCanMinimize", _site.CanStandardMdiWindowsMinimize);
        var primaryHost = _site.PrimaryMdiHost;
        writer.WriteBoolean("mdiMaximizedFramesVisible", _site.AreStandardMdiMaximizedWindowFramesVisible);
        writer.WriteString("mdiPrimaryWindowId", _site.Manager.Layout.MdiMode == MdiMode.Standard ? primaryHost?.PrimaryWindow?.Id : null);
        writer.WriteBoolean("mdiPrimaryRestoreEnabled", primaryHost?.RestorePrimaryWindowCommand.CanExecute(null) == true);
        writer.WriteBoolean("mdiPrimaryCloseEnabled", primaryHost?.ClosePrimaryWindowCommand.CanExecute(null) == true);
        writer.WriteNumber("mdiMinimizedLayoutBuilds", primaryHost?.MinimizedLayoutBuildCount ?? 0);
        writer.WriteNumber("mdiMinimizedLayoutReuses", primaryHost?.MinimizedLayoutReuseCount ?? 0);
        writer.WriteNumber("mdiExternalPresentationUpdates", _mdiControlBar?.PresentationUpdateCount ?? 0);
        writer.WriteStartObject("mdiExternalButtons");
        foreach (var (key, identity) in _mdiControlBar?.ButtonIdentities ?? new Dictionary<string, int>()) writer.WriteNumber(key, identity);
        writer.WriteEndObject();
        writer.WriteStartObject("mdiFrames");
        if (_site.Manager.Layout.MdiMode == MdiMode.Standard && _site.PrimaryMdiHost is { } host)
        {
            foreach (var (id, frame) in host.Windows)
            {
                writer.WriteStartObject(id);
                writer.WriteNumber("x", frame.Bounds.X); writer.WriteNumber("y", frame.Bounds.Y);
                writer.WriteNumber("width", frame.Bounds.Width); writer.WriteNumber("height", frame.Bounds.Height);
                writer.WriteBoolean("frameVisible", frame.IsFrameVisible);
                writer.WriteBoolean("moving", frame.IsMoving); writer.WriteBoolean("keyboardMoving", frame.IsKeyboardInteractionActive); writer.WriteString("keyboardMode", frame.KeyboardInteractionMode?.ToString()); writer.WriteNumber("z", Canvas.GetZIndex(frame));
                writer.WriteStartObject("buttons"); foreach (var (key, identity) in frame.CaptionButtonIdentities) writer.WriteNumber(key, identity); writer.WriteEndObject();
                writer.WriteEndObject();
            }
        }
        writer.WriteEndObject();
    }
}
