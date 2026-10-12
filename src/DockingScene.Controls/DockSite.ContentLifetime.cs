namespace DockingScene.Controls;

public partial class DockSite
{
    /// <summary>False by default: application-owned content is released, not disposed.
    /// Opt in only when each factory result is owned exclusively by this site. Applies
    /// to unregister and manager replacement, never ordinary close/hide/redock.</summary>
    public bool DisposeContentOnUnregister { get; set; }
    public event Action<string, object?>? WindowContentReleased;
    private void ReleaseWindowContent(IEnumerable<string> ids)
    {
        var released = new List<(string Id, object? Content)>();
        foreach (string id in ids)
        {
            if (!_windows.Remove(id, out var window)) continue;
            var content = window.Content; window.ContentFactory = null; window.Content = null;
            if (window is ToolWindow tool) tool.ToolWindowContainerTitleBarContextContentTemplate = null;
            window.StandardMdiTitleBarContextContentTemplate = null; window.DataContext = null;
            released.Add((id, content));
        }
        // Detach the whole batch before callbacks can register a replacement.
        var disposed = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var (id, content) in released)
        {
            try
            {
                if (DisposeContentOnUnregister && content is IDisposable disposable && disposed.Add(content)) disposable.Dispose();
                WindowContentReleased?.Invoke(id, content);
            }
            catch (Exception error) { OperationRejected?.Invoke("Content release failed: " + error.Message); }
        }
    }
}
