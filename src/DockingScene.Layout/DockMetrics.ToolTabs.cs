namespace DockingScene.Layout;

public sealed partial record DockMetrics
{
    /// <summary>Whether a one-tool group reserves tab-strip space. Show preserves
    /// standalone layout defaults. DockSite passes its own live setting.</summary>
    public SingleTabLayoutBehavior ToolWindowsSingleTabLayoutBehavior { get; init; } = SingleTabLayoutBehavior.Show;

    internal bool HasTabStrip(DockingScene.Core.DockGroup group) =>
        group.Kind != DockingScene.Core.GroupKind.Tools || group.Windows.Length != 1 ||
        ToolWindowsSingleTabLayoutBehavior == SingleTabLayoutBehavior.Show;
}
