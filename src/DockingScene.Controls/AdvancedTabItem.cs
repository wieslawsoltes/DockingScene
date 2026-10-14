using System.Diagnostics.CodeAnalysis;
using DockingScene.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DockingScene.Controls;

/// <summary>Bindable standalone tab content. The factory is evaluated once and the resulting visual is retained.</summary>
public partial class AdvancedTabItem : ContentControl
{
    private static DependencyProperty Property(string name, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] Type type, object? value) => DependencyProperty.Register(name, type, typeof(AdvancedTabItem), new PropertyMetadata(value, Changed));
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((AdvancedTabItem)sender).MetadataChanged?.Invoke((AdvancedTabItem)sender);
    public static readonly DependencyProperty IdProperty = Property(nameof(Id), typeof(string), "");
    public static readonly DependencyProperty HeaderProperty = Property(nameof(Header), typeof(string), "Document");
    public static readonly DependencyProperty IsPinnedProperty = Property(nameof(IsPinned), typeof(bool), false);
    public static readonly DependencyProperty IsPreviewProperty = Property(nameof(IsPreview), typeof(bool), false);
    public static readonly DependencyProperty IsModifiedProperty = Property(nameof(IsModified), typeof(bool), false);
    public static readonly DependencyProperty IsReadOnlyProperty = Property(nameof(IsReadOnly), typeof(bool), false);
    public static readonly DependencyProperty CanCloseProperty = Property(nameof(CanClose), typeof(bool), true);
    public static readonly DependencyProperty TabColorProperty = Property(nameof(TabColor), typeof(string), null);
    public string Id { get => (string)GetValue(IdProperty); set => SetValue(IdProperty, value); }
    public string Header { get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public bool IsPinned { get => (bool)GetValue(IsPinnedProperty); set => SetValue(IsPinnedProperty, value); }
    public bool IsPreview { get => (bool)GetValue(IsPreviewProperty); set => SetValue(IsPreviewProperty, value); }
    public bool IsModified { get => (bool)GetValue(IsModifiedProperty); set => SetValue(IsModifiedProperty, value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public bool CanClose { get => (bool)GetValue(CanCloseProperty); set => SetValue(CanCloseProperty, value); }
    public string? TabColor { get => (string?)GetValue(TabColorProperty); set => SetValue(TabColorProperty, value); }
    public Func<object?>? ContentFactory { get; set; }
    internal event Action<AdvancedTabItem>? MetadataChanged;
    public AdvancedTabItem() => Id = Guid.NewGuid().ToString("N");
    private bool _creatingContent;
    public void EnsureContent()
    {
        if (ContentFactory is not { } factory) return;
        if (_creatingContent) throw new InvalidOperationException("A tab content factory cannot recursively create itself.");
        _creatingContent = true;
        try
        {
            var content = factory();
            Content = content;
            if (ReferenceEquals(ContentFactory, factory)) ContentFactory = null;
        }
        finally { _creatingContent = false; }
    }
}
public partial class DockingWindow : AdvancedTabItem
{
    public DockWindow Model { get; private set; } = new("window", "Window", WindowKind.Document);
    public void Update(DockWindow model)
    {
        Model = model; Id = model.Id; Header = model.Title; IsPinned = model.DisplayMode == TabDisplayMode.Pinned;
        IsPreview = model.DisplayMode == TabDisplayMode.Preview; IsModified = model.IsModified;
        IsReadOnly = model.IsReadOnly; CanClose = model.Capabilities.HasFlag(DockCapabilities.Close); TabColor = model.TabColor;
    }
}
public sealed class DocumentWindow : DockingWindow { }
public sealed partial class ToolWindow : DockingWindow { }
public sealed class TabPointerEventArgs(string id, FrameworkElement source, PointerRoutedEventArgs pointer) : EventArgs
{
    public string Id { get; } = id;
    public FrameworkElement Source { get; } = source;
    public PointerRoutedEventArgs Pointer { get; } = pointer;
}
