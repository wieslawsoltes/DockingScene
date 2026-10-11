import assert from 'node:assert/strict';
import { assertPaintedHostTabs } from './painted-tabs.mjs';

/** Real control workloads and input regressions. No JavaScript model mutation. */
export async function runPerformanceRegressions({ page, state, strip, test, wait, settle, bounds, click, setting, mode }) {
  const measurements = [];
  const remember = async label => {
    const s = await strip();
    measurements.push({ label, count: s.count, realized: s.realized, textMeasurements: s.textMeasurements,
      layoutPasses: s.layoutPasses, headersCreated: s.headersCreated, workloadMs: (await state()).tabWorkloadMs });
  };
  const order = async () => (await strip()).items.map(i => i.id);
  async function drag(source, target, { cancel = false, disabled = false } = {}) {
    const r = await bounds('tab:' + source), t = await bounds('tab:' + target);
    await page.mouse.move(r.x + r.width / 2, r.y + r.height / 2); await page.mouse.down();
    try {
      if (!disabled) await wait(() => globalThis.dockingScene.tabStrips.find(t => t.id === 'playground').dragActive);
      await page.mouse.move(t.x + 2, t.y + t.height / 2, { steps: 12 });
      if (cancel) await page.keyboard.press('Escape');
    } finally { await page.mouse.up(); }
    await wait(() => !globalThis.dockingScene.tabStrips.find(t => t.id === 'playground').dragActive); await settle();
  }
  await test('2,002-tab bulk load measures each new header only once', async () => {
    await setting('Open standalone tab playground');
    await assertPaintedHostTabs(page);
    await click('play:measured'); await mode('ScrollWithMenu'); await settle();
    const before = await strip(); await click('perf:large');
    await wait(() => globalThis.dockingScene.tabStrips.find(t => t.id === 'playground').count === 2002); await settle();
    const after = await strip();
    assert.equal(after.textMeasurements - before.textMeasurements, 2002);
    assert(after.realized > 0 && after.realized < 50);
    assert.equal(after.items.filter(i => i.contentCreated).length, 1);
    await remember('load-2002');
  });
  await test('200 selections reuse all header measurements and allocation plans', async () => {
    const before = await strip(); await click('perf:select');
    await wait(() => globalThis.dockingScene.tabStrips.find(t => t.id === 'playground').selectedId === 'play-1781'); await settle();
    const after = await strip();
    assert.equal(after.textMeasurements, before.textMeasurements);
    assert.equal(after.layoutPasses, before.layoutPasses);
    assert(after.headersCreated - before.headersCreated < 50,
      'Unpainted intermediate selections should not construct hundreds of headers.');
    assert(after.realized < 50);
    assert.equal(after.items.filter(i => i.contentCreated).length, 200);
    await remember('select-200');
  });
  await test('Renaming one of 2,002 tabs measures only that header', async () => {
    const before = await strip(), id = before.selectedId, instance = before.items.find(i => i.id === id).contentInstance;
    await click('perf:rename'); await settle(); const after = await strip();
    assert.equal(after.textMeasurements, before.textMeasurements + 1);
    assert.equal(after.items.find(i => i.id === id).contentInstance, instance);
    await bounds('tab:' + id); await remember('rename-one');
  });
  await test('Same-id item replacement presents the new content instance', async () => {
    const before = await strip(), id = before.selectedId, instance = before.items.find(i => i.id === id).contentInstance;
    await click('perf:replace');
    await wait(({id, instance}) => { const s = globalThis.dockingScene.tabStrips.find(t => t.id === 'playground'); const item = s.items.find(i => i.id === id); return s.selectedId === id && item.contentCreated && item.contentInstance !== instance; }, {id, instance});
    await settle(); const after = await strip(); assert.equal(after.textMeasurements, before.textMeasurements + 1);
    await bounds('play-editor:' + id); await remember('replace-one');
  });
  await test('Standalone pointer reordering retains content and avoids remeasurement', async () => {
    await click('perf:small'); await click('play:compact'); await mode('ShrinkWithMenu'); await settle();
    await click('tab:play-2'); await settle(); const before = await strip();
    const instance = before.items.find(i => i.id === 'play-2').contentInstance, count = (await state()).tabReorders;
    await drag('play-2', 'play-0'); const after = await strip(); const ids = await order();
    assert(ids.indexOf('play-2') < ids.indexOf('play-0')); assert.equal((await state()).tabReorders, count + 1);
    assert.equal(after.items.find(i => i.id === 'play-2').contentInstance, instance);
    assert.equal(after.textMeasurements, before.textMeasurements);
  });
  await test('Standalone reorder veto rejects the whole move', async () => {
    await click('perf:veto'); await settle(); assert.equal((await state()).tabReorderVeto, true);
    const before = await order(), s = await state(); await drag('play-3', 'play-0');
    assert.deepEqual(await order(), before); assert.equal((await state()).tabReorders, s.tabReorders);
    assert.equal((await state()).tabReorderRequests, s.tabReorderRequests + 1);
    await click('perf:veto'); await settle();
  });
  await test('CanTabsDrag false disables standalone gesture capture and mutation', async () => {
    await click('perf:drag'); await settle(); assert.equal((await strip()).canDrag, false);
    const before = await order(), count = (await state()).tabReorderRequests;
    await drag('play-3', 'play-0', {disabled: true});
    assert.deepEqual(await order(), before); assert.equal((await state()).tabReorderRequests, count);
    await click('perf:drag'); await settle();
  });
  await test('Escape cancels standalone reordering without moving an item', async () => {
    const before = await order(), count = (await state()).tabReorders;
    await drag('play-3', 'play-0', {cancel: true});
    assert.deepEqual(await order(), before); assert.equal((await state()).tabReorders, count);
  });
  await test('Default content ownership retains close/open and releases without disposal', async () => {
    await click('perf:lifetime'); await bounds('lifetime:editor'); await settle();
    const before = await state(); assert.equal(before.contentCreations, 1); assert.equal(before.ownsContent, false);
    await click('lifetime:close'); await wait(() => !globalThis.dockingScene.bounds['lifetime:editor']);
    await click('lifetime:open'); await bounds('lifetime:editor'); await settle();
    let after = await state(); assert.equal(after.trackedContent, before.trackedContent); assert.equal(after.contentCreations, 1); assert.equal(after.contentDisposals, 0);
    await click('lifetime:unregister'); await wait(() => globalThis.dockingScene.contentReleases === 1);
    after = await state(); assert.equal(after.contentDisposals, 0); assert.equal(after.bounds['lifetime:editor'], undefined);
  });
  await test('Explicit content ownership disposes once on unregister and manager replacement', async () => {
    await click('lifetime:register'); await bounds('lifetime:editor'); await settle();
    assert.equal((await state()).contentCreations, 2);
    await click('lifetime:ownership'); await wait(() => globalThis.dockingScene.ownsContent);
    await click('lifetime:unregister'); await wait(() => globalThis.dockingScene.contentDisposals === 1 && globalThis.dockingScene.contentReleases === 2);
    await click('lifetime:register'); await bounds('lifetime:editor'); await settle();
    const before = await state(); await click('lifetime:manager');
    await wait(() => globalThis.dockingScene.contentDisposals === 2 && globalThis.dockingScene.contentReleases === 3 && globalThis.dockingScene.contentCreations === 4);
    await bounds('lifetime:editor'); assert.notEqual((await state()).trackedContent, before.trackedContent);
  });
  await test('Coalesced unregister and same-id registration cannot reuse released content', async () => {
    const before = await state(); await click('lifetime:recreate');
    await wait(before => globalThis.dockingScene.contentCreations === before.contentCreations + 1 &&
      globalThis.dockingScene.contentReleases === before.contentReleases + 1 &&
      globalThis.dockingScene.contentDisposals === before.contentDisposals + 1, before);
    await bounds('lifetime:editor'); assert.notEqual((await state()).trackedContent, before.trackedContent);
  });
  await test('Nested factory selection wins over the obsolete outer request', async () => {
    await setting('Open standalone tab playground'); await click('review:reentrant'); await settle();
    assert.equal((await strip()).selectedId, 'callback-target');
    assert.equal((await state()).factoryCallbackCount, 1); await bounds('review:target-content');
  });
  await test('A failed factory preserves the previous selection and content', async () => {
    await click('review:failure'); await settle();
    assert.equal((await state()).factoryFailureRetained, true);
    assert.equal((await strip()).selectedId, 'failure-target'); await bounds('review:retained-content');
  });
  await test('Recursive content creation fails without stack recursion', async () => {
    await click('review:recursive'); await settle();
    assert.equal((await state()).recursiveFactoryRejected, true);
  });
  console.log('PERFORMANCE_OBSERVATIONS ' + JSON.stringify(measurements));
  return measurements;
}
