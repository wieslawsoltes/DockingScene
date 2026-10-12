import assert from 'node:assert/strict';
import { pngPixels } from './painted-tabs.mjs';

// Coordinates are read-only model/site observations. Verify both the destination
// caption/content and vacated area, since correct hit geometry can coexist with
// an obsolete renderer picture. No JavaScript redraw or model mutation is used.
export async function assertFloatingPaint(page, site, current, previous) {
  const image = pngPixels(await page.screenshot());
  const scale = image.width / page.viewportSize().width;
  function Pixel(x,y,expected,label) {
    const actual=image.rgb(Math.round((site.x+x)*scale),Math.round((site.y+y)*scale));
    assert(actual.every((v,i)=>Math.abs(v-expected[i])<=3),`${label}: ${actual}, expected ${expected}`);
  }
  Pixel(current.x+current.width-16,current.y+3,[0,122,204],'Moved caption is painted at its new location');
  Pixel(current.x+current.width-8,current.y+70,[255,255,255],'Moved document body fills its new location');
  if(current.x>previous.x+12)
    Pixel(previous.x+5,previous.y+70,[240,240,240],'Vacated document pixels are cleared');
}
