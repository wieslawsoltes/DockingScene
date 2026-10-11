using System.Runtime.CompilerServices;
using System.Text.Json;
using DockingScene.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private void WriteTabDiagnostics(Utf8JsonWriter writer)
    {
        writer.WriteBoolean("factoryFailureRetained", _factoryFailureRetained);
        writer.WriteBoolean("recursiveFactoryRejected", _recursiveFactoryRejected);
        writer.WriteNumber("factoryCallbackCount", _factoryCallbackCount);
        writer.WriteBoolean("switcherHasInputFocus", _site.SwitcherHasInputFocus);
        writer.WriteString("focusedId", FocusManager.GetFocusedElement(XamlRoot) is DependencyObject focused ? AutomationProperties.GetAutomationId(focused) : null);
        writer.WriteNumber("geometryBuilds", _site.GeometryBuildCount);
        writer.WriteNumber("geometryReuses", _site.GeometryReuseCount);
        writer.WriteNumber("auxiliaryCreated", _site.AuxiliaryCreationCount);
        writer.WriteStartObject("auxiliaryIdentities");
        foreach (var (key, identity) in _site.AuxiliaryIdentities) writer.WriteNumber(key, identity);
        writer.WriteEndObject();
        writer.WriteString("switcherSelection", _site.SwitcherSelectedId);
        writer.WriteBoolean("switcherInputScoped", _site.IsSwitcherInputScoped);
        writer.WriteNumber("tabReorderRequests", _tabReorderRequests); writer.WriteNumber("tabReorders", _tabReorders);
        writer.WriteBoolean("tabReorderVeto", _vetoTabReorder); writer.WriteNumber("tabWorkloadMs", _lastTabWorkloadMs);
        writer.WriteNumber("contentCreations", _contentCreations); writer.WriteNumber("contentDisposals", _contentDisposals);
        writer.WriteNumber("contentReleases", _contentReleases); writer.WriteBoolean("ownsContent", _ownershipSite?.DisposeContentOnUnregister == true);
        writer.WriteNumber("trackedContent", _trackedContent is null ? 0 : RuntimeHelpers.GetHashCode(_trackedContent));
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
        writer.WriteNumber("textMeasurements", tabs.TextMeasurementCount); writer.WriteNumber("layoutPasses", tabs.HeaderLayoutCount);
        writer.WriteNumber("headersCreated", tabs.HeaderCreationCount); writer.WriteNumber("refreshes", tabs.RefreshCount);
        writer.WriteNumber("autoScrollSteps", tabs.DragAutoScrollSteps); writer.WriteBoolean("canAutoScroll", tabs.CanTabsAutoScroll);
        writer.WriteBoolean("dragActive", tabs.IsTabDragActive); writer.WriteBoolean("canDrag", tabs.CanTabsDrag);
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
