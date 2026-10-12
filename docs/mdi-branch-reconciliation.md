# MDI branch reconciliation and selector focus

PRs #9 and #10 independently implemented the same maximized-MDI feature. This merge preserves both commit histories, uses the reusable `StandardMdiControlBar` from #10 rather than the earlier app-only toolbar, and keeps the `MdiPrimaryState` public API and its complete fourteen-case contract suite from #9. Both primary-state APIs share one resolver. Ordinary outside tool groups cannot become a primary document, but a tool deliberately moved into a document group remains eligible.

The minimized-caption algorithms have equivalent inputs/results; one copied-signature, single-entry cache remains. The second suite retains additional concurrent-request coverage. Selected-id arrays are retained when unchanged, so the host's primary-state cache can reuse its input signature rather than invalidating on every projection.

The older branch's system-menu lifetime protection is retained and strengthened. Hiding/releasing a frame invalidates open menu callbacks. Replacing a host manager clears obsolete frame controls; frame event callbacks also check the original manager and registered frame identity. A stale retained frame cannot command a newly registered same-id document.

The preceding #10 run passed all docking, MDI and focus-stress checks but failed the inspector's immediate Home/Arrow/Enter sequence. Its screenshot showed an open native ComboBox still on the old selection. `DockComboBox` keeps the native Uno selector and templates, completes popup layout and selected-item focus during DropDownOpened, and does not synthesize or intercept keys. The existing failing scenario is unchanged; twelve additional fresh-workspace cycles send keys immediately after the opening click, without waiting for focus or popup geometry. The fix remains subject to that final browser gate.

No claim of native OS hosting, hardware GPU throughput, complete Actipro API/template/pixel parity, or a local browser pass is made. Local browser navigation is blocked by administrator policy and .NET is not installed here; execution evidence comes from repository CI. Previously blocked persistence/bookmark changes are not retried.

References: the public Actipro Standard MDI reference and Uno's ComboBox template/selection implementation. See `docs/maximized-mdi.md` for the delivered command surface and remaining limits.
