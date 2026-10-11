namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    public void EnableHostReview()
    {
        // Diagnostic/editor registries must not keep publishing an obsolete instance
        // after unregister or manager replacement while the next render is queued.
        _site.WindowContentReleased += (id, content) =>
        {
            if (_editors.TryGetValue(id, out var editor) && editor.TryGetTarget(out var target) && ReferenceEquals(target, content)) _editors.Remove(id);
            if (_contentRoots.TryGetValue(id, out var root) && root.TryGetTarget(out var visual) && ReferenceEquals(visual, content)) _contentRoots.Remove(id);
        };
    }
}
