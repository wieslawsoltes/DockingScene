using System.Collections.Immutable;

namespace DockingScene.Core;

public sealed partial class DockManager
{
    /// <summary>Registers a bounded collection as one cancellable/undoable change.
    /// Open batches contain one window kind and activate their first input item.
    /// Closed batches may mix kinds. A duplicate, invalid item, or stale revision
    /// rejects the entire batch. Enumeration stops at the available window budget.</summary>
    public DockResult RegisterMany(IEnumerable<DockWindow> windows, bool open = true,
        string? targetId = null, DockSide side = DockSide.Center, long? expectedRevision = null) =>
        Apply("RegisterMany", layout =>
        {
            ArgumentNullException.ThrowIfNull(windows);
            if (!Enum.IsDefined(side)) throw new ArgumentOutOfRangeException(nameof(side));
            var registry = layout.Windows.ToBuilder();
            var ids = ImmutableArray.CreateBuilder<string>();
            WindowKind? kind = null;
            foreach (var window in windows)
            {
                if (registry.Count >= Policy.MaximumWindows) throw new InvalidOperationException("Window limit reached.");
                ArgumentNullException.ThrowIfNull(window);
                if (string.IsNullOrWhiteSpace(window.Id)) throw new ArgumentException("A window id is required.");
                if (!registry.TryAdd(window.Id, window)) throw new InvalidOperationException("Duplicate window id: " + window.Id);
                if (open && kind is { } previous && previous != window.Kind)
                    throw new ArgumentException("An open registration batch must contain a single window kind.");
                kind ??= window.Kind;
                ids.Add(window.Id);
            }
            if (ids.Count == 0) return layout;
            var registered = ids.ToImmutable();
            var candidate = layout with { Windows = registry.ToImmutable(), HiddenWindows = layout.HiddenWindows.AddRange(registered) };
            return open ? Place(candidate, registered, targetId, side,
                kind == WindowKind.Document ? GroupKind.Documents : GroupKind.Tools) : candidate;
        }, expectedRevision);
}
