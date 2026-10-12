using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.Controls;

/// <summary>Retained contextual content for caption/tool surfaces. Template changes
/// create one new root; data changes reuse that root. UIElement data is deliberately
/// not passed into templates, preventing an editor from acquiring another parent.</summary>
public sealed class DockContextPresenter : UserControl
{
    private readonly ContentPresenter _presenter = new();
    private DataTemplate? _materializedTemplate;
    private object? _materialized;
    private bool _reconciling, _queued, _updating;
    public static readonly DependencyProperty ContextProperty = DependencyProperty.Register(nameof(Context), typeof(object), typeof(DockContextPresenter), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty ContextTemplateProperty = DependencyProperty.Register(nameof(ContextTemplate), typeof(DataTemplate), typeof(DockContextPresenter), new PropertyMetadata(null, Changed));
    public object? Context { get => GetValue(ContextProperty); set => SetValue(ContextProperty, value); }
    public DataTemplate? ContextTemplate { get => (DataTemplate?)GetValue(ContextTemplateProperty); set => SetValue(ContextTemplateProperty, value); }
    public long TemplateBuildCount { get; private set; }
    public object? PresentedContent => _presenter.Content;
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((DockContextPresenter)d).Reconcile();
    public DockContextPresenter()
    {
        IsTabStop = false;
        Content = _presenter;
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
            var template = ContextTemplate;
            if (template is null)
            {
                _materializedTemplate = null; _materialized = null;
                ContentLease.Set(_presenter, Context);
                return;
            }
            if (!ReferenceEquals(template, _materializedTemplate))
            {
                // Template code can reenter through application Loaded callbacks.
                // Do not install a result belonging to an obsolete template.
                var created = template.LoadContent();
                TemplateBuildCount++;
                if (!ReferenceEquals(template, ContextTemplate)) return;
                _materializedTemplate = template; _materialized = created;
            }
            object? data = Context is UIElement ? null : Context;
            if (_materialized is FrameworkElement element && !ReferenceEquals(element.DataContext, data)) element.DataContext = data;
            // DataContextChanged is application code too. It can replace the
            // template/data pair or release its owning host before attachment.
            // Do not attach that now-obsolete root, even for one layout cycle.
            if (!ReferenceEquals(template, ContextTemplate) ||
                !ReferenceEquals(data, Context is UIElement ? null : Context)) return;
            ContentLease.Set(_presenter, _materialized);
        }
        finally { _reconciling = false; }
    }
}
