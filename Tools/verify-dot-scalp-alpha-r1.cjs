const fs = require('fs'), zlib = require('zlib'), crypto = require('crypto'), assert = require('assert').strict;
const output = 'outputs/DOT_Integration_20261003/Scalp_Alpha_R1';
const plan = JSON.parse(fs.readFileSync(`${output}/plan.json`));
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
function glb(file) {
  const bytes = fs.readFileSync(file), size = bytes.readUInt32LE(12);
  assert.equal(bytes.readUInt32LE(0), 0x46546c67);
  return { json: JSON.parse(bytes.subarray(20, 20 + size)), bin: bytes.subarray(28 + size) };
}
function view(g, i) { const v = g.json.bufferViews[i]; return g.bin.subarray(v.byteOffset || 0, (v.byteOffset || 0) + v.byteLength); }
function png(bytes) {
  const w = bytes.readUInt32BE(16), h = bytes.readUInt32BE(20), type = bytes[25], count = type === 6 ? 4 : 3;
  assert.equal(bytes[24], 8); assert([2, 6].includes(type)); assert.equal(bytes[28], 0);
  const chunks = []; for (let i = 8; i < bytes.length;) { const size = bytes.readUInt32BE(i); if (bytes.toString('ascii', i + 4, i + 8) === 'IDAT') chunks.push(bytes.subarray(i + 8, i + 8 + size)); i += size + 12; }
  const raw = zlib.inflateSync(Buffer.concat(chunks)), pixels = Buffer.alloc(w * h * count), stride = w * count;
  assert.equal(raw.length, (stride + 1) * h);
  // note: Decode PNG's lossless row filters to compare exact RGB texels independently from the newly restored alpha channel.
  const paeth = (a, b, c) => { const p = a + b - c, x = Math.abs(p - a), y = Math.abs(p - b), z = Math.abs(p - c); return x <= y && x <= z ? a : y <= z ? b : c; };
  for (let y = 0; y < h; y++) {
    const filter = raw[y * (stride + 1)]; assert(filter <= 4);
    for (let x = 0; x < stride; x++) {
      const index = y * stride + x, a = x >= count ? pixels[index - count] : 0, b = y ? pixels[index - stride] : 0, c = y && x >= count ? pixels[index - stride - count] : 0;
      const predictor = [0, a, b, Math.floor((a + b) / 2), paeth(a, b, c)][filter]; pixels[index] = (raw[y * (stride + 1) + x + 1] + predictor) & 255;
    }
  }
  return { w, h, count, pixels };
}
const checks = [];
for (const file of plan.changes.filter(f => f.kind === 'runtime_glb')) {
  const backup = `${output}/Backup/${file.destination}`, baseline = fs.existsSync(backup) ? backup : file.destination;
  assert.equal(hash(fs.readFileSync(baseline)), file.oldSha256); assert.equal(hash(fs.readFileSync(file.staged)), file.newSha256);
  const old = glb(baseline), next = glb(file.staged);
  for (const field of ['nodes', 'skins', 'meshes', 'materials', 'textures', 'images', 'samplers', 'animations', 'accessors']) assert.deepEqual(old.json[field], next.json[field], `${file.destination}: ${field}`);
  assert.equal(old.json.bufferViews.length, next.json.bufferViews.length);
  const changed = old.json.images.filter(image => !view(old, image.bufferView).equals(view(next, image.bufferView)));
  assert.equal(changed.length, 1); const image = changed[0]; assert(/CC0_ponytail.*albedo/i.test(image.name));
  for (let i = 0; i < old.json.bufferViews.length; i++) {
    const a = { ...old.json.bufferViews[i] }, b = { ...next.json.bufferViews[i] }; delete a.byteOffset; delete b.byteOffset;
    if (i === image.bufferView) { delete a.byteLength; delete b.byteLength; } else assert(view(old, i).equals(view(next, i)), `${file.destination}: unchanged buffer ${i}`);
    assert.deepEqual(a, b);
  }
  const a = png(view(old, image.bufferView)), b = png(view(next, image.bufferView)); assert.equal(a.w, b.w); assert.equal(a.h, b.h); assert.equal(b.count, 4);
  let transparent = 0, partial = 0, opaque = 0;
  for (let i = 0; i < a.w * a.h; i++) {
    for (let k = 0; k < 3; k++) assert.equal(a.pixels[i * a.count + k], b.pixels[i * 4 + k], 'RGB unchanged');
    const alpha = b.pixels[i * 4 + 3]; if (!alpha) transparent++; else if (alpha === 255) opaque++; else partial++;
  }
  assert(transparent > 0 && partial > 0 && opaque > 0);
  checks.push({ file: file.destination, correctedImage: image.name, dimensions: [a.w, a.h], exactRgbAndAllOtherBinaryData: true, transparent, partial, opaque });
}
assert.equal(checks.length, 32);
fs.writeFileSync(`${output}/source-contracts.json`, JSON.stringify({ status: 'PASS', utc: new Date().toISOString(), checks, evidence: 'Exact GLB graph and immutable buffer comparison; decoded PNG RGB unchanged and restored nonuniform alpha. Unity import/sorting verification is separate.' }, null, 2));
console.log('PASS: 32 exact alpha-only GLB contract and decoded-pixel comparisons.');
