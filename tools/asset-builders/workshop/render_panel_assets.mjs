import {createRequire} from 'node:module';
import {readFile} from 'node:fs/promises';
import {dirname, join} from 'node:path';
import {fileURLToPath} from 'node:url';

const require = createRequire(import.meta.url);
const sharp = require(process.argv[2] || 'sharp');
const root = join(dirname(fileURLToPath(import.meta.url)), '../../../docs/assets/workshop/.panel-assets');
const tokens = JSON.parse(await readFile(join(root, 'assets.json'), 'utf8'));
for (const token of tokens) {
  await sharp(join(root, token + '.svg'), {density:192}).resize({width:1240})
    .ensureAlpha().png({compressionLevel:9,palette:false}).toFile(join(root, token + '.png'));
}
console.log('Rendered original-palette artwork with transparent canvas.');
