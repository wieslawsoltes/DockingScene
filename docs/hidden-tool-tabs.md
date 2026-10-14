# Visible tool-tab geometry

A single tool strip hidden by `DockSite.ToolWindowsSingleTabLayoutBehavior` now
reserves no tab width or height. The native control already hid the strip; the
full-tree and floating-subtree measurements previously retained a conservative
strip envelope, unnecessarily clamping splitter positions. This closes that
specific alpha.19 limitation without changing the stored layout or content sizes.

`DockMetrics.ToolWindowsSingleTabLayoutBehavior` exposes the same distinction to
UI-independent consumers. Its default is Show to preserve existing standalone
measurement. DockSite passes its live setting (default Hide). Two or more tools
retain a strip; document groups ignore the tool-only setting. Tool title visibility
is resolved independently. Width/height constraints still include real borders,
visible titles and the common content minimum/maximum envelope.

The geometry cache reuses rectangles when a hidden one-tool strip changes edges.
Membership, numeric metrics, visible strip axes and constraints remain cache keys.
DockSite retains one immutable metrics instance across unchanged renders and
shares it with splitter previews and floating plans. Splitter previews now use
the same rasterization scale as final layout. Visibility changes cancel captured
geometry before old coordinates can commit, just like title-height changes.

Window > Open tool caption sample > Instance policies > Tab layout and content
minimums offers Show/Hide commands. F4 toggles this sample's single-tool setting,
including during a captured resize. These are ordinary application commands;
diagnostics observe actual controls and never invoke measurement or warm caches.

Validation definitions: twelve ToolTabVisibilitySpecs include all four edges,
multiple-member/document cases, inherited titles, minimum-clamped splits,
cache identity, frozen/full agreement, malformed settings, 500 independent
seeded envelopes and strict 10,000-call allocation checks. Four new native-input
scenarios exercise visible dimensions, retained content, membership, color-only
refresh and captured-resize cancellation. The previous suites and their assertions
remain. Package-only validation exercises the added metrics property.

The initial model and complete native revisions have separate CI results. Local
XML/JavaScript/source-tree validation is not a .NET or native browser pass. The
local SDK download failed DNS resolution, and earlier managed local-browser
restrictions were not bypassed; runtime validation uses existing GitHub CI.

Primary public behavior reference:
https://www.actiprosoftware.com/docs/controls/wpf/api/actiprosoftware.windows.controls.docking.singletablayoutbehavior

This is an independent Uno implementation of visible-strip sizing. It does not
claim WPF binary/template compatibility, custom content size-to-content,
native/detached OS hosts, linked sites, complete accessibility/RTL or vendor pixel
certification. Tests render Uno Skia using SwiftShader, not hardware GPU timing.
The unrelated, partially published tab-image PR #22 is not part of this feature.
