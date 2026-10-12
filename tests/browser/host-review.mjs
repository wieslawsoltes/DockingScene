import { runMdiPolicyTests } from './mdi-policy.mjs';
import { runWindowKeyboardTests } from './window-keyboard.mjs';
import { runSelectorReview } from './selector-review.mjs';
import { chromium } from 'playwright';
import { mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { captureDiagnostics } from './diagnostics.mjs';
import { readHitBounds } from './hit-targets.mjs';
import { pngPixels } from './painted-tabs.mjs';
import { runMetroChromeTests } from './metro-chrome.mjs';
import { runMdiReviewTests } from './mdi-review.mjs';

const output = `${process.env.BROWSER_RESULTS || 'artifacts/browser-results'}/host-review`;
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ headless: true, args: ['--enable-webgl', '--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const context = await browser.newContext({ viewport: { width: 1440, height: 960 } });
await context.tracing.start({ screenshots: true, snapshots: true, sources: true });
const page = await context.newPage();
const results = [], errors = [], consoleMessages = [];
page.on('pageerror', e => errors.push(String(e))); page.on('console', m => consoleMessages.push(`${m.type()}: ${m.text()}`));
const address = new URL(process.env.BROWSER_URL || 'http://127.0.0.1:8765/DockingScene/'); address.searchParams.set('diagnostics', '1');
const state = () => page.evaluate(() => globalThis.dockingScene);
const wait = (fn, arg) => page.waitForFunction(fn, arg, { timeout: 15000 });
const bounds = id => readHitBounds(page, id);
const strip = async () => (await state()).tabStrips.find(t => t.id === 'playground');
async function settle() { const s = (await state()).sequence; await wait(s => globalThis.dockingScene.sequence > s + 1, s); }
async function click(id, options = {}) { const r = await bounds(id); await page.mouse.click(r.x + Math.min(r.width / 2, 45), r.y + r.height / 2, options); }
async function menu(button, item) { await click(button); await settle(); await click(item); await settle(); }
async function reset() {
  await page.keyboard.press('Escape'); const generation = (await state()).workspaceGeneration;
  await click('toolbar:Reset');
  await wait(g => globalThis.dockingScene.workspaceGeneration > g && globalThis.dockingScene.activeWindowId === 'welcome', generation); await settle();
}
async function test(name, action) {
  const start = Date.now();
  try { await action(); results.push({ name, passed: true, elapsedMs: Date.now() - start }); console.log('PASS ' + name); }
  catch (error) {
    results.push({ name, passed: false, elapsedMs: Date.now() - start, error: String(error) }); console.error('FAIL ' + name + ': ' + error);
    await page.screenshot({ path: `${output}/failure-${results.length}.png` }).catch(() => {});
    await writeFile(`${output}/failure-${results.length}.json`, JSON.stringify(await captureDiagnostics(page), null, 2));
    await page.mouse.up().catch(() => {}); await page.keyboard.press('Escape').catch(() => {});
  }
}
async function edgeDrag({ cancel = false, disabled = false, vertical = false } = {}) {
  const r = await bounds('tab:play-0'), host = await bounds('play:tabs');
  const s = await strip(), lane = s.lanes.find(l => l.mode === 'Normal');
  const x = vertical ? r.x + Math.min(40, r.width / 2) : host.x + lane.start + lane.extent - 2;
  const y = vertical ? host.y + lane.start + lane.extent - 2 : r.y + r.height / 2;
  await page.mouse.move(r.x + Math.min(30, r.width / 2), r.y + r.height / 2); await page.mouse.down();
  try {
    await wait(() => globalThis.dockingScene.tabStrips.find(t => t.id === 'playground').dragActive);
    await page.mouse.move(x, y, { steps: 12 });
    if (disabled) {
      await settle(); assert.equal((await strip()).lanes.find(l => l.mode === 'Normal').offset, lane.offset);
    } else await wait(offset => globalThis.dockingScene.tabStrips.find(t => t.id === 'playground').lanes.find(l => l.mode === 'Normal').offset > offset + 250, lane.offset);
    if (cancel || disabled) await page.keyboard.press('Escape');
  } finally { await page.mouse.up(); }
  await wait(() => !globalThis.dockingScene.tabStrips.find(t => t.id === 'playground').dragActive); await settle();
}
try {
  await page.goto(address.href, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForFunction(() => globalThis.dockingScene?.ready || globalThis.dockingSceneStartup?.error, null, { timeout: 90000 });
  assert(!(await captureDiagnostics(page)).startup?.error); await page.locator('.uno-loader').waitFor({ state: 'hidden', timeout: 30000 }); await settle();
  await test('A tab click focuses its editor for immediate typing without a second click', async () => {
    for (let i = 0; i < 12; i++) {
      await reset(); await click('tab:code');
      // No content click or focus wait: typed input follows the tab pointer release.
      await page.keyboard.press('Control+a'); await page.keyboard.type('tab-focus-' + i);
      await wait(text => globalThis.dockingScene.editors.code?.text === text, 'tab-focus-' + i);
      await click('editor:code'); await page.keyboard.press('Control+a'); await page.keyboard.type('body-focus-' + i);
      await wait(text => globalThis.dockingScene.editors.code?.text === text, 'body-focus-' + i);
      assert.equal((await state()).focusedId, 'editor:code');
    }
  });
  await test('Switching tabs restores the editor caret and selected text', async () => {
    await page.keyboard.press('Control+a'); await page.keyboard.type('abcde');
    await page.keyboard.press('Control+Home'); await page.keyboard.press('Shift+ArrowRight'); await page.keyboard.press('Shift+ArrowRight');
    await wait(() => globalThis.dockingScene.editors.code.selectionLength === 2);
    const instance = (await state()).editors.code.instance;
    await click('tab:main'); await page.keyboard.press('Control+a'); await page.keyboard.type('main-stays');
    await click('tab:code'); await page.keyboard.type('XY');
    await wait(() => globalThis.dockingScene.editors.code.text === 'XYcde');
    assert.equal((await state()).editors.code.instance, instance); assert.equal((await state()).editors.main.text, 'main-stays');
  });
  await test('Repeated activation reuses geometry and every auxiliary control', async () => {
    await settle(); const before = await state();
    for (let i = 0; i < 20; i++) { await click('tab:' + (i % 2 ? 'code' : 'main')); await settle(); }
    const after = await state(); assert.equal(after.geometryBuilds, before.geometryBuilds);
    assert(after.geometryReuses > before.geometryReuses); assert.equal(after.auxiliaryCreated, before.auxiliaryCreated);
    assert.deepEqual(after.auxiliaryIdentities, before.auxiliaryIdentities);
    await page.mouse.move(800, 300); await settle();
    const active = await bounds('tab:code'), inactive = await bounds('tab:main');
    const image = pngPixels(await page.screenshot());
    const scale = image.width / page.viewportSize().width;
    for (const [rect, expected] of [[active, [0,122,204]], [inactive, [231,231,231]]]) {
      const actual = image.rgb(Math.round((rect.x + 4) * scale), Math.round((rect.y + 3) * scale));
      assert(actual.every((value, i) => Math.abs(value - expected[i]) <= 3), 'Retained tab native paint is stale: ' + actual);
    }
    console.log('HOST_REUSE ' + JSON.stringify({ builds: after.geometryBuilds - before.geometryBuilds, reuses: after.geometryReuses - before.geometryReuses, created: after.auxiliaryCreated - before.auxiliaryCreated }));
  });
  await test('Retained splitter keyboard input uses fresh geometry on successive edits', async () => {
    const before = await state();
    const [id] = Object.entries(before.bounds).find(([id, r]) => id.startsWith('splitter:') && r.width < 10 && r.height > 500);
    const key = 'split:' + id.slice('splitter:'.length), instance = before.auxiliaryIdentities[key];
    await click(id); await settle(); const width = (await state()).groups.find(g => g.windows.includes('code')).bounds.width;
    await page.keyboard.press('ArrowLeft');
    await wait(width => Math.abs(globalThis.dockingScene.groups.find(g => g.windows.includes('code')).bounds.width - width) > 5, width);
    const next = (await state()).groups.find(g => g.windows.includes('code')).bounds.width;
    await page.keyboard.press('ArrowLeft');
    await wait(width => Math.abs(globalThis.dockingScene.groups.find(g => g.windows.includes('code')).bounds.width - width) > 5, next);
    assert.equal((await state()).auxiliaryIdentities[key], instance);
  });
  await test('Color-only theme changes retain host geometry and controls', async () => {
    const before = await state();
    await menu('menubar:Theme', 'appmenu:Metro Dark');
    const after = await state(); assert.equal(after.geometryBuilds, before.geometryBuilds); assert.deepEqual(after.auxiliaryIdentities, before.auxiliaryIdentities);
    await page.screenshot({ path: `${output}/retained-dark.png` }); await menu('menubar:Theme', 'appmenu:Metro Light');
  });
  await test('Dragging at the normal-lane edge scrolls to hidden destinations and commits', async () => {
    await reset(); await menu('toolbar:TabOptions', 'tab-options:Open standalone tab playground');
    const before = await strip(); const instance = before.items.find(i => i.id === 'play-0').contentInstance;
    await edgeDrag(); const after = await strip();
    assert(after.items.findIndex(i => i.id === 'play-0') > 2); assert(after.autoScrollSteps > before.autoScrollSteps);
    assert.equal(after.textMeasurements, before.textMeasurements); assert.equal(after.items.find(i => i.id === 'play-0').contentInstance, instance);
    await page.screenshot({ path: `${output}/edge-scrolled.png` });
  });
  await test('Escape cancels edge scrolling and removes its timer work', async () => {
    await click('perf:small'); await settle(); const before = (await strip()).items.map(i => i.id);
    await edgeDrag({ cancel: true }); const steps = (await strip()).autoScrollSteps;
    assert.deepEqual((await strip()).items.map(i => i.id), before); await settle(); assert.equal((await strip()).autoScrollSteps, steps);
  });
  await test('Disabled edge scrolling performs no scrolling while retaining cancellation', async () => {
    await click('perf:small'); await settle(); await menu('toolbar:Interaction', 'interaction:disable-scroll');
    assert.equal((await strip()).canAutoScroll, false); const before = await strip();
    await edgeDrag({ disabled: true }); assert.equal((await strip()).autoScrollSteps, before.autoScrollSteps);
    assert.deepEqual((await strip()).items.map(i => i.id), before.items.map(i => i.id));
    await menu('toolbar:Interaction', 'interaction:enable-scroll');
  });
  await test('Vertical edge scrolling keeps its axis and cancels without reordering', async () => {
    await click('perf:large'); await click('play:placement:Left'); await settle();
    const before = (await strip()).items.map(i => i.id); await edgeDrag({ vertical: true, cancel: true });
    assert.deepEqual((await strip()).items.map(i => i.id), before); assert.equal((await strip()).placement, 'Left');
  });
  await test('Selected content never reclaims a deliberate child focus transfer', async () => {
    await menu('toolbar:Interaction', 'interaction:handoff'); await bounds('focus:editor');
    await click('focus:editor'); await page.keyboard.press('Control+a'); await page.keyboard.type('handoff-stays');
    await wait(() => globalThis.dockingScene.focusedId === 'focus:editor');
    await click('focus:external');
    await wait(() => globalThis.dockingScene.focusedId === 'toolbar:Interaction');
    // Space must operate the external button, not the document's first control.
    await page.keyboard.press('Space'); await bounds('interaction:handoff');
    await page.keyboard.press('Escape'); await settle();
  });
  await runSelectorReview({page, state, test, reset, click, wait, settle});
  await runMetroChromeTests({ page, state, test, reset, click, wait, bounds, settle, menu, output });
  await runMdiReviewTests({ page, state, test, reset, click, wait, bounds, settle, menu, output });
  await runWindowKeyboardTests({page,state,test,reset,click,wait,bounds,settle,menu,output});
  await runMdiPolicyTests({page,state,test,reset,click,wait,bounds,settle,menu,output});
  await test('No unhandled exceptions and exact deployed revision', async () => {
    assert.deepEqual(errors, []); assert.equal((await captureDiagnostics(page)).diagnosticError, null);
    if (process.env.EXPECTED_BUILD_COMMIT) {
      const response = await page.request.get(new URL('build-info.json', address).href); assert(response.ok());
      assert.equal((await response.json()).commit, process.env.EXPECTED_BUILD_COMMIT);
    }
  });
} catch (error) { results.push({ name: 'Initialization', passed: false, error: String(error) }); console.error(error); }
finally {
  await writeFile(`${output}/results.json`, JSON.stringify({ url: address.href, results, errors, consoleMessages, diagnostics: await captureDiagnostics(page) }, null, 2));
  await context.tracing.stop({ path: `${output}/trace.zip` }).catch(() => {}); await browser.close();
}
if (!results.length || results.some(r => !r.passed)) process.exitCode = 1;
