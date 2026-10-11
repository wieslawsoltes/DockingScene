import assert from 'node:assert/strict';
import { readHitBounds } from './hit-targets.mjs';

/** Actual input regressions; no focus mutation hook or readiness delay before keys. */
export async function runSwitcherRegressions({ page, state, test, reset, click, wait }) {
  await test('Fast Enter and Escape are scoped before switcher focus settles', async () => {
    for (let iteration = 0; iteration < 6; iteration++) {
      await reset();
      await click('toolbar:Windows');
      // Deliberately do not wait for overlay geometry or keyboard focus.
      await page.keyboard.press('Enter');
      await wait(() => !globalThis.dockingScene.bounds['window-switcher'] &&
        !globalThis.dockingScene.switcherInputScoped && globalThis.dockingScene.activeWindowId === 'output');
      await reset();
      await click('toolbar:Windows');
      await page.keyboard.press('Escape');
      await wait(() => !globalThis.dockingScene.bounds['window-switcher'] &&
        !globalThis.dockingScene.switcherInputScoped && globalThis.dockingScene.activeWindowId === 'welcome');
    }
  });
  await test('Modal arrows move exactly once and cancellation detaches the scope', async () => {
    await reset(); await click('toolbar:Windows');
    await wait(() => globalThis.dockingScene.switcherInputScoped && globalThis.dockingScene.switcherSelection === 'output');
    await page.keyboard.press('ArrowDown');
    await wait(() => globalThis.dockingScene.switcherSelection === 'explorer');
    await page.keyboard.press('ArrowUp');
    await wait(() => globalThis.dockingScene.switcherSelection === 'output');
    await page.keyboard.press('Escape');
    await wait(() => !globalThis.dockingScene.switcherInputScoped && !globalThis.dockingScene.bounds['window-switcher']);
    assert.equal((await state()).activeWindowId, 'welcome');
  });
  await test('Dismissed switcher does not consume editor Enter or edit a background document', async () => {
    await reset(); await click('tab:code');
    await wait(() => globalThis.dockingScene.activeWindowId === 'code' && globalThis.dockingScene.editors.code);
    await click('editor:code'); await page.keyboard.press('Control+a'); await page.keyboard.type('first');
    await page.keyboard.press('Enter'); await page.keyboard.type('second');
    await wait(() => globalThis.dockingScene.editors.code.text.replace(/\r\n?|\n/g, '\n') === 'first\nsecond');
    const before = (await state()).editors.code.text;
    await page.keyboard.press('F8');
    await page.keyboard.press('Enter');
    await wait(() => !globalThis.dockingScene.switcherInputScoped &&
      !globalThis.dockingScene.bounds['window-switcher'] && globalThis.dockingScene.activeWindowId === 'welcome');
    assert.equal((await state()).editors.code.text, before);
  });
  await test('Enter character dispatch stays inside the modal scope until release', async () => {
    await reset(); await click('tab:code'); await click('editor:code');
    await page.keyboard.press('Control+a'); await page.keyboard.type('unchanged');
    await wait(() => globalThis.dockingScene.editors.code.text === 'unchanged');
    await page.keyboard.press('F8'); await page.keyboard.down('Enter');
    try {
      await wait(() => globalThis.dockingScene.switcherInputScoped && globalThis.dockingScene.bounds['window-switcher']);
      assert.equal((await state()).switcherHasInputFocus, true);
      assert.equal((await state()).editors.code.text, 'unchanged');
    } finally { await page.keyboard.up('Enter'); }
    await wait(() => !globalThis.dockingScene.switcherInputScoped && globalThis.dockingScene.activeWindowId === 'welcome');
    assert.equal((await state()).editors.code.text, 'unchanged');
  });

  await test('Immediate editor-to-switcher confirmation never edits the source', async () => {
    for (let iteration = 0; iteration < 12; iteration++) {
      await reset(); await click('tab:code'); await click('editor:code');
      await page.keyboard.press('Control+a'); await page.keyboard.type('retained-' + iteration);
      await wait(text => globalThis.dockingScene.editors.code.text === text, 'retained-' + iteration);
      await page.keyboard.press('F8');
      // No overlay, Loaded, focus, or render wait before the confirmation key.
      await page.keyboard.press('Enter');
      await wait(() => !globalThis.dockingScene.switcherInputScoped && globalThis.dockingScene.activeWindowId === 'welcome');
      assert.equal((await state()).editors.code.text, 'retained-' + iteration);
    }
  });

  await test('A layout-changing shortcut cancels the active docking gesture', async () => {
    await reset();
    const source = await readHitBounds(page, 'tab:code');
    const order = (await state()).groups.find(g => g.windows.includes('code')).windows;
    await page.mouse.move(source.x + 20, source.y + source.height / 2); await page.mouse.down();
    try {
      await wait(() => globalThis.dockingScene.interactionActive && globalThis.dockingScene.capturedPointerCount === 1);
      await page.mouse.move(520, 330, { steps: 8 });
      await page.keyboard.press('Control+1');
      await wait(() => !globalThis.dockingScene.interactionActive && globalThis.dockingScene.capturedPointerCount === 0);
    } finally { await page.mouse.up(); }
    assert.equal((await state()).floatingHosts.length, 0);
    assert.deepEqual((await state()).groups.find(g => g.windows.includes('code')).windows, order);
  });

}
