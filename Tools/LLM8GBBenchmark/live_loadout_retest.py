"""Paired generic/detailed feedback on the same previously rejected proposals; at most two repairs each."""
import copy
import json
import subprocess
import sys
import time
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
PREVIOUS=ROOT/'outputs/Llm8GBBatch2_20261007_2025'
BASE=ROOT/'outputs/LlmLatencyImplementation_20261007'
sys.path.insert(0,str(PREVIOUS))
import run_local as bench

OUT=BASE/time.strftime('LoadoutLive_%Y%m%d_%H%M%S')
DLL=BASE/'LoadoutChecks/bin/Release/net10.0/LoadoutChecks.dll'
counter=0


def source(operation,data):
    global counter
    counter+=1
    incoming,outgoing=OUT/f'source_{counter:03d}_in.json',OUT/f'source_{counter:03d}_out.json'
    bench.save(incoming,data)
    result=subprocess.run(['dotnet',str(DLL),operation,str(incoming),str(outgoing)],capture_output=True,text=True,timeout=30,creationflags=bench.FLAGS)
    if result.returncode:raise RuntimeError(result.stderr or result.stdout)
    return json.loads(outgoing.read_text(encoding='utf-8-sig'))


def main():
    OUT.mkdir(exist_ok=False);bench.OUT=OUT
    manifest=json.loads((PREVIOUS/'BatchManifest.json').read_text())
    models={m['id']:m for m in manifest['models']+manifest['controls']}
    prior=[r for r in json.loads((PREVIOUS/'RepairPass/Episodes.json').read_text()) if 'origin' in r['fixture']]
    rows=[]
    for index,episode in enumerate(prior):
        key=episode['modelId'];model=models[key]
        original=bench.strict_json(episode['trace'][0]['result']['raw'])
        check=source('check',dict(proposal=original))
        if check['valid']:
            rows.append(dict(model=key,condition='already_accepted',accepted=True,calls=0));continue
        engine=PREVIOUS/'PrismRuntime/llama-server.exe' if key.startswith('bonsai') else Path('E:/GPT LLM TEST/Runtime/b10828-cuda13.3/llama-server.exe')
        thinking='on' if key.startswith('lfm2-5-2-6b') else 'off'
        with bench.Server(model,engine,context=12288,label='loadout_'+key,thinking=thinking,reasoning_budget=768) as server:
            conditions=['generic','detailed'] if index%2==0 else ['detailed','generic']
            for condition in conditions:
                proposal=copy.deepcopy(original);previous=episode['trace'][0]['result']['raw'];diagnostic=check['diagnostic']
                calls=[];accepted=False;start=time.perf_counter()
                for attempt in range(1,3):
                    feedback=diagnostic if condition=='detailed' else 'loadout needs at least three named, described items'
                    built=source('prompt',dict(proposal=proposal,originalPrompt=episode['originalMessages'][0]['content'],diagnostic=feedback,previous=previous))
                    case=dict(id=f'{key}_{condition}_{attempt}',track='current_loadout_feedback',
                        messages=[dict(role='user',content=built['prompt'])],response_format={'type':'json_object'},max_tokens=650,temperature=.48,
                        evidence='Replayed rejected synthetic origin; current production repair prompt/merge/validation; paired diagnostic only')
                    call=bench.call(server,case,sample=attempt,output_extra=1024 if thinking=='on' else 0,
                        sampling_overrides=dict(temperature=.48,top_p=.86,top_k=20,repeat_penalty=1.05,presence_penalty=0.))
                    previous=call.get('raw','');result=dict(valid=False,diagnostic='Incomplete response')
                    if call.get('success') and call.get('finishReason')=='stop' and call.get('streamDone'):
                        try:
                            result=source('merge',dict(proposal=original,repair=previous))
                            if result.get('merged'):
                                candidate=bench.strict_json(result['merged'])
                                if {k:v for k,v in original.items() if k!='loadout'}!={k:v for k,v in candidate.items() if k!='loadout'}:
                                    raise RuntimeError('Protected origin field changed')
                                # note: Check the original required transport shape as well as current raw DTO acceptance.
                                bench.jsonschema.validate(candidate,episode['schema']);proposal=candidate
                        except (ValueError,bench.jsonschema.ValidationError) as error:
                            result=dict(valid=False,diagnostic=str(error))
                    call['currentSourceValidation']=result;calls.append(call)
                    accepted=bool(result['valid']);diagnostic='' if accepted else result.get('diagnostic') or 'Invalid loadout'
                    if accepted:break
                row=dict(model=key,condition=condition,accepted=accepted,calls=len(calls),seconds=time.perf_counter()-start,requests=calls,
                    protectedFieldsUnchanged=True,finalDiagnostic=diagnostic)
                rows.append(row);bench.save(OUT/'Results.json',dict(rows=rows))
                bench.event('loadout-pair-result',model=key,condition=condition,accepted=accepted,calls=len(calls),seconds=round(row['seconds'],3))
    bench.save(OUT/'Results.json',dict(rows=rows,evidence='Paired live repair of replayed rejected proposals. Current source acceptance; no fresh initial generation or persistence/Unity qualification.',gpuAfterCleanup=bench.gpu()))


if __name__=='__main__':main()
