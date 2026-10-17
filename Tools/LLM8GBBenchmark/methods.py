"""Measured residency, prefix caching and bounded choices; no game state or external server changes."""
import argparse
import copy
import hashlib
import json
import os
import shutil
import socket
import subprocess
import time
import urllib.request
from pathlib import Path

import psutil
import run as bench
from fixtures import build_cases


class OwnedOllama(bench.Server):
    def __init__(self, model, label='residency_owned_ollama'):
        super().__init__(model, Path(shutil.which('ollama')), cpu=True, port=11446,
                         label=label,thinking='off')

    def __enter__(self):
        # note: The private port and owned process isolate existing Ollama sessions; weights/manifests are read in place.
        with socket.socket() as check:
            check.bind(('127.0.0.1',self.port))
        self.baseline=bench.gpu()
        environment=os.environ.copy()
        environment.update(OLLAMA_HOST=f'127.0.0.1:{self.port}',OLLAMA_MODELS='D:/OllamaModels',
            OLLAMA_NUM_PARALLEL='1',OLLAMA_MAX_LOADED_MODELS='1',OLLAMA_NOPRUNE='true')
        self.log_path=bench.OUT/(self.label+'.log')
        self.log=self.log_path.open('w',encoding='utf-8')
        started=time.monotonic()
        self.process=subprocess.Popen([str(self.engine),'serve'],env=environment,
            stdout=self.log,stderr=subprocess.STDOUT,creationflags=bench.FLAGS)
        bench.event('owned-ollama-start',pid=self.process.pid,host=self.base,modelsDirectory=environment['OLLAMA_MODELS'])
        try:
            while time.monotonic()-started < 60:
                if self.process.poll() is not None:
                    raise RuntimeError('Private Ollama service exited')
                try:
                    version=bench.request(self.base,'/api/version',timeout=2)
                    self.version=version
                    bench.event('owned-ollama-ready',version=version,seconds=time.monotonic()-started)
                    return self
                except Exception as error:
                    if '10013' in str(error):raise
                    time.sleep(.2)
            raise TimeoutError('Private Ollama readiness exceeded budget')
        except BaseException:
            self.close();raise

    def placement(self):
        return dict(num_gpu=0,num_ctx=8192,num_thread=4,num_batch=128)

    def preload(self,label):
        started=time.monotonic()
        reply=bench.request(self.base,'/api/generate',dict(model=self.model['alias'],prompt='',
            keep_alive=-1,stream=False,options=self.placement()),timeout=120)
        resident=bench.request(self.base,'/api/ps')
        models=resident.get('models',[])
        owned=[m for m in models if m.get('name')==self.model['alias'] or m.get('model')==self.model['alias']]
        if len(owned)!=1 or owned[0].get('size_vram')!=0:
            raise RuntimeError('CPU-only helper residency was not proven by /api/ps')
        bench.save(bench.OUT/(label+'_preload.json'),dict(reply=reply,resident=resident))
        bench.event('cpu-helper-preloaded',label=label,seconds=time.monotonic()-started,
                    sizeVramBytes=owned[0]['size_vram'],sizeBytes=owned[0].get('size'),gpu=bench.gpu())
        return time.monotonic()-started

    def release(self):
        started=time.monotonic()
        bench.request(self.base,'/api/generate',dict(model=self.model['alias'],keep_alive=0,stream=False),timeout=30)
        while time.monotonic()-started < 30:
            resident=bench.request(self.base,'/api/ps')
            if not resident.get('models'):
                bench.event('cpu-helper-released',seconds=time.monotonic()-started);return
            time.sleep(.1)
        raise TimeoutError('Private CPU helper unload was not acknowledged')

    def infer(self,case,label):
        payload=dict(model=self.model['alias'],messages=case['messages'],stream=True,keep_alive=-1,think=False,
            format=case['schema'],options=dict(self.placement(),num_predict=case['max_tokens'],
                temperature=case['temperature'],seed=42,top_k=20,top_p=.9,repeat_penalty=1.05))
        bench.save(bench.OUT/(label+'_request.json'),payload)
        wire=urllib.request.Request(self.base+'/api/chat',data=json.dumps(payload).encode('utf-8'),
            headers={'Content-Type':'application/json'})
        started=time.monotonic();chunks=[];content=[];first=None;final={}
        with urllib.request.urlopen(wire,timeout=90) as response:
            for line in response:
                if time.monotonic()-started > 180:raise TimeoutError('CPU helper response budget exceeded')
                chunk=json.loads(line);chunks.append(chunk)
                if 'error' in chunk:raise RuntimeError(chunk['error'])
                text=chunk.get('message',{}).get('content','');content.append(text)
                if text and first is None:first=time.monotonic()-started
                if chunk.get('done'):final=chunk
        raw=''.join(content)
        try:
            parsed=bench.strict_json(raw);semantic=parsed==case['expected'] if 'expected' in case else None
            bench.jsonschema.validate(parsed,case['schema']);schema_valid=True
        except Exception:semantic=False;schema_valid=False
        result=dict(modelId=self.model['id'],fixture=case['id'],backend='private_owned_ollama',cpu=True,
            seconds=time.monotonic()-started,firstVisibleSeconds=first,raw=raw,
            success=bool(final.get('done')) and final.get('done_reason')=='stop',semanticPass=semantic,
            schemaValid=schema_valid,completionMetadata=final,gpu=bench.gpu(),residency=bench.request(self.base,'/api/ps'))
        bench.save(bench.OUT/(label+'_reply.json'),dict(measurement=result,chunks=chunks))
        return result


def residency(models,engine,cases,cycles):
    main=models['qwen35-current'];helper=models['summary-current']
    gpu_case=next(c for c in cases if c['id']=='reconstruct_conflicting_prose')
    cpu_case=next(c for c in cases if c['id']=='summary_preserve_uncertainty')
    results=[]
    # note: Requests remain serial under both policies; residency alone is the independent variable.
    with OwnedOllama(helper) as cpu:
        for policy in ['serial_switch','retain_gpu_and_cpu']:
            owned=None
            try:
                owned=bench.Server(main,engine,label='residency_'+policy+'_setup').__enter__()
                if policy=='retain_gpu_and_cpu':cpu.preload('residency_'+policy+'_setup')
                for cycle in range(cycles):
                    started=time.monotonic()
                    first=bench.call(owned,gpu_case,cycle,greedy=True)
                    if policy=='serial_switch':
                        owned.close();owned=None
                        cpu.preload(f'residency_{policy}_{cycle}')
                    middle=cpu.infer(cpu_case,f'residency_{policy}_cpu_{cycle}')
                    if policy=='serial_switch':
                        cpu.release()
                        owned=bench.Server(main,engine,label=f'residency_{policy}_reload_{cycle}').__enter__()
                    final=bench.call(owned,gpu_case,cycle+cycles,greedy=True)
                    active=[owned.process.pid,cpu.process.pid]
                    processes=[]
                    for pid in active:
                        process=psutil.Process(pid);processes.append(process);processes+=process.children(recursive=True)
                    row=dict(policy=policy,cycle=cycle,cycleSeconds=time.monotonic()-started,
                        inferenceSeconds=first['seconds']+middle['seconds']+final['seconds'],
                        allSemanticPass=all(r.get('semanticPass') and r.get('success') for r in [first,middle,final]),
                        gpu=bench.gpu(),ownedCombinedRssBytes=sum(p.memory_info().rss for p in processes),
                        requests=[{k:r.get(k) for k in ['modelId','fixture','seconds','success','semanticPass','raw']} for r in [first,middle,final]])
                    results.append(row);bench.event('residency-cycle',**{k:v for k,v in row.items() if k!='requests'})
                cpu.release()
            finally:
                if owned is not None:owned.close()
    bench.save(bench.OUT/'ResidencyResults.json',dict(rows=results,
        evidence='Owned production engine main + private native Ollama CPU helper, serial requests, Unity closed. Model retention diagnostic; no game scheduler integration.'))


def cached(models,engine,cases,cycles):
    selected=['reconstruct_long_context','reconstruct_cross_profile','reconstruct_conflicting_prose']
    rows=[]
    long_case=next(c for c in cases if c['id']=='reconstruct_long_context')
    variants=[]
    for i,inquiry in enumerate(['Refresh this player record from the accepted facts.',
        'Please reconstruct the current Mira record, keeping zero currency and unknown box contents.',
        'Recreate the same requested state from current facts; older archives remain untrusted.']):
        variant=copy.deepcopy(long_case);variant['id']='prefix_changed_inquiry_'+str(i)
        variant['messages'][0]['content']+='\nLATEST_INQUIRY:\n'+inquiry
        variants.append(variant)
    bench.save(bench.OUT/'MethodCacheFixtures.json',variants)
    for enabled in [False,True]:
        with bench.Server(models['qwen35-current'],engine,cache=enabled,label='prefix_cache_'+str(enabled).lower()) as server:
            for case_id in selected:
                case=next(c for c in cases if c['id']==case_id)
                for sample in range(cycles):rows.append(bench.call(server,case,sample,greedy=True))
            for case in variants:rows.append(bench.call(server,case,0,greedy=True))
    bench.save(bench.OUT/'PrefixCacheResults.json',dict(rows=rows,evidence='Same-slot full-context prefix reuse, including a profile change; not idle snapshot cache or a Unity frame-time test.'))


def choice(models,engine):
    rows=[];route=[('a',2,4,False),('b',3,6,True),('c',2,8,True),('d',1,9,True)]
    cases=[]
    for rotation in range(4):
        criteria=route[rotation:]+route[:rotation]
        cases.append(dict(id=f'route_rotation_{rotation}',state='It is hour 14. Budget is 8 gold. Must arrive by hour 16 inclusive.',
            instructions='Choose the safe affordable route that meets the deadline.',
            criteria=[dict(id=r[0],description=f'Route {r[0]} takes {r[1]} hours, costs {r[2]} gold, safe={r[3]}.') for r in criteria],gold='c'))
    for good_first in [True,False]:
        pairs=[('grounded','A courier reports the miner reached the chapel; recovery is unknown.'),
               ('invented','The miner recovered completely and Vey witnessed the rescue.')]
        if not good_first:pairs.reverse()
        cases.append(dict(id='grounded_'+str(good_first),state='Vey did not see the rescue. A courier reported arrival at the chapel. No recovery report exists.',
            instructions='Select the statement supported by the supplied evidence.',
            criteria=[dict(id=k,description=v) for k,v in pairs],gold='grounded'))
    for good_first in [True,False]:
        pairs=[('current','Rook: 37 gold, alive=false, revision=31.'),('stale','Mira: 999 gold, alive=true, revision=12.')]
        if not good_first:pairs.reverse()
        cases.append(dict(id='profile_'+str(good_first),state='Current selected player is Rook, revision 31, 37 gold, hp=0, alive=false. Archived other profile is Mira at revision 12.',
            instructions='Choose the record that exactly preserves the current selected profile.',
            criteria=[dict(id=k,description=v) for k,v in pairs],gold='current'))
    # note: OpenJev is tested on its published one-letter choice contract, never on invented JSON/chat duties.
    with bench.Server(models['openjev-q4'],engine,cpu=True,label='openjev_native_cpu_choice',thinking='off') as server:
        for case in cases:
            labels='ABCDEFGHIJKLMNOP'[:len(case['criteria'])]
            task=dict(primitive='choice',instructions=case['instructions'],criteria=[dict(label=l,description=c['description']) for l,c in zip(labels,case['criteria'])])
            user='Shared state:\n'+case['state']+'\n\n'+json.dumps(task,ensure_ascii=False,sort_keys=True)+'\nReturn only the selected letter: '+', '.join(labels)+'.\nAnswer:'
            rendered=bench.request(server.base,'/apply-template',dict(messages=[dict(role='user',content=user)],chat_template_kwargs=server.template_kwargs))['prompt']
            payload=dict(prompt=rendered,n_predict=1,temperature=0,cache_prompt=False,n_probs=16,stream=False,seed=42)
            started=time.monotonic();reply=bench.request(server.base,'/completion',payload,timeout=90)
            letter=reply.get('content','').strip();mapping={l:c['id'] for l,c in zip(labels,case['criteria'])}
            row=dict(caseId=case['id'],seconds=time.monotonic()-started,selectedLabel=letter,selected=mapping.get(letter),gold=case['gold'],semanticPass=mapping.get(letter)==case['gold'],reply=reply,request=payload)
            rows.append(row);bench.event('choice-finished',**{k:v for k,v in row.items() if k not in ['reply','request']})
    bench.save(bench.OUT/'OpenJevChoiceResults.json',dict(cases=cases,rows=rows,
        contractSource='https://huggingface.co/apus-ailab/APUS-OpenJev-v1-4B-GGUF/blob/main/openjev_contracts.py',
        evidence='Eight detached bounded choices; label distribution calibration and early-exit native runtime are not tested. Fixed routing can use deterministic code.'))


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--out',required=True)
    parser.add_argument('--phase',choices=['residency','cache','choice'],required=True)
    parser.add_argument('--cycles',type=int,default=3);parser.add_argument('--production-engine',action='store_true')
    args=parser.parse_args();bench.OUT=Path(args.out).resolve()
    if bench.ROOT not in bench.OUT.parents:raise ValueError('Output must stay within workspace')
    manifest=json.loads(bench.MANIFEST.read_text(encoding='utf-8-sig'))
    models={m['id']:m for m in manifest['models']+manifest['controls']}
    engine=Path(manifest['runtime']['productionExecutable'] if args.production_engine else manifest['runtime']['destinationDirectory']+'/llama-server.exe')
    cases=build_cases(bench.ROOT)
    bench.save(bench.OUT/('method_source_'+args.phase+'.json'),{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in [Path(__file__),Path(bench.__file__),Path(__file__).with_name('fixtures.py')]})
    bench.event('method-phase-start',phase=args.phase,baseline=bench.gpu(),unityRunning=any(p.info['name']=='Unity.exe' for p in psutil.process_iter(['name'])))
    if args.phase=='residency':residency(models,engine,cases,args.cycles)
    elif args.phase=='cache':cached(models,engine,cases,args.cycles)
    else:choice(models,engine)
    bench.event('method-phase-finished',phase=args.phase,gpu=bench.gpu())


if __name__=='__main__':main()
