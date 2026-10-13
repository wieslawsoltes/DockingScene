namespace DockingScene.Core;

public enum FloatingWindowTitleBarDoubleClickMode { ToggleMaximized, Dock }

public sealed partial class DockManager
{
    /// <summary>Execute a tool container's title gesture against current ownership.
    /// Floating ToggleMaximized operates on the enclosing host; Dock operates on
    /// the clicked group only. Docked and auto-hidden groups float as one group.</summary>
    public DockResult InvokeToolCaptionDoubleClick(string groupId,
        FloatingWindowTitleBarDoubleClickMode mode = FloatingWindowTitleBarDoubleClickMode.ToggleMaximized,
        long? expectedRevision = null) => Transaction("ToolCaptionDoubleClick", [manager =>
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (string.IsNullOrWhiteSpace(groupId)) throw new ArgumentException("A tool container id is required.");
        var layout = manager.Layout;
        var group = DockTree.Find(layout, groupId) as DockGroup;
        if (group is null)
        {
            var auto = layout.AutoHideGroups.FirstOrDefault(g => g.Id == groupId)
                ?? throw new ArgumentException("The tool container is no longer present.");
            return manager.FloatMany(auto.Windows);
        }
        if (group.Kind != GroupKind.Tools) throw new ArgumentException("Only tool container titles support this action.");
        var hostId = DockLayoutIndex.For(layout).Locate(group.Windows[0]).HostId;
        if (hostId is null) return manager.FloatMany(group.Windows);
        if (mode == FloatingWindowTitleBarDoubleClickMode.Dock) return manager.DockMany(group.Windows, null, DockSide.Center);
        var host = layout.FloatingHosts.First(h => h.Id == hostId);
        return manager.SetFloatingMaximized(hostId, !host.IsMaximized);
    }], expectedRevision);
}
