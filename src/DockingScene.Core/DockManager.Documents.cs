using System.Collections.Immutable;

namespace DockingScene.Core;

public sealed partial class DockManager
{
    public DockResult KeepOpen(string id) => UpdateWindow(id, window => window with { DisplayMode = TabDisplayMode.Normal });
    public DockResult SetPinned(string id, bool pinned) => UpdateWindow(id, window => window with { DisplayMode = pinned ? TabDisplayMode.Pinned : TabDisplayMode.Normal });

    /// <summary>Replaces the preview in the destination group atomically. A close veto also cancels the new preview.</summary>
    public DockResult OpenPreview(DockWindow document, string? targetId = null) => Apply("OpenPreview", layout =>
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Kind != WindowKind.Document) throw new InvalidOperationException("Only documents can be previews.");
        if (layout.Windows.TryGetValue(document.Id, out var existing) && existing.Kind != WindowKind.Document)
            throw new InvalidOperationException("Window identity and kind cannot change.");
        var target = ResolveTarget(layout, targetId) as DockGroup ?? DockTree.Descendants(layout.Root).OfType<DockGroup>().FirstOrDefault(group => group.Kind == GroupKind.Documents);
        if (target is { Kind: GroupKind.Tools }) throw new InvalidOperationException("A preview requires a document group.");
        var previous = target?.Windows.Where(id => id != document.Id && layout.Windows[id].DisplayMode == TabDisplayMode.Preview).ToImmutableArray() ?? [];
        Require(layout, previous, DockCapabilities.Close, false);
        layout = DockTree.RemoveReferences(layout, previous.ToHashSet(StringComparer.Ordinal));
        layout = layout with { HiddenWindows = layout.HiddenWindows.AddRange(previous) };
        if (!layout.Windows.ContainsKey(document.Id))
            layout = layout with { Windows = layout.Windows.Add(document.Id, document), HiddenWindows = layout.HiddenWindows.Add(document.Id) };
        else document = layout.Windows[document.Id];
        document = document with { DisplayMode = document.IsModified ? TabDisplayMode.Normal : TabDisplayMode.Preview };
        layout = layout with { Windows = layout.Windows.SetItem(document.Id, document) };
        string? destination = target is not null && DockTree.Find(layout, target.Id) is not null ? target.Id : null;
        return Place(layout, [document.Id], destination, DockSide.Center, GroupKind.Documents);
    });

    public DockResult CloseOtherTabs(string id, bool includePinned = false) => CloseRelative(id, 0, includePinned);
    public DockResult CloseTabsToLeft(string id, bool includePinned = false) => CloseRelative(id, -1, includePinned);
    public DockResult CloseTabsToRight(string id, bool includePinned = false) => CloseRelative(id, 1, includePinned);
    private DockResult CloseRelative(string id, int direction, bool includePinned) => Apply("CloseRelativeTabs", layout =>
    {
        var group = DockTree.GroupOf(layout, id) ?? throw new InvalidOperationException("No tab group.");
        int selected = group.Windows.IndexOf(id);
        var ids = group.Windows.Where((key, index) => key != id && (direction == 0 || (direction < 0 ? index < selected : index > selected)) &&
            (includePinned || layout.Windows[key].DisplayMode != TabDisplayMode.Pinned)).ToImmutableArray();
        Require(layout, ids, DockCapabilities.Close, false);
        var result = DockTree.RemoveReferences(layout, ids.ToHashSet(StringComparer.Ordinal));
        return result with { HiddenWindows = result.HiddenWindows.AddRange(ids) };
    });
    public DockResult ActivateTab(string groupId, int index, TabDisplayMode mode = TabDisplayMode.Normal) => Apply("ActivateTab", layout =>
    {
        var group = DockTree.Find(layout, groupId) as DockGroup ?? throw new InvalidOperationException("Unknown group.");
        var ids = group.Windows.Where(id => layout.Windows[id].DisplayMode == mode).ToArray();
        if (index < 0 || index >= ids.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return DockTree.Activate(layout, ids[index]);
    }, recordHistory: false);
}
