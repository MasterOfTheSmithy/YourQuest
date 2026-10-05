const fs = require('fs'), crypto = require('crypto'), assert = require('assert');
const root = 'Assets/Assets/GeneratedAssets/DOT Generated Assets', output = 'outputs/DOT_Integration_20261004/Races';
const read = p => JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
// note: This final source/layout audit complements Editor fixtures; it does not assert gameplay, material-extension parity or visual motion acceptance.
assert.equal(read(output+'/integration.json').status,'PASS','Complete the Editor transaction before delivery verification.');
const plan = read(output+'/plan.json'), layout = read(root+'/Catalogs/DOT_ASSET_LAYOUT.json'), index = read(root+'/Catalogs/DOT_ASSET_INDEX.json');
for(const f of plan.files) {
  assert.equal(hash(f.destination),f.sha256,f.destination);
  const row = layout.files.find(r=>r.path===f.destination); assert(row); assert.equal(row.origin,f.origin); assert.equal(row.sha256,f.sha256);
  const meta = fs.readFileSync(f.destination+'.meta','utf8'), guid = meta.match(/^guid: ([a-f0-9]+)$/m)?.[1];
  assert(guid); assert.equal(row.guid,guid); assert.equal(row.metaSha256,hash(f.destination+'.meta'));
  const indexed = index.files.find(r=>r.path===f.destination); assert(indexed); assert.equal(indexed.sha256,f.sha256); assert.equal(indexed.guid,guid);
}
const generated = [root+'/Catalogs/DOT_RACES_20261004_INSTALLED.json',...new Set(plan.files.filter(f=>f.destination.endsWith('.glb')).map(f=>f.materialFolder+'/MATERIAL_INDEX.json'))];
for(const p of [...generated,root+'/Catalogs/DOT_ASSET_LAYOUT.json']) {const row=index.files.find(f=>f.path===p);assert(row);assert.equal(row.sha256,hash(p));assert.equal(row.guid,fs.readFileSync(p+'.meta','utf8').match(/^guid: ([a-f0-9]+)$/m)[1]);}
const before=read(output+'/Backup/'+root+'/Catalogs/DOT_ASSET_LAYOUT.json');
for(const row of before.files) assert.deepEqual(layout.files.find(f=>f.path===row.path),row,'Prior layout contract changed: '+row.path);
assert.equal(read(root+'/Catalogs/DOT_RACES_20261004_INSTALLED.json').bodies.length,3);
const bindings=['dwarf','kitsune','bramblekin'].map(s=>read(output+'/'+s+'-binding-verification.json'));
assert(bindings.every(r=>r.status==='PASS'&&r.entries===1));
assert.equal(read(output+'/motion-verification.json').status,'PASS');
const preview=read(output+'/Preview/receipt.json');assert.equal(preview.status,'PASS');assert.equal(preview.samples.length,60);assert(preview.samples.every(s=>s.visiblePixels>=100&&fs.existsSync(s.image)));
const receipt={status:'PASS',utc:new Date().toISOString(),sourceFiles:plan.files.length,generatedMetadata:generated.length,preservedPriorLayoutRows:before.files.length,bodies:3,lods:12,renderedPoses:60,evidence:'Exact installed source bytes, categorized aliases, GUID/meta hashes, current source index and preserved prior layout rows; fresh Editor binding, motion/bounds and nonblank render receipts. Live PlaySafe acceptance remains unverified.'};
fs.writeFileSync(output+'/delivery-verification.json',JSON.stringify(receipt,null,2));console.log(JSON.stringify(receipt));
