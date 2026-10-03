import {createRequire} from 'node:module';
import {readFile,writeFile,stat} from 'node:fs/promises';
import {dirname,join,parse} from 'node:path';
import {fileURLToPath} from 'node:url';

const require=createRequire(import.meta.url);
const sharp=require(process.argv[2] || 'sharp');
const root=join(dirname(fileURLToPath(import.meta.url)),'../../../docs/assets/workshop');
const manifest=JSON.parse(await readFile(join(root,'manifest.json'),'utf8'));
for(const item of manifest.images){
  const vector=join(root,'.render',parse(item.filename).name+'.svg');
  const destination=join(root,'png',item.filename);
  await sharp(vector,{density:192}).resize({width:item.width}).ensureAlpha()
    .png({compressionLevel:9,palette:false}).toFile(destination);
  const {data,info}=await sharp(destination).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  let minimum=255,maximum=0;
  for(let i=3;i<data.length;i+=info.channels){minimum=Math.min(minimum,data[i]);maximum=Math.max(maximum,data[i]);}
  const corners=[0,info.width-1,(info.height-1)*info.width,info.width*info.height-1].map(i=>data[i*info.channels+3]);
  if(info.channels!==4 || minimum!==0 || maximum!==255 || corners.some(a=>a!==0)){
    throw new Error(`Transparency check failed: ${item.filename}`);
  }
  item.width=info.width;item.height=info.height;
  item.alpha={minimum,maximum,corners};
  item.bytes=(await stat(destination)).size;
}
await writeFile(join(root,'manifest.json'),JSON.stringify(manifest,null,2)+'\n');
console.log('Rendered and verified nine RGBA PNGs: transparent corners, transparent canvas and opaque artwork.');
