import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { resolve, relative, dirname, extname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';

export const projectRoot = fileURLToPath(new URL('../', import.meta.url));
export const ledgerPath = 'Docs/ClosedBetaRoadmap_2026-09-16/GOAL_STATUS.json';
const roadmapPath = 'Docs/ClosedBetaRoadmap_2026-09-16';
export const sha256 = text => createHash('sha256').update(text).digest('hex');
const normalized = text => text.replace(/\r\n/g, '\n').trim();

// note: Reading views live one directory above goals, so equivalent relative links have different literal targets.
function readingViewText(root, text, sourcePath) {
  return normalized(text).replace(/(\[[^\]\n]*\]\()(<[^>]+>|[^)\n]+)(\))/g, (match, prefix, target, suffix) => {
    const wrapped = target.startsWith('<');
    const bare = wrapped ? target.slice(1, -1) : target;
    if (!bare || /^(?:[a-z]+:|\/|#)/i.test(bare)) return match;
    const hash = bare.indexOf('#');
    const path = hash < 0 ? bare : bare.slice(0, hash);
    const fragment = hash < 0 ? '' : bare.slice(hash);
    const rebased = relative(resolve(root, roadmapPath), resolve(root, dirname(sourcePath), path)).replace(/\\/g, '/') + fragment;
    return `${prefix}${wrapped ? `<${rebased}>` : rebased}${suffix}`;
  });
}

// note: Validate only guidance directories; imported assets and Unity caches are outside this tool's scope.
function markdownFiles(root, directory) {
  const base = resolve(root, directory);
  if (!existsSync(base)) return [];
  return readdirSync(base, { withFileTypes: true }).flatMap(entry => {
    const path = `${directory}/${entry.name}`;
    if (entry.isDirectory()) return markdownFiles(root, path);
    return entry.isFile() && extname(entry.name) === '.md' ? [path] : [];
  });
}

function prerequisiteIds(text) {
  const ids = new Set(text.match(/\bG\d{2}\b/g) ?? []);
  for (const range of text.matchAll(/\bG(\d{2})[–—-](?:G)?(\d{2})\b/g)) {
    for (let number = Number(range[1]); number <= Number(range[2]); number++) {
      ids.add(`G${String(number).padStart(2, '0')}`);
    }
  }
  return [...ids].sort();
}

export function validateProjectGuidance(root = projectRoot, { guidanceOnly = false } = {}) {
  const errors = [];
  const warnings = [];
  let checks = 0;
  const check = (condition, message) => { checks++; if (!condition) errors.push(message); };
  const read = path => {
    const fullPath = resolve(root, path);
    check(existsSync(fullPath), `Missing required file: ${path}`);
    return existsSync(fullPath) ? readFileSync(fullPath, 'utf8') : '';
  };
  let ledger;
  try { ledger = JSON.parse(read(ledgerPath)); }
  catch (error) { return { status: 'FAIL', checks, errors: [...errors, `Invalid goal ledger: ${error.message}`], warnings }; }
  check(ledger.schemaVersion === 1, 'Unsupported goal-ledger schema.');
  check(/^\d{4}-\d{2}-\d{2}$/.test(ledger.observedOn ?? ''), 'Ledger must record its observation date.');
  const goals = Array.isArray(ledger.goals) ? ledger.goals : [];
  check(goals.length === 20, 'Expected exactly twenty numbered goals.');
  const expectedIds = Array.from({ length: 20 }, (_, i) => `G${String(i + 1).padStart(2, '0')}`);
  const ids = new Set(goals.map(goal => goal.id));
  check(ids.size === 20 && expectedIds.every(id => ids.has(id)), 'Goal IDs must be unique G01–G20.');
  const active = goals.filter(goal => goal.workflowState === 'active');
  check(active.length === 1 && active[0].id === ledger.activeGoalId, 'Ledger must select exactly one active goal.');
  const specificationView = normalized(read(`${roadmapPath}/Goal-specifications.md`));
  const roadmapView = normalized(read(`${roadmapPath}/ROADMAP.md`));

  // note: A hash binds workflow bookkeeping to the complete specification; it cannot certify runtime acceptance.
  for (const goal of goals) {
    const expectedPath = `${roadmapPath}/goals/Goal-${goal.id?.slice(1)}.md`;
    check(goal.specification === expectedPath, `${goal.id}: specification must use its canonical path.`);
    const text = read(expectedPath);
    check(sha256(text) === goal.specificationSha256, `${goal.id}: specification hash differs; refresh the ledger after reviewing the change. Actual: ${sha256(text)}`);
    const sections = [...text.matchAll(/^## (\d+)\. ([^\r\n]+)/gm)].map(match => Number(match[1]));
    check(sections.length === 13 && sections.every((number, i) => number === i + 1), `${goal.id}: required sections 1–13 must occur once in order.`);
    const viewText = readingViewText(root, text, expectedPath);
    check(specificationView.includes(viewText), `${goal.id}: Goal-specifications.md has diverged from the canonical goal.`);
    check(roadmapView.includes(viewText), `${goal.id}: ROADMAP.md has diverged from the canonical goal.`);
    const prerequisites = text.match(/^## 4\. PREREQUISITES\r?\n([\s\S]*?)(?=^## )/m)?.[1] ?? '';
    const declared = Array.isArray(goal.dependencies) ? goal.dependencies : [];
    check(JSON.stringify([...declared].sort()) === JSON.stringify(prerequisiteIds(prerequisites)), `${goal.id}: dependencies do not match its prerequisites.`);
    for (const dependency of declared) {
      check(ids.has(dependency) && dependency < goal.id, `${goal.id}: invalid, cyclic, or forward dependency ${dependency}.`);
    }
    check(['historical', 'active', 'queued', 'complete'].includes(goal.workflowState), `${goal.id}: invalid workflow state.`);
    check(['NOT_VERIFIED', 'PARTIAL', 'FAIL', 'PASS', 'BLOCKED', 'NOT_YET_TESTABLE'].includes(goal.acceptanceStatus), `${goal.id}: invalid acceptance status.`);
    const evidence = Array.isArray(goal.evidence) ? goal.evidence : [];
    if (goal.workflowState === 'complete' || goal.acceptanceStatus === 'PASS') {
      check(goal.acceptanceStatus === 'PASS' && evidence.length > 0 && Boolean(goal.acceptanceRationale), `${goal.id}: completion requires PASS evidence and an acceptance rationale.`);
    }
    for (const receipt of evidence) {
      const receiptText = read(receipt.path);
      check(sha256(receiptText) === receipt.sha256, `${goal.id}: receipt changed: ${receipt.path}`);
      check(Boolean(receipt.observedUtc) && Boolean(receipt.evidenceLevel), `${goal.id}: receipt needs time and evidence level.`);
      if (receipt.reportedResult) check(receiptText.includes(`- result: ${receipt.reportedResult}`), `${goal.id}: receipt result differs from the ledger.`);
    }
  }
  const workflow = ledger.activeWorkflow;
  if (ledger.activeGoalId === 'G08') check(workflow?.path === `${roadmapPath}/goals/8 fix 4.md`, 'G08 must retain its selected fix 4 workflow.');
  if (workflow?.path) {
    const workflowText = read(workflow.path);
    check(sha256(workflowText) === workflow.sha256, 'Active workflow hash differs from the ledger.');
    check(workflow.goalId === ledger.activeGoalId, 'Active workflow belongs to a different selected goal.');
  }
  for (const historicalWorkflow of ledger.historicalWorkflows ?? []) {
    const text = read(historicalWorkflow.path);
    check(sha256(text) === historicalWorkflow.sha256, `Historical workflow changed: ${historicalWorkflow.path}`);
  }

  // note: Check portable documentation links while allowing explicitly historical external-machine/log references.
  const rootGuides = readdirSync(root, { withFileTypes: true }).filter(entry => entry.isFile() && entry.name.endsWith('.md')).map(entry => entry.name);
  const guidance = [...rootGuides, ...markdownFiles(root, 'AI_CONTEXT'), ...markdownFiles(root, 'PROMPTS'), ...markdownFiles(root, roadmapPath), 'Docs/PROJECT_SETUP.md', 'Docs/PROJECT_GUIDANCE_AUDIT_2026-09-30.md'];
  for (const path of [...new Set(guidance)]) {
    const content = read(path).replace(/```[\s\S]*?```/g, '');
    for (const match of content.matchAll(/\[[^\]\n]*\]\((<[^>]+>|[^)\n]+)\)/g)) {
      const target = match[1].replace(/^<|>$/g, '').split('#')[0];
      if (!target || /^(?:[a-z]+:|\/)/i.test(target)) continue;
      let decoded;
      try { decoded = decodeURIComponent(target); } catch { check(false, `${path}: invalid link encoding ${target}`); continue; }
      const linked = resolve(dirname(resolve(root, path)), decoded);
      const fromRoot = relative(root, linked).replace(/\\/g, '/');
      if (fromRoot.startsWith('../') || /^(?:Logs|Temp|Library|outputs)\//.test(fromRoot)) continue;
      if (!['.md', '.json', '.mjs', '.yml', '.yaml'].includes(extname(linked))) continue;
      check(existsSync(linked), `${path}: broken documentation link ${target}`);
    }
  }
  for (const path of ['AGENTS.md', 'AI_DEV_WORKFLOW.md', 'MODEL_ROUTING.md', 'TASK_ROUTING_MATRIX.md', 'ARCHITECTURE_INDEX.md', 'PROMPTS/COMMON_TASK_TEMPLATES.md']) {
    const text = read(path);
    check(!text.includes('reproduce in `SampleScene`'), `${path}: stale SampleScene execution target.`);
    check(!/schemas (?:are )?6|schemas? is 6/.test(text), `${path}: schema 6 is incorrectly described as current.`);
  }
  if (!guidanceOnly) {
    const version = read('ProjectSettings/ProjectVersion.txt');
    check(version.includes(`m_EditorVersion: ${ledger.sourceContracts.unityVersion}`), 'Unity version differs from the recorded setup contract.');
    const state = read('Assets/Assets/Scripts/Data/State/YQStateFoundation.cs');
    check(state.includes(`CurrentStateSchemaVersion = ${ledger.sourceContracts.saveSchema};`), 'Save schema differs from live guidance.');
    const cell = read('Assets/Assets/Scripts/Generated/YQContinuousWorldCellAuthority.cs');
    check(cell.includes(`SchemaVersion = "${ledger.sourceContracts.cellSchema}"`) && cell.includes(`EdgeContractVersion = "${ledger.sourceContracts.edgeSchema}"`), 'Continuous-world contracts differ from the ledger.');
    const buildSettings = read('ProjectSettings/EditorBuildSettings.asset');
    for (const scene of ['YourQuest_PlaySafe', 'YourQuest_TitleEnvironment']) {
      const path = `Assets/Assets/Scenes/${scene}.unity`;
      read(path);
      const meta = read(`${path}.meta`);
      const guid = meta.match(/^guid: ([a-f0-9]+)$/m)?.[1];
      check(Boolean(guid) && buildSettings.includes(`path: ${path}`) && buildSettings.includes(`guid: ${guid}`), `Scene/build GUID mismatch: ${scene}`);
    }
  } else warnings.push('Guidance-only mode: Unity source, scene references, compilation, and runtime are not verified.');
  return { status: errors.length ? 'FAIL' : 'PASS', checks, goals: goals.length, activeGoal: ledger.activeGoalId, errors, warnings };
}

// note: Read-only CLI checks are suitable for both a dirty local checkout and a documentation review branch.
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const result = validateProjectGuidance(projectRoot, { guidanceOnly: process.argv.includes('--guidance-only') });
  console.log(JSON.stringify(result, null, 2));
  process.exitCode = result.errors.length ? 1 : 0;
}
