// Export the selected imagegen compositions at thumbnail resolution.
// Full generated masters are preserved in sources/ without pixel edits.
import {createRequire} from 'node:module';
import {dirname,join} from 'node:path';
import {fileURLToPath} from 'node:url';
const require=createRequire(import.meta.url);
const sharp=require(process.argv[2] || 'sharp');
const root=dirname(fileURLToPath(import.meta.url));
for(const name of ['autoarm','toolswap']){
 const master=name==='toolswap'?'toolswap-thumbnail-perspective-master.png':name+'-thumbnail-master.png';
 await sharp(join(root,'sources',master))
  .resize(512,512,{fit:'inside',kernel:'lanczos3'})
  .png({compressionLevel:9,adaptiveFiltering:true})
  .toFile(join(root,'branding',name+'-thumbnail.png'));
}
console.log('Exported two 512px square PNG thumbnails; generated masters preserved.');
