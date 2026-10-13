using System.Runtime.CompilerServices;
using DockingScene.Core;
using DockingScene.Layout;
using Microsoft.UI.Xaml;

namespace DockingScene.Controls;

public partial class DockSite
{
    private readonly ConditionalWeakTable<AdvancedTabControl, object> _tabAdapters = new();
    private static void TabsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((DockSite)d).QueueRender();
    public static readonly DependencyProperty DocumentTabOverflowBehaviorProperty = DependencyProperty.Register(nameof(DocumentTabOverflowBehavior), typeof(TabOverflowBehavior), typeof(DockSite), new PropertyMetadata(TabOverflowBehavior.ScrollWithMenu, TabsChanged));
    public static readonly DependencyProperty ToolTabOverflowBehaviorProperty = DependencyProperty.Register(nameof(ToolTabOverflowBehavior), typeof(TabOverflowBehavior), typeof(DockSite), new PropertyMetadata(TabOverflowBehavior.Shrink, TabsChanged));
    public static readonly DependencyProperty ToolWindowsSingleTabLayoutBehaviorProperty = DependencyProperty.Register(nameof(ToolWindowsSingleTabLayoutBehavior), typeof(SingleTabLayoutBehavior), typeof(DockSite), new PropertyMetadata(SingleTabLayoutBehavior.Hide, TabsChanged));
    public static readonly DependencyProperty HasDocumentTabPinButtonsProperty = DependencyProperty.Register(nameof(HasDocumentTabPinButtons), typeof(bool), typeof(DockSite), new PropertyMetadata(false, TabsChanged));
    public TabOverflowBehavior DocumentTabOverflowBehavior { get => (TabOverflowBehavior)GetValue(DocumentTabOverflowBehaviorProperty); set { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); SetValue(DocumentTabOverflowBehaviorProperty, value); } }
    public TabOverflowBehavior ToolTabOverflowBehavior { get => (TabOverflowBehavior)GetValue(ToolTabOverflowBehaviorProperty); set { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); SetValue(ToolTabOverflowBehaviorProperty, value); } }
    public SingleTabLayoutBehavior ToolWindowsSingleTabLayoutBehavior { get => (SingleTabLayoutBehavior)GetValue(ToolWindowsSingleTabLayoutBehaviorProperty); set { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); SetValue(ToolWindowsSingleTabLayoutBehaviorProperty, value); } }
    public bool HasDocumentTabPinButtons { get => (bool)GetValue(HasDocumentTabPinButtonsProperty); set => SetValue(HasDocumentTabPinButtonsProperty, value); }

    /// <summary>Read-only enumeration of currently realized tab controls, including auto-hide.</summary>
    public IEnumerable<(string GroupId, AdvancedTabControl Control)> TabControls
    {
        get
        {
            foreach (var container in _groups.Values)
                if (container.Group is { } group) yield return (group.Id, container.Tabs);
            if (_autoPopup?.Group is { } autoGroup) yield return (autoGroup.Id, _autoPopup.Tabs);
        }
    }
    private void ApplyTabOptions()
    {
        ApplyMdiMagnetismOptions();
        foreach (var (groupId, tabs) in TabControls)
        {
            if (!_tabAdapters.TryGetValue(tabs, out _))
            {
                _tabAdapters.Add(tabs, new());
                tabs.PinRequested += (id, pinned) => Submit(Manager.SetPinned(id, pinned));
            }
            if (_groups.TryGetValue(groupId, out var view)) view.ToolTitleContextAlignment = ToolWindowsTitleBarContextContentAlignment;
            else if (_autoPopup?.Group?.Id == groupId) _autoPopup.ToolTitleContextAlignment = ToolWindowsTitleBarContextContentAlignment;
            ApplyTabAppearance(tabs);
            bool identityChanged = tabs.AutomationIdPrefix != groupId;
            tabs.AutomationIdPrefix = groupId;
            tabs.OverflowBehavior = tabs.IsToolGroup ? ToolTabOverflowBehavior : DocumentTabOverflowBehavior;
            tabs.HasTabPinButtons = !tabs.IsToolGroup && HasDocumentTabPinButtons;
            tabs.HasTabImages = tabs.IsToolGroup ? ToolWindowsHaveTabImages : DocumentWindowsHaveTabImages;
            tabs.IsTabStripVisible = !tabs.IsToolGroup || tabs.Items.Count != 1 || ToolWindowsSingleTabLayoutBehavior == SingleTabLayoutBehavior.Show;
            if (identityChanged) tabs.InvalidateTabPresentation();
        }
    }
    private AdvancedTabControl? ActiveDocumentTabs()
    {
        var layout = Manager.Layout;
        var group = layout.ActiveWindowId is { } id ? DockTree.GroupOf(layout, id) : null;
        if (group is { Kind: GroupKind.Documents } && _groups.TryGetValue(group.Id, out var active)) return active.Tabs;
        return _groups.Values.FirstOrDefault(view => view.Group?.Kind == GroupKind.Documents)?.Tabs;
    }
    private void ShowActiveDocumentMenu()
    {
        var tabs = ActiveDocumentTabs();
        if (tabs?.SelectedId is { } id) ShowWindowMenu(id, tabs);
    }
}
