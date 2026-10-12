using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.Controls;

/// <summary>Retains one contextual template root. Changing data does not recreate
/// controls; UIElement template data is suppressed to preserve visual ownership.</summary>
public sealed class DockContextPresenter : UserControl
{
    private readonly Grid _visualHost = new();
    private readonly ContentPresenter _plainContent = new();
    private DataTemplate? _materializedTemplate;
    private UIElement? _materialized;
    private object? _presented;
    private bool _reconciling, _queued;
    private int _updateDepth;
    private long _version;
    public static readonly DependencyProperty ContextProperty = DependencyProperty.Register(nameof(Context), typeof(object), typeof(DockContextPresenter), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty ContextTemplateProperty = DependencyProperty.Register(nameof(ContextTemplate), typeof(DataTemplate), typeof(DockContextPresenter), new PropertyMetadata(null, Changed));
    public object? Context { get => GetValue(ContextProperty); set => SetValue(ContextProperty, value); }
    public DataTemplate? ContextTemplate { get => (DataTemplate?)GetValue(ContextTemplateProperty); set => SetValue(ContextTemplateProperty, value); }
    public long TemplateBuildCount { get; private set; }
    public object? PresentedContent => _presented;
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var presenter = (DockContextPresenter)d;
        presenter._version++;
        presenter.Reconcile();
    }
    public DockContextPresenter()
    {
        IsTabStop = false;
        Content = _visualHost;
        SizeChanged += (_, _) => Clip = new RectangleGeometry { Rect = new(0, 0, Math.Max(0, ActualWidth), Math.Max(0, ActualHeight)) };
        Loaded += (_, _) => Reconcile();
    }
    /// <summary>Apply data and template together without a transient bare visual.</summary>
    public void Update(object? context, DataTemplate? template)
    {
        _updateDepth++;
        try { Context = context; ContextTemplate = template; }
        finally { _updateDepth--; }
        Reconcile();
    }
    private void Reconcile()
    {
        if (_updateDepth != 0) return;
        if (_reconciling)
        {
            if (!_queued)
            {
                _queued = true;
                if (!DispatcherQueue.TryEnqueue(() => { _queued = false; Reconcile(); })) _queued = false;
            }
            return;
        }
        _reconciling = true;
        try
        {
            long version = _version;
            var template = ContextTemplate;
            var context = Context;
            if (template is null)
            {
                _materializedTemplate = null; _materialized = null;
                ContentLease.Set(_plainContent, context);
                if (version == _version) Attach(context is null ? null : _plainContent, context, version);
                return;
            }
            if (!ReferenceEquals(template, _materializedTemplate))
            {
                // The pinned Uno API already returns UIElement. Template roots
                // are exclusively owned here, not borrowed from an editor host.
                var created = template.LoadContent();
                TemplateBuildCount++;
                if (version != _version) return;
                _materializedTemplate = template; _materialized = created;
            }
            object? data = context is UIElement ? null : context;
            if (_materialized is FrameworkElement element &&
                (!ReferenceEquals(element.DataContext, data) || element.ReadLocalValue(DataContextProperty) == DependencyProperty.UnsetValue))
                element.DataContext = data;
            // Explicit local null prevents inherited UIElement data reappearing
            // on attachment. DataContextChanged can release or replace the owner.
            if (version != _version) return;
            ContentLease.Set(_plainContent, null);
            if (version == _version) Attach(_materialized, _materialized, version);
        }
        finally { _reconciling = false; }
    }
    private void Attach(UIElement? visual, object? presented, long version)
    {
        if (_visualHost.Children.Count == 1 && ReferenceEquals(_visualHost.Children[0], visual))
        { _presented = presented; return; }
        // Do not silently steal an application visual from an unrelated parent.
        if (visual is not null && VisualTreeHelper.GetParent(visual) is { } parent && !ReferenceEquals(parent, _visualHost))
            throw new InvalidOperationException("Context template content already belongs to another visual parent.");
        if (_visualHost.Children.Count > 0) _visualHost.Children.Clear();
        _presented = null;
        if (version != _version) return;
        if (visual is not null) _visualHost.Children.Add(visual);
        if (version == _version) _presented = presented;
    }
}
