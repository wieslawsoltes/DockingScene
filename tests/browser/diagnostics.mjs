// All captures are read-only. No browser test modifies the managed model through JavaScript.
export async function captureDiagnostics(page) {
  return await page.evaluate(() => ({
    state: globalThis.dockingScene ?? null,
    startup: globalThis.dockingSceneStartup ?? null,
    diagnosticError: globalThis.dockingSceneDiagnosticError ?? null,
    documentReady: document.readyState,
    canvasCount: document.querySelectorAll('canvas').length,
    bodyText: document.body?.innerText?.slice(0, 8000),
  })).catch(error => ({ captureError: String(error) }));
}
