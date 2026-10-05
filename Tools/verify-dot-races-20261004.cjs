const fs = require('fs'), crypto = require('crypto'), assert = require('assert');
const output = 'outputs/DOT_Integration_20261004/Races', root = 'Assets/Assets/GeneratedAssets/DOT Generated Assets';
const plan = JSON.parse(fs.readFileSync(output+'/plan.json','utf8').replace(/^\uFEFF/,''));
const hash = b => crypto.createHash('sha256').update(b).digest('hex');
const read = p => JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const rows = race => plan.files.filter(f=>f.race===race);
const file = (race,p) => rows(race).find(f=>f.relative===p);
const doc = (race,p) => read(file(race,p).staged);
// note: Manifests can describe a larger release than a supplied archive. Validate delivered members and explicitly retain absent members as unavailable.
const missing = [];
for(const [race,records,key] of [ ['Dwarf',doc('Dwarf','MANIFEST_R1.json').payload,'archive_path'], ['Kitsune',doc('Kitsune','Docs/Asset_Manifest.json').files,'file'], ['Bramblekin',doc('Bramblekin','Approval/Approved_Payload_Manifest_I16.json').files,'path'] ]) {
  for(const record of records) {const f=file(race,record[key]); if(!f){missing.push({race,path:record[key]});continue;} assert.equal(f.sha256,record.sha256,record[key]); assert.equal(f.bytes,record.bytes,record[key]);}
}
const contracts=[];
for(const race of ['Dwarf','Kitsune','Bramblekin']) {
  let base;
  for(const f of rows(race).filter(f=>f.relative.endsWith('.glb')).sort((a,b)=>a.relative.localeCompare(b.relative))) {
    const b=fs.readFileSync(f.staged); assert.equal(hash(b),f.sha256); assert.equal(b.readUInt32LE(0),0x46546c67);
    const size=b.readUInt32LE(12), j=JSON.parse(b.subarray(20,20+size)), bin=b.subarray(28+size);
    const accessor = i => {const a=j.accessors[i],v=j.bufferViews[a.bufferView]; assert(!a.sparse); return bin.subarray(v.byteOffset+(a.byteOffset||0),v.byteOffset+(a.byteOffset||0)+a.count*({SCALAR:1,VEC2:2,VEC3:3,VEC4:4,MAT4:16}[a.type])*({5126:4,5125:4,5123:2,5121:1}[a.componentType]));};
    const skin=j.skins[0], expected={Dwarf:166,Kitsune:132,Bramblekin:59}[race]; assert.equal(skin.joints.length,expected); assert.equal(j.animations.length,5);
    const skeleton=skin.joints.map(i=>{const n=j.nodes[i]; return {name:n.name,t:n.translation,r:n.rotation,s:n.scale,children:(n.children||[]).map(c=>j.nodes[c].name)};});
    const skeletalAnimation=j.animations.map(a=>({name:a.name,channels:a.channels.filter(c=>c.target.path!=='weights').map(c=>{const s=a.samplers[c.sampler];return {node:j.nodes[c.target.node].name,path:c.target.path,input:hash(accessor(s.input)),output:hash(accessor(s.output))};})}));
    const signature=JSON.stringify({skeleton,binds:hash(accessor(skin.inverseBindMatrices)),skeletalAnimation});
    if(base) assert.equal(signature,base,'All LODs must retain the exact own rig and skeletal motion: '+race); else base=signature;
    f.embeddedMaterials=j.materials.map(m=>({name:m.name,alphaMode:m.alphaMode||'OPAQUE',alphaCutoff:m.alphaCutoff,baseColorFactor:m.pbrMetallicRoughness?.baseColorFactor,extensions:m.extensions}));
    f.clips=j.animations.map(a=>{const times=a.samplers.map(s=>j.accessors[s.input]); const start=Math.min(...times.map(t=>t.min[0])),end=Math.max(...times.map(t=>t.max[0]));return {name:a.name,start,duration:end-start};});
    f.joints=expected; f.triangles=j.meshes.reduce((n,m)=>n+m.primitives.reduce((v,p)=>v+j.accessors[p.indices].count/3,0),0);
    f.renderers=j.nodes.filter(n=>n.mesh!==undefined).length;
    f.morphs=j.meshes.map(m=>({name:m.name,names:m.extras?.targetNames||[]}));
    contracts.push({race,path:f.relative,joints:expected,triangles:f.triangles,renderers:f.renderers,materials:j.materials.length,clips:f.clips,morphs:f.morphs});
  }
}
plan.bodies=[];
for(const race of ['Dwarf','Kitsune','Bramblekin']) {
  const models=rows(race).filter(f=>f.relative.endsWith('.glb')).sort((a,b)=>a.relative.localeCompare(b.relative));
  assert.equal(models.length,4);
  const clips=race==='Dwarf'?doc(race,'Integration/ACTOR_DISPLACEMENT_AND_CONTACT_RECIPE.json').clips:race==='Kitsune'?doc(race,'Docs/Motion_Recipe.json').motion.clips:null;
  let profile=null;
  if(clips) {profile={};for(const [phase,name] of [['start','walk_start'],['loop','walk_loop'],['stop','walk_stop']]){const c=clips.find(c=>c.name===name),samples=c.actor_displacement;assert(samples.every((s,i)=>Math.abs(s.time_s-i*c.duration_s/(samples.length-1))<1e-7));profile[phase+'Duration']=c.duration_s;profile[phase+'Distance']=samples.map(s=>s.forward_distance_m);assert.equal(profile[phase+'Distance'][0],0);assert(profile[phase+'Distance'].every((v,i,a)=>i===0||v>=a[i-1])); assert(Math.abs(models[0].clips.find(c=>c.name.endsWith(name)).duration-c.duration_s)<1e-6);}}
  plan.bodies.push({assetId:{Dwarf:'yq_dwarf_stonewright_revisionb_r1',Kitsune:'yq_kitsune_adultfemale_v20',Bramblekin:'yq_bramblekin_a_i16'}[race],species:race.toLowerCase(),kind:'race',category:race.toLowerCase(),bodyForm:{Dwarf:'stonewright',Kitsune:'adult_female',Bramblekin:'ordinary'}[race],compatibilityId:{Dwarf:'dwarf_stonewright_revisionb_r1',Kitsune:'kitsune_adult_female_v1',Bramblekin:'bramblekin_a_i16'}[race],sourcePaths:models.map(f=>f.destination),sourceHashes:models.map(f=>f.sha256),authoredWalkSpeed:profile?profile.loopDistance.at(-1)/profile.loopDuration:0,motionProfile:profile});
}
// note: The duplicate add-on is checked against the installed, categorized payload, and never queued for another import.
const avian=read(root+'/NPCs/Avian/Documentation/Manifests/Avian_Installed_Content.json');
const original='C:/Users/Garri/Downloads/YourQuest_Avian_Modular_Elemental_M2_Astra_Approved_ADDON_REQUIRES_V9b_2026-10-03.zip', duplicate=original.replace('.zip',' (1).zip');
assert.equal(hash(fs.readFileSync(original)),hash(fs.readFileSync(duplicate)));
for(const f of avian.files) assert.equal(hash(fs.readFileSync(f.destination)),f.sha256);
plan.status='PREFLIGHT_PASS';fs.writeFileSync(output+'/plan.json',JSON.stringify(plan,null,2));
fs.writeFileSync(output+'/source-contracts.json',JSON.stringify({status:'PASS',files:plan.files.length,models:contracts,missingSuppliedMembers:missing,duplicateAvian:{status:'IDENTICAL_ALREADY_INSTALLED',archiveSha256:hash(fs.readFileSync(duplicate)),installedFilesChecked:avian.files.length},limitations:['Kitsune editable source and seven modules are outside the supplied runtime archive.','Bramblekin travel samples absent; no authored travel profile is invented.','Own per-LOD clips retain cloth/morph animation; shared-rig/texture residency optimization and shader-extension parity remain unverified.']},null,2));
console.log(JSON.stringify({status:plan.status,files:plan.files.length,models:contracts.length,missingSuppliedMembers:missing,duplicateAvian:'identical; installed payload checked'}));
