using DockingScene.Core;
using Microsoft.UI.Xaml;

namespace DockingScene.Controls;

public partial class DockSite
{
    public static readonly DependencyProperty ToolWindowsHaveTitleBarsProperty = DependencyProperty.Register(nameof(ToolWindowsHaveTitleBars), typeof(bool), typeof(DockSite), new PropertyMetadata(true, ToolTitlePolicyChanged));
    public bool ToolWindowsHaveTitleBars { get => (bool)GetValue(ToolWindowsHaveTitleBarsProperty); set => SetValue(ToolWindowsHaveTitleBarsProperty, value); }
    private static void ToolTitlePolicyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var site = (DockSite)sender;
        // A title-height change invalidates pointer geometry even when the immutable
        // model revision did not change (unlike a button-only presentation change).
        site.CancelInteraction();
        site.CancelFloatingGeometry();
        site.QueueRender();
    }
    public static readonly DependencyProperty ToolWindowsHaveCloseButtonsProperty = DependencyProperty.Register(nameof(ToolWindowsHaveCloseButtons), typeof(bool), typeof(DockSite), new PropertyMetadata(true, TabsChanged));
    public static readonly DependencyProperty ToolWindowsHaveOptionsButtonsProperty = DependencyProperty.Register(nameof(ToolWindowsHaveOptionsButtons), typeof(bool), typeof(DockSite), new PropertyMetadata(true, TabsChanged));
    public static readonly DependencyProperty ToolWindowsHaveToggleAutoHideButtonsProperty = DependencyProperty.Register(nameof(ToolWindowsHaveToggleAutoHideButtons), typeof(bool), typeof(DockSite), new PropertyMetadata(true, TabsChanged));
    public static readonly DependencyProperty FloatingToolWindowContainerTitleBarDoubleClickModeProperty = DependencyProperty.Register(
        nameof(FloatingToolWindowContainerTitleBarDoubleClickMode), typeof(FloatingWindowTitleBarDoubleClickMode), typeof(DockSite),
        new PropertyMetadata(FloatingWindowTitleBarDoubleClickMode.ToggleMaximized, DoubleClickModeChanged));
    public static readonly DependencyProperty FloatingWindowTitleProperty = DependencyProperty.Register(nameof(FloatingWindowTitle), typeof(string), typeof(DockSite), new PropertyMetadata(null, TabsChanged));
    public static readonly DependencyProperty FloatingWindowTitleDelimiterProperty = DependencyProperty.Register(nameof(FloatingWindowTitleDelimiter), typeof(string), typeof(DockSite), new PropertyMetadata(" - ", TabsChanged));
    public bool ToolWindowsHaveCloseButtons { get => (bool)GetValue(ToolWindowsHaveCloseButtonsProperty); set => SetValue(ToolWindowsHaveCloseButtonsProperty, value); }
    public bool ToolWindowsHaveOptionsButtons { get => (bool)GetValue(ToolWindowsHaveOptionsButtonsProperty); set => SetValue(ToolWindowsHaveOptionsButtonsProperty, value); }
    public bool ToolWindowsHaveToggleAutoHideButtons { get => (bool)GetValue(ToolWindowsHaveToggleAutoHideButtonsProperty); set => SetValue(ToolWindowsHaveToggleAutoHideButtonsProperty, value); }
    public FloatingWindowTitleBarDoubleClickMode FloatingToolWindowContainerTitleBarDoubleClickMode
    {
        get => (FloatingWindowTitleBarDoubleClickMode)GetValue(FloatingToolWindowContainerTitleBarDoubleClickModeProperty);
        set { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); SetValue(FloatingToolWindowContainerTitleBarDoubleClickModeProperty, value); }
    }
    public string? FloatingWindowTitle { get => (string?)GetValue(FloatingWindowTitleProperty); set => SetValue(FloatingWindowTitleProperty, value); }
    public string? FloatingWindowTitleDelimiter { get => (string?)GetValue(FloatingWindowTitleDelimiterProperty); set => SetValue(FloatingWindowTitleDelimiterProperty, value); }
    private ToolCaptionOptions ToolCaptionPresentation => new(ToolWindowsHaveCloseButtons, ToolWindowsHaveOptionsButtons, ToolWindowsHaveToggleAutoHideButtons) { HasTitleBar = ToolWindowsHaveTitleBars };
    private static void DoubleClickModeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (!Enum.IsDefined((FloatingWindowTitleBarDoubleClickMode)args.NewValue)) throw new ArgumentOutOfRangeException(nameof(args));
        ((DockSite)sender).CancelInteraction();
    }
    public IEnumerable<(string GroupId, double Width)> ToolCaptionWidths
    {
        get
        {
            foreach (var view in _groups.Values)
                if (view.Group is { Kind: GroupKind.Tools } group) yield return (group.Id, view.CaptionAvailableWidth);
            if (_autoPopup?.Group is { } auto) yield return (auto.Id, _autoPopup.CaptionAvailableWidth);
        }
    }
    private readonly Dictionary<string, FloatingCaptionEntry> _floatingCaptions = new(StringComparer.Ordinal);
    public long FloatingCaptionFormatCount { get; private set; }
    /// <summary>Resolve a registered floating host's caption using its own primary
    /// document, not an unrelated tool or document in the main workspace.</summary>
    public string GetFloatingDockHostTitle(string hostId) => ResolveFloatingCaption(Manager.Layout, hostId);
    private string ResolveFloatingCaption(DockLayout layout, string hostId)
    {
        var primary = FloatingHostCaption.GetPrimaryWindow(layout, hostId);
        string? suffix = primary.Kind == WindowKind.Document ? FloatingWindowTitle : null;
        string? delimiter = string.IsNullOrEmpty(suffix) ? null : FloatingWindowTitleDelimiter;
        if (_floatingCaptions.TryGetValue(hostId, out var old) && old.Id == primary.Id && old.Title == primary.Title && old.Suffix == suffix && old.Delimiter == delimiter)
            return old.Formatted;
        string formatted = FloatingHostCaption.Format(primary, suffix, delimiter);
        _floatingCaptions[hostId] = new(primary.Id, primary.Title, suffix, delimiter, formatted);
        FloatingCaptionFormatCount++; return formatted;
    }
    private readonly record struct FloatingCaptionEntry(string Id, string Title, string? Suffix, string? Delimiter, string Formatted);
    private void InvokeToolCaption(DockContainer container)
    {
        if (!container.IsLoaded || container.Group is not { Kind: GroupKind.Tools } group) return;
        var manager = Manager;
        bool belongs = _groups.TryGetValue(group.Id, out var retained) && ReferenceEquals(retained, container) || ReferenceEquals(_autoPopup, container);
        if (!belongs) return;
        CancelInteraction();
        if (!ReferenceEquals(manager, Manager)) return;
        Submit(manager.InvokeToolCaptionDoubleClick(group.Id, FloatingToolWindowContainerTitleBarDoubleClickMode, manager.Layout.Revision));
    }
}
