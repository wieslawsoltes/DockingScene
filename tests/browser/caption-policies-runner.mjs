import { runCaptionPolicyTests } from './caption-policies.mjs';
import { runGroupGeometryTests } from './group-geometry.mjs';
import { runToolTabVisibilityTests } from './tool-tab-visibility.mjs';
import { chromium } from 'playwright';
import { mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { captureDiagnostics } from './diagnostics.mjs';
import { readHitBounds } from './hit-targets.mjs';

const output = `${process.env.BROWSER_RESULTS || 'artifacts/browser-results'}/caption-policies`;
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
try {
  await page.goto(address.href, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForFunction(() => globalThis.dockingScene?.ready || globalThis.dockingSceneStartup?.error, null, { timeout: 90000 });
  assert(!(await captureDiagnostics(page)).startup?.error); await page.locator('.uno-loader').waitFor({ state: 'hidden', timeout: 30000 }); await settle();
  await runCaptionPolicyTests({page,state,test,reset,click,wait,bounds,settle,menu,output});
  await runGroupGeometryTests({page,state,test,reset,click,wait,bounds,settle,menu,output});
  await runToolTabVisibilityTests({page,state,test,reset,click,wait,bounds,settle,menu,output});
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
