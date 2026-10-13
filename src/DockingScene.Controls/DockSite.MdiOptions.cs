using Microsoft.UI.Xaml;

namespace DockingScene.Controls;

public partial class DockSite
{
    public static readonly DependencyProperty CanStandardMdiWindowsMinimizeProperty = DependencyProperty.Register(nameof(CanStandardMdiWindowsMinimize), typeof(bool), typeof(DockSite), new PropertyMetadata(true, TabsChanged));
    public static readonly DependencyProperty CanStandardMdiWindowsMaximizeProperty = DependencyProperty.Register(nameof(CanStandardMdiWindowsMaximize), typeof(bool), typeof(DockSite), new PropertyMetadata(true, TabsChanged));
    public bool CanStandardMdiWindowsMinimize { get => (bool)GetValue(CanStandardMdiWindowsMinimizeProperty); set => SetValue(CanStandardMdiWindowsMinimizeProperty, value); }
    public bool CanStandardMdiWindowsMaximize { get => (bool)GetValue(CanStandardMdiWindowsMaximizeProperty); set => SetValue(CanStandardMdiWindowsMaximizeProperty, value); }
    public static readonly DependencyProperty IsStandardMdiScrollingEnabledProperty = DependencyProperty.Register(
        nameof(IsStandardMdiScrollingEnabled), typeof(bool), typeof(DockSite), new PropertyMetadata(false, (d, _) =>
        {
            var site = (DockSite)d;
            if (site._mdi is { } host) host.IsScrollingEnabled = site.IsStandardMdiScrollingEnabled;
            site.QueueRender();
        }));
    public bool IsStandardMdiScrollingEnabled { get => (bool)GetValue(IsStandardMdiScrollingEnabledProperty); set => SetValue(IsStandardMdiScrollingEnabledProperty, value); }
    public StandardMdiHost? PrimaryMdiHost => _mdi;
}
