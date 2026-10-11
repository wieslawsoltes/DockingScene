namespace DockingScene.Core;

public sealed partial class DockManager
{
    /// <summary>Keyboard tab access: digits 1-8 identify a position, 9 selects the
    /// last matching tab. Filtering is by layout kind, not absolute group index.</summary>
    public DockResult ActivateTabByDigit(string groupId, int digit, TabDisplayMode mode = TabDisplayMode.Normal) => Apply("ActivateTabByDigit", layout =>
    {
        if (digit < 1 || digit > 9 || !Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(digit));
        var group = DockTree.Find(layout, groupId) as DockGroup ?? throw new InvalidOperationException("Unknown tab group.");
        var ids = group.Windows.Where(id => layout.Windows[id].DisplayMode == mode).ToArray();
        int index = digit == 9 ? ids.Length - 1 : digit - 1;
        if (index < 0 || index >= ids.Length) throw new InvalidOperationException("No tab matches that keyboard position.");
        return DockTree.Activate(layout, ids[index]);
    }, recordHistory: false);
}
