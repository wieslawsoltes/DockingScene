# Tab layout, overflow and reusable control behavior

The tab control remains a real Uno control. It renders measured text, vector button glyphs and retained content using the host's Uno renderer. The tab allocator in `DockingScene.Layout` does not depend on Uno and can be reused by another renderer.

## Three independent lanes

`TabLayoutEngine.Arrange` groups measurements into pinned, normal and preview lanes while retaining each item's original index. Pinned tabs occupy the leading edge; previews occupy the trailing edge. The normal lane fills the remaining span, including the empty spacer when there are no normal tabs. Scrolling the normal lane does not move the other two lanes.

If pinned and preview content cannot fit, the allocator reserves a usable normal slot and allocates the special lanes proportionally. Those lanes have independent offsets as well. If shrink mode is selected, widths are reduced globally *before* lane reservation, so the special lanes cannot starve normal tabs when all minimum extents can fit.

Shrinking uses water-filling: the longest tabs are capped first while short natural labels keep their size. No tab is shrunk below its declared minimum. When the sum of minima exceeds the viewport, the model explicitly remains overflowing rather than reporting an impossible fit. This also preserves fixed-size tabs.

## Overflow choices

| `TabOverflowBehavior` | User interaction |
| --- | --- |
| `None` | No overflow controls or wheel scrolling; lane offsets remain zero. |
| `Menu` | A menu lists all tabs. Selecting a hidden tab reveals it without changing item order. |
| `Scroll` | Pointer wheel and backward/forward buttons move overflowing lanes. |
| `ScrollWithMenu` | Scrolling and the all-tab menu are available together. |
| `Shrink` | Feasible widths are shrunk to fit. The selected tab can still be revealed if minimum constraints force overflow. |
| `ShrinkWithMenu` | Shrinking plus a menu for accessing any tab. |

A compressed header reserves a label selection target. Embedded actions that do not fit collapse, with close actions taking precedence over pin actions. Context menus, middle-click (when permitted), keyboard access and the overflow menu remain available according to the configured mode.

The alpha preserves the previous `ScrollWithMenu` default for document containers and standalone controls. Tool groups default to `Shrink`. These defaults are configuration choices, not a claim that every Actipro default or template is identical.

## Standalone usage

```csharp
using DockingScene.Controls;
using DockingScene.Layout;

var tabs = new AdvancedTabControl
{
    OverflowBehavior = TabOverflowBehavior.ShrinkWithMenu,
    MinTabExtent = 60,
    MaxTabExtent = 240,
    HasTabPinButtons = true
};
tabs.Items.Add(new AdvancedTabItem
{
    Id = "editor",
    Header = "Editor.cs",
    ContentFactory = () => new Microsoft.UI.Xaml.Controls.TextBox
    {
        AcceptsReturn = true,
        Text = "// Created only when selected"
    }
});
tabs.TabClosing += (_, e) => e.Cancel = e.Item.IsModified;
tabs.MenuOpening += (_, e) => e.Menu.Items.Add(
    new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Application action" });
```

Standalone close, pin/unpin and preview-promotion actions update the caller-owned `Items` collection or its metadata when no corresponding host handler is attached. `TabClosing` is raised before closing and can veto both button and middle-click requests. `CanClose` is always respected. `MenuOpening` exposes the overflow menu before presentation and supports cancellation.

Within `DockSite`, the adapter directs these actions into the validated manager instead. Docking content and identities therefore remain under the same engine transaction rules.

`IsTabStripVisible`, `HasTabCloseButtons`, `HasTabPinButtons`, `CanTabsPromote`, `CanTabsCloseOnMiddleClick`, `MinTabExtent`, `MaxTabExtent` and `OverflowBehavior` are dependency properties. Set minimum and maximum extents to the same value for fixed horizontal tab sizes. Vertical strips keep horizontal label orientation with theme-height rows; rotated tab content is not implemented.

## DockSite settings

`DocumentTabOverflowBehavior` and `ToolTabOverflowBehavior` configure the relevant groups, including hosted floating groups. `HasDocumentTabPinButtons` adds pin buttons to normal document tabs. Pinned document tabs always have an unpin action when there is room.

`ToolWindowsSingleTabLayoutBehavior` defaults to `Hide`. Setting it to `Show` restores the strip for singleton tool groups. Hiding a tab strip changes the content layout but does not recreate or dispose the selected editor. These are application presentation settings; they are not persisted in the engine's layout JSON.

## Keyboard behavior

`DockManager.ActivateTabByDigit` interprets digits 1-8 as filtered positions and digit 9 as the last matching tab. The DockSite maps Ctrl+digit to normal tabs, Ctrl+Alt+digit to pinned tabs and Ctrl+Shift+digit to preview tabs. Ctrl+Alt+Down opens the document list; Alt+- opens the active document context menu. A browser may reserve some shortcuts before WebAssembly receives them.

Arrow/Home/End navigation inside a tab header follows visual lane order. Editor caret navigation remains outside the header handlers and does not switch documents.

## Virtualization and ownership

Measurements are cached per item identity and presentation key. Metadata changes remeasure only affected headers; theme/extent policy changes invalidate the relevant cache. Lane allocation is reused while measurements, extent and overflow policy are unchanged. Scrolling reuses that plan and realizes only the intersecting header interval plus 100 logical units of overscan per lane, using `TabStripMetrics` binary searches. Each lane has a real Uno clip; diagnostic bounds obey those clips.

Only selected content factories are invoked. Switching overflow modes, moving a tab between lanes, changing strip placement and hiding/showing the strip retain the content object. Standalone controls do not dispose application-owned content on close.

## Browser demonstration and scope

Choose **Tab options → Open standalone tab playground** in the sample. Its 20 synthetic tabs expose all six modes, fixed/measured extents, four placements, strip visibility, a close veto and an application-added menu entry. Tests operate these actual controls with pointer and keyboard input and observe opt-in diagnostics; there is no JavaScript model mutation hook.

This increment does not implement full Actipro templates, slanted/overlapping borders, rotated content, layout animations, complete native accessibility peers or detached OS hosts. It does not include the separate layout-profile model update that was blocked during upload. The existing persistence and exact reopen-position limitations remain documented in `parity.md`.

## Standalone reordering and diagnostics

`CanTabsDrag` enables real pointer reordering within a display-mode lane. `ReorderTab(id, finalIndex)` offers the same operation directly. `TabReordering` can veto the mutation; `TabReordered` reports its final indexes. Escape, capture loss, unload or intervening collection changes cancel the drag. Content identity is preserved. Cross-lane promotion, edge-autoscroll and detached/cross-site transfers remain separate scope.

`TextMeasurementCount`, `HeaderLayoutCount`, `HeaderCreationCount` and `RealizedHeaderCount` expose the performed work without claiming a hardware frame rate. The performance playground and tests use these counters to reject redundant remeasurement and unbounded header realization.
