"""Pure static Florence review: stdlib reads/hashes only; never imports model code."""
import ast
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Docs/PlayableEvaluation_2026-10-09/reviews/florence_native_primary"
UNIT = ROOT / "outputs/PlayableEvaluation_20261009/downloads/florence-native-2/unit.json"
FIXTURES = ROOT / "Tools/PlayableEvaluation/native_vision_tests/fixtures.json"
ARCHIVE = Path("E:/Temp text models")


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def identity(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return {"path": str(path), "bytes": path.stat().st_size, "sha256": digest.hexdigest()}


def main():
    # note: Compile means AST parsing here, not Python execution/import of inspected sources.
    runner_path = ROOT / "Tools/PlayableEvaluation/run_legacy_florence.py"
    runner_tree = ast.parse(runner_path.read_text(encoding="utf-8"))
    units, fixtures = read(UNIT), read(FIXTURES)
    baseline = fixtures["candidate_units"]["SRC-26"]
    by_name = {r["provenance"]["source_file"]: r for r in units["files"]}
    originals = {r["provenance"]["source_file"]: r for r in baseline["files"]}
    assert len(baseline["files"]) == 6 and baseline["bytes_total"] == 465680283
    assert len(units["files"]) == len(by_name) == 9
    assert all(by_name[name] == record for name, record in originals.items())
    constants = {n.targets[0].id: n.value for n in runner_tree.body
                 if isinstance(n, ast.Assign) and isinstance(n.targets[0], ast.Name)}
    pin = ast.literal_eval(constants["PIN"])
    source_guard = ast.literal_eval(constants["SOURCE"])
    assert set(by_name) == set(originals) | set(source_guard)
    identities = [identity(runner_path), identity(UNIT), identity(FIXTURES)]
    for record in units["files"]:
        path = ARCHIVE / record["master"]
        observed = identity(path)
        assert observed["sha256"] == record["sha256"] and observed["bytes"] == record["bytes"]
        assert record["provenance"]["revision"] == pin
        identities.append(observed)
    for name, (size, digest) in source_guard.items():
        assert by_name[name]["bytes"] == size and by_name[name]["sha256"] == digest
        ast.parse((ARCHIVE / by_name[name]["master"]).read_text(encoding="utf-8"))

    tokenizer = read(ARCHIVE / by_name["tokenizer.json"]["master"])
    vocabulary = tokenizer["model"]["vocab"]
    plain_vocab = read(ARCHIVE / by_name["vocab.json"]["master"])
    assert vocabulary == plain_vocab
    assert len(vocabulary) == 50265 and max(vocabulary.values()) == 50264
    # note: Flatten only literal list operands from the pinned token assignment; model code is never evaluated.
    processor_tree = ast.parse((ARCHIVE / by_name["processing_florence2.py"]["master"]).read_text(encoding="utf-8"))
    assignment = next(n for n in ast.walk(processor_tree)
                      if isinstance(n, ast.Assign) and any(isinstance(t, ast.Name) and t.id == "tokens_to_add" for t in n.targets))
    expression = assignment.value.values[0]

    def operands(node):
        if isinstance(node, ast.BinOp) and isinstance(node.op, ast.Add):
            return operands(node.left) + operands(node.right)
        return [node]

    parts = operands(expression)
    assert len(parts) == 4
    assert isinstance(parts[0], ast.Attribute) and parts[0].attr == "additional_special_tokens"
    assert ast.literal_eval(parts[1]) == ["<od>", "</od>", "<ocr>", "</ocr>"]
    loc = parts[2]
    assert isinstance(loc, ast.ListComp) and ast.unparse(loc) == "[f'<loc_{x}>' for x in range(1000)]"
    tail = ast.literal_eval(parts[3])
    assert len(tail) == 20 and tail[-1] == "<and>"
    additions = ast.literal_eval(parts[1]) + [f"<loc_{x}>" for x in range(1000)] + tail
    assert len(additions) == len(set(additions)) == 1024
    assert not set(additions) & set(vocabulary)
    expected_ids = {token: len(vocabulary) + index for index, token in enumerate(additions)}
    assert expected_ids["<od>"] == 50265 and expected_ids["<ocr>"] == 50267
    assert expected_ids["<loc_0>"] == 50269 and expected_ids["<loc_999>"] == 51268
    assert expected_ids["<and>"] == 51288
    config = read(ARCHIVE / by_name["config.json"]["master"])
    assert config["vocab_size"] == config["text_config"]["vocab_size"] == 51289
    assert config["auto_map"]["AutoModelForCausalLM"] == "modeling_florence2.Florence2ForConditionalGeneration"
    preprocessor = read(ARCHIVE / by_name["preprocessor_config.json"]["master"])
    assert preprocessor["auto_map"]["AutoProcessor"] == "processing_florence2.Florence2Processor"
    catalog = ROOT / fixtures["catalog_path"]
    observed_catalog = identity(catalog)
    assert observed_catalog["sha256"] == fixtures["catalog_sha256"]
    identities.append(observed_catalog)
    for row in fixtures["assets"]:
        path = FIXTURES.parent / row["path"]
        observed = identity(path)
        assert observed["bytes"] == row["bytes"] and observed["sha256"] == row["sha256"]
    for path in (ROOT / "Tools/PlayableEvaluation/native_vision_tests/runner.py", ROOT / "Tools/PlayableEvaluation/storage.py"):
        ast.parse(path.read_text(encoding="utf-8"))
        identities.append(identity(path))
    manifest_path = ROOT / "outputs/PlayableEvaluation_20261009/staging-manifest.json"
    manifest = read(manifest_path)
    staging = {"snapshot_state": manifest.get("state"), "snapshot_unit": manifest.get("unit"),
               "exact_ready_nine_file_unit": manifest.get("state") == "ready" and manifest.get("unit") == units["unit"] and manifest.get("files") == [{k: row[k] for k in ("master", "staged", "bytes", "sha256")} for row in units["files"]],
               "staged_files_read_or_mutated": False,
               "limit": "Snapshot only; execution runner must recheck staging state and all actual staged hashes."}
    identities.append(identity(manifest_path))
    result = {"status": "PURE_STATIC_CHECKS_PASS", "evidence": "STDLIB_AST_JSON_AND_ARCHIVE_HASHES_ONLY_NO_MODEL_IMPORT_OR_EXECUTION",
              "identity": identities, "checks": {"original_six_rows_unchanged": True, "all_nine_master_sizes_hashes_match": True,
              "three_pinned_sources_match_and_parse": True, "base_vocab_equals_tokenizer_vocab": True, "base_vocab_size": 50265,
              "unique_new_special_tokens": 1024, "predicted_final_vocab": 51289, "configured_final_vocab": 51289,
              "all_declared_synthetic_assets_match": True, "catalog_matches_fixture_guard": True},
              "predicted_all_added_token_ids": expected_ids, "staging_snapshot": staging,
              "unverified": ["Actual tokenizer special-token additions under4.57.6", "Heavy dependency imports/API compatibility/model loading/inference",
                             "Exact staged bytes at execution time", "Native caption/OCR/detection/segmentation semantic quality"]}
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "static_checks.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps({"status": result["status"], "master_files": 9, "model_imported": False,
                      "predicted_vocab": 51289, "staging_snapshot_ready": staging["exact_ready_nine_file_unit"]}))


if __name__ == "__main__":
    main()
