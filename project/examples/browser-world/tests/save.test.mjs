import test from 'node:test';
import assert from 'node:assert/strict';
import {EventEmitter} from 'node:events';
import {mkdtemp,mkdir,writeFile,readFile,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';
import {loadConfigFromFile} from 'vite';
const loaded=await loadConfigFromFile({command:'serve',mode:'development'},resolve('vite.config.ts'));
const cases=[
 ['invalid assets', {sceneEdits:['changed'],editorAssets:{assets:'invalid',instances:[]}},500,['original'],{assets:[],instances:[]}],
 ['null assets', {sceneEdits:['changed'],editorAssets:null},500,['original'],{assets:[],instances:[]}],
 ['legacy scene-only payload', ['changed'],200,['changed'],{assets:[],instances:[]}],
 ['combined payload', {sceneEdits:['changed'],editorAssets:{assets:[{id:'sample'}],instances:[]}},200,['changed'],{assets:[{id:'sample'}],instances:[]}],
];
for(const [name,payload,status,scene,assets] of cases) {
 test(name,async()=>{
  const cwd=process.cwd();
  const dir=await mkdtemp(join(tmpdir(),'critter-save-'));
  try {
   await mkdir(join(dir,'src/data'),{recursive:true});
   await writeFile(join(dir,'src/data/scene-edits.json'),'["original"]');
   await writeFile(join(dir,'src/data/editor-assets.json'),'{"assets":[],"instances":[]}');
   process.chdir(dir);
   const handlers=new Map();
   loaded.config.plugins[0].configureServer({middlewares:{use:(path,fn)=>handlers.set(path,fn)}});
   const req=new EventEmitter();req.method='POST';req.setEncoding=()=>{};
   const result=await new Promise(done=>{
    const res={setHeader(){},end(body){done({status:this.statusCode,body:JSON.parse(body)});}};
    handlers.get('/__cc/save-scene-edits')(req,res,()=>assert.fail('Unexpected fallthrough'));
    req.emit('data',JSON.stringify(payload));req.emit('end');
   });
   assert.equal(result.status,status);
   assert.equal(result.body.ok,status===200);
   // Let any incorrectly started writes settle before checking rejection integrity.
   await new Promise(done=>setTimeout(done,50));
   assert.deepEqual(JSON.parse(await readFile(join(dir,'src/data/scene-edits.json'),'utf8')),scene);
   assert.deepEqual(JSON.parse(await readFile(join(dir,'src/data/editor-assets.json'),'utf8')),assets);
  } finally {
   process.chdir(cwd);await rm(dir,{recursive:true,force:true});
  }
 });
}
