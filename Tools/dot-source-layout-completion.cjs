// note: Complete the existing source-only migration; preserve payloads and sidecars and retire only the replaced generated index and unreferenced empty folders.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const {spawnSync} = require('node:child_process');
const root = 'Assets/Assets/GeneratedAssets/DOT Generated Assets';
const output = 'outputs/DOT_Source_Completion_20261003';
const manifestPath = `${root}/Catalogs/DOT_ASSET_LAYOUT.json`;
const slash = value => value.replaceAll('\\', '/');
const read = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const guid = file => /^guid: ([a-f0-9]{32})$/m.exec(fs.readFileSync(file + '.meta', 'utf8'))?.[1];
function walk(dir, directories = []) {
  return fs.readdirSync(dir, {withFileTypes: true}).flatMap(entry => {
    const file = `${dir}/${entry.name}`;
    if (entry.isDirectory()) { directories.push(file); return walk(file, directories); }
    return [file];
  });
}
function target(row) {
  const name = path.posix.basename(row.path);
  // note: The shipped slot manifests distinguish attachment geometry from outfits, including left and right replacement horns.
  if (row.pack === 'Lizardmen' && row.path.includes('/Modular/')) {
    const slot = /_dorsal\.glb$/.test(name) ? 'Dorsal' : /_temple_L\.glb$/.test(name) ? 'Temple Left' : /_temple_R\.glb$/.test(name) ? 'Temple Right' : /_masked_anatomy_/.test(name) ? 'Masked Anatomy' : 'Outfit';
    return row.path.replace(/\/Outfit\//, `/${slot}/`);
  }
  if (row.pack === 'Satyr' && row.path.includes('/Modular/')) return row.path.replace('/Horns/', /_horn_L\.glb$/.test(name) ? '/Horn Left/' : '/Horn Right/');
  if (!row.path.includes('/Documentation/')) return row.path;
  let owner = row.path.slice(0, row.path.indexOf('/Documentation/'));
  if (owner === root) owner = row.pack.startsWith('Crafting') ? `${root}/Crafting/Documentation/${row.pack === 'Crafting' ? 'Starter Kit' : 'Armor Coverage'}` : `${root}/Equipment/Documentation/${row.pack === 'EquipmentLibrary' ? 'Library' : 'Modular'}`;
  else owner += '/Documentation';
  if (row.kind === 'Models/Source') return `${root}/Equipment/Shared/Models/Source/${name}`;
  if (row.kind === 'Previews') {
    const previewOwner = name.startsWith('Boots_') ? 'Equipment/Clothing/Boots/Female' : name.startsWith('Sabatons_') ? 'Equipment/Armor/Sabatons/Female' : name.startsWith('Trousers_') ? 'Equipment/Clothing/Trousers' : name.startsWith('Curated_Weapons_') ? 'Equipment/Weapons' : 'Equipment/Shared';
    return `${root}/${previewOwner}/Previews/${name}`;
  }
  const ext = path.posix.extname(name).toLowerCase();
  let role = ['.py', '.cjs', '.cs'].includes(ext) ? 'Tools' : /\/Validation\//i.test(row.path) || /(?:_QA|_qa|motion_build|reimport|release_qa|proof_contract|actual_dense_ground|module_bindings)/.test(name) ? 'Validation' : row.kind === 'Documentation/Provenance' ? 'Provenance' : /schema/i.test(name) ? 'Schemas' : /recipe|resolved_examples/.test(name) ? 'Recipes' : /manifest|catalog|checksum|summary|inventory|file_list/i.test(name) ? 'Manifests' : 'Guides';
  let tail = name;
  if (role === 'Validation') {
    const match = /\/Validation\/(.+)$/.exec(row.path);
    if (match) tail = match[1];
    const patch = /\/Patches\/([^/]+)\//.exec(row.path);
    if (patch) tail = `Patches/${patch[1]}/${tail}`;
  }
  // note: Keep distinct supplied guides with the same basename; their contents and filenames are not rewritten.
  if (/^README\./i.test(name)) {
    let context = path.posix.basename(path.posix.dirname(row.path));
    if (context === 'Pack' || ['MonsterCrafting', 'monster_crafting_armor_coverage', 'EquipmentLibrary', 'EquipmentModular', 'YourQuest_Lizardmen', 'SatyrKit', 'OrcKit', 'YourQuest_Enemies', 'YourQuest_Rabbit'].includes(context)) context = 'Overview';
    tail = `${context}/${name}`;
  }
  if (/\.provenance\.json$/.test(name) || /\/Textures\/PROVENANCE\.json$/.test(row.path)) return owner.replace(/\/Documentation$/, '') + '/Materials/Provenance/' + name;
  return `${owner}/${role}/${tail}`;
}
function plan() {
  fs.mkdirSync(output, {recursive: true});
  const layout = read(manifestPath), directories = [], all = walk(root, directories);
  const retired = layout.files.filter(row => row.pack === 'PriorCatalogs');
  if (retired.length !== 1 || retired[0].path !== `${root}/Catalogs/Previous Layout/DOT_ASSET_INDEX.json` || read(retired[0].path).schema !== 'yourquest.dot-asset-index.v1') throw Error('Expected only the superseded generated index.');
  const payloads = all.filter(file => !file.endsWith('.meta'));
  const original = new Map(layout.files.map(row => [row.path, row]));
  const rows = payloads.map(file => ({path:file, destination:original.has(file) ? target(original.get(file)) : file, sha256:hash(file), guid:guid(file), metaSha256:hash(file + '.meta'), retire:retired.some(row => row.path === file)}));
  const destinations = rows.filter(row => !row.retire).map(row => row.destination.toLowerCase());
  if (new Set(destinations).size !== destinations.length || rows.some(row => !row.guid)) throw Error('Destination collision or missing source GUID.');
  if (all.some(file => file.endsWith('.meta') && !fs.existsSync(file.slice(0, -5)))) throw Error('Orphaned metadata must be resolved before migration.');
  const emptyFolders = directories.filter(dir => !rows.some(row => !row.retire && row.destination.startsWith(dir + '/'))).map(dir => ({path:dir,guid:guid(dir)}));
  const retiredGuids = new Set([...emptyFolders.map(row => row.guid), ...retired.map(row => row.guid)]);
  const ownMetas = new Set([...emptyFolders.map(row => row.path + '.meta'), ...retired.map(row => row.path + '.meta')]);
  const references = [];
  // note: A folder or replaced generated catalog may be deleted only after checking actual Unity serialized references, including importer sidecars.
  console.log('Source snapshot captured; checking cleanup GUID references.');
  fs.writeFileSync(`${output}/cleanup-guids.txt`,[...retiredGuids].join('\n')+'\n');
  const scan=spawnSync('rg',['--line-number','--no-heading','--fixed-strings','--file',`${output}/cleanup-guids.txt`,'--glob','*.asset','--glob','*.prefab','--glob','*.unity','--glob','*.mat','--glob','*.controller','--glob','*.overrideController','--glob','*.meta','Assets'],{encoding:'utf8',maxBuffer:4*1024*1024});
  if(scan.status!==0&&scan.status!==1)throw Error('GUID reference search failed: '+scan.stderr);
  for(const line of scan.stdout.split(/\r?\n/).filter(Boolean)) {const match=/^([^:]+):(\d+):(.*)$/.exec(line);if(!match||!ownMetas.has(slash(match[1])))references.push({reference:line});}
  if (references.length) throw Error('Referenced cleanup candidates: ' + JSON.stringify(references.slice(0, 8)));
  const protectedFiles = Object.fromEntries(walk('Assets/YourQuest DOT Creatures').concat(['Assets/Assets/Resources/GeneratedRpgContentLibrary.asset']).map(file => [file,hash(file)]));
  const result = {status:'PREFLIGHT_PASS',utc:new Date().toISOString(),root,originalImportCount:layout.files.length,rows,emptyFolders,retired,protectedFiles,serializedCleanupReferences:references};
  fs.writeFileSync(`${output}/plan.json`, JSON.stringify(result,null,2)+'\n');
  console.log(JSON.stringify({status:result.status,payloads:rows.length,moves:rows.filter(row=>row.path!==row.destination).length,emptyFolders:emptyFolders.length,retiredGeneratedIndexes:retired.length,serializedCleanupReferences:references.length},null,2));
}
// note: Unity may fold space-containing paths; omit only complete location fields and their continuations when comparing accepted catalog metadata.
function catalogMetadata(text) {
  let locationIndent = null;
  return text.replaceAll('\r\n','\n').split('\n').filter(line => {
    const field = /^(\s+)(?:sourcePaths|sourcePath|partsSourcePath):/.exec(line);
    if(field){locationIndent=field[1].length;return false;}
    if(locationIndent!==null){const indent=/^\s*/.exec(line)[0].length;if(indent>locationIndent||(indent===locationIndent&&/^\s+- /.test(line)))return false;locationIndent=null;}
    return true;
  }).join('\n');
}
const metadataHash = file => crypto.createHash('sha256').update(catalogMetadata(fs.readFileSync(file,'utf8'))).digest('hex');
function verify() {
  const plan = read(`${output}/plan.json`), layout = read(manifestPath), failures = [];
  const mutable = file => file === manifestPath || file === `${root}/Catalogs/DOT_ASSET_INDEX.json` || file === `${root}/README.md` || file.endsWith('/MATERIAL_INDEX.json');
  for (const row of plan.rows) {
    if (row.retire) { if(fs.existsSync(row.path)||fs.existsSync(row.path+'.meta'))failures.push('Retired catalog remains: '+row.path); continue; }
    const file=row.destination;
    if (!fs.existsSync(file)||!fs.existsSync(file+'.meta')){failures.push('Missing: '+file);continue;}
    if(hash(file+'.meta')!==row.metaSha256||guid(file)!==row.guid)failures.push('Metadata changed: '+file);
    if(!mutable(file)&&hash(file)!==row.sha256)failures.push('Payload changed: '+file);
  }
  for (const [file,expected] of Object.entries(plan.protectedFiles)) if(!fs.existsSync(file)||hash(file)!==expected)failures.push('User creature tree or production pool changed: '+file);
  // note: Only source-path fields may change in the runtime catalogs; accepted identities, prefab keys, modules and mechanical data must be identical.
  for(const [name,catalog] of [['creature','YQDotCreatureCatalog'],['equipment','YQDotEquipmentCatalog']]) {
    const baseline=`${output}/Before/${name}-catalog.asset`;
    const expected=fs.existsSync(baseline)?metadataHash(baseline):plan.catalogMetadataSha256?.[name];
    if(!expected||metadataHash(`Assets/Assets/Resources/Player/${catalog}.asset`)!==expected)failures.push('Catalog metadata beyond locations changed: '+catalog);
  }
  const directories=[],current=walk(root,directories),actual=current.filter(file=>!file.endsWith('.meta'));
  if(current.some(file=>file.endsWith('.meta')&&!fs.existsSync(file.slice(0,-5))))failures.push('Orphaned metadata');
  if(directories.some(dir=>!fs.readdirSync(dir).length))failures.push('Empty folder remains');
  const index=read(`${root}/Catalogs/DOT_ASSET_INDEX.json`);
  if(index.files.length!==actual.length-1||index.fileCount!==index.files.length)failures.push('Incomplete live catalog');
  for(const row of index.files)if(!fs.existsSync(row.path)||hash(row.path)!==row.sha256||guid(row.path)!==row.guid)failures.push('Live catalog mismatch: '+row.path);
  // note: The relocation manifest tracks original imports; the live catalog also lists generated material indexes and library documentation.
  if(layout.files.length!==plan.originalImportCount-plan.retired.length)failures.push('Original source inventory count changed');
  for(const file of actual.filter(file=>file.endsWith('/MATERIAL_INDEX.json')))for(const material of read(file).materials)if(!fs.existsSync(material.sourcePath)||guid(material.sourcePath)!==material.sourceGuid)failures.push('Material source mismatch: '+material.sourcePath);
  const result={status:failures.length?'FAIL':'PASS',utc:new Date().toISOString(),payloads:actual.length,liveCatalogEntries:index.files.length,preservedOriginalImports:layout.files.length,protectedUserFiles:Object.keys(plan.protectedFiles).length,removedEmptyFolders:plan.emptyFolders.length,retiredGeneratedIndexes:plan.retired.length,failures,evidence:'Every source payload and sidecar compared with the pre-move snapshot, complete live catalog and material references checked, user creature folder tree and production pool preserved.'};
  fs.writeFileSync(`${output}/file-verification.json`,JSON.stringify(result,null,2)+'\n');
  console.log(JSON.stringify({...result,failures:failures.slice(0,8)},null,2));if(failures.length)process.exitCode=1;
}
function cleanup() {
  // note: Retain the compact regression snapshot and receipts; delete only this operation's temporary full-file backups and search patterns after both checks pass.
  if(read(`${output}/editor-verification.json`).status!=='PASS'||read(`${output}/file-verification.json`).status!=='PASS')throw Error('Editor and source preservation checks must pass before cleanup.');
  const plan=read(`${output}/plan.json`);
  const before=`${output}/Before`;
  let removedBackups=0;
  if(fs.existsSync(before)) {
    plan.catalogMetadataSha256=Object.fromEntries(['creature','equipment'].map(name=>[name,metadataHash(`${before}/${name}-catalog.asset`)]));
    fs.writeFileSync(`${output}/plan.json`,JSON.stringify(plan,null,2)+'\n');
    if(!path.resolve(before).startsWith(path.resolve(output)+path.sep))throw Error('Backup cleanup target escapes the operation directory.');
    removedBackups=walk(before).length;
    fs.rmSync(before,{recursive:true});
  }
  if(fs.existsSync(`${output}/cleanup-guids.txt`))fs.unlinkSync(`${output}/cleanup-guids.txt`);
  const result={status:'PASS',utc:new Date().toISOString(),removedTemporaryBackups:removedBackups,removedEmptySourceFolders:plan.emptyFolders.length,retiredSupersededIndexes:plan.retired.length,sourceModelsDeleted:0,userCreatureFoldersPreserved:true,retainedEvidence:['plan.json','editor-verification.json','file-verification.json','cleanup.json']};
  fs.writeFileSync(`${output}/cleanup.json`,JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify(result,null,2));
}
if(process.argv[2]==='plan')plan();else if(process.argv[2]==='verify')verify();else if(process.argv[2]==='cleanup')cleanup();else throw Error('Use plan, verify or cleanup from the project root.');
