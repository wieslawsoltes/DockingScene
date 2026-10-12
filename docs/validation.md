# Validation

## Model and package gates

Both workflows enumerate every `tests/*Specs/*.csproj` executable under shell `pipefail`; failures cannot be hidden by logging. There are **22 suites** covering the immutable model, transactions, invalid input, layout, MVVM/source binding, serialization, vectors, tab allocation, bulk operations, caching, keyboard/window geometry, contextual host scope and magnetism. Seeded loops supplement named specifications; they are not additional browser scenarios.

`Uno application` compiles desktop, publishes the actual WebAssembly application and packs all six libraries at `0.1.0-alpha.13` from one source revision. Isolated NuGet consumers restore into a fresh cache: a plain .NET executable runs the five UI-independent packages, and an Uno consumer compiles the controls API without source-project references. They include the magnetism engine and both DockSite/StandardMdiHost options. Compilation does not certify native desktop UI behavior.

## Browser gates

Four independent Chromium runners consume the same exact-revision artifact. Three product suites use real pointer and keyboard input; the fourth is the focus stress runner. Read-only diagnostics observe geometry, state and performed work. JavaScript never invokes manager commands or replaces rendering. Every suite runs even if another fails, and any failure blocks deployment.

| Suite | Named product scenarios | Coverage |
| --- | ---: | --- |
| Docking | 28 | Editing/caret, docking/floating, MDI, policies, history, persistence, auto-hide, switcher and themes |
| Standalone tabs | 32 | Three lanes, six overflow modes, header geometry, close/pin/menus, factories, content lifetime and 2,002-tab workloads |
| Host, Metro and MDI | 69 | Retained geometry/pixels, immediate focus, selectors, MDI state/commands, contextual titles, independent hosts and pointer magnetism |

The source defines **129 product scenarios**, extending the preceding 122 with seven magnetism scenarios. Counts describe test definitions; the exact revision's result files and exit codes establish whether they passed.

The separate **100-cycle focus investigation** remains a required gate. It exercises native editor/body input through the packaged canvas boundary and is not counted as another 100 named product cases. Helper contracts cover trusted-pointer scoping, independent documents, idempotence, disposal and atomic clipped hit-target reads.

## Retained presentation and visual assertions

External MDI controls must target the host's primary document even when a tool is active. Frame-free maximize must release actual content space and remove caption hit targets without replacing editors or restore bounds. Repeated state/theme changes retain buttons and contextual roots. Minimized plans must be reused across activation-only changes. Closed/released hosts must remove stale targets and disable commands.

Actual PNG assertions inspect tabs, captions, status, hover and highlight paint. Theme round-trip checks require visible glyph ink in editor, Explorer and Properties fields in both Metro themes. Contextual titles test real data binding, local toggle state, explicit UIElement-data suppression, template replacement and reentrant release. Tool title tests include near/far coordinates and docked/auto-hide ownership transfer. Selected pixels and screenshots are not complete vendor pixel-equivalence certification.

## Magnetism and resize review

Sixteen MagnetismSpecs cover default/custom gaps, same/adjacent edges, perpendicular separation, stable ties, defensive copying, disabled behavior, opposite-edge anchors, constraints, reversal, invalid/overflow input and 1,000 seeded resizes. A warm loop performs 20,000 move/resize queries over 128 neighbors and requires zero managed per-query allocation; construction is excluded. The general geometry review adds a regression for single-axis resizing that previously modified an untouched overconstrained extent.

Seven physical-input cases exercise actual gap placement, provisional versus committed state, one undo entry, a single plan per gesture, Ctrl/zero bypass, custom gaps, right/left resize anchors, Escape, viewport-change cancellation and exclusion of minimized/other-host windows. Editor and caption identities must remain stable. Keyboard Move/Size scenarios remain unchanged and do not use pointer snapping.

These are targeted operation/allocation checks, not frame-rate, input-to-photon, power or hardware-GPU measurements. Gesture-plan queries still examine their captured neighbors; no unmeasured universal speedup is claimed.

## Deployment and exact source

After all main-branch gates pass, Pages deploys that build artifact. All four runners then verify the live build's `build-info.json` against the source commit and repeat the browser checks. Deployment success and post-deployment validation are separate statuses. Source snapshots, packages, contracts, screenshots and traces identify their tested revisions. Feature-branch compilation alone does not publish an app.

## Diagnostics and platform boundaries

`?diagnostics=1` enables synthetic editor text/caret/identity, focus automation ids, model metadata, clipped hit bounds and work counters. It is off by default; normal sessions do not run the periodic visual-tree diagnostic walk. Do not enable it around confidential content. Bounds are clipped by ancestors but do not model sibling occlusion, so tests require actual input-driven changes as well as geometry assertions.

CI renders actual Uno Skia through **ANGLE/SwiftShader software WebGL**. Native desktop runtime, detached windows, multiple monitors, touch/stylus/inertia, screen readers, RTL, other browsers and hardware-GPU performance need separate qualification. Complete API, behavior and visual parity remains unfinished; see [parity.md](parity.md).

Earlier investigations and retained coverage are documented in [maximized MDI](maximized-mdi.md), [MDI review](mdi-review.md), [individual policies](individual-mdi-policies.md), [contextual titles/host scope](mdi-context-and-scope.md), [tool title context](tool-title-context.md), [theme text](theme-text-review.md), [selector readiness](selector-readiness.md), [retained-host performance](retained-host-review.md), and [MDI magnetism](mdi-magnetism.md). Their historical totals do not replace the current exact-head results.
