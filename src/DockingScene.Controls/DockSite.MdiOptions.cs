using Microsoft.UI.Xaml;

namespace DockingScene.Controls;

public partial class DockSite
{
    public static readonly DependencyProperty CanStandardMdiWindowsMinimizeProperty = DependencyProperty.Register(nameof(CanStandardMdiWindowsMinimize), typeof(bool), typeof(DockSite), new PropertyMetadata(true, TabsChanged));
    public static readonly DependencyProperty CanStandardMdiWindowsMaximizeProperty = DependencyProperty.Register(nameof(CanStandardMdiWindowsMaximize), typeof(bool), typeof(DockSite), new PropertyMetadata(true, TabsChanged));
    public bool CanStandardMdiWindowsMinimize { get => (bool)GetValue(CanStandardMdiWindowsMinimizeProperty); set => SetValue(CanStandardMdiWindowsMinimizeProperty, value); }
    public bool CanStandardMdiWindowsMaximize { get => (bool)GetValue(CanStandardMdiWindowsMaximizeProperty); set => SetValue(CanStandardMdiWindowsMaximizeProperty, value); }
    public static readonly DependencyProperty AreStandardMdiMaximizedWindowFramesVisibleProperty = DependencyProperty.Register(nameof(AreStandardMdiMaximizedWindowFramesVisible), typeof(bool), typeof(DockSite), new PropertyMetadata(true, TabsChanged));
    public bool AreStandardMdiMaximizedWindowFramesVisible { get => (bool)GetValue(AreStandardMdiMaximizedWindowFramesVisibleProperty); set => SetValue(AreStandardMdiMaximizedWindowFramesVisibleProperty, value); }
    public StandardMdiHost? PrimaryMdiHost => _mdi;
}
