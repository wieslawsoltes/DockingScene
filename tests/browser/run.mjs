import { chromium } from 'playwright';
import { mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { captureDiagnostics } from './diagnostics.mjs';

const output = process.env.BROWSER_RESULTS || 'artifacts/browser-results';
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ headless: true, args: ['--enable-webgl', '--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const context = await browser.newContext({ viewport: { width: 1440, height: 960 } });
await context.tracing.start({ screenshots: true, snapshots: true, sources: true });
const page = await context.newPage();
const errors = [], results = [], consoleMessages = [], networkFailures = [];
page.on('pageerror', error => errors.push(String(error)));
page.on('console', message => consoleMessages.push(`${message.type()}: ${message.text()}`));
page.on('requestfailed', request => networkFailures.push({ url: request.url(), error: request.failure()?.errorText }));
const address = new URL(process.env.BROWSER_URL || 'http://127.0.0.1:8765/DockingScene/'); address.searchParams.set('diagnostics', '1');
const url = address.href;
const state = () => page.evaluate(() => globalThis.dockingScene ?? null);
const windowState = async id => (await state()).windows.find(window => window.id === id);
async function wait(predicate, arg) { await page.waitForFunction(predicate, arg, { timeout: 15000 }); }
async function settle() {
  const sequence = (await state()).sequence;
  await wait(sequence => globalThis.dockingScene.sequence > sequence, sequence);
  await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
}
async function bounds(id) {
  await wait(id => !!globalThis.dockingScene?.bounds?.[id], id);
  const rect = (await state()).bounds[id]; assert(rect.width > 0 && rect.height > 0, `Empty hit target: ${id}`); return rect;
}
async function click(id, options = {}) {
  const r = await bounds(id); await page.mouse.click(r.x + Math.min(r.width / 2, 60), r.y + r.height / 2, options);
}
const menusGone = () => wait(() => !Object.keys(globalThis.dockingScene.bounds).some(id => id.startsWith('appmenu:') || id.startsWith('menu:') || id.startsWith('tab-list:')));
async function menu(name, item) {
  await menusGone(); await settle(); await click(`menubar:${name}`); await settle();
  await click(`appmenu:${item}`); await menusGone(); await settle();
}
async function contextMenu(id, item) {
  await menusGone(); await click(`tab:${id}`, { button: 'right' }); await settle();
  await click(`menu:${item}`); await menusGone(); await settle();
}
async function reset() {
  await page.keyboard.press('Escape'); await menusGone();
  const generation = (await state()).workspaceGeneration;
  await click('toolbar:Reset');
  await wait(generation => globalThis.dockingScene.workspaceGeneration > generation &&
    globalThis.dockingScene.windows.length === 12 && globalThis.dockingScene.activeWindowId === 'welcome', generation);
  await settle();
}
async function drag(id, destination, modifiers = []) {
  const r = await bounds(id); const x = r.x + Math.min(r.width / 2, 45), y = r.y + r.height / 2;
  for (const modifier of modifiers) await page.keyboard.down(modifier);
  try {
    await page.mouse.move(x, y); await page.mouse.down();
    await page.mouse.move(destination.x, destination.y, { steps: 18 });
    await page.mouse.up();
  } finally { for (const modifier of [...modifiers].reverse()) await page.keyboard.up(modifier); }
  await wait(() => globalThis.dockingScene.interactionActive === false);
  await settle();
}
async function test(name, action) {
  const start = Date.now();
  try { await action(); results.push({ name, passed: true, elapsedMs: Date.now() - start }); console.log(`PASS ${name}`); }
  catch (error) {
    results.push({ name, passed: false, error: String(error), elapsedMs: Date.now() - start }); console.error(`FAIL ${name}: ${error}`);
    await page.screenshot({ path: `${output}/failure-${results.length}.png` }).catch(() => {});
    await writeFile(`${output}/failure-${results.length}.json`, JSON.stringify(await captureDiagnostics(page), null, 2));
    await page.mouse.up().catch(() => {}); await page.keyboard.press('Escape').catch(() => {});
  }
}
try {
  await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForFunction(() => globalThis.dockingScene?.ready || globalThis.dockingSceneStartup?.error, null, { timeout: 90000 });
  const startup = await captureDiagnostics(page); assert(!startup.startup?.error, startup.startup?.error);
  await page.locator('.uno-loader').waitFor({ state: 'hidden', timeout: 30000 }); await settle();
  await test('Uno WebAssembly startup and complete painted workspace', async () => {
    const s = await state(); assert.equal(s.renderer, 'Uno Skia'); assert.equal(s.windows.length, 12);
    assert.equal(s.activeWindowId, 'welcome'); assert.equal(s.mdiMode, 'Tabbed'); assert(s.groups.length >= 4);
    assert.equal(s.autoHideGroups.length, 1); assert.deepEqual(errors, []); await page.screenshot({ path: `${output}/workspace.png` });
  });
  await test('New document is initially unmodified', async () => {
    await click('toolbar:New'); await wait(() => globalThis.dockingScene.windows.some(w => w.id === 'new-1')); await settle();
    assert.equal((await state()).activeWindowId, 'new-1'); assert.equal((await windowState('new-1')).isModified, false);
    assert.equal((await state()).editors['new-1'].text.replace(/\r\n?|\n/g, '\n'),
      '// new-1\n// This editor instance is retained while docking, floating and hiding.\n');
  });
  await test('Close and reopen through a menu preserves registered identity', async () => {
    const instance = (await state()).editors['new-1'].instance;
    await click('close:new-1'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'new-1')?.placement === 'Hidden');
    await menu('View', 'Document 1.cs'); await wait(() => globalThis.dockingScene.activeWindowId === 'new-1');
    assert.equal((await state()).editors['new-1'].instance, instance);
  });
  await test('Layout save, structural edit and restore', async () => {
    await click('toolbar:Save'); await settle(); await click('close:new-1');
    await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'new-1')?.placement === 'Hidden');
    await click('toolbar:Load'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'new-1')?.placement === 'Document');
  });
  await test('Standard MDI cascade, tiling and return to tabbed MDI', async () => {
    await menu('Window', 'Standard MDI'); await wait(() => globalThis.dockingScene.mdiMode === 'Standard');
    await page.screenshot({ path: `${output}/standard-mdi.png` }); await menu('Window', 'Tile vertically');
    const documents = (await state()).windows.filter(w => w.placement === 'Document');
    assert(documents.every(w => w.mdiBounds.width > 0 && w.mdiBounds.height > 0));
    assert(new Set(documents.map(w => w.mdiBounds.x)).size === documents.length);
    await menu('Window', 'Tabbed MDI'); await wait(() => globalThis.dockingScene.mdiMode === 'Tabbed');
  });
  await test('Layout lock and unlock', async () => {
    await menu('Window', 'Lock layout'); assert.equal((await state()).locked, true);
    await menu('Window', 'Unlock layout'); assert.equal((await state()).locked, false);
  });
  await test('Editor caret input does not navigate document tabs', async () => {
    await reset(); await click('tab:preview'); await wait(() => globalThis.dockingScene.editors.preview);
    assert.equal((await windowState('preview')).displayMode, 'Preview'); assert.equal((await windowState('preview')).isModified, false);
    await click('editor:preview'); await page.keyboard.press('Control+a'); await page.keyboard.type('abcde');
    await page.keyboard.press('End'); await page.keyboard.press('ArrowLeft'); await page.keyboard.type('X');
    // Text updates and the corresponding managed metadata notification need not
    // appear in the same diagnostic tick. Wait for the whole expected transition.
    await wait(() => globalThis.dockingScene.editors.preview.text === 'abcdXe' &&
      globalThis.dockingScene.windows.some(w => w.id === 'preview' && w.isModified && w.displayMode === 'Normal'));
    assert.equal((await state()).activeWindowId, 'preview'); assert.equal((await windowState('preview')).displayMode, 'Normal');
    assert.equal((await windowState('preview')).isModified, true);
  });
  await test('Preview keep-open header action', async () => {
    await reset(); await click('keep-open:preview'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'preview').displayMode === 'Normal');
  });
  await test('Pointer tab reordering uses measured insertion positions', async () => {
    await reset(); const target = await bounds('tab:main');
    await drag('tab:code', { x: target.x + 4, y: target.y + target.height / 2 });
    await wait(() => { const ids = globalThis.dockingScene.groups.find(g => g.windows.includes('code')).windows; return ids.indexOf('code') < ids.indexOf('main'); });
    assert.equal((await state()).floatingHosts.length, 0);
  });
  await test('Ctrl-drag floats a document and retains its editor', async () => {
    await click('tab:code'); await wait(() => globalThis.dockingScene.editors.code); const instance = (await state()).editors.code.instance;
    await drag('tab:code', { x: 500, y: 350 }, ['Control']);
    await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').placement === 'Floating');
    assert.equal((await state()).editors.code.instance, instance); assert.equal((await state()).floatingHosts.length, 1);
    await page.screenshot({ path: `${output}/floating.png` });
  });
  await test('Floating tab can be dragged back into a document header', async () => {
    // Without this precondition, a failed preceding Float test can produce a false pass.
    assert.equal((await windowState('code')).placement, 'Floating', 'Redocking requires an actually floating source');
    assert.equal((await state()).floatingHosts.length, 1);
    const instance = (await state()).editors.code.instance; const target = await bounds('tab:main');
    await drag('tab:code', { x: target.x + target.width / 2, y: target.y + target.height / 2 });
    await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').placement === 'Document');
    assert.equal((await state()).editors.code.instance, instance); assert.equal((await state()).floatingHosts.length, 0);
  });
  await test('Splitter pointer movement changes constrained layout', async () => {
    await reset(); const s = await state(); const [id, r] = Object.entries(s.bounds).find(([id, r]) => id.startsWith('splitter:') && r.height > 500 && r.width < 10);
    const before = s.groups.find(g => g.windows.includes('welcome')).bounds.width;
    await drag(id, { x: r.x - 90, y: r.y + r.height / 2 });
    await wait(before => Math.abs(globalThis.dockingScene.groups.find(g => g.windows.includes('welcome')).bounds.width - before) > 50, before);
    assert.equal((await state()).windows.length, 12);
  });
  await test('Auto-hide popup can be resized and pinned with pointer input', async () => {
    await reset(); await click('autohide:toolbox'); await wait(() => globalThis.dockingScene.autoHideWindowId === 'toolbox');
    const before = (await windowState('toolbox')).autoHideSize, r = await bounds('autohide-resize:toolbox');
    await drag('autohide-resize:toolbox', { x: r.x + 100, y: r.y + Math.min(200, r.height / 2) });
    await wait(before => globalThis.dockingScene.windows.find(w => w.id === 'toolbox').autoHideSize > before + 50, before);
    const group = (await state()).autoHideGroups.find(g => g.windows.includes('toolbox'));
    await click(`group:${group.id}:Pin`); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'toolbox').placement === 'Docked');
  });
  await test('Pin and close-others preserve pinned documents', async () => {
    await reset(); await contextMenu('main', 'Pin'); await contextMenu('code', 'Close other documents');
    assert.notEqual((await windowState('main')).placement, 'Hidden'); assert.notEqual((await windowState('welcome')).placement, 'Hidden');
    assert.equal((await windowState('readme')).placement, 'Hidden');
  });
  await test('Window switcher cancels without changing activation', async () => {
    await reset(); const active = (await state()).activeWindowId;
    await click('toolbar:Windows'); await bounds('window-switcher'); await page.keyboard.press('Escape');
    await wait(() => !globalThis.dockingScene.bounds['window-switcher']); assert.equal((await state()).activeWindowId, active);
  });
  await test('Theme changes preserve editor content and identity', async () => {
    await reset(); await click('tab:code'); await wait(() => globalThis.dockingScene.editors.code); const editor = (await state()).editors.code;
    await menu('Theme', 'Metro Dark'); assert.equal((await state()).theme, 'Metro Dark');
    assert.equal((await state()).editors.code.instance, editor.instance); assert.equal((await state()).editors.code.text, editor.text);
    await page.screenshot({ path: `${output}/dark.png` }); await menu('Theme', 'Metro Light');
  });
  await test('Final reset and exception-free browser session', async () => {
    await reset();
    assert.equal((await state()).floatingHosts.length, 0);
    assert.equal((await state()).interactionActive, false);
    assert.equal((await state()).bounds['window-switcher'], undefined);
    assert.equal((await state()).autoHideWindowId, null);
    assert.deepEqual(errors, []);
    const diagnostics = await captureDiagnostics(page); assert.equal(diagnostics.diagnosticError, null); assert.equal(diagnostics.startup?.error, null);
    await page.screenshot({ path: `${output}/workspace-final.png` });
  });
} catch (error) {
  results.push({ name: 'Browser initialization', passed: false, error: String(error) }); console.error(error);
  await page.screenshot({ path: `${output}/startup-failure.png` }).catch(() => {});
} finally {
  const diagnostics = await captureDiagnostics(page);
  await writeFile(`${output}/results.json`, JSON.stringify({ url, results, errors, consoleMessages, networkFailures, diagnostics }, null, 2));
  await writeFile(`${output}/final-state.json`, JSON.stringify(diagnostics, null, 2));
  await context.tracing.stop({ path: `${output}/trace.zip` }).catch(error => console.error(error)); await browser.close();
}
if (!results.length || results.some(test => !test.passed)) process.exitCode = 1;
