import assert from 'node:assert/strict';
import { pngPixels } from './painted-tabs.mjs';

export async function runToolCaptionTests({page,state,test,reset,click,wait,bounds,settle,menu,output}) {
  const current=async()=>(await state()).toolCaptions;
  const option=id=>menu('caption-sample:options','caption-option:'+id);
  async function prepare(){const s=await current();await click('caption-sample:reset');await wait(g=>globalThis.dockingScene.toolCaptions.generation>g,s.generation);await settle();}
  const group=async(id='cap-a')=>(await current()).windows[id].group;
  const doubleTitle=async(id='cap-a')=>{await click('group-title:'+await group(id),{clickCount:2,delay:80});await settle();};
  await test('Floating tool caption double-click defaults to complete-host maximize and restore',async()=>{
    await reset();await menu('menubar:Window','mdi-context:tool-caption-sample');await bounds('caption-sample:options');await settle();
    const before=await current(),g=await group(),host=before.hosts[before.toolHost];
    assert.equal(before.mode,'ToggleMaximized');
    await doubleTitle();await wait(id=>globalThis.dockingScene.toolCaptions.hosts[id].maximized,before.toolHost);
    const max=await current();assert.equal(max.revision,before.revision+1);assert.equal(max.hosts[before.toolHost].root,host.root);
    assert.equal(max.hosts[before.toolHost].width,host.width);assert.equal(max.hosts[before.toolHost].height,host.height);
    assert.deepEqual(max.hosts[before.documentHost],before.hosts[before.documentHost]);
    await page.screenshot({path:`${output}/tool-caption-maximized.png`});
    await doubleTitle();await wait(id=>!globalThis.dockingScene.toolCaptions.hosts[id].maximized,before.toolHost);
    const after=await current();assert.equal(after.revision,before.revision+2);assert.equal(after.interaction,false);
    assert.equal(after.hosts[before.toolHost].root,host.root);assert.equal(after.hosts[before.toolHost].frame,host.frame);
    assert.deepEqual(after.buttons[g],before.buttons[g]);assert.equal(after.windows['cap-a'].editor,before.windows['cap-a'].editor);
  });
  await test('Configured Dock double-click affects the clicked tool group and remains undoable',async()=>{
    await prepare();await option('dock');const before=await current();await doubleTitle();
    await wait(()=>globalThis.dockingScene.toolCaptions.windows['cap-a'].placement==='Docked');
    const after=await current();assert.equal(after.revision,before.revision+1);
    assert.equal(after.windows['cap-b'].host,before.toolHost);assert.equal(after.hosts[before.toolHost].width,before.hosts[before.toolHost].width);
    assert.equal(after.windows['cap-a'].editor,before.windows['cap-a'].editor);
    await click('caption-sample:undo');await wait(id=>globalThis.dockingScene.toolCaptions.windows['cap-a'].host===id,before.toolHost);
    assert.equal((await current()).hosts[before.toolHost].root,before.hosts[before.toolHost].root);
  });
  await test('A sibling resize restriction rejects caption maximization without changing layout',async()=>{
    await prepare();await option('resize');const before=await current();await doubleTitle();const after=await current();
    assert.equal(after.revision,before.revision);assert.equal(after.hosts[before.toolHost].maximized,false);assert(after.error);
    assert.deepEqual(after.hosts,before.hosts);assert.equal(after.interaction,false);
    await option('resize');await doubleTitle();await wait(id=>globalThis.dockingScene.toolCaptions.hosts[id].maximized,before.toolHost);
  });
  await test('Tool title action toggles reclaim every hidden slot without recreating buttons or editors',async()=>{
    await prepare();const before=await current(),g=await group();
    for(const [id,name] of [['close','Close'],['pin','AutoHide'],['options','Options']]){
      await option(id);await settle();assert.equal((await state()).bounds['group:'+g+':'+name],undefined);
    }
    const hidden=await current();assert.equal(hidden.revision,before.revision);assert.equal(hidden.geometryBuilds,before.geometryBuilds);
    assert.equal(hidden.captionWidths[g]-before.captionWidths[g],69);
    assert.deepEqual(hidden.buttons[g],before.buttons[g]);assert.equal(hidden.windows['cap-a'].editor,before.windows['cap-a'].editor);
    await page.screenshot({path:`${output}/tool-caption-hidden-actions.png`});
    for(const id of ['options','pin','close'])await option(id);
    await bounds('group:'+g+':Close');await settle();const restored=await current();
    assert.deepEqual(restored.buttons[g],before.buttons[g]);assert.equal(restored.captionWidths[g],before.captionWidths[g]);
    await click('caption-sample:theme');await settle();assert.deepEqual((await current()).buttons[g],before.buttons[g]);
    await page.screenshot({path:`${output}/tool-caption-dark.png`});await click('caption-sample:theme');
  });
  await test('Hiding caption buttons does not bypass permissions or remove alternative close commands',async()=>{
    await prepare();const g=await group();await option('close');await option('options');const before=await current();
    await click('group-title:'+g,{button:'right'});await bounds('menu:Close');await click('menu:Close');
    await wait(()=>globalThis.dockingScene.toolCaptions.windows['cap-a'].placement==='Hidden');
    const after=await current();assert.equal(after.revision,before.revision+1);assert.equal(after.windows['cap-b'].placement,'Floating');
    assert.equal(after.windows['cap-doc1'].placement,'Floating');
    await click('caption-sample:undo');await wait(()=>globalThis.dockingScene.toolCaptions.windows['cap-a'].placement==='Floating');
    assert.equal((await current()).windows['cap-a'].editor,before.windows['cap-a'].editor);
  });
  await test('Auto-hide popup tool captions use the same visibility settings and retain content',async()=>{
    await prepare();const before=await current();await option('autohide');await option('close');await option('options');await option('pin');
    await click('autohide:cap-a');await bounds('group-title:'+await group());const g=await group();
    for(const action of ['Close','Options','Pin'])assert.equal((await state()).bounds['group:'+g+':'+action],undefined);
    assert.equal((await current()).windows['cap-a'].editor,before.windows['cap-a'].editor);
    // Restoring pin presentation must expose a real working native pin action.
    await option('pin');await click('autohide:cap-a');await bounds('group:'+g+':Pin');await click('group:'+g+':Pin');
    await wait(()=>globalThis.dockingScene.toolCaptions.windows['cap-a'].placement==='Docked');
    assert.equal((await current()).windows['cap-a'].editor,before.windows['cap-a'].editor);
  });
  await test('Floating application titles follow the primary document, retain metadata updates and custom delimiters',async()=>{
    await prepare();const before=await current(),docHost=before.documentHost;
    assert.equal(before.hosts[docHost].title,'Document1.cs - DockingScene');
    await click('caption-sample:doc2');await wait(id=>globalThis.dockingScene.toolCaptions.hosts[id].title==='Document2.cs - DockingScene',docHost);
    await click('caption-sample:b');await settle();assert.equal((await current()).hosts[docHost].title,'Document2.cs - DockingScene');
    assert.equal((await current()).hosts[before.toolHost].title,'Properties');
    await click('caption-sample:doc1');await option('rename');await option('delimiter');
    await wait(id=>globalThis.dockingScene.toolCaptions.hosts[id].title==='Renamed.cs | DockingScene',docHost);
    await option('mixed');await click('caption-sample:a');await settle();
    assert.equal((await current()).windows['cap-a'].host,docHost);assert.equal((await current()).hosts[docHost].title,'Renamed.cs | DockingScene');
    await option('no-app');await wait(id=>globalThis.dockingScene.toolCaptions.hosts[id].title==='Renamed.cs',docHost);
    await page.screenshot({path:`${output}/floating-primary-document-title.png`});
  });
  await test('Unchanged title and appearance projections reuse formatted captions and retain painted actions',async()=>{
    await prepare();const before=await current(),g=await group(),docHost=before.documentHost;
    for(let i=0;i<4;i++){await option('close');await option('close');}
    await settle();const after=await current();assert.equal(after.titleFormats,before.titleFormats);assert.equal(after.geometryBuilds,before.geometryBuilds);
    assert.deepEqual(after.buttons[g],before.buttons[g]);assert.equal(after.hosts[docHost].frame,before.hosts[docHost].frame);
    const r=await bounds('group:'+g+':Close'),png=pngPixels(await page.screenshot()),scale=png.width/page.viewportSize().width;
    // The native caption action keeps its active tool background after toggles.
    const rgb=png.rgb(Math.round((r.x+2)*scale),Math.round((r.y+2)*scale));
    assert(rgb.every((v,i)=>Math.abs(v-[0,122,204][i])<=3),'Retained caption button paint: '+rgb);
    assert.equal(after.error,null);await reset();
  });
}
