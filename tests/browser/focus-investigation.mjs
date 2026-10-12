import { chromium } from 'playwright';
import { mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { readHitBounds } from './hit-targets.mjs';
import { captureDiagnostics } from './diagnostics.mjs';

const output = process.env.BROWSER_RESULTS || 'artifacts/focus-investigation';
await mkdir(output, { recursive: true });
const browser = await chromium.launch({headless:true,args:['--enable-webgl','--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader']});
const context = await browser.newContext({viewport:{width:1440,height:960}});
const page = await context.newPage();
await page.addInitScript(() => {
  const records = []; globalThis.dockingSceneNativeFocusTrace = records;
  function identity(element) { return element ? `${element.tagName}#${element.id}` : null; }
  function record(event, phase) {
    const input = document.getElementById('uno-input');
    records.push({time:performance.now(), phase, type:event.type, target:identity(event.target), related:identity(event.relatedTarget),
      active:identity(document.activeElement), input:identity(input), prevented:event.defaultPrevented, detail:event.detail,
      x:event.clientX, y:event.clientY, stack:event.type === 'blur' ? new Error().stack : undefined});
    if (records.length > 256) records.shift();
  }
  for (const type of ['pointerdown','pointerup','mousedown','mouseup','click','focus','blur','focusin','focusout']) {
    document.addEventListener(type, e => { record(e,'capture'); queueMicrotask(() => record(e,'after')); },true);
    document.addEventListener(type, e => record(e,'bubble'));
  }
});
let stage = 'startup', completed = 0, failed = false;
const wait = (fn,arg) => page.waitForFunction(fn,arg,{timeout:15000});
const state = () => page.evaluate(() => globalThis.dockingScene);
const click = async id => {const r=await readHitBounds(page,id); await page.mouse.click(r.x+Math.min(r.width/2,45),r.y+r.height/2);};
try {
  const address = new URL(process.env.BROWSER_URL || 'http://127.0.0.1:8765/DockingScene/'); address.searchParams.set('diagnostics','1');
  await page.goto(address.href,{waitUntil:'domcontentloaded',timeout:60000});
  await page.waitForFunction(() => globalThis.dockingScene?.ready,null,{timeout:90000});
  await page.locator('.uno-loader').waitFor({state:'hidden',timeout:30000});
  const iterations = Number(process.env.FOCUS_ITERATIONS || 100); assert(Number.isInteger(iterations) && iterations>0 && iterations<=500);
  for (let i=0;i<iterations;i++) {
    stage=`reset-${i}`; await page.keyboard.press('Escape');const generation=(await state()).workspaceGeneration;
    await click('toolbar:Reset');await wait(g=>globalThis.dockingScene.workspaceGeneration>g&&globalThis.dockingScene.activeWindowId==='welcome',generation);
    const sequence=(await state()).sequence;await wait(s=>globalThis.dockingScene.sequence>s+1,sequence);
    stage=`tab-${i}`;await click('tab:code');
    stage=`body-${i}`;await click('editor:code');
    stage=`typing-${i}`;await page.keyboard.press('Control+a');await page.keyboard.type('probe-'+i);
    await wait(text=>globalThis.dockingScene.editors.code.text===text,'probe-'+i);
    stage=`switch-${i}`;await page.keyboard.press('F8');await page.keyboard.press('Enter');
    await wait(()=>!globalThis.dockingScene.switcherInputScoped&&globalThis.dockingScene.activeWindowId==='welcome');
    assert.equal((await state()).editors.code.text,'probe-'+i); completed++;
    console.log('PASS focus cycle '+i);
  }
} catch(error) {failed=true;console.error('FAIL '+stage+': '+error);}
finally {
  await page.screenshot({path:`${output}/focus.png`}).catch(()=>{});
  await writeFile(`${output}/focus.json`,JSON.stringify({stage,completed,failed,diagnostics:await captureDiagnostics(page),
    managedEvents:await page.evaluate(()=>globalThis.dockingSceneManagedFocusTrace ?? []).catch(()=>[]),
    events:await page.evaluate(()=>globalThis.dockingSceneNativeFocusTrace ?? []).catch(()=>[])},null,2));
  await browser.close();
}
if(failed)process.exitCode=1;
