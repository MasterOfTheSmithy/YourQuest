import { constants, openSync, closeSync, fstatSync, readSync, lstatSync, realpathSync, readdirSync } from 'node:fs';
import { resolve, relative, isAbsolute, sep } from 'node:path';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';

const roadmap = 'Docs/ClosedBetaRoadmap_2026-09-16';
const ledgerPath = `${roadmap}/GOAL_STATUS.json`;
const goalIds = Array.from({ length: 20 }, (_, i) => `G${String(i + 1).padStart(2, '0')}`);
const receiptName = /^G(0[1-9]|1[0-9]|20)_[A-Za-z0-9][A-Za-z0-9_-]*\.md$/;
const documents = {
  AGENTS: 'AGENTS.md', GDD: 'Docs/YourQuest_Game_Design_Document.md',
  ROADMAP: `${roadmap}/ROADMAP.md`, GOAL_STATUS: ledgerPath, ROADMAP_README: `${roadmap}/README.md`,
  AUDIT_INTEGRATION: `${roadmap}/AUDIT_INTEGRATION.md`, SUPPORTING_ARCHITECTURE: `${roadmap}/Supporting-architecture.md`,
  PROJECT_SETUP: 'Docs/PROJECT_SETUP.md', UNITY_ACCESS: 'UNITY_MCP_GUIDE.md',
  PROJECT_SKILL: '.agents/skills/yourquest-evidence/SKILL.md',
  AGENTS_SCRIPTS: 'Assets/Assets/Scripts/AGENTS.md', AGENTS_DATA: 'Assets/Assets/Scripts/Data/AGENTS.md',
  AGENTS_GENERATED: 'Assets/Assets/Scripts/Generated/AGENTS.md',
  AGENTS_EDITOR: 'Assets/Assets/Scripts/Generated/Editor/AGENTS.md',
  AGENTS_LLM: 'Assets/Assets/Scripts/LLM/AGENTS.md', AGENTS_TUTORIAL: 'Assets/Assets/Scripts/Tutorial/AGENTS.md',
  G08_WORKFLOW: `${roadmap}/goals/8 fix 4.md`,
};
const ownerSources = {
  verifier: 'Assets/Assets/Scripts/Generated/YQSemanticChunkRuntimeVerification.cs',
  streamer: 'Assets/Assets/Scripts/Generated/YQPlayerFollowingSemanticChunkStreamer.cs',
};
const contractSources = {
  unity: 'ProjectSettings/ProjectVersion.txt',
  state: 'Assets/Assets/Scripts/Data/State/YQStateFoundation.cs',
  cell: 'Assets/Assets/Scripts/Generated/YQContinuousWorldCellAuthority.cs',
};
const digest = bytes => createHash('sha256').update(bytes).digest('hex');

export class AccessError extends Error {
  constructor(code, message) { super(message); this.code = code; }
}

// note: Curated documents are the access boundary; generated ledger paths never widen it.
function documentPath(id) {
  if (Object.hasOwn(documents, id)) return documents[id];
  if (goalIds.includes(id)) return `${roadmap}/goals/Goal-${id.slice(1)}.md`;
  if (typeof id === 'string' && id.startsWith('receipt:') && receiptName.test(id.slice(8))) return `Docs/${id.slice(8)}`;
  throw new AccessError('DENIED_DOCUMENT', 'Document ID is not allowlisted.');
}

// note: Defence in depth for credentials accidentally pasted into an otherwise approved document.
function redact(text) {
  let changed = false;
  const replace = (pattern, replacement) => { text = text.replace(pattern, (...args) => { changed = true; return replacement(...args); }); };
  replace(/(["']?authorization["']?\s*[:=]\s*)(?:"[^"\r\n]*"|'[^'\r\n]*'|[^\r\n]+)/gi, (_, prefix) => `${prefix}[REDACTED]`);
  replace(/(["']?(?:api[_-]?key|access[_-]?token|refresh[_-]?token|client[_-]?secret|password)["']?\s*[:=]\s*)(?:"[^"\r\n]*"|'[^'\r\n]*'|[^\s,;\r\n]+)/gi, (_, prefix) => `${prefix}[REDACTED]`);
  replace(/\bBearer\s+[A-Za-z0-9._~+\/-]+/gi, () => 'Bearer [REDACTED]');
  replace(/\b(?:sk-[A-Za-z0-9_-]{16,}|gh[pousr]_[A-Za-z0-9_]{20,})\b/g, () => '[REDACTED]');
  replace(/-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----/g, () => '[REDACTED PRIVATE KEY]');
  replace(/(https?:\/\/)[^\s/@:]+:[^\s/@]+@/gi, (_, scheme) => `${scheme}[REDACTED]@`);
  return { text, redacted: changed };
}

export function createProjectReader(projectRoot) {
  const root = realpathSync(resolve(projectRoot));
  const within = path => {
    const rel = relative(root, path);
    return !isAbsolute(rel) && rel !== '..' && !rel.startsWith(`..${sep}`);
  };

  // note: Reject symlinks/junctions at every component and verify the opened file before returning bytes.
  function guardedPath(path) {
    if (typeof path !== 'string' || path.includes('\\') || path.includes(':') || path.includes('\0') || path.split('/').some(part => !part || part === '.' || part === '..')) {
      throw new AccessError('DENIED_PATH', 'Invalid contained path.');
    }
    let current = root;
    for (const part of path.split('/')) {
      current = resolve(current, part);
      if (!within(current)) throw new AccessError('DENIED_PATH', 'Path leaves the project root.');
      const stat = lstatSync(current);
      if (stat.isSymbolicLink() || !within(realpathSync(current))) throw new AccessError('DENIED_LINK', 'Symlink or junction access is denied.');
    }
    return current;
  }

  function read(path, maximum = 2 * 1024 * 1024) {
    let fd;
    try {
      const full = guardedPath(path);
      fd = openSync(full, constants.O_RDONLY | (constants.O_NOFOLLOW ?? 0));
      const before = fstatSync(fd);
      if (!before.isFile() || before.size > maximum) throw new AccessError('FILE_LIMIT', 'Document is not a bounded regular file.');
      const bytes = Buffer.alloc(before.size);
      let offset = 0;
      while (offset < bytes.length) {
        const count = readSync(fd, bytes, offset, bytes.length - offset, offset);
        if (!count) throw new AccessError('CHANGED_DURING_READ', 'Document changed during inspection; retry.');
        offset += count;
      }
      const after = fstatSync(fd);
      const current = lstatSync(guardedPath(path));
      if (before.ino !== after.ino || before.size !== after.size || before.mtimeMs !== after.mtimeMs || current.ino !== after.ino || current.dev !== after.dev || current.size !== after.size || current.mtimeMs !== after.mtimeMs) {
        throw new AccessError('CHANGED_DURING_READ', 'Document changed during inspection; retry.');
      }
      return { path, sha256: digest(bytes), bytes: bytes.length, modifiedUtc: after.mtime.toISOString(), text: bytes.toString('utf8') };
    } catch (error) {
      if (error instanceof AccessError) throw error;
      throw new AccessError('UNAVAILABLE_DOCUMENT', 'Approved document is unavailable.');
    } finally { if (fd !== undefined) closeSync(fd); }
  }

  // note: Only fixed, non-shell Git metadata commands run; disable fsmonitor execution and optional index writes.
  function gitState() {
    try {
      const options = { cwd: root, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'], timeout: 5000, maxBuffer: 1024 * 1024, windowsHide: true, env: { ...process.env, GIT_OPTIONAL_LOCKS: '0' } };
      const prefix = ['--no-optional-locks', '-c', 'core.fsmonitor=false', '-c', 'core.untrackedCache=false'];
      const head = execFileSync('git', [...prefix, 'rev-parse', 'HEAD'], options).trim();
      const output = execFileSync('git', [...prefix, 'status', '--porcelain=v1', '-z', '--no-renames', '--untracked-files=normal'], options);
      const changes = output.split('\0').filter(Boolean).map(line => ({ status: line.slice(0, 2), path: line.slice(3).replace(/\\/g, '/') }));
      return { available: true, head, checkoutDirty: changes.length > 0, changedEntryCount: changes.length, changes };
    } catch { return { available: false, head: null, checkoutDirty: null, changedEntryCount: null, changes: [] }; }
  }
  const statusFor = (path, git) => git.available ? (git.changes.find(change => change.path === path || (change.path.endsWith('/') && path.startsWith(change.path)))?.status ?? 'not_reported_as_changed') : 'unknown';
  const provenance = (file, git) => ({ path: file.path, sha256: file.sha256, bytes: file.bytes, modifiedUtc: file.modifiedUtc, gitStatus: statusFor(file.path, git) });

  function loadLedger() {
    const file = read(ledgerPath);
    let data;
    try { data = JSON.parse(file.text); } catch { throw new AccessError('INVALID_LEDGER', 'Goal index is invalid JSON.'); }
    if (data.schemaVersion !== 1 || !Array.isArray(data.goals) || data.goals.length !== 20 || new Set(data.goals.map(goal => goal.id)).size !== 20 || !goalIds.every(id => data.goals.some(goal => goal.id === id)) || !goalIds.includes(data.activeGoalId) || !/^\d{4}-\d{2}-\d{2}$/.test(data.observedOn ?? '')) {
      throw new AccessError('INVALID_LEDGER', 'Goal index must identify all 20 canonical goals and an observation date.');
    }
    for (const goal of data.goals) {
      if (goal.specification !== documentPath(goal.id) || !Array.isArray(goal.dependencies) || goal.dependencies.some(id => !goalIds.includes(id) || id >= goal.id) || !['historical', 'active', 'queued', 'complete'].includes(goal.workflowState) || !['NOT_VERIFIED', 'PARTIAL', 'FAIL', 'PASS', 'BLOCKED', 'NOT_YET_TESTABLE'].includes(goal.acceptanceStatus)) {
        throw new AccessError('INVALID_LEDGER', 'Goal index has an invalid specification path or dependency.');
      }
    }
    const active = data.goals.filter(goal => goal.workflowState === 'active');
    if (active.length !== 1 || active[0].id !== data.activeGoalId) throw new AccessError('INVALID_LEDGER', 'Goal index must select exactly one active workflow.');
    return { file, data };
  }

  function evidenceMetadata(path, git, hashCache) {
    const file = read(path);
    const text = file.text;
    const field = name => text.match(new RegExp(`^- ${name}: ([^\\r\\n]+)`, 'm'))?.[1];
    const setup = field('measurementProfilerSetup') ?? '';
    const runtime = Boolean(field('startedUtc'));
    const instrumented = /(?:enabled|binaryLog|deepProfiling|traceRequested)=True/.test(setup);
    const reportedResult = field('result')?.match(/^(PASS|FAIL|PARTIAL|BLOCKED|NOT_VERIFIED|NOT_YET_TESTABLE)\b/)?.[1]
      ?? text.match(/Status:\s*(?:diagnostic\s+)?(PASS|FAIL|PARTIAL|BLOCKED|NOT_VERIFIED)/i)?.[1]?.toUpperCase() ?? null;
    const sourceComparisons = [];
    for (const [owner, sourcePath] of Object.entries(ownerSources)) {
      const reported = field(`${owner}SourceSha256`)?.match(/\bsha256=([a-f0-9]{64})\b/i)?.[1]?.toLowerCase();
      if (!reported) continue;
      if (!hashCache.has(owner)) {
        try { hashCache.set(owner, read(sourcePath, 4 * 1024 * 1024).sha256); } catch { hashCache.set(owner, null); }
      }
      sourceComparisons.push({ owner, path: sourcePath, reportedSha256: reported, currentSha256: hashCache.get(owner), matches: hashCache.get(owner) === reported });
    }
    return {
      id: `receipt:${path.slice(5)}`, ...provenance(file, git),
      evidenceClass: runtime ? 'historical_runtime_receipt' : 'curated_summary_or_analysis',
      observedUtc: field('completedUtc') ?? field('finishedUtc') ?? field('startedUtc') ?? null,
      startedUtc: field('startedUtc') ?? null, reportedResult,
      measurementMode: runtime && setup ? (instrumented ? 'instrumented' : 'clean') : 'not_declared',
      reportedScope: redact((field('verificationScope') ?? '').slice(0, 600)).text || null,
      reportedAssemblySha256: field('executingAssemblySha256')?.match(/\bsha256=([a-f0-9]{64})\b/i)?.[1]?.toLowerCase() ?? null,
      sourceComparisons, certifiesCurrentCheckout: false,
    };
  }

  // note: Discovery is shallow and bounded to curated goal receipts, excluding Logs, outputs and hidden stores.
  function findEvidenceInternal(goalId, limit, query, git) {
    const warnings = [];
    const entries = readdirSync(guardedPath('Docs'), { withFileTypes: true });
    const candidates = [];
    for (const entry of entries) {
      if (!receiptName.test(entry.name) || !entry.name.startsWith(`${goalId}_`) || (query && !entry.name.toLowerCase().includes(query.toLowerCase()))) continue;
      if (entry.isSymbolicLink()) { warnings.push({ code: 'EXCLUDED_LINK', path: `Docs/${entry.name}` }); continue; }
      if (!entry.isFile()) continue;
      const path = `Docs/${entry.name}`;
      try { candidates.push({ path, modified: lstatSync(guardedPath(path)).mtimeMs }); }
      catch { warnings.push({ code: 'UNAVAILABLE_EVIDENCE', path }); }
    }
    candidates.sort((a, b) => b.modified - a.modified || a.path.localeCompare(b.path));
    const hashCache = new Map();
    const results = [];
    for (const candidate of candidates.slice(0, 32)) {
      try { results.push(evidenceMetadata(candidate.path, git, hashCache)); }
      catch (error) { warnings.push({ code: error.code, path: candidate.path }); }
    }
    results.sort((a, b) => (b.observedUtc ?? b.modifiedUtc).localeCompare(a.observedUtc ?? a.modifiedUtc) || a.path.localeCompare(b.path));
    return { candidates: candidates.length, inspected: Math.min(candidates.length, 32), discoveryTruncated: candidates.length > 32, evidence: results.slice(0, limit), warnings };
  }

  function readDocument({ id, startLine = 1, lineCount = 100 }) {
    const file = read(documentPath(id));
    const clean = redact(file.text);
    const lines = clean.text.split(/\r?\n/);
    const selected = lines.slice(startLine - 1, startLine - 1 + lineCount);
    let used = 0;
    let truncated = false;
    const output = [];
    for (const line of selected) {
      if (used + Math.min(line.length, 2000) > 24000) { truncated = true; break; }
      if (line.length > 2000) truncated = true;
      output.push(line.slice(0, 2000));
      used += output.at(-1).length;
    }
    const relatedDocuments = goalIds.includes(id) ? ['AGENTS', 'GOAL_STATUS', ...(id === 'G08' ? ['G08_WORKFLOW'] : [])] : [];
    return { id, ...provenance(file, gitState()), totalLines: lines.length, startLine, returnedLines: output.length, nextLine: startLine + output.length <= lines.length ? startLine + output.length : null, text: output.join('\n'), redacted: clean.redacted, longLineOrByteLimitReached: truncated, relatedDocuments, inspectionLevel: 'SOURCE_INSPECTED', runtimeVerified: false };
  }

  function findEvidence({ goalId, limit = 5, query = '' }) {
    const { data } = loadLedger();
    const result = findEvidenceInternal(goalId, limit, query, gitState());
    if (result.evidence.some(item => (item.observedUtc ?? item.modifiedUtc).slice(0, 10) > data.observedOn)) result.warnings.push({ code: 'NEWER_EVIDENCE_THAN_INDEX', indexObservedOn: data.observedOn });
    if (result.evidence.some(item => item.sourceComparisons.some(source => !source.matches))) result.warnings.push({ code: 'REPORTED_SOURCE_DIFFERS', message: 'Affected source changed or is unavailable; review dependencies before reusing this receipt.' });
    return { goalId, indexObservedOn: data.observedOn, ...result, inspectionLevel: 'SOURCE_INSPECTED', runtimeVerified: false };
  }

  function getSnapshot() {
    const { file: index, data } = loadLedger();
    const git = gitState();
    const warnings = [];
    if (!git.available) warnings.push({ code: 'GIT_UNAVAILABLE', message: 'Working-tree provenance is unknown; untracked evidence may exist.' });
    const goals = data.goals.map(goal => {
      const spec = read(documentPath(goal.id));
      const sections = [...spec.text.matchAll(/^## (\d+)\. [^\r\n]+/gm)].map(match => Number(match[1]));
      const requiredSectionsPresent = sections.length === 13 && sections.every((number, index) => number === index + 1);
      if (!requiredSectionsPresent) warnings.push({ code: 'INCOMPLETE_SPECIFICATION_STRUCTURE', goalId: goal.id });
      const evidence = (goal.evidence ?? []).map(entry => {
        if (typeof entry.path !== 'string' || !entry.path.startsWith(`Docs/${goal.id}_`) || !receiptName.test(entry.path.slice(5))) {
          warnings.push({ code: 'DENIED_INDEXED_EVIDENCE', goalId: goal.id });
          return { available: false };
        }
        try {
          const receipt = read(entry.path);
          const matches = receipt.sha256 === entry.sha256;
          const actualReportedResult = receipt.text.match(/^- result: (PASS|FAIL|PARTIAL|BLOCKED|NOT_VERIFIED|NOT_YET_TESTABLE)\b/m)?.[1] ?? null;
          const resultMatches = entry.reportedResult ? entry.reportedResult === actualReportedResult : null;
          if (!matches) warnings.push({ code: 'INDEXED_RECEIPT_HASH_DIFFERS', goalId: goal.id, path: entry.path });
          if (resultMatches === false) warnings.push({ code: 'INDEXED_RESULT_DIFFERS', goalId: goal.id, path: entry.path });
          return { ...provenance(receipt, git), indexedSha256: entry.sha256, hashMatches: matches, observedUtc: entry.observedUtc, evidenceLevel: entry.evidenceLevel, indexedReportedResult: entry.reportedResult, actualReportedResult, resultMatches, coverage: redact(String(entry.coverage ?? '').slice(0, 600)).text || null, certifiesCurrentCheckout: false };
        } catch (error) { warnings.push({ code: error.code, goalId: goal.id, path: entry.path }); return { path: entry.path, available: false }; }
      });
      if (spec.sha256 !== goal.specificationSha256) warnings.push({ code: 'SPECIFICATION_HASH_DIFFERS', goalId: goal.id });
      // note: Flag unsupported bookkeeping without converting any record into a current acceptance verdict.
      const passEvidence = evidence.some(entry => entry.hashMatches && entry.actualReportedResult === 'PASS' && entry.resultMatches !== false);
      if ((goal.workflowState === 'complete' || goal.acceptanceStatus === 'PASS') && (goal.acceptanceStatus !== 'PASS' || !passEvidence || !goal.acceptanceRationale || !requiredSectionsPresent)) warnings.push({ code: 'UNSUPPORTED_COMPLETION_RECORD', goalId: goal.id });
      return { id: goal.id, title: redact(String(goal.title).slice(0, 300)).text, dependencies: goal.dependencies, workflowState: goal.workflowState, acceptanceStatus: goal.acceptanceStatus, indexedAcceptanceRationale: redact(String(goal.acceptanceRationale ?? '').slice(0, 2000)).text || null, specification: provenance(spec, git), indexedSpecificationSha256: goal.specificationSha256, specificationHashMatches: spec.sha256 === goal.specificationSha256, requiredSectionsPresent, evidence };
    });
    let activeWorkflow = null;
    if (data.activeWorkflow?.path) {
      if (data.activeGoalId !== 'G08' || data.activeWorkflow.goalId !== 'G08' || data.activeWorkflow.path !== documents.G08_WORKFLOW) warnings.push({ code: 'DENIED_ACTIVE_WORKFLOW' });
      else {
        const workflow = read(documents.G08_WORKFLOW);
        activeWorkflow = { id: 'G08_WORKFLOW', ...provenance(workflow, git), hashMatches: workflow.sha256 === data.activeWorkflow.sha256 };
        if (!activeWorkflow.hashMatches) warnings.push({ code: 'ACTIVE_WORKFLOW_HASH_DIFFERS' });
      }
    }
    const version = read(contractSources.unity);
    const state = read(contractSources.state);
    const cell = read(contractSources.cell);
    const liveContracts = {
      unityVersion: version.text.match(/^m_EditorVersion: (.+)$/m)?.[1]?.trim() ?? null,
      saveSchema: Number(state.text.match(/\bCurrentStateSchemaVersion\s*=\s*(\d+)\s*;/)?.[1]) || null,
      cellSchema: cell.text.match(/\bSchemaVersion\s*=\s*"([^"]+)"/)?.[1] ?? null,
      edgeSchema: cell.text.match(/\bEdgeContractVersion\s*=\s*"([^"]+)"/)?.[1] ?? null,
      sources: [version, state, cell].map(source => provenance(source, git)),
    };
    for (const key of ['unityVersion', 'saveSchema', 'cellSchema', 'edgeSchema']) if (liveContracts[key] !== data.sourceContracts?.[key]) warnings.push({ code: 'CONTRACT_DIFFERS_FROM_INDEX', contract: key });
    const latest = findEvidenceInternal(data.activeGoalId, 6, '', git);
    warnings.push(...latest.warnings);
    if (latest.evidence.some(item => (item.observedUtc ?? item.modifiedUtc).slice(0, 10) > data.observedOn)) warnings.push({ code: 'NEWER_EVIDENCE_THAN_INDEX', indexObservedOn: data.observedOn });
    if (latest.evidence.some(item => item.sourceComparisons.some(source => !source.matches))) warnings.push({ code: 'REPORTED_SOURCE_DIFFERS', message: 'Receipt identity does not cover all current affected dependencies.' });
    return {
      observedAtUtc: new Date().toISOString(), inspectionLevel: 'SOURCE_INSPECTED', runtimeVerified: false,
      authority: 'Workflow bookkeeping and source identity only; canonical goals define acceptance and accepted saves own generated canon.',
      index: { ...provenance(index, git), observedOn: data.observedOn },
      git: { available: git.available, head: git.head, checkoutDirty: git.checkoutDirty, changedEntryCount: git.changedEntryCount },
      activeGoalId: data.activeGoalId, nextGoalAfterAcceptance: data.nextGoalAfterAcceptance,
      indexedBlockers: Array.isArray(data.currentBlockers) ? data.currentBlockers.slice(0, 10).map(value => redact(String(value).slice(0, 600)).text) : [],
      goals, activeWorkflow, liveContracts, latestEvidence: latest.evidence,
      evidenceDiscovery: { candidates: latest.candidates, inspected: latest.inspected, truncated: latest.discoveryTruncated }, warnings,
    };
  }
  return { getSnapshot, readDocument, findEvidence };
}

export const documentIds = [...Object.keys(documents), ...goalIds];
export const canonicalGoalIds = goalIds;
