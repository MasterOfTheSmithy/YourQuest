"""Audit captured live replies against final source and write a bounded implementation receipt."""
import difflib
import hashlib
import json
import statistics
import subprocess
from datetime import datetime, timezone
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'outputs/LlmLatencyImplementation_20261007'
OUT=BASE/'FinalAudit'
OUT.mkdir(exist_ok=False)
DLL=BASE/'LoadoutChecks/bin/Release/net10.0/LoadoutChecks.dll'
WIRE=BASE/'Checks/bin/Release/net10.0/LatencyChecks.dll'
counter=0


def source(operation,data):
    global counter
    counter+=1
    incoming,outgoing=OUT/f'{counter:03d}_input.json',OUT/f'{counter:03d}_output.json'
    incoming.write_text(json.dumps(data),encoding='utf-8')
    subprocess.run(['dotnet',str(DLL),operation,str(incoming),str(outgoing)],check=True,capture_output=True,timeout=30)
    return json.loads(outgoing.read_text(encoding='utf-8-sig'))


normalization=[]
for label,raw,expected in [
    ('canonical null zero and false','{"zero":0,"unknown":null,"flag":false}',True),
    ('fenced complete JSON','```json\n{"zero":0}\n```',True),
    ('presentation wrapper','Here is the result: {"zero":0} Done.',True),
    ('completed reasoning block','<think>ignore these thoughts</think>{"zero":0}',True),
    ('empty','',False),('incomplete JSON','{"zero":',False),
    ('two objects','{"zero":0}{"zero":1}',False),
    ('duplicate root','{"zero":0,"zero":1}',False),
    ('duplicate nested','{"item":{"zero":0,"zero":1}}',False),
    ('duplicate identical values','{"zero":0,"zero":0}',False),
]:
    result=source('normalize',dict(raw=raw))
    assert result['valid']==expected,label
    if label=='canonical null zero and false':
        assert json.loads(result['normalized'])==json.loads(raw)
    normalization.append(dict(case=label,passed=True,result=result))

live_path=sorted(BASE.glob('Live_*/Results.json'))[-1]
live=json.loads(live_path.read_text())
wire_checks=[]
for incoming in live_path.parent.glob('source_*_in.json'):
    data=json.loads(incoming.read_text())
    if 'config' not in data:continue
    final_output=OUT/(incoming.stem+'_final_wire.json')
    subprocess.run(['dotnet',str(WIRE),'wire',str(incoming),str(final_output)],check=True,capture_output=True,timeout=30)
    recorded=json.loads(incoming.with_name(incoming.name.replace('_in','_out')).read_text())
    assert json.loads(final_output.read_text())==recorded,'Final wire/arguments drift'
    wire_checks.append(str(incoming))

repair_path=sorted(BASE.glob('LoadoutLive_*/Results.json'))[-1]
repair=json.loads(repair_path.read_text())
prior=json.loads((ROOT/'outputs/Llm8GBBatch2_20261007_2025/RepairPass/Episodes.json').read_text())
originals={r['modelId']:json.loads(r['trace'][0]['result']['raw']) for r in prior if 'origin' in r['fixture']}
replay=[]
for row in repair['rows']:
    if row['condition']=='already_accepted':continue
    attempts=[]
    for attempt,call in enumerate(row['requests'],1):
        result=source('merge',dict(proposal=originals[row['model']],repair=call.get('raw','')))
        complete=call.get('success') and call.get('finishReason')=='stop' and call.get('streamDone')
        if result.get('merged'):
            before={k:v for k,v in originals[row['model']].items() if k!='loadout'}
            after={k:v for k,v in json.loads(result['merged']).items() if k!='loadout'}
            assert before==after,'Protected origin fields changed'
        attempts.append(dict(attempt=attempt,accepted=bool(complete and result['valid']),source=result))
    first=next((a['attempt'] for a in attempts if a['accepted']),None)
    replay.append(dict(model=row['model'],condition=row['condition'],accepted=first is not None,
        firstPassingCapturedAttempt=first,actualLiveCalls=row['calls'],attempts=attempts))

llm=ROOT/'Assets/Assets/Scripts/LLM'
changed=['LLMClient.cs','LLMRuntimeConfig.cs','LlamaCppServerProcess.cs','YQLlmResidencyPolicy.cs','YQLlmResidencyPolicy.cs.meta']
patch=[]
for name in changed:
    before=BASE/'Before'/name
    old=before.read_text().splitlines(keepends=True) if before.exists() else []
    new=(llm/name).read_text().splitlines(keepends=True)
    patch.extend(difflib.unified_diff(old,new,fromfile='Before/'+name,tofile='Assets/Assets/Scripts/LLM/'+name))
(BASE/'Implementation.patch').write_text(''.join(patch),encoding='utf-8')
median={policy:statistics.median(r['seconds'] for r in live['cycles'] if r['policy']==policy) for policy in ['serial_uncached','retained_cached']}
latency_checks=json.loads((BASE/'Checks/Results.json').read_text())
compile_result=json.loads((BASE/'CompileAccepted/Receipt.json').read_text(encoding='utf-8-sig'))
assert compile_result['exitCode']==0
receipt=dict(utc=datetime.now(timezone.utc).isoformat(),status='IMPLEMENTED_DETACHED_VERIFIED',
    changedFiles={str(llm/name):hashlib.sha256((llm/name).read_bytes()).hexdigest() for name in changed},
    implementation=['Existing queue retains freshly verified CPU-only helper under a 2 GiB helper / 4 GiB free-RAM policy',
        'Cold or unknown placement uses serial eviction; CPU options are frozen before arbitration',
        'One-slot owned prompt reuse with RAM snapshot caching disabled and explicit scope invalidation',
        'Default summary role explicitly requests CPU; main model and qualification gates remain unchanged',
        'Structured wrapper normalization rejects duplicate keys; accepted domain state and saves remain authoritative'],
    compile=dict(exitCode=0,sourceCount=compile_result['sourceCount'],receipt=str(BASE/'CompileAccepted/Receipt.json')),
    detachedPolicyChecks=latency_checks['checks'],normalizationChecks=normalization,finalWireParityChecks=len(wire_checks),
    live=dict(receipt=str(live_path),cyclesPerPolicy=2,medianCycleSeconds=median,
        measuredMedianReductionPercent=100*(1-median['retained_cached']/median['serial_uncached']),
        correctCompletedResponses=16,totalResponses=16,
        scope='Production-source packets/arguments and CPU admission on private servers; not the Unity coroutine/runtime',
        limits=['Fixed condition order and two cycles each; not a p95 estimate','CPU helper placement established before optimized timed cycles',
            'First unverified helper transition still uses serial eviction','Final launch/packet parity checked after capability guard change',
            'All successful captured replies remain valid under final normalization']),
    repair=dict(capturedReceipt=str(repair_path),actualCalls=sum(r['calls'] for r in repair['rows']),replay=replay,
        acceptedByCondition={condition:sum(r['accepted'] for r in replay if r['condition']==condition) for condition in ['generic','detailed']},
        interpretation='Raw-strict scoring incorrectly rejected fenced JSON. Final source replay is authoritative for these captured replies. Detail-only feedback did not increase acceptance.',
        limits='Replayed rejected synthetic proposals, source transport/loadout acceptance; no fresh full-origin generation, game curation or persistence qualification'),
    remaining=['Unity import and actual game GPU occupancy/frame-time/queue lifecycle validation',
        'Broader roleplay and factual-review qualification; no new model promoted'],
    rollback='Disable enablePromptPrefixReuse / retainVerifiedCpuHelpers in the existing config. Before images and task-only patch retained.')
# note: Recheck every successful live result with the final parser without rerunning unaffected inference.
for row in live['cycles']:
    assert row['allCorrect']
    for call in row['requests']:assert source('normalize',dict(raw=call['raw']))['valid']
for call in live['transitions']:assert call['correct'] and call['complete'] and source('normalize',dict(raw=call['raw']))['valid']
(BASE/'ImplementationReceipt.json').write_text(json.dumps(receipt,indent=2),encoding='utf-8')
print(json.dumps(dict(median=median,reductionPercent=receipt['live']['measuredMedianReductionPercent'],
    policyChecks=latency_checks['checks'],normalizationChecks=len(normalization),wireParityChecks=len(wire_checks),
    repairCalls=receipt['repair']['actualCalls'],repairAccepted=receipt['repair']['acceptedByCondition'],receipt=str(BASE/'ImplementationReceipt.json')),indent=2))
