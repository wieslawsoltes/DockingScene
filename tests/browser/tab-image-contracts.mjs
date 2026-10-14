import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { mkdir, writeFile } from 'node:fs/promises';
import { pngPixels } from './painted-tabs.mjs';

const output=process.env.BROWSER_RESULTS||'artifacts/image-results';
await mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,args:['--enable-webgl','--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1440,height:960}});
const results=[],errors=[]; page.on('pageerror',e=>errors.push(String(e)));
const state=()=>page.evaluate(()=>globalThis.imageContracts);
const wait=(fn,arg)=>page.waitForFunction(fn,arg,{timeout:20000});
const image=(s,id)=>s.images.find(i=>i.id==='tab-image:'+id);
async function settle(){const s=(await state()).sequence; await wait(s=>globalThis.imageContracts.sequence>s+2,s);}
async function click(id){await wait(id=>globalThis.imageContracts?.bounds[id]?.width>0,id); const r=(await state()).bounds[id]; await page.mouse.click(r.x+Math.min(30,r.width/2),r.y+r.height/2); await settle();}
async function action(id){await click('action:'+id);}
async function loaded(id){await wait(id=>globalThis.imageContracts?.images.some(i=>i.id==='tab-image:'+id && i.pixels===16),id);}
async function paint(id,expected){await loaded(id);const s=await state(),r=image(s,id);const p=pngPixels(await page.screenshot());const actual=p.rgb(Math.floor(r.x+8),Math.floor(r.y+8)); assert(actual.every((c,i)=>Math.abs(c-expected[i])<=3),`Actual image pixels ${actual}; expected ${expected}`);}
async function test(name,run){try{await run();results.push({name,passed:true});console.log('PASS '+name);}catch(e){results.push({name,passed:false,error:String(e),stack:e.stack});console.error('FAIL '+name+': '+e);await page.screenshot({path:`${output}/failure-${results.length}.png`}).catch(()=>{});}}
try{
  await page.goto(process.env.BROWSER_URL||'http://127.0.0.1:8765/image-contracts/',{waitUntil:'domcontentloaded',timeout:60000});
  await wait(()=>globalThis.imageContracts?.sequence>2||globalThis.imageContractError);
  assert.equal(await page.evaluate(()=>globalThis.imageContractError??null),null);
  await page.locator('.uno-loader').waitFor({state:'hidden',timeout:30000});await settle();
  let baseline;
  await test('Image APIs are opt-in and do not instantiate off-screen content',async()=>{
    baseline=await state();assert.equal(baseline.enabled,false);assert.equal(baseline.documents,false);assert.equal(baseline.tools,false);
    assert.equal(baseline.images.length,0);assert.equal(baseline.created,1);assert.equal(baseline.count,3);
  });
  await test('Native images paint exact fixture pixels and reserve a 20-unit label slot',async()=>{
    await action('Toggle');await loaded('tab-0');await loaded('tab-1');const s=await state();
    assert.equal(s.images.length,2);assert(!image(s,'tab-2'));assert.equal(s.extent-baseline.extent,40);
    for(const i of s.images){assert.equal(i.width,16);assert.equal(i.height,16);assert.equal(i.hitTest,false);}
    assert.equal(s.created,1);assert.equal(s.contents['tab-0'],baseline.contents['tab-0']);await paint('tab-0',[230,25,65]);
  });
  await test('Replacing an image retains its visual, measured text, layout and content',async()=>{
    const a=await state(),before=image(a,'tab-0');await action('Swap');await paint('tab-0',[25,95,230]);const b=await state(),after=image(b,'tab-0');
    assert.equal(after.identity,before.identity);assert.notEqual(after.source,before.source);
    assert.equal(b.measured,a.measured);assert.equal(b.headers,a.headers);assert.equal(b.layouts,a.layouts);assert.equal(b.extent,a.extent);
    assert.equal(b.contents['tab-0'],a.contents['tab-0']);
  });
  await test('Clearing and restoring one image reclaims only its own measured slot',async()=>{
    const a=await state(),sibling=image(a,'tab-1');await action('Clear');const b=await state();assert(!image(b,'tab-0'));
    assert.equal(a.extent-b.extent,20);assert.equal(image(b,'tab-1').identity,sibling.identity);
    await action('Restore');await paint('tab-0',[230,25,65]);const c=await state();assert.equal(c.extent,a.extent);assert.equal(c.created,a.created);
  });
  await test('A pointer click on a non-hit-testable image activates its native tab',async()=>{
    const r=image(await state(),'tab-1');await page.mouse.click(r.x+8,r.y+8);await wait(()=>globalThis.imageContracts.selected==='tab-1');await settle();
    const s=await state();assert.equal(s.created,2);assert(s.contents['tab-1']);assert.equal(s.contents['tab-0'],baseline.contents['tab-0']);
  });
  await test('All four placements and both themes retain selected content and paint images',async()=>{
    const a=await state();
    for(const side of ['Bottom','Left','Right','Top']){await action('Edge');assert.equal((await state()).placement,side);await paint('tab-1',[230,25,65]);assert.equal((await state()).contents['tab-1'],a.contents['tab-1']);}
    await action('Theme');assert((await state()).dark);await paint('tab-1',[230,25,65]);await page.screenshot({path:`${output}/images-dark.png`});
    await action('Theme');assert(!(await state()).dark);assert.equal((await state()).contents['tab-1'],a.contents['tab-1']);
  });
  await test('Hiding and restoring the strip releases image visuals without recreating content',async()=>{
    const a=await state();await action('Strip');const b=await state();assert.equal(b.strip,false);assert.equal(b.images.length,0);assert.equal(b.created,a.created);
    await action('Strip');await loaded('tab-1');assert.equal((await state()).contents['tab-1'],a.contents['tab-1']);
  });
  await test('Two thousand shared image sources retain bounded header virtualization',async()=>{
    await action('Many');await loaded('tab-0');const a=await state();assert.equal(a.count,2002);assert.equal(a.created,1);
    assert(a.realized>0&&a.realized<30);assert.equal(a.images.length,a.realized);
    await action('Swap');const b=await state();assert.equal(b.measured,a.measured);assert.equal(b.headers,a.headers);assert.equal(b.created,1);
  });
  await test('Document and tool image policies are independent and survive floating and undo',async()=>{
    const a=await state();await action('Tools');await loaded('host-tool-a');await loaded('host-tool-b');const b=await state();
    assert.equal(b.tools,true);assert.equal(b.documents,false);assert(!image(b,'host-doc'));assert.equal(b.measured,a.measured);
    await action('Documents');await loaded('host-doc');await paint('host-doc',[230,25,65]);
    const content=(await state()).contents['host-doc'];await action('Float');await loaded('host-doc');assert.equal((await state()).contents['host-doc'],content);
    await action('Undo');await loaded('host-doc');assert.equal((await state()).contents['host-doc'],content);
    await action('Tools');assert(!image(await state(),'host-tool-a'));assert(image(await state(),'host-doc'));await page.screenshot({path:`${output}/images-hosted.png`});
  });
  await test('No native or JavaScript errors and the exact source revision is tested',async()=>{
    assert.deepEqual(errors,[]);assert.equal((await state()).error,null);assert.equal(await page.evaluate(()=>globalThis.imageContractError??null),null);
    const r=await page.request.get(new URL('build-info.json',page.url()).href);assert(r.ok());
    assert.equal((await r.json()).commit,process.env.EXPECTED_BUILD_COMMIT);
  });
}catch(e){results.push({name:'Initialization',passed:false,error:String(e),stack:e.stack});console.error(e);}
finally{await writeFile(`${output}/results.json`,JSON.stringify({results,errors,state:await state().catch(()=>null)},null,2));await browser.close();}
if(results.length!==10||results.some(r=>!r.passed))process.exitCode=1;
