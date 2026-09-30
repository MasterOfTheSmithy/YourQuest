import { mkdtempSync, cpSync, readFileSync, writeFileSync, rmSync, readdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve, join, sep, basename } from 'node:path';
import { validateProjectGuidance, projectRoot, ledgerPath } from './verify-project-guidance.mjs';

// note: Exercise failed handoffs on an isolated documentation copy, never on the dirty Unity checkout.
const tempRoot = resolve(tmpdir());
const fixture = mkdtempSync(join(tempRoot, 'yourquest-guidance-test-'));
const results = [];
try {
  for (const entry of readdirSync(projectRoot, { withFileTypes: true })) {
    if (entry.isFile() && entry.name.endsWith('.md')) cpSync(join(projectRoot, entry.name), join(fixture, entry.name));
  }
  for (const directory of ['Docs', 'AI_CONTEXT', 'PROMPTS']) cpSync(join(projectRoot, directory), join(fixture, directory), { recursive: true });
  const run = () => validateProjectGuidance(fixture, { guidanceOnly: true });
  const positive = run();
  if (positive.errors.length) throw new Error(`Fixture must pass before negative checks: ${positive.errors.join('; ')}`);
  results.push({ case: 'Equivalent rebased reading-view links', status: 'PASS' });
  const ledgerFile = resolve(fixture, ledgerPath);
  const ledgerText = readFileSync(ledgerFile, 'utf8');
  const invalidLedger = (label, mutate, expected) => {
    const ledger = JSON.parse(ledgerText);
    mutate(ledger);
    writeFileSync(ledgerFile, JSON.stringify(ledger));
    const result = run();
    writeFileSync(ledgerFile, ledgerText);
    if (!result.errors.some(error => error.includes(expected))) throw new Error(`${label} was not rejected: ${result.errors.join('; ')}`);
    results.push({ case: label, status: 'PASS' });
  };
  invalidLedger('Forward or cyclic dependency', ledger => { ledger.goals[0].dependencies = ['G20']; }, 'forward dependency');
  invalidLedger('Duplicate active workflows', ledger => { ledger.goals[8].workflowState = 'active'; }, 'exactly one active goal');
  invalidLedger('PASS without supporting acceptance evidence', ledger => { ledger.goals[8].acceptanceStatus = 'PASS'; }, 'completion requires PASS evidence');
  const specification = resolve(fixture, 'Docs/ClosedBetaRoadmap_2026-09-16/goals/Goal-08.md');
  const original = readFileSync(specification, 'utf8');
  writeFileSync(specification, original.replace('## 13. COMPLETION GATE', '## OMITTED COMPLETION GATE'));
  if (!run().errors.some(error => error.includes('sections 1–13'))) throw new Error('Missing completion section was accepted.');
  writeFileSync(specification, original);
  results.push({ case: 'Missing completion gate', status: 'PASS' });
  const readingView = resolve(fixture, 'Docs/ClosedBetaRoadmap_2026-09-16/Goal-specifications.md');
  const originalView = readFileSync(readingView, 'utf8');
  writeFileSync(readingView, originalView.replace('## 13. COMPLETION GATE', '## OMITTED COMPLETION GATE'));
  if (!run().errors.some(error => error.includes('has diverged'))) throw new Error('Diverged reading view was accepted.');
  results.push({ case: 'Diverged aggregate specification', status: 'PASS' });
  console.log(JSON.stringify({ status: 'PASS', cases: results }, null, 2));
} finally {
  // note: Only remove the exact minted fixture after proving its absolute path stays under the intended temporary root.
  const target = resolve(fixture);
  if (!target.startsWith(tempRoot + sep) || !basename(target).startsWith('yourquest-guidance-test-')) throw new Error('Unexpected cleanup target.');
  rmSync(target, { recursive: true, force: true });
}
