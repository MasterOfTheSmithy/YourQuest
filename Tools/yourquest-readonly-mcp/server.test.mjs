import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, cpSync, readFileSync, writeFileSync, rmSync, renameSync, symlinkSync, unlinkSync, lstatSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve, join, dirname, basename, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { createProjectReader, canonicalGoalIds } from './project-reader.mjs';

const projectRoot = fileURLToPath(new URL('../../', import.meta.url));
const serverPath = fileURLToPath(new URL('./server.mjs', import.meta.url));
const roadmap = 'Docs/ClosedBetaRoadmap_2026-09-16';
const indexPath = `${roadmap}/GOAL_STATUS.json`;
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const write = (root, path, text) => { mkdirSync(dirname(join(root, path)), { recursive: true }); writeFileSync(join(root, path), text); };

// note: Negative tests mutate only a minted temporary fixture, retaining the actual 20 canonical specifications.
function fixture(t) {
  const tempRoot = realTempRoot();
  const base = mkdtempSync(join(tempRoot, 'yourquest-mcp-test-'));
  const root = join(base, 'checkout');
  mkdirSync(root);
  const index = JSON.parse(readFileSync(join(projectRoot, indexPath), 'utf8'));
  for (const id of canonicalGoalIds) {
    const path = `${roadmap}/goals/Goal-${id.slice(1)}.md`;
    mkdirSync(dirname(join(root, path)), { recursive: true });
    cpSync(join(projectRoot, path), join(root, path));
  }
  for (const path of ['AGENTS.md', 'Docs/YourQuest_Game_Design_Document.md', `${roadmap}/goals/8 fix 4.md`]) {
    mkdirSync(dirname(join(root, path)), { recursive: true }); cpSync(join(projectRoot, path), join(root, path));
  }
  write(root, 'ProjectSettings/ProjectVersion.txt', `m_EditorVersion: ${index.sourceContracts.unityVersion}\n`);
  write(root, 'Assets/Assets/Scripts/Data/State/YQStateFoundation.cs', `public const int CurrentStateSchemaVersion = ${index.sourceContracts.saveSchema};\n`);
  write(root, 'Assets/Assets/Scripts/Generated/YQContinuousWorldCellAuthority.cs', `public const string SchemaVersion = "${index.sourceContracts.cellSchema}";\npublic const string EdgeContractVersion = "${index.sourceContracts.edgeSchema}";\n`);
  write(root, 'Assets/Assets/Scripts/Generated/YQSemanticChunkRuntimeVerification.cs', 'fixture verifier\n');
  write(root, 'Assets/Assets/Scripts/Generated/YQPlayerFollowingSemanticChunkStreamer.cs', 'fixture streamer\n');
  const indexed = index.goals.find(goal => goal.id === 'G08').evidence[0];
  write(root, indexed.path, `# Fixture receipt\n- startedUtc: ${indexed.observedUtc}\n- result: FAIL\n`);
  indexed.sha256 = hash(readFileSync(join(root, indexed.path)));
  write(root, indexPath, JSON.stringify(index));
  const cleanName = 'G08_R2_Speed260_2026-10-02_120000_Clean_FAIL.md';
  const clean = `# YourQuest Semantic Chunk Runtime Verification\n- startedUtc: 2026-10-02T12:00:00Z\n- completedUtc: 2026-10-02T12:00:20Z\n- verificationScope: focused witness; full rows not run\n- measurementProfilerSetup: enabled=False binaryLog=False deepProfiling=False traceRequested=False\n- verifierSourceSha256: path=ignored sha256=${hash('fixture verifier\n')}\n- streamerSourceSha256: path=ignored sha256=${'a'.repeat(64)}\n- result: FAIL\n`;
  write(root, `Docs/${cleanName}`, clean);
  write(root, 'Docs/G08_R2_Speed260_2026-10-02_130000_Diagnostic_FAIL.md', clean.replaceAll('12:00:', '13:00:').replace('traceRequested=False', 'traceRequested=True'));
  t.after(() => {
    // note: Remove only this exact checked temporary root; junction tests unlink their links first.
    const target = resolve(base);
    assert.ok(target.startsWith(tempRoot + sep) && basename(target).startsWith('yourquest-mcp-test-'));
    rmSync(target, { recursive: true, force: true });
  });
  return { base, root, index, cleanName, reader: createProjectReader(root) };
}
function realTempRoot() { return resolve(tmpdir()); }

function client(root) {
  const child = spawn(process.execPath, [serverPath], { env: { ...process.env, YQ_PROJECT_ROOT: root }, stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true });
  const waiting = new Map();
  const messages = [];
  let nextId = 1;
  let buffer = '';
  let stderr = '';
  child.stderr.setEncoding('utf8'); child.stderr.on('data', chunk => { stderr += chunk; });
  child.stdout.setEncoding('utf8');
  child.stdout.on('data', chunk => {
    buffer += chunk;
    let end;
    while ((end = buffer.indexOf('\n')) >= 0) {
      const line = buffer.slice(0, end); buffer = buffer.slice(end + 1);
      const message = JSON.parse(line);
      messages.push(message);
      const pending = waiting.get(message.id);
      if (pending) { waiting.delete(message.id); clearTimeout(pending.timer); pending.resolve(message); }
    }
  });
  child.on('error', error => { for (const pending of waiting.values()) { clearTimeout(pending.timer); pending.reject(error); } waiting.clear(); });
  const wait = id => new Promise((resolve, reject) => {
    const timer = setTimeout(() => { waiting.delete(id); reject(new Error('MCP response timeout')); }, 15000);
    waiting.set(id, { resolve, reject, timer });
  });
  return {
    child, messages,
    async request(method, params) { const id = nextId++; const result = wait(id); child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id, method, ...(params === undefined ? {} : { params }) })}\n`); return result; },
    async raw(line) { const result = wait(null); child.stdin.write(`${line}\n`); return result; },
    notify(method) { child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', method })}\n`); },
    async initialize(version = '2025-11-25') { const response = await this.request('initialize', { protocolVersion: version, capabilities: {}, clientInfo: { name: 'local-test', version: '1.0.0' } }); this.notify('notifications/initialized'); return response; },
    async close() { const finished = once(child, 'close'); child.stdin.end(); const [code] = await finished; assert.equal(code, 0); assert.equal(stderr, ''); assert.equal(buffer, ''); },
  };
}

test('all 20 canonical goals retain complete gates, dependency map and hashes', t => {
  const f = fixture(t);
  const snapshot = f.reader.getSnapshot();
  assert.equal(snapshot.goals.length, 20);
  assert.equal(snapshot.activeGoalId, f.index.activeGoalId);
  assert.equal(snapshot.activeWorkflow.hashMatches, true);
  for (const goal of snapshot.goals) {
    assert.equal(goal.specificationHashMatches, true);
    assert.equal(goal.requiredSectionsPresent, true);
    assert.deepEqual(goal.dependencies, f.index.goals.find(entry => entry.id === goal.id).dependencies);
    assert.match(f.reader.readDocument({ id: goal.id, lineCount: 200 }).text, /## 13\. COMPLETION GATE/);
  }
  assert.equal(snapshot.runtimeVerified, false);
});

test('newer clean and instrumented receipts preserve provenance and warn about source drift', t => {
  const f = fixture(t);
  const result = f.reader.findEvidence({ goalId: 'G08', limit: 2 });
  assert.equal(result.evidence.length, 2);
  assert.deepEqual(new Set(result.evidence.map(entry => entry.measurementMode)), new Set(['clean', 'instrumented']));
  assert.ok(result.evidence.every(entry => entry.reportedResult === 'FAIL' && !entry.certifiesCurrentCheckout));
  assert.ok(result.evidence.every(entry => entry.sourceComparisons.find(source => source.owner === 'verifier').matches));
  assert.ok(result.evidence.every(entry => !entry.sourceComparisons.find(source => source.owner === 'streamer').matches));
  assert.ok(result.warnings.some(entry => entry.code === 'NEWER_EVIDENCE_THAN_INDEX'));
  assert.ok(result.warnings.some(entry => entry.code === 'REPORTED_SOURCE_DIFFERS'));
  assert.equal(result.runtimeVerified, false);
});

test('traversal, absolute paths, hidden stores, logs, saves, credentials and arbitrary sources are denied', t => {
  const { reader } = fixture(t);
  for (const id of ['../AGENTS.md', 'C:/Users/Garri/.codex/auth.json', '\\\\server\\share\\secret', '.git/config', '.env', '.codex/sessions/x.jsonl', 'Logs/G08_secret.md', 'outputs/G08_secret.json', 'PlayerState.json', 'Assets/Assets/Scripts/LLM/LLMClient.cs', 'receipt:../AGENTS.md', 'receipt:G08_../../secret.md', 'receipt:G08_%2e%2e_secret.md', 'receipt:G08_secret.md:stream', 'receipt:G00_secret.md', 'receipt:G21_secret.md']) {
    assert.throws(() => reader.readDocument({ id }), error => error.code === 'DENIED_DOCUMENT', id);
  }
});

test('document paging and credential redaction retain original-byte provenance', t => {
  const f = fixture(t);
  const secret = 'api_key = "fixture-secret-value"\nAuthorization: Bearer fixture-access-value\nhttps://user:fixture-password@example.invalid\n';
  write(f.root, 'AGENTS.md', `First\n${secret}Last\n`);
  const result = f.reader.readDocument({ id: 'AGENTS', startLine: 2, lineCount: 2 });
  assert.equal(result.sha256, hash(readFileSync(join(f.root, 'AGENTS.md'))));
  assert.equal(result.returnedLines, 2);
  assert.equal(result.nextLine, 4);
  assert.equal(result.redacted, true);
  assert.ok(!result.text.includes('fixture-secret-value') && !result.text.includes('fixture-access-value'));
  const tail = f.reader.readDocument({ id: 'AGENTS', startLine: 4, lineCount: 1 });
  assert.ok(!tail.text.includes('fixture-password'));
});

test('malformed canonical paths and duplicate active goals cannot widen access', t => {
  const f = fixture(t);
  const index = structuredClone(f.index);
  index.goals[0].specification = '../outside-secret';
  write(f.root, indexPath, JSON.stringify(index));
  assert.throws(() => f.reader.getSnapshot(), error => error.code === 'INVALID_LEDGER');
  index.goals[0].specification = f.index.goals[0].specification;
  index.goals[8].workflowState = 'active';
  write(f.root, indexPath, JSON.stringify(index));
  assert.throws(() => f.reader.getSnapshot(), error => error.code === 'INVALID_LEDGER');
});

test('unsupported completion and escaping indexed evidence produce warnings without certification', t => {
  const f = fixture(t);
  const index = structuredClone(f.index);
  index.goals[8].workflowState = 'complete'; index.goals[8].acceptanceStatus = 'PASS';
  index.goals[7].evidence = [{ path: '../outside-secret', sha256: 'a'.repeat(64) }];
  write(f.root, indexPath, JSON.stringify(index));
  const snapshot = f.reader.getSnapshot();
  assert.ok(snapshot.warnings.some(entry => entry.code === 'UNSUPPORTED_COMPLETION_RECORD'));
  assert.ok(snapshot.warnings.some(entry => entry.code === 'DENIED_INDEXED_EVIDENCE'));
  assert.equal(snapshot.runtimeVerified, false);
});

test('conflicting indexed PASS and removed completion gates remain explicit warnings', t => {
  const f = fixture(t);
  const index = structuredClone(f.index);
  const receiptPath = 'Docs/G09_Fixture_FAIL.md';
  write(f.root, receiptPath, '# Fixture\n- result: FAIL\n');
  index.goals[8].workflowState = 'complete'; index.goals[8].acceptanceStatus = 'PASS'; index.goals[8].acceptanceRationale = 'Fixture bookkeeping claim';
  index.goals[8].evidence = [{ path: receiptPath, sha256: hash(readFileSync(join(f.root, receiptPath))), reportedResult: 'PASS' }];
  const specPath = index.goals[8].specification;
  write(f.root, specPath, readFileSync(join(f.root, specPath), 'utf8').replace('## 13. COMPLETION GATE', '## Removed gate'));
  index.goals[8].specificationSha256 = hash(readFileSync(join(f.root, specPath)));
  write(f.root, indexPath, JSON.stringify(index));
  const snapshot = f.reader.getSnapshot();
  assert.ok(snapshot.warnings.some(entry => entry.code === 'INDEXED_RESULT_DIFFERS'));
  assert.ok(snapshot.warnings.some(entry => entry.code === 'INCOMPLETE_SPECIFICATION_STRUCTURE'));
  assert.ok(snapshot.warnings.some(entry => entry.code === 'UNSUPPORTED_COMPLETION_RECORD'));
  assert.equal(snapshot.goals[8].acceptanceStatus, 'PASS');
  assert.equal(snapshot.runtimeVerified, false);
});

test('symlink or Windows junction ancestors cannot escape the project root', t => {
  const f = fixture(t);
  const outside = join(f.base, 'outside'); mkdirSync(outside);
  write(outside, 'YourQuest_Game_Design_Document.md', 'outside secret canary');
  const docs = join(f.root, 'Docs'); const original = join(f.root, 'Docs-original');
  renameSync(docs, original);
  symlinkSync(outside, docs, process.platform === 'win32' ? 'junction' : 'dir');
  try {
    assert.ok(lstatSync(docs).isSymbolicLink());
    assert.throws(() => f.reader.readDocument({ id: 'GDD' }), error => error.code === 'DENIED_LINK');
    assert.throws(() => f.reader.findEvidence({ goalId: 'G08' }), error => error.code === 'DENIED_LINK');
    assert.throws(() => f.reader.getSnapshot(), error => error.code === 'DENIED_LINK');
  } finally { unlinkSync(docs); renameSync(original, docs); }
});

test('file sizes and discovery candidates remain bounded; queries are literal', t => {
  const f = fixture(t);
  write(f.root, 'AGENTS.md', 'x'.repeat(2 * 1024 * 1024 + 1));
  assert.throws(() => f.reader.readDocument({ id: 'AGENTS' }), error => error.code === 'FILE_LIMIT');
  for (let i = 0; i < 40; i++) write(f.root, `Docs/G08_Fixture_${i}.md`, '# Fixture\n**Status: FAIL**\n');
  const result = f.reader.findEvidence({ goalId: 'G08', limit: 20 });
  assert.equal(result.inspected, 32); assert.equal(result.discoveryTruncated, true); assert.ok(result.evidence.length <= 20);
  assert.equal(f.reader.findEvidence({ goalId: 'G08', query: '.*' }).evidence.length, 0);
});

test('stdio startup, negotiation, discovery and all three harmless calls work', async t => {
  const f = fixture(t); const c = client(f.root);
  t.after(() => { if (c.child.exitCode === null) c.child.kill(); });
  assert.equal((await c.request('tools/list')).error.code, -32002);
  assert.deepEqual((await c.request('ping')).result, {});
  assert.equal((await c.initialize('2026-07-28')).result.protocolVersion, '2025-11-25');
  const list = (await c.request('tools/list')).result.tools;
  assert.deepEqual(list.map(tool => tool.name), ['get_project_snapshot', 'read_project_document', 'find_project_evidence']);
  assert.ok(list.every(tool => tool.annotations.readOnlyHint && !tool.annotations.openWorldHint));
  const snapshot = await c.request('tools/call', { name: 'get_project_snapshot', arguments: {} });
  assert.equal(snapshot.result.isError, false); assert.equal(snapshot.result.structuredContent.goals.length, 20);
  const document = await c.request('tools/call', { name: 'read_project_document', arguments: { id: 'G08', lineCount: 20 } });
  assert.equal(document.result.isError, false); assert.equal(document.result.structuredContent.returnedLines, 20);
  const evidence = await c.request('tools/call', { name: 'find_project_evidence', arguments: { goalId: 'G08', limit: 2 } });
  assert.equal(evidence.result.isError, false); assert.equal(evidence.result.structuredContent.evidence.length, 2);
  assert.deepEqual(JSON.parse(snapshot.result.content[0].text), snapshot.result.structuredContent);
  await c.close();
});

test('protocol rejects unknown tools, extra grants, invalid bounds and malformed frames', async t => {
  const f = fixture(t); const c = client(f.root);
  t.after(() => { if (c.child.exitCode === null) c.child.kill(); }); await c.initialize();
  for (const params of [
    { name: 'execute_command', arguments: { command: 'anything' } },
    { name: 'get_project_snapshot', arguments: { root: '../outside' } },
    { name: 'read_project_document', arguments: { id: 'AGENTS', path: '.env' } },
    { name: 'read_project_document', arguments: { id: 'AGENTS', lineCount: 201 } },
    { name: 'read_project_document', arguments: { id: 'AGENTS', startLine: 0 } },
    { name: 'find_project_evidence', arguments: { goalId: 'G21' } },
    { name: 'find_project_evidence', arguments: { goalId: 'G08', limit: 21 } },
  ]) assert.equal((await c.request('tools/call', params)).error.code, -32602);
  const denied = await c.request('tools/call', { name: 'read_project_document', arguments: { id: '../AGENTS.md' } });
  assert.equal(denied.result.isError, true); assert.equal(denied.result.structuredContent.error.code, 'DENIED_DOCUMENT');
  assert.equal((await c.request('resources/read', { uri: 'file:///outside' })).error.code, -32601);
  assert.equal((await c.raw('{broken')).error.code, -32700);
  assert.equal((await c.raw('[]')).error.code, -32600);
  assert.equal((await c.raw(JSON.stringify({ jsonrpc: '2.0', id: 999, method: 'ping', params: { text: 'x'.repeat(70000) } }))).error.code, -32600);
  assert.deepEqual((await c.request('ping')).result, {});
  await c.close();
});

test('queued requests survive output backpressure without losing framing or shutdown', async t => {
  const f = fixture(t); const c = client(f.root);
  t.after(() => { if (c.child.exitCode === null) c.child.kill(); }); await c.initialize();
  c.child.stdout.pause();
  const snapshot = c.request('tools/call', { name: 'get_project_snapshot', arguments: {} });
  const list = c.request('tools/list');
  const document = c.request('tools/call', { name: 'read_project_document', arguments: { id: 'G20', lineCount: 200 } });
  await new Promise(resolve => setTimeout(resolve, 100));
  c.child.stdout.resume();
  const results = await Promise.all([snapshot, list, document]);
  assert.equal(results[0].result.structuredContent.goals.length, 20);
  assert.equal(results[1].result.tools.length, 3);
  assert.equal(results[2].result.isError, false);
  await c.close();
});

test('actual checkout protocol reads preserve authoritative files and identify untracked evidence', async t => {
  const protectedPaths = ['AGENTS.md', indexPath, ...canonicalGoalIds.map(id => `${roadmap}/goals/Goal-${id.slice(1)}.md`)];
  const before = protectedPaths.map(path => hash(readFileSync(join(projectRoot, path))));
  const c = client(projectRoot); t.after(() => { if (c.child.exitCode === null) c.child.kill(); }); await c.initialize();
  const snapshot = (await c.request('tools/call', { name: 'get_project_snapshot', arguments: {} })).result;
  assert.equal(snapshot.isError, false); assert.equal(snapshot.structuredContent.goals.length, 20); assert.equal(snapshot.structuredContent.runtimeVerified, false);
  const document = (await c.request('tools/call', { name: 'read_project_document', arguments: { id: 'AGENTS', lineCount: 5 } })).result;
  assert.equal(document.isError, false); assert.equal(document.structuredContent.returnedLines, 5);
  const evidence = (await c.request('tools/call', { name: 'find_project_evidence', arguments: { goalId: 'G08', limit: 3 } })).result;
  assert.equal(evidence.isError, false); assert.ok(evidence.structuredContent.evidence.length > 0);
  assert.ok(evidence.structuredContent.evidence.every(entry => Object.hasOwn(entry, 'gitStatus') && Object.hasOwn(entry, 'sha256')));
  await c.close();
  assert.deepEqual(protectedPaths.map(path => hash(readFileSync(join(projectRoot, path)))), before);
});

test('portable package manifests describe the tested local entrypoint and no dependencies', () => {
  const base = dirname(serverPath);
  const plugin = JSON.parse(readFileSync(join(base, 'plugin.json'), 'utf8'));
  const config = JSON.parse(readFileSync(join(base, 'mcp.json'), 'utf8'));
  const pkg = JSON.parse(readFileSync(join(base, 'package.json'), 'utf8'));
  assert.equal(plugin.name, pkg.name); assert.equal(plugin.version, pkg.version); assert.equal(pkg.dependencies, undefined);
  const server = config.mcpServers['yourquest-evidence'];
  assert.equal(server.type, 'stdio'); assert.equal(server.command, 'node'); assert.deepEqual(server.args, ['${PLUGIN_ROOT}/server.mjs']);
  assert.equal(server.env.YQ_PROJECT_ROOT, 'C:/Users/Garri/YourQuest');
});
