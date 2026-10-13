# Validation

## Model and package gates

Both workflows enumerate every `tests/*Specs/*.csproj` executable under shell `pipefail`; failures cannot be hidden by logging. There are **28 suites** covering the immutable model, transactions, invalid input, layout, MVVM/source binding, serialization, vectors, tab allocation, bulk operations, caching, keyboard/window geometry, contextual host scope, magnetism, floating-drag plans, live floating subtree layout and MDI scrolling. Seeded loops supplement named specifications; they are not additional browser scenarios.

`Uno application` compiles desktop, publishes the actual WebAssembly application and packs all six libraries at `0.1.0-alpha.19` from one source revision. Isolated NuGet consumers restore into a fresh cache: a plain .NET executable runs the five UI-independent packages, and an Uno consumer compiles the controls API without source-project references. They include the magnetism engine, floating plans, retained feedback, and DockSite/StandardMdiHost settings. Compilation does not certify native desktop UI behavior.

## Browser gates

Five independent Chromium runners consume the same exact-revision artifact. Four product suites use real pointer and keyboard input; the fifth is the focus stress runner. Read-only diagnostics observe geometry, state and performed work. JavaScript never invokes manager commands or replaces rendering. Every suite runs even if another fails, and any failure blocks deployment.

| Suite | Named product scenarios | Coverage |
| --- | ---: | --- |
| Docking | 28 | Editing/caret, docking/floating, MDI, policies, history, persistence, auto-hide, switcher and themes |
| Standalone tabs | 32 | Three lanes, six overflow modes, header geometry, close/pin/menus, factories, content lifetime and 2,002-tab workloads |
| Caption policies and group geometry | 16 | Instance/site inheritance, physical title/button space, vertical constraints, content retention, persistence, themes and capture cancellation |
| Host, Metro and MDI | 101 | Retained geometry/pixels, immediate focus, selectors, MDI state/commands, contextual titles, independent hosts, pointer magnetism and floating-drag review |

The source defines **177 product scenarios**: the preceding 172 plus five physical-input vertical group geometry cases. Counts describe test definitions; the exact revision's result files and exit codes establish whether they passed.

The separate **100-cycle focus investigation** remains a required gate. It exercises native editor/body input through the packaged canvas boundary and is not counted as another 100 named product cases. Helper contracts cover trusted-pointer scoping, independent documents, idempotence, disposal and atomic clipped hit-target reads.

## Retained presentation and visual assertions

External MDI controls must target the host's primary document even when a tool is active. Frame-free maximize must release actual content space and remove caption hit targets without replacing editors or restore bounds. Repeated state/theme changes retain buttons and contextual roots. Minimized plans must be reused across activation-only changes. Closed/released hosts must remove stale targets and disable commands.

Actual PNG assertions inspect tabs, captions, status, hover and highlight paint. Theme round-trip checks require visible glyph ink in editor, Explorer and Properties fields in both Metro themes. Contextual titles test real data binding, local toggle state, explicit UIElement-data suppression, template replacement and reentrant release. Tool title tests include near/far coordinates and docked/auto-hide ownership transfer. Selected pixels and screenshots are not complete vendor pixel-equivalence certification.

## Magnetism and resize review

Sixteen MagnetismSpecs cover default/custom gaps, same/adjacent edges, perpendicular separation, stable ties, defensive copying, disabled behavior, opposite-edge anchors, constraints, reversal, invalid/overflow input and 1,000 seeded resizes. A warm loop performs 20,000 move/resize queries over 128 neighbors and requires zero managed per-query allocation; construction is excluded. The general geometry review covers single-axis resizing that previously modified an untouched overconstrained extent.

Seven physical-input MDI cases exercise actual gap placement, provisional versus committed state, one undo entry, a single plan per gesture, Ctrl/zero bypass, custom gaps, right/left resize anchors, Escape, viewport-change cancellation and neighbor exclusion. Editor and caption identities remain stable. Keyboard Move/Size scenarios remain unchanged and do not use pointer snapping.

## Floating drag review

Fourteen FloatingDragSpecs cover full split-host identity and geometry, Drag versus Float capabilities, partial detach, self-exclusion, stale snapshots, locking/veto, no-op/undo, invalid inputs, mutable caller arrays and frozen displayed bounds. Eight additional browser cases exercise the real nested DockSite: exact gaps, full original preview dimensions, retained split roots/editors/frames, bypass/disable, resize anchors, bounded guide reuse, settings/viewport cancellation and stationary tab clicks that build no magnetism plan. All preceding scenarios remain required.

The overlay counters reject repeated visual construction; the magnetism allocation checks exclude planning. Docking-guide hit testing still allocates model records and traverses candidates. These are targeted operation/allocation checks, not frame-rate, input-to-photon, power or hardware-GPU measurements.

## Live floating layout

Thirteen FloatingLayoutSpecs compare against full DockLayoutEngine, including 1,000 seeded resizes and fractional DPI, and require no managed allocation for 20,000 warm subtree arrangements. Eight additional real-input cases verify actual editor/group/splitter geometry before release, commit/undo, Escape rollback, raw reversal, keyboard Move/Size, frame-only opt-out, color refresh, reentrant completion and invalidation. The full global layout is not rebuilt by provisional frame changes. See [live floating layout](floating-live-layout.md). Geometry counters exclude native text/measure work and diagnostics; they are not a hardware-GPU speed claim.

## Deployment and exact source

After all main-branch gates pass, Pages deploys that build artifact. All five runners then verify the live build's `build-info.json` against the source commit and repeat the browser checks. Deployment success and post-deployment validation are separate statuses. Source snapshots, packages, contracts, screenshots and traces identify their tested revisions. Feature-branch compilation alone does not publish an app.

## Diagnostics and platform boundaries

`?diagnostics=1` enables synthetic editor text/caret/identity, focus automation ids, model metadata, clipped hit bounds and work counters. It is off by default; normal sessions do not run the periodic visual-tree diagnostic walk. Do not enable it around confidential content. Bounds are clipped by ancestors but do not model sibling occlusion, so tests require actual input-driven changes as well as geometry assertions.

CI renders actual Uno Skia through **ANGLE/SwiftShader software WebGL**. Native desktop runtime, detached windows, multiple monitors, touch/stylus/inertia, screen readers, RTL, other browsers and hardware-GPU performance need separate qualification. Complete API, behavior and visual parity remains unfinished; see [parity.md](parity.md).

Earlier investigations and retained coverage are documented in [maximized MDI](maximized-mdi.md), [MDI review](mdi-review.md), [individual policies](individual-mdi-policies.md), [contextual titles/host scope](mdi-context-and-scope.md), [tool title context](tool-title-context.md), [theme text](theme-text-review.md), [selector readiness](selector-readiness.md), [retained-host performance](retained-host-review.md), [MDI magnetism](mdi-magnetism.md) and [floating drag](floating-drag-review.md). Historical totals do not replace the current exact-head results.

## Scrollable Standard MDI

Thirteen `MdiScrollSpecs` cover negative coordinates, interdependent scrollbars, viewport anchoring, minimally revealing frames, minimized overflow, invalid/bounded geometry, 1,000 seeded layouts and 10,000 allocation-checked geometry/reveal queries. Eight new native-input scenarios cover scrollbar arrows, world-coordinate preservation, off-screen activation, cache/control retention, scrolled movement/undo, interrupted-gesture cancellation, maximize/restore, themes/toggle lifetime and wheel access to minimized overflow. These extend the preceding 145 product cases to **153**, with **93 host** cases; all prior assertions and the separate 100-cycle focus gate remain.

## Tool captions and floating titles

Sixteen ToolCaptionSpecs cover complete-host maximize/restore, clicked-group docking, main-site focus isolation, group/host capability distinctions, auto-hide, stale/invalid input, veto/lock/undo, document/tool title selection, metadata and warm resolution allocation. Eight additional native-input cases check title double-clicks, hidden action geometry, unchanged caption/editor identities, alternative commands, auto-hide popup settings, primary document title/delimiter updates and retained action paint. They extend the 153-case baseline to 161, with 101 host cases. Definition counts are not pass claims; exact-head results establish execution. See [tool caption review](tool-caption-review.md).


## Inherited per-window title and button presentation

Sixteen CaptionPolicySpecs exercise all 1,296 instance/default combinations,
81 nullable serialization combinations, 256 legacy missing/null/boolean
combinations, malformed-input rejection, source and MVVM projection, retained
identity, undo/veto, capability independence, effective-value geometry caching,
floating measurement, and 100,000 warmed allocation-free value resolutions.
These are model specifications, not browser or vendor-conformance counts.

The independent `caption-policies-runner.mjs` adds ten native-input behavior
cases plus a revision/error gate. It checks reclaimed 23-pixel button slots and
title-row height, selected-window inheritance, per-window exceptions to site
defaults, retained editors/buttons, auto-hide pinning, disabled action focus
state, JSON restore/undo, and caption capture/Escape/double-click. It joins both
the pre-deployment and live matrix; none of the 101 existing host cases or any
other existing gate was removed. Keeping this new suite independent avoids
adding its execution time to the existing host runner's critical path.

The primary-caption cache keeps the exact zero-byte warm-query assertion and
adds equal-revision isolation, concurrent-host reads, and unknown-host rejection.
The allocator responsible for the earlier intermittent result was not identified;
source changes and test definitions are not substituted for exact-head execution.
See [caption inheritance](tool-caption-inheritance.md) and the two caption input
and allocation review documents for the source-level changes.


## Vertical group geometry and default-query allocation

Seventeen GroupMeasurementSpecs cover all four tab edges, correct cross-axis chrome,
minimum-clamped splitters, opposite-edge cache reuse, inherited title envelopes,
conflicting member maxima, invalid input and finite overflow. Five hundred seeded
measurements are compared with an independent multi-pass oracle. Full and frozen
floating geometry must agree at fractional DPI.

Two strict allocation checks require zero managed bytes for 10,000 warmed
omitted-metrics cache queries and 10,000 measurements of a 128-member group.
Construction and native content/text costs are excluded. No hardware-GPU claim
follows from these CPU-side tests. The package-only consumer calls the new API.

Five added native-input scenarios check actual editor/group dimensions, left/right
content movement, Top/Bottom and Left/Right cache reuse, stacked-height reclamation,
undo and editable-content retention, dark PNG pixels and live resizing/Escape.
They run inside the existing caption-policy runner before its revision/error gate,
both before deployment and against the deployed app. No prior gate is removed.
See [vertical tab geometry](vertical-tab-geometry.md). Definitions are not pass
claims: exact-head CI results establish execution.
