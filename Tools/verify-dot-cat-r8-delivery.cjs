const fs=require('fs'),crypto=require('crypto'),assert=require('assert');
const root='Assets/Assets/GeneratedAssets/DOT Generated Assets',output='outputs/DOT_Integration_20261004/Cat_R8';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const guid=p=>fs.readFileSync(p+'.meta','utf8').match(/^guid: ([a-f0-9]+)$/m)?.[1];
// note: Complement fresh Editor fixtures with exact source, categorized aliases, metadata and index preservation; no live gameplay claim.
const integration=read(output+'/integration.json');assert.equal(integration.status,'PASS');assert.equal(integration.detail.totalEntries,212);assert.equal(integration.detail.preservedPriorEntries,211);
const plan=read(output+'/plan.json'),layout=read(root+'/Catalogs/DOT_ASSET_LAYOUT.json'),index=read(root+'/Catalogs/DOT_ASSET_INDEX.json');
for(const file of plan.files){assert.equal(hash(file.destination),file.sha256);const row=layout.files.find(r=>r.path===file.destination);assert(row);assert.equal(row.origin,file.origin);assert.equal(row.sha256,file.sha256);assert.equal(row.guid,guid(file.destination));assert.equal(row.metaSha256,hash(file.destination+'.meta'));}
const before=read(output+'/Backup/'+root+'/Catalogs/DOT_ASSET_LAYOUT.json');for(const row of before.files)assert.deepEqual(layout.files.find(r=>r.path===row.path),row);
const priorIndex=read(output+'/Backup/'+root+'/Catalogs/DOT_ASSET_INDEX.json');for(const row of priorIndex.files)if(row.path!==root+'/Catalogs/DOT_ASSET_LAYOUT.json')assert.deepEqual(index.files.find(r=>r.path===row.path),row);
const indexedPaths=[...plan.files.map(f=>f.destination),root+'/Catalogs/DOT_ASSET_LAYOUT.json',root+'/Wildlife/Cat/Documentation/Manifests/R8/Cat_Installed_Content.json',root+'/Wildlife/Cat/Body Types/Domestic Tabby/Materials/R8/MATERIAL_INDEX.json'];
const corrections=index.files.filter(r=>r.path.includes('/Unity Texture Alias Corrections/'));
for(const p of [...indexedPaths,...corrections.map(r=>r.path)]){const row=index.files.find(r=>r.path===p);assert(row,p);assert.equal(row.sha256,hash(p));assert.equal(row.guid,guid(p));}
assert.equal(corrections.filter(r=>r.path.includes('/Wildlife/Cat/')&&r.path.endsWith('.mat')).length,18);
assert.equal(corrections.filter(r=>r.path.includes('/NPCs/Bramblekin/')&&r.path.endsWith('.mat')).length,2);
assert.equal(index.fileCount,index.files.length);assert.equal(new Set(index.files.map(r=>r.path)).size,index.files.length);assert.equal(new Set(layout.files.map(r=>r.path)).size,layout.files.length);
assert.equal(read(output+'/cat-binding-verification.json').status,'PASS');const motion=read(output+'/motion-verification.json');assert.equal(motion.status,'PASS');assert.equal(motion.evaluatedSkinFrames,642);
const preview=read(output+'/Preview/receipt.json');assert.equal(preview.status,'PASS');assert.equal(preview.samples.length,12);assert(preview.samples.every(s=>s.visiblePixels>=100&&fs.existsSync(s.image)));
const receipt={status:'PASS',utc:new Date().toISOString(),sourceFiles:18,preservedPriorLayoutRows:before.files.length,preservedPriorIndexRows:priorIndex.files.length-1,ownedCatMaterialCorrections:18,lods:3,ownLoops:4,evaluatedSkinFrames:642,renderedPoses:12,evidence:'Exact source bytes and categorized aliases, GUID/meta hashes, current indexes and preserved prior rows; fresh Editor binding/motion/render receipts. Live contact and moving LODs remain unverified.'};
fs.writeFileSync(output+'/delivery-verification.json',JSON.stringify(receipt,null,2));console.log(JSON.stringify(receipt));
