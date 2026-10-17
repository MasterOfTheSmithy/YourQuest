"""Run actual detached NPC callbacks against installed CPU models, including bounded repair."""
from pathlib import Path
import argparse, hashlib, json, os, socket, subprocess, time, urllib.request

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--author-model')
args = parser.parse_args()
OUT = args.output.resolve()
HARNESS = OUT / 'Harness/bin/Debug/net10.0/Harness.dll'
BASE = 'http://127.0.0.1:11442'
QUALIFIED = json.loads((ROOT / 'outputs/SpeechRecovery_20261008/NpcQualification/ModelSummary.json').read_text(encoding='utf-8'))
assert QUALIFIED['status'] == 'PASS', 'NPC review must qualify before the author/repair chain is assessed.'

# note: Fictional held-out conversations do not alter a profile, NPC memory, accepted plan or runtime settings.
FIXTURES = [
    dict(name='ferryman', persona='Name: Orrin. Role: ferryman. Temperament: blunt, weary and practical. Faction: independent boatworkers.',
        objective='Ask Orrin about the missing toll ledger.',
        snapshot=dict(observations=['Orrin is repairing his rowboat.', 'Orrin needs a dry rope and two helpers.', 'The west gate is closed.']),
        turns=[
            dict(name='specific need', question='What do you need to repair your boat?'),
            dict(name='unseen inspection', question='Have you inspected the north road?'),
            dict(name='known plus unknown', question='Is the west gate open, and when does the ferry leave?'),
        ]),
    dict(name='guard', persona='Name: Kesa. Role: gate guard. Temperament: stern, dry humor, takes her duty seriously. Faction: town watch.',
        objective='Ask Kesa how to enter the east gate.',
        snapshot=dict(observations=['Kesa is guarding the east gate.', 'A pass-token is required for visitors to enter.', 'The courier has not arrived.']),
        turns=[
            dict(name='malformed envelope repair', question='What must I show to enter?', injectRaw='{"npcText":'),
            dict(name='semantic contradiction repair', question='Has the courier arrived?', injectRaw='{"npcText":"Yes, the courier arrived and I let him through.","action":"none","confidence":0.9}'),
            dict(name='character disagreement', question='Your rules annoy me. Why should I respect your work?'),
        ]),
]
if args.author_model:
    for fixture in FIXTURES: fixture['authorModel'] = args.author_model

def wire(path, payload=None, timeout=300):
    body = None if payload is None else json.dumps(payload,ensure_ascii=False).encode('utf-8')
    req = urllib.request.Request(BASE+path,data=body,headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(req,timeout=timeout) as response: return json.load(response)

calls, turns, used = [], [], set()
service = chain = None
failure = None
started = time.monotonic()
try:
    with socket.socket() as probe:
        if probe.connect_ex(('127.0.0.1',11442)) == 0: raise RuntimeError('Private qualification port already owned.')
    timing = json.loads((ROOT/'outputs/LlmTiming_20261005/Cases.json').read_text(encoding='utf-8-sig'))
    env = dict(os.environ,OLLAMA_HOST='127.0.0.1:11442',OLLAMA_MODELS=timing['ollamaModelDirectory'],OLLAMA_NUM_PARALLEL='1',OLLAMA_MAX_LOADED_MODELS='1')
    service = subprocess.Popen([timing['ollamaExecutable'],'serve'],env=env,stdout=(OUT/'ModelServer.log').open('w'),stderr=subprocess.STDOUT,
        creationflags=subprocess.CREATE_NO_WINDOW|subprocess.BELOW_NORMAL_PRIORITY_CLASS)
    for _ in range(250):
        if service.poll() is not None: raise RuntimeError('Private model server exited before readiness.')
        try: tags = wire('/api/tags',timeout=2); break
        except OSError: time.sleep(.2)
    else: raise RuntimeError('Private model server did not become ready.')
    models = {item['name']:item['digest'] for item in tags['models']}
    (OUT/'Fixtures.json').write_text(json.dumps(FIXTURES,indent=2),encoding='utf-8')
    for fixture in FIXTURES:
        input_path = OUT/(fixture['name']+'.json')
        input_path.write_text(json.dumps(fixture),encoding='utf-8')
        chain = subprocess.Popen(['dotnet',str(HARNESS),'npc-chain',str(input_path)],cwd=ROOT,stdin=subprocess.PIPE,stdout=subprocess.PIPE,
            stderr=(OUT/(fixture['name']+'-adapter.log')).open('w'),text=True,encoding='utf-8',bufsize=1,creationflags=subprocess.CREATE_NO_WINDOW)
        for line in chain.stdout:
            packet = json.loads(line)
            if packet['kind'] == 'result':
                turns.append(dict(fixture=fixture['name'],**packet))
                (OUT/'Turns.json').write_text(json.dumps(turns,ensure_ascii=False,indent=2),encoding='utf-8')
                print(json.dumps({k:packet[k] for k in ['turn','generatedAndAccepted','fallback','generatorCalls','verifierCalls','line']}),flush=True)
                continue
            payload=packet['payload']; model=payload['model']
            assert model in models, 'Requested model is not installed: '+model
            assert payload['options']['num_gpu']==0
            if packet['review']:
                assert packet['reviewerHash']==QUALIFIED['reviewerHash'], 'NPC review configuration drifted after qualification.'
                assert models[model]==packet['requiredDigest']==QUALIFIED['digests'][model]
            call_started=time.monotonic()
            injected=packet['injectedRaw'] is not None
            if injected:
                result={}; raw=packet['injectedRaw']; thinking=''
            else:
                used.add(model)
                result=wire(packet['endpoint'],payload)
                answer=result.get('message',result)
                raw=answer.get('content',answer.get('response','')); thinking=answer.get('thinking','')
                assert all(item.get('size_vram',0)==0 for item in wire('/api/ps',timeout=10)['models'])
            calls.append(dict(fixture=fixture['name'],turn=packet['turn'],model=model,digest=models[model],review=packet['review'],
                injected=injected,raw=raw,thinking=thinking,seconds=round(time.monotonic()-call_started,3),payload=payload,
                endpoint=packet['endpoint'],requestKey=packet['requestKey'],parentKey=packet['parentKey'],outputTokens=result.get('eval_count')))
            (OUT/'Calls.json').write_text(json.dumps(calls,ensure_ascii=False,indent=2),encoding='utf-8')
            chain.stdin.write(json.dumps(dict(raw=raw),ensure_ascii=False)+'\n');chain.stdin.flush()
        chain.wait(timeout=10)
        if chain.returncode: raise RuntimeError('NPC callback adapter exited: '+str(chain.returncode))
        chain=None
except Exception as error:
    failure=repr(error)
finally:
    if chain is not None and chain.poll() is None:
        chain.terminate();chain.wait(timeout=10)
    if service is not None:
        try:
            for model in used: wire('/api/generate',dict(model=model,stream=False,keep_alive=0),timeout=30)
        finally:
            service.terminate()
            try: service.wait(timeout=10)
            except subprocess.TimeoutExpired: service.kill();service.wait(timeout=5)
    source_paths=['Assets/Assets/Scripts/Dialogue/DialogueThinkService.cs','Assets/Assets/Scripts/Dialogue/YQDialogueGrounding.cs',
        'Assets/Assets/Scripts/LLM/LLMRuntimeConfig.cs','Assets/Assets/Scripts/LLM/YQBoundedRepair.cs','Tools/GoddessSpeechVerification/NpcChainHarness.cs']
    expected=sum(len(f['turns']) for f in FIXTURES)
    summary=dict(utc=time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime()),
        status='AUTOMATED_CHAIN_PASS_REQUIRES_PROSE_REVIEW' if failure is None and len(turns)==expected and all(t['generatedAndAccepted'] for t in turns) else 'FAIL',
        evidence='Actual current NPC service callbacks and local CPU models with explicit detached scene/network adapters; no Unity UI, physical save or gameplay latency claim',
        authorOverride=args.author_model,turns=len(turns),expectedTurns=expected,accepted=sum(t['generatedAndAccepted'] for t in turns),physicalCalls=sum(not c['injected'] for c in calls),
        injectedFailureResponses=sum(c['injected'] for c in calls),seconds=round(time.monotonic()-started,3),failure=failure,
        sourceHashes={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in source_paths})
    (OUT/'Summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
    print(json.dumps(summary),flush=True)
if failure: raise RuntimeError(failure)
