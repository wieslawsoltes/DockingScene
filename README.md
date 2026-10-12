# DockingScene

A modular docking and multiple-document workspace for **Uno Platform**, with a UI-independent .NET engine and retained controls rendered by Uno Skia on desktop and WebAssembly.

**Working alpha, not full Actipro Docking & MDI parity.** This is an independent implementation, not a WPF binary replacement or an Actipro product. Native/detached hosts, linked-site transfers, advanced persistence, complete accessibility and vendor pixel certification remain unfinished. See the [compatibility matrix](docs/parity.md).

**[Open the browser demonstration](https://wieslawsoltes.github.io/DockingScene/)**. Main builds and tests before deployment, then verifies the live source revision and interactions. A feature branch is not published merely because its source has been pushed.

## Reusable packages

All six packages use **0.1.0-alpha.8** and are packed from one source revision. The sample composes the packages; it is not the docking engine.

| Package | Responsibility |
| --- | --- |
| `DockingScene.Core` | Immutable layout trees, capabilities, validated/cancellable transactions, previews, history, indexed queries and primary-document state |
| `DockingScene.Layout` | Constrained geometry, guides, tab allocation/virtualization metrics, MDI arrangement, bounded layout caches and drag edge scrolling |
| `DockingScene.Serialization` | Versioned bounded JSON/stream input, unknown-window policies and atomic file storage |
| `DockingScene.Mvvm` | Stable observable projections, commands and bounded collection synchronization |
| `DockingScene.Themes` | Palettes, metrics, strings, independent vector icons and bounded SVG-path parsing |
| `DockingScene.Controls` | Uno `DockSite`, `AdvancedTabControl`, document/tool containers, `WindowControl`, `StandardMdiHost` and switcher |

Only Controls depends on Uno UI. Application content and view models are never included in layout persistence. Isolated package-only consumers execute the five non-UI packages and compile the controls APIs without source project references.

## Build and run

The repository pins Uno SDK **6.7.30** and uses **.NET 10**. Install the SDK and Uno's platform prerequisites.

```sh
# Every UI-independent executable specification suite
for project in tests/*Specs/*.csproj; do
  dotnet run --project "$project" -c Release || exit 1
done

# Desktop sample
dotnet run --project src/DockingScene.App -f net10.0-desktop -c Release

# Browser distribution
dotnet workload install wasm-tools
dotnet publish src/DockingScene.App -f net10.0-browserwasm -c Release -o artifacts/browser
mkdir -p artifacts/site/DockingScene
cp -a artifacts/browser/wwwroot/. artifacts/site/DockingScene/
python3 -m http.server 8765 --directory artifacts/site
```

Open `http://localhost:8765/DockingScene/`. The application uses this base path, including for Pages; serving it at `/` requires changing `WasmShellWebAppBasePath` and rebuilding.

## Engine and retained content

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

Content is created lazily and retained through tab switches, movement, hiding and reopening. Applications own it by default. Set `DisposeContentOnUnregister = true` only for exclusively owned factory results; ordinary close/hide still retains content. `WindowContentReleased` reports cache release. `DockLayoutStream.ReadAsync` leaves its input stream open and supports bounded, cancellable loading.

The manager validates changes, supports closing vetoes and expected revisions, and commits batches atomically. Source adapters are disposable and do not own application view models. Read [architecture and contracts](docs/architecture.md) for threading, lifecycle, synchronization and persistence boundaries.

## Tabs and docking

`AdvancedTabControl` has independently clipped pinned, normal and preview lanes; six overflow policies; measured minimum/fixed/shrunk widths; four placements; strip visibility; pin/unpin and preview promotion; close cancellation and middle-click policy; customizable menus; retained content; and same-lane pointer reordering with cancellable events and edge scrolling. Hidden headers do not instantiate their document content.

Choose **Tab options → Open standalone tab playground** for 20-tab and 2,002-tab workloads, overflow/placement controls and content-lifetime examples. [Tab layout](docs/tab-layout.md) documents the standalone API. `CanTabsDrag` and `CanTabsAutoScroll` control drag behavior; `FocusSelectedContent` restores the selected content's eligible focus target.

Drag document tabs or tool captions to dock, split, reorder or float. Hold **Ctrl** to suppress docking and **Escape** to cancel. Four-sided auto-hide supports hover/click activation, inner-edge resizing and pinning. Context menus expose preview promotion, pinning, placement and directional closing. Floating windows are currently hosted inside the DockSite, not separate OS windows.

The sample's compact Metro presentation includes native Solution Explorer and Properties views, dotted caption grips, contextual document actions, strip accents and a status bar. **Appearance** changes these settings without replacing the workspace. [Metro fidelity](docs/metro-fidelity.md) and [template follow-up](docs/metro-follow-up.md) identify the public references and remaining visual differences.

## Standard MDI and external commands

Standard MDI supports overlapping windows, movement, eight resize edges/corners, minimize/maximize/restore, cascade and tiling. Minimized captions wrap in nonoverlapping bottom-up rows without changing normal restore bounds. Vertical overflow is reported and clipped; full scrollable virtual extents are not implemented.

Window icons and title context menus expose state and close commands. Host minimize/maximize settings control UI actions separately from engine capabilities. Gestures preserve stacking order and cancel on stale revisions, Escape, lost capture or unload. See [MDI review](docs/mdi-review.md).

Set `StandardMdiHost.AreMaximizedWindowFramesVisible = false`, or the corresponding `DockSite.AreStandardMdiMaximizedWindowFramesVisible`, to reclaim maximized title/frame space. `PrimaryWindow`, `IsExternalMaximizedUIRequired` and four primary-window commands support external application controls. The sample displays retained minimize/restore/close buttons at the menu bar's right edge; activating a tool keeps their target on the hosted document. **Window → Toggle maximized window frames** demonstrates both modes. [MDI presentation](docs/mdi-presentation.md) explains commands, invalidation and limits.

## Keyboard and persistence

**Ctrl+Tab / Ctrl+Shift+Tab** cycle the MRU switcher; releasing Ctrl commits. **F8** opens it when the browser reserves Ctrl+Tab. **Ctrl+F4 / Ctrl+W** close the active window where permitted. **Ctrl+1…8** selects normal document tabs and **Ctrl+9** selects the last; add **Alt** for pinned or **Shift** for preview tabs. **Ctrl+Alt+Down** opens the document list; **Alt+-** opens its context menu. Browser-reserved keys may not reach WebAssembly.

The toolbar and File menu save/load layout metadata. Browser storage uses local storage; desktop storage uses the application data directory. This is DockingScene JSON, not Actipro XML, and does not save document text.

## Performance, tests and artifacts

Snapshot indexes, geometry caches, retained tab measurements/lane plans, selective MVVM notifications and retained caption/auxiliary controls avoid repeated work. Minimized-caption layouts are reused when ordered IDs and geometry inputs are unchanged. Counters describe performed work, not an application FPS guarantee. See [performance review](docs/performance-review.md) and [retained-host review](docs/retained-host-review.md).

`Validate` executes every model suite. `Uno application` repeats them, builds desktop/WebAssembly, packs all libraries, validates isolated consumers and drives real browser inputs and pixel checks. Any failed gate blocks distribution. Source snapshots, packages, logs, screenshots and traces are retained as workflow artifacts. [Validation scope](docs/validation.md) distinguishes defined scenarios from actual results for a specific commit.

```sh
npm install --prefix tests/browser
cd tests/browser && npx playwright install chromium && cd ../..
BROWSER_URL=http://localhost:8765/DockingScene/ node tests/browser/run.mjs
BROWSER_URL=http://localhost:8765/DockingScene/ node tests/browser/tab-layout.mjs
BROWSER_URL=http://localhost:8765/DockingScene/ node tests/browser/host-review.mjs
```

Diagnostics are **off by default**. The harness adds `?diagnostics=1` for read-only geometry, synthetic editor text/caret/identity and performed-work observations. No JavaScript test hook changes the managed model. Do not enable diagnostics around confidential documents; the observations themselves add overhead.

## Rendering and license

Uno Skia renders the actual controls and independent vector geometry. GPU selection/fallback belongs to the host. CI uses **SwiftShader software WebGL**: it validates rendering and interaction, not hardware-GPU throughput or native desktop runtime behavior.

Project source is MIT licensed; dependencies retain their own licenses. No Actipro source, assemblies, proprietary assets, branding or fonts are redistributed. Full compatibility is not claimed.
