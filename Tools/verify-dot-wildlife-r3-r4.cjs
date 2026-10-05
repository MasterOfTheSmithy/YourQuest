const fs = require('fs');
const crypto = require('crypto');
const assert = require('assert').strict;
const output = 'outputs/DOT_Integration_20261003/Wildlife_R3_R4';
const plan = JSON.parse(fs.readFileSync(`${output}/plan.json`));
const checks = [];
function glb(path) {
  const bytes = fs.readFileSync(path), size = bytes.readUInt32LE(12);
  assert.equal(bytes.readUInt32LE(0), 0x46546c67);
  return { json: JSON.parse(bytes.subarray(20, 20 + size).toString()), bin: bytes.subarray(28 + size) };
}
function accessor(g, index) {
  const a = g.json.accessors[index], v = g.json.bufferViews[a.bufferView];
  const components = { SCALAR: 1, VEC2: 2, VEC3: 3, VEC4: 4, MAT4: 16 }[a.type];
  const width = { 5121: 1, 5123: 2, 5125: 4, 5126: 4 }[a.componentType] * components;
  const offset = (v.byteOffset || 0) + (a.byteOffset || 0);
  if (!v.byteStride || v.byteStride === width) return g.bin.subarray(offset, offset + a.count * width);
  return Buffer.concat(Array.from({ length: a.count }, (_, i) => g.bin.subarray(offset + i * v.byteStride, offset + i * v.byteStride + width)));
}
// note: Compare only motion outputs and the disclosed LOD1 skin exception as mutable; source data cannot alter geometry, binds, materials or timing unnoticed.
for (const file of plan.changes) {
  if (file.destination.endsWith('.json')) {
    const old = JSON.parse(fs.readFileSync(file.destination)), next = JSON.parse(fs.readFileSync(file.staged));
    assert.equal(old.contact_gait.loop_controller_speed_m_s, next.contact_gait.loop_controller_speed_m_s);
    assert.deepEqual(old.contact_gait.profiles, next.contact_gait.profiles);
    checks.push({ file: file.destination, unchangedControllerTravel: true });
  }
  if (!file.destination.endsWith('.glb')) continue;
  const old = glb(file.destination), next = glb(file.staged);
  for (const field of ['nodes', 'skins', 'meshes', 'materials', 'textures', 'images', 'samplers', 'accessors', 'bufferViews']) assert.deepEqual(old.json[field], next.json[field], `${file.destination}: ${field}`);
  assert.equal(old.json.animations.length, next.json.animations.length);
  const mutable = new Set();
  for (let i = 0; i < old.json.animations.length; i++) {
    const a = old.json.animations[i], b = next.json.animations[i];
    assert.equal(a.name, b.name); assert.deepEqual(a.channels, b.channels);
    assert.deepEqual(a.samplers.map(s => [s.input, s.output]), b.samplers.map(s => [s.input, s.output]));
    for (const sampler of b.samplers) assert(['STEP', 'LINEAR'].includes(sampler.interpolation));
    for (const sampler of a.samplers) { assert(accessor(old, sampler.input).equals(accessor(next, sampler.input))); mutable.add(sampler.output); }
  }
  const skinRepair = file.species === 'deer' && file.destination.endsWith('LOD1.glb');
  const weights = new Set(old.json.meshes.flatMap(m => m.primitives.map(p => p.attributes.WEIGHTS_0)).filter(x => x !== undefined));
  let changedSkinBytes = 0;
  for (let i = 0; i < old.json.accessors.length; i++) {
    if (mutable.has(i)) continue;
    const a = accessor(old, i), b = accessor(next, i);
    if (skinRepair && weights.has(i)) {
      assert.equal(a.length, b.length); for (let j = 0; j < a.length; j++) if (a[j] !== b[j]) changedSkinBytes++;
    } else assert(a.equals(b), `${file.destination}: immutable accessor ${i}`);
  }
  for (const image of old.json.images || []) if (image.bufferView !== undefined) {
    const view = old.json.bufferViews[image.bufferView], offset = view.byteOffset || 0;
    assert(old.bin.subarray(offset, offset + view.byteLength).equals(next.bin.subarray(offset, offset + view.byteLength)));
  }
  // note: The delivered patch modifies two complete VEC4 float weights (32-byte scope); individual byte differences can be fewer.
  assert(skinRepair ? changedSkinBytes > 0 && changedSkinBytes <= 32 : changedSkinBytes === 0);
  checks.push({ file: file.destination, frozenGeometryMaterialsBindsClipNamesChannelsTimes: true, changedSkinBytes });
}
fs.writeFileSync(`${output}/source-contracts.json`, JSON.stringify({ status: 'PASS', utc: new Date().toISOString(), checks, evidence: 'Fresh exact source contracts; Unity import and visible gameplay are separate checks.' }, null, 2));
console.log(`Source contracts PASS: ${checks.length} GLB/metadata comparisons.`);
