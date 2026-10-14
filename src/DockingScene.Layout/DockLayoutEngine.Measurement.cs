using DockingScene.Core;

namespace DockingScene.Layout;

public static partial class DockLayoutEngine
{
    /// <summary>Measures the common content constraints and physical tab/title chrome
    /// of a nonempty group without allocating a temporary window array. Application
    /// content is not created or measured. Selected tabs do not change the envelope.</summary>
    public static DockMeasure MeasureGroup(DockLayout layout, DockGroup group, DockMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(group);
        metrics ??= DockMetrics.Default;
        ValidateMetrics(metrics);
        var result = MeasureGroupCore(layout, group, metrics);
        if (!result.Minimum.IsFinite || !result.Maximum.IsFinite)
            throw new InvalidOperationException("Group size arithmetic overflowed.");
        return result;
    }

    private static DockMeasure MeasureGroupCore(DockLayout layout, DockGroup group, DockMetrics metrics)
    {
        if (group.Windows.IsDefaultOrEmpty) throw new ArgumentException("Cannot measure an empty group.", nameof(group));
        var measure = new DockGroupMeasurement();
        foreach (string id in group.Windows) measure.Add(layout.Windows[id], metrics.ToolWindowsHaveTitleBars);
        return measure.Complete(group, metrics);
    }

    internal static void ValidateMetrics(DockMetrics metrics)
    {
        if (metrics.ToolWindowsSingleTabLayoutBehavior is not (SingleTabLayoutBehavior.Show or SingleTabLayoutBehavior.Hide) ||
            metrics.DpiScale <= 0 || !double.IsFinite(metrics.DpiScale) ||
            !Valid(metrics.Splitter) || !Valid(metrics.TitleHeight) || !Valid(metrics.TabHeight) ||
            !Valid(metrics.Border) || !Valid(metrics.AutoHideRail))
            throw new ArgumentException("Invalid layout metrics.", nameof(metrics));
        static bool Valid(double value) => double.IsFinite(value) && value >= 0;
    }
}

// Shared by the full tree and the frozen floating-subtree plan. The latter also
// validates node/member ownership while accumulating, avoiding an extra scan.
internal struct DockGroupMeasurement
{
    private double _minWidth, _minHeight, _maxWidth, _maxHeight;
    private int _count;
    private bool _hasTitle;
    public DockGroupMeasurement() { _maxWidth = _maxHeight = double.MaxValue; }

    public void Add(DockWindow window, bool titleDefault)
    {
        ArgumentNullException.ThrowIfNull(window);
        var min = window.MinimumSize; var max = window.MaximumSize;
        if (!min.IsFinite || !max.IsFinite || min.Width < 0 || min.Height < 0 ||
            max.Width <= 0 || max.Height <= 0 || max.Width < min.Width || max.Height < min.Height)
            throw new ArgumentException("Invalid group content constraints.", nameof(window));
        _minWidth = Math.Max(_minWidth, min.Width); _minHeight = Math.Max(_minHeight, min.Height);
        _maxWidth = Math.Min(_maxWidth, max.Width); _maxHeight = Math.Min(_maxHeight, max.Height);
        _hasTitle |= window.HasTitleBar ?? titleDefault; _count++;
    }

    public readonly DockMeasure Complete(DockGroup group, DockMetrics metrics)
    {
        // These closed enum domains are deliberately checked without generic
        // Enum metadata/reflection in the repeatedly executed measurement path.
        if (_count == 0 || group.Kind is not (GroupKind.Documents or GroupKind.Tools) ||
            group.TabPlacement is not (TabPlacement.Top or TabPlacement.Bottom or TabPlacement.Left or TabPlacement.Right))
            throw new ArgumentException("Invalid group presentation.", nameof(group));
        bool vertical = group.TabPlacement is TabPlacement.Left or TabPlacement.Right;
        bool tabs = metrics.HasTabStrip(group);
        double widthChrome = 2 * metrics.Border + (tabs && vertical ? DockMetrics.VerticalTabStripWidth : 0);
        double heightChrome = 2 * metrics.Border + (tabs && !vertical ? metrics.TabHeight : 0) +
            (group.Kind == GroupKind.Tools && _hasTitle ? metrics.TitleHeight : 0);
        double minWidth = _minWidth + widthChrome, minHeight = _minHeight + heightChrome;
        return new DockMeasure(new(minWidth, minHeight),
            new(Math.Max(minWidth, _maxWidth + widthChrome), Math.Max(minHeight, _maxHeight + heightChrome)));
    }
}
