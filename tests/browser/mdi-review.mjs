import assert from 'node:assert/strict';
import { pngPixels } from './painted-tabs.mjs';

export async function runMdiReviewTests({page, state, test, reset, click, wait, bounds, settle, menu, output}) {
  const win = async id => (await state()).windows.find(w => w.id === id);
  const frame = async id => (await state()).mdiFrames[id];
  const activate = id => menu('menubar:Activate', 'activate:' + id);
  const options = id => menu('menubar:Window', 'mdi-option:' + id);
  const system = (id, command) => menu('window:' + id + ':system', 'window-menu:' + id + ':' + command);
  await test('Standard MDI retains caption buttons across repeated activation', async () => {
    await reset(); await menu('menubar:Window', 'appmenu:Standard MDI'); await settle();
    const before = (await state()).mdiFrames;
    for (const id of ['main', 'code', 'main', 'code']) await activate(id);
    const after = (await state()).mdiFrames;
    for (const id of Object.keys(before)) assert.deepEqual(after[id].buttons, before[id].buttons);
    const title = await bounds('window-title:code');
    const rgb = pngPixels(await page.screenshot()).rgb(Math.floor(title.x + 3), Math.floor(title.y + 3));
    assert(rgb.every((value, i) => Math.abs(value - [0,122,204][i]) <= 3), 'Active MDI caption has stale native paint: ' + rgb);
  });
  await test('MDI system maximize and caption restore preserve bounds and editor identity', async () => {
    const before = await win('code'), instance = (await state()).editors.code.instance;
    await system('code', 'maximize'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Maximized');
    assert.deepEqual((await win('code')).mdiBounds, before.mdiBounds);
    await click('window:code:restore'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Normal');
    assert.deepEqual((await win('code')).mdiBounds, before.mdiBounds);
    assert.equal((await state()).editors.code.instance, instance);
  });
  await test('Minimized MDI caption restores without destroying normal bounds', async () => {
    const before = await win('code'), instance = (await state()).editors.code.instance;
    await click('window:code:minimize'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Minimized');
    await bounds('window:code:restore');
    assert.deepEqual((await win('code')).mdiBounds, before.mdiBounds);
    assert.equal((await state()).bounds['window:code:minimize'], undefined);
    await click('window:code:restore'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Normal');
    assert.equal((await state()).editors.code.instance, instance);
    assert.deepEqual((await win('code')).mdiBounds, before.mdiBounds);
  });
  await test('MDI presentation policies disable buttons and double-click but allow explicit model commands', async () => {
    await options('toggle-maximize'); await options('toggle-minimize'); await activate('code');
    assert.equal((await state()).mdiCanMaximize, false); assert.equal((await state()).mdiCanMinimize, false);
    assert.equal((await state()).bounds['window:code:maximize'], undefined); assert.equal((await state()).bounds['window:code:minimize'], undefined);
    await click('window-title:code', {clickCount: 2}); await settle(); assert.equal((await win('code')).mdiState, 'Normal');
    await options('maximize-active'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Maximized');
    await click('window:code:restore'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Normal');
    await options('toggle-maximize'); await options('toggle-minimize');
  });
  await test('MDI pointer movement owns capture and Escape cancels without committing', async () => {
    await activate('code'); const before = (await win('code')).mdiBounds, r = await bounds('window-title:code'), z = (await frame('code')).z;
    await page.mouse.move(r.x + 25, r.y + r.height/2); await page.mouse.down();
    try {
      await wait(() => globalThis.dockingScene.mdiFrames.code.moving);
      await page.mouse.move(r.x + 145, r.y + r.height/2 + 60, {steps: 12});
      await wait(x => globalThis.dockingScene.mdiFrames.code.x > x + 50, before.x);
      assert.equal((await frame('code')).z, z, 'Provisional movement must retain the active frame stacking order');
      await page.keyboard.press('Escape');
      await wait(() => !globalThis.dockingScene.mdiFrames.code.moving);
    } finally { await page.mouse.up(); }
    await settle(); assert.deepEqual((await win('code')).mdiBounds, before);
    assert.equal((await frame('code')).x, before.x);
  });
  await test('MDI title drag commits new coordinates through the move command', async () => {
    const before = (await win('code')).mdiBounds, r = await bounds('window-title:code');
    await page.mouse.move(r.x + 25, r.y + r.height/2); await page.mouse.down();
    try {
      await wait(() => globalThis.dockingScene.mdiFrames.code.moving);
      await page.mouse.move(r.x + 105, r.y + r.height/2 + 35, {steps: 12});
    } finally { await page.mouse.up(); }
    await wait(before => { const b = globalThis.dockingScene.windows.find(w => w.id === 'code').mdiBounds; return b.x > before.x + 50 && b.y > before.y + 20; }, before);
    const after = (await win('code')).mdiBounds; assert.equal(after.width, before.width); assert.equal(after.height, before.height);
  });
  await test('Minimized MDI windows wrap into nonoverlapping rows and retain restored bounds', async () => {
    for (let i = 0; i < 6; i++) { await click('toolbar:New'); await settle(); }
    const before = (await state()).windows.filter(w => w.kind === 'Document' && w.placement === 'Document');
    await options('minimize-all');
    await wait(ids => ids.every(id => globalThis.dockingScene.windows.find(w => w.id === id).mdiState === 'Minimized'), before.map(w => w.id));
    await options('arrange'); const frames = (await state()).mdiFrames; const rectangles = Object.values(frames);
    assert(rectangles.length >= 10); assert(new Set(rectangles.map(r => r.y)).size > 1);
    for (const w of before) assert.deepEqual((await win(w.id)).mdiBounds, w.mdiBounds);
    for (let i = 0; i < rectangles.length; i++) for (let j = i+1; j < rectangles.length; j++) {
      const a = rectangles[i], b = rectangles[j];
      assert(a.x+a.width <= b.x || b.x+b.width <= a.x || a.y+a.height <= b.y || b.y+b.height <= a.y, 'Minimized captions overlap');
    }
    await page.screenshot({path: `${output}/mdi-minimized.png`});
    await activate('code'); await click('window:code:restore');
    await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Normal');
    assert.deepEqual((await win('code')).mdiBounds, before.find(w => w.id === 'code').mdiBounds);
  });
  await test('MDI title context menu closes only its target and returns cleanly to tabbed mode', async () => {
    await activate('code'); await click('window-title:code', {button: 'right'}); await settle();
    await click('window-menu:code:close'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').placement === 'Hidden');
    assert.notEqual((await win('main')).placement, 'Hidden');
    await reset(); await settle(); await page.screenshot({path: `${output}/mdi-review-final.png`});
  });
}
