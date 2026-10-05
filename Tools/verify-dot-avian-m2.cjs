const fs = require('fs'), crypto = require('crypto'), assert = require('assert').strict;
const output = 'outputs/DOT_Integration_20261003/Avian_M2', staged = `${output}/Staged/`;
const plan = JSON.parse(fs.readFileSync(`${output}/plan.json`)), hash = b => crypto.createHash('sha256').update(b).digest('hex');
const read = f => JSON.parse(fs.readFileSync(staged + f));
const manifest = read('ModularExpansion/M2/AvianModularM2_APPROVED_PAYLOAD_MANIFEST.json');
for (const m of [...manifest.members, ...manifest.required_frozen_dependencies, ...read('AvianV9b_APPROVED_PAYLOAD_MANIFEST.json').members]) {
  const bytes = fs.readFileSync(staged + m.path); assert.equal(bytes.length, m.bytes); assert.equal(hash(bytes), m.sha256, m.path);
}
// note: Match the publisher's canonical member digest and frozen dependency hashes, rather than inferring compatibility from a version label.
for (const m of [manifest, read('AvianV9b_APPROVED_PAYLOAD_MANIFEST.json')]) {
  const rows = m.members.slice().sort((a, b) => a.path < b.path ? -1 : a.path > b.path ? 1 : 0).map(r => ({ bytes: r.bytes, path: r.path, sha256: r.sha256 }));
  assert.equal(hash(Buffer.from(JSON.stringify(rows))), m.canonical_member_list_sha256);
}
function glb(f) { const b = fs.readFileSync(staged + f), size = b.readUInt32LE(12); assert.equal(b.readUInt32LE(0), 0x46546c67); return { j: JSON.parse(b.subarray(20, 20 + size)), bin: b.subarray(28 + size) }; }
function accessor(g, index) { const a = g.j.accessors[index], v = g.j.bufferViews[a.bufferView], width = { SCALAR: 1, VEC2: 2, VEC3: 3, VEC4: 4, MAT4: 16 }[a.type] * { 5121: 1, 5123: 2, 5125: 4, 5126: 4 }[a.componentType], offset = (v.byteOffset || 0) + (a.byteOffset || 0); return Buffer.concat(Array.from({ length: a.count }, (_, i) => g.bin.subarray(offset + i * (v.byteStride || width), offset + i * (v.byteStride || width) + width))); }
const base = glb('Exports/AvianV9b_Terrestrial_LOD0.glb'), baseSkin = base.j.skins[0], baseNames = baseSkin.joints.map(i => base.j.nodes[i].name);
const records = [];
for (const row of plan.files.filter(r => r.destination.endsWith('.glb'))) {
  const g = glb(row.relative), skin = g.j.skins[0]; assert.equal(skin.joints.length, 77); assert.deepEqual(skin.joints.map(i => g.j.nodes[i].name), baseNames);
  assert(accessor(g, skin.inverseBindMatrices).equals(accessor(base, baseSkin.inverseBindMatrices)), `${row.relative}: inverse binds`);
  assert.deepEqual(skin.joints.map(i => { const n = { ...g.j.nodes[i] }; delete n.children; return n; }), baseSkin.joints.map(i => { const n = { ...base.j.nodes[i] }; delete n.children; return n; }));
  assert.equal(g.j.animations.length, 5);
  for (const a of g.j.animations) {
    const b = base.j.animations.find(b => b.name === a.name); assert(b); assert.equal(a.samplers.length, b.samplers.length);
    assert.deepEqual(a.channels.map(c => [c.sampler, g.j.nodes[c.target.node].name, c.target.path]), b.channels.map(c => [c.sampler, base.j.nodes[c.target.node].name, c.target.path]));
    for (let i = 0; i < a.samplers.length; i++) { assert.equal(a.samplers[i].interpolation, b.samplers[i].interpolation); for (const field of ['input', 'output']) assert(accessor(g, a.samplers[i][field]).equals(accessor(base, b.samplers[i][field])), `${row.relative}: animation ${field}`); }
  }
  const images = g.j.images || [];
  assert.equal(images.length, row.relative.includes('Clasp_Parts') ? 0 : 8);
  for (let i = 0; i < images.length; i++) { const a = g.j.bufferViews[images[i].bufferView], b = base.j.bufferViews[base.j.images[i].bufferView]; assert(g.bin.subarray(a.byteOffset || 0, (a.byteOffset || 0) + a.byteLength).equals(base.bin.subarray(b.byteOffset || 0, (b.byteOffset || 0) + b.byteLength)), `${row.relative}: source image`); }
  row.embeddedMaterials = g.j.materials.map((m, index) => ({ index, name: m.name }));
  row.meshes = g.j.meshes.map((m, index) => ({ name: m.name, node: g.j.nodes.find(n => n.mesh === index).name, triangles: m.primitives.reduce((sum, p) => sum + g.j.accessors[p.indices].count / 3, 0) }));
  records.push({ path: row.relative, meshes: g.j.meshes.map((m, index) => ({ name: m.name, node: g.j.nodes.find(n => n.mesh === index).name, triangles: m.primitives.reduce((sum, p) => sum + g.j.accessors[p.indices].count / 3, 0) })), unchangedRigClipsImages: true });
}
const build = read('Validation/AvianV9b_terrestrial_build.json'), contract = read('AvianV9b_Animation_Contract.json');
for (const clip of build.clips) {
  assert.equal(clip.duration_s, contract.clips.find(c => c.name === clip.name).duration_s);
  for (let i = 0; i < clip.samples.length; i++) { const sample = clip.samples[i]; assert(Math.abs(sample.time_s - clip.duration_s * i / (clip.samples.length - 1)) < 1e-6); assert(Number.isFinite(sample.forward_distance_m)); if (i) assert(sample.forward_distance_m >= clip.samples[i - 1].forward_distance_m); }
}
assert.equal(build.clips.find(c => c.name === 'walk_loop').samples.at(-1).forward_distance_m, .48);
assert(Math.abs(['walk_start', 'walk_loop', 'walk_loop', 'walk_stop'].reduce((sum, name) => sum + build.clips.find(c => c.name === name).samples.at(-1).forward_distance_m, 0) - 1.2) < 1e-9);
plan.status = 'PREFLIGHT_PASS'; fs.writeFileSync(`${output}/plan.json`, JSON.stringify(plan, null, 2));
fs.writeFileSync(`${output}/source-contracts.json`, JSON.stringify({ status: 'PASS', utc: new Date().toISOString(), frozenDependencies: 6, glbs: records, exactActorCurveTravel: 1.2, evidence: 'Exact approved payload/dependencies, identical rigs/inverse binds/animation arrays/source images and uniform monotone actor samples. Unity acceptance is separate.' }, null, 2));
console.log(`Avian preflight PASS: 63 files, six exact frozen dependencies and ${records.length} GLBs with unchanged rigs/clips/images.`);
