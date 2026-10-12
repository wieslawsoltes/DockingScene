using DockingScene.Core;
using DockingScene.Layout;
using Microsoft.UI.Xaml;

namespace DockingScene.Controls;

public sealed partial class StandardMdiHost
{
    public static readonly DependencyProperty MagnetismSnapDistanceProperty = DependencyProperty.Register(
        nameof(MagnetismSnapDistance), typeof(double), typeof(StandardMdiHost), new PropertyMetadata(5d, MagnetismChanged));
    public static readonly DependencyProperty MagnetismGapDistanceProperty = DependencyProperty.Register(
        nameof(MagnetismGapDistance), typeof(double), typeof(StandardMdiHost), new PropertyMetadata(1d, MagnetismChanged));
    public double MagnetismSnapDistance
    {
        get => (double)GetValue(MagnetismSnapDistanceProperty);
        set { WindowMagnetism.ValidateDistance(value); SetValue(MagnetismSnapDistanceProperty, value); }
    }
    public double MagnetismGapDistance
    {
        get => (double)GetValue(MagnetismGapDistanceProperty);
        set { WindowMagnetism.ValidateDistance(value); SetValue(MagnetismGapDistanceProperty, value); }
    }
    public long MagnetismPlanBuildCount { get; private set; }
    private static void MagnetismChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        WindowMagnetism.ValidateDistance((double)e.NewValue);
        ((StandardMdiHost)d).CancelMagnetismGestures();
    }
    private void CancelMagnetismGestures()
    {
        foreach (var frame in _windows.Values) frame.CancelMovement();
        _gestureRevisions.Clear();
    }
    private void BeginPointerMagnetism(WindowControl frame)
    {
        frame.SetPointerMagnetism(null);
        if (frame.IsKeyboardInteractionActive || MagnetismSnapDistance == 0 || !_active || _manager is null) return;
        var layout = _manager.Layout;
        var neighbors = new List<DockRect>(Math.Max(0, _windows.Count - 1));
        foreach (var (id, other) in _windows)
        {
            if (ReferenceEquals(frame, other) || !layout.Windows.TryGetValue(id, out var window) ||
                window.MdiState != MdiWindowState.Normal || DockTree.Locate(layout, id).Placement == WindowPlacement.Hidden) continue;
            neighbors.Add(other.Bounds);
        }
        if (neighbors.Count == 0) return;
        frame.SetPointerMagnetism(new(neighbors, MagnetismSnapDistance, MagnetismGapDistance));
        MagnetismPlanBuildCount++;
    }
}
