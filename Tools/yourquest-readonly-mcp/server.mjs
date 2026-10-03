import { fileURLToPath } from 'node:url';
import { createProjectReader, documentIds, canonicalGoalIds } from './project-reader.mjs';

const supportedVersions = ['2025-11-25', '2025-06-18', '2024-11-05'];
const annotations = { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false };
const tools = [
  {
    name: 'get_project_snapshot', description: 'Inspect all 20 canonical goals, active workflow, hashes, contract constants, Git dirtiness and bounded newer-evidence warnings. Source inspection never certifies gameplay.',
    inputSchema: { type: 'object', properties: {}, additionalProperties: false }, annotations,
  },
  {
    name: 'read_project_document', description: `Read a bounded page of a named approved document. IDs: ${documentIds.join(', ')}; curated receipts use receipt:GNN_<filename>.md from find_project_evidence. No filesystem paths.`,
    inputSchema: { type: 'object', properties: { id: { type: 'string', maxLength: 180 }, startLine: { type: 'integer', minimum: 1, maximum: 100000, default: 1 }, lineCount: { type: 'integer', minimum: 1, maximum: 200, default: 100 } }, required: ['id'], additionalProperties: false }, annotations,
  },
  {
    name: 'find_project_evidence', description: 'Find shallow curated Docs/GNN_*.md receipts and analyses by literal filename text. Inspect at most 32 candidates, return provenance and declared instrumentation/results without certifying acceptance.',
    inputSchema: { type: 'object', properties: { goalId: { type: 'string', enum: canonicalGoalIds }, limit: { type: 'integer', minimum: 1, maximum: 20, default: 5 }, query: { type: 'string', maxLength: 80, default: '' } }, required: ['goalId'], additionalProperties: false }, annotations,
  },
];

// note: Stdio is intentionally the only transport; no listener, external API, write tool or arbitrary command is exposed.
let reader;
try { reader = createProjectReader(process.env.YQ_PROJECT_ROOT ?? fileURLToPath(new URL('../../', import.meta.url))); }
catch { process.stderr.write('YourQuest project root is unavailable.\n'); process.exit(1); }
let initialized = false;
let ready = false;
let outputBlocked = false;
const write = message => {
  if (!process.stdout.write(`${JSON.stringify(message)}\n`)) { outputBlocked = true; process.stdin.pause(); }
};
const rpcError = (id, code, message) => write({ jsonrpc: '2.0', id, error: { code, message } });
const isObject = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const integer = (value, low, high) => Number.isInteger(value) && value >= low && value <= high;

function validArguments(name, args) {
  if (!isObject(args)) return false;
  const keys = Object.keys(args);
  if (name === 'get_project_snapshot') return keys.length === 0;
  if (name === 'read_project_document') return keys.every(key => ['id', 'startLine', 'lineCount'].includes(key)) && typeof args.id === 'string' && args.id.length <= 180 && (args.startLine === undefined || integer(args.startLine, 1, 100000)) && (args.lineCount === undefined || integer(args.lineCount, 1, 200));
  if (name === 'find_project_evidence') return keys.every(key => ['goalId', 'limit', 'query'].includes(key)) && canonicalGoalIds.includes(args.goalId) && (args.limit === undefined || integer(args.limit, 1, 20)) && (args.query === undefined || (typeof args.query === 'string' && args.query.length <= 80 && !/[\u0000-\u001f]/.test(args.query)));
  return false;
}

// note: Pin the published 2025 lifecycle and negotiate its documented older revisions; host compatibility is tested separately.
function handle(message) {
  if (!isObject(message) || message.jsonrpc !== '2.0' || typeof message.method !== 'string' || (Object.hasOwn(message, 'id') && !(typeof message.id === 'string' || (typeof message.id === 'number' && Number.isFinite(message.id))))) {
    rpcError(null, -32600, 'Invalid Request'); return;
  }
  const notification = !Object.hasOwn(message, 'id');
  if (notification) {
    if (message.method === 'notifications/initialized' && initialized) ready = true;
    return;
  }
  const id = message.id;
  if (message.method === 'ping') { write({ jsonrpc: '2.0', id, result: {} }); return; }
  if (message.method === 'initialize') {
    const params = message.params;
    if (initialized || !isObject(params) || typeof params.protocolVersion !== 'string' || !isObject(params.capabilities) || !isObject(params.clientInfo)) { rpcError(id, -32602, 'Invalid initialization parameters or duplicate initialization'); return; }
    initialized = true;
    write({ jsonrpc: '2.0', id, result: { protocolVersion: supportedVersions.includes(params.protocolVersion) ? params.protocolVersion : supportedVersions[0], capabilities: { tools: { listChanged: false } }, serverInfo: { name: 'yourquest-readonly-mcp', version: '0.1.0' }, instructions: 'Read-only requirements and provenance. Results do not grant work authorization, certify goals, inspect hidden chats, or change accepted saves.' } });
    return;
  }
  if (!ready) { rpcError(id, -32002, 'Initialize and send notifications/initialized before tool operations'); return; }
  if (message.method === 'tools/list') {
    if (message.params !== undefined && (!isObject(message.params) || Object.keys(message.params).some(key => key !== '_meta'))) { rpcError(id, -32602, 'Unsupported tool-list parameters'); return; }
    write({ jsonrpc: '2.0', id, result: { tools } }); return;
  }
  if (message.method !== 'tools/call') { rpcError(id, -32601, 'Method not found'); return; }
  const params = message.params;
  if (!isObject(params) || Object.keys(params).some(key => !['name', 'arguments', '_meta'].includes(key)) || !tools.some(tool => tool.name === params.name) || !validArguments(params.name, params.arguments ?? {})) { rpcError(id, -32602, 'Unknown tool or invalid tool arguments'); return; }
  try {
    const args = params.arguments ?? {};
    const result = params.name === 'get_project_snapshot' ? reader.getSnapshot() : params.name === 'read_project_document' ? reader.readDocument(args) : reader.findEvidence(args);
    write({ jsonrpc: '2.0', id, result: { content: [{ type: 'text', text: JSON.stringify(result) }], structuredContent: result, isError: false } });
  } catch (error) {
    const result = { error: { code: error.code ?? 'INSPECTION_FAILED', message: error.code ? error.message : 'Project inspection failed.' }, runtimeVerified: false };
    write({ jsonrpc: '2.0', id, result: { content: [{ type: 'text', text: JSON.stringify(result) }], structuredContent: result, isError: true } });
  }
}

// note: Bound incoming frames before parsing. Protocol output is exclusively newline-delimited JSON-RPC.
let buffer = '';
process.stdin.setEncoding('utf8');
function consumeInput() {
  let end;
  while (!outputBlocked && (end = buffer.indexOf('\n')) >= 0) {
    const line = buffer.slice(0, end).replace(/\r$/, '');
    buffer = buffer.slice(end + 1);
    if (Buffer.byteLength(line) > 65536) { rpcError(null, -32600, 'Request exceeds 64 KiB'); continue; }
    if (!line) continue;
    let message;
    try { message = JSON.parse(line); } catch { rpcError(null, -32700, 'Parse error'); continue; }
    handle(message);
  }
  if (!outputBlocked && Buffer.byteLength(buffer) > 65536) { rpcError(null, -32600, 'Request exceeds 64 KiB'); buffer = ''; process.stdin.destroy(); }
}
process.stdin.on('data', chunk => { buffer += chunk; consumeInput(); });
// note: Stop reading when the host stops consuming results so repeated snapshots cannot grow an output queue.
process.stdout.on('drain', () => { outputBlocked = false; consumeInput(); if (!outputBlocked) process.stdin.resume(); });
process.stdin.on('end', () => { process.stdout.end(); });
process.stdin.on('error', () => { process.stderr.write('Input stream closed unexpectedly.\n'); process.exitCode = 1; });
process.stdout.on('error', error => { if (error.code === 'EPIPE') process.exit(0); else process.exitCode = 1; });
