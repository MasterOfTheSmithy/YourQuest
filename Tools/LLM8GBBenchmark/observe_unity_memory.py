"""One bounded, model-free replay: capture world residency, stop Play, capture retention."""
import json
import argparse
import time
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/LlmRequalification_20261008'
REQUESTS = ROOT / 'Assets/Assets/EditorBuildRequests'
parser = argparse.ArgumentParser()
parser.add_argument('--owner-pid', type=int)
parser.add_argument('--report-directory', default='outputs/LlmRequalification_20261008')
parser.add_argument('--post-stop-seconds', type=int, default=20)
args = parser.parse_args()
# note: Keep before/after receipts separate and target only the explicitly selected Editor process.
RESULT = (ROOT / args.report_directory).resolve()
assert RESULT.is_relative_to(ROOT)
RESULT.mkdir(parents=True, exist_ok=True)
owner = args.owner_pid or json.loads((OUT / 'UnityMemoryReplayLaunch.json').read_text(encoding='utf-8-sig'))['pid']
started = time.monotonic()
materialized_at = None
stop_requested_at = None
captures = []
result = dict(ownerPid=owner, startedUtc=datetime.now(timezone.utc).isoformat(), status='observing', captures=captures)


def read(path):
    try:
        return json.loads(path.read_text(encoding='utf-8-sig'))
    except (FileNotFoundError, json.JSONDecodeError):
        return {}


def capture(reason):
    # note: This invokes the existing named read-only Editor census; no model, asset, scene or quality setting is changed.
    before = read(OUT / 'MemoryCensus/Current.json').get('utc')
    (REQUESTS / 'YQ_LLM_MEMORY_CENSUS.request').write_text('read-only memory reproduction', encoding='utf-8')
    deadline = time.monotonic() + 25
    while time.monotonic() < deadline:
        current = read(OUT / 'MemoryCensus/Current.json')
        if current.get('utc') != before and current.get('processId') == owner:
            captures.append(dict(reason=reason, utc=current['utc'], playing=current['playing'],
                                 allocatedBytes=current['allocatedBytes'], textureBytes=current['textureMemoryBytes'],
                                 driverBytes=current['graphicsDriverAllocatedBytes'], host=read(OUT / 'HostCurrent.json')))
            return True
        time.sleep(1)
    captures.append(dict(reason=reason, status='capture timed out'))
    return False


while time.monotonic() - started < 480:
    current = read(OUT / 'UnityBaseline/Current.json')
    host = read(OUT / 'HostCurrent.json')
    if current.get('processId') != owner:
        time.sleep(2)
        continue
    if current.get('materialized') and materialized_at is None:
        materialized_at = time.monotonic()
        capture('first materialization; readiness recorded independently')
    if not stop_requested_at and current.get('playMode'):
        pressure = host.get('gpu', {}).get('freeMiB', 8192) < 350 or host.get('ram', {}).get('availableBytes', 2**40) < 2 * 2**30
        settled = materialized_at is not None and time.monotonic() - materialized_at >= 120
        failed = current.get('status') == 'BLOCKED'
        if pressure or settled or failed:
            capture('world under memory pressure' if pressure else 'world after settling' if settled else 'blocked world')
            # note: Stop only this explicit assisted replay through the project-owned Play control; the Editor remains open for retained-memory observation.
            (REQUESTS / 'YQ_STREAMER_RUNTIME_STOP.request').write_text('end bounded memory reproduction', encoding='utf-8')
            stop_requested_at = time.monotonic()
    if stop_requested_at and not current.get('playMode') and time.monotonic() - stop_requested_at >= args.post_stop_seconds:
        capture('after Play stopped; Editor still open')
        result['status'] = 'captured'
        break
    if not stop_requested_at and current.get('status') == 'BLOCKED' and not current.get('playMode'):
        result['status'] = 'interrupted before world capture'
        break
    result['latestUnityState'] = current
    (RESULT / 'MemoryReproduction.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    time.sleep(2)
else:
    result['status'] = 'bounded observation expired; no qualification'
    # note: A timed-out owned replay must not leave Play running after the observer exits.
    current = read(OUT / 'UnityBaseline/Current.json')
    if current.get('processId') == owner and current.get('playMode'):
        (REQUESTS / 'YQ_STREAMER_RUNTIME_STOP.request').write_text('end expired memory reproduction', encoding='utf-8')
result['finishedUtc'] = datetime.now(timezone.utc).isoformat()
result['latestUnityState'] = read(OUT / 'UnityBaseline/Current.json')
result['evidence'] = 'Diagnostic assisted replay, not ordinary gameplay or an LLM benchmark.'
(RESULT / 'MemoryReproduction.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(dict(status=result['status'], captures=len(captures), ownerPid=owner)))
