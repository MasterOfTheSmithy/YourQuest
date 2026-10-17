"""Full-GPU retest: no automatic CPU or partial offload, no state publication."""
import argparse, copy, json, math, re, time
from pathlib import Path
import psutil
import run as bench
from coexistence import ready, read
from staged_model import staged_model
from schema_prompt import with_visible_schema
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'outputs/LlmGpuOnly_20261008'

def main():
    global OUT
    parser=argparse.ArgumentParser();parser.add_argument('--measure-fourb',action='store_true');parser.add_argument('--title-candidate',action='store_true');parser.add_argument('--control-only',action='store_true');parser.add_argument('--headroom',type=int,choices=[1536,2048],default=2048);args=parser.parse_args()
    inputs=OUT
    if args.control_only: OUT=OUT/'Control1536';OUT.mkdir(exist_ok=True)
    if args.measure_fourb: OUT=OUT/'FourBProbe'; OUT.mkdir(exist_ok=True)
    if args.title_candidate:
        inputs=ROOT/'outputs/LlmRoleIntegration_20261008/AdaptiveInputs';OUT=OUT/'TitleProbeRetry';OUT.mkdir(exist_ok=True)
    bench.OUT=OUT/'Inference'; bench.OUT.mkdir(exist_ok=True)
    routes=read(inputs/'Routes.json')
    if args.title_candidate: routes=[r for r in routes if r['role']=='TitleClass']
    inventory=read(ROOT/'outputs/LlmRequalification_20261008/Inventory.json')
    fixtures=read(ROOT/'outputs/LlmRequalification_20261008/FullSweep_20261008_172243/Fixtures.json')
    cases=read(inputs/'ProductionCases.json'); npc=read(inputs/'NpcCase.json')
    tracks=dict(Dialogue=['roleplay'],Summarization=['summary'],NpcPopulation=['npc_identity'],Equipment=['equipment'],SkillSpell=['skill_spell'],TitleClass=['title_class'],Consumable=['consumable'],Lore=['world_player_lore'])
    groups={}
    for route in routes:
        if route['role'] in tracks or route['role']=='QuestGeneration': groups.setdefault(route['model'],[]).append(route)
    rows=[]; stages=[]; replies=[]
    for ordinal,(path,roles) in enumerate(groups.items()):
        if args.measure_fourb and Path(path).name not in ('Spark-X2.5-4B-Q4_K_M.gguf','Qwen3.5-4B-Q4_K_M.gguf'): continue
        if args.control_only and Path(path).name!='Qwen3.5-4B-Q4_K_M.gguf': continue
        baseline=ready()
        if any((p.info['name'] or '').lower()=='llama-server.exe' for p in psutil.process_iter(['name'])): raise RuntimeError('Competing llama server; no second residency')
        model=next(m for m in inventory if m['localPath']==path); profile=roles[0]['profile']
        free=bench.gpu()['freeMiB']; needed=args.headroom+(0 if args.measure_fourb else 256)+math.ceil(model['bytes']/1024**2)
        stage=dict(model=model['id'],freeBeforeMiB=free,minimumAdmissionMiB=needed,placement='FULL_GPU',unity=baseline); stages.append(stage)
        # note: Probe mode measures workspace instead of assuming it; post-load reserve is unchanged and must pass before inference.
        if free < needed:
            stage.update(status='BLOCKED_HEADROOM',reason='Full GPU and configured reserve do not fit; no model loaded')
            bench.save(OUT/'Staging.json',stages); continue
        probes=[]
        for case in cases:
            if case['model']==path:
                wire=case['payload']; probes.append(dict(id=case['id'],track='production_progression',messages=wire['messages'],max_tokens=wire['max_tokens'],temperature=wire['temperature'],schema=wire.get('json_schema'),response_format=wire.get('response_format'),evidence=case['evidence'],production=True))
        if npc['model']==path:
            wire=npc['payload']; probes.append(dict(id='production_npc_batch',track='production_npc',messages=wire['messages'],max_tokens=wire['max_tokens'],temperature=wire['temperature'],schema=None,response_format=wire.get('response_format'),production=True,evidence='Actual production batch; detached parsing only'))
        for role in roles:
            for fixture in fixtures:
                if fixture['track'] in tracks.get(role['role'],[]):
                    probe=with_visible_schema(copy.deepcopy(fixture))
                    if role['profile']['systemPrompt']: probe['messages'].insert(0,dict(role='system',content=role['profile']['systemPrompt']))
                    probes.append(probe)
        sampling=dict(temperature=profile['temperature'],top_p=profile['topP'],top_k=profile['topK'],presence_penalty=profile['presencePenalty'],repeat_penalty=profile['repeatPenalty'])
        if profile['minP']>=0: sampling['min_p']=profile['minP']
        try:
            with staged_model(model,OUT/'SSDStage',stage) as staged:
                with bench.Server(staged,Path('E:/GPT LLM TEST/Runtime/b10828-cuda13.3/llama-server.exe'),context=roles[0]['context'],headroom=args.headroom,cache=True,cache_ram_mib=256,checkpoints=2,checkpoint_min_step=256,thinking='on' if profile['reasoningMode'] else 'off',reasoning_budget=profile['reasoningBudget'],kv_type='q8_0',gpu_layers=999,cpu=False,verbosity=5,label='fullgpu'+str(ordinal)) as server:
                    log=server.log_path.read_text(encoding='utf-8',errors='replace')
                    matches=re.findall(r'offloaded (\d+)/(\d+) layers to GPU',log)
                    if not matches or matches[-1][0]!=matches[-1][1]: raise RuntimeError('Full GPU layer placement not verified in server log')
                    stage['offloadedLayers']=matches[-1];stage['loadedGpu']=bench.gpu()
                    if stage['loadedGpu']['freeMiB']<args.headroom: raise RuntimeError('Post-load headroom below reserve; unload without inference')
                    for case in probes:
                        before=ready()
                        row=bench.call(server,case,sample=0,native_sampling=False,output_extra=0 if case.get('production') else max(0,profile['reasoningBudget']),sampling_overrides=sampling)
                        rows.append(dict(model=model['id'],id=case['id'],measurement=row,unity=ready(before['renderedFrames'])))
                        bench.save(OUT/'Cases.json',rows)
                        if bench.gpu()['freeMiB']<args.headroom: raise RuntimeError('Inference reserve crossed; ending model residency')
                        if case.get('production'):
                            if case['id']=='production_npc_batch': bench.save(OUT/'NpcReply.json',dict(raw=row.get('raw','')))
                            else:
                                replies.append(dict(id=case['id'],model=model['id'],raw=row.get('raw','')));bench.save(OUT/'ProductionReplies.json',replies)
            stage['status']='COMPLETE'
        except Exception as error: stage.update(status='BLOCKED',error=str(error))
        bench.save(OUT/'Staging.json',stages)
    bench.save(OUT/'Complete.json',dict(utc=bench.utc(),stages=stages,calls=len(rows),evidence='Full GPU only beside rendered Unity; no ordinary gameplay or semantic qualification implied'))

if __name__=='__main__': main()

