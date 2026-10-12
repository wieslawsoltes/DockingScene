using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DockingScene.Controls;
using DockingScene.Core;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private bool? _diagnosticsEnabled;
    private long _diagnosticSequence;
    private void PublishDiagnostics()
    {
#if BROWSER_WASM
        _diagnosticsEnabled ??= Uno.Foundation.WebAssemblyRuntime.InvokeJS("new URL(location.href).searchParams.get('diagnostics') === '1' ? 'true' : 'false'") == "true";
        if (_diagnosticsEnabled != true) { _diagnostics.Stop(); return; }
        try
        {
            // Publish model metadata and arranged hit targets from the same UI
            // projection. A current model with old caption bounds is not a valid
            // snapshot (e.g. immediately after restoring a maximized frame).
            _root.UpdateLayout();
            if (_site.IsVisualUpdatePending) return;
            var owner = _site.Manager;
            var layout = owner.Layout;
            var hitBounds = DockChrome.AutomationBounds(_root).ToArray();
            if (_site.IsVisualUpdatePending || !ReferenceEquals(owner, _site.Manager) ||
                !ReferenceEquals(layout, owner.Layout)) return;
            var buffer = new ArrayBufferWriter<byte>(); using var writer = new Utf8JsonWriter(buffer);
            _site.InputDiagnosticsEnabled = true;
            writer.WriteStartObject(); writer.WriteBoolean("ready", true); writer.WriteNumber("sequence", ++_diagnosticSequence);
            writer.WriteNumber("workspaceGeneration", _workspaceGeneration);
            writer.WriteBoolean("interactionActive", _site.IsInteractionActive);
            writer.WriteNumber("capturedPointerCount", _site.CapturedPointerCount);
            writer.WriteStartArray("inputEvents");
            foreach (var item in _site.InteractionLog) writer.WriteStringValue(item);
            writer.WriteEndArray();
            writer.WriteString("focusedElement", Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot)?.GetType().Name);
            writer.WriteString("version", "0.1.0"); writer.WriteString("renderer", "Uno Skia"); writer.WriteNumber("revision", layout.Revision);
            writer.WriteString("activeWindowId", layout.ActiveWindowId); writer.WriteString("mdiMode", layout.MdiMode.ToString()); writer.WriteString("theme", _theme.Name);
            writer.WriteBoolean("locked", layout.IsLayoutLocked); writer.WriteBoolean("canUndo", _site.Manager.CanUndo); writer.WriteBoolean("canRedo", _site.Manager.CanRedo);
            writer.WriteBoolean("modified", _modified); writer.WriteNumber("cachedContentCount", _site.CachedContentCount); writer.WriteString("lastMessage", _lastMessage);
            writer.WriteString("autoHideWindowId", _site.OpenAutoHideWindowId); WriteRect(writer, "autoHideBounds", _site.OpenAutoHideBounds);
            WriteTabDiagnostics(writer);
            WriteSelectorDiagnostics(writer);
            writer.WriteStartArray("windows");
            foreach (var window in layout.Windows.Values)
            {
                writer.WriteStartObject(); writer.WriteString("id", window.Id); writer.WriteString("title", window.Title); writer.WriteString("kind", window.Kind.ToString());
                writer.WriteBoolean("isModified", window.IsModified); writer.WriteBoolean("isReadOnly", window.IsReadOnly); writer.WriteNumber("autoHideSize", window.AutoHideSize);
                writer.WriteString("displayMode", window.DisplayMode.ToString()); writer.WriteString("placement", DockTree.Locate(layout, window.Id).Placement.ToString());
                WriteRect(writer, "mdiBounds", window.MdiBounds); writer.WriteString("mdiState", window.MdiState.ToString()); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteStartArray("groups");
            foreach (var group in DockTree.Groups(layout))
            {
                writer.WriteStartObject(); writer.WriteString("id", group.Id); writer.WriteString("kind", group.Kind.ToString()); WriteIds(writer, group.Windows);
                writer.WriteString("selectedId", group.SelectedId); writer.WriteString("tabPlacement", group.TabPlacement.ToString());
                if (_site.Geometry?.Nodes.TryGetValue(group.Id, out var bounds) == true) WriteRect(writer, "bounds", bounds);
                writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteStartArray("floatingHosts");
            foreach (var host in layout.FloatingHosts)
            {
                writer.WriteStartObject(); writer.WriteString("id", host.Id); WriteRect(writer, "bounds", host.Bounds); writer.WriteBoolean("isMaximized", host.IsMaximized); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteStartArray("autoHideGroups");
            foreach (var group in layout.AutoHideGroups)
            {
                writer.WriteStartObject(); writer.WriteString("id", group.Id); writer.WriteString("side", group.Side.ToString()); WriteIds(writer, group.Windows); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteStartObject("editors");
            foreach (var (id, reference) in _editors)
            {
                if (!layout.Windows.ContainsKey(id) || !reference.TryGetTarget(out var editor)) continue;
                writer.WriteStartObject(id); writer.WriteString("text", editor.Text); writer.WriteBoolean("readOnly", editor.IsReadOnly);
                writer.WriteNumber("selectionStart", editor.SelectionStart); writer.WriteNumber("selectionLength", editor.SelectionLength);
                writer.WriteNumber("instance", RuntimeHelpers.GetHashCode(editor)); writer.WriteEndObject();
            }
            writer.WriteEndObject(); writer.WriteStartObject("bounds");
            foreach (var (id, bounds) in hitBounds) WriteRect(writer, id, bounds);
            writer.WriteEndObject(); writer.WriteStartArray("errors");
            foreach (var warning in _site.Geometry?.ConstraintWarnings.ToArray() ?? []) writer.WriteStringValue(warning);
            writer.WriteEndArray(); writer.WriteEndObject(); writer.Flush();
            Uno.Foundation.WebAssemblyRuntime.InvokeJS("globalThis.dockingScene = " + Encoding.UTF8.GetString(buffer.WrittenSpan) + "; globalThis.dockingSceneDiagnosticError = null;");
        }
        catch (Exception error) { Uno.Foundation.WebAssemblyRuntime.InvokeJS("globalThis.dockingSceneDiagnosticError = " + JsonSerializer.Serialize(error.ToString()) + ";"); }
#endif
    }
    private void WriteSelectorDiagnostics(Utf8JsonWriter writer)
    {
        if (!_contentRoots.TryGetValue("properties", out var reference) || !reference.TryGetTarget(out var root)) return;
        var pending = new Stack<Microsoft.UI.Xaml.DependencyObject>(); pending.Push(root);
        while (pending.TryPop(out var node))
        {
            if (node is DockComboBox selector)
            {
                writer.WriteStartObject("inspectorSelector");
                writer.WriteBoolean("open", selector.IsDropDownOpen);
                writer.WriteNumber("selectedIndex", selector.SelectedIndex);
                writer.WriteNumber("forwardedKeys", selector.ForwardedKeyCount);
                writer.WriteEndObject();
                return;
            }
            for (int i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node); i++)
                pending.Push(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i));
        }
    }
    private static void WriteIds(Utf8JsonWriter writer, IEnumerable<string> ids)
    { writer.WriteStartArray("windows"); foreach (var id in ids) writer.WriteStringValue(id); writer.WriteEndArray(); }
    private static void WriteRect(Utf8JsonWriter writer, string name, DockRect bounds)
    {
        writer.WriteStartObject(name); writer.WriteNumber("x", bounds.X); writer.WriteNumber("y", bounds.Y);
        writer.WriteNumber("width", bounds.Width); writer.WriteNumber("height", bounds.Height); writer.WriteEndObject();
    }
}
