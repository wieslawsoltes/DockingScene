import assert from 'node:assert/strict';

// No wait for popup geometry or focus between the opening click and keys.
export async function runSelectorReview({ page, state, test, reset, click, wait, settle }) {
  await test('Native inspector selector handles immediate opening keys for twelve fresh workspaces', async () => {
    for (let iteration = 0; iteration < 12; iteration++) {
      await reset(); await click('toolbar:New'); await settle();
      const s = await state(), id = s.activeWindowId;
      const index = s.inspectorWindowIds.indexOf(id); assert(index >= 0);
      await click('properties:window'); await page.keyboard.press('Home');
      for (let i = 0; i < index; i++) await page.keyboard.press('ArrowDown');
      await page.keyboard.press('Enter');
      await wait(id => globalThis.dockingScene.inspectorWindowId === id, id);
      assert.equal((await state()).activeWindowId, 'properties');
    }
    await reset();
  });
}
