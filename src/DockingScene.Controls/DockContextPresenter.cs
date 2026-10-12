using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.Controls;

/// <summary>Retained contextual content for caption/tool surfaces. Template changes
/// create one new root; data changes reuse that root. UIElement data is deliberately
/// not passed into templates, preventing an editor from acquiring another parent.</summary>
public sealed class DockContextPresenter : UserControl
{
    private readonly Grid _visualHost = new();
    private readonly ContentPresenter _plainContent = new();
    private DataTemplate? _materializedTemplate;
    private UIElement? _materialized;
    private object? _presented;
    private bool _reconciling, _queued, _updating;
    public static readonly DependencyProperty ContextProperty = DependencyProperty.Register(nameof(Context), typeof(object), typeof(DockContextPresenter), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty ContextTemplateProperty = DependencyProperty.Register(nameof(ContextTemplate), typeof(DataTemplate), typeof(DockContextPresenter), new PropertyMetadata(null, Changed));
    public object? Context { get => GetValue(ContextProperty); set => SetValue(ContextProperty, value); }
    public DataTemplate? ContextTemplate { get => (DataTemplate?)GetValue(ContextTemplateProperty); set => SetValue(ContextTemplateProperty, value); }
    public long TemplateBuildCount { get; private set; }
    public object? PresentedContent => _presented;
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((DockContextPresenter)d).Reconcile();
    public DockContextPresenter()
    {
        IsTabStop = false;
        Content = _visualHost;
        SizeChanged += (_, _) => Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry { Rect = new(0, 0, Math.Max(0, ActualWidth), Math.Max(0, ActualHeight)) };
        Loaded += (_, _) => Reconcile();
    }
    /// <summary>Apply a data/template pair atomically, avoiding a transient bare
    /// UIElement when a templated visual context is cleared or rebound.</summary>
    public void Update(object? context, DataTemplate? template)
    {
        _updating = true;
        try { Context = context; ContextTemplate = template; }
        finally { _updating = false; }
        Reconcile();
    }
    private void Reconcile()
    {
        if (_updating) return;
        if (_reconciling)
        {
            if (!_queued) { _queued = true; if (!DispatcherQueue.TryEnqueue(() => { _queued = false; Reconcile(); })) _queued = false; }
            return;
        }
        _reconciling = true;
        try
        {
            var template = ContextTemplate; var context = Context;
            bool Current() => ReferenceEquals(template, ContextTemplate) && ReferenceEquals(context, Context);
            if (template is null)
            {
                _materializedTemplate = null; _materialized = null;
                ContentLease.Set(_plainContent, context);
                if (Current()) Attach(context is null ? null : _plainContent, context, Current);
                return;
            }
            if (!ReferenceEquals(template, _materializedTemplate))
            {
                var created = template.LoadContent();
                TemplateBuildCount++;
                if (!Current()) return;
                if (created is not null and not UIElement) throw new InvalidOperationException("A context template must create a UIElement.");
                _materializedTemplate = template; _materialized = (UIElement?)created;
            }
            object? data = context is UIElement ? null : context;
            if (_materialized is FrameworkElement element && !ReferenceEquals(element.DataContext, data)) element.DataContext = data;
            // Do not feed an already materialized template root through another
            // ContentPresenter. Its native template/content synchronization can
            // substitute the surrounding data context during attachment/theme work.
            // The owned visual host keeps the explicit template context authoritative.
            if (Current()) Attach(_materialized, _materialized, Current);
        }
        finally { _reconciling = false; }
    }
    private void Attach(UIElement? visual, object? presented, Func<bool> current)
    {
        if (_visualHost.Children.Count == 1 && ReferenceEquals(_visualHost.Children[0], visual))
        { _presented = presented; return; }
        if (_visualHost.Children.Count > 0) _visualHost.Children.Clear();
        _presented = null;
        // Unloaded callbacks may replace the context pair too.
        if (!current()) return;
        if (visual is not null)
        {
            ContentLease.Detach(visual);
            if (!current()) return;
            _visualHost.Children.Add(visual);
        }
        if (current()) _presented = presented;
    }
}
