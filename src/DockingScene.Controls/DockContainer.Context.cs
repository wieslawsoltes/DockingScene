using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.Controls;

public enum ContextContentAlignment { Near, Far }

public partial class DockContainer
{
    private ToolWindow? _contextCandidate, _contextOwner;
    private DockContextPresenter? _contextView;
    private bool _contextRefreshing, _contextRefreshQueued;
    private long _contextVersion;
    public static readonly DependencyProperty ToolTitleContextAlignmentProperty = DependencyProperty.Register(
        nameof(ToolTitleContextAlignment), typeof(ContextContentAlignment), typeof(DockContainer),
        new PropertyMetadata(ContextContentAlignment.Far, (d, _) => ((DockContainer)d).UpdateTitleMetrics()));
    public ContextContentAlignment ToolTitleContextAlignment
    {
        get => (ContextContentAlignment)GetValue(ToolTitleContextAlignmentProperty);
        set { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); SetValue(ToolTitleContextAlignmentProperty, value); }
    }
    public DockContextPresenter? TitleBarContextPresenter => _contextView;
    private void ConfigureToolTitleContext()
    {
        _title.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Grid.SetColumn(_buttons, 3);
        _title.SizeChanged += (_, _) => UpdateTitleMetrics();
        _buttons.SizeChanged += (_, _) => UpdateTitleMetrics();
        Loaded += (_, _) => SelectToolTitleContext(_contextCandidate);
        Unloaded += (_, _) => ReleaseToolTitleContext();
    }
    private void SelectToolTitleContext(ToolWindow? window)
    {
        _contextCandidate = window;
        if (!ReferenceEquals(window, _contextOwner))
        {
            ReleaseToolTitleContext();
            if (window is not null)
            {
                // Only known docking containers participate in this transfer.
                // The template/editor's other application parents are never touched.
                window.TitleContextHost?.ReleaseToolTitleContext(discardCandidate: true);
                _contextOwner = window;
                window.TitleContextHost = this;
            }
        }
        RefreshToolTitleContext();
    }
    private void ReleaseToolTitleContext(bool discardCandidate = false)
    {
        _contextVersion++;
        if (discardCandidate) _contextCandidate = null;
        var window = _contextOwner;
        _contextOwner = null;
        if (window is not null && ReferenceEquals(window.TitleContextHost, this)) window.TitleContextHost = null;
        DetachToolTitleView();
    }
    private void DetachToolTitleView()
    {
        var view = _contextView;
        _contextView = null;
        if (view is not null) { view.SizeChanged -= ContextSizeChanged; _title.Children.Remove(view); }
    }
    internal void RefreshToolTitleContext()
    {
        _contextVersion++;
        if (_contextRefreshing)
        {
            if (!_contextRefreshQueued)
            {
                _contextRefreshQueued = true;
                if (!DispatcherQueue.TryEnqueue(() => { _contextRefreshQueued = false; if (IsLoaded) RefreshToolTitleContext(); })) _contextRefreshQueued = false;
            }
            return;
        }
        _contextRefreshing = true;
        try
        {
            long version = _contextVersion;
            var owner = _contextOwner;
            var view = owner?.GetTitleContext();
            if (version != _contextVersion || !ReferenceEquals(owner, _contextOwner)) return;
            if (!ReferenceEquals(view, _contextView))
            {
                DetachToolTitleView();
                if (version != _contextVersion || !ReferenceEquals(owner, _contextOwner)) return;
                if (view is not null)
                {
                    _contextView = view;
                    view.SizeChanged += ContextSizeChanged;
                    _title.Children.Add(view);
                }
            }
            if (version != _contextVersion) return;
            if (_contextView is { } current) current.Foreground = _caption.Foreground;
            UpdateTitleMetrics();
        }
        finally { _contextRefreshing = false; }
    }
    private void ContextSizeChanged(object sender, SizeChangedEventArgs e)
    { if (ReferenceEquals(sender, _contextView)) UpdateTitleMetrics(); }
    private void UpdateTitleMetrics()
    {
        if (_title.ColumnDefinitions.Count < 4) return;
        bool near = ToolTitleContextAlignment == ContextContentAlignment.Near;
        _title.ColumnDefinitions[0].Width = GridLength.Auto;
        _title.ColumnDefinitions[1].Width = near ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        _title.ColumnDefinitions[2].Width = near ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        Grid.SetColumn(_caption, near ? 1 : 0);
        Grid.SetColumn(_grip, near ? 2 : 1);
        Grid.SetColumn(_buttons, 3);
        // Built-in slots have fixed widths; count only visible actions. Using the
        // previous ActualWidth here can reserve hidden slots until another measure.
        double actionWidth = 0;
        foreach (var action in _buttons.Children)
            if (action is FrameworkElement { Visibility: Visibility.Visible } button) actionWidth += button.Width;
        double contextWidth = 0;
        if (_contextView is { } context)
        {
            Grid.SetColumn(context, near ? 0 : 2);
            double available = Math.Max(0, _title.ActualWidth - actionWidth - 66);
            double limit = Math.Min(available, Math.Max(0, _title.ActualWidth * .45));
            if (context.MaxWidth != limit) context.MaxWidth = limit;
            contextWidth = Math.Min(context.DesiredSize.Width, limit) + context.Margin.Left + context.Margin.Right;
        }
        double captionWidth = Math.Max(0, _title.ActualWidth - actionWidth - contextWidth - 16);
        if (_caption.MaxWidth != captionWidth) _caption.MaxWidth = captionWidth;
    }
}
