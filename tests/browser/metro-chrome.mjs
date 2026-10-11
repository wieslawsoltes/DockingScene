import assert from 'node:assert/strict';
import { pngPixels } from './painted-tabs.mjs';

// Public UI commands and actual pixels; no script writes to the model or visuals.
export async function runMetroChromeTests({page, state, test, reset, click, wait, bounds, settle, menu, output}) {
  const docs = async () => {
    const s = await state(); const group = s.groups.find(g => g.windows.includes('main'));
    return s.tabStrips.find(t => t.id === group.id);
  };
  const appearance = async id => menu('toolbar:Appearance', 'appearance:' + id);
  function near(actual, expected, message) { assert(actual.every((x, i) => Math.abs(x - expected[i]) <= 3), message + ': ' + actual); }
  await test('Metro status and tool grippers have real rendered chrome', async () => {
    await reset(); await page.mouse.move(800, 300); await settle();
    const s = await state(), status = await bounds('status:metro');
    const group = s.groups.find(g => g.windows.includes('output')); const grip = await bounds('group-grip:' + group.id);
    assert(grip.width > 50 && grip.height > 10);
    const image = pngPixels(await page.screenshot({path: `${output}/metro-light.png`}));
    near(image.rgb(Math.floor(status.x + 3), Math.floor(status.y + 3)), [0,122,204], 'Status strip');
    let dots = 0;
    for (let y = Math.ceil(grip.y + 3); y < grip.y + grip.height - 3; y++)
      for (let x = Math.ceil(grip.x + 4); x < Math.min(grip.x + 104, grip.x + grip.width - 2); x++) {
        const [r,g,b] = image.rgb(x,y); if (r < 215 && Math.abs(r-g) < 5 && Math.abs(g-b) < 5) dots++;
      }
    assert(dots > 10, 'The grip must contain painted dots, not only diagnostic geometry');
    assert.equal(s.toolTabCloseButtons, false); assert.equal(s.bounds['close:output'], undefined);
  });
  await test('Tool caption buttons survive activation changes without recreation', async () => {
    const before = (await state()).captionButtonIdentities;
    for (const id of ['output','explorer','output','welcome']) {
      await menu('menubar:Activate', 'activate:' + id); await wait(id => globalThis.dockingScene.activeWindowId === id, id);
    }
    assert.deepEqual((await state()).captionButtonIdentities, before);
  });
  await test('The dotted gripper supports captured floating and undo for the whole group', async () => {
    const s = await state(), group = s.groups.find(g => g.windows.includes('output'));
    const r = await bounds('group-grip:' + group.id);
    await page.keyboard.down('Control');
    try {
      await page.mouse.move(r.x + 35, r.y + r.height / 2); await page.mouse.down();
      await wait(() => globalThis.dockingScene.capturedPointerCount === 1);
      await page.mouse.move(550, 350, {steps: 15}); await page.mouse.up();
    } finally { await page.mouse.up(); await page.keyboard.up('Control'); }
    await wait(ids => ids.every(id => globalThis.dockingScene.windows.find(w => w.id === id).placement === 'Floating'), group.windows);
    assert.equal((await state()).capturedPointerCount, 0);
    await click('toolbar:Undo');
    await wait(ids => ids.every(id => globalThis.dockingScene.windows.find(w => w.id === id).placement === 'Docked'), group.windows);
    await reset();
  });
  await test('Contextual tab actions reveal on hover without measuring or shifting tabs', async () => {
    await page.mouse.move(800, 300); await settle();
    assert.equal((await state()).bounds['close:main'], undefined);
    const before = await docs(), r = await bounds('tab:main');
    await page.mouse.move(r.x + 15, r.y + r.height / 2); await bounds('close:main'); await settle();
    const image = pngPixels(await page.screenshot());
    near(image.rgb(Math.floor(r.x + 4), Math.floor(r.y + 3)), [217,234,247], 'Hovered tab');
    assert.deepEqual(await bounds('tab:main'), r);
    assert.equal((await docs()).textMeasurements, before.textMeasurements);
    assert.equal((await docs()).layoutPasses, before.layoutPasses);
    await page.mouse.move(800, 300); await wait(() => !globalThis.dockingScene.bounds['close:main']);
  });
  await test('A hovered unselected document can close without activating it', async () => {
    const r = await bounds('tab:main'); await page.mouse.move(r.x + 15, r.y + r.height/2);
    await click('close:main');
    await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'main').placement === 'Hidden');
    assert.equal((await state()).activeWindowId, 'welcome');
    await click('toolbar:Undo'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'main').placement === 'Document');
  });
  await test('Always-visible and tool-close options update existing controls', async () => {
    await settle(); const before = await docs(); await appearance('always');
    assert.equal((await state()).documentButtonsAlwaysVisible, true); await bounds('close:main');
    assert.equal((await docs()).textMeasurements, before.textMeasurements);
    await appearance('contextual'); await page.mouse.move(800,300); await wait(() => !globalThis.dockingScene.bounds['close:main']);
    await appearance('tool-close'); await bounds('close:output');
    await appearance('tool-no-close'); assert.equal((await state()).bounds['close:output'], undefined);
  });
  await test('Document strip highlight toggles its pixels without new tab measurements', async () => {
    const before = await docs(); const r = await bounds('tab-highlight:' + before.id);
    const x = Math.floor(r.x + r.width * .7), y = Math.ceil(r.y + r.height) - 1;
    near(pngPixels(await page.screenshot()).rgb(x,y), [0,122,204], 'Active highlight');
    await appearance('highlight'); assert.equal((await state()).documentStripHighlight, false);
    near(pngPixels(await page.screenshot()).rgb(x,y), [231,231,231], 'Removed highlight');
    assert.equal((await docs()).textMeasurements, before.textMeasurements);
    await appearance('highlight');
  });
  await test('Compact native tree rows open their document through normal pointer input', async () => {
    const r = await bounds('source-tree:DockManager.cs'); assert(r.height <= 24);
    const next = await bounds('source-tree:DockTree.cs'); assert(next.y - r.y <= 26);
    await click('source-tree:DockManager.cs'); await wait(() => globalThis.dockingScene.activeWindowId === 'code');
  });
  await test('Compact property inspector applies metadata and tracks newly registered windows', async () => {
    await reset(); await bounds('properties:apply');
    await click('properties:title'); await page.keyboard.press('Control+a'); await page.keyboard.type('Edited.xaml');
    await click('properties:readonly'); await click('properties:apply');
    await wait(() => globalThis.dockingScene.windows.some(w => w.id === 'main' && w.title === 'Edited.xaml' && w.isReadOnly));
    await click('toolbar:New'); await settle();
    const s = await state(); const id = s.activeWindowId; assert(s.inspectorWindowIds.includes(id));
    await click('properties:window'); await page.keyboard.press('Home');
    for (let i = 0; i < s.inspectorWindowIds.indexOf(id); i++) await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter'); await wait(id => globalThis.dockingScene.inspectorWindowId === id, id);
  });
  await test('Metro dark chrome preserves active document content and caption instances', async () => {
    await reset(); await click('tab:code'); await settle(); const before = await state();
    await menu('menubar:Theme','appmenu:Metro Dark');
    assert.equal((await state()).editors.code.instance, before.editors.code.instance);
    assert.deepEqual((await state()).captionButtonIdentities, before.captionButtonIdentities);
    await page.screenshot({path: `${output}/metro-dark.png`});
    await menu('menubar:Theme','appmenu:Metro Light'); await reset(); await settle();
    await page.screenshot({path: `${output}/metro-final.png`});
  });
}
