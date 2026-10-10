using System.Buffers;
using System.Text;
using System.Text.Json;
using DockingScene.Controls;
using DockingScene.Core;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private void PublishDiagnostics()
    {
#if BROWSER_WASM
        try
        {
            // Write the observation schema directly: safe for trimmed/AOT builds, with no anonymous-type reflection.
            var buffer = new ArrayBufferWriter<byte>();
            using var writer = new Utf8JsonWriter(buffer);
            var layout = _site.Manager.Layout;
            writer.WriteStartObject();
            writer.WriteBoolean("ready", true); writer.WriteString("version", "0.1.0"); writer.WriteString("renderer", "Uno Skia");
            writer.WriteNumber("revision", layout.Revision); writer.WriteString("activeWindowId", layout.ActiveWindowId);
            writer.WriteString("mdiMode", layout.MdiMode.ToString()); writer.WriteString("theme", _theme.Name);
            writer.WriteBoolean("locked", layout.IsLayoutLocked); writer.WriteBoolean("canUndo", _site.Manager.CanUndo); writer.WriteBoolean("canRedo", _site.Manager.CanRedo);
            writer.WriteBoolean("modified", _modified); writer.WriteNumber("cachedContentCount", _site.CachedContentCount); writer.WriteString("lastMessage", _lastMessage);
            writer.WriteStartArray("windows");
            foreach (var window in layout.Windows.Values)
            {
                writer.WriteStartObject(); writer.WriteString("id", window.Id); writer.WriteString("title", window.Title); writer.WriteString("kind", window.Kind.ToString());
                writer.WriteBoolean("isModified", window.IsModified); writer.WriteBoolean("isReadOnly", window.IsReadOnly);
                writer.WriteString("displayMode", window.DisplayMode.ToString()); writer.WriteString("placement", DockTree.Locate(layout, window.Id).Placement.ToString());
                WriteRect(writer, "mdiBounds", window.MdiBounds); writer.WriteString("mdiState", window.MdiState.ToString()); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteStartArray("groups");
            foreach (var group in DockTree.Groups(layout))
            {
                writer.WriteStartObject(); writer.WriteString("id", group.Id); writer.WriteString("kind", group.Kind.ToString());
                WriteIds(writer, group.Windows); writer.WriteString("selectedId", group.SelectedId); writer.WriteString("tabPlacement", group.TabPlacement.ToString()); writer.WriteEndObject();
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
            writer.WriteEndArray(); writer.WriteStartObject("bounds");
            foreach (var (id, bounds) in DockChrome.AutomationBounds(_root)) WriteRect(writer, id, bounds);
            writer.WriteEndObject(); writer.WriteStartArray("errors");
            foreach (var warning in _site.Geometry?.ConstraintWarnings.ToArray() ?? []) writer.WriteStringValue(warning);
            writer.WriteEndArray(); writer.WriteEndObject(); writer.Flush();
            string json = Encoding.UTF8.GetString(buffer.WrittenSpan);
            Uno.Foundation.WebAssemblyRuntime.InvokeJS("globalThis.dockingScene = " + json + "; globalThis.dockingSceneDiagnosticError = null;");
        }
        catch (Exception error)
        {
            Uno.Foundation.WebAssemblyRuntime.InvokeJS("globalThis.dockingSceneDiagnosticError = " + JsonSerializer.Serialize(error.ToString()) + ";");
        }
#endif
    }
    private static void WriteIds(Utf8JsonWriter writer, IEnumerable<string> ids)
    {
        writer.WriteStartArray("windows"); foreach (var id in ids) writer.WriteStringValue(id); writer.WriteEndArray();
    }
    private static void WriteRect(Utf8JsonWriter writer, string name, DockRect bounds)
    {
        writer.WriteStartObject(name); writer.WriteNumber("x", bounds.X); writer.WriteNumber("y", bounds.Y);
        writer.WriteNumber("width", bounds.Width); writer.WriteNumber("height", bounds.Height); writer.WriteEndObject();
    }
}
