# Individual MDI presentation policies

`DockWindow.CanStandardMdiMinimize` and `CanStandardMdiMaximize` default to true. They complement the existing host-wide settings, following the documented individual-window behavior in Actipro's Standard MDI reference.

Window caption actions and primary-window commands combine the individual flag with the host flag, Resize capability and layout locking. Disabling a document's maximize action also disables its title double-click. Other documents remain unaffected. Minimized or maximized documents still have Restore when Resize is allowed; the UI cannot strand a programmatically maximized document merely because its maximize button is hidden.

These flags are presentation policy, not a new engine permission. Direct `DockManager.SetMdiState` retains its existing capability checks and can change state despite a hidden UI action. Window menu callbacks resolve the latest flags before issuing UI-originated commands. Consumers set the metadata through `DockManager.UpdateWindow`, the source descriptor, or the corresponding `DockWindowViewModel` properties. This is the independent model-first API, not a claim of the vendor's entire XAML property surface.

Both flags round-trip in DockingScene JSON. Older schema-1 JSON without these optional fields loads with true defaults. No content or application state is serialized. Geometry caches ignore these non-geometric flags; captions update existing buttons rather than recreating editors or frames. Source binding projects both flags while preserving ownership and validated transactions.

The sample Window menu contains individual primary-document toggles. Twelve model specifications cover default/intersection behavior, independent documents, capabilities, restore, programmatic changes, current/legacy JSON, MVVM/source projection, undo, value-equal no-ops and geometry cache reuse. Three physical-input browser cases check actual button disappearance, disabled double-click, a different document remaining enabled, programmatic maximize/restore, host intersection and unchanged caption/editor identities. There are no JavaScript mutation hooks.

Reference: https://www.actiprosoftware.com/docs/controls/wpf/docking/workspace-mdi-features/standard-mdi . Public documentation distinguishes individual `CanStandardMdiMaximize`/`CanStandardMdiMinimize` presentation settings from programmatic state changes. This increment does not implement native/detached windows, complete templates, RTL/accessibility or certified vendor pixel parity. CI uses Uno Skia with SwiftShader software WebGL, not a hardware-performance benchmark.

Package version: all six libraries advance together to `0.1.0-alpha.10`. This increment extends the merged popup/window-keyboard work rather than maintaining another stacked implementation of it.
