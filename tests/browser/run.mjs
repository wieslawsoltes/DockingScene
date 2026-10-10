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
const url = process.env.BROWSER_URL || 'http://127.0.0.1:8765/DockingScene/';
const state = () => page.evaluate(() => globalThis.dockingScene ?? null);
async function wait(predicate, arg) { await page.waitForFunction(predicate, arg, { timeout: 15000 }); }
async function click(id, options = {}) {
  await wait(id => !!globalThis.dockingScene?.bounds?.[id], id);
  const r = (await state()).bounds[id];
  assert(r.width > 0 && r.height > 0, `Empty hit target: ${id}`);
  await page.mouse.click(r.x + Math.min(r.width / 2, 60), r.y + r.height / 2, options);
}
async function menu(name, item) { await click(`menubar:${name}`); await click(`appmenu:${item}`); }
async function test(name, action) {
  const start = Date.now();
  try { await action(); results.push({ name, passed: true, elapsedMs: Date.now() - start }); console.log(`PASS ${name}`); }
  catch (error) {
    results.push({ name, passed: false, error: String(error), elapsedMs: Date.now() - start }); console.error(`FAIL ${name}: ${error}`);
    await page.screenshot({ path: `${output}/failure-${results.length}.png` }).catch(() => {});
    await writeFile(`${output}/failure-${results.length}.json`, JSON.stringify(await captureDiagnostics(page), null, 2));
    await page.keyboard.press('Escape').catch(() => {});
  }
}
try {
  await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForFunction(() => globalThis.dockingScene?.ready || globalThis.dockingSceneStartup?.error, null, { timeout: 90000 });
  const startup = await captureDiagnostics(page);
  assert(!startup.startup?.error, startup.startup?.error);
  await test('Uno WebAssembly startup and complete initial workspace', async () => {
    const s = await state(); assert.equal(s.renderer, 'Uno Skia'); assert.equal(s.windows.length, 12);
    assert.equal(s.activeWindowId, 'welcome'); assert.equal(s.mdiMode, 'Tabbed');
    assert(s.groups.length >= 4); assert.equal(s.autoHideGroups.length, 1); assert.deepEqual(errors, []);
    await page.screenshot({ path: `${output}/workspace.png` });
  });
  await test('New document uses real toolbar input', async () => {
    await click('toolbar:New'); await wait(() => globalThis.dockingScene.windows.some(w => w.id === 'new-1'));
    assert.equal((await state()).activeWindowId, 'new-1');
  });
  await test('Close and reopen through menu preserves registered identity', async () => {
    await click('close:new-1'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'new-1')?.placement === 'Hidden');
    await menu('View', 'Document 1.cs'); await wait(() => globalThis.dockingScene.activeWindowId === 'new-1');
    assert.notEqual((await state()).windows.find(w => w.id === 'new-1').placement, 'Hidden');
  });
  await test('Layout save, structural edit and restore', async () => {
    await click('toolbar:Save'); await click('close:new-1');
    await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'new-1')?.placement === 'Hidden');
    await click('toolbar:Load'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'new-1')?.placement === 'Document');
  });
  await test('Standard and tabbed MDI switching', async () => {
    await menu('Window', 'Standard MDI'); await wait(() => globalThis.dockingScene.mdiMode === 'Standard');
    await page.screenshot({ path: `${output}/standard-mdi.png` });
    await menu('Window', 'Tile vertically');
    assert((await state()).windows.filter(w => w.placement === 'Document').every(w => w.mdiBounds.width > 0 && w.mdiBounds.height > 0));
    await menu('Window', 'Tabbed MDI'); await wait(() => globalThis.dockingScene.mdiMode === 'Tabbed');
  });
  await test('Layout lock and unlock', async () => {
    await menu('Window', 'Lock layout'); await wait(() => globalThis.dockingScene.locked);
    await menu('Window', 'Unlock layout'); await wait(() => !globalThis.dockingScene.locked);
  });
  await test('Reset returns to initial registered set', async () => {
    await click('toolbar:Reset');
    await wait(() => globalThis.dockingScene.windows.length === 12 && globalThis.dockingScene.activeWindowId === 'welcome');
    assert.equal((await state()).floatingHosts.length, 0);
  });
  await test('No unhandled browser exceptions', async () => assert.deepEqual(errors, []));
} catch (error) {
  results.push({ name: 'Browser initialization', passed: false, error: String(error) }); console.error(error);
  await page.screenshot({ path: `${output}/startup-failure.png` }).catch(() => {});
} finally {
  const diagnostics = await captureDiagnostics(page);
  await writeFile(`${output}/results.json`, JSON.stringify({ url, results, errors, consoleMessages, networkFailures, diagnostics }, null, 2));
  await writeFile(`${output}/final-state.json`, JSON.stringify(diagnostics, null, 2));
  await context.tracing.stop({ path: `${output}/trace.zip` }).catch(error => console.error(error));
  await browser.close();
}
if (!results.length || results.some(test => !test.passed)) process.exitCode = 1;
