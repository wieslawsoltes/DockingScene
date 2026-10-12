import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import vm from 'node:vm';

const source = await readFile(new URL('../../src/DockingScene.Controls/WasmScripts/DockingScene.CanvasFocus.js', import.meta.url), 'utf8');
class DocumentFixture {
  handlers = new Map(); activeElement;
  addEventListener(type, handler) { const set = this.handlers.get(type) ?? new Set(); set.add(handler); this.handlers.set(type, set); }
  removeEventListener(type, handler) { this.handlers.get(type)?.delete(handler); }
  emit(type, overrides = {}) {
    const e = { isTrusted: true, defaultPrevented: false, cancelable: true, button: 0, detail: 1,
      target: { tagName: 'CANVAS', id: 'uno-canvas', ownerDocument: this },
      preventDefault() { this.defaultPrevented = true; },
      stopPropagation() { throw new Error('Propagation must not be stopped'); }, ...overrides };
    for (const h of this.handlers.get(type) ?? []) h(e);
    return e;
  }
  input(tagName = 'TEXTAREA') { return {tagName, id: 'uno-input', ownerDocument: this, isConnected: true}; }
}
const doc = new DocumentFixture(), context = vm.createContext({ document: doc });
vm.runInContext(source, context); const api = context.DockingSceneCanvasFocus;
assert.equal(api.version, 1); const registration = api.install(doc);
for (const tag of ['INPUT', 'TEXTAREA']) {
  doc.activeElement = doc.input(tag);
  for (const type of ['mousedown', 'click']) assert.equal(doc.emit(type).defaultPrevented, true);
}
assert.equal(registration.preventedCount, 4);
const attempts = [
  { isTrusted: false }, { cancelable: false }, { button: 2 }, { detail: 0 },
  { target: {tagName: 'BUTTON', id: 'outside', ownerDocument: doc} },
  { target: {tagName: 'CANVAS', id: 'another-canvas', ownerDocument: doc} },
  { target: {tagName: 'CANVAS', id: 'uno-canvas', ownerDocument: {}} }
];
for (const attempt of attempts) assert.equal(doc.emit('mousedown', attempt).defaultPrevented, false);
for (const input of [null, {...doc.input(), id: 'native-editor'}, {...doc.input(), isConnected: false}, {...doc.input(), tagName: 'DIV'}, {...doc.input(), ownerDocument: {}}]) {
  doc.activeElement = input; assert.equal(doc.emit('mousedown').defaultPrevented, false);
}
assert.equal(api.install(doc), registration); vm.runInContext(source, context);
assert.equal(doc.handlers.get('mousedown').size, 1); assert.equal(registration.preventedCount, 4);
registration.dispose(); registration.dispose(); assert.equal(doc.handlers.get('click').size, 0);
doc.activeElement = doc.input(); assert.equal(doc.emit('mousedown').defaultPrevented, false);
const next = api.install(doc); assert.notEqual(next, registration); assert.equal(doc.emit('mousedown').defaultPrevented, true);
assert.equal(next.preventedCount, 1);
console.log('PASS canvas focus boundary: input types, trusted pointer scope, native controls, separate documents, idempotence and disposal');
