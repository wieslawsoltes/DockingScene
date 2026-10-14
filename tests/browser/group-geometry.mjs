import assert from 'node:assert/strict';
import { pngPixels } from './painted-tabs.mjs';

export async function runGroupGeometryTests({page,state,test,reset,click,wait,bounds,settle,menu,output}) {
  const current=async()=>(await state()).toolCaptions;
  const near=(a,b,label)=>assert(Math.abs(a-b)<=1,`${label}: ${a} != ${b}`);
  const group=s=>s.policies.geometry.groups[s.windows['cap-a'].group];
  async function layout(id) {
    await click('caption-sample:policies'); await settle();
    await click('caption-policy:layout'); await settle();
    await click('caption-layout:'+id); await settle();
  }
  async function prepare(axis='horizontal') {
    await page.keyboard.press('Escape');
    if(!(await state()).toolCaptions) {
      await reset(); await menu('menubar:Window','mdi-context:tool-caption-sample'); await bounds('caption-sample:policies');
    }
    await layout(axis); await bounds('caption-editor:cap-a'); await settle();
    const s=await current(); assert.equal(s.error,null); assert.equal(group(s).placement,'Top'); assert(group(s).stripVisible);
    return s;
  }
  function retained(a,b) {
    assert.equal(b.windows['cap-a'].editor,a.windows['cap-a'].editor);
    assert.equal(group(b).identity,group(a).identity); assert.equal(group(b).tabs,group(a).tabs);
    assert.equal(b.hosts[a.toolHost].frame,a.hosts[a.toolHost].frame);
    assert.equal(b.error,null);
  }

  await test('Vertical tool tabs reserve real width without consuming the editor minimum',async()=>{
    const top=await prepare(),before=await bounds('caption-editor:cap-a');
    near(group(top).width,122,'Horizontal-tab group minimum'); assert(before.width>=119);
    await layout('Left'); const left=await current(),after=await bounds('caption-editor:cap-a');
    near(group(left).width,277,'Vertical-tab group includes its 155-pixel strip');
    near(after.width,before.width,'Content minimum survives axis change'); near(after.x-before.x,155,'Editor moves beside the native strip');
    assert(after.width>=119); assert(left.geometryBuilds>top.geometryBuilds); retained(top,left);
    await page.screenshot({path:`${output}/vertical-tabs-minimum.png`});
  });
  await test('Opposite tab edges reuse group geometry while native content switches sides',async()=>{
    const top=await prepare(); await layout('Bottom'); const bottom=await current();
    assert.equal(bottom.geometryBuilds,top.geometryBuilds); retained(top,bottom);
    await layout('Left'); const left=await current(),before=await bounds('caption-editor:cap-a');
    await layout('Right'); const right=await current(),after=await bounds('caption-editor:cap-a');
    assert.equal(right.geometryBuilds,left.geometryBuilds); near(group(right).width,group(left).width,'Equivalent vertical envelope');
    near(before.x-after.x,155,'Right tabs move content back to the leading edge'); near(before.width,after.width,'Content width'); retained(left,right);
    await layout('Top'); assert((await current()).geometryBuilds>right.geometryBuilds);
  });
  await test('Stacked tools recover horizontal tab height when switched to vertical tabs',async()=>{
    const top=await prepare('vertical'),before=await bounds('caption-editor:cap-a'); assert(before.height>=79);
    await layout('Left'); const left=await current(),after=await bounds('caption-editor:cap-a');
    near(group(top).height-group(left).height,top.policies.geometry.tabHeight,'Vertical tabs remove horizontal chrome height');
    near(before.height,after.height,'Editor minimum is retained'); assert(after.height>=79); retained(top,left);
    await menu('caption-sample:policies','caption-policy:title-hide'); const hidden=await current(),content=await bounds('caption-editor:cap-a');
    near(group(left).height-group(hidden).height,left.policies.titleHeight,'Hidden title removes its constrained height');
    near(content.height,after.height,'Title removal does not reduce content minimum'); retained(left,hidden);
  });
  await test('Placement undo and theme changes retain editable content with actual dark paint',async()=>{
    await prepare(); await layout('Left');
    await click('caption-editor:cap-a'); await page.keyboard.press('Control+a'); await page.keyboard.type('vertical geometry retained text');
    await wait(()=>globalThis.dockingScene.toolCaptions.policies.windows['cap-a'].text==='vertical geometry retained text');
    const left=await current(); await layout('Top'); await click('caption-sample:undo'); await settle();
    const undone=await current(); assert.equal(group(undone).placement,'Left'); retained(left,undone);
    assert.equal(undone.policies.windows['cap-a'].text,'vertical geometry retained text');
    if(!undone.dark) {await click('caption-sample:theme'); await settle();}
    const dark=await current(); assert(dark.dark); retained(undone,dark);
    const r=await bounds('caption-editor:cap-a'),image=pngPixels(await page.screenshot({path:`${output}/vertical-tabs-dark.png`}));
    const color=image.rgb(Math.floor(r.x+r.width/2),Math.floor(r.y+r.height-14));
    assert(color.every(c=>c<110),`Editor must paint dark pixels, actual ${color}`);
    await click('caption-sample:theme'); await settle(); const light=await current(); assert(!light.dark); retained(dark,light);
    assert.equal(light.policies.windows['cap-a'].text,'vertical geometry retained text');
  });
  await test('Live floating resizing uses the vertical envelope and Escape restores all bounds',async()=>{
    await prepare(); await layout('Left'); const before=await current();
    const frame=before.policies.geometry;
    const p={x:frame.frameX+frame.frameWidth-.5,y:frame.frameY+70};
    await page.mouse.move(p.x,p.y); await page.mouse.down();
    try {
      await wait(()=>globalThis.dockingScene.toolCaptions.policies.geometry.moving);
      await page.mouse.move(p.x+80,p.y,{steps:12}); await settle();
      const live=await current(),editor=await bounds('caption-editor:cap-a');
      assert(live.policies.geometry.frameWidth>before.policies.geometry.frameWidth+60);
      assert.equal(live.policies.geometry.plans,1); assert(live.policies.geometry.arrangements>before.policies.geometry.arrangements);
      assert.equal(live.revision,before.revision); near(group(live).width,277,'Live plan preserves minimum-clamped left pane'); assert(editor.width>=119); retained(before,live);
      await page.keyboard.press('Escape'); await wait(()=>!globalThis.dockingScene.toolCaptions.policies.geometry.moving);
    } finally {await page.mouse.up();}
    await settle(); const after=await current(); retained(before,after);
    assert.equal(after.revision,before.revision); assert.equal(after.policies.geometry.plans,0);
    near(after.policies.geometry.frameWidth,before.policies.geometry.frameWidth,'Frame restored');
    near(group(after).width,group(before).width,'Group restored');
  });
  await reset();
}
