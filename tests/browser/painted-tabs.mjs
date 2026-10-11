import assert from 'node:assert/strict';
import { inflateSync } from 'node:zlib';
import { readHitBounds } from './hit-targets.mjs';

/** Read the RGB/RGBA PNG screenshots produced by Chromium; no DOM color substitute. */
export function pngPixels(png) {
  assert(png.subarray(0, 8).equals(Buffer.from([137,80,78,71,13,10,26,10])), 'Invalid PNG signature');
  let width, height, channels; const chunks = [];
  for (let offset = 8; offset + 12 <= png.length;) {
    const length = png.readUInt32BE(offset), end = offset + length + 12;
    assert(end <= png.length, 'Truncated PNG chunk');
    const kind = png.toString('ascii', offset + 4, offset + 8), data = png.subarray(offset + 8, end - 4);
    if (kind === 'IHDR') {
      width = data.readUInt32BE(0); height = data.readUInt32BE(4);
      assert(data[8] === 8 && [2, 6].includes(data[9]) && data[10] === 0 && data[11] === 0 && data[12] === 0, 'Expected non-interlaced 8-bit RGB/RGBA');
      channels = data[9] === 2 ? 3 : 4;
    } else if (kind === 'IDAT') chunks.push(data);
    else if (kind === 'IEND') break;
    offset = end;
  }
  assert(width > 0 && height > 0 && width * height <= 16000000 && channels, 'Invalid screenshot dimensions');
  const stride = width * channels, expected = (stride + 1) * height;
  const raw = inflateSync(Buffer.concat(chunks), { maxOutputLength: expected });
  assert.equal(raw.length, expected);
  const pixels = Buffer.alloc(stride * height);
  function paeth(a, b, c) {
    const p = a + b - c, da = Math.abs(p - a), db = Math.abs(p - b), dc = Math.abs(p - c);
    return da <= db && da <= dc ? a : db <= dc ? b : c;
  }
  for (let y = 0; y < height; y++) {
    const source = y * (stride + 1), target = y * stride, filter = raw[source];
    assert(filter <= 4, 'Unknown PNG row filter');
    for (let x = 0; x < stride; x++) {
      const a = x >= channels ? pixels[target + x - channels] : 0;
      const b = y > 0 ? pixels[target + x - stride] : 0;
      const c = y > 0 && x >= channels ? pixels[target + x - stride - channels] : 0;
      const prediction = filter === 0 ? 0 : filter === 1 ? a : filter === 2 ? b : filter === 3 ? Math.floor((a + b) / 2) : paeth(a, b, c);
      pixels[target + x] = (raw[source + x + 1] + prediction) & 255;
    }
  }
  return { width, height, rgb(x, y) {
    assert(Number.isInteger(x) && Number.isInteger(y) && x >= 0 && y >= 0 && x < width && y < height);
    const offset = y * stride + x * channels; return [...pixels.subarray(offset, offset + 3)];
  } };
}

export async function assertPaintedHostTabs(page) {
  const active = await readHitBounds(page, 'tab:tab-playground');
  const inactive = await readHitBounds(page, 'tab:welcome');
  const image = pngPixels(await page.screenshot());
  const scale = image.width / page.viewportSize().width;
  for (const [name, rect, expected] of [['active', active, [0,122,204]], ['inactive', inactive, [231,231,231]]]) {
    const actual = image.rgb(Math.round((rect.x + 4) * scale), Math.round((rect.y + 3) * scale));
    assert(actual.every((channel, i) => Math.abs(channel - expected[i]) <= 3),
      `${name} host tab paint ${actual} does not match Metro Light ${expected}`);
  }
}
