import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { projectRoot, ledgerPath, sha256, validateProjectGuidance } from './verify-project-guidance.mjs';

// note: Assemble complete files without summarizing binding acceptance criteria or launching work.
try {
  const result = validateProjectGuidance(projectRoot, { guidanceOnly: true });
  if (result.errors.length) throw new Error(`Guidance validation failed:\n${result.errors.join('\n')}`);
  const ledger = JSON.parse(readFileSync(resolve(projectRoot, ledgerPath), 'utf8'));
  const id = (process.argv[2] ?? ledger.activeGoalId).toUpperCase();
  const goal = ledger.goals.find(entry => entry.id === id);
  if (!goal) throw new Error(`Unknown goal ${id}. Use G01 through G20.`);
  const paths = ['AGENTS.md', goal.specification];
  if (id === ledger.activeGoalId && ledger.activeWorkflow?.path) paths.push(ledger.activeWorkflow.path);
  const header = [
    `# YourQuest ${id} execution packet`,
    '',
    `Observed workflow: ${goal.workflowState}; acceptance: ${goal.acceptanceStatus}. Ledger date: ${ledger.observedOn}.`,
    `Dependencies: ${goal.dependencies.join(', ') || 'none'}.`,
    'This is a read-only handoff artifact. Confirm the current user request and affected evidence before acting.',
    'Historical routing names describe roles; use the available configured agent. No other goals or chats are launched.',
    '',
    'Receipt pointers:',
    ...(goal.evidence ?? []).map(entry => `- ${entry.path} (${entry.evidenceLevel}; ${entry.observedUtc}; ${entry.reportedResult ?? 'see receipt'})`),
    '',
    'Record actual checkout/source identity, coordinator, allowed files, and the next failing row before implementation.',
  ];
  console.log(header.join('\n'));
  for (const path of paths) {
    const text = readFileSync(resolve(projectRoot, path), 'utf8');
    console.log(`\n---\n\nSource: ${path}\nSHA-256: ${sha256(text)}\n\n${text}`);
  }
} catch (error) {
  console.error(error.message);
  process.exitCode = 1;
}
