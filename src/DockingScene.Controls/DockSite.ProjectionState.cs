namespace DockingScene.Controls;

public partial class DockSite
{
    /// <summary>True while a queued or executing projection can still change the
    /// visual tree. UI-thread observers should not combine current model metadata
    /// with hit targets from the previous projection.</summary>
    public bool IsVisualUpdatePending => _rendering || Volatile.Read(ref _queued) != 0;
}
