import assert from 'node:assert/strict';

export async function runMdiPresentationTests({page, state, test, reset, click, wait, bounds, settle, menu, output}) {
  const activate = id => menu('menubar:Activate', 'activate:' + id);
  const primary = action => menu('menubar:Window', 'mdi-primary:' + action);
  const model = async id => (await state()).windows.find(w => w.id === id);
  const toggle = () => menu('menubar:Window', 'mdi-option:frames');
  await test('Maximized MDI frames disappear without losing editor space or content', async () => {
    await reset(); await menu('menubar:Window', 'appmenu:Standard MDI'); await activate('code');
    const original = (await model('code')).mdiBounds;
    await primary('maximize'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Maximized');
    const before = await bounds('editor:code'), instance = (await state()).editors.code.instance;
    await toggle(); await wait(() => globalThis.dockingScene.mdiExternalVisible && !globalThis.dockingScene.mdiFrames.code.frameVisible);
    const after = await bounds('editor:code'); assert(after.height >= before.height + 23); assert(after.width >= before.width);
    assert.equal((await state()).bounds['window-title:code'], undefined); await bounds('mdi-external:restore');
    assert.equal((await state()).editors.code.instance, instance); assert.deepEqual((await model('code')).mdiBounds, original);
    // Input close to the top edge must reach content, not an invisible resize grip.
    await page.mouse.click(after.x + 40, after.y + 2); await page.keyboard.press('Control+a'); await page.keyboard.type('frameless-editor');
    await wait(() => globalThis.dockingScene.editors.code.text === 'frameless-editor');
    await page.screenshot({path: `${output}/mdi-frameless-light.png`});
  });
  await test('External MDI commands retain the document target after tool activation', async () => {
    await activate('output'); await wait(() => globalThis.dockingScene.activeWindowId === 'output');
    assert.equal((await state()).mdiPrimaryId, 'code'); assert.equal((await state()).mdiExternalVisible, true);
    await click('mdi-external:restore'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Normal');
    await bounds('window-title:code'); assert.equal((await state()).mdiExternalVisible, false);
    assert.equal((await state()).activeWindowId, 'output');
    await primary('maximize'); await wait(() => globalThis.dockingScene.mdiExternalVisible);
  });
  await test('External state controls obey locking and reactivate without rebuilding', async () => {
    const before = (await state()).mdiExternalButtons;
    await menu('menubar:Window', 'appmenu:Lock layout'); await settle();
    const locked = (await state()).mdiExternalButtons;
    assert.equal(locked.restore.enabled, false); assert.equal(locked.minimize.enabled, false); assert.equal(locked.close.enabled, true);
    await click('mdi-external:restore'); await settle(); assert.equal((await model('code')).mdiState, 'Maximized');
    await menu('menubar:Window', 'appmenu:Unlock layout'); await settle();
    for (const name of Object.keys(before)) assert.equal((await state()).mdiExternalButtons[name].instance, before[name].instance);
    assert.equal((await state()).mdiExternalButtons.restore.enabled, true);
  });
  await test('Frameless MDI dark theme retains editor and external command objects', async () => {
    const before = await state(); await menu('menubar:Theme', 'appmenu:Metro Dark');
    assert.equal((await state()).editors.code.instance, before.editors.code.instance);
    assert.deepEqual((await state()).mdiExternalButtons, before.mdiExternalButtons);
    await page.screenshot({path: `${output}/mdi-frameless-dark.png`});
    await menu('menubar:Theme', 'appmenu:Metro Light');
  });
  await test('External minimize restore and close act on the hosted primary only', async () => {
    const original = (await model('code')).mdiBounds;
    await click('mdi-external:minimize'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Minimized');
    await primary('restore'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Normal');
    assert.deepEqual((await model('code')).mdiBounds, original);
    await primary('maximize'); await wait(() => globalThis.dockingScene.mdiExternalVisible);
    await click('mdi-external:close'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').placement === 'Hidden');
    assert.notEqual((await model('output')).placement, 'Hidden'); assert.notEqual((await state()).mdiPrimaryId, 'code');
    await toggle(); await reset();
  });
  await test('Minimized caption layout is reused across activation and color-only themes', async () => {
    await menu('menubar:Window', 'appmenu:Standard MDI');
    await activate('readme'); await primary('minimize'); await settle();
    const before = await state();
    for (let i = 0; i < 12; i++) await activate(i % 2 ? 'code' : 'main');
    const after = await state(); assert.equal(after.minimizedLayoutBuilds, before.minimizedLayoutBuilds);
    assert(after.minimizedLayoutReuses > before.minimizedLayoutReuses);
    await menu('menubar:Theme', 'appmenu:Metro Dark');
    assert.equal((await state()).minimizedLayoutBuilds, before.minimizedLayoutBuilds);
    await menu('menubar:Theme', 'appmenu:Metro Light'); await reset();
    assert.equal((await state()).mdiPrimaryId, null); assert.equal((await state()).mdiExternalVisible, false);
  });
}
