const fs=require('fs'),crypto=require('crypto'),assert=require('assert');
const root='Assets/Assets/GeneratedAssets/DOT Generated Assets',output='outputs/DOT_Integration_20261004/Wildlife';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const guid=p=>fs.readFileSync(p+'.meta','utf8').match(/^guid: ([a-f0-9]+)$/m)?.[1];
// note: Confirm exact installed source and preserved prior indexes; Editor and gameplay evidence remain distinct.
const integration=read(output+'/integration.json');assert.equal(integration.status,'PASS');
const plan=read(output+'/plan.json'),layout=read(root+'/Catalogs/DOT_ASSET_LAYOUT.json'),index=read(root+'/Catalogs/DOT_ASSET_INDEX.json');
for(const file of plan.files){assert.equal(hash(file.destination),file.sha256);const row=layout.files.find(r=>r.path===file.destination);assert(row);assert.equal(row.origin,file.origin);assert.equal(row.sha256,file.sha256);assert.equal(row.guid,guid(file.destination));assert.equal(row.metaSha256,hash(file.destination+'.meta'));}
const before=read(output+'/Backup/'+root+'/Catalogs/DOT_ASSET_LAYOUT.json');for(const row of before.files)assert.deepEqual(layout.files.find(r=>r.path===row.path),row);
const priorIndex=read(output+'/Backup/'+root+'/Catalogs/DOT_ASSET_INDEX.json');for(const row of priorIndex.files)if(row.path!==root+'/Catalogs/DOT_ASSET_LAYOUT.json')assert.deepEqual(index.files.find(r=>r.path===row.path),row);
const paths=[...plan.files.map(f=>f.destination),root+'/Catalogs/DOT_ASSET_LAYOUT.json',root+'/Catalogs/DOT_WILDLIFE_20261004_INSTALLED.json',...new Set(plan.files.filter(f=>f.destination.endsWith('.glb')).map(f=>f.materialFolder+'/MATERIAL_INDEX.json'))];
for(const p of paths){const row=index.files.find(r=>r.path===p);assert(row,p);assert.equal(row.sha256,hash(p));assert.equal(row.guid,guid(p));}
assert.equal(index.fileCount,index.files.length);assert.equal(new Set(index.files.map(r=>r.path)).size,index.files.length);assert.equal(new Set(layout.files.map(r=>r.path)).size,layout.files.length);
for(const body of plan.bodies)assert.equal(read(output+'/'+body.species+'-binding-verification.json').status,'PASS');
const motion=read(output+'/motion-verification.json');assert.equal(motion.status,'PASS');
const preview=read(output+'/Preview/receipt.json');assert.equal(preview.status,'PASS');assert.equal(preview.samples.length,60);assert(preview.samples.every(s=>s.visiblePixels>=100&&fs.existsSync(s.image)));
assert.equal(read(output+'/duplicate-cat.json').installedMembersIdentical,true);
for(const file of read('outputs/DOT_Integration_20261004/Cat_R8/plan.json').files)assert.equal(hash(file.destination),file.sha256);
const receipt={status:'PASS',utc:new Date().toISOString(),sourceFiles:plan.files.length,preservedPriorLayoutRows:before.files.length,preservedPriorIndexRows:priorIndex.files.length-1,priorCatalogEntries:integration.detail.preservedPriorEntries,totalCatalogEntries:integration.detail.totalEntries,lods:15,evaluatedSkinFrames:motion.evaluatedSkinFrames,renderedPoses:60,bear:'Placeholder scope retained',duplicateCat:'18 source/installed members match; no repeated intake',evidence:'Exact installed hashes, categorized aliases, GUID/meta and index preservation; Editor rig/travel/material/bounds/render fixtures. Live PlaySafe acceptance remains unverified.'};
fs.writeFileSync(output+'/delivery-verification.json',JSON.stringify(receipt,null,2));console.log(JSON.stringify(receipt));
