import assert from 'node:assert/strict';

export async function runToolTabVisibilityTests({page,state,test,reset,click,wait,bounds,settle,menu,output}) {
  const current=async()=>(await state()).toolCaptions;
  const group=s=>s.policies.geometry.groups[s.windows['cap-a'].group];
  const near=(a,b,label)=>assert(Math.abs(a-b)<=1,`${label}: ${a} != ${b}`);
  async function layout(id) {
    await click('caption-sample:policies'); await settle();
    await click('caption-policy:layout'); await settle();
    await click('caption-layout:'+id); await settle();
  }
  async function prepare(axis) {
    await reset(); await menu('menubar:Window','mdi-context:tool-caption-sample');
    await bounds('caption-sample:policies'); await layout(axis);
    await bounds('caption-editor:cap-a'); await settle();
    assert.equal((await current()).error,null);
  }
  function retained(a,b) {
    assert.equal(b.windows['cap-a'].editor,a.windows['cap-a'].editor);
    assert.equal(group(b).identity,group(a).identity); assert.equal(group(b).tabs,group(a).tabs);
    assert.equal(b.hosts[a.toolHost].frame,a.hosts[a.toolHost].frame); assert.equal(b.error,null);
  }
  await test('Hiding a vertical single-tool strip reclaims its constraint and retains the editor',async()=>{
    await prepare('horizontal'); await layout('Left');
    const shown=await current(),before=await bounds('caption-editor:cap-a'); near(group(shown).width,277,'Shown width');
    await layout('hide-single'); const hidden=await current(),after=await bounds('caption-editor:cap-a');
    assert(!group(hidden).stripVisible); near(group(hidden).width,122,'Hidden width'); near(after.width,before.width,'Editor minimum');
    assert.equal(hidden.revision,shown.revision); retained(shown,hidden);
    await page.screenshot({path:`${output}/single-tool-strip-hidden.png`});
    await layout('Top'); const top=await current(); assert.equal(top.geometryBuilds,hidden.geometryBuilds); retained(hidden,top);
    await layout('Right'); const right=await current(); assert.equal(right.geometryBuilds,hidden.geometryBuilds); retained(hidden,right);
    await layout('show-single'); const restored=await current(); near(group(restored).width,277,'Restored strip');
    assert(group(restored).stripVisible); retained(shown,restored);
  });
  await test('Hidden horizontal single-tool strip returns only the strip height, not content minimums',async()=>{
    await prepare('vertical'); const shown=await current(),before=await bounds('caption-editor:cap-a');
    await layout('hide-single'); const hidden=await current(),after=await bounds('caption-editor:cap-a');
    near(group(shown).height-group(hidden).height,shown.policies.geometry.tabHeight,'Strip height');
    near(before.height,after.height,'Content minimum'); assert(after.height>=79); retained(shown,hidden);
    await menu('caption-sample:policies','caption-policy:title-hide'); const frameless=await current();
    near(group(hidden).height-group(frameless).height,hidden.policies.titleHeight,'Independent title height');
    assert((await bounds('caption-editor:cap-a')).height>=79); retained(hidden,frameless);
  });
  await test('Adding a second tool restores a real strip and activation reuses retained metrics',async()=>{
    await prepare('horizontal'); await layout('Left'); await layout('hide-single');
    await menu('caption-sample:policies','caption-policy:join');
    const joined=await current(); assert(group(joined).stripVisible);
    assert.equal(joined.windows['cap-b'].group,joined.windows['cap-a'].group);
    await bounds('tab:cap-a'); await bounds('tab:cap-b');
    for(let i=0;i<6;i++){await click('caption-sample:'+(i%2?'a':'b'));await settle();}
    const after=await current(); assert.equal(after.policies.geometry.metricsBuilds,joined.policies.geometry.metricsBuilds);
    assert.equal(after.geometryBuilds,joined.geometryBuilds); retained(joined,after);
    await click('caption-sample:theme'); await settle(); const dark=await current();
    assert.equal(dark.policies.geometry.metricsBuilds,after.policies.geometry.metricsBuilds); retained(after,dark);
  });
  await test('Changing single-tool visibility cancels captured live resizing without a stale commit',async()=>{
    await prepare('horizontal'); await layout('Left'); const before=await current(),frame=before.policies.geometry;
    const x=frame.frameX+frame.frameWidth-.5,y=frame.frameY+70;
    await page.mouse.move(x,y); await page.mouse.down();
    try {
      await wait(()=>globalThis.dockingScene.toolCaptions.policies.geometry.moving);
      await page.mouse.move(x+75,y,{steps:10}); await settle();
      assert.equal((await current()).policies.geometry.plans,1);
      await page.keyboard.press('F4');
      await wait(()=>globalThis.dockingScene.toolCaptions.policies.geometry.singleTabs==='Hide' && !globalThis.dockingScene.toolCaptions.policies.geometry.moving);
    } finally {await page.mouse.up();}
    await settle(); const after=await current();
    assert.equal(after.revision,before.revision); assert.equal(after.policies.geometry.plans,0);
    near(after.policies.geometry.frameWidth,before.policies.geometry.frameWidth,'Frame restored');
    near(group(after).width,122,'New visibility projects after cancellation'); retained(before,after);
  });
  await reset();
}
