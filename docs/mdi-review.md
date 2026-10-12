# Standard MDI state, chrome and movement review

This increment follows the public [Actipro Standard MDI reference](https://www.actiprosoftware.com/docs/controls/wpf/docking/workspace-mdi-features/standard-mdi). The independent Uno implementation adds host minimize/maximize policies, window-icon and title context menus for state/close commands, explicit minimized restore, and retained caption buttons. It does not claim the entire vendor WindowControl or StandardMdiHost surface.

## State and movement correctness

Minimizing and maximizing preserve the model's `MdiBounds`, which are its normal restored bounds. A minimized caption cannot be dragged or resized into replacing those bounds. It exposes a distinct Restore action; double-click restores it. Maximized frames also restore their normal bounds. `CanWindowsMinimize` and `CanWindowsMaximize` are StandardMdiHost dependency properties; DockSite adapts them through `CanStandardMdiWindowsMinimize` and `CanStandardMdiWindowsMaximize`. They control UI actions, not direct engine state changes. Capabilities and layout locking still guard the engine commands.

`MoveMdiWindow` requires Drag rather than Resize capability, preserves size, rejects nonfinite/stale/minimized movement and is undoable. Resizing still requires Resize. Hosted window movements retain the starting revision; a changed snapshot cancels the provisional gesture or rejects its commit. Escape is captured by a temporary root preview-key handler and restores the original rectangle without emitting a move command. Pointer cancellation, lost capture and unload also cancel.

## Minimized layout

The prior modulo-width position calculation placed later captions on top of the first row and could partially overlap adjacent captions. `MinimizedWindowLayout.Arrange` instead computes complete columns and bottom-up rows in O(window count). Caller order is retained. Caption width shrinks only when the host is narrower than one caption; spacing, required height and all coordinates are checked for finite arithmetic.

The result reports `RequiredHeight` and `IsOverflowing`. If all rows cannot fit vertically they extend above the viewport instead of silently overlapping. The current host clips them; they remain available from the application Activate/switcher menus. Full scrollable MDI virtual extents remain unimplemented. This boundary is explicit rather than presenting an impossible fit. `ArrangeMinimizedWindows` recomputes the presentation without overwriting normal bounds or adding history entries.

## Chrome and lifetime

The system icon and four caption action controls are created once per WindowControl and updated in place. Content clicks and resize grips activate their frame as well as caption clicks. Provisional movement retains its stacking order, and unchanged projections do not reset a captured frame to its prior model coordinates. Compiled tab-button templates keep state-dependent fills accurate while retaining native Button input. The icon and title right-click open Restore/Minimize/Maximize/Close menus. Menu callbacks recheck live flags before dispatch. Move/Size keyboard system-menu sessions, owner-window icon customization, maximized-frame hiding and size-to-content are separate unfinished API areas.

StandardMdiHost checks factory-side revision/owner changes and skips queued work after its content has been released. It exposes rejected operations instead of silently discarding a failed command. Its public Update call remains the snapshot-projection contract; this increment does not add automatic ownership of arbitrary external source collections.

## Validation

Eleven model specifications cover minimized geometry, invalid/extreme input, 500 seeded nonoverlap cases, restored bounds, move-only capabilities and stale/minimized rejection. Eight additional real-input browser scenarios exercise retained caption identities and actual caption paint, system maximize/restore, minimized restore, presentation policies, Escape cancellation, committed movement, multiple minimized rows and title-menu closing. The existing 81 browser scenarios remain mandatory, for 89 total.

All six packages use 0.1.0-alpha.7. Isolated package consumers execute the minimized-layout/move APIs and compile the new host options. Work-count/identity checks are not FPS or hardware-GPU benchmarks. The runner renders the actual Uno Skia application through SwiftShader software WebGL. Native desktop runtime, advanced scrollable MDI, detached hosts, full accessibility/RTL and vendor pixel certification remain outside this increment. Previously blocked profile/bookmark work is not included or retried.

## Prior live-site focus finding

The first post-merge run for PR #7 deployed the correct revision and passed 80/81 scenarios. Its sole failure was again the repeated typing precondition before F8, with no focused control after a body click. The original failing test remains unchanged. Selected-content pointer release now performs one guarded post-route reconciliation when native cleanup leaves no focus owner. It does not poll or steal a non-null focus owner; content identity, XamlRoot and unload guards prevent an obsolete callback from reclaiming a replaced document. The existing child-directed toolbar focus test remains mandatory. This correction requires final-head and live verification; earlier passing subsets do not establish it.

The minimized-window screenshot also exposed a stale sample toolbar selector: commands changed the MDI model while the selector still displayed its startup value. Metro projection now synchronizes that existing selector with the model, caching the control lookup until the toolbar is rebuilt. Browser assertions cover Standard mode after a menu command and Tabbed mode after reset. This changes no selection policy or source model and creates no replacement toolbar controls.
