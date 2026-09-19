import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { PNG } from 'pngjs';
import pixelmatch from 'pixelmatch';

export function compare(reference, actual, output, maxRatio = 0) {
  if (!Number.isFinite(maxRatio) || maxRatio < 0 || maxRatio > 1) throw new Error('maxRatio must be 0..1');
  const inputs = [reference, actual].map(p => path.resolve(p).toLowerCase());
  for (const name of ['diff.png', 'overlay.png', 'side-by-side.png', 'result.json']) {
    if (inputs.includes(path.resolve(output, name).toLowerCase())) throw new Error('Output would overwrite an input image');
  }
  const a = PNG.sync.read(fs.readFileSync(reference));
  const b = PNG.sync.read(fs.readFileSync(actual));
  if (a.width !== b.width || a.height !== b.height) throw new Error('Size mismatch: align crop and DPI explicitly; never silently stretch a reference');
  const diff = new PNG({ width: a.width, height: a.height });
  const overlay = new PNG({ width: a.width, height: a.height });
  const side = new PNG({ width: a.width * 2, height: a.height });
  const changed = pixelmatch(a.data, b.data, diff.data, a.width, a.height, { threshold: 0.1, includeAA: false });
  for (let i = 0; i < a.data.length; i++) overlay.data[i] = Math.round((a.data[i] + b.data[i]) / 2);
  PNG.bitblt(a, side, 0, 0, a.width, a.height, 0, 0);
  PNG.bitblt(b, side, 0, 0, b.width, b.height, a.width, 0);
  fs.mkdirSync(output, { recursive: true });
  for (const [name, png] of [['diff', diff], ['overlay', overlay], ['side-by-side', side]]) fs.writeFileSync(path.join(output, `${name}.png`), PNG.sync.write(png));
  const result = { width: a.width, height: a.height, changed, ratio: changed / (a.width * a.height), maxRatio, threshold: 0.1, includeAA: false };
  result.pass = result.ratio <= maxRatio;
  fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify(result, null, 2));
  return result;
}
if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  try {
    if (process.argv.length < 5 || process.argv.length > 6) throw new Error('Usage: node compare.mjs reference.png actual.png output-directory [maxRatio=0]');
    const result = compare(...process.argv.slice(2, 5), Number(process.argv[5] ?? 0));
    console.log(JSON.stringify(result));
    process.exitCode = result.pass ? 0 : 1;
  } catch (e) { console.error(e.message); process.exitCode = 2; }
}
