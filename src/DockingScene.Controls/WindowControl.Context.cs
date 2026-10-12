using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;

namespace DockingScene.Controls;

public partial class WindowControl
{
    private readonly DockContextPresenter _titleContext = new() { VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private DockingWindow? _contextOwner;
    private bool _settingContextOwner;
    public DockContextPresenter TitleBarContextPresenter => _titleContext;
    public static readonly DependencyProperty TitleBarContextContentProperty = DependencyProperty.Register(nameof(TitleBarContextContent), typeof(object), typeof(WindowControl), new PropertyMetadata(null, ContextChanged));
    public static readonly DependencyProperty TitleBarContextContentTemplateProperty = DependencyProperty.Register(nameof(TitleBarContextContentTemplate), typeof(DataTemplate), typeof(WindowControl), new PropertyMetadata(null, ContextChanged));
    public object? TitleBarContextContent { get => GetValue(TitleBarContextContentProperty); set => SetValue(TitleBarContextContentProperty, value); }
    public DataTemplate? TitleBarContextContentTemplate { get => (DataTemplate?)GetValue(TitleBarContextContentTemplateProperty); set => SetValue(TitleBarContextContentTemplateProperty, value); }
    private static void ContextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((WindowControl)d).UpdateTitleContext();
    private void ConfigureTitleContext()
    {
        _title.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Grid.SetColumn(_buttons, 3); Grid.SetColumn(_titleContext, 2); _title.Children.Add(_titleContext);
        _title.SizeChanged += (_, _) => UpdateTitleContextWidth();
        _buttons.SizeChanged += (_, _) => UpdateTitleContextWidth();
        _titleContext.AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) =>
        { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) Activated?.Invoke(); }), true);
    }
    internal void SetTitleContextOwner(DockingWindow? owner)
    {
        if (ReferenceEquals(_contextOwner, owner)) return;
        _settingContextOwner = true;
        try
        {
            _contextOwner = owner;
            ClearValue(TitleBarContextContentTemplateProperty); ClearValue(TitleBarContextContentProperty);
            if (owner is not null)
            {
                // Bind once per owning window, not per activation or metadata refresh.
                SetBinding(TitleBarContextContentTemplateProperty, new Binding { Source = owner, Path = new PropertyPath(nameof(DockingWindow.StandardMdiTitleBarContextContentTemplate)), Mode = BindingMode.OneWay });
                SetBinding(TitleBarContextContentProperty, new Binding { Source = owner, Path = new PropertyPath(nameof(DataContext)), Mode = BindingMode.OneWay });
            }
        }
        finally { _settingContextOwner = false; }
        UpdateTitleContext();
    }

    private void UpdateTitleContext()
    {
        // A bare DockingWindow.DataContext is data, not default caption content.
        // Direct standalone WindowControl content remains supported without a template.
        if (_settingContextOwner) return;
        _titleContext.Update(_contextOwner is not null && TitleBarContextContentTemplate is null ? null : TitleBarContextContent,
            TitleBarContextContentTemplate);
        _titleContext.Visibility = !IsMinimized && (TitleBarContextContentTemplate is not null || _titleContext.Context is not null)
            ? Visibility.Visible : Visibility.Collapsed;
        UpdateTitleContextWidth();
    }
    private void UpdateTitleContextWidth()
    {
        // Leave space for native caption actions and a usable title drag target,
        // even if application context requests infinite or very large width.
        double available = Math.Max(0, _title.ActualWidth - _buttons.ActualWidth - 23 - 60);
        double limit = Math.Min(available, Math.Max(0, _title.ActualWidth * 0.45));
        if (_titleContext.MaxWidth != limit) _titleContext.MaxWidth = limit;
    }
}
