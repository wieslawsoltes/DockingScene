import assert from 'node:assert/strict';
import { pngPixels } from './painted-tabs.mjs';

// Count real rendered glyph ink inside content, excluding borders and captions.
// Model text and foreground properties alone cannot detect white-on-white paint.
export async function assertPaintedContentText(page, bounds, dark) {
  const regions = [];
  for (const [id, maxWidth, maxHeight] of [
    ['editor:code', 200, 48], ['source-tree-text:DockManager.cs', 180, 22], ['properties:title', 180, 25]
  ]) {
    const r = await bounds(id);
    const inset = id.startsWith('source-tree-text:') ? 1 : 5;
    regions.push({id, x:r.x+inset, y:r.y+inset, width:Math.min(maxWidth,r.width)-2*inset, height:Math.min(maxHeight,r.height)-2*inset});
  }
  const image = pngPixels(await page.screenshot());
  const scale = image.width/page.viewportSize().width;
  for (const r of regions) {
    let ink = 0;
    for (let y=Math.ceil(r.y*scale); y<Math.floor((r.y+r.height)*scale); y++)
      for (let x=Math.ceil(r.x*scale); x<Math.floor((r.x+r.width)*scale); x++) {
        const rgb=image.rgb(x,y), lo=Math.min(...rgb), hi=Math.max(...rgb);
        if (hi-lo < 30 && (dark ? lo > 185 : hi < 140)) ink++;
      }
    assert(ink >= 25, `${r.id} has no readable ${dark?'light':'dark'} glyph ink: ${ink} pixels`);
  }
}
