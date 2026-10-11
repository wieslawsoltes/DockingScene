/** Resolve and copy geometry in the same diagnostic tick that satisfies the wait.
 * This is observational only: it never writes to the application's model or DOM.
 */
export async function readHitBounds(page, id, timeout = 15000) {
  if (typeof id !== 'string' || !id.length) throw new TypeError('A nonempty automation id is required.');
  const handle = await page.waitForFunction(id => {
    const r = globalThis.dockingScene?.bounds?.[id];
    if (!r || !Number.isFinite(r.x) || !Number.isFinite(r.y) ||
        !Number.isFinite(r.width) || !Number.isFinite(r.height) || r.width <= 0 || r.height <= 0) return null;
    return { x: r.x, y: r.y, width: r.width, height: r.height };
  }, id, { timeout });
  try { return await handle.jsonValue(); }
  finally { await handle.dispose(); }
}
