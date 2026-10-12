import assert from 'node:assert/strict';

export async function runMdiPolicyTests({ page, state, test, reset, click, wait, bounds, settle, menu, output }) {
  const option = id => menu('menubar:Window', 'mdi-option:' + id);
  const activate = id => menu('menubar:Activate', 'activate:' + id);
  const model = async id => (await state()).windows.find(w => w.id === id);
  await test('Individual MDI actions hide only the target document buttons without reallocating chrome', async () => {
    await reset(); await menu('menubar:Window', 'appmenu:Standard MDI');
    await option('show-frames'); await activate('code'); await settle();
    const before = await state();
    await option('document-maximize'); await option('document-minimize');
    await wait(() => !globalThis.dockingScene.mdiPrimaryMaximizeEnabled && !globalThis.dockingScene.mdiPrimaryMinimizeEnabled);
    const after = await state();
    assert.equal(after.bounds['window:code:maximize'], undefined);
    assert.equal(after.bounds['window:code:minimize'], undefined);
    assert.deepEqual(after.mdiFrames.code.buttons, before.mdiFrames.code.buttons);
    assert.equal(after.editors.code.instance, before.editors.code.instance);
    assert.equal(after.geometryBuilds, before.geometryBuilds);
    assert.equal(after.mdiMinimizedLayoutBuilds, before.mdiMinimizedLayoutBuilds);
    await click('window-title:code', { clickCount: 2 }); await settle();
    assert.equal((await model('code')).mdiState, 'Normal');
    await activate('main'); await bounds('window:main:maximize'); await bounds('window:main:minimize');
    assert((await state()).mdiPrimaryMaximizeEnabled && (await state()).mdiPrimaryMinimizeEnabled);
    await activate('code');
    await page.screenshot({ path: `${output}/individual-mdi-policy.png` });
  });
  await test('A programmatically maximized policy-restricted document can still restore exactly', async () => {
    const rect = (await model('code')).mdiBounds, instance = (await state()).editors.code.instance;
    await option('maximize-active');
    await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Maximized');
    assert((await state()).mdiPrimaryRestoreEnabled);
    await click('window:code:restore');
    await wait(() => globalThis.dockingScene.windows.find(w => w.id === 'code').mdiState === 'Normal');
    assert.deepEqual((await model('code')).mdiBounds, rect);
    assert.equal((await state()).editors.code.instance, instance);
    assert.equal((await state()).bounds['window:code:maximize'], undefined);
  });
  await test('Individual MDI policy changes respect host restrictions and reuse caption controls', async () => {
    const identities = (await state()).mdiFrames.code.buttons;
    await option('toggle-maximize'); // Disable host-wide maximize.
    await option('document-maximize'); // Re-enable only this document.
    assert.equal((await state()).mdiPrimaryMaximizeEnabled, false);
    assert.equal((await state()).bounds['window:code:maximize'], undefined);
    await option('toggle-maximize'); await bounds('window:code:maximize');
    assert((await state()).mdiPrimaryMaximizeEnabled);
    await option('document-minimize'); await bounds('window:code:minimize');
    assert((await state()).mdiPrimaryMinimizeEnabled);
    assert.deepEqual((await state()).mdiFrames.code.buttons, identities);
    await reset();
  });
}
