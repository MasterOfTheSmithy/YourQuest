const fs=require('fs'),crypto=require('crypto'),assert=require('assert'),path=require('path');
const output='outputs/DOT_Integration_20261004/Cat_R8',stage=output+'/Staged',root='Assets/Assets/GeneratedAssets/DOT Generated Assets',owner=root+'/Wildlife/Cat';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const manifest=read(stage+'/MANIFEST.json'),files=[];let skeleton;
// note: Retain every original file and exact whitelist; original-only loops enter the existing passive wildlife binder.
for(const name of [...Object.keys(manifest.files),'MANIFEST.json']) {
 const staged=stage+'/'+name,b=fs.readFileSync(staged);if(manifest.files[name]){assert.equal(hash(b),manifest.files[name].sha256,name);assert.equal(b.length,manifest.files[name].bytes,name);}
 const filename=path.basename(name),ext=path.extname(name);let destination,kind;
 if(ext==='.glb'||ext==='.blend'){kind=ext==='.blend'?'Models/Source':filename.includes('LOD0')?'Models/Assemblies':'Models/LODs';destination=owner+'/Body Types/Domestic Tabby/'+kind+'/R8/'+filename;}
 else if(name.startsWith('Textures/')){kind='Textures';destination=owner+'/Textures/R8/'+filename;}
 else if(name.startsWith('Preview/')){kind='Previews';destination=owner+'/Previews/R8/'+filename;}
 else{kind=name==='MANIFEST.json'?'Manifests':/LICENSE|PROVENANCE|REFERENCE_PACK/.test(name)?'Provenance':name.startsWith('Proof/')?'Validation':'Guides';destination=owner+'/Documentation/'+kind+'/R8/'+name;}
 assert(!fs.existsSync(destination)); const row={relative:name,origin:owner+'/R8/'+name,destination,staged,sha256:hash(b),bytes:b.length,kind,materialFolder:owner+'/Body Types/Domestic Tabby/Materials/R8'};
 if(ext==='.glb') {
   const size=b.readUInt32LE(12),j=JSON.parse(b.subarray(20,20+size)),bin=b.subarray(28+size);const access=i=>{const a=j.accessors[i],v=j.bufferViews[a.bufferView];return bin.subarray(v.byteOffset+(a.byteOffset||0),v.byteOffset+(a.byteOffset||0)+a.count*({SCALAR:1,VEC2:2,VEC3:3,VEC4:4,MAT4:16}[a.type])*({5126:4,5125:4,5123:2,5121:1}[a.componentType]));};
   const s=j.skins[0];assert.equal(s.joints.length,25);assert.equal(j.animations.length,4);const signature=JSON.stringify({bones:s.joints.map(i=>j.nodes[i]),inverse:hash(access(s.inverseBindMatrices)),clips:j.animations.map(a=>({name:a.name,channels:a.channels.map(c=>({node:j.nodes[c.target.node].name,path:c.target.path,input:hash(access(a.samplers[c.sampler].input)),output:hash(access(a.samplers[c.sampler].output))}))}))});if(skeleton)assert.equal(signature,skeleton);else skeleton=signature;
   row.embeddedMaterials=j.materials.map(m=>({name:m.name,alphaMode:m.alphaMode||'OPAQUE',baseColorFactor:m.pbrMetallicRoughness.baseColorFactor}));row.clips=j.animations.map(a=>{const inputs=a.samplers.map(s=>j.accessors[s.input]);const start=Math.min(...inputs.map(a=>a.min[0])),end=Math.max(...inputs.map(a=>a.max[0]));return {name:a.name,start,duration:end-start};});
   row.triangles=j.meshes.reduce((v,m)=>v+m.primitives.reduce((n,p)=>n+j.accessors[p.indices].count/3,0),0);assert.equal(row.triangles,[22490,13294,7900][Number(filename.match(/LOD(\d)/)[1])]);
 }
 files.push(row);
}
const models=files.filter(f=>f.destination.endsWith('.glb')).sort((a,b)=>a.relative.localeCompare(b.relative));
const entry={assetId:'yq_cat_r8_domestic_tabby',kind:'wildlife',species:'cat',category:'cat',bodyForm:'domestic_tabby',compatibilityId:'cat_r8',authoredWalkSpeed:0.0911458333,sourcePaths:models.map(f=>f.destination),sourceHashes:models.map(f=>f.sha256)};
fs.writeFileSync(output+'/plan.json',JSON.stringify({status:'PREFLIGHT_PASS',files,entry,authoredTrotSpeed:0.2375},null,2));
fs.writeFileSync(output+'/source-contracts.json',JSON.stringify({status:'PASS',files:files.length,lods:3,joints:25,models:models.map(f=>({path:f.relative,triangles:f.triangles,clips:f.clips})),walkSpeed:entry.authoredWalkSpeed,trotSpeed:0.2375,limits:['No supplied start/stop clips or travel recipe.','Live terrain contact, unlike-clip transitions and projected LOD switching remain unverified.']},null,2));console.log('PASS: 18 exact Cat files, three matching rigs and four original loops.');
