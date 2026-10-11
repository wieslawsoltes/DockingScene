# Metro visual fidelity and retained chrome

## Reference and scope

This increment follows the public [Actipro Docking & MDI product examples](https://www.actiprosoftware.com/products/controls/wpf/docking), the published [Metro Light theme screenshot](https://files.actiprosoftware.com/images/blog-legacy/DockingMetroLightThemeUpdates.png), and the [AdvancedTabControl reference](https://www.actiprosoftware.com/docs/controls/wpf/docking/advanced-tab-control). The screenshot is a historical visual reference, not an assertion that every current vendor theme is identical. No vendor source, binary, image, icon or font is redistributed. All grippers and icons are independent vector geometry rendered by Uno.

The goals are a compact IDE-like layout, dotted tool-caption grips, restrained document-strip accents and contextual embedded buttons. This is not certified pixel parity, a WPF binary substitute, or the complete Actipro template/API surface. The sample retains its own text and project identity.

## Reusable control behavior

`AdvancedTabControl.AreTabEmbeddedButtonsAlwaysVisible` defaults to **true** to preserve existing DockingScene consumers. The sample explicitly sets the corresponding DockSite option to false. Contextual mode shows actions on the selected tab, pointer-over tab or keyboard-focused header. Pinned unpin and preview keep-open actions remain available. Hidden actions retain their layout slots so the label does not shift on hover, but do not receive pointer hits, tab focus or content-level automation exposure. This behavior does not execute new text measurement or lane allocation.

`CanTabsHighlightOnPointerOverWhenInactive` controls whether an inactive strip paints hover feedback. Active strips use the theme's existing hover brush. `HighlightThickness`, `HighlightBrushActive` and `HighlightBrushInactive` provide a retained, non-hit-test strip accent. Invalid negative, nonfinite or excessively large thickness values are rejected. DockSite can orient a one-unit document/content seam for each tab placement via `HasDocumentTabStripHighlight`.

`DockSite.HasToolTabCloseButtons` controls redundant close actions inside tool tabs. The sample hides them because its tool caption already exposes the close action. Existing callers retain the previous true default. These settings do not alter close capabilities or the model's cancellation rules.

`DockCaptionGrip` is a reusable control made from two retained dashed vector lines. Its visual count is independent of width; there is no element or geometry figure allocated per dot. Caption text and the grip both support the existing captured group docking gesture and context menu. The three caption buttons are now created once per tool container and updated in place for title, activation, capability, pin and theme changes. Event handlers resolve the current group rather than a stale selected window.

## Sample changes and issue fixes

The welcome document is a compact feature demonstration instead of a large landing page. The status bar uses the theme accent, separates its status text from document/MDI details, and truncates long messages rather than overflowing. The new Activate menu operates on currently open windows through manager commands. Appearance settings expose contextual buttons, tool-tab close buttons and the document accent without replacing the workspace.

Solution Explorer remains the actual Uno TreeView, with native expansion, keyboard navigation and selection. Compact row resources and vector folder/document labels replace decorative Unicode symbols. File invocation still opens a real retained editor. Theme changes update vector strokes as well as text.

The Properties view uses a compact two-column grid so its editable fields and Apply button fit the original lower-right pane. Its window selector now reconciles registration changes instead of holding the startup list forever. It preserves the selected id, avoids replacing field text on unrelated activation changes, and unsubscribes when its view is detached. Callbacks from a replaced manager cannot edit the new workspace. Applying metadata still goes through the engine and updates the bound editor's read-only state.

## Validation and performance interpretation

Ten additional real-input cases are composed into the retained-host browser suite. They inspect actual PNG pixels for the accent bar, gripper dots, tab hover and strip highlight; operate the grip with captured pointer input; test close-on-hover without activation; toggle presentation policies; open documents through the native compact tree; and edit/select newly registered windows in Properties. Light/dark screenshots are retained as evidence.

Work-count assertions require no new tab text measurements or lane plans for hover and highlight changes. Caption button identity is checked across activation and theme changes. All previous model, package and browser scenarios remain mandatory, including focus, coalesced content lifetime and 2,002-tab workloads. The isolated Uno package consumer compiles the new appearance API without source project references.

These are defined interaction and pixel samples, not exhaustive visual certification. They do not measure hardware FPS, input-to-photon latency or native desktop performance. CI runs the actual Uno Skia application with SwiftShader software WebGL. Full data-source/template parity, detached/native hosts, linked sites, advanced persistence, exact reopen behavior, RTL, accessibility and other browsers remain separate work. See `parity.md` and `validation.md` for the remaining boundaries and exact scenario counts.
