# Maximized MDI frames, external commands and retained layouts

This increment follows the public [Standard MDI frame-hiding behavior](https://www.actiprosoftware.com/docs/controls/wpf/docking/workspace-mdi-features/standard-mdi#hiding-frames-when-maximized) and [StandardMdiHost command API](https://www.actiprosoftware.com/docs/controls/wpf/api/actiprosoftware.windows.controls.docking.standardmdihost). All code and visuals are independent; no vendor assets are redistributed.

## External maximized document controls

`StandardMdiHost.AreMaximizedWindowFramesVisible` defaults to true, preserving prior callers. Set it to false to reclaim the complete title/frame region of maximized children. DockSite adapts this through `AreStandardMdiMaximizedWindowFramesVisible`. The reusable `WindowControl.IsFrameVisible` property removes frame borders, title height and resize hit targets without detaching or recreating content. Restoring the child reinstates the normal frame and the previously stored normal bounds.

Applications can bind to `PrimaryWindow`, `IsExternalMaximizedUIRequired`, and `MinimizePrimaryWindowCommand`, `MaximizePrimaryWindowCommand`, `RestorePrimaryWindowCommand`, and `ClosePrimaryWindowCommand`. Properties implement `INotifyPropertyChanged`; command instances remain stable and notify only when availability changes. Queries re-evaluate changed snapshots even before a consumer explicitly refreshes the projection. Host Update remains the external model-to-view contract.

The primary window is the active hosted document. When a tool pane takes focus it remains the most recently active document within that host, not the tool. Hidden/unregistered IDs cannot become a command target. `PrimaryWindow` is a DockingScene immutable `DockWindow` model, not the vendor's WPF `DockingWindow` control; no binary or complete API compatibility is implied.

The independent `MdiPrimaryState` helper computes the same selection and availability for non-Uno consumers. Commands respect current capabilities, layout locking and host minimize/maximize presentation settings, while restore remains available when only the presentation settings disable maximize/minimize. An executed command targets the captured manager/id under an expected revision; no old callback silently targets a replacement manager.

The sample uses three retained buttons and a truncated title at the right of its menu bar. Activating a tool does not make those buttons operate on the tool. Window → Toggle maximized window frames switches presentation without changing the persisted layout or content. All four commands are also available in the Window menu. Releasing the MDI host disables external commands. Open frame system menus are dismissed and obsolete menu callbacks invalidated on release/unload or frame hiding.

## Performance and invalidation

`MinimizedWindowLayoutCache` retains one successful minimized-caption layout. Its signature includes ordered IDs, viewport origin/size, caption size and gap. It compares IDs without creating a replacement array on a hit; misses copy IDs before calculating, so subsequent caller mutation cannot alias the cache. A rejected input preserves the last valid result. Clear releases the entry but preserves lifetime counters. Synchronized calls are supported, but the caller must not mutate its list during a call.

The MDI adapter uses this cache and reports build/reuse counters. Primary command state is cached by snapshot, hosted-ID sequence identity and options. Unchanged ID lists no longer replace the adapter's stored array. This avoids redundant result/command work; it does not eliminate the adapter's linear reconciliation or all temporary arrays, nor does it claim hardware frame-rate improvements.

## Evidence and boundaries

Fourteen UI-independent specifications exercise cache identity, caller mutation, all invalidations, rejected inputs, clearing/concurrent reuse, primary selection, close fallback, external-frame requirements, host policies and lock/capability limits. Six actual browser cases check content-space reclamation, retained content and command buttons, tool-focus routing, locked commands, light/dark presentation, target-specific actions and no additional minimized-layout builds for repeated activation and color-only themes. Package-only consumers compile the new Uno APIs and execute the neutral helpers.

These additions do not supply scrollable MDI overflow, automatic all-document maximization, size-to-content, per-window nullable presentation overrides, complete native Move/Size sessions, detached hosts, linked sites, RTL/accessibility certification or full vendor templates. Tests render the actual Uno Skia application through software SwiftShader WebGL, not hardware-GPU benchmarking. Final source-revision logs, screenshots and live checks determine release readiness.
