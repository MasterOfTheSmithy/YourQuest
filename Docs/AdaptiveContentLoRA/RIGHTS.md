# Dataset rights and review policy

Allow only original YourQuest examples authored specifically for this project, original checked synthetic candidates, user-owned traces with explicit authorization, or material with recorded compatible licensing. Use external stories only for abstract design principles. Do not copy story dialogue, descriptions, quests, panels, subtitles, wiki dumps, fanfiction or long excerpts into prompts or training data.

Repository presence does not establish training rights. Imported packages/assets are excluded. The model's published Apache-2.0 license does not license unrelated content. Record actual base/tokenizer/license revisions when provenance is recovered; model-name similarity is insufficient. [Official base model card](https://huggingface.co/Qwen/Qwen3.5-4B)

Every record includes source type, author, source references, rights status, human-review status and explicit no-private-text/no-hidden-reasoning flags. These declarations require honest review; a metadata flag cannot detect all confidential or copyrighted text. Preserve source evidence and reviewer identity rather than auto-approving generated candidates.

Current seeds are original **agent-authored pending owner review**. They are not labelled human-reviewed. `export-training` requires both human approval and approved-original/licensed rights status. A schema-valid candidate remains pending until reviewed. Representative review must include ordinary/incubating cases, noncombat paths, adverse canon/capability cases, rarity calibration and naming counterfactuals.

Teacher pipeline stages are: prepare original train-family-only briefs; obtain authorization before any external/paid call; collect candidates with source/version records; schema/domain/canon/rarity checks; exact/near/semantic duplication review; rights and human review; then approved export. This package emits local briefs and processes explicit local candidate files only. It has no teacher API implementation and made no external teacher call.

Runtime-derived data is a separate future stage. Do not read saves, private player text, chat stores or raw captures by default. After authorization, sanitize stable event facts, remove identifiers/text not needed for the task, retain provenance privately under an approved policy and obtain review before adding examples. Accepted game identity prose can also contain private information; do not assume it is safe to upload.

Only concise decisions, evidence IDs and externally useful reasons belong in outputs. No hidden chain-of-thought or private model reasoning is collected or trained.
