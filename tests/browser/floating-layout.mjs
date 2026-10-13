import assert from 'node:assert/strict';
import { assertFloatingPaint } from './painted-floating.mjs';
import { pngPixels } from './painted-tabs.mjs';

export async function runFloatingLayoutTests({page,state,test,reset,click,wait,bounds,settle,menu,output}) {
  const snapshot = async () => (await state()).floatingReview;
  const near = (a,b,message) => assert(Math.abs(a-b)<.05, `${message}: ${a} != ${b}`);
  async function prepare() {
    const previous=await snapshot();
    await click('float-review:reset');
    await wait(g=>globalThis.dockingScene.floatingReview.generation>g,previous.generation);
    await settle();
    return snapshot();
  }
  async function begin(before, edge='right') {
    const site=await bounds('float-review:site'), r=before.hosts[before.sourceId].frame;
    const p=edge==='bottom' ? {x:site.x+r.x+r.width/2,y:site.y+r.y+r.height-.5}
      : {x:site.x+r.x+r.width-.5,y:site.y+r.y+70};
    await page.mouse.move(p.x,p.y); await page.mouse.down();
    await wait(id=>globalThis.dockingScene.floatingReview.hosts[id].moving,before.sourceId);
    return p;
  }
  async function move(p,dx,dy=0) { await page.mouse.move(p.x+dx,p.y+dy,{steps:12}); await settle(); }
  function retained(before,after) {
    assert.deepEqual(after.editors,before.editors);
    assert.deepEqual(after.hosts[before.sourceId].nodes,before.hosts[before.sourceId].nodes);
    assert.equal(after.hosts[before.sourceId].rootIdentity,before.hosts[before.sourceId].rootIdentity);
    assert.equal(after.hosts[before.sourceId].frameIdentity,before.hosts[before.sourceId].frameIdentity);
    assert.deepEqual(after.hosts[before.neighborId],before.hosts[before.neighborId]);
    assert.deepEqual(after.contents['float-c'],before.contents['float-c']);
  }
  function sourceFits(s) {
    const r=s.hosts[s.sourceId].frame,a=s.contents['float-a'],b=s.contents['float-b'];
    for(const c of [a,b]) {
      near(c.group.x,r.x+1,'Group follows frame x'); near(c.group.width,r.width-2,'Group fills frame');
      assert(c.editor.width>=c.group.width-4,'Retained editor fills resized group');
      assert(c.editor.height<=c.group.height && c.editor.height>0,'Editor remains inside group');
    }
    const bottom=Math.max(a.group.y+a.group.height,b.group.y+b.group.height);
    near(bottom,r.y+r.height-1,'Last group follows frame bottom');
    const splitter=s.splitters[s.hosts[s.sourceId].rootId];
    near(splitter.x,r.x+1,'Splitter follows frame x'); near(splitter.width,r.width-2,'Splitter fills live width');
  }
  await test('Live floating resize reflows both editors and splitter before a single commit',async()=>{
    await reset(); await menu('menubar:Window','mdi-context:floating-sample'); await bounds('float-review:reset'); await settle();
    const before=await snapshot(),p=await begin(before);
    try {
      await move(p,46); const live=await snapshot();
      near(live.hosts[before.sourceId].frame.width,309,'Magnetic live resize'); sourceFits(live);
      assert.equal(live.revision,before.revision);
      assert.deepEqual(live.hosts[before.sourceId].bounds,before.hosts[before.sourceId].bounds);
      assert.equal(live.layoutPlans,before.layoutPlans+1); assert.equal(live.activeLayoutPlans,1);
      assert.equal(live.fullLayoutBuilds,before.fullLayoutBuilds); retained(before,live);
      await assertFloatingPaint(page,await bounds('float-review:site'),live.hosts[before.sourceId].frame,before.hosts[before.sourceId].frame);
      await page.screenshot({path:`${output}/floating-live-resize.png`});
    } finally { await page.mouse.up(); }
    await wait(r=>globalThis.dockingScene.floatingReview.revision===r+1,before.revision); await settle();
    const after=await snapshot(); sourceFits(after); retained(before,after); assert.equal(after.activeLayoutPlans,0);
    await click('float-review:undo'); await settle();
    assert.deepEqual((await snapshot()).contents,before.contents);
  });
  await test('Bottom resizing and raw reversal reflow split heights and Escape restores both layers',async()=>{
    const before=await prepare(),p=await begin(before,'bottom');
    try {
      await move(p,0,60); const larger=await snapshot(); sourceFits(larger);
      near(larger.hosts[before.sourceId].frame.height,280,'Live height');
      assert(larger.contents['float-a'].group.height>before.contents['float-a'].group.height);
      assert(larger.contents['float-b'].group.height>before.contents['float-b'].group.height);
      for(const dy of [30,70,20,60]) { await move(p,0,dy); sourceFits(await snapshot()); }
      assert.equal((await snapshot()).layoutPlans,before.layoutPlans+1);
      assert.equal((await snapshot()).revision,before.revision);
      await page.screenshot({path:`${output}/floating-live-split-heights.png`});
      await page.keyboard.press('Escape');
      await wait(()=>globalThis.dockingScene.floatingReview.activeLayoutPlans===0);
    } finally { await page.mouse.up(); }
    await settle(); const after=await snapshot();
    assert.deepEqual(after.contents,before.contents); assert.deepEqual(after.splitters,before.splitters);
    assert.deepEqual(after.hosts,before.hosts); assert.equal(after.revision,before.revision);
  });
  await test('Keyboard floating Move carries real contents provisionally and commits on Enter release',async()=>{
    const before=await prepare(),id=before.sourceId;
    await menu('window:'+id+':system','window-menu:'+id+':move');
    for(const key of ['ArrowRight','ArrowRight','ArrowDown']) await page.keyboard.press(key);
    await settle(); const live=await snapshot(); sourceFits(live);
    near(live.contents['float-a'].group.x,before.contents['float-a'].group.x+16,'Editor group moved');
    near(live.contents['float-a'].group.y,before.contents['float-a'].group.y+8,'Editor group moved vertically');
    assert.equal(live.revision,before.revision); retained(before,live);
    await assertFloatingPaint(page,await bounds('float-review:site'),live.hosts[id].frame,before.hosts[id].frame);
    await page.keyboard.down('Enter');
    try { await settle(); assert.equal((await snapshot()).revision,before.revision); }
    finally { await page.keyboard.up('Enter'); }
    await wait(r=>globalThis.dockingScene.floatingReview.revision===r+1,before.revision); await settle();
    sourceFits(await snapshot()); assert.equal((await snapshot()).activeLayoutPlans,0);
  });
  await test('Keyboard Size has live content and unchanged completion releases its layout plan',async()=>{
    const before=await prepare(),id=before.sourceId;
    await menu('window:'+id+':system','window-menu:'+id+':size');
    for(const key of ['ArrowRight','ArrowRight','ArrowDown','ArrowDown']) await page.keyboard.press(key);
    await settle(); const live=await snapshot(); sourceFits(live);
    near(live.hosts[id].frame.width,before.hosts[id].frame.width+8,'Keyboard width');
    near(live.hosts[id].frame.height,before.hosts[id].frame.height+8,'Keyboard height');
    await page.keyboard.press('Escape'); await settle();
    assert.deepEqual((await snapshot()).contents,before.contents); assert.equal((await snapshot()).revision,before.revision);
    await menu('window:'+id+':system','window-menu:'+id+':size'); await page.keyboard.press('Enter'); await settle();
    assert.equal((await snapshot()).activeLayoutPlans,0); assert.equal((await snapshot()).revision,before.revision);
  });
  await test('Frame-only mode avoids live layout work but commits the same final editor geometry',async()=>{
    await prepare(); await click('float-review:layout'); await settle(); const before=await snapshot();
    assert.equal(before.liveLayout,false); const p=await begin(before);
    try {
      await move(p,46); const preview=await snapshot();
      near(preview.hosts[before.sourceId].frame.width,309,'Frame-only preview');
      assert.deepEqual(preview.contents,before.contents); assert.equal(preview.layoutPlans,before.layoutPlans);
      assert.equal(preview.activeLayoutPlans,0); assert.equal(preview.layoutArranges,before.layoutArranges);
    } finally { await page.mouse.up(); }
    await settle(); sourceFits(await snapshot()); await click('float-review:layout');
  });
  await test('A color-only refresh preserves live floating geometry and retained editor identity',async()=>{
    const before=await prepare(),p=await begin(before);
    try {
      await move(p,46); const live=await snapshot();
      await page.keyboard.press('F2'); await settle(); const dark=await snapshot();
      assert.notEqual(dark.theme,live.theme); sourceFits(dark);
      assert.deepEqual(dark.contents,live.contents); assert.equal(dark.activeLayoutPlans,1);
      assert.equal(dark.layoutPlans,live.layoutPlans); assert.deepEqual(dark.editors,before.editors);
      const image=pngPixels(await page.screenshot()),site=await bounds('float-review:site');
      const r=dark.contents['float-a'].editor;
      const color=image.rgb(Math.round(site.x+r.x+r.width-8),Math.round(site.y+r.y+10));
      assert(color.every(v=>v<100),'Dark live editor must actually paint dark pixels');
      await page.screenshot({path:`${output}/floating-live-dark.png`});
      await page.keyboard.press('Escape'); await wait(()=>globalThis.dockingScene.floatingReview.activeLayoutPlans===0);
    } finally { await page.mouse.up(); }
    assert.equal((await snapshot()).revision,before.revision);
  });
  await test('Settings and viewport invalidation cancel live resize without leaving a stale content projection',async()=>{
    for(const reason of ['snap','viewport']) {
      const before=await prepare(),p=await begin(before),viewport=page.viewportSize();
      try {
        await move(p,46); sourceFits(await snapshot());
        if(reason==='snap') await page.keyboard.press('F9');
        else await page.setViewportSize({width:viewport.width+50,height:viewport.height+20});
        await wait(()=>globalThis.dockingScene.floatingReview.activeLayoutPlans===0);
        await settle(); const after=await snapshot();
        assert.equal(after.revision,before.revision); assert.deepEqual(after.contents,before.contents);
        assert.deepEqual(after.hosts[before.sourceId].bounds,before.hosts[before.sourceId].bounds);
      } finally { await page.mouse.up(); await page.setViewportSize(viewport); await settle(); }
    }
    await reset();
  });
}
