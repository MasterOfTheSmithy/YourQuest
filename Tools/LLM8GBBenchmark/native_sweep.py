"""Sequential specialist qualification; each child owns one model and all its applicable cases."""
import argparse
import json
import os
import shutil
import subprocess
import sys
import time
import traceback
from pathlib import Path
import psutil
import run as bench
from coexistence import BASE, ready, read
from staged_model import staged_model

SUPPORT = BASE / 'NativeSupport'
ROUTES = ['NPC conversation', 'Goddess dialogue', 'item equipment and consumable creation',
          'skill and spell creation', 'title and class creation', 'world and player lore',
          'quest creation', 'accepted state JSON extraction']
ROUTE_CASES = [
    ('Answer as Vey the ferrymender, speaking directly to Mira about the missing ledger.', 0),
    ('Write the goddess response to the player asking whether the rescued miner recovered. Preserve uncertainty.', 1),
    ('Propose a leather shield and a small river-travel ration with concrete containers and uses.', 2),
    ('Design a defensive spell with targeting, resource cost and cooldown.', 3),
    ('Offer a new title and character class inspired by the recorded rescue.', 4),
    ('Write local river folklore and a factual biography from the accepted player history.', 5),
    ('Create an offered quest to find the lost ferry ledger, using explicit objective records.', 6),
    ('Convert the accepted state into JSON: player Mira, gold 0, ledger missing. Do not invent anything.', 7),
    ('The inventory screen needs one new pair of protective boots and their material description.', 2),
    ('A shrine visitor asks the deity for reassurance after rescuing a miner; write her spoken reply.', 1),
    ('Extract only the current faction and completed objective count from this saved record.', 7),
    ('An innkeeper needs a believable reply when asked about an unconfirmed rumor.', 0),
]
STATE = 'Mira carried a miner to the chapel. Vey heard this from a courier. The miner\'s recovery is unknown. Mira has 0 gold. The ferry ledger is missing. No quest is complete. North crossing safety is unknown. The world plan describes seasonal floods, not current flooding.'
DECISIONS = [
    ('Choose the response supported by the state.', ['The miner recovered.', 'The miner was carried to the chapel; recovery is unknown.', 'Vey witnessed the rescue.'], 1),
    ('Choose the valid quest state.', ['Complete and pay the reward.', 'Offer a search for the missing ledger.', 'Record that the ledger was found.'], 1),
    ('Choose the valid purchase action for an item costing 5 gold.', ['Deduct 5 gold and grant the item.', 'Reject purchase for insufficient gold.', 'Invent a discount to zero gold.'], 1),
    ('Choose a grounded observation about the north crossing.', ['Safe now.', 'Flooded now.', 'Current safety is unknown.'], 2),
    ('Choose the supported memory for Vey.', ['I saw Mira rescue the miner.', 'A courier told me about Mira carrying a miner to the chapel.', 'Mira paid me for the rescue.'], 1),
    ('Choose the safe next step after generated JSON fails its required schema.', ['Persist the malformed response.', 'Validate and attempt a bounded repair before acceptance.', 'Skip required fields to pass.'], 1),
    ('Select the generation lane: create a new spell with cost and cooldown.', ['NPC dialogue', 'Skill and spell creation', 'World terrain rendering'], 1),
    ('Select the generation lane: extract accepted inventory facts into JSON.', ['State reconstruction', 'Inventing character history', 'Goddess voice'], 0),
    ('The quest requires 3 recovered pages; only 2 are recorded. Choose status.', ['Complete', 'In progress', 'Failed automatically'], 1),
    ('Proposed reply says a flood is happening now because the world plan includes seasonal flooding. Choose verdict.', ['Accept as observed fact.', 'Reject the unsupported current condition.', 'Mark the quest complete.'], 1),
]


def save(path, value):
    bench.save(path, value)


def child(args):
    # note: Enforce offline loading and a bounded CPU allocation; every model unloads when this process exits.
    os.environ.update(HF_HUB_OFFLINE='1', TRANSFORMERS_OFFLINE='1', TOKENIZERS_PARALLELISM='false', OMP_NUM_THREADS='4')
    out = Path(args.out); bench.OUT = out
    model_info = read(out/'Model.json'); path = Path(args.path)
    rows = []; start = time.perf_counter(); before = ready(); active_device='cpu'
    def record(case, fn, expected=None, evaluator=None):
        baseline = ready(); tick = time.perf_counter()
        try:
            result = fn(); seconds = time.perf_counter()-tick
            passed = evaluator(result) if evaluator else (result == expected if expected is not None else None)
            row = dict(case=case, seconds=seconds, result=result, expected=expected, exactPass=passed, status='TESTED')
        except Exception as exc:
            row = dict(case=case, seconds=time.perf_counter()-tick, status='CASE_RUNTIME_BLOCKED', error=str(exc))
        row.update(device=active_device,unity=ready(baseline['renderedFrames']), rssBytes=psutil.Process().memory_info().rss,
                   ramAvailable=psutil.virtual_memory().available, gpu=bench.gpu())
        rows.append(row); save(out/'Cases.json',rows)
        print(json.dumps(dict(case=case,status=row['status'],seconds=row['seconds'],exactPass=row.get('exactPass'))),flush=True)
    if args.kind == 'apus':
        sys.path.insert(0,str(SUPPORT/'apus'))
        from openjev_local import decide
        model_info['localPath'] = str(path/'current-model.gguf')
        for active_device in ('gpu','cpu'):
            # note: An orchestrator must also be tested without occupying the gameplay/voice GPU; reuse the same staged file.
            with bench.Server(model_info,Path('E:/GPT LLM TEST/Runtime/b10828-cuda13.3/llama-server.exe'),
                              cpu=active_device=='cpu',context=9216,headroom=1536,thinking='off',cache=True,label='openjev_'+active_device) as server:
                save(out/('Load_'+active_device+'.json'),dict(seconds=server.load_seconds,gpu=server.loaded,pid=server.process.pid))
                for i,(instruction,choices,gold) in enumerate(DECISIONS):
                    for reverse in (False,True):
                        criteria=[dict(id=str(n),description=t) for n,t in enumerate(choices)]
                        if reverse:criteria.reverse()
                        task=dict(id=f'game-{i}',group_id=f'game-{i}',primitive='choice',state=STATE,instructions=instruction,criteria=criteria)
                        record(f'decision_{active_device}_{i}_reverse_{reverse}',lambda task=task:decide(task,'llama-server',server.base),
                               expected=str(gold),evaluator=lambda r,gold=gold:r.get('choice')==str(gold))
    else:
        import torch
        torch.set_num_threads(4);torch.set_num_interop_threads(2)
        from transformers import AutoModel, AutoModelForCausalLM, AutoTokenizer
        load_start = time.perf_counter()
        if args.kind == 'gliclass':
            from gliclass import GLiClassModel, ZeroShotClassificationPipeline
            model = GLiClassModel.from_pretrained(str(path),local_files_only=True).eval()
            tokenizer = AutoTokenizer.from_pretrained(str(path),local_files_only=True)
            pipeline = ZeroShotClassificationPipeline(model,tokenizer,classification_type='multi-label',device='cpu')
        elif args.kind == 'gliner':
            from gliner2 import GLiNER2
            model = GLiNER2.from_pretrained(str(path),local_files_only=True).eval()
        elif args.kind == 'router':
            # note: This pinned custom module was read and inspected; it only patches the local LFM2 forward math.
            model = AutoModel.from_pretrained(str(path),trust_remote_code=True,local_files_only=True).eval()
            tokenizer = AutoTokenizer.from_pretrained(str(path),local_files_only=True)
        elif args.kind == 'pleias':
            tokenizer=AutoTokenizer.from_pretrained(str(path),local_files_only=True)
            model=AutoModelForCausalLM.from_pretrained(str(path),local_files_only=True,dtype=torch.float32).eval()
        save(out/'Load.json',dict(seconds=time.perf_counter()-load_start,device='cpu',rssBytes=psutil.Process().memory_info().rss,gpu=bench.gpu()))
        if args.kind in ('gliclass','router','gliner'):
            for i,(text,gold) in enumerate(ROUTE_CASES):
                def classify(text=text):
                    with torch.inference_mode():
                        if args.kind=='router': return model.route(text,ROUTES,tokenizer=tokenizer)
                        if args.kind=='gliclass': return pipeline(text,ROUTES,threshold=0)[0]
                        return model.classify_text(text,{'route':ROUTES})
                def route_pass(result,gold=gold):
                    if isinstance(result,list):
                        winner=max(result,key=lambda x:x['score']);return winner.get('route',winner.get('label'))==ROUTES[gold]
                    value=result.get('route',result.get('classification',{}).get('route'))
                    return value==ROUTES[gold] or (isinstance(value,list) and value==[ROUTES[gold]])
                record(f'route_{i}',classify,expected=ROUTES[gold],evaluator=route_pass)
        if args.kind=='gliner':
            entity_cases=[
                ('Mira met Vey in Alderfen.', ['person','location'], {'person':['Mira','Vey'],'location':['Alderfen']}),
                ('Mira carried a miner to the chapel. His recovery is unknown.', ['person','destination'], {'person':['Mira'],'destination':['chapel']}),
                ('The inventory contains a leather shield and two oatcakes.', ['equipment','consumable'], {'equipment':['leather shield'],'consumable':['oatcakes']}),
                ('The offered quest is Find the Ledger. It is not complete.', ['quest title','quest status'], {'quest title':['Find the Ledger'],'quest status':['offered']}),
            ]
            for i,(text,labels,expected) in enumerate(entity_cases):
                record(f'entities_{i}',lambda text=text,labels=labels:model.extract_entities(text,labels),expected,
                       lambda r,expected=expected:r.get('entities')==expected)
            for i,text in enumerate(['Mira has 0 gold and carries a leather shield.','Vey has 12 gold and carries a reed basket.']):
                record(f'structured_{i}',lambda text=text:model.extract_json(text,{'inventory':['owner::str::The named owner','gold::str::Gold count','item::str::The carried item']}))
            for i,text in enumerate(['Mira trusts Vey. Vey distrusts Orin.','Mira carried the miner to the chapel. Vey heard the rescue report from a courier.']):
                record(f'relations_{i}',lambda text=text:model.extract_relations(text,['trusts','distrusts','carried','heard_from']))
        if args.kind=='pleias':
            rag_cases=[
                ('What is known about the miner\'s recovery?', [STATE]),
                ('Where did Mira take the miner, and how did Vey learn about it?', [STATE]),
                ('Is the north crossing flooded right now?', ['The world plan includes seasonal flooding near river crossings.','Current observations: no crossing inspection or weather report is recorded.']),
                ('What is Mira\'s current gold? Use the current record over the old record.', ['Old record revision 1: Mira has 12 gold.','Current record revision 2: Mira has 0 gold.']),
                ('Return a JSON object with playerName, gold, and minerRecovered (null if unknown), using only accepted facts.', [STATE]),
            ]
            for i,(query,sources) in enumerate(rag_cases):
                def generate(query=query,sources=sources):
                    # note: Exact publisher prompt tokens; arbitrary chat templates would invalidate this specialist test.
                    prompt=f'<|query_start|>{query}<|query_end|>\n'
                    prompt+=''.join(f'<|source_start|><|source_id|>{n} {source}<|source_end|>\n' for n,source in enumerate(sources,1))
                    prompt+='<|language_start|>\n'
                    tokens=tokenizer(prompt,return_tensors='pt')
                    if tokens.input_ids.shape[1]+768>4096:raise ValueError('Native context budget exceeded')
                    with torch.inference_mode():
                        generated=model.generate(**tokens,max_new_tokens=768,max_time=180,do_sample=False,pad_token_id=tokenizer.eos_token_id)
                    return dict(raw=tokenizer.decode(generated[0,tokens.input_ids.shape[1]:],skip_special_tokens=False),
                                outputTokens=int(generated.shape[1]-tokens.input_ids.shape[1]))
                record(f'rag_{i}',generate)
    save(out/'Complete.json',dict(status='SUITE_COMPLETE',kind=args.kind,caseCount=len(rows),seconds=time.perf_counter()-start,
        unityBefore=before,unityAfter=ready(),evidence='Native specialist fixtures. Semantic review and production integration remain separate.'))


def driver(args):
    if args.after:
        # note: Queue dependency only; this process loads no model until the ordinary sweep has closed its final server.
        deadline=time.monotonic()+7200
        while not Path(args.after).exists():
            if time.monotonic()>deadline:raise RuntimeError('Ordinary sweep did not finish within the bounded queue wait')
            ready();time.sleep(15)
    out=BASE/('NativeSweep_'+time.strftime('%Y%m%d_%H%M%S'));out.mkdir();bench.OUT=out
    inventory=read(BASE/'Inventory.json'); stages=[]
    queue=[('apus','APUS-OpenJev'),('gliclass','gliclass-small'),('gliner','gliner2-base'),('router','LFM2.5-Encoder'),('pleias','Pleias-RAG')]
    for kind,prefix in queue:
        model=next(m for m in inventory if m['id'].startswith(prefix));ready()
        if any((p.info['name'] or '').lower()=='llama-server.exe' for p in psutil.process_iter(['name'])):raise RuntimeError('Another llama-server is still running')
        folder=out/kind;folder.mkdir();save(folder/'Model.json',model)
        stage=dict(model=model['id'],kind=kind,status='STAGING',baselineGpu=bench.gpu());stages.append(stage);save(out/'Staging.json',stages)
        copied=[];dirs=[];process=None
        try:
            filename='current-model.gguf' if kind=='apus' else 'model.safetensors'
            with staged_model(model,out/'SSDStage',stage,target_name=filename) as staged:
                stage_dir=Path(staged['localPath']).parent
                try:
                    if kind!='apus':
                        source=SUPPORT/'router' if kind=='router' else Path(model['localPath']).parent
                        for file in source.rglob('*'):
                            if not file.is_file() or file.suffix not in ('.json','.model','.py'):continue
                            if 'onnx' in file.parts:continue
                            relative=file.relative_to(source);destination=stage_dir/relative
                            if file.stat().st_size>25000000:raise ValueError('Unexpected large supporting file')
                            if destination.exists():raise ValueError('Supporting filename collision')
                            if not destination.parent.exists():destination.parent.mkdir(parents=True);dirs.append(destination.parent)
                            shutil.copyfile(file,destination);copied.append(destination)
                    log=(folder/'Worker.log').open('w',encoding='utf-8')
                    env=dict(os.environ,PYTHONIOENCODING='utf-8',HF_HUB_OFFLINE='1',TRANSFORMERS_OFFLINE='1')
                    process=subprocess.Popen([str(BASE/'NativeEnv/Scripts/python.exe'),str(Path(__file__).resolve()),'--child',kind,'--path',str(stage_dir),'--out',str(folder)],
                        stdout=log,stderr=subprocess.STDOUT,env=env,creationflags=subprocess.CREATE_NO_WINDOW|subprocess.BELOW_NORMAL_PRIORITY_CLASS)
                    try:code=process.wait(timeout=1200)
                    except subprocess.TimeoutExpired:
                        # note: Terminate only this worker and its owned descendants before deleting its temporary weights.
                        for p in psutil.Process(process.pid).children(recursive=True):p.kill()
                        process.kill();process.wait();raise RuntimeError('Native suite exceeded 20-minute budget')
                    finally:log.close()
                    stage.update(status='SUITE_COMPLETE' if code==0 and (folder/'Complete.json').exists() else 'RUNTIME_BLOCKED',exitCode=code)
                finally:
                    for file in copied:
                        if file.is_symlink() or stage_dir.resolve() not in file.resolve().parents:raise RuntimeError('Support cleanup path changed')
                        file.unlink()
                    for directory in sorted(set(dirs),key=lambda p:len(p.parts),reverse=True):directory.rmdir()
            stage.update(gpuAfterClose=bench.gpu(),unityAfter=ready())
        except Exception as exc:stage.update(status='RUNTIME_BLOCKED',error=str(exc))
        save(out/'Staging.json',stages);print(json.dumps(stage),flush=True)
    save(out/'Complete.json',dict(stages=stages,unity=ready()))


if __name__=='__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    parser=argparse.ArgumentParser();parser.add_argument('--child',dest='kind');parser.add_argument('--path');parser.add_argument('--out');parser.add_argument('--after');args=parser.parse_args()
    if args.kind:
        try:child(args)
        except Exception:
            traceback.print_exc();sys.exit(1)
    else:driver(args)
