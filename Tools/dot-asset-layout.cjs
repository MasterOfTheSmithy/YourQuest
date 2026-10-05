// note: Classify the installed DOT files without regenerating content, then verify every moved payload and Unity sidecar against the captured revision.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const project = process.cwd();
const root = 'Assets/Assets/GeneratedAssets/DOT Generated Assets';
const output = 'outputs/DOT_Asset_Layout_20261003';
const manifestPath = `${root}/Catalogs/DOT_ASSET_LAYOUT.json`;
const slash = value => value.replaceAll('\\', '/');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const readJson = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const title = value => value.split('_').map(word => word[0].toUpperCase() + word.slice(1)).join(' ');
function walk(dir) {
  return fs.readdirSync(dir, {withFileTypes: true}).sort((a, b) => a.name.localeCompare(b.name)).flatMap(entry => {
    const file = `${dir}/${entry.name}`;
    return entry.isDirectory() ? walk(file) : [file];
  });
}
function guid(meta) {
  const result = /^guid: ([a-f0-9]{32})$/m.exec(fs.readFileSync(meta, 'utf8'));
  if (!result) throw Error(`Missing Unity GUID: ${meta}`);
  return result[1];
}
function confined(file) {
  const absolute = path.resolve(file);
  if (!absolute.startsWith(path.resolve(root) + path.sep)) throw Error(`Outside DOT root: ${file}`);
}
const packs = [
  ['EquipmentLibrary', 'YourQuest_Complete_Rebuilt_GLB_Library_2026-10-02', 'Equipment/Library'],
  ['EquipmentModular', 'DOT Gen Modular Expansion', 'Equipment/Modular'],
  ['Elf', 'Races/YourQuest_Elf_Modular_Race_Kit_2026-10-03 (1)', 'NPCs/Races/YourQuest_Elf_Modular_Race_Kit_2026-10-03 (1)'],
  ['Lizardmen', 'Races/YourQuest_Lizardmen_Modular_Race_Kit_2026-10-03', 'NPCs/Races/YourQuest_Lizardmen_Modular_Race_Kit_2026-10-03'],
  ['Orc', 'Races/YourQuest_Orc_Modular_Race_Kit_2026-10-03', 'NPCs/Races/YourQuest_Orc_Modular_Race_Kit_2026-10-03'],
  ['Satyr', 'NPCs/YourQuest_Satyr_Modular_Race_Kit_2026-10-03', 'NPCs/Races/YourQuest_Satyr_Modular_Race_Kit_2026-10-03'],
  ['Enemies', 'Enemies/YourQuest_Modular_Enemies_Batch01_2026-10-03', 'Monsters/YourQuest_Modular_Enemies_Batch01_2026-10-03'],
  ['Rabbit', 'Wildlife/YourQuest_Wildlife_Batch01_Rabbit_2026-10-03'],
  ['WildlifeHerd', 'Wildlife/YourQuest_Wildlife_Batch02_Horse_Cow_Stag_2026-10-03'],
  ['Crafting', 'Crafting/YourQuest_Monster_Material_Crafting_Starter_Kit_2026-10-03'],
  ['CraftingArmorCoverage', 'Crafting/YourQuest_Monster_Armor_Coverage_Supplement_1.1_2026-10-03']
];
const armor = new Set(['chest_armor', 'helmet', 'shoulder_armor', 'arm_guard', 'bracer', 'elbow_guard', 'faulds', 'gauntlets', 'knee_guard', 'manica', 'pteruges', 'sabatons', 'shin_guard', 'tasset']);
function equipmentType(category, family) {
  if (category === 'Weapons') return `Equipment/Weapons/${title(family)}`;
  if (category === 'Special_Weapons') return `Equipment/Weapons/Special/${title(family)}`;
  if (category === 'Defense') return `Equipment/Armor/${title(family)}`;
  if (category === 'Accessories') return `Equipment/Accessories/${title(family)}`;
  if (category === 'Consumables') return `Items/Consumables/${title(family)}`;
  if (category === 'Wearables') return `Equipment/${armor.has(family) ? 'Armor' : 'Clothing'}/${title(family)}`;
  throw Error(`Unknown equipment category: ${category}`);
}
function assetKind(file, relative) {
  const ext = path.extname(file).toLowerCase();
  if (ext === '.blend') return 'Models/Source';
  if (ext === '.glb') return /_LOD[1-9]/i.test(file) ? 'Models/LODs' : /_Parts\.glb$/i.test(file) ? 'Models/Parts' : 'Models/Assemblies';
  if (ext === '.mhmat') return 'Materials/Definitions';
  if (/Previews/i.test(relative) || ['.mp4', '.jpg'].includes(ext)) return 'Previews';
  if (ext === '.png') return 'Materials/Textures';
  if (/Validation/i.test(relative)) return 'Validation';
  if (/Licenses?|Provenance|References/i.test(relative)) return 'Documentation/Provenance';
  if (['.py', '.cjs', '.cs'].includes(ext)) return 'Documentation/Tools';
  return 'Data';
}
function plan() {
  if (fs.existsSync(manifestPath)) throw Error('DOT layout is already installed; use verify.');
  fs.mkdirSync(output, {recursive: true});
  // note: Accept the earlier GUID-preserving pack moves as a current revision and keep both historical root spellings as exact aliases.
  for (const pack of packs) {
    pack.push(pack[1]);
    if (!fs.existsSync(`${root}/${pack[1]}`) && pack[2] && fs.existsSync(`${root}/${pack[2]}`)) pack[1] = pack[2];
  }
  packs.push(['PriorCatalogs', 'Catalogs']);
  const craftingRoot = `${root}/${packs.find(p => p[0] === 'Crafting')[1]}/MonsterCrafting`;
  const craftingManifest = readJson(`${craftingRoot}/Data/visual_manifest.v1.json`);
  const craftingRows = new Map([...craftingManifest.visuals, ...craftingManifest.assemblies].map(row => [row.file, row]));
  const coveragePrefix = `${root}/${packs.find(p => p[0] === 'CraftingArmorCoverage')[1]}/monster_crafting_armor_coverage`;
  const coverageManifest = fs.existsSync(coveragePrefix) ? readJson(`${coveragePrefix}/Data/visual_manifest.v1_1.json`) : null;
  const coverageRows = new Map(coverageManifest ? [...coverageManifest.visuals, ...coverageManifest.assemblies].map(row => [row.file, row]) : []);
  const elfManifest = readJson(`${root}/${packs.find(p => p[0] === 'Elf')[1]}/NPCs/npc_manifest_v2.json`);
  const orcManifest = readJson(`${root}/${packs.find(p => p[0] === 'Orc')[1]}/NPCs/OrcKit/orc_manifest.json`);
  const moduleSlots = new Map([...elfManifest.modules, ...orcManifest.modules].map(row => [path.basename(row.file), row.slot]));
  const allFiles = walk(root);
  const files = [];
  const destinations = new Set();
  const externalUris = [];
  for (const origin of allFiles.filter(file => !file.endsWith('.meta'))) {
    const rel = origin.slice(root.length + 1);
    const pack = packs.find(p => rel.startsWith(p[1] + '/'));
    if (!pack) throw Error(`Unclassified source: ${origin}`);
    const within = rel.slice(pack[1].length + 1);
    const name = path.basename(origin);
    const kind = assetKind(name, within);
    let target = `Documentation/Packs/${pack[0]}/${within}`;
    if (pack[0].startsWith('Equipment')) {
      let parts = within.split('/');
      const revised = parts[0] === 'Consumable_Update';
      if (revised || parts[0] === 'Items') parts = parts.slice(1);
      const category = parts[0];
      if (['Weapons', 'Special_Weapons', 'Defense', 'Accessories', 'Consumables', 'Wearables'].includes(category)) {
        const body = category === 'Wearables' ? parts[1] : null;
        const family = parts[body ? 2 : 1];
        if (family && family !== 'Materials' && fs.statSync(`${root}/${pack[1]}/${within.split('/').slice(0, revised || within.startsWith('Items/') ? 3 : 2).join('/')}`).isDirectory()) {
          const base = equipmentType(category, family) + (body ? '/' + body : '');
          const variant = revised ? 'Revised' : pack[0] === 'EquipmentLibrary' ? 'Library' : 'Modular';
          const tail = parts.slice(body ? 3 : 2).join('/');
          target = `${base}/${variant}/${pack[0] === 'EquipmentModular' && !revised ? tail : kind + '/' + name}`;
        } else if (family === 'Materials') target = `Equipment/Shared/Materials/Textures/${name}`;
      } else if (parts[0] === 'Shared_Atlas') target = `Equipment/Shared/Materials/Textures/${name}`;
    } else if (['Elf', 'Lizardmen', 'Orc', 'Satyr'].includes(pack[0])) {
      const base = `NPCs/${pack[0]}`;
      const body = /athletic/i.test(name) ? 'Athletic' : /slender/i.test(name) ? 'Slender' : /warden/i.test(name) ? 'Warden' : /brute/i.test(name) ? 'Brute' : /broad|Emberstone/i.test(name) ? 'Broad' : 'Standard';
      if (kind.startsWith('Models/')) target = `${base}/Body Types/${body}/${kind}/${name}`;
      else if (/\/Modules\//.test('/' + within)) target = `${base}/Modular/${body}/${title(moduleSlots.get(name) || (/affinity/i.test(name) ? 'affinity_set' : 'outfit'))}/${name}`;
      else if (kind === 'Previews' || kind.startsWith('Materials/')) target = `${base}/${kind}/${name}`;
      else target = `${base}/Documentation/Pack/${within}`;
      // note: Standalone modules remain under declared body/slot boundaries, including GLBs which otherwise look like assembled models.
      if (/\/Modules\//.test('/' + within)) target = `${base}/Modular/${body}/${title(moduleSlots.get(name) || (/horn/i.test(name) ? 'horns' : /affinity/i.test(name) ? 'affinity_set' : 'outfit'))}/${name}`;
    } else if (pack[0] === 'Enemies') {
      const species = /^(cairnback|thornweaver|sporewarden|maw|mimic)(?:_|\.)/i.exec(name)?.[1]?.toLowerCase();
      const base = species ? `Enemies/${title(species)}` : 'Enemies/Shared';
      const body = /_(standard|heavy|lean|elder)(?:_|\.)/i.exec(name)?.[1] || 'standard';
      if (within.startsWith('YourQuest_Enemies/Modules/')) target = `${base}/Modular/Affinity Sets/${name}`;
      else if (kind.startsWith('Models/')) target = `${base}/Archetypes/${title(body)}/${kind}/${name}`;
      else if (kind.startsWith('Materials/') || kind === 'Previews') target = `${base}/${kind}/${name}`;
      else target = `${base}/Documentation/Pack/${within}`;
    } else if (pack[0] === 'Rabbit' || pack[0] === 'WildlifeHerd') {
      const species = pack[0] === 'Rabbit' ? 'Rabbit' : /horse/i.test(name) ? 'Horse' : /cow/i.test(name) ? 'Cow' : /deer|stag/i.test(name) ? 'Stag' : 'Shared';
      const base = `Wildlife/${species}`;
      target = kind.startsWith('Models/') || kind.startsWith('Materials/') || kind === 'Previews' ? `${base}/${kind}/${name}` : `${base}/Documentation/Pack/${within}`;
    } else if (pack[0].startsWith('Crafting')) {
      const coverage = pack[0] === 'CraftingArmorCoverage';
      const row = (coverage ? coverageRows : craftingRows).get(within.replace(/^(MonsterCrafting|monster_crafting_armor_coverage)\//, ''));
      if (row) {
        const material = row.material_key?.split('.');
        const assembly = !row.slot;
        const base = material ? `Crafting/Monster Materials/${title(material[0])}/${title(material[1])}` : `Crafting/Assemblies/${row.category === 'weapon' ? 'Weapons' : 'Armor'}/${title(row.family)}`;
        target = `${base}/${row.sex ? title(row.sex) + '/' : ''}${coverage ? 'Armor Coverage/' : ''}${assembly ? 'Assemblies' : 'Modular/' + title(row.slot)}/${name}`;
      } else if (kind.startsWith('Materials/') || kind === 'Previews' || kind.startsWith('Models/')) target = `Crafting/Shared/${coverage ? 'Armor Coverage/' : ''}${kind}/${name}`;
    }
    const destination = `${root}/${target}`;
    confined(destination);
    if (destinations.has(destination.toLowerCase()) || fs.existsSync(destination)) throw Error(`Destination collision: ${destination}`);
    destinations.add(destination.toLowerCase());
    if (!fs.existsSync(origin + '.meta')) throw Error(`Missing sidecar: ${origin}`);
    const bytes = fs.readFileSync(origin);
    let embeddedMaterials = [];
    if (path.extname(origin) === '.glb') {
      if (bytes.readUInt32LE(0) !== 0x46546c67 || bytes.readUInt32LE(16) !== 0x4e4f534a) throw Error(`Invalid GLB: ${origin}`);
      const json = JSON.parse(bytes.subarray(20, 20 + bytes.readUInt32LE(12)).toString('utf8').trim());
      embeddedMaterials = (json.materials || []).map((material, index) => ({index, name: material.name || `Material ${index}`}));
      for (const item of [...(json.images || []), ...(json.buffers || [])]) if (item.uri && !item.uri.startsWith('data:')) externalUris.push({origin, uri: item.uri});
    }
    const aliases = [...new Set([pack[2], pack[3]].filter(p => p && p !== pack[1]).map(p => `${root}/${p}/${within}`))];
    if (pack[0] === 'EquipmentModular') aliases.push(`Assets/Assets/GeneratedAssets/DOT Gen Modular Expansion/${within}`);
    if (pack[0] === 'PriorCatalogs') target = `Catalogs/Previous Layout/${within}`;
    const splitBase = target.split(/\/(?:Models|Library|Modular|Revised|Archetypes|Body Types)\//)[0];
    const materialBase = splitBase === target ? path.posix.dirname(target).replace(/\/Assemblies$/, '') : splitBase;
    files.push({origin, path: pack[0] === 'PriorCatalogs' ? `${root}/${target}` : destination, aliases, pack: pack[0], kind, sha256: hash(bytes), guid: guid(origin + '.meta'), metaSha256: hash(fs.readFileSync(origin + '.meta')), embeddedMaterials, materialFolder: `${root}/${materialBase}/Materials`});
  }
  if (externalUris.length) throw Error(`External GLB dependencies need preserving: ${JSON.stringify(externalUris.slice(0, 5))}`);
  const orphanMetas = allFiles.filter(file => file.endsWith('.meta') && !fs.existsSync(file.slice(0, -5)));
  if (orphanMetas.length) throw Error(`Orphaned sidecars: ${orphanMetas.join(', ')}`);
  const directories = allFiles.filter(file => file.endsWith('.meta') && fs.statSync(file.slice(0, -5)).isDirectory()).map(meta => {
    const origin = meta.slice(0, -5), rel = origin.slice(root.length + 1);
    return {origin, path: `${root}/Documentation/Source Folder Metadata/${rel}`, guid: guid(meta), metaSha256: hash(fs.readFileSync(meta))};
  });
  const topFolders = fs.readdirSync(root, {withFileTypes: true}).filter(e => e.isDirectory()).map(e => e.name);
  const result = {schema: 'yourquest.dot-asset-layout.v1', status: 'PREFLIGHT_PASS', utc: new Date().toISOString(), files, directories, topFolders, externalGlbDependencies: externalUris.length};
  fs.writeFileSync(`${output}/plan.json`, JSON.stringify(result, null, 2) + '\n');
  console.log(JSON.stringify({status: result.status, files: files.length, folders: directories.length, glbs: files.filter(f => f.path.endsWith('.glb')).length, embeddedMaterials: files.reduce((sum, f) => sum + f.embeddedMaterials.length, 0), categories: [...new Set(files.map(f => f.path.slice(root.length + 1).split('/')[0]))], maxDestinationLength: Math.max(...files.map(f => path.resolve(f.path + '.meta').length)), plan: `${output}/plan.json`}, null, 2));
}
function verify() {
  const manifest = readJson(manifestPath);
  const failures = [];
  for (const row of [...manifest.files, ...manifest.directories]) {
    if (row.retiredUnreferencedFolder) continue;
    if (!fs.existsSync(row.path) || !fs.existsSync(row.path + '.meta')) { failures.push(`Missing: ${row.path}`); continue; }
    if (guid(row.path + '.meta') !== row.guid || hash(fs.readFileSync(row.path + '.meta')) !== row.metaSha256) failures.push(`Sidecar changed: ${row.path}`);
    // note: Explicit corrective deliveries retain the original intake hash and declare the reviewed installed revision separately.
    const installedHash = row.installedSha256 || row.sha256;
    if (installedHash && hash(fs.readFileSync(row.path)) !== installedHash) failures.push(`Payload changed: ${row.path}`);
  }
  let catalogMetadataChecks = 0;
  for (const [name, current] of [['creature', 'YQDotCreatureCatalog'], ['equipment', 'YQDotEquipmentCatalog']]) {
    const before = `${output}/${name}-catalog-before.asset`;
    if (!fs.existsSync(before)) continue;
    // note: Only source-location fields may differ; prefab keys, accepted identities, compatibility, eligibility and all other catalog metadata must remain unchanged.
    const normalize = text => {
      // note: Unity folds paths containing spaces onto indented continuation lines; exclude the complete allowed location field, including its array or scalar continuations.
      let locationIndent = null;
      return text.replaceAll('\r\n', '\n').split('\n').filter(line => {
        const field = /^(\s+)(?:sourcePaths|sourcePath|partsSourcePath):/.exec(line);
        if (field) { locationIndent = field[1].length; return false; }
        if (locationIndent !== null) {
          const indent = /^\s*/.exec(line)[0].length;
          if (indent > locationIndent || (indent === locationIndent && /^\s+- /.test(line))) return false;
          locationIndent = null;
        }
        return true;
      }).join('\n');
    };
    if (normalize(fs.readFileSync(before, 'utf8')) !== normalize(fs.readFileSync(`Assets/Assets/Resources/Player/${current}.asset`, 'utf8'))) failures.push(`Catalog metadata beyond locations changed: ${current}`);
    catalogMetadataChecks++;
  }
  if (fs.existsSync(`${output}/protected-files.json`)) {
    for (const [file, expected] of Object.entries(readJson(`${output}/protected-files.json`))) if (hash(fs.readFileSync(file)) !== expected) failures.push(`Protected wrapper or pool changed: ${file}`);
  }
  const result = {status: failures.length ? 'FAIL' : 'PASS', utc: new Date().toISOString(), payloads: manifest.files.length, folders: manifest.directories.length, catalogMetadataChecks, failures, evidence: 'Current file bytes, Unity GUIDs and complete sidecars compared with the pre-move snapshot; catalog metadata permits only source-location changes.'};
  fs.writeFileSync(`${output}/file-verification.json`, JSON.stringify(result, null, 2) + '\n');
  console.log(JSON.stringify({...result, failures: failures.slice(0, 10)}, null, 2));
  if (failures.length) process.exitCode = 1;
}
function cleanup() {
  // note: Cleanup is authorized only after both preservation checks pass and a serialized GUID-reference scan finds no users of the old empty folders.
  if (readJson(`${output}/editor-verification.json`).status !== 'PASS' || readJson(`${output}/file-verification.json`).status !== 'PASS') throw Error('Successful editor and payload verification are required before cleanup.');
  const manifest = readJson(manifestPath);
  const previous = fs.existsSync(`${output}/cleanup.json`) ? readJson(`${output}/cleanup.json`) : null;
  const archive = `${root}/Documentation/Source Folder Metadata`;
  confined(archive);
  if (fs.existsSync(archive)) {
    const references = fs.readFileSync(`${output}/folder-guid-references.txt`, 'utf8').trim();
    if (references) throw Error('Original folder GUIDs remain referenced; retain their metadata.');
    if (walk(archive).some(file => !file.endsWith('.meta'))) throw Error('Source folder archive is not empty of assets.');
    const expectedPrefix = path.resolve(project, root) + path.sep;
    if (!path.resolve(archive).startsWith(expectedPrefix)) throw Error('Invalid cleanup target.');
    fs.rmSync(archive, {recursive: true});
    if (fs.existsSync(archive + '.meta')) fs.unlinkSync(archive + '.meta');
    for (const row of manifest.directories) row.retiredUnreferencedFolder = true;
    fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n');
  } else {
    // note: Later taxonomy adjustments create new temporary snapshots; reuse the completed retirement receipt only when every old folder is already recorded as retired.
    if (previous?.status !== 'PASS' || !manifest.directories.every(row => row.retiredUnreferencedFolder)) throw Error('Missing verified source-folder retirement evidence.');
  }
  const removed = [];
  for (const file of fs.readdirSync(output)) {
    if (/^(executed-.*\.request(?:\.meta)?|.*-catalog-before\.asset|protected-files\.json|plan\.json|folder-guids\.txt|folder-guid-references\.txt)$/.test(file)) {
      const target = path.resolve(output, file);
      if (!target.startsWith(path.resolve(project, output) + path.sep)) throw Error('Invalid temporary-file target.');
      fs.unlinkSync(target); removed.push(file);
    }
  }
  const staging = `${output}/OriginalTree`;
  if (fs.existsSync(staging)) {
    if (walk(staging).length) throw Error('Recoverable assets remain in staging; retain them.');
    if (!path.resolve(staging).startsWith(path.resolve(project, output) + path.sep)) throw Error('Invalid staging target.');
    fs.rmSync(staging, {recursive: true});
  }
  const failed = `${output}/FailedLayout`;
  if (fs.existsSync(failed)) {
    if (walk(failed).some(file => !file.endsWith('.meta') && !file.endsWith('/DOT_ASSET_LAYOUT.json') && !file.endsWith('/MATERIAL_INDEX.json'))) throw Error('Unexpected files in failed-layout backup; retain them.');
    if (!path.resolve(failed).startsWith(path.resolve(project, output) + path.sep)) throw Error('Invalid failed-layout target.');
    fs.rmSync(failed, {recursive: true});
  }
  const result = {status: 'PASS', utc: new Date().toISOString(), retiredUnreferencedFolders: manifest.directories.length, removedTemporaryFiles: [...new Set([...(previous?.removedTemporaryFiles ?? []), ...removed])], sourceAssetsDeleted: 0, folderGuidReferenceScan: 'No serialized references found'};
  fs.writeFileSync(`${output}/cleanup.json`, JSON.stringify(result, null, 2) + '\n');
  console.log(JSON.stringify(result, null, 2));
}
function restore() {
  // note: Recover only the failed organization transaction: all original files must be present with their captured bytes in its staged tree.
  const manifest = readJson(`${output}/plan.json`);
  for (const row of [...manifest.files, ...manifest.directories]) {
    const staged = `${output}/OriginalTree/${row.origin.slice(root.length + 1)}`;
    if (!fs.existsSync(staged) || !fs.existsSync(staged + '.meta') || hash(fs.readFileSync(staged + '.meta')) !== row.metaSha256 || (row.sha256 && hash(fs.readFileSync(staged)) !== row.sha256)) throw Error(`Incomplete staging recovery: ${staged}`);
  }
  const remaining = walk(root).filter(file => !file.endsWith('.meta'));
  if (remaining.some(file => file !== manifestPath && !file.endsWith('/MATERIAL_INDEX.json'))) throw Error('Unrecognized files remain in failed layout; preserve them for review.');
  const failed = `${output}/FailedLayout`;
  const workspacePrefix = path.resolve(project) + path.sep;
  for (const location of [root, failed, `${output}/OriginalTree`]) if (!path.resolve(location).startsWith(workspacePrefix)) throw Error('Recovery target escapes workspace.');
  if (fs.existsSync(failed)) throw Error('Recovery backup already exists.');
  fs.renameSync(root, failed);
  fs.mkdirSync(root);
  for (const top of manifest.topFolders) {
    if (top.includes('/') || top.includes('\\') || top.includes('..')) throw Error('Invalid source folder.');
    fs.renameSync(`${output}/OriginalTree/${top}`, `${root}/${top}`);
    if (fs.existsSync(`${output}/OriginalTree/${top}.meta`)) fs.renameSync(`${output}/OriginalTree/${top}.meta`, `${root}/${top}.meta`);
  }
  console.log(JSON.stringify({status: 'RESTORED', payloads: manifest.files.length, originalFolders: manifest.directories.length, sourceAssetsDeleted: 0}, null, 2));
}
if (process.argv[2] === 'plan') plan();
else if (process.argv[2] === 'verify') verify();
else if (process.argv[2] === 'cleanup') cleanup();
else if (process.argv[2] === 'restore') restore();
else throw Error('Use plan, verify, cleanup or restore from the YourQuest project root.');
