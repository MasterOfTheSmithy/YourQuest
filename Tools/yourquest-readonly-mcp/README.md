# YourQuest read-only MCP

Local, dependency-free Node 22+ tooling for a dated Space view of the production checkout. The three tools inspect requirements and provenance; they do not certify a goal or change game state.

| Tool | Inputs | Data |
| --- | --- | --- |
| `get_project_snapshot` | none | All 20 indexed goals/dependencies/statuses, specification and workflow hashes, current source contracts, bounded recent evidence, checkout dirtiness and warnings |
| `read_project_document` | named `id`, optional `startLine`/`lineCount` | Up to 200 lines / 24,000 characters; original file hash, time and Git status; next-page pointer and redaction/truncation flags |
| `find_project_evidence` | `goalId`, optional literal filename `query`/`limit` | Shallow curated `Docs/GNN_*.md` metadata; at most 32 candidates inspected and 20 results returned; reported scope/result/instrumentation and verifier/streamer hash comparisons |

Document IDs are listed by `tools/list`. `G01` through `G20` select canonical numbered specifications. `G08_WORKFLOW` selects fix 4; it remains a separate complete document. Follow `relatedDocuments` and pagination to read the complete selected contract. `receipt:<basename>` reads only approved curated goal documents returned by evidence discovery.

The server mirrors the existing goal assembler's authority order, without executing the assembler or its broad document validation through MCP. It validates the 20-goal index and uses canonical paths. ROADMAP reading views, historical audits and all runtime receipts retain their evidence dates. A newer receipt triggers a warning, never an automatic acceptance update. Reported verifier/streamer hashes cover only those two sources; other dependencies, assembly identity, hardware, profile and runtime state require separate review. Raw logs, outputs, caches, saves, hidden chats, credentials and arbitrary source/path reads are unavailable.

## Run and test locally

From the repository root:

```powershell
node Tools/yourquest-readonly-mcp/server.mjs
node --test Tools/yourquest-readonly-mcp/server.test.mjs
node Tools/verify-project-guidance.mjs --guidance-only
```

The server uses stdio and exits when its input closes. Set `YQ_PROJECT_ROOT` in the launching host if the package is copied elsewhere. `mcp.json` anchors the installation to `C:/Users/Garri/YourQuest`; review that explicit root before registration. No dependencies, background service or network listener are installed.

The implementation targets the [2025-11-25 MCP lifecycle](https://modelcontextprotocol.io/specification/2025-11-25/basic/lifecycle) and [newline-delimited stdio transport](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports), with 2025-06-18 and 2024-11-05 negotiation. It returns structured results plus text and read-only tool annotations. The newer 2026 protocol and the actual host's compatibility are not certified by these local tests.

## Access boundary

Named IDs and canonical ledger paths cannot grant another path. Every read rejects traversal, symlinks/junctions and out-of-root resolution, verifies the opened regular file before/after reading, and applies a size bound. Evidence discovery is shallow. Common credential formats are redacted as defence in depth; only curate non-sensitive material into approved documents, since arbitrary secrets in prose cannot be recognized reliably. Returned hashes describe original bytes before redaction.

Git is used only for fixed `rev-parse HEAD` and porcelain `status` calls with no shell, no caller-controlled arguments, fsmonitor disabled, optional locks disabled and a five-second timeout. Tool responses expose aggregate checkout dirtiness and the requested approved file's status, not unrelated file names. No command-execution tool, filesystem-write tool, network client or upload exists. This process runs with its host's OS permissions; it is not a separate OS sandbox against a malicious concurrent process replacing project directories.

## Registration boundary

`plugin.json` and `mcp.json` are source package manifests. No marketplace, account plugin, global config, persistent permission or host connection has been changed. The repository skill is `.agents/skills/yourquest-evidence/SKILL.md`; host discovery of that new skill remains to be checked in a fresh session.

After reviewing the package and passing local tests, the next authorization is **register this local stdio package in the selected host, launching only its three read-only tools against this checkout, then verify discovery and harmless calls in that host**. Use the host's supported installation flow; do not edit installed caches. A local stdio registration does not make the service reachable from ChatGPT web/mobile. Remote access would need separately approved hosting/authentication and a reviewed data boundary. The parked local-thread access issue remains independent.
