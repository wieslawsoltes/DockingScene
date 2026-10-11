import assert from 'node:assert/strict';

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
}
