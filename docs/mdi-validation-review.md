# MDI input validation review

The `b52907e` artifact passed 103 of 107 named browser scenarios and all 100 editor-focus cycles. Its failures were inspected individually; they were not treated as an overall pass.

The minimize-after-restore trace exposed an incoherent diagnostic snapshot. The model already said Normal, while the emitted minimize hit rectangle was still at the maximized caption's old coordinates (1031,70). The test physically clicked that stale coordinate, not the restored frame's minimize button. Diagnostic publication now flushes pending layout and defers a snapshot while DockSite has a queued or running projection. It then captures the model and hit rectangles together. This is opt-in observer consistency; it neither edits the model nor adds a delay to the pointer sequence. All existing state/content assertions remain.

The keyboard Move/Size suite inherited AreMaximizedWindowFramesVisible=false from the preceding frame-free suite. A layout reset intentionally preserves presentation policy, so two later cases attempted to click a correctly hidden caption and failed. Its setup now selects Show maximized window frames through the actual public menu. The disabled-action, cleanup and frame-free assertions themselves are unchanged.

The selector's immediate-key stress still requires the separate native popup routing correction. Its diagnostic snapshot additionally reports open/selected-index/forwarded-key counts, not typed key data. A complete final-head gate and post-deployment run are still required. See selector-input.md and validation-execution.md.
