# DockingScene

A modular docking and multiple-document workspace for **Uno Platform**, with a UI-independent .NET engine and real retained controls rendered by Uno Skia on desktop and WebAssembly.

**Status: working alpha, not full Actipro Docking & MDI parity.** This is an independent implementation, not a WPF binary replacement or an Actipro product. Native OS floating windows, linked-site transfers, advanced serialization behaviors, complete accessibility parity, and pixel-for-pixel comparison remain unfinished. See [the explicit compatibility matrix](docs/parity.md).

**[Open the browser demonstration](https://wieslawsoltes.github.io/DockingScene/)**. The main-branch workflow tests the application before deployment and verifies the live site's source commit afterward. A feature branch is not published until merged and validated.

## Packages

| Package | Responsibility | UI dependency |
| --- | --- | --- |
| `DockingScene.Core` | Immutable layout trees, capability policy, validated/cancellable transactions, preview documents, filtered keyboard navigation, undo/redo | None |
| `DockingScene.Layout` | Constrained layout, splitters, docking guides, independent pinned/normal/preview lanes, six tab-overflow policies, snapping and MDI arrangement | None |
| `DockingScene.Serialization` | Versioned bounded JSON, unknown-window policies, atomic file layout storage | None |
| `DockingScene.Mvvm` | Observable window projections, commands, stable identities and atomic collection synchronization | None |
| `DockingScene.Themes` | Palettes, metrics, strings, original vector icons and bounded SVG-path parsing | None |
| `DockingScene.Controls` | `DockSite`, standalone `AdvancedTabControl`, `DockingWindow`, `WindowControl`, `StandardMdiHost`, `StandardMdiControlBar`, `StandardSwitcher`, `DockDragOverlay` and containers | Uno Platform |

All six packages use **`0.1.0-alpha.18`** and are packed from the same commit. Isolated package-only consumers execute the five non-UI libraries and compile the controls separately from the source-project builds. The sample App composes these packages; it is not the docking engine. Application content is never embedded in layout persistence.

## Build and run

The repository pins Uno SDK **6.7.30** and uses **.NET 10**. Install the .NET 10 SDK and the platform prerequisites required by Uno's desktop target.

```sh
# All UI-independent executable specification suites
for project in tests/*Specs/*.csproj; do
  dotnet run --project "$project" -c Release || exit 1
done

# Desktop sample
dotnet run --project src/DockingScene.App -f net10.0-desktop -c Release

# WebAssembly sample
dotnet workload install wasm-tools
dotnet publish src/DockingScene.App -f net10.0-browserwasm -c Release -o artifacts/browser
mkdir -p artifacts/site/DockingScene
cp -a artifacts/browser/wwwroot/. artifacts/site/DockingScene/
python3 -m http.server 8765 --directory artifacts/site
```

Open `http://localhost:8765/DockingScene/`. The application is built with this base path, including for GitHub Pages; do not serve it at `/` without changing `WasmShellWebAppBasePath` and rebuilding.

## Use the engine and a DockSite

```csharp
using DockingScene.Core;
using DockingScene.Controls;
using DockingScene.Layout;

var manager = new DockManager();
manager.Register(new("editor", "Document.cs", WindowKind.Document)).ThrowIfFailed();
manager.Register(new("explorer", "Explorer", WindowKind.Tool),
    targetId: "workspace", side: DockSide.Right).ThrowIfFailed();

var site = new DockSite
{
    Manager = manager,
    DocumentTabOverflowBehavior = TabOverflowBehavior.Menu,
    ToolWindowsSingleTabLayoutBehavior = SingleTabLayoutBehavior.Hide,
    ContentFactory = window => new Microsoft.UI.Xaml.Controls.TextBox
    {
        AcceptsReturn = true,
        Text = window.Title
    }
};

// Content is created lazily. Moving or hiding retains the actual control.
manager.Float("editor").ThrowIfFailed();
manager.Undo().ThrowIfFailed();
```

`DockSite.Manager`, theme and tab presentation settings are dependency properties. The controls package is independent of the sample. See [architecture and API contracts](docs/architecture.md) for batches, closing vetoes, MVVM, threading, persistence and content ownership.

## Standalone tab controls

`AdvancedTabControl` supports three independently clipped tab lanes. Pinned tabs remain at the leading edge and preview tabs remain at the trailing edge while normal tabs scroll. Overflow can be disabled, menu-only, scroll-only, scroll plus menu, shrink-only, or shrink plus menu. Measured extents honor minima and maxima; equal limits produce fixed horizontal tab sizes. Shrinking keeps a clickable label instead of allowing embedded buttons to consume the whole header.

The control supports close cancellation, `CanClose`, optional middle-click closing, application customization of its overflow menu, pin/unpin, preview promotion, four placements and strip visibility without recreating selected content. Header virtualization does not instantiate off-screen document content.

Choose **Tab options → Open standalone tab playground** to try these features with 20 synthetic documents. The toolbar also configures document overflow and single-tool-tab visibility. Read [the tab-layout guide](docs/tab-layout.md) for reusable APIs, allocation details and boundaries.

## Performance and ownership

The engine caches immutable layout lookups by snapshot identity. Tab controls retain measured headers and lane plans across selection, scrolling and unrelated layout changes, and report measurement/realization counters for profiling. The playground includes 2,002-tab workloads, cancellable standalone reordering and content-lifetime examples. See [the performance review](docs/performance-review.md) for the measured workload, fixes, API contracts and remaining costs.

Application content is not disposed by default. Set `DockSite.DisposeContentOnUnregister = true` only for exclusively owned factory results. Close/hide continues to retain content. `DockLayoutStream.ReadAsync` adds bounded, cancellable JSON stream loading without taking ownership of the input stream.

## Docking interactions

Drag document tabs or tool-window captions to dock, split, reorder or float. Hold **Ctrl** while dragging to suppress docking; **Escape** cancels. Tool windows can be auto-hidden on four sides, resized from their popup's inner edge and pinned. Context menus provide preview promotion, pinning, group creation, tab placement and directional closing.

**Ctrl+Tab / Ctrl+Shift+Tab** cycle the DockSite MRU switcher; releasing Ctrl commits. **F8** opens it where a browser reserves Ctrl+Tab. **Ctrl+F4 / Ctrl+W** close the active window where the host permits these shortcuts. **Ctrl+1…8** selects normal document tabs; **Ctrl+9** selects the last normal tab. Add **Alt** for pinned tabs or **Shift** for preview tabs. **Ctrl+Alt+Down** opens the document list and **Alt+-** opens its context menu. Browser-reserved shortcuts are not guaranteed to reach WebAssembly applications.

The toolbar switches between tabbed and standard MDI. Standard MDI supports overlapping windows, minimize/maximize, cascade and horizontal/vertical/grid tiling. Layout save/load is available from the toolbar and File menu. The browser sample stores layout metadata in local storage, not document text.

## Validation and artifacts

`Validate` runs every `tests/*Specs` executable. `Uno application` repeats these contracts, builds desktop, publishes WebAssembly, packs all libraries, checks isolated NuGet consumers, and drives four product browser suites (docking, standalone tabs, retained hosts, and per-window caption policies) plus the independent 100-cycle focus runner. Failure in any runner blocks deployment. All five runners then check the deployed URL and verify `build-info.json`.

```sh
npm install --prefix tests/browser
cd tests/browser && npx playwright install chromium && cd ../..
BROWSER_URL=http://localhost:8765/DockingScene/ node tests/browser/run.mjs
BROWSER_URL=http://localhost:8765/DockingScene/ node tests/browser/tab-layout.mjs
BROWSER_URL=http://localhost:8765/DockingScene/ node tests/browser/host-review.mjs
```

Browser tests use actual pointer/keyboard input. Diagnostics are read-only; no JavaScript hook mutates the managed model. **Diagnostic capture is off by default.** The harness adds `?diagnostics=1`, exposing layout geometry and synthetic sample-editor observations, including text, for testing. Do not enable it around sensitive documents. Normal sessions do not perform the periodic diagnostic visual-tree walk.

Artifacts include source snapshots, browser distributions, all six alpha NuGet packages, contract logs, screenshots and Playwright traces. Read [validation scope](docs/validation.md) before interpreting a green check as complete product compatibility.

## Rendering and license

Uno Skia renders the actual controls and original vector icon geometry. GPU selection and fallback are the host's responsibility. Chromium CI explicitly uses **SwiftShader software WebGL**: these tests validate rendering and interaction, not hardware-GPU performance.

Project source is MIT licensed. Uno Platform, Skia and other dependencies retain their own licenses. No Actipro source, assemblies, proprietary assets or branding are included.

## Retained-host performance and interaction

Host geometry is reused across activation and non-geometric metadata changes. Splitter buttons, auto-hide rails, popup resize grips and empty-workspace presenters retain their visual identity until removed from the layout. A tab click restores the selected content's last focused control, and detached queued projections cannot reclaim transferred content. Standalone tabs support elapsed-time-based drag edge scrolling with a `CanTabsAutoScroll` toggle. The [retained-host review](docs/retained-host-review.md) documents cache inputs, focus behavior, real-input regressions and remaining costs.

## Metro visual fidelity

The sample uses contextual document actions, a thin document-strip accent, dotted tool-caption grips and an accent status bar. Solution Explorer and Properties use compact native layouts. Tool-caption buttons retain their identities across activation and theme changes, and hover-only actions do not remeasure tab headers. The Appearance menu exposes these policies without changing the underlying workspace. See [Metro fidelity](docs/metro-fidelity.md) for public references, reusable APIs, defaults and pixel-test boundaries.

## Standard MDI review

Window icons and title context menus expose state and close commands. Minimized captions wrap in nonoverlapping bottom-up rows and restore without overwriting normal bounds. Host minimize/maximize buttons are configurable, window movement uses its own capability, and caption controls remain retained across activation. Read [the MDI review](docs/mdi-review.md) for API contracts, overflow limits and validation.

## Frame-free maximized MDI

`StandardMdiHost.AreMaximizedWindowFramesVisible` can remove a maximized document's caption and border while retaining its editor and restore bounds. `StandardMdiControlBar` provides a reusable external title, icon and primary-window commands. The sample's Window menu exposes frame visibility, and its external commands remain scoped to MDI documents when a tool has focus. Minimized caption plans are cached across activation changes. See [maximized MDI](docs/maximized-mdi.md) for integration, performance boundaries and the current validation scope; this supersedes the alpha.7 limitation on maximized-frame hiding.

## Individual MDI actions

Per-document `CanStandardMdiMinimize` and `CanStandardMdiMaximize` metadata combine with host settings without affecting other documents or removing Restore. The Window menu demonstrates them; MVVM and source descriptors expose the same policies. See [individual MDI policies](docs/individual-mdi-policies.md).

## Contextual MDI titles and independent hosts

`DockingWindow.StandardMdiTitleBarContextContentTemplate` hosts ordinary Uno controls beside an MDI title. Configure the template and `DataContext` in `DockSite.WindowCreated`. The reusable `DockContextPresenter` retains the instantiated template when the data or activation changes. The sample Window menu demonstrates editable title content and status templates. Its independent-host sample exercises two `StandardMdiHost` controls sharing one manager; cascade/tiling commands now affect only the invoking host. See [contextual MDI and host scoping](docs/mdi-context-and-scope.md).

## Contextual tool titles

`ToolWindow.ToolWindowContainerTitleBarContextContentTemplate` adds retained native controls to a tool title. `DockSite.ToolWindowsTitleBarContextContentAlignment` selects Near or Far placement. The sample Appearance menu demonstrates Explorer title controls; their local state follows the tool through tab selection and auto-hide/pin transitions. See [tool title context](docs/tool-title-context.md).

## Standard MDI magnetism

Standard MDI pointer movement and resizing snap to sibling window edges with configurable gaps. `DockSite.MagnetismSnapDistance` defaults to 5 and `MagnetismGapDistance` to 1 logical unit; the standalone `StandardMdiHost` exposes the same options. Zero disables snapping and Ctrl temporarily bypasses it. The independent-host sample includes a magnetic layout demonstration. A renderer-independent `WindowMagnetism` plan supports allocation-free queries, and the controls retain one plan per gesture. See [MDI magnetism](docs/mdi-magnetism.md) for edge rules, tests and platform boundaries.

## Floating hosts and retained drag feedback

Hosted floating-window dragging and pointer resizing use the same snap/gap settings. A whole-host movement preserves its original dimensions, split layout, group identities and editor controls; detaching an individual document remains a separate operation with its own permission checks. The reusable `FloatingDragPlan` freezes source geometry and excludes source hosts from magnetic neighbors. It is created only after the pointer crosses the drag threshold, not for an ordinary tab click.

`DockDragOverlay` retains its preview and up to nine guide visuals across pointer updates instead of rebuilding them. The Window menu opens a floating-drag review sample with real split hosts and snapping controls. See [floating drag review](docs/floating-drag-review.md) for retained-identity tests, bounded visual reuse, cancellation and remaining native/live-content boundaries.

## Live floating layout

Floating frame movement and resizing now reflow their retained editors, tab containers and splitters while pointer or keyboard input is still active. One measured `FloatingLayoutPlan` and caller-owned buffers are reused for the entire gesture, without cloning the global layout or adding provisional model history. Escape restores the original content geometry. Other hosts remain untouched.

`DockSite.IsLiveFloatingWindowLayoutEnabled` defaults to `true`; set it to `false` for frame-only feedback around expensive application content. The floating review sample exposes this option and light/dark switching (F2). See [live floating layout](docs/floating-live-layout.md) for APIs, performance scope and validation.

## Scrollable Standard MDI

`StandardMdiHost.IsScrollingEnabled` defaults to false, retaining the constrained non-scrolling behavior. Enable it directly or use `DockSite.IsStandardMdiScrollingEnabled` to expose native horizontal/vertical scrollbars as child windows move outside the viewport, including negative coordinates. Activation reveals off-screen windows; `ScrollWindowIntoView` reveals without activation, and `ScrollTo` changes extent offsets without modifying layout history. Native editor content and its own scrolling remain independent.

Window > Open scrollable MDI sample demonstrates positive/negative windows, native scrolling, maximized restoration, themes and minimized-row overflow. The renderer-independent `MdiScrollLayout` computes ranges and maps between persisted world coordinates and viewport positions. See [MDI scrolling](docs/mdi-scrolling.md) for behavior and validation boundaries.

## Tool caption options and floating titles

`DockSite.ToolWindowsHaveCloseButtons`, `ToolWindowsHaveOptionsButtons` and `ToolWindowsHaveToggleAutoHideButtons` default to `true`. Hiding an action reclaims its slot and removes its hit/focus target without recreating the retained button or changing model capabilities. Standalone containers accept `ToolCaptionOptions` in `Update`.

Floating tool-container double-clicks now maximize/restore their entire floating host by default. `FloatingToolWindowContainerTitleBarDoubleClickMode = FloatingWindowTitleBarDoubleClickMode.Dock` instead docks the clicked tool group. The core `InvokeToolCaptionDoubleClick` method validates current ownership, capabilities and revisions and produces one undo step. Floating document captions combine their primary document with `FloatingWindowTitle` and `FloatingWindowTitleDelimiter`; unrelated tool activation does not replace the document title. `GetFloatingDockHostTitle` resolves the current title.

Window > Open tool caption sample demonstrates native title gestures, action visibility, split hosts and application captions. See [tool caption review](docs/tool-caption-review.md) for reference behavior, retained-control tests and remaining limitations.


## Inherited per-window tool captions

`DockWindow.HasTitleBar`, `HasCloseButton`, `HasOptionsButton` and `HasToggleAutoHideButton` are nullable presentation overrides. `null` inherits the corresponding `DockSite.ToolWindowsHave*` default; explicit `true` or `false` applies only to that window, including when tools share a selected tab group or move into an auto-hide popup. `ToolWindowsHaveTitleBars` defaults to `true`, as do the three existing button defaults. Hiding a title reclaims its row; hiding buttons reclaims their individual slots without replacing native controls or editors. Capabilities, layout locking and closing vetoes remain authoritative.

This alpha changes the model's `HasTitleBar` from `bool` to `bool?`. Existing boolean assignments and serialized explicit booleans remain supported. Code reading a non-nullable result should use `window.HasTitleBar ?? site.ToolWindowsHaveTitleBars`, or the UI-independent `ToolCaptionSettings.Resolve` API. Other adapters can use the same resolver without loading Uno. MVVM and source bindings expose all four values, and layout serialization preserves null versus explicit booleans. The original three-argument `ToolCaptionOptions` constructor and deconstruction remain available.

The tool-caption sample's **Instance policies** menu demonstrates overrides, inheritance reset, live title visibility, capabilities, and save/restore. Button-only changes preserve geometric cache entries; effective title-height changes invalidate measurement, including live floating and splitter previews. Repeated floating-primary queries use an immutable-snapshot cache instead of rescanning host members. See [caption inheritance](docs/tool-caption-inheritance.md), [native gesture review](docs/tool-caption-input-review.md), and [caption query review](docs/tool-caption-allocation-review.md) for contracts and validation boundaries.
