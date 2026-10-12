import assert from 'node:assert/strict';

export async function runFloatingDragTests({page,state,test,reset,click,wait,bounds,settle,menu,output}) {
  const snapshot=async()=>(await state()).floatingReview;
  async function prepare() {
    const before=await snapshot();await click('float-review:reset');
    await wait(g=>globalThis.dockingScene.floatingReview.generation>g,before.generation);await settle();
  }
  async function begin() {
    const s=await snapshot(), r=await bounds('window-title:'+s.sourceId);
    const p={x:r.x+Math.min(15,r.width/2),y:r.y+r.height/2};
    await page.mouse.move(p.x,p.y);await page.mouse.down();
    await wait(()=>globalThis.dockingScene.floatingReview.interaction);return p;
  }
  async function move(p,dx,dy=0) {await page.mouse.move(p.x+dx,p.y+dy,{steps:12});await settle();}
  await test('Floating whole-host movement preserves split structure, dimensions and content with Float disabled',async()=>{
    await reset();await menu('menubar:Window','mdi-context:floating-sample');await bounds('float-review:reset');await settle();
    const before=await snapshot(), source=before.hosts[before.sourceId], p=await begin();
    try {
      await move(p,46);const preview=await snapshot();
      assert.equal(preview.raw.x,66);assert.equal(preview.preview.x,69);assert.equal(preview.previewTarget,null);
      assert.equal(preview.preview.width,source.bounds.width);assert.equal(preview.preview.height,source.bounds.height);
      assert.deepEqual(preview.hosts,before.hosts);assert.equal(preview.revision,before.revision);
    }finally{await page.mouse.up();}
    await wait(r=>globalThis.dockingScene.floatingReview.revision===r+1,before.revision);
    const after=await snapshot(), host=after.hosts[before.sourceId];
    assert.equal(host.bounds.x,69);assert.equal(host.rootIdentity,source.rootIdentity);assert.deepEqual(host.nodes,source.nodes);
    assert.equal(host.frameIdentity,source.frameIdentity);assert.deepEqual(host.buttons,source.buttons);assert.deepEqual(after.editors,before.editors);
    assert.equal(after.error,null);await page.screenshot({path:`${output}/floating-preserved-split.png`});
    await click('float-review:undo');await wait(id=>globalThis.dockingScene.floatingReview.hosts[id].bounds.x===20,before.sourceId);
  });
  await test('Floating Ctrl bypass and zero-distance disable use raw host coordinates',async()=>{
    await prepare();await page.keyboard.down('Control');
    try{const p=await begin();try{await move(p,46);}finally{await page.mouse.up();}}finally{await page.keyboard.up('Control');}
    await wait(()=>{const s=globalThis.dockingScene.floatingReview;return s.hosts[s.sourceId].bounds.x===66;});
    await prepare();await click('float-review:off');const p=await begin();try{await move(p,46);}finally{await page.mouse.up();}
    await wait(()=>{const s=globalThis.dockingScene.floatingReview;return s.hosts[s.sourceId].bounds.x===66;});
  });
  await test('Floating windows honor the configured magnetic gap',async()=>{
    await prepare();await click('float-review:gap8');const p=await begin();try{await move(p,45);}finally{await page.mouse.up();}
    await wait(()=>{const s=globalThis.dockingScene.floatingReview;return s.hosts[s.sourceId].bounds.x===62;});
    const s=await snapshot(), a=s.hosts[s.sourceId].bounds,b=s.hosts[s.neighborId].bounds;
    assert.equal(b.x-a.x-a.width,8);await page.screenshot({path:`${output}/floating-gap8.png`});
  });
  await test('Floating border resizing snaps without changing the opposite edge or split identity',async()=>{
    await prepare();const before=await snapshot(), source=before.hosts[before.sourceId], site=await bounds('float-review:site');
    const p={x:site.x+source.frame.x+source.frame.width-.5,y:site.y+source.frame.y+70};
    await page.mouse.move(p.x,p.y);await page.mouse.down();
    try{
      await wait(id=>globalThis.dockingScene.floatingReview.hosts[id].moving,before.sourceId);
      await move(p,46);const preview=await snapshot();assert.equal(preview.hosts[before.sourceId].frame.width,309);
      assert.deepEqual(preview.hosts[before.sourceId].bounds,source.bounds);
    }finally{await page.mouse.up();}
    await wait(id=>globalThis.dockingScene.floatingReview.hosts[id].bounds.width===309,before.sourceId);
    const after=(await snapshot()).hosts[before.sourceId];assert.equal(after.bounds.x,source.bounds.x);assert.equal(after.rootIdentity,source.rootIdentity);
  });
  await test('Docking feedback reuses visuals while guides hide and reappear during one captured drag',async()=>{
    await prepare();const before=await snapshot(),p=await begin();
    try{
      await move(p,15);const warm=await snapshot();assert(warm.guideCount>0);
      for(const dx of [25,35,45,30,20])await move(p,dx);
      await page.keyboard.down('Control');try{await move(p,26);assert.equal((await snapshot()).guideCount,0);}finally{await page.keyboard.up('Control');}
      await move(p,15);const after=await snapshot();
      assert(after.guideCount>0);assert.equal(after.feedbackCreated,warm.feedbackCreated);assert.equal(after.feedbackIdentity,warm.feedbackIdentity);
      assert.equal(after.plans,before.plans+1);assert(after.feedbackUpdates>warm.feedbackUpdates);
      await page.screenshot({path:`${output}/retained-floating-guides.png`});await page.keyboard.press('Escape');
    }finally{await page.mouse.up();}
    const end=await snapshot();assert.equal(end.interaction,false);assert.equal(end.guideCount,0);assert.equal(end.preview,undefined);
    assert.deepEqual(end.hosts,before.hosts);assert.equal(end.revision,before.revision);
  });
  await test('Changing snap settings through a real shortcut cancels an active floating drag',async()=>{
    await prepare();const before=await snapshot(),p=await begin();
    try{await move(p,46);await page.keyboard.press('F9');await wait(()=>!globalThis.dockingScene.floatingReview.interaction);}
    finally{await page.mouse.up();}
    const after=await snapshot();assert.equal(after.snap,0);assert.equal(after.revision,before.revision);assert.deepEqual(after.hosts,before.hosts);
  });
  await test('Viewport changes cancel floating geometry without a stale commit',async()=>{
    await prepare();const before=await snapshot(),p=await begin(),viewport=page.viewportSize();
    try{
      await move(p,46);await page.setViewportSize({width:viewport.width+50,height:viewport.height+20});
      await wait(()=>!globalThis.dockingScene.floatingReview.interaction);
      const after=await snapshot();assert.equal(after.revision,before.revision);
      assert.deepEqual(after.hosts[before.sourceId].bounds,before.hosts[before.sourceId].bounds);
    }finally{await page.mouse.up();await page.setViewportSize(viewport);await settle();}
  });
  await test('A stationary floating tab click does not build a magnetism plan or preview',async()=>{
    await prepare();const before=await snapshot();await click('tab:float-a');await settle();const after=await snapshot();
    assert.equal(after.plans,before.plans);assert.equal(after.feedbackUpdates,before.feedbackUpdates);assert.equal(after.interaction,false);
    await reset();
  });
}
