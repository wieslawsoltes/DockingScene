using DockingScene.Layout;
using Microsoft.UI.Xaml;

namespace DockingScene.Controls;

public partial class DockSite
{
    /// <summary>Standard MDI pointer snapping range in logical units. Zero disables.
    /// Floating docking-preview magnetism retains its separate existing behavior.</summary>
    public static readonly DependencyProperty MagnetismSnapDistanceProperty = DependencyProperty.Register(
        nameof(MagnetismSnapDistance), typeof(double), typeof(DockSite), new PropertyMetadata(5d, MagnetismChanged));
    public static readonly DependencyProperty MagnetismGapDistanceProperty = DependencyProperty.Register(
        nameof(MagnetismGapDistance), typeof(double), typeof(DockSite), new PropertyMetadata(1d, MagnetismChanged));
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
    private static void MagnetismChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        WindowMagnetism.ValidateDistance((double)e.NewValue);
        var site = (DockSite)d;
        site.ApplyMdiMagnetismOptions();
        site.QueueRender();
    }
    private void ApplyMdiMagnetismOptions()
    {
        if (_mdi is not { } host) return;
        host.MagnetismSnapDistance = MagnetismSnapDistance;
        host.MagnetismGapDistance = MagnetismGapDistance;
    }
}
