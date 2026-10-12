import assert from 'node:assert/strict';
import './canvas-focus.contract.mjs';

export async function runCanvasFocusReview({page, state, test, reset, click, wait}) {
  await test('Packaged canvas focus boundary loads in the actual Uno application', async () => {
    assert.equal(await page.evaluate(() => globalThis.DockingSceneCanvasFocus?.version), 1);
  });
  await test('Rapid editor-body clicks retain native text entry through repeated workspace resets', async () => {
    for (let iteration = 0; iteration < 24; iteration++) {
      await reset(); await click('tab:code'); await click('editor:code');
      await page.keyboard.press('Control+a'); await page.keyboard.type('canvas-focus-' + iteration);
      await wait(text => globalThis.dockingScene.editors.code.text === text, 'canvas-focus-' + iteration);
      await click('editor:code'); await page.keyboard.press('Control+a'); await page.keyboard.type('second-click-' + iteration);
      await wait(text => globalThis.dockingScene.editors.code.text === text, 'second-click-' + iteration);
    }
    assert.equal((await state()).focusedId, 'editor:code');
  });
}
