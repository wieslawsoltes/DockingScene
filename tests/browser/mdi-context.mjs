import assert from 'node:assert/strict';

export async function runMdiContextTests({page,state,test,reset,click,wait,bounds,settle,menu,output}) {
  const option = id => menu('menubar:Window','mdi-context:'+id);
  const activate = id => menu('menubar:Activate','activate:'+id);
  const context = async () => (await state()).mdiContexts.code;
  await test('Native contextual caption controls retain their input without starting a window drag', async () => {
    await reset(); await menu('menubar:Window','appmenu:Standard MDI');
    await menu('menubar:Window','mdi-option:show-frames'); await activate('code'); await option('show');
    await bounds('mdi-context:note:code');
    const before=await state(), rect=before.windows.find(w=>w.id==='code').mdiBounds;
    await click('mdi-context:note:code'); await page.keyboard.press('Control+a'); await page.keyboard.type('Reviewed');
    await wait(()=>globalThis.dockingScene.mdiContexts.code.note==='Reviewed' && globalThis.dockingScene.mdiContexts.code.description==='Reviewed');
    await click('mdi-context:track:code'); await wait(()=>globalThis.dockingScene.mdiContexts.code.tracked);
    assert.equal((await state()).activeWindowId,'code'); assert.equal((await state()).mdiFrames.code.moving,false);
    assert.deepEqual((await state()).windows.find(w=>w.id==='code').mdiBounds,rect);
    assert.equal((await state()).editors.code.instance,before.editors.code.instance);
  });
  await test('Context template root and local control state survive activation and theme changes', async () => {
    const before=await context(), identities=(await state()).mdiFrames.code.buttons;
    for(let i=0;i<4;i++){await activate('main');await activate('code');}
    await menu('menubar:Theme','appmenu:Metro Dark'); await settle();
    assert.equal((await context()).instance,before.instance);assert.equal((await context()).builds,before.builds);assert((await context()).tracked);
    assert.deepEqual((await state()).mdiFrames.code.buttons,identities);
    await page.screenshot({path:`${output}/mdi-context-dark.png`});
    await menu('menubar:Theme','appmenu:Metro Light');
  });
  await test('Context controls survive minimize and frame-free maximize without covering system actions', async () => {
    const before=await context();
    await click('window:code:minimize');await wait(()=>globalThis.dockingScene.windows.find(w=>w.id==='code').mdiState==='Minimized');
    assert.equal((await state()).bounds['mdi-context:note:code'],undefined);
    await click('window:code:restore');await bounds('mdi-context:note:code');
    await click('window:code:maximize');await menu('menubar:Window','mdi-option:hide-frames');
    await wait(()=>!globalThis.dockingScene.mdiFrames.code.frameVisible);
    assert.equal((await state()).bounds['mdi-context:note:code'],undefined);
    await click('mdi-primary:restore');await bounds('mdi-context:note:code');
    assert.equal((await context()).instance,before.instance);assert((await context()).tracked);
    const r=await bounds('mdi-context:track:code'), close=await bounds('window:code:close');
    assert(r.x+r.width<=close.x);
    await page.screenshot({path:`${output}/mdi-context-light.png`});
  });
  await test('Template replacement and UIElement context keep the editor single-owned', async () => {
    const editor=(await state()).editors.code.instance, before=await context();
    await option('visual-data');await wait(()=>globalThis.dockingScene.mdiContexts.code.visualDataSuppressed);
    assert.equal((await state()).editors.code.instance,editor);assert.equal((await context()).instance,before.instance);
    await option('show');await option('status-template');await bounds('mdi-context:status:code');
    assert.equal((await context()).builds,before.builds+1);
    await option('clear');await settle();assert.equal((await context()).instance,0);
    await option('show');await bounds('mdi-context:note:code');assert.equal((await context()).builds,before.builds+2);
    assert.equal((await state()).editors.code.instance,editor);
    await reset();assert.deepEqual((await state()).mdiContexts,{});
  });
  await test('Standalone host arrangement leaves a second host sharing its manager untouched', async () => {
    await option('scope-sample');await bounds('scope:left');await wait(()=>!!globalThis.dockingScene.scopedMdi);
    const before=(await state()).scopedMdi;
    await click('scope:left');await wait(rev=>globalThis.dockingScene.scopedMdi.revision===rev+1,before.revision);
    const left=(await state()).scopedMdi;
    assert.deepEqual(left.windows.c,before.windows.c);assert.deepEqual(left.windows.d,before.windows.d);
    await click('scope:right');await wait(rev=>globalThis.dockingScene.scopedMdi.revision===rev+1,left.revision);
    const right=(await state()).scopedMdi;
    assert.deepEqual(right.windows.a,left.windows.a);assert.deepEqual(right.windows.b,left.windows.b);
    await click('scope:right');await settle();assert.equal((await state()).scopedMdi.revision,right.revision);
    await page.screenshot({path:`${output}/mdi-independent-hosts.png`});
  });
  await test('Context callbacks cannot attach an obsolete template root or reclaim a released host', async () => {
    await click('scope:reentrant-context');
    await wait(()=>globalThis.dockingScene.scopedMdi.contextProbe==='Replacement context');
    assert.equal((await state()).scopedMdi.obsoleteContextLoads,0);
    const before=(await state()).scopedMdi;
    await click('scope:reentrant-release');
    await wait(()=>globalThis.dockingScene.scopedMdi.releasedDocumentDetached);
    assert.equal((await state()).scopedMdi.obsoleteContextLoads,0);
    assert.equal((await state()).scopedMdi.revision,before.revision);
  });
  await test('Released host commands reject without mutating either document group', async () => {
    const before=(await state()).scopedMdi;
    await click('scope:release');await click('scope:left');await settle();
    const after=(await state()).scopedMdi;
    assert.equal(after.success,false);assert.equal(after.revision,before.revision);assert.deepEqual(after.windows,before.windows);
    await reset();
  });
}
