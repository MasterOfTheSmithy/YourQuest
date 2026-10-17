# Board guide

YourQuest Board uses the standard Kanban community-plugin format with basic settings. Without the plugin it remains readable Markdown with linked cards.

## Status definitions

- Backlog: problem or opportunity recorded, scope not ready.
- Specified: owner, boundaries, dependencies, and acceptance written.
- Building: implementation actively changing.
- Built: local implementation exists and is ready for focused review.
- In Review: runtime, asset, serialization, or design review is still required.
- Reviewed: reviewer examined it and recorded findings.
- Merged: a real merge or integration record exists.
- Verified: current build and relevant runtime or artifact evidence pass.

Keep active Building work at five cards or fewer. Never use Merged as a synonym for code exists.

## Evidence convention

Task notes hold acceptance criteria, evidence, next action, and related work. Screenshots are observations; logs, play-mode checks, serialized references, build output, and reproducible artifact checks are stronger evidence. When evidence is missing, keep the card in review.
