# Validation

## Executable specifications

All `tests/*Specs/*.csproj` applications are dependency-light assertion suites returning a nonzero exit code on failure. CI runs every matching project with shell `pipefail`; a failed process cannot be hidden by `tee`.

| Suite | Coverage |
| --- | --- |
| DockingScene.Specs | Baseline engine, docking, capabilities, lifecycle, serialization and history contracts |
| DockingScene.LayoutSpecs | Split constraints, geometry, guide targeting and MDI arrangements |
| DockingScene.ParitySpecs | Malformed/null/deep/cyclic layouts, geometry overflow, metadata and serialization rejection |
| DockingScene.TransactionSpecs | Batches, rollback, stale revisions, close cancellation, observer reentrancy/order, preview replacement, directional closing |
| DockingScene.MvvmSpecs | Stable proxies, commands, metadata, content lifetime, dispatch, disposal, source collections and cancellation |
| DockingScene.VectorSpecs | Every built-in icon, SVG command grammar, smooth curves, arcs, numeric syntax, locale independence and input budgets |
| DockingScene.TabSpecs | Measured offsets, binary visible ranges, insertion positions, scrolling, large strips and drag index correction |

The suite summaries in each CI artifact are the authoritative counts for that commit. New tests must assert real behavior rather than merely inspect source strings.

## Uno browser validation

The workflow publishes the same `net10.0-browserwasm` application distributed to users, serves it under `/DockingScene/`, and opens it in Chromium. The harness explicitly waits for the splash to disappear and uses real pointer/keyboard input. Geometry and editor observations are read from the opt-in diagnostic snapshot; no test-only hook calls manager commands from JavaScript.

Scenarios include startup/paint, document creation without spurious dirty state, content identity through close/open, persistence restore, standard/tabbed MDI, layout locking, editor caret behavior, preview promotion/keep-open, measured tab reordering, Ctrl-drag floating, drag redocking, split resizing, auto-hide resizing/pinning, pinned close-others, switcher cancellation, theme/content retention and final reset.

Every scenario produces a pass/fail entry. Failures capture the actual screenshot and diagnostic state. Successful runs include workspace, standard MDI, floating and dark-theme screenshots. The trace archive can be opened with Playwright's trace viewer. UI tests do not certify complete Actipro compatibility.

Chromium is launched with ANGLE/SwiftShader software WebGL. This exercises the actual Skia-rendered application, but is **not** a hardware GPU benchmark. Desktop targets are compiled in CI; native OS window interaction, Windows/macOS runtime testing, multi-monitor behavior, touch/stylus, screen readers, RTL, Firefox/WebKit and visual comparison against Actipro require additional validation.

## Diagnostics and privacy

Normal browser sessions do not start periodic diagnostic capture. `?diagnostics=1` enables a read-only object containing window metadata, bounds and sample editor text/caret/instance observations. This is intended for local tests and the public demonstration's synthetic documents. Do not enable it around confidential content in a downstream app.

The diagnostics contain approximated hit bounds. A supplementary change to clip them by every ancestor viewport was blocked during upload and is **not** part of the repository implementation at this point. Tests must not treat off-viewport diagnostic rectangles as proof that a control was painted or clickable.

## Release gate

A release should be tied to a concrete commit with passing executable suites, successful desktop/browser builds and passing browser scenarios. Pages deployment runs only from a successful main-branch workflow. Preserve source snapshots and validation artifacts to make release claims auditable. Full parity must not be declared until the outstanding rows in `parity.md` are implemented and tested.
