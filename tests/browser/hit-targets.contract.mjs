import assert from 'node:assert/strict';
import { readHitBounds } from './hit-targets.mjs';

/** Pure Node fixtures, not the browser app: reproduce a snapshot replacement
 * between predicate completion and protocol serialization of its result. */
export async function validateHitTargetReader() {
  const descriptor = Object.getOwnPropertyDescriptor(globalThis, 'dockingScene');
  const original = { x: 10.5, y: 20, width: 88, height: 24 };
  let disposed = 0, checked = 0;
  const page = (states, afterCapture = () => {}, failJson = false) => ({
    async waitForFunction(predicate, id, options) {
      assert.equal(id, 'target'); assert.equal(options.timeout, 15000);
      let found;
      for (const state of states) {
        globalThis.dockingScene = state;
        const result = predicate(id);
        if (result) { found = result; break; }
        checked++;
      }
      if (!found) throw new Error('Fixture timeout');
      afterCapture();
      return {
        async jsonValue() { if (failJson) throw new Error('Fixture serialization failure'); return found; },
        async dispose() { disposed++; },
      };
    },
  });
  try {
    const replacing = page([{ bounds: { target: { ...original } } }], () => {
      globalThis.dockingScene = { bounds: {} };
    });
    assert.deepEqual(await readHitBounds(replacing, 'target'), original);
    assert.equal(globalThis.dockingScene.bounds.target, undefined);

    const mutable = { ...original };
    assert.deepEqual(await readHitBounds(page([{ bounds: { target: mutable } }], () => {
      mutable.x = 500; mutable.width = 0;
    }), 'target'), original);

    const invalid = [null, {}, { width: 0 }, { ...original, width: 0 },
      { ...original, height: -1 }, { ...original, x: NaN },
      { ...original, y: Infinity }, { ...original, width: '88' }];
    const invalidStates = invalid.map(target => ({ bounds: { target } }));
    invalidStates.push({ bounds: { target: { ...original } } });
    assert.deepEqual(await readHitBounds(page(invalidStates), 'target'), original);
    assert.equal(checked, invalid.length);

    await assert.rejects(() => readHitBounds(page([{ bounds: {} }]), 'target'), /Fixture timeout/);
    await assert.rejects(() => readHitBounds(page([{ bounds: { target: original } }], undefined, true), 'target'), /Fixture serialization failure/);
    await assert.rejects(() => readHitBounds({}, ''), /nonempty automation id/);
    await assert.rejects(() => readHitBounds({}, null), /nonempty automation id/);
    assert.equal(disposed, 4, 'Every acquired handle must be released, including failed serialization');
    console.log('PASS hit-target reader contracts: snapshot replacement, source mutation, invalid geometry, timeout, disposal and argument validation');
  } finally {
    if (descriptor) Object.defineProperty(globalThis, 'dockingScene', descriptor);
    else delete globalThis.dockingScene;
  }
}
