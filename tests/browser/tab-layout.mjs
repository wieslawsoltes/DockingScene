import { chromium } from 'playwright';
import { mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { captureDiagnostics } from './diagnostics.mjs';
import { runPerformanceRegressions } from './performance-regressions.mjs';
import { readHitBounds } from './hit-targets.mjs';
import { validateHitTargetReader } from './hit-targets.contract.mjs';

await validateHitTargetReader();

const output = `${process.env.BROWSER_RESULTS || 'artifacts/browser-results'}/tab-layout`;
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ headless: true, args: ['--enable-webgl', '--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const context = await browser.newContext({ viewport: { width: 1440, height: 960 } });
await context.tracing.start({ screenshots: true, snapshots: true, sources: true });
const page = await context.newPage();
const errors = [], results = [], consoleMessages = [];
page.on('pageerror', e => errors.push(String(e)));
page.on('console', m => consoleMessages.push(`${m.type()}: ${m.text()}`));
const address = new URL(process.env.BROWSER_URL || 'http://127.0.0.1:8765/DockingScene/'); address.searchParams.set('diagnostics', '1');
const state = () => page.evaluate(() => globalThis.dockingScene);
const strip = async () => (await state()).tabStrips.find(t => t.id === 'playground');
async function wait(fn, arg) { await page.waitForFunction(fn, arg, { timeout: 15000 }); }
async function settle() { const s = (await state()).sequence; await wait(s => globalThis.dockingScene.sequence > s + 1, s); }
async function bounds(id) { return readHitBounds(page, id); }
async function click(id, options = {}) { const r = await bounds(id); await page.mouse.click(r.x + Math.min(r.width / 2, 45), r.y + r.height / 2, options); }
const menusGone = () => wait(() => !Object.keys(globalThis.dockingScene.bounds).some(id => id.startsWith('tab-options:') || id.startsWith('tab-list:') || id === 'play:custom-menu'));
async function setting(title) { await menusGone(); await click('toolbar:TabOptions'); await settle(); await click('tab-options:' + title); await menusGone(); await settle(); }
async function mode(value) { await click('play:mode:' + value); await settle(); assert.equal((await strip()).overflow, value); }
async function selectMenu(id) { await click('tabs:playground:overflow'); await settle(); await click('tab-list:' + id); await menusGone(); await wait(id => globalThis.dockingScene.tabStrips.find(t => t.id === 'playground').selectedId === id, id); await settle(); }
async function test(name, action) {
  const start = Date.now();
  try { await action(); results.push({ name, passed: true, elapsedMs: Date.now() - start }); console.log('PASS ' + name); }
  catch (error) {
    results.push({ name, passed: false, error: String(error), elapsedMs: Date.now() - start }); console.error('FAIL ' + name + ': ' + error);
    await page.screenshot({ path: `${output}/failure-${results.length}.png` }).catch(() => {});
    await writeFile(`${output}/failure-${results.length}.json`, JSON.stringify(await captureDiagnostics(page), null, 2));
    await page.mouse.up().catch(() => {}); await page.keyboard.press('Escape').catch(() => {});
  }
}
try {
  await page.goto(address.href, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForFunction(() => globalThis.dockingScene?.ready || globalThis.dockingSceneStartup?.error, null, { timeout: 90000 });
  assert(!(await captureDiagnostics(page)).startup?.error);
  await page.locator('.uno-loader').waitFor({ state: 'hidden', timeout: 30000 }); await settle();
  await test('Standalone control creates only selected content', async () => {
    await setting('Open standalone tab playground'); await wait(() => globalThis.dockingScene.tabStrips?.some(t => t.id === 'playground'));
    const s = await strip(); assert.equal(s.count, 20); assert.equal(s.selectedId, 'play-0');
    assert.equal(s.items.filter(i => i.contentCreated).length, 1); assert(s.realized < s.count);
    await page.screenshot({ path: `${output}/playground.png` });
  });
  await test('Normal wheel scrolling leaves pinned and preview headers stationary', async () => {
    await mode('ScrollWithMenu');
    const pinned = await bounds('tab:play-pinned'), preview = await bounds('tab:play-preview'), normal = await bounds('tab:play-0');
    await page.mouse.move(normal.x + 20, normal.y + normal.height / 2); await page.mouse.wheel(0, 400);
    await wait(() => globalThis.dockingScene.tabStrips.find(t => t.id === 'playground').lanes.find(l => l.mode === 'Normal').offset > 200);
    assert(Math.abs((await bounds('tab:play-pinned')).x - pinned.x) < 1);
    assert(Math.abs((await bounds('tab:play-preview')).x - preview.x) < 1);
  });
  await test('Menu-only overflow reveals a previously hidden last tab', async () => {
    await mode('Menu'); assert.equal((await state()).bounds['tabs:playground:back'], undefined);
    await selectMenu('play-17'); await bounds('tab:play-17'); assert((await strip()).lanes.find(l => l.mode === 'Normal').offset > 0);
  });
  await test('Global shrinking fits all tabs when their minima are feasible', async () => {
    await click('play:compact'); await mode('ShrinkWithMenu');
    const s = await strip(); assert(s.lanes.every(l => l.contentExtent <= l.extent + .01)); assert.equal(s.realized, 20);
    await page.screenshot({ path: `${output}/shrink.png` });
  });
  await test('Every compressed header retains a working pointer selection target', async () => {
    const ids = ['play-pinned', 'play-preview', ...Array.from({ length: 18 }, (_, i) => 'play-' + i)];
    for (const id of ids) {
      const r = await bounds('tab:' + id); assert(r.width >= 20, 'A compressed tab lost its label hit target: ' + id);
      await click('tab:' + id); await wait(id => globalThis.dockingScene.tabStrips.find(t => t.id === 'playground').selectedId === id, id);
    }
    assert.equal((await strip()).count, 20); assert.equal((await strip()).selectedId, 'play-17');
  });
  await test('Fixed tab extents survive an overconstrained viewport', async () => {
    await click('play:fixed'); await settle(); const s = await strip();
    assert.equal(s.minimum, 100); assert.equal(s.maximum, 100);
    for (const lane of s.lanes) assert(Math.abs(lane.contentExtent - lane.count * 100) < .01);
    assert(s.lanes.some(l => l.contentExtent > l.extent));
  });
  await test('None mode disables overflow affordances and resets offsets', async () => {
    await mode('None'); const s = await state();
    for (const name of ['back', 'forward', 'overflow']) assert.equal(s.bounds['tabs:playground:' + name], undefined);
    assert((await strip()).lanes.every(l => l.offset === 0));
  });
  await test('Hiding the strip reclaims space without recreating content', async () => {
    const before = (await strip()).items.find(i => i.id === 'play-17').contentInstance;
    const editor = await bounds('play-editor:play-17'); await click('play:strip'); await settle();
    assert.equal((await strip()).visible, false); assert.equal((await strip()).realized, 0);
    assert((await bounds('play-editor:play-17')).height > editor.height + 15);
    assert.equal((await strip()).items.find(i => i.id === 'play-17').contentInstance, before);
    await click('play:strip'); await settle(); assert.equal((await strip()).visible, true);
  });
  await test('All four placements retain the selected content instance', async () => {
    await mode('Menu'); await click('play:measured'); await settle();
    const before = (await strip()).items.find(i => i.id === 'play-17').contentInstance;
    for (const placement of ['Left', 'Right', 'Bottom', 'Top']) {
      await click('play:placement:' + placement); await settle(); assert.equal((await strip()).placement, placement);
      assert.equal((await strip()).items.find(i => i.id === 'play-17').contentInstance, before); await bounds('tab:play-17');
    }
  });
  await test('Scroll mode exposes arrows without a menu', async () => {
    await mode('Scroll'); assert.equal((await state()).bounds['tabs:playground:overflow'], undefined);
    const before = (await strip()).lanes.find(l => l.mode === 'Normal').offset;
    await click('tabs:playground:back'); await settle(); assert((await strip()).lanes.find(l => l.mode === 'Normal').offset < before);
  });
  await test('Shrink mode fits feasible tabs without a menu', async () => {
    await click('play:compact'); await mode('Shrink');
    assert((await strip()).lanes.every(l => l.contentExtent <= l.extent + .01)); assert.equal((await state()).bounds['tabs:playground:overflow'], undefined);
  });
  await test('Standalone pin and unpin actions update real item metadata', async () => {
    await click('play:measured'); await mode('Menu'); await selectMenu('play-0');
    await click('pin:play-0'); await settle(); assert((await strip()).items.find(i => i.id === 'play-0').pinned);
    await click('pin:play-0'); await settle(); assert.equal((await strip()).items.find(i => i.id === 'play-0').pinned, false);
  });
  await test('Standalone TabClosing cancellation prevents removal', async () => {
    await selectMenu('play-0'); const count = (await strip()).count; const calls = (await state()).playgroundClosingCount;
    await click('close:play-0'); await settle(); assert.equal((await strip()).count, count);
    assert.equal((await state()).playgroundClosingCount, calls + 1); assert.equal((await state()).playgroundClosingId, 'play-0');
    await click('play:veto'); await click('close:play-0'); await settle(); assert.equal((await strip()).count, count - 1);
  });
  await test('CanClose false also prevents middle-click closing', async () => {
    const count = (await strip()).count, calls = (await state()).playgroundClosingCount;
    assert.equal((await state()).bounds['close:play-pinned'], undefined);
    await click('tab:play-pinned', { button: 'middle' }); await settle();
    assert.equal((await strip()).count, count); assert.equal((await state()).playgroundClosingCount, calls);
  });
  await test('MenuOpening supports application-added actions', async () => {
    await click('tabs:playground:overflow'); await settle(); await click('play:custom-menu'); await menusGone(); await settle();
    assert.equal((await strip()).selectedId, 'play-preview');
  });
  await test('Single tool-tab visibility changes without replacing the workspace', async () => {
    const generation = (await state()).workspaceGeneration;
    await setting('Show single tool tabs'); await bounds('tab:properties');
    await setting('Hide single tool tabs'); assert.equal((await state()).bounds['tab:properties'], undefined);
    assert.equal((await state()).workspaceGeneration, generation); await bounds('play-editor:play-preview');
  });
  await test('Digit nine and preview shortcuts use filtered positions', async () => {
    for (let i = 0; i < 9; i++) { await click('toolbar:New'); await settle(); }
    const s = await state(); const group = s.groups.find(g => g.windows.includes(s.activeWindowId));
    const normal = group.windows.filter(id => s.windows.find(w => w.id === id).displayMode === 'Normal');
    assert(normal.length > 9);
    await page.keyboard.press('Control+1'); await wait(id => globalThis.dockingScene.activeWindowId === id, normal[0]);
    await page.keyboard.press('Control+9'); await wait(id => globalThis.dockingScene.activeWindowId === id, normal.at(-1));
    await page.keyboard.press('Control+Shift+1'); await wait(() => globalThis.dockingScene.activeWindowId === 'preview');
    await page.keyboard.press('Control+Alt+9'); await wait(() => globalThis.dockingScene.activeWindowId === 'welcome');
  });
  await runPerformanceRegressions({ page, state, strip, test, wait, settle, bounds, click, setting, mode });
  await test('No unhandled exceptions and verified deployment identity', async () => {
    assert.deepEqual(errors, []); assert.equal((await captureDiagnostics(page)).diagnosticError, null);
    if (process.env.EXPECTED_BUILD_COMMIT) {
      const response = await page.request.get(new URL('build-info.json', address).href); assert(response.ok());
      assert.equal((await response.json()).commit, process.env.EXPECTED_BUILD_COMMIT);
    }
  });
} catch (error) { results.push({ name: 'Initialization', passed: false, error: String(error) }); }
finally {
  await writeFile(`${output}/results.json`, JSON.stringify({ url: address.href, results, errors, consoleMessages, diagnostics: await captureDiagnostics(page) }, null, 2));
  await context.tracing.stop({ path: `${output}/trace.zip` }).catch(() => {}); await browser.close();
}
if (!results.length || results.some(r => !r.passed)) process.exitCode = 1;
