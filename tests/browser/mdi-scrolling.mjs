import assert from 'node:assert/strict';
import { pngPixels } from './painted-tabs.mjs';

export async function runMdiScrollingTests({page,state,test,reset,click,wait,bounds,settle,menu,output}) {
  const sample=async()=>(await state()).mdiScrolling;
  const models=s=>Object.fromEntries(Object.entries(s.windows).map(([id,w])=>[id,w.model]));
  async function prepare(){await click('scroll-sample:reset');await click('scroll-sample:enable');await settle();}
  async function captionPixel(id){
    await settle();const r=await bounds('window-title:'+id),image=pngPixels(await page.screenshot());
    const scale=image.width/page.viewportSize().width;
    const rgb=image.rgb(Math.round((r.x+Math.min(r.width-2,5))*scale),Math.round((r.y+2)*scale));
    assert(rgb.every((v,i)=>Math.abs(v-[0,122,204][i])<=3),'Scrolled active caption paint must match hit geometry: '+rgb);
  }
  await test('Standard MDI scrolling is opt-in and native scrollbar input does not mutate window bounds',async()=>{
    await reset();await menu('menubar:Window','mdi-context:scroll-sample');await bounds('scroll-sample:enable');
    assert.equal((await sample()).enabled,false);
    await click('scroll-sample:enable');await bounds('mdi-scroll:sample:horizontal');await bounds('mdi-scroll:sample:vertical');await settle();
    const before=await sample(),h=await bounds('mdi-scroll:sample:horizontal');
    await page.mouse.click(h.x+h.width-4,h.y+h.height/2);
    await wait(x=>globalThis.dockingScene.mdiScrolling.offsetX>x,before.offsetX);
    const next=await sample(),v=await bounds('mdi-scroll:sample:vertical');
    await page.mouse.click(v.x+v.width/2,v.y+v.height-4);
    await wait(y=>globalThis.dockingScene.mdiScrolling.offsetY>y,next.offsetY);
    const after=await sample();assert.equal(after.revision,before.revision);assert.deepEqual(models(after),models(before));
    assert.equal(after.builds,before.builds);assert(after.reuses>before.reuses);
    await page.screenshot({path:`${output}/mdi-scroll-native-bars.png`});
  });
  await test('Activation reveals positive and negative off-screen documents without rewriting their coordinates',async()=>{
    await prepare();const before=await sample();
    await click('scroll-sample:far');await bounds('window-title:scroll-far');
    let s=await sample(),far=s.windows['scroll-far'];
    assert(s.viewport.x>0 && s.viewport.y>0);assert(far.frame.x>=0 && far.frame.y>=0);
    assert(far.frame.x+far.frame.width<=s.viewport.width+.01 && far.frame.y+far.frame.height<=s.viewport.height+.01);
    assert.deepEqual(far.model,before.windows['scroll-far'].model);await captionPixel('scroll-far');
    await page.screenshot({path:`${output}/mdi-scroll-far-document.png`});
    await click('scroll-sample:negative');await bounds('window-title:scroll-negative');
    s=await sample();assert(s.viewport.x<0 && s.viewport.y<0);assert(s.windows['scroll-negative'].frame.x>=0);
    assert.deepEqual(models(s),models(before));await captionPixel('scroll-negative');
    await page.screenshot({path:`${output}/mdi-scroll-negative-document.png`});
  });
  await test('Reveal and repeated scrolling retain native frames, editors and extent plans',async()=>{
    await prepare();const before=await sample();
    for(let i=0;i<4;i++){await click('scroll-sample:reveal');await bounds('window-title:scroll-far');await click('scroll-sample:origin');await settle();}
    const after=await sample();assert.equal(after.revision,before.revision);assert.equal(after.active,before.active);assert.equal(after.builds,before.builds);
    for(const id of Object.keys(before.windows)){
      assert.equal(after.windows[id].identity,before.windows[id].identity);assert.equal(after.windows[id].editor,before.windows[id].editor);
      assert.deepEqual(after.windows[id].buttons,before.windows[id].buttons);
    }
  });
  await test('Dragging in a scrolled viewport commits world-space deltas once and remains undoable',async()=>{
    await prepare();await click('scroll-sample:far');await bounds('window-title:scroll-far');const before=await sample(),r=await bounds('window-title:scroll-far');
    const p={x:r.x+20,y:r.y+r.height/2};await page.mouse.move(p.x,p.y);await page.mouse.down();
    try{
      await wait(()=>globalThis.dockingScene.mdiScrolling.windows['scroll-far'].moving);
      await page.mouse.move(p.x-36,p.y-28,{steps:10});await settle();
      assert.deepEqual((await sample()).windows['scroll-far'].model,before.windows['scroll-far'].model);
    }finally{await page.mouse.up();}
    await wait(r=>globalThis.dockingScene.mdiScrolling.revision===r+1,before.revision);
    const moved=await sample();assert.equal(moved.windows['scroll-far'].model.x,before.windows['scroll-far'].model.x-36);
    assert.equal(moved.windows['scroll-far'].model.y,before.windows['scroll-far'].model.y-28);
    assert.equal(moved.windows['scroll-far'].editor,before.windows['scroll-far'].editor);
    await click('scroll-sample:undo');await wait(x=>globalThis.dockingScene.mdiScrolling.windows['scroll-far'].model.x===x,before.windows['scroll-far'].model.x);
    assert.deepEqual(models(await sample()),models(before));
  });
  await test('Scrolling during captured movement cancels the old coordinate session before release',async()=>{
    await prepare();await click('scroll-sample:far');await bounds('window-title:scroll-far');const before=await sample(),r=await bounds('window-title:scroll-far');
    const p={x:r.x+20,y:r.y+r.height/2};await page.mouse.move(p.x,p.y);await page.mouse.down();
    try{
      await page.mouse.move(p.x-30,p.y-24,{steps:8});await settle();assert((await sample()).windows['scroll-far'].moving);
      await page.keyboard.press('F4');await wait(()=>!globalThis.dockingScene.mdiScrolling.windows['scroll-far'].moving);
    }finally{await page.mouse.up();}
    const after=await sample();assert.equal(after.revision,before.revision);assert.deepEqual(models(after),models(before));
    assert(after.offsetX<before.offsetX || after.offsetY<before.offsetY);
  });
  await test('Maximized frames fill the scroll viewport and restore to their saved off-screen bounds',async()=>{
    await prepare();await click('scroll-sample:far');await bounds('window-title:scroll-far');const before=await sample();
    await click('window:scroll-far:maximize');await wait(()=>globalThis.dockingScene.mdiScrolling.windows['scroll-far'].state==='Maximized');
    const maximized=await sample(),f=maximized.windows['scroll-far'].frame;
    assert.equal(f.x,0);assert.equal(f.y,0);assert.equal(f.width,maximized.viewport.width);assert.equal(f.height,maximized.viewport.height);
    await click('window:scroll-far:restore');await bounds('window-title:scroll-far');await settle();
    const after=await sample();assert.deepEqual(after.windows['scroll-far'].model,before.windows['scroll-far'].model);
    assert.equal(after.windows['scroll-far'].editor,before.windows['scroll-far'].editor);assert(after.viewport.x>0 && after.viewport.y>0);
    await captionPixel('scroll-far');
  });
  await test('Theme changes and disabling scrolling retain content and do not add layout history',async()=>{
    await prepare();await click('scroll-sample:negative');await bounds('window-title:scroll-negative');const before=await sample();
    await click('scroll-sample:theme');await settle();const dark=await sample();
    assert.equal(dark.dark,true);assert.equal(dark.builds,before.builds);assert.deepEqual(dark.viewport,before.viewport);
    await captionPixel('scroll-negative');await page.screenshot({path:`${output}/mdi-scroll-dark.png`});
    await click('scroll-sample:disable');await settle();assert.equal((await state()).bounds['mdi-scroll:sample:horizontal'],undefined);
    assert.equal((await state()).bounds['mdi-scroll:sample:vertical'],undefined);
    await click('scroll-sample:enable');await bounds('window-title:scroll-negative');await settle();
    const after=await sample();assert.equal(after.revision,before.revision);assert.deepEqual(models(after),models(before));
    for(const id of Object.keys(before.windows))assert.equal(after.windows[id].editor,before.windows[id].editor);
  });
  await test('Minimized caption rows above zero can be reached with real wheel scrolling',async()=>{
    await click('scroll-sample:minimized');await bounds('mdi-scroll:sample:vertical');await settle();const before=await sample();
    assert(before.extent.y<0 && before.vertical);const view=await bounds('mdi-scroll:sample:viewport');
    await page.mouse.move(view.x+view.width/2,view.y+12);await page.mouse.wheel(0,-2400);
    await wait(y=>globalThis.dockingScene.mdiScrolling.viewport.y<y,before.viewport.y);
    await bounds('window-title:scroll-min-39');const after=await sample();assert.equal(after.revision,before.revision);
    assert(after.windows['scroll-min-39'].frame.y>=0);assert.equal(after.windows['scroll-min-39'].editor,undefined);
    await page.screenshot({path:`${output}/mdi-scroll-minimized-rows.png`});
    // Six captions fit exactly in two rows in the full 510x54 client. Starting
    // from the old bar-reduced width would wrap them into three rows forever.
    await click('scroll-sample:six');
    await wait(()=>!globalThis.dockingScene.mdiScrolling.vertical && !globalThis.dockingScene.mdiScrolling.horizontal);
    const reduced=await sample();assert.equal(reduced.revision,after.revision+1);
    assert.equal(reduced.viewport.x,0);assert.equal(reduced.viewport.y,0);
    assert.equal(reduced.viewport.width,510);assert.equal(reduced.viewport.height,54);
    assert.equal((await state()).bounds['mdi-scroll:sample:vertical'],undefined);
    await page.screenshot({path:`${output}/mdi-scroll-shrunk-extents.png`});
    await reset();
  });
}
