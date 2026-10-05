const fs=require('fs'),path=require('path'),crypto=require('crypto'),assert=require('assert');
const output='outputs/DOT_Integration_20261004/Wildlife',root='Assets/Assets/GeneratedAssets/DOT Generated Assets';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const specs={goat:{manifest:'release_manifest.json',contract:'animation/G18_RIG08_clip_contract.json',triangles:[48352,28014,20517],bones:26},dog:{manifest:'PACKAGE_MANIFEST.json',contract:'Validation/B14_clip_contract.json',triangles:[34472,16546,6894],bones:25},bear:{manifest:'Proof/RELEASE_MANIFEST.json',triangles:[17968,10062,4850],bones:21},sheep:{manifest:'Payload_Manifest.json',contract:'Integration/S12_clip_contract.json',triangles:[30886,13086,4470],bones:21},boar:{manifest:'RELEASE_MANIFEST.json',contract:'validation/NEW_BOAR_R34_authoring_contract.json',triangles:[48820,21652,8058],bones:23}};
const files=[],bodies=[],checks=[];
// note: Source proof is archive data. Verify delivered hashes and complete skeletal contracts without promoting external approval to Unity acceptance.
for(const pack of read(output+'/archive-inventory.json')){
 const spec=specs[pack.species],stage=output+'/Staged/'+pack.species,manifest=read(stage+'/'+spec.manifest);
 const declared=pack.species==='sheep'?[...manifest.payloads,...manifest.supporting_files]:manifest.files||manifest.entries;
 for(const row of declared){let p=row.path||row.archive_path||row.member;if(!fs.existsSync(stage+'/'+p))p=p.slice(p.indexOf('/')+1);const b=fs.readFileSync(stage+'/'+p);assert.equal(hash(b),row.sha256,p);assert.equal(b.length,row.bytes,p);}checks.push(pack.species+': exact release manifest hashes');
 const owner=root+'/Wildlife/'+pack.species[0].toUpperCase()+pack.species.slice(1),modelRoot=owner+'/Body Types/'+(pack.species==='bear'?'Brown Bear Placeholder':'Standard');
 let skeleton,models=[];
 for(const file of pack.files){assert.equal(hash(fs.readFileSync(file.staged)),file.sha256);const ext=path.extname(file.relative),name=path.basename(file.relative);let kind,destination;
  if(ext==='.glb'||ext==='.blend'){kind=ext==='.blend'?'Models/Source':/LOD0/.test(name)?'Models/Assemblies':'Models/LODs';destination=modelRoot+'/'+kind+'/'+pack.version+'/'+name;}
  else if(/^textures\//i.test(file.relative)){kind='Textures';destination=owner+'/Textures/'+pack.version+'/'+name;}
  else if(/^previews?\//i.test(file.relative)){kind='Previews';destination=owner+'/Previews/'+pack.version+'/'+name;}
  else{kind=/clip_contract|authoring_contract/i.test(name)?'Contracts':/manifest|SHA256SUMS/i.test(name)?'Manifests':/LICENSE|PROVENANCE|REFERENCE|SOURCE_AND_LICENSE/i.test(file.relative)?'Provenance':/^(evidence|proof|validation)\//i.test(file.relative)?'Validation':'Guides';destination=owner+'/Documentation/'+kind+'/'+pack.version+'/'+file.relative;}
  assert(!fs.existsSync(destination),'Destination already present: '+destination);const row={...file,kind,destination,origin:owner+'/'+pack.version+'/'+file.relative,materialFolder:modelRoot+'/Materials/'+pack.version};
  if(ext==='.glb'){
   const b=fs.readFileSync(file.staged);assert.equal(b.readUInt32LE(0),0x46546c67);assert.equal(b.readUInt32LE(8),b.length);const length=b.readUInt32LE(12),g=JSON.parse(b.subarray(20,20+length)),bin=b.subarray(28+length);
   const bytes=i=>{const a=g.accessors[i],v=g.bufferViews[a.bufferView],width={SCALAR:1,VEC2:2,VEC3:3,VEC4:4,MAT4:16}[a.type]*({5126:4,5125:4,5123:2,5121:1}[a.componentType]);assert(!v.byteStride||v.byteStride===width,'Unexpected strided skeletal accessor');return bin.subarray((v.byteOffset||0)+(a.byteOffset||0),(v.byteOffset||0)+(a.byteOffset||0)+a.count*width);};
   assert.equal(g.skins.length,1);const skin=g.skins[0];assert.equal(skin.joints.length,spec.bones);assert.equal(g.animations.length,4);assert(g.images.every(i=>i.bufferView!==undefined));
   const rig=skin.joints.map(i=>{const n=g.nodes[i];return {name:n.name,translation:n.translation,rotation:n.rotation,scale:n.scale,parent:g.nodes.find(n=>n.children?.includes(i))?.name};});
   const signature=JSON.stringify({rig,inverse:hash(bytes(skin.inverseBindMatrices)),clips:g.animations.map(a=>({name:a.name,channels:a.channels.map(c=>({target:g.nodes[c.target.node].name,path:c.target.path,input:hash(bytes(a.samplers[c.sampler].input)),output:hash(bytes(a.samplers[c.sampler].output))}))}))});if(skeleton)assert.equal(signature,skeleton,pack.species+' own LOD skeleton/clip equality');else skeleton=signature;
   row.triangles=g.meshes.reduce((sum,m)=>sum+m.primitives.reduce((n,p)=>n+g.accessors[p.indices].count/3,0),0);const lod=Number(name.match(/LOD(\d)/)[1]);assert.equal(row.triangles,spec.triangles[lod]);
   row.embeddedMaterials=g.materials.map(m=>({name:m.name,alphaMode:m.alphaMode||'OPAQUE',doubleSided:!!m.doubleSided,extensions:m.extensions}));row.clips=g.animations.map(a=>{const inputs=a.samplers.map(s=>g.accessors[s.input]);return {name:a.name,start:Math.min(...inputs.map(a=>a.min[0])),duration:Math.max(...inputs.map(a=>a.max[0]))-Math.min(...inputs.map(a=>a.min[0]))};});assert(row.clips.every(c=>c.start===0));models.push(row);
  }files.push(row);
 }
 models.sort((a,b)=>a.destination.localeCompare(b.destination));assert.equal(models.length,3);
 const entry={assetId:'yq_'+pack.species+'_'+pack.version.toLowerCase(),kind:'wildlife',species:pack.species,category:pack.species,bodyForm:pack.species==='bear'?'brown_placeholder':'standard',compatibilityId:pack.species+'_'+pack.version.toLowerCase(),sourcePaths:models.map(r=>r.destination),sourceHashes:models.map(r=>r.sha256)};
 if(spec.contract){const c=read(stage+'/'+spec.contract),clips=c.clips||c,profile={};for(const [phase,key]of [['start','locomotion_start'],['loop','locomotion_loop'],['stop','locomotion_stop']]){
   const samples=pack.species==='boar'?c.contact_targets[key]:clips[key].samples,duration=pack.species==='boar'?c.frames[key]/c.fps:clips[key].duration_s,values=samples.map(s=>pack.species==='boar'?s.engine_travel_forward_m:s.virtual_ground_distance_m);assert.equal(samples[0].frame,0);assert(samples.every((s,i)=>s.frame===i));assert.equal(values[0],0);assert(values.every((v,i)=>Number.isFinite(v)&&(!i||v>=values[i-1]-1e-10)));assert(values.at(-1)>0);const clip=models[0].clips.find(a=>a.name.endsWith(key));assert(Math.abs(clip.duration-duration)<1e-7);profile[phase+'Duration']=duration;profile[phase+'Distance']=values;
  }entry.motionProfile=profile;entry.authoredWalkSpeed=profile.loopDistance.at(-1)/profile.loopDuration;
 }else{entry.authoredWalkSpeed=0.2564102564;entry.motionProfile=null;checks.push('bear: placeholder fidelity retained; no invented sampled transition travel');}
 bodies.push(entry);checks.push(pack.species+': three exact LOD skeletons, inverse binds and four own animations');
}
assert.equal(new Set(files.map(f=>f.destination)).size,files.length);assert.equal(read(output+'/duplicate-cat.json').installedMembersIdentical,true);
fs.writeFileSync(output+'/plan.json',JSON.stringify({status:'PREFLIGHT_PASS',files,bodies,duplicateCat:'18 exact source/installed members identical; ZIP container bytes differ; skip repeat'},null,2));
fs.writeFileSync(output+'/source-contracts.json',JSON.stringify({status:'PASS',utc:new Date().toISOString(),files:files.length,bodies:5,lods:15,checks,limits:['Bear is a placeholder; no source start/stop travel samples were delivered.','External approval is not Unity or live PlaySafe certification.']},null,2));console.log('PASS: '+files.length+' source files, five bodies, fifteen own-rig LODs; repeated Cat skipped.');
