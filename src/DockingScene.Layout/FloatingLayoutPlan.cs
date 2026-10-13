using DockingScene.Core;

namespace DockingScene.Layout;

/// <summary>Measured, immutable layout of one floating host. Construction visits
/// only that host; Arrange writes into caller-owned buffers without allocations.
/// Concurrent callers must use separate, non-overlapping output buffers.</summary>
public sealed class FloatingLayoutPlan
{
    public const int MaximumNodes = 32768;
    private readonly Entry[] _entries;
    private readonly DockMetrics _metrics;
    public string HostId { get; }
    public long Revision { get; }
    public int NodeCount => _entries.Length;
    public int SplitterCount { get; }
    public DockMeasure ContentMeasure => _entries[0].Measure;
    private readonly record struct Entry(string Id, DockMeasure Measure, int First, int Second,
        int Splitter, SplitAxis Axis, double Ratio);

    public FloatingLayoutPlan(DockLayout snapshot, string hostId, DockMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        _metrics = metrics ?? new();
        if (!double.IsFinite(_metrics.DpiScale) || _metrics.DpiScale <= 0 ||
            !ValidMetric(_metrics.Border) || !ValidMetric(_metrics.TitleHeight) ||
            !ValidMetric(_metrics.TabHeight) || !ValidMetric(_metrics.Splitter) ||
            !ValidMetric(_metrics.AutoHideRail)) throw new ArgumentException("Invalid layout metrics.", nameof(metrics));
        var host = snapshot.FloatingHosts.FirstOrDefault(h => h.Id == hostId)
            ?? throw new ArgumentException("The floating host is not registered.", nameof(hostId));
        HostId = hostId; Revision = snapshot.Revision;
        var entries = new List<Entry>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var members = new HashSet<string>(StringComparer.Ordinal);
        int splitters = 0;
        Build(host.Root, 0);
        _entries = entries.ToArray(); SplitterCount = splitters;

        int Build(DockNode node, int depth)
        {
            if (node is null || depth > 64 || entries.Count >= MaximumNodes ||
                string.IsNullOrWhiteSpace(node.Id) || !identities.Add(node.Id))
                throw new ArgumentException("Invalid, repeated or excessively deep floating layout.", nameof(snapshot));
            int slot = entries.Count; entries.Add(default);
            int first = -1, second = -1, splitter = -1;
            SplitAxis axis = default; double ratio = 0;
            DockMeasure measure;
            switch (node)
            {
                case DockGroup group:
                    if (!Enum.IsDefined(group.Kind) || group.Windows.IsDefaultOrEmpty)
                        throw new ArgumentException("Invalid floating group.", nameof(snapshot));
                    double minW = 0, minH = 0, maxW = double.MaxValue, maxH = double.MaxValue;
                    bool title = false;
                    foreach (string id in group.Windows)
                    {
                        if (members.Count >= MaximumNodes || string.IsNullOrWhiteSpace(id) || !members.Add(id) ||
                            !snapshot.Windows.TryGetValue(id, out var window))
                            throw new ArgumentException("Invalid or repeated floating group member.", nameof(snapshot));
                        var min = window.MinimumSize; var max = window.MaximumSize;
                        if (!min.IsFinite || !max.IsFinite || min.Width < 0 || min.Height < 0 ||
                            max.Width <= 0 || max.Height <= 0 || max.Width < min.Width || max.Height < min.Height)
                            throw new ArgumentException("Invalid floating content constraints.", nameof(snapshot));
                        minW = Math.Max(minW, min.Width); minH = Math.Max(minH, min.Height);
                        maxW = Math.Min(maxW, max.Width); maxH = Math.Min(maxH, max.Height);
                        title |= window.HasTitleBar ?? _metrics.ToolWindowsHaveTitleBars;
                    }
                    double chrome = _metrics.TabHeight + (group.Kind == GroupKind.Tools && title ? _metrics.TitleHeight : 0) + 2 * _metrics.Border;
                    minW += 2 * _metrics.Border; minH += chrome;
                    measure = new(new(minW, minH), new(Math.Max(minW, maxW + 2 * _metrics.Border), Math.Max(minH, maxH + chrome)));
                    break;
                case DockWorkspace workspace:
                    first = workspace.Content is null ? -1 : Build(workspace.Content, depth + 1);
                    measure = first < 0 ? new(new(80, 60), DockSize.Maximum) : entries[first].Measure;
                    break;
                case DockSplit split:
                    if (!Enum.IsDefined(split.Axis) || !double.IsFinite(split.Ratio) || split.Ratio < 0 || split.Ratio > 1)
                        throw new ArgumentException("Invalid floating splitter.", nameof(snapshot));
                    splitter = splitters++; axis = split.Axis; ratio = split.Ratio;
                    first = Build(split.First, depth + 1); second = Build(split.Second, depth + 1);
                    var a = entries[first].Measure; var b = entries[second].Measure;
                    measure = axis == SplitAxis.Horizontal
                        ? new(new(a.Minimum.Width + b.Minimum.Width + _metrics.Splitter, Math.Max(a.Minimum.Height, b.Minimum.Height)),
                            new(a.Maximum.Width + b.Maximum.Width + _metrics.Splitter, Math.Min(a.Maximum.Height, b.Maximum.Height)))
                        : new(new(Math.Max(a.Minimum.Width, b.Minimum.Width), a.Minimum.Height + b.Minimum.Height + _metrics.Splitter),
                            new(Math.Min(a.Maximum.Width, b.Maximum.Width), a.Maximum.Height + b.Maximum.Height + _metrics.Splitter));
                    break;
                default: throw new ArgumentException("Unknown floating layout node.", nameof(snapshot));
            }
            if (!measure.Minimum.IsFinite || !measure.Maximum.IsFinite)
                throw new ArgumentException("Floating layout measurement overflowed.", nameof(snapshot));
            entries[slot] = new(node.Id, measure, first, second, splitter, axis, ratio);
            return slot;
        }
    }

    public string GetNodeId(int index) => _entries[index].Id;
    /// <summary>Returns -1 for a non-split node; otherwise its output splitter slot.</summary>
    public int GetSplitterIndex(int nodeIndex) => _entries[nodeIndex].Splitter;

    /// <summary>Arrange the host at its displayed frame bounds. No viewport
    /// coercion or model mutation occurs. Reuse buffers of NodeCount and
    /// SplitterCount rectangles. Zero-sized frames are supported.</summary>
    public void Arrange(DockRect frame, Span<DockRect> nodeBounds, Span<DockRect> splitterBounds)
    {
        ValidateRect(frame);
        if (nodeBounds.Length < NodeCount || splitterBounds.Length < SplitterCount ||
            nodeBounds.Overlaps(splitterBounds)) throw new ArgumentException("Insufficient or overlapping layout buffers.");
        var content = new DockRect(frame.X + _metrics.Border, frame.Y + _metrics.TitleHeight,
            Math.Max(0, frame.Width - 2 * _metrics.Border), Math.Max(0, frame.Height - _metrics.TitleHeight - _metrics.Border));
        ValidateRect(content); // Validate everything that can overflow before touching caller output.
        nodeBounds[0] = content;
        for (int i = 0; i < _entries.Length; i++)
        {
            ref readonly var entry = ref _entries[i];
            if (entry.First < 0) continue;
            var rect = nodeBounds[i];
            if (entry.Second < 0) { nodeBounds[entry.First] = rect; continue; }
            bool horizontal = entry.Axis == SplitAxis.Horizontal;
            double extent = horizontal ? rect.Width : rect.Height;
            double gap = Math.Min(_metrics.Splitter, extent);
            double available = Math.Max(0, extent - gap);
            var a = _entries[entry.First].Measure; var b = _entries[entry.Second].Measure;
            double minA = horizontal ? a.Minimum.Width : a.Minimum.Height;
            double minB = horizontal ? b.Minimum.Width : b.Minimum.Height;
            double maxA = horizontal ? a.Maximum.Width : a.Maximum.Height;
            double maxB = horizontal ? b.Maximum.Width : b.Maximum.Height;
            double size;
            if (minA + minB > available)
                size = minA + minB == 0 ? 0 : available * (minA / (minA + minB));
            else
            {
                double low = Math.Max(minA, available - maxB), high = Math.Min(maxA, available - minB);
                if (low > high) { low = minA; high = Math.Max(low, available - minB); }
                size = Math.Clamp(available * entry.Ratio, low, high);
            }
            if (double.IsFinite(size * _metrics.DpiScale)) size = Math.Round(size * _metrics.DpiScale) / _metrics.DpiScale;
            size = Math.Clamp(size, 0, available);
            var bar = horizontal ? new DockRect(rect.X + size, rect.Y, gap, rect.Height)
                : new DockRect(rect.X, rect.Y + size, rect.Width, gap);
            nodeBounds[entry.First] = horizontal ? new(rect.X, rect.Y, size, rect.Height) : new(rect.X, rect.Y, rect.Width, size);
            nodeBounds[entry.Second] = horizontal ? new(bar.Right, rect.Y, available - size, rect.Height)
                : new(rect.X, bar.Bottom, rect.Width, available - size);
            splitterBounds[entry.Splitter] = bar;
        }
    }
    private static bool ValidMetric(double value) => double.IsFinite(value) && value >= 0;
    private static void ValidateRect(DockRect rect)
    {
        if (!ValidMetric(rect.Width) || !ValidMetric(rect.Height) || !double.IsFinite(rect.X) ||
            !double.IsFinite(rect.Y) || !double.IsFinite(rect.Right) || !double.IsFinite(rect.Bottom))
            throw new ArgumentException("Invalid floating layout bounds.");
    }
}
