import assert from 'node:assert/strict';

export async function runMdiMagnetismTests({page,state,test,reset,click,wait,bounds,settle,menu,output}) {
  const scope = async () => (await state()).scopedMdi;
  const prepare = async () => {
    await click('scope:magnetic-reset');
    await wait(() => globalThis.dockingScene.scopedMdi?.windows.a.x===16 && globalThis.dockingScene.scopedMdi?.windows.b.x===220);
    await settle();
  };
  async function beginMove(id='a') {
    const title=await bounds('window-title:'+id);
    const p={x:title.x+Math.min(20,title.width/2),y:title.y+title.height/2};
    await page.mouse.move(p.x,p.y);await page.mouse.down();
    await wait(id=>globalThis.dockingScene.scopedMdi.leftFrames[id].moving,id);
    return p;
  }
  async function moveTo(p, dx, dy=0) { await page.mouse.move(p.x+dx,p.y+dy,{steps:12});await settle(); }
  await test('MDI magnetism previews a precise gap, builds one plan and commits one undoable change',async()=>{
    await reset();await menu('menubar:Window','mdi-context:scope-sample');await bounds('scope:magnetic-reset');await prepare();
    const before=await scope(), p=await beginMove();
    try {
      await moveTo(p,40);
      const preview=await scope();
      assert.equal(preview.leftFrames.a.rawX,56);assert.equal(preview.leftFrames.a.x,59);
      assert.deepEqual(preview.windows.a,before.windows.a);assert.equal(preview.revision,before.revision);
      assert.equal(preview.planBuilds,before.planBuilds+1);assert(preview.leftFrames.a.queries>before.leftFrames.a.queries);
      await settle();assert.equal((await scope()).planBuilds,preview.planBuilds);
    } finally {await page.mouse.up();}
    await wait(rev=>globalThis.dockingScene.scopedMdi.revision===rev+1,before.revision);
    const after=await scope();assert.equal(after.windows.a.x,59);assert.equal(after.windows.a.width,160);
    assert.equal(after.leftFrames.a.magnetic,false);assert.equal(after.leftFrames.a.contentInstance,before.leftFrames.a.contentInstance);
    assert.deepEqual(after.leftFrames.a.buttons,before.leftFrames.a.buttons);
    assert.deepEqual(after.windows.c,before.windows.c);assert.deepEqual(after.windows.d,before.windows.d);
    await page.screenshot({path:`${output}/mdi-magnetism-gap1.png`});
    await click('scope:undo');await wait(()=>globalThis.dockingScene.scopedMdi.windows.a.x===16);
  });
  await test('Control bypass and zero snap distance preserve raw pointer placement',async()=>{
    await prepare();await page.keyboard.down('Control');
    try {
      const p=await beginMove();try {await moveTo(p,40);}finally{await page.mouse.up();}
    } finally {await page.keyboard.up('Control');}
    await wait(()=>globalThis.dockingScene.scopedMdi.windows.a.x===56);
    await prepare();await click('scope:magnetic-off');const before=await scope();
    const p=await beginMove();try{await moveTo(p,40);}finally{await page.mouse.up();}
    await wait(()=>globalThis.dockingScene.scopedMdi.windows.a.x===56);
    assert.equal((await scope()).planBuilds,before.planBuilds);
  });
  await test('Configured magnetism gaps change actual native frame placement',async()=>{
    await prepare();await click('scope:magnetic-gap8');
    const before=await scope(), p=await beginMove();try{await moveTo(p,39);}finally{await page.mouse.up();}
    await wait(()=>globalThis.dockingScene.scopedMdi.windows.a.x===52);
    const after=await scope();assert.equal(after.windows.b.x-after.windows.a.x-after.windows.a.width,8);
    assert.equal(after.leftFrames.a.contentInstance,before.leftFrames.a.contentInstance);
    await page.screenshot({path:`${output}/mdi-magnetism-gap8.png`});
  });
  await test('Magnetic resize snaps right and left edges while preserving opposite anchors',async()=>{
    for(const id of ['a','b']) {
      await prepare();const before=await scope(), host=await bounds('scope:left-host'), f=before.leftFrames[id];
      const p={x:host.x+f.x+(id==='a'?f.width-2:2),y:host.y+f.y+f.height/2};
      await page.mouse.move(p.x,p.y);await page.mouse.down();
      try {
        await wait(id=>globalThis.dockingScene.scopedMdi.leftFrames[id].moving,id);
        await moveTo(p,id==='a'?40:-40);
        const preview=await scope();assert.equal(preview.leftFrames[id].width,203);assert.deepEqual(preview.windows[id],before.windows[id]);
      }finally{await page.mouse.up();}
      await wait(id=>globalThis.dockingScene.scopedMdi.windows[id].width===203,id);
      const changed=(await scope()).windows[id];
      assert.equal(id==='a'?changed.x:changed.x+changed.width,id==='a'?16:380);
      assert.equal(changed.height,before.windows[id].height);
    }
  });
  await test('Escape cancels magnetic preview and releases the retained neighbor plan',async()=>{
    await prepare();const before=await scope(), p=await beginMove();
    try {
      await moveTo(p,40);assert.equal((await scope()).leftFrames.a.x,59);
      await page.keyboard.press('Escape');
      await wait(()=>!globalThis.dockingScene.scopedMdi.leftFrames.a.moving);
    }finally{await page.mouse.up();}
    const after=await scope();assert.deepEqual(after.windows,before.windows);assert.equal(after.revision,before.revision);
    assert.equal(after.leftFrames.a.x,16);assert.equal(after.leftFrames.a.magnetic,false);
  });
  await test('Host viewport changes invalidate a magnetic gesture instead of committing stale geometry',async()=>{
    await prepare();const before=await scope(), p=await beginMove(), viewport=page.viewportSize();
    try {
      await moveTo(p,40);await page.setViewportSize({width:viewport.width+60,height:viewport.height+30});
      await wait(()=>!globalThis.dockingScene.scopedMdi.leftFrames.a.moving);
      const after=await scope();assert.equal(after.revision,before.revision);assert.deepEqual(after.windows,before.windows);
      assert.equal(after.leftFrames.a.magnetic,false);
    }finally{await page.mouse.up();await page.setViewportSize(viewport);await settle();}
  });
  await test('Minimized siblings and documents in another host are excluded from snap plans',async()=>{
    await prepare();await click('window:b:minimize');await settle();
    // A's title activates it before the frame gesture records the revision.
    const before=await scope(), p=await beginMove();
    try {await moveTo(p,40);assert.equal((await scope()).leftFrames.a.x,56);}
    finally{await page.mouse.up();}
    await wait(()=>globalThis.dockingScene.scopedMdi.windows.a.x===56);
    assert.equal((await scope()).planBuilds,before.planBuilds);
    await reset();
  });
}
