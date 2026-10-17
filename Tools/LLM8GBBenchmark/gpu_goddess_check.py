"""Five current-source Goddess probes with bounded source-validator repairs, entirely on GPU."""
import copy, re
from pathlib import Path
import psutil
import run as bench
from coexistence import read, ready, source_check, validate
from staged_model import staged_model
from schema_prompt import with_visible_schema
ROOT=Path(__file__).resolve().parents[2]; OUT=ROOT/'outputs/LlmGpuOnly_20261008/GoddessSpark'

def main():
    OUT.mkdir(exist_ok=True);bench.OUT=OUT
    ready()
    if any((p.info['name'] or '').lower()=='llama-server.exe' for p in psutil.process_iter(['name'])):raise RuntimeError('Another server is resident')
    model=next(m for m in read(ROOT/'outputs/LlmRequalification_20261008/Inventory.json') if m['id']=='Spark-X2.5-4B-Q4_K_M.gguf')
    cases=read(ROOT/'outputs/LlmRequalification_20261008/GoddessSourceHarness/CurrentSourceCases.json')['cases']
    stage={};rows=[]
    try:
        with staged_model(model,OUT/'SSDStage',stage) as staged:
            with bench.Server(staged,Path('E:/GPT LLM TEST/Runtime/b10828-cuda13.3/llama-server.exe'),cpu=False,context=4096,headroom=1536,cache=True,gpu_layers=999,thinking='off',reasoning_budget=0,kv_type='q8_0',verbosity=5,cache_ram_mib=256,checkpoints=2,checkpoint_min_step=256,label='spark_goddess') as server:
                matches=re.findall(r'offloaded (\d+)/(\d+) layers to GPU',server.log_path.read_text(encoding='utf-8',errors='replace'))
                if not matches or matches[-1][0]!=matches[-1][1]:raise RuntimeError('GPU placement unverified')
                stage['offloadedLayers']=matches[-1];stage['loadedGpu']=bench.gpu()
                if stage['loadedGpu']['freeMiB']<1536:raise RuntimeError('Headroom below approved floor')
                for fixture in cases:
                    before=ready();case=with_visible_schema(copy.deepcopy(fixture));case.update(max_tokens=400,temperature=.2)
                    # note: Existing fixture facts and schema stay authoritative; this only distinguishes the speaker from the player.
                    case['messages'].insert(0,dict(role='system',content='You speak as the Goddess identified by the supplied context. Address the player naturally, never as the player. Use known facts only. Do not convert an unknown into safety, danger, a deadline, an outcome, or a promise. Do not grant rewards or invent world events. Follow the requested JSON contract.'))
                    original=copy.deepcopy(case)
                    for attempt in range(3):
                        case['id']=fixture['id']+'_gpu_try'+str(attempt)
                        row=bench.call(server,case,sample=0,native_sampling=False,sampling_overrides=dict(temperature=.2,top_p=.8,top_k=20,presence_penalty=0,repeat_penalty=1))
                        check=source_check(fixture,row.get('raw',''),'gpu_spark_'+fixture['id']+'_'+str(attempt)) if row.get('success') else None
                        accepted,diagnostic=validate(fixture,row,check)
                        rows.append(dict(id=fixture['id'],attempt=attempt,measurement=row,sourceCheck=check,sourceAccepted=accepted,unity=ready(before['renderedFrames'])))
                        bench.save(OUT/'Cases.json',rows)
                        if bench.gpu()['freeMiB']<1536:raise RuntimeError('Headroom floor crossed')
                        if accepted:break
                        case=copy.deepcopy(original);case['messages'].append(dict(role='user',content='Repair this response using only the original supplied facts. Validation: '+diagnostic+'\nPrevious response:\n'+row.get('raw','')))
                stage['status']='COMPLETE'
    except Exception as error:stage.update(status='BLOCKED',error=str(error))
    bench.save(OUT/'Complete.json',dict(stage=stage,calls=len(rows),evidence='Detached source-envelope/prose validation with explicit GPU placement; independent factual and ordinary gameplay review remain separate'))
if __name__=='__main__':main()
