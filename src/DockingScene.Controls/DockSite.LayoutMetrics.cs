using DockingScene.Layout;

namespace DockingScene.Controls;

public partial class DockSite
{
    private DockMetrics? _layoutMetrics;
    public long LayoutMetricsBuildCount { get; private set; }
    private DockMetrics GetLayoutMetrics()
    {
        double dpi = XamlRoot?.RasterizationScale ?? 1;
        if (_layoutMetrics is { } old && old.TabHeight == Theme.TabHeight &&
            old.TitleHeight == Theme.TitleHeight && old.Splitter == Theme.SplitterSize &&
            old.DpiScale == dpi && old.ToolWindowsHaveTitleBars == ToolWindowsHaveTitleBars &&
            old.ToolWindowsSingleTabLayoutBehavior == ToolWindowsSingleTabLayoutBehavior) return old;
        LayoutMetricsBuildCount++;
        return _layoutMetrics = new()
        {
            TabHeight = Theme.TabHeight, TitleHeight = Theme.TitleHeight,
            Splitter = Theme.SplitterSize, DpiScale = dpi,
            ToolWindowsHaveTitleBars = ToolWindowsHaveTitleBars,
            ToolWindowsSingleTabLayoutBehavior = ToolWindowsSingleTabLayoutBehavior
        };
    }
}
