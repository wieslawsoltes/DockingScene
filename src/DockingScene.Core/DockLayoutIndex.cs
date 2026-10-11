using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace DockingScene.Core;

/// <summary>One linear traversal per immutable snapshot, followed by constant-time lookups.
/// The weak, reference-identity cache cannot extend a layout's lifetime or alias equal revisions.
/// Consume validated immutable layouts, as with DockTree. Never mutate records via reflection.</summary>
public sealed class DockLayoutIndex
{
    private static readonly ConditionalWeakTable<DockLayout, DockLayoutIndex> Cache = new();
    private static readonly WindowLocation Hidden = new(WindowPlacement.Hidden, null, null, DockSide.Center, -1);
    private readonly Dictionary<string, DockNode> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DockGroup> _groupsByWindow = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WindowLocation> _locations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _ranks = new(StringComparer.Ordinal);
    public ImmutableArray<DockNode> Nodes { get; }
    public ImmutableArray<DockGroup> Groups { get; }
    public int WindowCount => _locations.Count;
    public static DockLayoutIndex For(DockLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return Cache.GetValue(layout, static snapshot => new(snapshot));
    }
    public DockNode? Find(string id) => _nodes.GetValueOrDefault(id);
    public DockGroup? GroupOf(string id) => _groupsByWindow.GetValueOrDefault(id);
    public WindowLocation Locate(string id) => _locations.GetValueOrDefault(id) ?? Hidden;
    public int ActivationRank(string id) => _ranks.GetValueOrDefault(id, -1);
    private DockLayoutIndex(DockLayout layout)
    {
        var nodes = ImmutableArray.CreateBuilder<DockNode>();
        var groups = ImmutableArray.CreateBuilder<DockGroup>();
        var stack = new Stack<(DockNode Node, string? Host, int Depth)>();
        // Push in reverse order to preserve DockTree.Nodes' primary/host traversal order.
        for (int i = layout.FloatingHosts.Length - 1; i >= 0; i--)
            stack.Push((layout.FloatingHosts[i].Root, layout.FloatingHosts[i].Id, 0));
        if (layout.Root is { } root) stack.Push((root, null, 0));
        while (stack.TryPop(out var entry))
        {
            var node = entry.Node;
            if (entry.Depth > 64 || node is null || !_nodes.TryAdd(node.Id, node))
                throw new InvalidOperationException("Invalid or cyclic docking tree.");
            nodes.Add(node);
            switch (node)
            {
                case DockSplit split:
                    stack.Push((split.Second, entry.Host, entry.Depth + 1));
                    stack.Push((split.First, entry.Host, entry.Depth + 1)); break;
                case DockWorkspace { Content: { } content }:
                    stack.Push((content, entry.Host, entry.Depth + 1)); break;
                case DockGroup group:
                    groups.Add(group);
                    for (int i = 0; i < group.Windows.Length; i++)
                    {
                        string id = group.Windows[i];
                        _groupsByWindow.Add(id, group);
                        _locations.Add(id, new(entry.Host is not null ? WindowPlacement.Floating :
                            group.Kind == GroupKind.Documents ? WindowPlacement.Document : WindowPlacement.Docked,
                            group.Id, entry.Host, DockSide.Center, i));
                    }
                    break;
            }
        }
        foreach (var group in layout.AutoHideGroups)
            for (int i = 0; i < group.Windows.Length; i++)
                _locations.Add(group.Windows[i], new(WindowPlacement.AutoHidden, group.Id, null, group.Side, i));
        foreach (string id in layout.HiddenWindows) _locations.Add(id, Hidden);
        for (int i = 0; i < layout.ActivationHistory.Length; i++) _ranks.TryAdd(layout.ActivationHistory[i], i);
        Nodes = nodes.ToImmutable(); Groups = groups.ToImmutable();
    }
}
