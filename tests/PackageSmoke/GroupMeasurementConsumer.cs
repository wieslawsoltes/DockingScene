using System.Runtime.CompilerServices;
using DockingScene.Core;
using DockingScene.Layout;

internal static class GroupMeasurementConsumer
{
    [ModuleInitializer]
    internal static void Validate()
    {
        var manager = new DockManager();
        manager.Register(new DockWindow("measured-tool", "Measured", WindowKind.Tool)
        { MinimumSize = new(120, 80) }).ThrowIfFailed();
        var group = DockTree.GroupOf(manager.Layout, "measured-tool")!;
        var horizontal = DockLayoutEngine.MeasureGroup(manager.Layout, group);
        manager.SetTabPlacement(group.Id, TabPlacement.Left).ThrowIfFailed();
        group = DockTree.GroupOf(manager.Layout, "measured-tool")!;
        var vertical = DockLayoutEngine.MeasureGroup(manager.Layout, group);
        if (vertical.Minimum.Width - horizontal.Minimum.Width != DockMetrics.VerticalTabStripWidth ||
            horizontal.Minimum.Height - vertical.Minimum.Height != DockMetrics.Default.TabHeight)
            throw new InvalidOperationException("Packaged vertical group measurement failed.");
        var hidden = DockMetrics.Default with { ToolWindowsSingleTabLayoutBehavior = SingleTabLayoutBehavior.Hide };
        if (DockLayoutEngine.MeasureGroup(manager.Layout, group, hidden).Minimum.Width != 122)
            throw new InvalidOperationException("Packaged single-tool visibility measurement failed.");
        var cache = new DockGeometryCache(); var bounds = new DockRect(0, 0, 1000, 700);
        if (!ReferenceEquals(cache.Arrange(manager.Layout, bounds), cache.Arrange(manager.Layout, bounds)))
            throw new InvalidOperationException("Packaged omitted-metrics cache did not retain its result.");
    }
}
