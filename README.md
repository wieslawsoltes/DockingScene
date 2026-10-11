# DockingScene

A modular docking and multiple-document workspace for **Uno Platform**, with a UI-independent .NET engine and real retained controls rendered by Uno Skia on desktop and WebAssembly.

**Status: working alpha, not full Actipro Docking & MDI parity.** This is an independent implementation, not a WPF binary replacement or an Actipro product. Native OS floating windows, linked-site transfers, several advanced serialization behaviors, complete accessibility parity, and pixel-for-pixel comparison remain unfinished. See [the explicit compatibility matrix](docs/parity.md).

## Packages

| Package | Responsibility | UI dependency |
| --- | --- | --- |
| `DockingScene.Core` | Immutable layout trees, capability policy, validated/cancellable transactions, preview documents, undo/redo | None |
| `DockingScene.Layout` | Constrained layout, splitter geometry, docking guides, measured/virtualized tab geometry, window snapping, MDI arrangement | None |
| `DockingScene.Serialization` | Versioned bounded JSON, unknown-window policies, atomic file layout storage | None |
| `DockingScene.Mvvm` | Observable window projections, commands, stable content identities, atomic collection synchronization | None |
| `DockingScene.Themes` | Palettes, metrics, strings, original vector icons and bounded SVG-path parsing | None |
| `DockingScene.Controls` | `DockSite`, `AdvancedTabControl`, `DockingWindow`, `WindowControl`, `StandardMdiHost`, `StandardSwitcher` and container controls | Uno Platform |

The `DockingScene.App` sample composes these packages; it is not the docking engine. Application content is never embedded in layout persistence. The core, geometry, MVVM and serialization packages can be used without creating a UI.

## Build and run

The repository pins Uno SDK **6.7.30** and uses **.NET 10**. Install the .NET 10 SDK and the platform prerequisites required by Uno's desktop target.

```sh
# UI-independent executable specification suites
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

The Pages workflow deploys only a successfully validated `main` build, then runs the browser suite against the deployed URL and verifies its source commit. The intended project URL is [wieslawsoltes.github.io/DockingScene](https://wieslawsoltes.github.io/DockingScene/); consult the repository's deployment status rather than assuming an unvalidated branch has been deployed.

## Use the engine and a DockSite

```csharp
using DockingScene.Core;
using DockingScene.Controls;

var manager = new DockManager();
manager.Register(new("editor", "Document.cs", WindowKind.Document)).ThrowIfFailed();
manager.Register(new("explorer", "Explorer", WindowKind.Tool),
    targetId: "workspace", side: DockSide.Right).ThrowIfFailed();

var site = new DockSite
{
    Manager = manager,
    ContentFactory = window => new Microsoft.UI.Xaml.Controls.TextBox
    {
        AcceptsReturn = true,
        Text = window.Title
    }
};

// The factory is lazy; moving or hiding a window retains the actual control.
manager.Float("editor").ThrowIfFailed();
manager.Undo().ThrowIfFailed();
```

`DockSite.Manager` and `DockSite.Theme` are dependency properties. The controls package can be consumed independently of the sample app. `AdvancedTabItem` exposes bindable metadata dependency properties; `AdvancedTabControl.Update` accepts caller-owned items and retains their content.

See [architecture and API contracts](docs/architecture.md) for atomic batches, closing vetoes, MVVM, threading, persistence, and ownership rules.

## Interactions

Drag document tabs or tool-window captions to dock, split, reorder or float. Hold **Ctrl** while dragging to suppress docking; **Escape** cancels. Tool windows can be auto-hidden on four sides; their popups expose a resize edge and pin command. Context menus provide preview promotion, pinned tabs, group creation, placement, and directional closing.

**Ctrl+Tab / Ctrl+Shift+Tab** cycle the MRU switcher; releasing Ctrl commits. **F8** opens the switcher where a browser reserves Ctrl+Tab. **Ctrl+F4 / Ctrl+W** close the active window where the host permits these shortcuts. **Ctrl+1…9** selects ordinary document tabs; **Ctrl+Alt+1…9** selects pinned tabs. Browser-reserved shortcuts are not guaranteed to reach WebAssembly applications.

The toolbar switches between tabbed and standard MDI. Standard MDI supports overlapping windows, minimize/maximize, cascade and horizontal/vertical/grid tiling. Layout save/load is available from the toolbar and File menu. The browser sample stores layout metadata in local storage, not document text.

## Validation and artifacts

`Validate` runs every `tests/*Specs` executable and packs UI-independent libraries. `Uno application` builds the desktop target, publishes the actual browser application, packs all six libraries, and drives pointer/keyboard interactions through Chromium. Browser tests read diagnostics; they do **not** call JavaScript hooks to mutate the managed model.

```sh
npm install --prefix tests/browser
cd tests/browser && npx playwright install chromium && cd ../..
BROWSER_URL=http://localhost:8765/DockingScene/ node tests/browser/run.mjs
```

Diagnostic capture is **off by default**. The browser harness adds `?diagnostics=1`, enabling read-only geometry, layout, and sample-editor observations for the test session. Do not enable this in an application containing sensitive documents. Disabling diagnostics removes the periodic visual-tree walk.

Workflow artifacts include source snapshots, browser distributions, the complete six-package `0.1.0-alpha.1` set, executable-specification logs, browser results, screenshots and Playwright traces. Package-only consumers validate the generated NuGet feed separately from project-reference builds. Read [validation scope](docs/validation.md) before interpreting a green check as complete product compatibility.

## Rendering and license

Rendering is performed by Uno Skia; icons are original vector paths built into explicit Uno geometry, with no runtime XAML string conversion. GPU selection and fallback are the host's responsibility. Chromium CI explicitly uses **SwiftShader software WebGL**, so these runs validate rendering and interaction but are not hardware-GPU performance measurements.

Project source is MIT licensed. Uno Platform, Skia and other dependencies retain their own licenses. No Actipro source, assemblies, proprietary assets or branding are included.
