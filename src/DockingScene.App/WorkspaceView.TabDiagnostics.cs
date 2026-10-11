using System.Runtime.CompilerServices;
using System.Text.Json;
using DockingScene.Controls;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private void WriteTabDiagnostics(Utf8JsonWriter writer)
    {
        writer.WriteString("switcherSelection", _site.SwitcherSelectedId);
        writer.WriteBoolean("switcherInputScoped", _site.IsSwitcherInputScoped);
        writer.WriteStartArray("tabStrips");
        foreach (var (groupId, tabs) in _site.TabControls) WriteTabStrip(writer, groupId, tabs);
        if (_tabPlayground is { } playground && _site.Manager.Layout.Windows.ContainsKey("tab-playground")) WriteTabStrip(writer, "playground", playground);
        writer.WriteEndArray();
        writer.WriteBoolean("playgroundCloseVeto", _vetoPlaygroundClose);
        writer.WriteString("playgroundClosingId", _lastPlaygroundClosingId);
        writer.WriteNumber("playgroundClosingCount", _playgroundClosingCount);
    }
    private static void WriteTabStrip(Utf8JsonWriter writer, string id, AdvancedTabControl tabs)
    {
        writer.WriteStartObject(); writer.WriteString("id", id); writer.WriteString("overflow", tabs.OverflowBehavior.ToString());
        writer.WriteString("placement", tabs.Placement.ToString()); writer.WriteString("selectedId", tabs.SelectedId);
        writer.WriteBoolean("visible", tabs.IsTabStripVisible); writer.WriteNumber("realized", tabs.RealizedHeaderCount);
        writer.WriteNumber("count", tabs.Items.Count); writer.WriteNumber("minimum", tabs.MinTabExtent); writer.WriteNumber("maximum", tabs.MaxTabExtent);
        writer.WriteStartArray("lanes");
        foreach (var lane in tabs.HeaderLayout?.Lanes ?? [])
        {
            writer.WriteStartObject(); writer.WriteString("mode", lane.Mode.ToString()); writer.WriteNumber("start", lane.Start);
            writer.WriteNumber("extent", lane.Extent); writer.WriteNumber("contentExtent", lane.Metrics.Length);
            writer.WriteNumber("offset", tabs.GetLaneOffset(lane.Mode)); writer.WriteNumber("count", lane.Indices.Length); writer.WriteEndObject();
        }
        writer.WriteEndArray(); writer.WriteStartArray("items");
        foreach (var item in tabs.Items)
        {
            writer.WriteStartObject(); writer.WriteString("id", item.Id); writer.WriteBoolean("pinned", item.IsPinned); writer.WriteBoolean("preview", item.IsPreview);
            writer.WriteBoolean("contentCreated", item.ContentFactory is null);
            if (item.Content is { } content) writer.WriteNumber("contentInstance", RuntimeHelpers.GetHashCode(content));
            writer.WriteEndObject();
        }
        writer.WriteEndArray(); writer.WriteEndObject();
    }
}
