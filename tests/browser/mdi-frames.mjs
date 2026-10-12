import assert from 'node:assert/strict';

/** Actual external command-bar input, not calls into the managed model. */
export async function runMdiFrameTests({page, state, test, reset, click, wait, bounds, settle, menu, output}) {
  const activate = id => menu('menubar:Activate', 'activate:' + id);
  const option = id => menu('menubar:Window', 'mdi-option:' + id);
  const model = async id => (await state()).windows.find(w => w.id === id);
  let normalBounds, editorInstance, captionButtons, externalButtons;
  await test('External MDI commands resolve the primary document while a tool is active', async () => {
    await reset(); await menu('menubar:Window', 'appmenu:Standard MDI'); await activate('code'); await settle();
    normalBounds = (await model('code')).mdiBounds;
    editorInstance = (await state()).editors.code.instance;
    captionButtons = (await state()).mdiFrames.code.buttons;
    externalButtons = (await state()).mdiExternalButtons;
    await activate('explorer');
    assert.equal((await state()).activeWindowId, 'explorer'); assert.equal((await state()).mdiPrimaryWindowId, 'code');
    await click('mdi-primary:maximize');
    await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Maximized');
    assert.equal((await model('explorer')).mdiState, 'Normal'); assert.deepEqual((await model('code')).mdiBounds, normalBounds);
    await activate('code');
  });
  await test('Hidden maximized frames reclaim caption and border space without replacing content', async () => {
    const before = await bounds('editor:code'); await option('hide-frames');
    await wait(() => !globalThis.dockingScene.mdiFrames.code.frameVisible && !globalThis.dockingScene.bounds['window-title:code']);
    const after = await bounds('editor:code');
    assert(after.y < before.y - 20 && after.height > before.height + 20, 'Caption space must return to actual document content');
    assert.equal((await state()).editors.code.instance, editorInstance);
    assert.deepEqual((await state()).mdiFrames.code.buttons, captionButtons);
    await bounds('mdi-primary:restore'); await bounds('mdi-primary:close');
    await page.screenshot({path: `${output}/mdi-frame-free.png`});
  });
  await test('External system menu restores a frame-free primary window to its original bounds', async () => {
    await menu('mdi-primary:system', 'window-menu:code:restore');
    await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Normal' && globalThis.dockingScene.mdiFrames.code.frameVisible);
    assert.deepEqual((await model('code')).mdiBounds, normalBounds);
    assert.equal((await state()).editors.code.instance, editorInstance);
    assert.deepEqual((await state()).mdiExternalButtons, externalButtons);
    await bounds('window-title:code');
  });
  await test('Frame-free maximize and theme changes preserve external and caption control identities', async () => {
    await click('mdi-primary:maximize'); await wait(() => !globalThis.dockingScene.mdiFrames.code.frameVisible);
    await menu('menubar:Theme','appmenu:Metro Dark'); await settle();
    assert.equal((await state()).editors.code.instance, editorInstance);
    assert.deepEqual((await state()).mdiExternalButtons, externalButtons);
    assert.deepEqual((await state()).mdiFrames.code.buttons, captionButtons);
    await page.screenshot({path: `${output}/mdi-frame-free-dark.png`});
    await menu('menubar:Theme','appmenu:Metro Light'); await option('show-frames');
    await bounds('window-title:code');
    await click('mdi-primary:restore'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Normal');
  });
  await test('External restore obeys layout locking without disabling permitted closing', async () => {
    await click('mdi-primary:maximize'); await option('hide-frames'); await bounds('mdi-primary:restore');
    const restore = await bounds('mdi-primary:restore');
    await menu('menubar:Window','appmenu:Lock layout'); await settle();
    assert.equal((await state()).mdiPrimaryRestoreEnabled, false); assert.equal((await state()).mdiPrimaryCloseEnabled, true);
    await page.mouse.click(restore.x + restore.width/2, restore.y + restore.height/2); await settle();
    assert.equal((await model('code')).mdiState, 'Maximized');
    await menu('menubar:Window','appmenu:Unlock layout'); await bounds('mdi-primary:restore');
    await click('mdi-primary:restore'); await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Normal');
  });
  await test('Populated minimized-caption plans are reused across document and tool activation', async () => {
    await activate('main'); await click('mdi-primary:minimize');
    await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'main').mdiState === 'Minimized');
    await activate('code'); await settle(); const before = await state();
    for (let i = 0; i < 10; i++) await activate(i % 2 ? 'code' : 'explorer');
    const after = await state();
    assert.equal(after.mdiMinimizedLayoutBuilds, before.mdiMinimizedLayoutBuilds);
    assert(after.mdiMinimizedLayoutReuses > before.mdiMinimizedLayoutReuses);
    for (const key of ['x','y','width','height']) assert.equal(after.mdiFrames.main[key], before.mdiFrames.main[key]);
    console.log('MDI_MINIMIZED_REUSE ' + JSON.stringify({builds: after.mdiMinimizedLayoutBuilds - before.mdiMinimizedLayoutBuilds, reuses: after.mdiMinimizedLayoutReuses - before.mdiMinimizedLayoutReuses}));
  });
  await test('External close after tool activation closes only the primary document', async () => {
    await activate('code'); await activate('explorer');
    assert.equal((await state()).mdiPrimaryWindowId, 'code');
    await click('mdi-primary:close');
    await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').placement === 'Hidden' && !globalThis.dockingScene.mdiFrames.code);
    assert.equal((await model('explorer')).placement, 'Docked');
    assert.notEqual((await state()).mdiPrimaryWindowId, 'code');
  });
  await test('Releasing standard MDI disables its commands and hides the external bar', async () => {
    await menu('menubar:Window','appmenu:Tabbed MDI'); await settle();
    assert.equal((await state()).mdiPrimaryWindowId, null);
    assert.equal((await state()).mdiPrimaryCloseEnabled, false); assert.equal((await state()).mdiPrimaryRestoreEnabled, false);
    assert.equal((await state()).bounds['mdi-primary:close'], undefined);
    await reset(); await settle();
  });
}
