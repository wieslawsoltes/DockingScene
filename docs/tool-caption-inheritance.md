# Inherited tool-caption presentation

## API and defaults

All four `DockWindow` presentation values are `bool?`: `HasTitleBar`,
`HasCloseButton`, `HasOptionsButton`, and `HasToggleAutoHideButton`. A null value
inherits the corresponding site default; explicit false hides and explicit true
shows. `DockSite.ToolWindowsHaveTitleBars`, `ToolWindowsHaveCloseButtons`,
`ToolWindowsHaveOptionsButtons`, and `ToolWindowsHaveToggleAutoHideButtons` all
default to true. Site defaults do not mutate registered window metadata.

```csharp
var site = new DockSite { ToolWindowsHaveOptionsButtons = false };
site.Manager.Register(new DockWindow("explorer", "Explorer", WindowKind.Tool)
{
    HasOptionsButton = true, // This tool opts back in.
    HasTitleBar = null,      // Inherit the site's title setting.
    HasCloseButton = false
}).ThrowIfFailed();
// Reset an instance value to inheritance without changing other windows.
site.Manager.UpdateWindow("explorer", w => w with { HasCloseButton = null })
    .ThrowIfFailed();
```

`ToolCaptionSettings` in Core is a value-type resolver independent of Uno.
`ToolCaptionOptions` in Controls retains its three positional button arguments
and three-value deconstruction, adds an init-only title default, and exposes the
same resolution. A parameterless `new ToolCaptionSettings()` or
`new ToolCaptionOptions()` enables all defaults; `default(T)` has the ordinary
all-false struct meaning. MVVM properties and source descriptors project the
nullable values without overwriting engine-owned placement or geometric state.

## Native layout and behavior

Each retained tool container resolves its selected window, not the active window
of another host. The same path is used for auto-hide popups. Hidden buttons are
collapsed, are not tab stops, and occupy no native slot. Other button objects,
caption controls, contextual roots and editor instances are retained. Hiding
the complete title collapses the native title row and releases its context
presentation; it does not hide document tabs or change a window's placement.

Visibility never grants permission. Close and pin actions still honor effective
capabilities and layout locking; model closing vetoes are independent. Hidden
presentation does not disable a permitted direct command or context-menu path.
An explicit visible action with a denied capability remains disabled.

Button-only policy edits do not affect the layout geometry cache. Nullable title
values are compared after resolution: changing null to the existing default
reuses geometry, while changing the effective title setting invalidates it.
Measurement uses the maximum resolved title envelope of a group so selecting a
tab cannot oscillate its minimum constraints. The selected native title row
still follows that individual window. Main arrangement, live floating subtree
layout and splitter previews receive the same site title default. Changing the
site's title setting cancels in-progress geometry gestures even though there is
no model revision change; stale live plans cannot commit obsolete geometry.

## Persistence and source compatibility

The existing bounded source-generated JSON serializer preserves null, true and
false. Missing fields inherit the site default, and legacy explicit booleans
keep their meaning. Malformed values reject the complete restore. Layout schema
version remains 1; application content is still not serialized.

This alpha intentionally changes `DockWindow.HasTitleBar` from `bool` to `bool?`.
Boolean assignment remains source-compatible, but a consumer reading a bool must
resolve it, for example `window.HasTitleBar ?? site.ToolWindowsHaveTitleBars`.
Recompile package consumers against the matching six alpha.18 libraries; this
is not a WPF binary-compatibility promise. The original DockWindow three-argument
constructor and deconstruction remain supported.

## References, validation, and boundaries

The publicly documented nullable per-window HasTitleBar/HasOptionsButton pattern
is the behavioral reference:
https://www.actiprosoftware.com/docs/controls/wpf/docking/docking-window-features/docking-window-capabilities

The per-window close and pin flags are independent DockingScene presentation
extensions. Their names do not imply an identical vendor WPF property surface.
No proprietary source, theme resource, font or icon asset is redistributed.

Sixteen model specifications and the separate eleven-case Chromium runner cover
the contracts above, using real pointer/keyboard actions and read-only native
state. Isolated package consumers compile the original three-argument options
API and execute nullable policy serialization/resolution. Existing suites remain
unchanged. Test definitions and static checks are not runtime pass evidence;
use the result artifacts and exact revision gates described in validation.md.

This increment does not add detached OS hosts, monitor-aware geometry, linked
sites, size-to-content, the other contextual-tab locations, complete RTL or
screen-reader parity, or certified vendor pixel equivalence. Snapshot cache
and allocation microbenchmarks do not establish whole-application frame rates.
