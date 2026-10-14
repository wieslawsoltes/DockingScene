# Vertical tab geometry and measurement review

## Correct physical constraints

The native left/right tab strip uses a 155 logical-unit cross-axis width. Previous
full-tree and floating-plan group measurement always reserved horizontal tab
height, leaving too little content width beside a vertical strip and too much
reserved height above/below it. Both paths now share a single-pass accumulator.
Horizontal placement adds tab height; vertical placement adds strip width.
Content minimums use the largest member minimum and maxima use the smallest
member maximum, clamped against the common minimum. Optional tool title height
uses the maximum resolved title envelope, independent of the selected tab.

The group cache key tracks the tab axis. Top/Bottom and Left/Right reuse their
geometric results while the native control updates which side contains content.
Changing axes rebuilds the layout; floating gesture snapshots stay immutable.
The native control and measurement reference DockMetrics.VerticalTabStripWidth.

## Public APIs and allocation scope

DockLayoutEngine.MeasureGroup(layout, group, metrics) returns content/chrome
minimum and maximum sizes without constructing or measuring application content.
It validates nonempty groups, presentation enums, numeric constraints and metrics;
invalid registry membership remains a caller error. It is not a complete layout
validator. Use DockManager for validated window ownership and structures.

DockMetrics.Default is immutable. Omitted-metrics calls to full layout, the
geometry cache and the floating-plan constructor reuse this value. A with-copy
can customize metrics without changing another caller's defaults. Full layout
still allocates its output geometry; only warmed cache hits and group measurement
are allocation-free. No zero-allocation claim is made for native text, painting,
source snapshot changes or cold frozen-plan construction.

## Reusable controls and interactive example

Window > Open tool caption sample > Instance policies > Tab layout and content
minimums opens constrained horizontal/vertical examples and four placement
commands. These are normal application transactions. The initial split requests
1% space, deliberately forcing the content minimum to govern placement. Showing
single-tool tabs makes the actual strip visible; Reset restores normal defaults.
Diagnostics read native group/tab/editor/frame state without invoking layout or
warming its caches. Browser checks use physical pointer and keyboard input.

## Validation and boundaries

Seventeen new model specifications include 500 independent seeded envelopes,
full/frozen plan agreement, opposite-edge cache identity, invalid/overflow input,
10,000 omitted-default cache hits and 10,000 measurements of 128-member groups.
Five new native scenarios cover actual widths/heights, edge placement, retained
editors and tabs, undo, theme PNGs and live-resize cancellation. Existing suites
and the separate 100-cycle focus gate remain. The isolated package consumer
exercises the exported measurement and default-cache APIs.

The native integration is pending exact-head CI when this document is written;
local JavaScript/XML/source checks are not .NET or native browser execution.

This corrects the existing physical-strip measurement, not complete Actipro/WPF
sizing equivalence. Measurement retains a conservative tab envelope even when a
single-tool strip is hidden. The width is not a new configurable setting.
Dynamic application-content measurement, size-to-content, custom tab/header
constraints, native/detached hosts, linked sites, full RTL/accessibility and
vendor-certified pixel parity remain separate. CI runs actual Uno Skia with
SwiftShader software rendering, not hardware-GPU benchmarking.
