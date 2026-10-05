#!/usr/bin/env node
/**
 * Read-only G04/DOT source reconciliation. Requires Node 18+; no packages.
 *
 * Usage:
 *   node Tools/reconcile-world-asset-library.mjs --root .
 *   node Tools/reconcile-world-asset-library.mjs --root . --check-only
 *   node Tools/reconcile-world-asset-library.mjs --root . --family-root "Assets/Forst"
 *   node Tools/reconcile-world-asset-library.mjs --root . --no-family-scan
 *
 * --output defaults to outputs/G08_EnvironmentCohesion_20261003/world-asset-library-reconciliation.json.
 * --family-root may repeat; supplying it replaces the bounded default roots.
 * --max-family-files limits metadata enumeration per root (default 100000).
 * --check-only performs the same extraction and focused checks without writing.
 * The output is investigation data derived from existing G04 authorities. It is
 * never a runtime registry, approval decision, asset importer, or runtime PASS.
 * Binary payload bytes are never read or hashed. Text source snapshots are hashed.
 */
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';

const SOURCES = {
  g04: 'Assets/Assets/GeneratedAssets/WorldIntake/YQWorldGenerationAssetInventory.json',
  dotIndex: 'Assets/Assets/GeneratedAssets/DOT Generated Assets/Catalogs/DOT_ASSET_INDEX.json',
  dotLayout: 'Assets/Assets/GeneratedAssets/DOT Generated Assets/Catalogs/DOT_ASSET_LAYOUT.json',
  equipment: 'Assets/Assets/Resources/Player/YQDotEquipmentCatalog.asset',
  creatures: 'Assets/Assets/Resources/Player/YQDotCreatureCatalog.asset',
  tracker: 'Docs/ArtDirection_Production_Tracker.md',
  itemPools: 'Assets/Assets/Resources/GeneratedRpgContentLibrary.asset',
  equipmentIntake: 'Assets/Assets/Scripts/Generated/Editor/YQDotEquipmentIntake.cs',
  creatureSelector: 'Assets/Assets/Scripts/Generated/YQDotCreatureCatalog.cs',
  worldSelector: 'Assets/Assets/Scripts/Generated/YQWorldAssetCatalog.cs',
  ecologyConsumer: 'Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs',
  siteConsumer: 'Assets/Assets/Scripts/Generated/YQRuntimeWorldSiteCatalog.cs',
  inventoryOwner: 'Assets/Assets/Scripts/Generated/Editor/YQRuntimeWorldAssetRegistryBuilder.cs'
};
const DEFAULT_FAMILIES = [
  'Assets/Grass And Flowers Pack 1',
  'Assets/GabrielAguiarProductions',
  'Assets/Forst',
  'Assets/Magic Pig Games (Infinity PBR)/Audio',
  'Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor',
  'Assets/Magic Pig Games (Infinity PBR)/Characters',
  'Assets/YourQuest DOT Creatures',
  'Assets/YourQuest DOT Equipment',
  'SourceAssets/DOT'
];
const OUTPUT_FOLDER = 'outputs/G08_EnvironmentCohesion_20261003';
const slash = value => String(value ?? '').replaceAll('\\', '/');
const normalized = value => slash(value).replace(/^\.\//, '').toLowerCase();
const hash = value => crypto.createHash('sha256').update(value).digest('hex');
const compare = (left, right) => left < right ? -1 : left > right ? 1 : 0;
const sortStrings = values => [...new Set(values.filter(Boolean))].sort(compare);

// note: Only named read-only inputs and one derived report are permitted; paths cannot escape the project.
function containedPath(root, relative) {
  const absolute = path.resolve(root, relative);
  const difference = path.relative(root, absolute);
  if (difference.startsWith('..') || path.isAbsolute(difference))
    throw new Error(`Path escapes project: ${relative}`);
  return absolute;
}

function argumentsFrom(argv) {
  const options = { root: process.cwd(), output: `${OUTPUT_FOLDER}/world-asset-library-reconciliation.json`,
    familyRoots: [], maxFamilyFiles: 100000, checkOnly: false, noFamilyScan: false };
  for (let index = 0; index < argv.length; index++) {
    const flag = argv[index];
    if (flag === '--help' || flag === '-h') { options.help = true; continue; }
    if (flag === '--check-only') { options.checkOnly = true; continue; }
    if (flag === '--no-family-scan') { options.noFamilyScan = true; continue; }
    if (!['--root', '--output', '--family-root', '--max-family-files'].includes(flag))
      throw new Error(`Unknown argument: ${flag}`);
    const value = argv[++index];
    if (!value || value.startsWith('--')) throw new Error(`Missing value for ${flag}`);
    if (flag === '--root') options.root = path.resolve(value);
    if (flag === '--output') options.output = value;
    if (flag === '--family-root') options.familyRoots.push(slash(value));
    if (flag === '--max-family-files') options.maxFamilyFiles = Number(value);
  }
  if (!Number.isSafeInteger(options.maxFamilyFiles) || options.maxFamilyFiles < 1)
    throw new Error('--max-family-files must be a positive integer.');
  if (options.familyRoots.length === 0) options.familyRoots = [...DEFAULT_FAMILIES];
  options.familyRoots = sortStrings(options.familyRoots);
  const reportRelative = slash(path.relative(options.root, containedPath(options.root, options.output)));
  if (!reportRelative.startsWith(`${OUTPUT_FOLDER}/`) || !reportRelative.endsWith('.json'))
    throw new Error(`--output must be a JSON report inside ${OUTPUT_FOLDER}.`);
  return options;
}

function countBy(rows, selector) {
  const result = new Map();
  for (const row of rows) {
    const key = String(selector(row) ?? 'unknown');
    result.set(key, (result.get(key) ?? 0) + 1);
  }
  return Object.fromEntries([...result].sort(([left], [right]) => compare(left, right)));
}

function scalar(value) {
  const trimmed = value.trim();
  if (trimmed.startsWith("'") && trimmed.endsWith("'"))
    return trimmed.slice(1, -1).replaceAll("''", "'");
  if (trimmed.startsWith('"') && trimmed.endsWith('"')) {
    try { return JSON.parse(trimmed); } catch { throw new Error('Unsupported quoted YAML scalar.'); }
  }
  return trimmed;
}

// note: This parser supports the inspected Unity record shape only and ignores embedded contract JSON.
// Paths wrapped by Unity and top-level sequence boundaries receive explicit focused checks below.
function unityRecords(text, key, scalarFields, listFields = []) {
  const records = [];
  let record = null, activeField = null, activeList = false;
  const wantedScalars = new Set([key, ...scalarFields]);
  const wantedLists = new Set(listFields);
  for (const line of text.replace(/^\uFEFF/, '').split(/\r?\n/)) {
    const start = new RegExp(`^  - ${key}:\\s?(.*)$`).exec(line);
    if (start) {
      record = { [key]: scalar(start[1]) };
      records.push(record); activeField = null; continue;
    }
    if (!record) continue;
    if (/^  [A-Za-z_][\w]*:/.test(line)) { record = null; activeField = null; continue; }
    const field = /^    ([A-Za-z_][\w]*):(?: (.*))?$/.exec(line);
    if (field) {
      activeField = field[1]; activeList = wantedLists.has(activeField);
      if (activeList) record[activeField] = field[2]?.trim() === '[]' ? [] : [];
      else if (wantedScalars.has(activeField)) record[activeField] = scalar(field[2] ?? '');
      else activeField = null;
      continue;
    }
    if (!activeField) continue;
    const item = /^    - (.*)$/.exec(line);
    if (item && activeList) { record[activeField].push(scalar(item[1])); continue; }
    const continuation = /^      (\S.*)$/.exec(line);
    if (!continuation) continue;
    if (activeList) {
      const values = record[activeField];
      if (values.length === 0) throw new Error(`Orphan YAML continuation: ${activeField}`);
      values[values.length - 1] += ` ${continuation[1].trim()}`;
    } else record[activeField] += ` ${continuation[1].trim()}`;
  }
  return records;
}

function topLevelLists(text, fields) {
  const result = Object.fromEntries(fields.map(field => [field, []]));
  let field = null;
  for (const line of text.split(/\r?\n/)) {
    const start = /^  ([A-Za-z_][\w]*):/.exec(line);
    if (start) { field = fields.includes(start[1]) ? start[1] : null; continue; }
    if (!field) continue;
    const item = /^  - (.*)$/.exec(line);
    if (item) result[field].push(scalar(item[1]));
    else if (/^    \S/.test(line) && result[field].length > 0)
      result[field][result[field].length - 1] += ` ${line.trim()}`;
  }
  return result;
}

function focusedParserChecks() {
  const fixture = `  entries:\n  - assetId: sample\n    sourcePaths:\n    - Assets/Pack/Affinity\n      Sets/sample.glb\n    sourceHashes:\n    - abc\n    prefabPath: 'Assets/Owned/It''s Sample.prefab'\n    ignoredContract: '{"assetId":"wrong"}'\n    generationEligible: 0\n  craftingMaterials:\n  - assetId: must_not_be_an_entry\n`;
  const records = catalogEntries(fixture, ['prefabPath', 'generationEligible'], ['sourcePaths', 'sourceHashes']);
  // note: Catalog record extraction is stopped by its owning sibling section, preventing a second domain from being counted.
  assert.equal(records.length, 1);
  assert.equal(records[0].sourcePaths[0], 'Assets/Pack/Affinity Sets/sample.glb');
  assert.equal(records[0].prefabPath, "Assets/Owned/It's Sample.prefab");
  assert.equal(records[0].generationEligible, '0');
  assert.equal(topLevelLists('  weaponPrefabKeys:\n  - Assets/Long\n    Path/test.prefab\n  other: []', ['weaponPrefabKeys']).weaponPrefabKeys[0], 'Assets/Long Path/test.prefab');
  assert.equal(normalized('Assets\\Example.prefab'), normalized('Assets/Example.prefab'));
  const joined = buildSourceJoin({ assets: [{ assetPath: 'Assets/base.glb', sourceGuid: 'guid-a' }] },
    { files: [{ path: 'Assets/base.glb', guid: 'guid-a', sha256: 'shared-hash' },
      { path: 'Assets/base_LOD1.glb', guid: 'guid-b', sha256: 'shared-hash' }] },
    { files: [{ path: 'Assets/base.glb', guid: 'guid-a', aliases: ['Assets/old-base.glb'] }] });
  assert.equal(joined.nodes.length, 2, 'Overlapping source snapshots must not add another file identity.');
  assert.equal(joined.resolve('Assets/old-base.glb', 'shared-hash').method, 'layout_alias');
  assert.equal(joined.resolve('Assets/unlisted.glb', 'shared-hash').state, 'ambiguous',
    'Equal bytes across distinct GUIDs must not silently merge asset identities.');
  return ['wrapped Unity source paths', 'quoted Unity scalar escapes', 'literal eligibility values',
    'owning catalog section boundary', 'wrapped production pool paths', 'path separator identity',
    'path/GUID cross-snapshot deduplication', 'layout alias joins', 'ambiguous supplied-hash preservation'];
}

function catalogEntries(text, scalarFields, listFields = []) {
  const entryStart = /^  entries:\s*$/m.exec(text);
  if (!entryStart) throw new Error('Unity catalog entries section was not found.');
  const tail = text.slice(entryStart.index + entryStart[0].length);
  const nextSection = /^  [A-Za-z_][\w]*:/m.exec(tail);
  const section = nextSection ? tail.slice(0, nextSection.index) : tail;
  const records = unityRecords(section, 'assetId', scalarFields, listFields);
  const expected = (section.match(/^  - assetId:/gm) ?? []).length;
  assert.equal(records.length, expected, 'All catalog identities must be extracted.');
  assert.ok(records.length > 0, 'Catalog cannot be silently treated as empty.');
  return records;
}

function supportedPool(entry) {
  // note: These names mirror the inspected intake mapping; a source assertion below detects contract drift.
  if (['Weapons', 'Special_Weapons', 'weapon_assembly'].includes(entry.category))
    return entry.family === 'arrow' ? null : 'weaponPrefabKeys';
  if (entry.family === 'shield') return 'offhandPrefabKeys';
  if (entry.category === 'Consumables') return 'consumablePrefabKeys';
  if (entry.family === 'ring') return 'ringPrefabKeys';
  if (entry.family === 'amulet') return 'necklacePrefabKeys';
  if (entry.category === 'Accessories') return 'trinketPrefabKeys';
  return null;
}

function approvalEvidence(text) {
  const lines = text.split(/\r?\n/);
  const definitions = [
    { version: 'Fairy v4', filename: 'YourQuest_Fairy_v4_REVIEW_ONLY_UNAPPROVED_2026-10-03.zip' },
    { version: 'Avian V6', filename: 'YourQuest_Avian_V6_REVIEW_ONLY_UNAPPROVED_2026-10-03.zip' },
    { version: 'Kitsune v12 runtime', filename: 'YourQuest_Kitsune_AdultBase_v12_Runtime.zip' },
    { version: 'Kitsune v12 editable modules', filename: 'YourQuest_Kitsune_AdultBase_v12_Editable_Modules.zip' }
  ];
  return definitions.map(definition => {
    const index = lines.findIndex(line => line.includes(definition.filename));
    if (index < 0) return { ...definition, state: 'unknown', missingEvidence: true };
    let headerIndex = index;
    while (headerIndex >= 0 && !lines[headerIndex].startsWith('**')) headerIndex--;
    const header = headerIndex >= 0 ? lines[headerIndex] : '';
    const state = /Withdrawn from delivery/i.test(header) ? 'withdrawn_from_delivery'
      : /HELD/i.test(header) ? 'held_delivery_not_allowed' : 'unknown';
    return { ...definition, state, source: SOURCES.tracker, line: index + 1,
      archiveSha256: /(?:SHA-256|sha256)[ `]*([a-f0-9]{64})/i.exec(lines[index])?.[1] ?? null,
      headerLine: headerIndex + 1, header, evidence: lines[index] };
  });
}

function snapshotReader(root) {
  const snapshots = [], texts = new Map();
  return { snapshots, texts, read(label, relative) {
    const absolute = containedPath(root, relative);
    const before = fs.statSync(absolute);
    const bytes = fs.readFileSync(absolute);
    const after = fs.statSync(absolute);
    assert.ok(before.size === after.size && before.mtimeMs === after.mtimeMs && bytes.length === after.size,
      `Source changed while reading: ${relative}`);
    const text = bytes.toString('utf8').replace(/^\uFEFF/, '');
    snapshots.push({ label, path: slash(relative), sha256: hash(bytes), bytes: bytes.length,
      modifiedUtc: before.mtime.toISOString(), identityBasis: 'actual text bytes read; not payload verification' });
    texts.set(label, text); return text;
  } };
}

function sourceModelClass(relative) {
  const lower = relative.toLowerCase();
  if (/\/(lods|lod)\//.test(lower) || /_lod[0-9]+(?:\.|_)/.test(lower)) return 'lod_variant';
  if (/\/(parts|part bank|modular|modules)\//.test(lower)) return 'module_or_part_source';
  return 'source_model_role_unknown_until_manifest_join';
}

// note: File identity and runtime binding identity remain separate so dependencies and LODs never inflate placeable variety.
function buildSourceJoin(g04, dotIndex, layout) {
  const nodes = [], byPath = new Map(), byGuid = new Map(), byHash = new Map();
  const conflicts = [];
  function attach(map, key, node) {
    if (!key) return;
    const values = map.get(key) ?? new Set(); values.add(node); map.set(key, values);
  }
  function one(map, key) {
    const values = map.get(key); return values?.size === 1 ? [...values][0] : null;
  }
  function add(row, source) {
    const relative = slash(row.assetPath ?? row.path);
    const guid = String(row.sourceGuid ?? row.guid ?? '').toLowerCase();
    const existingPath = one(byPath, normalized(relative));
    const existingGuid = one(byGuid, guid);
    if (existingPath && existingGuid && existingPath !== existingGuid)
      conflicts.push({ kind: 'path_guid_disagreement', path: relative, guid });
    const node = existingPath ?? existingGuid ?? { index: nodes.length, paths: [], guids: [], hashes: [], sources: [], families: [] };
    if (node.index === nodes.length) nodes.push(node);
    node.paths = sortStrings([...node.paths, relative]);
    node.guids = sortStrings([...node.guids, guid]);
    node.hashes = sortStrings([...node.hashes, row.sha256]);
    node.sources = sortStrings([...node.sources, source]);
    node.families = sortStrings([...node.families, row.assetFamily ?? row.category]);
    attach(byPath, normalized(relative), node); attach(byGuid, guid, node);
    if (row.sha256) attach(byHash, row.sha256.toLowerCase(), node);
    return node;
  }
  for (const row of g04.assets) add(row, 'g04_master');
  for (const row of dotIndex.files) add(row, 'dot_source_index');
  const layoutUnindexed = [], aliasConflicts = [];
  for (const row of layout.files) {
    const target = one(byPath, normalized(row.path)) ?? one(byGuid, String(row.guid ?? '').toLowerCase());
    if (!target) { layoutUnindexed.push(row.path); continue; }
    target.sources = sortStrings([...target.sources, 'dot_source_layout']);
    target.families = sortStrings([...target.families, row.pack]);
    for (const alias of [row.origin, ...(row.aliases ?? [])].filter(Boolean)) {
      const prior = one(byPath, normalized(alias));
      if (prior && prior !== target) aliasConflicts.push({ alias, target: row.path });
      attach(byPath, normalized(alias), target);
    }
    if (row.sha256) {
      target.hashes = sortStrings([...target.hashes, row.sha256]);
      attach(byHash, row.sha256.toLowerCase(), target);
    }
  }
  function resolve(relative, suppliedHash) {
    const pathMatches = byPath.get(normalized(relative));
    if (pathMatches?.size === 1) {
      const node = [...pathMatches][0];
      return { state: 'matched', method: node.paths.some(value => normalized(value) === normalized(relative)) ? 'source_path' : 'layout_alias',
        canonicalPaths: node.paths, guids: node.guids, suppliedHashMatches: suppliedHash ? node.hashes.includes(suppliedHash) : null };
    }
    if (pathMatches?.size > 1) return { state: 'ambiguous', method: 'path_or_alias', candidates: pathMatches.size };
    const hashes = suppliedHash ? byHash.get(suppliedHash.toLowerCase()) : null;
    if (hashes?.size === 1) {
      const node = [...hashes][0];
      return { state: 'matched', method: 'unique_supplied_source_hash', canonicalPaths: node.paths, guids: node.guids, suppliedHashMatches: true };
    }
    if (hashes?.size > 1) return { state: 'ambiguous', method: 'shared_supplied_source_hash', candidates: hashes.size };
    return { state: 'unmatched', method: 'none' };
  }
  return { nodes, byPath, byGuid, byHash, resolve, conflicts, aliasConflicts, layoutUnindexed };
}

function readDotRegistryReferences(root, reader) {
  const folder = 'Assets/Assets/Resources/YQWorldAssetShards';
  const files = fs.readdirSync(containedPath(root, folder)).filter(name =>
    /^YQWorldAssets_yourquest_dot_(?:creatures|equipment)_.+\.asset$/i.test(name)).sort(compare);
  const byKey = new Map();
  // note: Existing registry keys are durable bindings even when wrapper files move; serialized GUIDs bridge that distinction.
  for (const file of files) {
    const relative = `${folder}/${file}`;
    const text = reader.read(`dotRegistry:${file}`, relative);
    const rows = unityRecords(text, 'assetPath', ['prefab']);
    for (const row of rows) {
      const guid = /\bguid: ([a-f0-9]{32})\b/i.exec(row.prefab ?? '')?.[1]?.toLowerCase();
      if (!guid) continue;
      const key = normalized(row.assetPath), values = byKey.get(key) ?? [];
      values.push({ guid, source: relative, serializedKey: row.assetPath }); byKey.set(key, values);
    }
  }
  return { byKey, shardFiles: files.length };
}

function sourceFamilies(g04) {
  const groups = new Map();
  for (const row of g04.assets) {
    const key = row.sourceRoot || 'unknown';
    const rows = groups.get(key) ?? []; rows.push(row); groups.set(key, rows);
  }
  return [...groups].sort(([a], [b]) => compare(a, b)).map(([root, rows]) => ({ root,
    sourceRecords: rows.length, types: countBy(rows, row => row.assetType),
    sourceFinalStates: countBy(rows, row => row.finalState),
    sourceReviewDispositions: countBy(rows, row => row.reviewDisposition),
    eligibilityBasis: 'existing G04 fields only; not renewed by this report' }));
}

function scanFamily(root, relative, maximum, sourceJoin, bindingPrefabPaths, bindingPrefabGuids, physicalPrefabPathsByGuid, reader) {
  const absolute = containedPath(root, relative);
  if (!fs.existsSync(absolute)) return { root: relative, availability: 'missing', examined: false };
  const pending = [absolute], files = [], skippedLinks = [], errors = [];
  let capped = false;
  // note: Enumerate names and extensions only; vendor/source binaries are not loaded, hashed, or uploaded.
  while (pending.length > 0 && !capped) {
    const directory = pending.pop();
    let entries;
    try { entries = fs.readdirSync(directory, { withFileTypes: true }); }
    catch (error) { errors.push({ path: slash(path.relative(root, directory)), reason: error.code }); continue; }
    entries.sort((a, b) => compare(a.name, b.name));
    for (const entry of entries) {
      const full = path.join(directory, entry.name);
      const name = slash(path.relative(root, full));
      if (entry.isSymbolicLink()) { skippedLinks.push(name); continue; }
      if (entry.isDirectory()) pending.push(full);
      else if (entry.isFile() && !name.endsWith('.meta')) {
        if (files.length === maximum) { capped = true; break; }
        files.push(name);
      }
    }
  }
  files.sort(compare);
  const currentGuids = new Map();
  for (const file of files.filter(file => file.toLowerCase().endsWith('.prefab'))) {
    const meta = `${file}.meta`;
    if (!fs.existsSync(containedPath(root, meta))) continue;
    const text = reader.read(`prefabGuid:${file}`, meta);
    const guid = /^guid: ([a-f0-9]{32})\s*$/m.exec(text)?.[1]?.toLowerCase();
    if (!guid) continue;
    currentGuids.set(normalized(file), guid);
    const paths = physicalPrefabPathsByGuid.get(guid) ?? new Set(); paths.add(file); physicalPrefabPathsByGuid.set(guid, paths);
  }
  const sourceCovered = file => sourceJoin.byPath.has(normalized(file)) ||
    sourceJoin.byGuid.has(currentGuids.get(normalized(file)));
  const domainCoveredByIdentity = file => bindingPrefabPaths.has(normalized(file)) ||
    bindingPrefabGuids.has(currentGuids.get(normalized(file)));
  const uncovered = files.filter(file => !sourceCovered(file));
  const covered = files.filter(sourceCovered);
  const domainCovered = files.filter(domainCoveredByIdentity);
  const outsideAll = uncovered.filter(file => !domainCoveredByIdentity(file));
  return { root: relative, availability: 'present', examined: !capped && errors.length === 0 && skippedLinks.length === 0,
    examination: 'bounded path/extension enumeration; no asset/material/visual inspection', capped, errors, skippedLinks,
    fileCountExcludingMeta: files.length, extensions: countBy(files, file => path.posix.extname(file).toLowerCase() || '(none)'),
    cataloguedByG04OrDotFileIdentity: covered.length, outsideSourceSnapshots: uncovered.length,
    outsideSourceSnapshotsSample: uncovered.slice(0, 12),
    cataloguedByDomainBindingPrefabPathOrRegistryGuid: domainCovered.length,
    outsideAllSuppliedCatalogs: outsideAll.length,
    outsideAllSuppliedCatalogsSample: outsideAll.slice(0, 12),
    accountingNote: 'A wrapper may be absent from source indexes and present in a domain binding by stable key or registry GUID; unmatched files remain unknown rather than excluded.',
    approvalState: 'unknown unless supplied by a joined authoritative record',
    eligibilityState: 'unknown unless supplied by a joined authoritative record' };
}

function evidenceGaps(g04, index, layout, equipment, creatures) {
  return [
    { id: 'approval_provenance', owner: SOURCES.inventoryOwner,
      gap: 'Master records provide disposition/policy, without explicit approval source/date/payload version or license location.',
      affectedRecords: g04.assets.filter(row => !row.approvalSource || !row.approvalDate || !row.licenseLocation).length,
      decision: 'unknown; preserve source states and obtain exact approval/provenance links' },
    { id: 'dot_source_approval', owner: SOURCES.dotLayout,
      gap: 'DOT source index/layout provide file/package identity, without a universal approval field or production consumer.',
      indexRecordsWithoutExplicitApproval: index.files.filter(row => !row.approvalSource && !row.approvalState).length,
      layoutRecordsWithoutExplicitApproval: layout.files.filter(row => !row.approvalSource && !row.approvalState).length,
      decision: 'source presence and checksums do not approve assets' },
    { id: 'dot_equipment_approval', owner: SOURCES.equipment,
      gap: 'generationEligible and eligibilityReason are existing technical/domain admission; catalog lacks explicit approval source/date.',
      affectedRecords: equipment.length, decision: 'copy eligibility literally; approval remains unknown here' },
    { id: 'dot_creature_approval', owner: SOURCES.creatures,
      gap: 'Catalog entries bind kind/species/source hashes and module interfaces; no per-entry approval state or eligibility flag.',
      affectedRecords: creatures.length, decision: 'copy installed binding facts; approval and generation eligibility remain unknown here' },
    { id: 'actual_production_uses', owner: SOURCES.ecologyConsumer,
      gap: 'This extractor can identify source/catalog/pool references, but executes no materialization, traversal, rendering or persistence flow.',
      decision: 'production use NOT VERIFIED' },
    { id: 'g04_specific_consumer_links', owner: SOURCES.inventoryOwner,
      gap: 'Existing registry/palette status fields do not identify the exact palette and production consumer for each file record.',
      recordsWithoutExplicitConsumerLinks: g04.assets.filter(row => !row.runtimeConsumer && !row.runtimeConsumers).length,
      decision: 'preserve existing assignment states; add source-linked consumer coverage at the G04 projection boundary' },
    { id: 'legacy_terminal_policy', owner: `${SOURCES.inventoryOwner}:7191`,
      gap: 'Existing master converts missing curation into exclusion. Expanded coverage needs specific family/role review of those unchanged exclusions.',
      sourceExcludedUnreviewedContext: g04.assets.filter(row => row.finalState === 'intentionally_excluded_unreviewed_context').length,
      decision: 'source exclusion preserved; expanded justification unknown' }
  ];
}

function duplicateIdentities(rows, key) {
  return Object.entries(countBy(rows, row => row[key])).filter(([, count]) => count > 1)
    .map(([identity, count]) => ({ identity, count }));
}

function main() {
  const options = argumentsFrom(process.argv.slice(2));
  if (options.help) {
    process.stdout.write(fs.readFileSync(new URL(import.meta.url), 'utf8').split(' */')[0].replace(/^#![^\n]*\n/, '') + '\n');
    return;
  }
  const parserChecks = focusedParserChecks();
  const reader = snapshotReader(options.root);
  reader.read('extractor', 'Tools/reconcile-world-asset-library.mjs');
  for (const [label, relative] of Object.entries(SOURCES)) reader.read(label, relative);
  const g04 = JSON.parse(reader.texts.get('g04'));
  const dotIndex = JSON.parse(reader.texts.get('dotIndex'));
  const layout = JSON.parse(reader.texts.get('dotLayout'));
  assert.ok(Array.isArray(g04.assets) && Array.isArray(dotIndex.files) && Array.isArray(layout.files));
  const equipment = catalogEntries(reader.texts.get('equipment'), ['family', 'category', 'referenceBody', 'moduleSlot',
    'compatibilityId', 'sourcePath', 'partsSourcePath', 'sourceSha256', 'prefabPath', 'assembled', 'generationEligible', 'eligibilityReason']);
  const creatures = catalogEntries(reader.texts.get('creatures'), ['kind', 'species', 'category', 'bodyForm',
    'compatibilityId', 'moduleSlot', 'prefabPath', 'requiresSlotReplacement'], ['sourcePaths', 'sourceHashes']);
  const poolNames = ['weaponPrefabKeys', 'offhandPrefabKeys', 'consumablePrefabKeys', 'ringPrefabKeys', 'necklacePrefabKeys', 'trinketPrefabKeys'];
  for (const name of poolNames) assert.ok(reader.texts.get('equipmentIntake').includes(`"${name}"`), `Intake consumer changed: ${name}`);
  const pools = topLevelLists(reader.texts.get('itemPools'), poolNames);
  const sourceJoin = buildSourceJoin(g04, dotIndex, layout);
  const registryReferences = readDotRegistryReferences(options.root, reader);
  const matches = [], bindingRows = [];
  function link(binding, relative, suppliedHash, role) {
    const joined = sourceJoin.resolve(relative, suppliedHash);
    const result = { suppliedPath: relative, suppliedSha256: suppliedHash || null, sourceRole: role, ...joined };
    matches.push({ domain: binding.domain, id: binding.assetId, ...result }); return result;
  }
  for (const entry of equipment) {
    const binding = { domain: 'equipment', assetId: entry.assetId, prefabPath: entry.prefabPath,
      family: entry.family, category: entry.category, moduleSlot: entry.moduleSlot || null,
      compatibilityId: entry.compatibilityId || null,
      approvalState: 'unknown_in_catalog',
      sourceEligibility: entry.generationEligible === '1' ? true : entry.generationEligible === '0' ? false : null,
      sourceEligibilityReason: entry.eligibilityReason || null,
      sourceEligibilityEvidence: SOURCES.equipment, sources: [] };
    if (entry.sourcePath) binding.sources.push(link(binding, entry.sourcePath, entry.sourceSha256, 'declared_source'));
    if (entry.partsSourcePath) binding.sources.push(link(binding, entry.partsSourcePath, null, 'declared_parts_source'));
    const pool = supportedPool(entry);
    binding.consumerEvidence = { expectedPoolByExistingIntake: pool,
      presentInExistingProductionPool: pool ? pools[pool].some(value => normalized(value) === normalized(entry.prefabPath)) : null,
      source: SOURCES.itemPools, mappingSource: `${SOURCES.equipmentIntake}:426`, runtimeUse: 'NOT VERIFIED' };
    bindingRows.push(binding);
  }
  for (const entry of creatures) {
    const paths = entry.sourcePaths ?? [], hashes = entry.sourceHashes ?? [];
    assert.equal(paths.length, hashes.length, `Creature source/hash arity: ${entry.assetId}`);
    const binding = { domain: 'creature', assetId: entry.assetId, prefabPath: entry.prefabPath,
      kind: entry.kind, species: entry.species, category: entry.category,
      moduleSlot: entry.moduleSlot || null, compatibilityId: entry.compatibilityId || null,
      approvalState: 'unknown_in_catalog', sourceEligibility: null,
      sourceEligibilityReason: 'Catalog declares no eligibility field.', sourceEligibilityEvidence: SOURCES.creatures,
      sources: [], consumerEvidence: { source: SOURCES.creatureSelector,
        declaredSelectorKind: entry.kind, declaredSelectorSpecies: entry.species,
        moduleInterface: entry.moduleSlot ? entry.compatibilityId || 'unknown' : null,
        runtimeUse: 'NOT VERIFIED' } };
    for (let index = 0; index < paths.length; index++) binding.sources.push(link(binding, paths[index], hashes[index],
      index === 0 ? 'declared_primary_source' : 'declared_additional_source_not_independent_binding'));
    bindingRows.push(binding);
  }
  bindingRows.sort((left, right) => compare(`${left.domain}:${left.assetId}`, `${right.domain}:${right.assetId}`));
  for (const binding of bindingRows) {
    const rows = registryReferences.byKey.get(normalized(binding.prefabPath)) ?? [];
    binding.registryReferenceEvidence = { referenceGuids: sortStrings(rows.map(row => row.guid)),
      shardSources: sortStrings(rows.map(row => row.source)),
      source: 'existing serialized lazy registry shards', runtimeUse: 'NOT VERIFIED' };
  }
  const withdrawn = approvalEvidence(reader.texts.get('tracker'));
  for (const record of withdrawn) {
    const familyPattern = record.version.startsWith('Kitsune') ? /kitsune.*v12/i
      : record.version.startsWith('Fairy') ? /fairy.*v4/i : /avian.*v6/i;
    record.matchingCurrentSourceIndexPaths = dotIndex.files.filter(row => familyPattern.test(row.path)).map(row => row.path);
    record.matchingCurrentBindingIdentities = bindingRows.filter(row => familyPattern.test(`${row.assetId} ${row.prefabPath}`))
      .map(row => `${row.domain}:${row.assetId}`);
    record.interpretation = 'recorded approval/version evidence; no external archive availability or runtime selection audit';
  }
  const bindingPrefabPaths = new Set(bindingRows.map(row => normalized(row.prefabPath)).filter(Boolean));
  const bindingPrefabGuids = new Set(bindingRows.flatMap(row => row.registryReferenceEvidence.referenceGuids));
  const physicalPrefabPathsByGuid = new Map();
  const families = options.noFamilyScan ? [] : options.familyRoots.map(relative => scanFamily(options.root, relative,
    options.maxFamilyFiles, sourceJoin, bindingPrefabPaths, bindingPrefabGuids, physicalPrefabPathsByGuid, reader));
  for (const binding of bindingRows) {
    const guids = binding.registryReferenceEvidence.referenceGuids;
    const actualPaths = sortStrings(guids.flatMap(guid => [...(physicalPrefabPathsByGuid.get(guid) ?? [])]));
    binding.physicalPrefabEvidence = { actualPaths, state: options.noFamilyScan ? 'not_examined'
      : guids.length !== 1 ? 'registry_reference_missing_or_ambiguous'
      : actualPaths.length === 1 ? 'current_prefab_meta_guid_matches_registry'
      : actualPaths.length === 0 ? 'not_found_in_examined_family_roots' : 'duplicate_physical_prefab_guid',
      meaning: 'static GUID/file linkage, not import, material, collider or runtime verification' };
  }
  const sourceChanges = reader.snapshots.filter(snapshot => {
    const current = fs.statSync(containedPath(options.root, snapshot.path));
    return current.size !== snapshot.bytes || current.mtime.toISOString() !== snapshot.modifiedUtc;
  }).map(snapshot => snapshot.path);
  assert.equal(sourceChanges.length, 0, `Source changed during extraction: ${sourceChanges.join(', ')}`);
  const sourceCountMatches = g04.counts?.total === g04.assets.length;
  const dotCountMatches = dotIndex.fileCount === dotIndex.files.length;
  assert.ok(sourceCountMatches, 'G04 declared source count must match emitted records.');
  assert.ok(dotCountMatches, 'DOT index declared count must match emitted records.');
  const duplicatePayloadGroups = [...sourceJoin.byHash].filter(([, nodes]) => nodes.size > 1)
    .map(([sha256, nodes]) => ({ sha256, distinctPathOrGuidIdentities: nodes.size, paths: sortStrings([...nodes].flatMap(node => node.paths)) }))
    .sort((left, right) => compare(left.sha256, right.sha256));
  const report = { schema: 'yourquest.world-asset-library-reconciliation.v1',
    generatedUtc: new Date().toISOString(), authority: 'Derived investigation data; existing G04 catalogs remain canonical.',
    evidenceLevel: 'STATIC EXTRACTION ONLY', legacyAcceptance: 'Existing user-accepted forced PASS is unchanged; this report does not recertify or reopen legacy goals.',
    scope: { sourceSnapshotReconciliation: true, narrowAdditionalFamilyMetadata: !options.noFamilyScan,
      fullAvailableLibraryExamined: false, artApprovalPerformed: false, runtimeVerificationPerformed: false,
      binaryPayloadsReadOrHashed: false, runtimeCatalogsModified: false },
    invocation: { root: options.root, output: slash(options.output), familyRoots: options.noFamilyScan ? [] : options.familyRoots,
      maxFamilyFiles: options.maxFamilyFiles, reproducibleCommand: 'node Tools/reconcile-world-asset-library.mjs --root .',
      deterministicContentNote: 'Record ordering and counts are deterministic for unchanged inputs; generated time and filesystem metadata are observational.' },
    sourceSnapshots: reader.snapshots,
    existingG04: { schema: g04.schemaVersion, generatedUtc: g04.generatedUtc, reviewPolicyVersion: g04.reviewPolicyVersion,
      sourceCounts: g04.counts, actualRecords: g04.assets.length, sourceTypes: countBy(g04.assets, row => row.assetType),
      sourceStates: countBy(g04.assets, row => row.finalState), sourceFamilies: sourceFamilies(g04),
      sourceRegistryStates: countBy(g04.assets, row => row.registryState),
      sourcePaletteAssignmentStates: countBy(g04.assets, row => row.paletteAssignmentStatus),
      configuredRoots: g04.approvedRoots, configuredRootMeaning: 'existing discovery boundary; not independent approval evidence for every contained payload' },
    dotSourceSnapshot: { indexSchema: dotIndex.schema, indexUtc: dotIndex.utc, indexFiles: dotIndex.files.length,
      layoutSchema: layout.schema, layoutUtc: layout.updatedUtc ?? layout.utc, layoutFiles: layout.files.length,
      indexCategories: countBy(dotIndex.files, row => row.category), layoutPackages: countBy(layout.files, row => row.pack),
      provenanceLicenseAndApprovalDocumentCandidates: dotIndex.files.filter(row =>
        /(?:^|\/)[^/]*(?:provenance|license|approval|release_summary)[^/]*$/i.test(row.path))
        .map(row => ({ path: row.path, sha256: row.sha256, guid: row.guid })),
      documentCandidateCaveat: 'Candidate file names identify investigation inputs; their presence does not establish approval or licensing terms.',
      modelFileRolesByPathConvention: countBy(dotIndex.files.filter(row => ['.glb', '.fbx', '.obj', '.blend'].includes(path.posix.extname(row.path).toLowerCase())), row => sourceModelClass(row.path)),
      roleClassificationCaveat: 'Path roles are accounting labels only; filenames provide no approval, mechanic, or placeability evidence.' },
    reconciliation: { inputG04AndDotFileRecords: g04.assets.length + dotIndex.files.length,
      canonicalFileIdentitiesByPathOrGuid: sourceJoin.nodes.length,
      overlappingSourceSnapshots: sourceJoin.nodes.filter(node => node.sources.includes('g04_master') && node.sources.includes('dot_source_index')).length,
      distinctFileIdentitiesWithSameSuppliedHashGroups: duplicatePayloadGroups.length,
      duplicatePayloadGroups, sourceIdentityConflicts: sourceJoin.conflicts, aliasConflicts: sourceJoin.aliasConflicts,
      layoutRowsUnmatchedToIndex: sourceJoin.layoutUnindexed,
      uniqueCatalogBindingIdentities: new Set(bindingRows.map(row => `${row.domain}:${row.assetId}`)).size,
      duplicateEquipmentIds: duplicateIdentities(equipment, 'assetId'), duplicateCreatureIds: duplicateIdentities(creatures, 'assetId'),
      equipmentBindings: equipment.length, equipmentExistingEligibility: countBy(equipment, row => row.generationEligible === '1' ? 'true' : row.generationEligible === '0' ? 'false' : 'unknown'),
      creatureBindings: creatures.length, creatureKinds: countBy(creatures, row => row.kind), creatureSpecies: countBy(creatures, row => row.species),
      sourceLinks: matches.length, sourceLinkStates: countBy(matches, row => row.state), sourceLinkMethods: countBy(matches, row => row.method),
      uniqueMatchedFileIdentitiesReferencedByBindings: new Set(matches.filter(row => row.state === 'matched')
        .map(row => row.canonicalPaths.join('|'))).size,
      sourceLinkCountMeaning: 'Relationship count; repeated module references and LOD sources are not additional placeable bindings.',
      examinedDotRegistryShardFiles: registryReferences.shardFiles,
      physicalPrefabEvidenceStates: countBy(bindingRows, row => row.physicalPrefabEvidence.state),
      missingOrAmbiguousPhysicalPrefabBindings: bindingRows.filter(row => row.physicalPrefabEvidence.state !== 'current_prefab_meta_guid_matches_registry')
        .map(row => ({ domain: row.domain, assetId: row.assetId, stableKey: row.prefabPath, ...row.physicalPrefabEvidence })),
      sourceHashMismatchLinks: matches.filter(row => row.suppliedHashMatches === false),
      unmatchedOrAmbiguousSourceLinks: matches.filter(row => row.state !== 'matched'),
      equipmentEligibleMissingExpectedProductionPool: bindingRows.filter(row => row.domain === 'equipment' && row.sourceEligibility === true && row.consumerEvidence.presentInExistingProductionPool !== true).map(row => row.assetId),
      productionPoolReferenceCounts: Object.fromEntries(Object.entries(pools).map(([name, rows]) => [name, rows.length])),
      independentPlaceableAssetTotal: null,
      independentPlaceableAssetTotalReason: 'Canonical file identities, dependencies, LOD variants, modules and domain bindings are different units; the required approval/role join is incomplete.' },
    additionalFamilyMetadata: families, approvalVersionEvidence: withdrawn,
    evidenceGaps: evidenceGaps(g04, dotIndex, layout, equipment, creatures),
    bindings: bindingRows,
    staticExtractionChecks: { result: 'PASS', meaning: 'Only parser, record arithmetic, source/hash arity and stable source-read checks listed here.',
      checks: [...parserChecks, 'all source counts reconciled with arrays', 'all creature source/hash arrays aligned',
        'all catalog record identities accounted for', 'production pool names matched to intake source', 'source snapshots unchanged during read'],
      doesNotEstablish: ['art approval', 'full-library coverage', 'independent unique placeable totals', 'registry-order invariance', 'runtime use', 'G08 completion'] },
    nextCatalogIntegrationBoundary: { owner: SOURCES.inventoryOwner,
      action: 'Extend the existing inventory emitter and intake approval/provenance fields to ingest the DOT index/layout/domain catalogs and newly examined family roots; derive downstream views from that G04 authority.',
      preserve: ['source approval states', 'GUIDs', 'source payloads', 'accepted bindings', 'legacy acceptance', 'runtime ownership'],
      runtimeSelectionChange: 'Separate later reviewed slice with its own affected verification.' },
    limitations: ['Source hashes are compared to supplied hashes, not freshly verified against binary payload bytes.',
      'External Library archives, license stores and vendor sources outside named roots were not accessed.',
      'Family enumeration identifies source-coverage gaps but does not approve or exclude those sources.',
      'A present production pool reference is static reachability evidence only.',
      'Withdrawn-version filename matching does not prove absence under every possible alias.',
      'Existing G04 generation-ready states are copied literally and are not reconciled here to every production palette consumer.'] };
  if (!options.checkOnly) {
    const absolute = containedPath(options.root, options.output);
    fs.mkdirSync(path.dirname(absolute), { recursive: true });
    fs.writeFileSync(absolute, `${JSON.stringify(report, null, 2)}\n`, 'utf8');
  }
  process.stdout.write(`${JSON.stringify({ mode: options.checkOnly ? 'check-only' : 'derived-report',
    report: options.checkOnly ? null : slash(options.output), evidence: report.evidenceLevel,
    sourceRecords: report.reconciliation.inputG04AndDotFileRecords,
    canonicalFileIdentities: report.reconciliation.canonicalFileIdentitiesByPathOrGuid,
    equipmentBindings: equipment.length, creatureBindings: creatures.length,
    sourceLinkStates: report.reconciliation.sourceLinkStates,
    additionalFamilies: families.length, staticExtractionChecks: report.staticExtractionChecks.result })}\n`);
}

try { main(); } catch (error) { process.stderr.write(`${error.stack ?? error}\n`); process.exitCode = 1; }
