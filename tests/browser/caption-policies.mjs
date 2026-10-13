import assert from 'node:assert/strict';

export async function runCaptionPolicyTests({page,state,test,reset,click,wait,bounds,settle,menu,output}) {
  const current=async()=>(await state()).toolCaptions;
  const policy=id=>menu('caption-sample:policies','caption-policy:'+id);
  const option=id=>menu('caption-sample:options','caption-option:'+id);
  const group=async(id='cap-a')=>(await current()).windows[id].group;
  async function prepare(){
    await page.keyboard.press('Escape');
    if(!(await state()).toolCaptions){await reset();await menu('menubar:Window','mdi-context:tool-caption-sample');await bounds('caption-sample:policies');}
    const s=await current();await click('caption-sample:reset');await wait(g=>globalThis.dockingScene.toolCaptions.generation>g,s.generation);await settle();
  }
  async function missing(id){await settle();assert.equal((await state()).bounds[id],undefined,id+' remains hit-testable');}
  function retained(a,b,g){assert.deepEqual(a.buttons[g],b.buttons[g]);assert.equal(a.windows['cap-a'].editor,b.windows['cap-a'].editor);}

  await test('Instance caption buttons hide only their own window and reclaim native slots',async()=>{
    await prepare();const before=await current(),g=await group(),other=await group('cap-b');
    await policy('buttons-hide');const after=await current();
    for(const name of ['Close','Options','AutoHide']){
      await missing('group:'+g+':'+name);await bounds('group:'+other+':'+name);
      assert.deepEqual(after.policies.actions['group:'+g+':'+name],{enabled:true,tabStop:false,visible:false});
    }
    assert.equal(after.captionWidths[g]-before.captionWidths[g],69);assert.equal(after.captionWidths[other],before.captionWidths[other]);
    assert.equal(after.revision,before.revision+1);assert.equal(after.geometryBuilds,before.geometryBuilds);retained(before,after,g);
    await page.screenshot({path:`${output}/caption-instance-hidden.png`});
  });
  await test('Explicit instance buttons override disabled site defaults without changing sibling policy',async()=>{
    await prepare();for(const id of ['close','options','pin'])await option(id);
    const before=await current(),g=await group(),other=await group('cap-b');await policy('buttons-show');
    for(const name of ['Close','Options','AutoHide']){await bounds('group:'+g+':'+name);await missing('group:'+other+':'+name);}
    const after=await current();assert.equal(after.captionWidths[g],before.captionWidths[g]-69);retained(before,after,g);
    assert.equal(after.geometryBuilds,before.geometryBuilds);assert.equal(after.close,false);assert.equal(after.options,false);assert.equal(after.pin,false);
    await page.screenshot({path:`${output}/caption-instance-override.png`});
  });
  await test('Each close options and pin override independently resets to inheritance',async()=>{
    await prepare();const before=await current(),g=await group();
    for(const [id,name] of [['close','Close'],['options','Options'],['pin','AutoHide']]){
      await policy(id+'-hide');await missing('group:'+g+':'+name);
      assert.equal((await current()).captionWidths[g],before.captionWidths[g]+23);
      await policy(id+'-inherit');await bounds('group:'+g+':'+name);
      assert.equal((await current()).policies.windows['cap-a'][id],null);
    }
    const after=await current();retained(before,after,g);assert.equal(after.captionWidths[g],before.captionWidths[g]);assert.equal(after.geometryBuilds,before.geometryBuilds);
  });
  await test('Selected tools resolve their own overrides in one retained tab group',async()=>{
    await prepare();await policy('buttons-hide');await policy('join');const g=await group();
    assert.equal(g,await group('cap-b'));await bounds('tab:cap-a');
    await click('tab:cap-a');await missing('group:'+g+':Close');const before=await current();
    await click('tab:cap-b');await bounds('group:'+g+':Close');const selected=await current();
    assert.equal(selected.captionWidths[g],before.captionWidths[g]-69);assert.deepEqual(selected.buttons[g],before.buttons[g]);
    await click('tab:cap-a');await missing('group:'+g+':Close');const after=await current();
    retained(before,after,g);assert.equal(after.geometryBuilds,before.geometryBuilds);assert.equal(after.policies.windows['cap-b'].close,null);
  });
  await test('Hiding one title reclaims its physical row and preserves the editable content',async()=>{
    await prepare();await click('caption-editor:cap-a');await page.keyboard.press('Control+a');await page.keyboard.type('retained title policy text');
    await wait(()=>globalThis.dockingScene.toolCaptions.policies.windows['cap-a'].text==='retained title policy text');
    const before=await current(),g=await group(),r=await bounds('caption-editor:cap-a');
    await policy('title-hide');await missing('group-title:'+g);const after=await current(),next=await bounds('caption-editor:cap-a');
    assert(Math.abs(next.height-r.height-before.policies.titleHeight)<=1,'Hidden title leaves unused pixels');
    assert(Math.abs(next.y-r.y+before.policies.titleHeight)<=1);retained(before,after,g);
    assert.equal(after.policies.windows['cap-a'].text,'retained title policy text');assert.equal(after.hosts[before.toolHost].frame,before.hosts[before.toolHost].frame);
    await policy('title-inherit');await bounds('group-title:'+g);const restored=await bounds('caption-editor:cap-a');assert(Math.abs(restored.height-r.height)<=1);
  });
  await test('Global title defaults are inherited while an explicit instance keeps its row',async()=>{
    await prepare();await policy('title-show');const before=await current(),g=await group(),other=await group('cap-b');
    await policy('site-title');await bounds('group-title:'+g);await missing('group-title:'+other);const after=await current();
    assert.equal(after.policies.siteTitle,false);assert.equal(after.revision,before.revision);assert(after.geometryBuilds>before.geometryBuilds);retained(before,after,g);
    await policy('title-inherit');await missing('group-title:'+g);await policy('site-title');await bounds('group-title:'+g);await bounds('group-title:'+other);
    assert.equal((await current()).policies.windows['cap-a'].title,null);
  });
  await test('Auto-hide popup honors explicit pin presentation over hidden site defaults',async()=>{
    await prepare();for(const id of ['close','options','pin'])await option(id);await policy('buttons-show');const before=await current();
    await option('autohide');await click('autohide:cap-a');const g=await group();await bounds('group:'+g+':Pin');
    await click('group:'+g+':Pin');await wait(()=>globalThis.dockingScene.toolCaptions.windows['cap-a'].placement==='Docked');
    const after=await current();assert.equal(after.windows['cap-a'].editor,before.windows['cap-a'].editor);assert.equal(after.pin,false);assert.equal(after.error,null);
  });
  await test('Explicit visible actions remain disabled when close and pin capabilities are denied',async()=>{
    await prepare();await policy('buttons-show');await policy('capabilities');const before=await current(),g=await group();
    for(const name of ['Close','AutoHide']){
      const action=before.policies.actions['group:'+g+':'+name];assert.deepEqual(action,{enabled:false,tabStop:false,visible:true});
      const r=await bounds('group:'+g+':'+name);await page.mouse.click(r.x+r.width/2,r.y+r.height/2);await settle();
    }
    const after=await current();assert.equal(after.revision,before.revision);assert.deepEqual(after.hosts,before.hosts);assert.equal(after.interaction,false);
    await policy('capabilities');await bounds('group:'+g+':Close');await click('group:'+g+':Close');
    await wait(()=>globalThis.dockingScene.toolCaptions.windows['cap-a'].placement==='Hidden');
  });
  await test('Policy save and restore retains null distinctions native content and undo',async()=>{
    await prepare();await policy('close-hide');await policy('options-show');await policy('save');const before=await current(),g=await group();assert(before.policies.saved);
    await policy('buttons-show');await policy('title-hide');await policy('restore');await bounds('group-title:'+g);await missing('group:'+g+':Close');
    const after=await current();assert.deepEqual(after.policies.windows['cap-a'],before.policies.windows['cap-a']);retained(before,after,g);
    assert.equal(after.hosts[before.toolHost].frame,before.hosts[before.toolHost].frame);assert.equal(after.error,null);
    await click('caption-sample:undo');await missing('group-title:'+g);
  });
  await test('Native caption capture supports Escape and a subsequent complete double-click',async()=>{
    await prepare();const before=await current(),g=await group(),r=await bounds('group-title:'+g);
    await page.mouse.move(r.x+20,r.y+r.height/2);await page.mouse.down();
    try{
      await wait(()=>globalThis.dockingScene.toolCaptions.policies.captures===1);assert((await current()).policies.keyboardCapture);
      await page.mouse.move(r.x+90,r.y+60,{steps:8});await page.keyboard.press('Escape');
      await wait(()=>!globalThis.dockingScene.toolCaptions.interaction&&globalThis.dockingScene.toolCaptions.policies.captures===0);
    }finally{await page.mouse.up();}
    const cancelled=await current();assert.equal(cancelled.revision,before.revision);assert.equal(cancelled.policies.keyboardCapture,false);
    await click('group-title:'+g,{clickCount:2,delay:80});await wait(id=>globalThis.dockingScene.toolCaptions.hosts[id].maximized,before.toolHost);
    const after=await current();assert.equal(after.revision,before.revision+1);assert.equal(after.policies.captures,0);assert.equal(after.policies.keyboardCapture,false);
    retained(before,after,g);
  });
  await reset();
}
