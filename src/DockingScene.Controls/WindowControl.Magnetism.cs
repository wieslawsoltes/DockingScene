using DockingScene.Core;
using DockingScene.Layout;

namespace DockingScene.Controls;

public partial class WindowControl
{
    private WindowMagnetism? _pointerMagnetism;
    public bool HasPointerMagnetism => _pointerMagnetism is not null;
    public long MagnetismQueryCount { get; private set; }
    public DockRect RawPointerBounds { get; private set; }
    internal void SetPointerMagnetism(WindowMagnetism? plan) => _pointerMagnetism = plan;
    private DockRect ApplyPointerMagnetism(DockRect raw, bool bypass)
    {
        RawPointerBounds = raw;
        if (bypass || _pointerMagnetism is not { } plan) return raw;
        MagnetismQueryCount++;
        return _resizeX == DockSide.Center && _resizeY == DockSide.Center ? plan.Move(raw) :
            plan.Resize(raw, _resizeX, _resizeY, MinimumSize, MaximumSize);
    }
}
