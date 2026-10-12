using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.Controls;

/// <summary>Native Uno ComboBox with opening-scoped keyboard routing. Popup and
/// root routes forward the original event at most once. Native selection, Escape,
/// type search, scrolling and item templates remain owned by ComboBox.</summary>
public partial class DockComboBox : ComboBox
{
    private Popup? _popup;
    private FrameworkElement? _inputScope;
    private UIElement? _inputRoot;
    private readonly KeyEventHandler _popupKeyDown, _popupKeyUp;
    private KeyRoutedEventArgs? _lastDown, _lastUp;
    private bool _completingFocus, _forwarding;
    public long ForwardedKeyCount { get; private set; }
    public bool InputDiagnosticsEnabled { get; set; }
    private readonly Queue<string> _navigationTrace = new();
    public IReadOnlyList<string> NavigationTrace => _navigationTrace.ToArray();
    private void TraceNavigation(string phase, KeyRoutedEventArgs e)
    {
        if (!InputDiagnosticsEnabled) return;
        var focused = XamlRoot is null ? null : FocusManager.GetFocusedElement(XamlRoot);
        int index = focused is DependencyObject d ? IndexFromContainer(d) : -1;
        var adjacent = ContainerFromIndex(Math.Clamp(index + 1, 0, Math.Max(0, Items.Count - 1))) as Control;
        _navigationTrace.Enqueue($"{phase} key={e.Key} original={e.OriginalKey} handled={e.Handled} open={IsDropDownOpen} selected={SelectedIndex} focused={focused?.GetType().Name}:{index} next={adjacent?.GetType().Name} loaded={adjacent?.IsLoaded} enabled={adjacent?.IsEnabled} tabstop={adjacent?.IsTabStop} selectorFocus={FocusState}");
        while (_navigationTrace.Count > 24) _navigationTrace.Dequeue();
    }

    public DockComboBox()
    {
        _popupKeyDown = (_, e) =>
        {
            if (ReferenceEquals(e, _lastDown) || !CanForward(e)) return;
            _lastDown = e; _forwarding = true;
            try
            {
                if (DeferOpeningKey(e, true)) return;
                ForwardedKeyCount++; TraceNavigation("before", e); base.OnKeyDown(e); TraceNavigation("after", e);
            }
            finally { _forwarding = false; }
        };
        _popupKeyUp = (_, e) =>
        {
            if (ReferenceEquals(e, _lastUp) || !CanForward(e)) return;
            _lastUp = e; _forwarding = true;
            try { if (!DeferOpeningKey(e, false)) base.OnKeyUp(e); }
            finally { _forwarding = false; }
        };
        DropDownOpened += (_, _) => { AttachPopupInput(); CompletePopupFocus(); };
        DropDownClosed += (_, _) => DetachPopupInput();
        Unloaded += (_, _) => DetachPopupInput();
        Items.VectorChanged += (_, _) => DiscardOpeningKeys();
        RegisterPropertyChangedCallback(ItemsSourceProperty, (_, _) => DiscardOpeningKeys());
        RegisterPropertyChangedCallback(IsEditableProperty, (_, _) => DiscardOpeningKeys());
        RegisterPropertyChangedCallback(IsEnabledProperty, (_, _) => { if (!IsEnabled) DiscardOpeningKeys(); });
    }
    protected override void OnApplyTemplate()
    {
        DetachPopupInput();
        base.OnApplyTemplate();
        _popup = GetTemplateChild("Popup") as Popup;
        if (IsDropDownOpen) AttachPopupInput();
    }
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is FrameworkElement container)
        {
            container.Loaded -= OnOpeningLayoutUpdated;
            container.Loaded += OnOpeningLayoutUpdated;
            QueueOpeningDrain();
        }
    }
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is FrameworkElement container) container.Loaded -= OnOpeningLayoutUpdated;
        base.ClearContainerForItemOverride(element, item);
    }
    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (IsDropDownOpen) { AttachPopupInput(); CompletePopupFocus(); }
    }
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    { if (!ReferenceEquals(e, _lastDown)) base.OnKeyDown(e); }
    protected override void OnKeyUp(KeyRoutedEventArgs e)
    { if (!ReferenceEquals(e, _lastUp)) base.OnKeyUp(e); }

    private bool CanForward(KeyRoutedEventArgs e)
    {
        if (_forwarding || e.Handled || IsEditable || !IsEnabled || !IsLoaded || !IsDropDownOpen ||
            XamlRoot is not { } root || _inputRoot != root.Content) return false;
        var focused = FocusManager.GetFocusedElement(root) as DependencyObject;
        for (var node = focused; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, this) || ReferenceEquals(node, _inputScope)) return true;
            if (node is ComboBoxItem item && ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(item), this)) return true;
        }
        return false;
    }
    private void AttachPopupInput()
    {
        if (IsEditable || XamlRoot?.Content is not { } root) return;
        var child = _popup?.Child as FrameworkElement;
        if (ReferenceEquals(root, _inputRoot) && ReferenceEquals(child, _inputScope)) return;
        DetachPopupInput(); _inputRoot = root; _inputScope = child;
        LayoutUpdated += OnOpeningLayoutUpdated;
        root.AddHandler(PreviewKeyDownEvent, _popupKeyDown, false);
        root.AddHandler(PreviewKeyUpEvent, _popupKeyUp, false);
        if (child is not null && !ReferenceEquals(child, root))
        {
            child.LayoutUpdated += OnOpeningLayoutUpdated;
            child.Loaded += OnOpeningLayoutUpdated;
            child.AddHandler(PreviewKeyDownEvent, _popupKeyDown, false);
            child.AddHandler(PreviewKeyUpEvent, _popupKeyUp, false);
        }
    }
    private void DetachPopupInput()
    {
        LayoutUpdated -= OnOpeningLayoutUpdated;
        DiscardOpeningKeys();
        if (_inputRoot is { } root)
        {
            root.RemoveHandler(PreviewKeyDownEvent, _popupKeyDown);
            root.RemoveHandler(PreviewKeyUpEvent, _popupKeyUp);
        }
        if (_inputScope is { } scope && !ReferenceEquals(scope, _inputRoot))
        {
            scope.LayoutUpdated -= OnOpeningLayoutUpdated;
            scope.Loaded -= OnOpeningLayoutUpdated;
            scope.RemoveHandler(PreviewKeyDownEvent, _popupKeyDown);
            scope.RemoveHandler(PreviewKeyUpEvent, _popupKeyUp);
        }
        _inputRoot = null; _inputScope = null;
        // Retain last identities through the original route, including close.
    }
    private void CompletePopupFocus()
    {
        if (_completingFocus || IsEditable || !IsDropDownOpen || XamlRoot is not { } root) return;
        _completingFocus = true;
        try
        {
            (_popup?.Child as FrameworkElement)?.UpdateLayout();
            if (!IsLoaded || !IsDropDownOpen || XamlRoot != root) return;
            int index = SelectedIndex >= 0 ? SelectedIndex : 0;
            if (ContainerFromIndex(index) is Control item && item.IsEnabled && item.IsLoaded && item.Focus(FocusState.Keyboard)) return;
            Focus(FocusState.Keyboard);
        }
        finally { _completingFocus = false; }
    }
}
