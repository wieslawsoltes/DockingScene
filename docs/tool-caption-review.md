# Tool caption behavior, visibility and floating titles

## Reference and scope

The public Actipro floating-host reference documents `FloatingToolWindowContainerTitleBarDoubleClickMode`: `ToggleMaximized` is the default and `Dock` is the alternative. DockingScene previously docked a floating tool group unconditionally. The corrected hosted Uno behavior toggles the complete enclosing floating host, preserving its split root, window sizes and retained editors. Dock mode returns the clicked tool group to the main site and leaves any sibling group in its existing host. Docked and auto-hidden tool containers continue to float on double-click.

The Core package exposes `DockManager.InvokeToolCaptionDoubleClick`. It resolves current group ownership, checks the expected revision and delegates permissions to the same atomic model operations used elsewhere. Maximizing requires Resize on all host members; docking checks the clicked group, not an unrelated sibling. Locking, vetoes and stale identities reject without partial changes. Successful operations produce one notification/undo step. UI activation may separately update the active window before the gesture; it is not part of that model command.

## Retained native actions and spacing

`DockSite.ToolWindowsHaveCloseButtons`, `ToolWindowsHaveOptionsButtons` and `ToolWindowsHaveToggleAutoHideButtons` are dependency properties defaulting to true. The same options apply to docked/floating tool containers and auto-hide popup titles. Standalone `DockContainer.Update` accepts a `ToolCaptionOptions` value; its default keeps all actions.

Buttons are instantiated once. Visibility changes collapse their visual slots and remove them from pointer/focus input, while preserving their identities for subsequent re-enabling. Capability restrictions still disable a visible action. The title/context layout now reserves only the visible fixed-width action slots instead of an unconditional 69-unit minimum, giving long titles the reclaimed width. Existing drag/context surfaces and contextual content remain native controls.

These properties are presentation policies, not permission changes. Hiding the close/options button does not remove a permitted context-menu or programmatic command. They do not implement the full vendor per-window nullable property overrides. Native non-hosted caption minimize/maximize buttons remain a separate OS integration boundary and are not implied by these hosted controls.

## Primary floating captions

`FloatingHostCaption` in Core selects the most recently active document in a floating host, or its most recently active tool if there are no documents. Main-workspace focus and tools inside a mixed host cannot overwrite that primary document. The optional application title follows the document title with a configurable delimiter, default `" - "`. Tool-only hosts show the tool title without the application suffix. Empty/null application titles avoid an extra delimiter.

`DockSite.FloatingWindowTitle`, `FloatingWindowTitleDelimiter` and `GetFloatingDockHostTitle(hostId)` expose this behavior. Structural membership is cached weakly by immutable host-root identity; activation ranks and metadata come from the current snapshot. The controls cache formatted title strings by primary id/title and effective suffix/delimiter, so unrelated appearance refresh does not concatenate them again. Removing a host or replacing the manager releases the per-site entry. The independent Uno API is not a WPF binary-compatible DockHost/virtual override surface.

## Validation and remaining work

Window > Open tool caption sample is a real nested DockSite with a split tool host and a separate tabbed document host. Native pointer input exercises double-clicks, menus, action toggles, auto-hide/pin and document selection. Diagnostics read actual title text and identities; they never invoke the formatter or mutate the model. Thirteen model specifications and eight browser cases supplement all existing model, browser, pixel, focus and package gates. The source defines 26 model suites and 161 product scenarios, plus the independent 100-cycle focus test. Exact-head workflow results, not these definitions, establish success.

The warm caption-resolution test requires zero managed allocation for 10,000 repeated selections after membership/index warm-up. UI tests require unchanged title-format counts on repeated appearance toggles, unchanged global geometry counts, all retained button/editor identities, exactly reclaimed action-slot width and native caption pixels. This does not assert zero application/native rendering allocation, frame rate or hardware-GPU throughput. CI uses actual Uno Skia through SwiftShader software WebGL.

Native/detached windows, linked sites, instant floated-tab dragging, size-to-content, further contextual tab locations, full accessibility/RTL, full instance override APIs and certified vendor pixel equivalence remain unfinished. No proprietary source, templates or assets were copied.

References:
- https://www.actiprosoftware.com/docs/controls/wpf/docking/docking-window-features/floating-dock-hosts
- https://www.actiprosoftware.com/docs/controls/wpf/docking/docking-window-features/docking-window-capabilities
