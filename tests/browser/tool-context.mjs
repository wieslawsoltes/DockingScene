import assert from 'node:assert/strict';

export async function runToolContextTests({page,state,test,reset,click,wait,bounds,settle,menu,output}) {
  const option = id => menu('toolbar:Appearance','tool-context:'+id);
  const context = async () => (await state()).toolTitleContexts.explorer;
  const group = async () => (await state()).groups.find(g=>g.windows.includes('explorer'));
  await test('Tool title context edits metadata without dragging or replacing native caption buttons', async()=>{
    await reset(); await menu('menubar:Activate','activate:explorer'); await option('far'); await option('show');
    await bounds('mdi-context:note:explorer'); const before=await state(), g=await group();
    await click('mdi-context:note:explorer'); await page.keyboard.press('Control+a'); await page.keyboard.type('Connected');
    await wait(()=>globalThis.dockingScene.toolTitleContexts.explorer.description==='Connected');
    await click('mdi-context:track:explorer'); await wait(()=>globalThis.dockingScene.toolTitleContexts.explorer.tracked);
    const after=await state(); assert.equal(after.interactionActive,false); assert.equal(after.activeWindowId,'explorer');
    assert.equal(after.geometryBuilds,before.geometryBuilds); assert.deepEqual(after.captionButtonIdentities[g.id],before.captionButtonIdentities[g.id]);
  });
  await test('Tool contexts retain local state across tab selection, near/far alignment and themes', async()=>{
    const before=await context();
    await click('tab:outline'); await settle(); assert.equal((await state()).toolTitleContexts.explorer,undefined);
    await click('tab:explorer'); await bounds('mdi-context:note:explorer');
    assert.equal((await context()).instance,before.instance); assert((await context()).tracked);
    await option('near'); await settle();
    const g=await group(), near=await bounds('mdi-context:note:explorer'), caption=await bounds('group-title:'+g.id);
    assert(near.x < caption.x);
    await option('far'); await settle(); const far=await bounds('mdi-context:note:explorer'), title=await bounds('group-title:'+g.id);
    assert(far.x > title.x);
    const close=await bounds('group:'+g.id+':Close'); assert(far.x+far.width<=close.x);
    await menu('menubar:Theme','appmenu:Metro Dark'); await settle();
    assert.equal((await context()).instance,before.instance); assert.equal((await context()).builds,before.builds); assert((await context()).tracked);
    await page.screenshot({path:`${output}/tool-title-context-dark.png`});
    await menu('menubar:Theme','appmenu:Metro Light');
  });
  await test('Auto-hide and pin transfer the same tool title context between containers', async()=>{
    const before=await context(), g=await group();
    await click('group:'+g.id+':AutoHide'); await wait(()=>globalThis.dockingScene.windows.find(w=>w.id==='explorer').placement==='AutoHidden');
    await click('autohide:explorer'); await wait(()=>globalThis.dockingScene.autoHideWindowId==='explorer'); await bounds('mdi-context:note:explorer');
    assert.equal((await context()).instance,before.instance); assert((await context()).tracked);
    const auto=(await state()).autoHideGroups.find(g=>g.windows.includes('explorer'));
    await click('group:'+auto.id+':Pin'); await wait(()=>globalThis.dockingScene.windows.find(w=>w.id==='explorer').placement==='Docked');
    await bounds('mdi-context:note:explorer'); assert.equal((await context()).instance,before.instance);
    assert.equal((await context()).note,'Connected'); assert((await context()).tracked);
    await page.screenshot({path:`${output}/tool-title-context-light.png`});
  });
  await test('Closing retains tool context while clearing and reset release it', async()=>{
    const before=await context(), g=await group(); await click('group:'+g.id+':Close');
    await wait(()=>globalThis.dockingScene.windows.find(w=>w.id==='explorer').placement==='Hidden');
    await menu('menubar:View','appmenu:Solution Explorer'); await bounds('mdi-context:note:explorer');
    assert.equal((await context()).instance,before.instance); assert((await context()).tracked);
    await option('clear'); await settle(); assert.equal((await state()).toolTitleContexts.explorer,undefined);
    assert.equal((await state()).bounds['mdi-context:note:explorer'],undefined);
    await reset(); assert.deepEqual((await state()).toolTitleContexts,{});
  });
}
