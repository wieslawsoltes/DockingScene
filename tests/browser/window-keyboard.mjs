import assert from 'node:assert/strict';

export async function runWindowKeyboardTests({page,state,test,reset,click,wait,bounds,settle,menu,output}) {
  const model = async id => (await state()).windows.find(w => w.id === id);
  const command = (id,name) => menu('window:'+id+':system','window-menu:'+id+':'+name);
  const activate = id => menu('menubar:Activate','activate:'+id);
  await test('System Move previews arrows and commits exactly once on Enter release', async () => {
    await reset(); await menu('menubar:Window','appmenu:Standard MDI');
    // Presentation options intentionally outlive a layout reset. This suite
    // exercises framed commands, independently of the frame-free suite before it.
    await menu('menubar:Window','mdi-option:show-frames'); await activate('code');
    const before = await state(), rect = (await model('code')).mdiBounds;
    await command('code','move'); await wait(() => globalThis.dockingScene.mdiFrames.code.keyboardMoving);
    for (const key of ['ArrowRight','ArrowRight','ArrowRight','ArrowDown','ArrowDown']) await page.keyboard.press(key);
    await wait(r => globalThis.dockingScene.mdiFrames.code.x === r.x+24 && globalThis.dockingScene.mdiFrames.code.y === r.y+16,rect);
    assert.deepEqual((await model('code')).mdiBounds,rect);
    await page.keyboard.down('Enter');
    try { await settle(); assert.equal((await state()).mdiFrames.code.keyboardMoving,true); assert.deepEqual((await model('code')).mdiBounds,rect); }
    finally { await page.keyboard.up('Enter'); }
    await wait(() => !globalThis.dockingScene.mdiFrames.code.keyboardMoving);
    const after = await state(); assert.equal(after.revision,before.revision+1);
    assert.deepEqual((await model('code')).mdiBounds,{...rect,x:rect.x+24,y:rect.y+16});
    assert.equal(after.editors.code.text,before.editors.code.text);
  });
  await test('System Move Escape and unchanged Enter preserve model history', async () => {
    const before = await state(), rect = (await model('code')).mdiBounds;
    await command('code','move'); await page.keyboard.press('ArrowLeft'); await page.keyboard.press('Escape');
    await wait(() => !globalThis.dockingScene.mdiFrames.code.keyboardMoving);
    assert.deepEqual((await model('code')).mdiBounds,rect); assert.equal((await state()).revision,before.revision);
    await command('code','move'); await page.keyboard.press('Enter'); await settle();
    assert.equal((await state()).revision,before.revision);
  });
  await test('System Size selects anchored edges and commits their final geometry', async () => {
    const before = await state(), rect = (await model('code')).mdiBounds;
    await command('code','size');
    for(const key of ['ArrowLeft','ArrowRight','ArrowRight','ArrowUp','ArrowDown']) await page.keyboard.press(key);
    await wait(r => globalThis.dockingScene.mdiFrames.code.width === r.width-16 && globalThis.dockingScene.mdiFrames.code.height === r.height-8,rect);
    assert.deepEqual((await model('code')).mdiBounds,rect);
    await page.keyboard.press('Enter'); await wait(() => !globalThis.dockingScene.mdiFrames.code.keyboardMoving);
    const after=(await model('code')).mdiBounds;
    assert.deepEqual(after,{x:rect.x+16,y:rect.y+8,width:rect.width-16,height:rect.height-8});
    assert.equal((await state()).revision,before.revision+1);
    await page.screenshot({path:`${output}/keyboard-sized-mdi.png`});
  });
  await test('Pointer command changes cancel keyboard geometry instead of committing stale bounds', async () => {
    const rect=(await model('code')).mdiBounds;
    await command('code','move'); await page.keyboard.press('ArrowRight');
    await menu('menubar:Window','appmenu:Lock layout'); await settle();
    assert.equal((await state()).mdiFrames.code.keyboardMoving,false);assert.deepEqual((await model('code')).mdiBounds,rect);
    await menu('menubar:Window','appmenu:Unlock layout');
  });
  await test('Maximized frames reject Move and Size system actions', async () => {
    await activate('code'); await click('window:code:maximize'); await wait(() => globalThis.dockingScene.windows.find(w=>w.id==='code').mdiState==='Maximized');
    for(const name of ['move','size']) {
      await click('window:code:system'); await settle(); await click('window-menu:code:'+name); await settle();
      assert.equal((await state()).mdiFrames.code.keyboardMoving,false);await page.keyboard.press('Escape');
    }
    await click('window:code:restore'); await wait(() => globalThis.dockingScene.windows.find(w=>w.id==='code').mdiState==='Normal');
  });
  await test('Reset removes keyboard scopes and preserves subsequent editor keyboard input', async () => {
    await command('code','move'); await page.keyboard.press('ArrowLeft');
    await reset(); await click('tab:code'); await page.keyboard.press('Control+a');
    await page.keyboard.type('keyboard');await page.keyboard.press('Enter');await page.keyboard.type('released');
    await wait(() => globalThis.dockingScene.editors.code?.text.replace(/\r\n?|\n/g,'\n')==='keyboard\nreleased');
    assert.deepEqual((await state()).mdiFrames,{});
  });
  await test('Floating frames also commit keyboard movement without resizing their documents', async () => {
    await reset();await click('tab:code'); await click('tab:code',{button:'right'});await settle();await click('menu:Float');
    await wait(() => globalThis.dockingScene.floatingHosts.length===1);
    const before=await state(), host=before.floatingHosts[0];
    await command(host.id,'move');await page.keyboard.press('ArrowRight');await page.keyboard.press('ArrowDown');await page.keyboard.press('Enter');
    await wait(h => { const r=globalThis.dockingScene.floatingHosts.find(f=>f.id===h.id)?.bounds;return r?.x===h.bounds.x+8&&r?.y===h.bounds.y+8; },host);
    const after=(await state()).floatingHosts[0].bounds;
    assert.equal(after.width,host.bounds.width);assert.equal(after.height,host.bounds.height);
    assert.equal((await state()).editors.code.instance,before.editors.code.instance);
    await click('toolbar:Undo');await wait(h => globalThis.dockingScene.floatingHosts[0].bounds.x===h.bounds.x,host);
    await reset();
  });
}
