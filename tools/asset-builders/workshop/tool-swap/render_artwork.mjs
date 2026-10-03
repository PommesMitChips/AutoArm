import {createRequire} from 'node:module';
import {readFile} from 'node:fs/promises';
import {dirname,join,parse} from 'node:path';
import {fileURLToPath} from 'node:url';

const require=createRequire(import.meta.url);
const sharp=require(process.argv[2] || 'sharp');
const root=dirname(fileURLToPath(import.meta.url));
const data=JSON.parse(await readFile(join(root,'artwork-manifest.json'),'utf8'));
for(const item of data.artwork){
 const name=parse(item.filename).name;
 await sharp(join(root,'artwork',name+'.svg'),{density:192}).resize({width:1240})
   .ensureAlpha().png({compressionLevel:9,palette:false}).toFile(join(root,'artwork',item.filename));
}
console.log('Rendered transparent ToolSwap artwork.');
