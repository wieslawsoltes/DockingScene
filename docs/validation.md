# Validation

## Required gates

Both workflows enumerate every `tests/*Specs/*.csproj` under shell `pipefail`.
There are **29 model suites**. `Uno application` repeats these contracts, builds
desktop, publishes the real WebAssembly app, and packs all six libraries at
`0.1.0-alpha.21`. Isolated NuGet consumers use a fresh package cache, execute the
five non-UI libraries and compile the Uno API without source-project references.
Compilation does not certify native desktop UI behavior.

| Browser suite | Named product scenarios |
| --- | ---: |
| Docking | 28 |
| Standalone tabs | 32 |
| Host, Metro and MDI | 101 |
| Caption policies and group geometry | 21 |

The source defines **182 main-application product scenarios**, plus the separate **100-cycle
focus runner**. Counts describe definitions; exact-revision result files and exit
codes establish passing execution. The four product suites use actual pointer
and keyboard input; diagnostics only observe state and performed work. The fifth
runner stresses editor/body focus across the packaged canvas boundary. Every
runner executes independently and any failure blocks deployment. The required
aggregate `build` check certifies all browser jobs, not compilation alone.

Main-branch Pages deployment uses the validated artifact. All five runners then
repeat against the live URL and compare `build-info.json` with the tested commit.
Deployment and post-deployment validation are separate statuses. Feature-branch
success is not a deployment claim. Source ZIPs, package versions, contract logs,
screenshots and traces identify their revisions. Validate archives source before
running tests so a later failure does not erase recovery evidence.

## Model and interaction coverage

The suites cover immutable trees, seeded state transitions, capability rules,
atomic batches, vetoes, reentrancy, history, malformed/bounded input, serialization,
MVVM/source ownership, vector parsing, large tab workloads, geometry/cache identity,
keyboard moves/resizes, independent hosts and contextual templates. Browser tests
exercise editing/caret retention, docking/floating, persistence, MDI state,
auto-hide, switchers, menus, policies, themes and actual native controls.

Tabs have independent pinned/normal/preview lanes, six overflow modes, constrained
measured extents, header virtualization, factories, ownership, reorder cancellation
and drag edge scrolling. Retained-host checks cover geometry and auxiliary-control
reuse, stale queued projections, immediate typing and native selector readiness.

MDI checks cover system commands, title context templates, independent host
arrangement, minimized row overflow and reuse, frame-free maximize, external
primary commands, individual policies, keyboard geometry and reentrant completion.
Tool context tests cover Near/Far positioning, local state and auto-hide transfer.

Sixteen magnetism model cases include 1,000 seeded resizes, anchors, stable ties,
constraints, invalid/extreme geometry and a 20,000-query/128-neighbor zero-allocation
loop. Fourteen floating-drag cases cover complete-host versus partial detachment,
permissions, frozen display bounds, stale/veto/no-op/undo and mutable caller input.
Native tests exercise real gaps, bypass/disable, opposite-edge anchors, bounded
retained guides and settings/viewport cancellation.

Thirteen floating-layout cases compare full and frozen layout with 1,000 seeded
resizes at fractional DPI and 20,000 warm zero-allocation subtree arrangements.
Native scenarios verify provisional editor/group/splitter bounds, one final
commit, Escape, raw reversal, keyboard geometry, frame-only opt-out, color refresh
and callback-started replacement gestures. Thirteen MDI-scroll cases plus native
scenarios cover negative extents, mutually induced native scrollbars, reveal,
world-coordinate preservation, minimized rows and interrupted sessions.

Sixteen tool-caption cases test complete-host maximize/restore, clicked-group
Dock mode, capability distinctions, main-site focus isolation, title selection,
metadata, equal-revision cache isolation, concurrent reads and warm allocation.
Eight native scenarios cover real double-clicks, button-slot reclamation,
alternative commands, auto-hide, retained paint and title/delimiter changes.
Sixteen caption-inheritance cases cover 1,296 default/override combinations,
81 serializer combinations and 256 legacy missing/null/boolean combinations.
Ten native inheritance scenarios plus the suite revision/error check retain all
prior caption assertions and verify actual title/button space, selected-window
policies, disabled input, restore/undo and capture/Escape behavior.

Seventeen group-measurement cases cover four tab edges, true constrained minima,
opposite-edge cache reuse, nullable title envelopes, 500 independently calculated
seeded envelopes, frozen/full agreement, invalid input and overflow. Five native
cases verify editor dimensions, edge changes, retained content, undo, theme PNGs
and live resizing. Twelve new ToolTabVisibilitySpecs extend this with visible-only
single-tool chrome, membership, title independence, hidden-edge cache identity,
invalid policies, 500 more independent envelopes and frozen visibility. Four new
native scenarios verify reclaimed width/height, multi-tool strips, metrics reuse
and captured F4 visibility changes without stale commits.

## Isolated native image contracts

The additional `Native tab image contracts` workflow publishes a separate Uno
control fixture referencing the actual Controls library. Its ten native-input
contracts cover opt-in defaults, fixture pixels and measured slots, source
replacement, null/restore, pointer input through images, four tab placements,
theme and strip transitions, 2,002 shared sources, hosted document/tool policies,
floating/undo ownership and the exact revision/error gate. These are additional
to the 182 main-application scenarios and the 100-cycle focus runner.
The historical blocked application sample is not part of the deliverable.
See [native tab images](native-tab-images.md).

The sample visibility shortcut also has a before-first-menu and repeated-menu
physical-input regression; it supplements the unchanged captured-resize case.
See [shortcut review](tool-tab-shortcut-review.md).

## Allocation and pixel evidence

Strict warmed intervals cover 10,000 default-cache queries, 10,000 128-member
group measurements, 10,000 MDI-scroll measure/reveal operations, 10,000 primary
title queries and hidden-strip measurement/cache hits. A test-only no-inline
boundary excludes reporting/delegate construction; empty/explicit-allocation
controls must report zero/positive bytes. No baseline subtraction, tolerance,
retry-until-pass, GC suppression or runtime/JIT override is used. Cold planning,
output geometry and native content/text work are not included in warm-query
claims. See [allocation intervals](allocation-interval-review.md). Earlier
intermittent allocation failures remain historical evidence, not identified
allocator traces or a general zero-allocation guarantee.

PNG assertions inspect tab/caption/status backgrounds, highlights, action glyphs
and editor/Explorer/Properties text through theme round trips. Contextual templates,
caption buttons and editor identities are checked across state changes. These are
selected pixel assertions, not complete vendor-pixel-equivalence certification.

## Platform and diagnostic boundaries

CI renders actual Uno Skia through **ANGLE/SwiftShader software WebGL**. Native OS
windows, multi-monitor behavior, touch/stylus/inertia, screen readers, RTL, other
browsers and hardware-GPU performance require separate qualification.
`?diagnostics=1` enables synthetic editor text/caret/identity, automation bounds
and work counters; normal sessions do not run the periodic diagnostic walk. Do
not enable diagnostics around confidential documents. Bounds clip ancestors but
do not model sibling occlusion, so tests also require input-driven state changes.

Local JavaScript/helper/XML/source-tree checks are distinct from .NET/Uno runs.
The current local SDK download failed DNS resolution. Previous administrator
restrictions on local browser navigation were not bypassed. The blocked tab-image
application integration in PR #22 is unrelated and is not part of these tests.

## Detailed investigations

[Architecture](architecture.md), [compatibility](parity.md), [tabs](tab-layout.md),
[performance](performance-review.md), [retained hosts](retained-host-review.md),
[Metro fidelity](metro-fidelity.md), [theme text](theme-text-review.md),
[selector readiness](selector-readiness.md), [MDI review](mdi-review.md),
[maximized MDI](maximized-mdi.md), [individual MDI](individual-mdi-policies.md),
[context/host scoping](mdi-context-and-scope.md), [tool title context](tool-title-context.md),
[magnetism](mdi-magnetism.md), [floating drag](floating-drag-review.md),
[live floating layout](floating-live-layout.md), [scrolling](mdi-scrolling.md),
[tool captions](tool-caption-review.md), [capture repair](tool-caption-input-review.md),
[caption allocation](tool-caption-allocation-review.md), [inheritance](tool-caption-inheritance.md),
[vertical tabs](vertical-tab-geometry.md), [hidden strips](hidden-tool-tabs.md).
