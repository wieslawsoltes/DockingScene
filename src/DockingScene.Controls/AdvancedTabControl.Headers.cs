using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DockingScene.Controls;

public partial class AdvancedTabControl
{
    private HeaderVisual CreateHeader(AdvancedTabItem item)
    {
        HeaderCreationCount++;
        var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var text = DockChrome.Text(item.Header + (item.IsModified ? " *" : ""), Theme);
        if (item.IsPreview) text.FontStyle = Windows.UI.Text.FontStyle.Italic;
        var label = new Grid();
        label.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); label.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        if (item.IsReadOnly) { var icon = DockChrome.Glyph(DockGlyphs.Lock, Theme.MutedText, 11); icon.Margin = new(0, 0, 5, 0); label.Children.Add(icon); }
        label.Children.Add(text); Grid.SetColumn(text, 1);
        var select = DockChrome.Button(item.Header, "tab:" + item.Id, Theme, () => Select(item.Id, true)); select.Content = label;
        select.HorizontalAlignment = HorizontalAlignment.Stretch; select.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        select.Height = double.NaN; select.VerticalAlignment = VerticalAlignment.Stretch;
        select.AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            if (e.GetCurrentPoint(select).Properties.IsMiddleButtonPressed) { if (CanTabsCloseOnMiddleClick) RequestClose(item); e.Handled = true; return; }
            if (e.GetCurrentPoint(select).Properties.IsLeftButtonPressed) { Select(item.Id, true);
                if (CanTabsDrag)
                {
                    if (TabPointerPressed is { } host) host(this, new(item.Id, select, e));
                    else BeginTabDrag(item, e);
                } }
        }), true);
        select.RightTapped += (_, e) => { ContextRequested?.Invoke(item.Id, select); e.Handled = true; };
        select.DoubleTapped += (_, e) => { Promote(item); e.Handled = true; };
        grid.Children.Add(select);
        var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal }; Grid.SetColumn(actionsPanel, 1); grid.Children.Add(actionsPanel);
        var buttons = new List<Button>();
        void Add(string name, string id, string glyph, Action action)
        {
            var button = DockChrome.Button(name, id, Theme, action, glyph); button.Width = 23; button.Padding = new(4);
            button.Height = double.NaN; button.VerticalAlignment = VerticalAlignment.Stretch; actionsPanel.Children.Add(button); buttons.Add(button);
        }
        if (!IsToolGroup && (item.IsPinned || HasTabPinButtons && !item.IsPreview))
            Add((item.IsPinned ? "Unpin " : "Pin ") + item.Header, "pin:" + item.Id, item.IsPinned ? DockGlyphs.Unpin : DockGlyphs.Pin, () =>
            {
                bool pinned = !item.IsPinned;
                if (PinRequested is { } handler) handler(item.Id, pinned); else { item.IsPinned = pinned; item.IsPreview = false; }
            });
        if (item.IsPreview && CanTabsPromote) Add("Keep " + item.Header + " open", "keep-open:" + item.Id, DockGlyphs.Pin, () => Promote(item));
        if (HasTabCloseButtons && item.CanClose) Add("Close " + item.Header, "close:" + item.Id, DockGlyphs.Close, () => RequestClose(item));
        return new(new Border { Child = grid, BorderThickness = new(0, 0, 1, 0), BorderBrush = DockChrome.Brush(Theme.Border) }, select, text, buttons);
    }
    private void RequestClose(AdvancedTabItem item)
    {
        if (!item.CanClose || !Items.Contains(item)) return;
        var args = new TabClosingEventArgs(item); TabClosing?.Invoke(this, args);
        if (args.Cancel) return;
        if (CloseRequested is { } handler) handler(item.Id); else Items.Remove(item);
    }
    private void Promote(AdvancedTabItem item)
    {
        if (item.IsPreview && !CanTabsPromote) return;
        if (TabDoubleTapped is { } handler) handler(item.Id); else if (item.IsPreview) item.IsPreview = false;
    }
    private void PaintHeader(HeaderVisual header, AdvancedTabItem item)
    {
        bool selected = item.Id == _selectedId;
        string color = selected ? IsGroupActive && !IsToolGroup ? Theme.Accent : Theme.Surface : Theme.InactiveTab;
        if (!selected && item.TabColor is { Length: 7 or 9 } custom && custom[0] == '#' && custom.Skip(1).All(Uri.IsHexDigit)) color = custom;
        string foreground = selected && IsGroupActive && !IsToolGroup ? Theme.AccentText : selected && IsToolGroup ? Theme.Accent : Theme.Text;
        if (header.Paint == (color, foreground)) return;
        header.Paint = (color, foreground);
        header.Border.Background = DockChrome.Brush(color); header.Select.Background = DockChrome.Brush(color);
        header.Select.Foreground = DockChrome.Brush(foreground); header.Text.Foreground = DockChrome.Brush(foreground);
        foreach (var button in header.Actions)
        {
            button.Background = DockChrome.Brush(color); button.Foreground = DockChrome.Brush(foreground);
            if (button.Content is Microsoft.UI.Xaml.Shapes.Path icon) icon.Stroke = DockChrome.Brush(foreground);
        }
    }
}
