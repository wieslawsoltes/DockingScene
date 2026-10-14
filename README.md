# DockingScene

A modular docking and multiple-document workspace for **Uno Platform**, with a UI-independent .NET engine and retained controls rendered by Uno Skia on desktop and WebAssembly.

**Working alpha, not full Actipro Docking & MDI parity.** This is an independent implementation, not a WPF binary replacement or an Actipro product. Native OS floating windows, linked sites, size-to-content, complete accessibility/RTL and vendor pixel certification remain unfinished. See [the compatibility matrix](docs/parity.md).

**[Open the browser demonstration](https://wieslawsoltes.github.io/DockingScene/)**. Main-branch builds are tested before deployment and verified against their source commit afterward. A feature branch is not published merely by compiling.

## Packages

| Package | Responsibility |
| --- | --- |
| `DockingScene.Core` | Immutable trees, capabilities, validated/cancellable transactions, preview documents, navigation and undo/redo |
| `DockingScene.Layout` | Constrained layout, docking guides, three tab lanes, six overflow modes, snapping, scrolling and MDI arrangement |
| `DockingScene.Serialization` | Versioned bounded JSON, unknown-window policies, atomic storage and cancellable stream loading |
| `DockingScene.Mvvm` | Observable projections, commands, stable identities and atomic source-collection synchronization |
| `DockingScene.Themes` | Palettes, metrics, strings, original icons and bounded vector-path parsing |
| `DockingScene.Controls` | `DockSite`, `AdvancedTabControl`, `DockingWindow`, `WindowControl`, `StandardMdiHost`, `StandardMdiControlBar`, `StandardSwitcher`, `DockDragOverlay` and containers |

Only Controls depends on Uno. All six libraries use the version in `Directory.Build.props` and are packed from the same commit. Isolated package-only consumers run the five non-UI libraries and compile Controls without source references. The sample App composes these libraries; application content is never embedded in layout persistence.

## Build and run

The repository pins **Uno SDK 6.7.30** and uses **.NET 10**. Install the .NET SDK and Uno's platform prerequisites.

```sh
for project in tests/*Specs/*.csproj; do
  dotnet run --project "$project" -c Release || exit 1
done

# Desktop
dotnet run --project src/DockingScene.App -f net10.0-desktop -c Release

# Browser
dotnet workload install wasm-tools
dotnet publish src/DockingScene.App -f net10.0-browserwasm -c Release -o artifacts/browser
mkdir -p artifacts/site/DockingScene
cp -a artifacts/browser/wwwroot/. artifacts/site/DockingScene/
python3 -m http.server 8765 --directory artifacts/site
```

Open `http://localhost:8765/DockingScene/`. The application uses this base path, including on Pages; change `WasmShellWebAppBasePath` and rebuild before serving it at `/`.

## Use the engine and DockSite

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
manager.Float("editor").ThrowIfFailed();
manager.Undo().ThrowIfFailed();
```

Content is lazy and retained through movement, close/hide and reactivation. Application content is not disposed by default. Set `DisposeContentOnUnregister` only for exclusively owned factory results. Dependency properties expose the manager, theme and presentation settings. See [architecture](docs/architecture.md) for threading, batches, vetoes, MVVM, persistence and ownership.

## Interactions

Drag tabs or tool captions to dock, split, reorder or float. **Ctrl** suppresses docking and magnetic snapping during pointer gestures; **Escape** cancels. Context menus provide preview promotion, pinning, tab placement, group creation and directional closing. Auto-hide works on four edges, with inner-edge popup resizing and pinning.

**Ctrl+Tab / Ctrl+Shift+Tab** opens/cycles the MRU switcher; releasing Ctrl commits. **F8** is available where a browser reserves Ctrl+Tab. **Ctrl+F4 / Ctrl+W** closes the active window. **Ctrl+1…8** selects normal tabs and **Ctrl+9** selects the last; add **Alt** for pinned tabs or **Shift** for preview tabs. **Ctrl+Alt+Down** opens the document list and **Alt+-** its context menu. Browser-reserved shortcuts are not guaranteed to reach the app.

The toolbar switches tabbed/standard MDI and saves/loads layout. Browser persistence stores metadata in local storage, not document text. Standard MDI supports overlap, minimize/maximize, cascade and horizontal/vertical/grid tiling. Window and Appearance menus open the focused interactive samples below.

## Feature guides and samples

| Area | APIs, examples and limits |
| --- | --- |
| Standalone tabs | [Three pinned/normal/preview lanes, six overflow modes, measured/fixed extents, four placements, close vetoes, middle-click closing, pinning, promotion, customizable menus and strip visibility](docs/tab-layout.md) |
| Performance and ownership | [2,002-tab workloads, retained measurement, reordering, content factories/lifetime and bounded stream input](docs/performance-review.md); [geometry caches, retained splitters/rails, focus restoration and edge scrolling](docs/retained-host-review.md) |
| Metro appearance | [Contextual actions, document accent, dotted caption grips, compact Explorer/Properties and themes](docs/metro-fidelity.md); [theme text](docs/theme-text-review.md); [selector readiness](docs/selector-readiness.md) |
| MDI state and commands | [System menus, minimized rows, retained captions and independent move/resize capabilities](docs/mdi-review.md); [frame-free maximization and external `StandardMdiControlBar`](docs/maximized-mdi.md); [individual minimize/maximize policies](docs/individual-mdi-policies.md) |
| Contextual content | [Retained MDI title templates, data contexts and independent hosts](docs/mdi-context-and-scope.md); [tool title templates, Near/Far alignment and auto-hide transfer](docs/tool-title-context.md) |
| Magnetic movement | [Standard MDI snap/gap settings and `WindowMagnetism`](docs/mdi-magnetism.md); [whole-host/partial floating drag plans and retained guides](docs/floating-drag-review.md) |
| Live floating layout | [`FloatingLayoutPlan`, provisional retained editor/splitter reflow, pointer/keyboard rollback and frame-only opt-out](docs/floating-live-layout.md) |
| Scrollable MDI | [Opt-in native scrolling, negative coordinates, viewport/world conversion, reveal, maximize and minimized overflow](docs/mdi-scrolling.md) |
| Tool captions | [Site action visibility, complete-host maximize/restore versus clicked-group docking, primary document titles and delimiters](docs/tool-caption-review.md); [native capture repair](docs/tool-caption-input-review.md); [primary-title caching](docs/tool-caption-allocation-review.md) |
| Window overrides | [Nullable title/close/options/pin overrides, site inheritance, physical row/slot collapse, MVVM and JSON](docs/tool-caption-inheritance.md) |
| Tab constraints | [Physical vertical-strip sizing and allocation-free `MeasureGroup`](docs/vertical-tab-geometry.md); [hidden single-tool strips, retained metrics and gesture cancellation](docs/hidden-tool-tabs.md) |
| Optional tab images | [Native image sources, independent document/tool policies, retained source replacement and isolated browser contracts](docs/native-tab-images.md) |

`DockWindow.HasTitleBar` is nullable in this alpha. Boolean assignments and explicit JSON booleans remain valid; resolve reads with `window.HasTitleBar ?? site.ToolWindowsHaveTitleBars` or `ToolCaptionSettings.Resolve`. The original three-argument `ToolCaptionOptions` constructor/deconstruction remains. Close/pin instance visibility is an independent DockingScene extension, not an identical vendor API claim.

Hidden single-tool strips reserve no invisible width/height. Standalone measurement defaults to Show for compatibility; DockSite supplies its live setting. Its constrained tool-caption sample offers Show/Hide commands and an F4 toggle. Existing editors survive placement, theme, visibility, undo and live-resize transitions.

## Validation and artifacts

`Validate` executes all model specifications under `pipefail`. `Uno application` repeats them, builds desktop/WebAssembly, packs all libraries, checks isolated consumers and runs four product browser suites plus the independent 100-cycle focus runner. All five repeat against the deployed revision. A failure blocks deployment. See [validation scope](docs/validation.md), [allocation interval review](docs/allocation-interval-review.md) and exact-run result artifacts.

```sh
npm install --prefix tests/browser
(cd tests/browser && npx playwright install chromium)
for script in run.mjs tab-layout.mjs host-review.mjs caption-policies-runner.mjs focus-investigation.mjs; do
  BROWSER_URL=http://localhost:8765/DockingScene/ node "tests/browser/$script" || exit 1
done
```

Tests use actual pointer/keyboard input. Read-only diagnostics are **off by default**; `?diagnostics=1` exposes synthetic editor text, caret, identities and geometry for testing. Do not enable it around confidential documents. Normal sessions avoid the diagnostic visual-tree walk. Artifacts contain source snapshots, packages, build/contract logs, screenshots and Playwright traces.

## Rendering and license

Uno Skia renders actual retained controls and original vector geometry. Host configuration governs GPU selection/fallback. CI uses **SwiftShader software WebGL**, so passing tests do not establish hardware-GPU throughput, native desktop behavior, screen-reader conformance or vendor pixel equivalence.

Source is MIT licensed. Uno, Skia and other dependencies retain their own licenses. No Actipro source, assemblies, proprietary assets or branding are included.
