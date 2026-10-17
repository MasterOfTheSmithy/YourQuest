"""One bounded warm-reuse check after observed zero-hit cache comparisons."""
import json
import sys
import time
from pathlib import Path
import psutil
import run as bench
from coexistence import BASE, ready, read, source_check, validate
from runtime_controls import separate_speaker
from staged_model import staged_model


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    out=BASE/('CacheReuse_'+time.strftime('%Y%m%d_%H%M%S'));out.mkdir();bench.OUT=out
    model=next(m for m in read(BASE/'Inventory.json') if m['id']=='Qwen3.5-4B-Q4_K_M.gguf')
    cases=[c for c in read(BASE/'FullSweep_20261008_172243/Fixtures.json') if c['track'] in ('roleplay','goddess_current_source')]
    ready()
    if any((p.info['name'] or '').lower()=='llama-server.exe' for p in psutil.process_iter(['name'])):
        raise RuntimeError('Another model server is still running')
    stage=dict(model=model['id'],status='STAGING');rows=[]
    try:
        with staged_model(model,out/'SSDStage',stage) as staged:
            with bench.Server(staged,Path('E:/GPT LLM TEST/Runtime/b10828-cuda13.3/llama-server.exe'),
                              context=4096,headroom=2048,cache=True,thinking='off',kv_type='q8_0',gpu_layers=24,
                              cache_ram_mib=256,checkpoints=2,checkpoint_min_step=256,label='bounded_cache') as server:
                for index,case in enumerate(cases):
                    for repeat in (0,1):
                        before=ready();probe=separate_speaker(case)
                        probe['id']=case['id']+('_first' if repeat==0 else '_repeat')
                        row=bench.call(server,probe,sample=0,native_sampling=True,sampling_overrides=dict(temperature=.2,presence_penalty=0))
                        check=source_check(case,row.get('raw',''),out.name+'_'+str(index)+'_'+str(repeat)) if row.get('success') else None
                        accepted,diagnostic=validate(case,row,check)
                        rows.append(dict(model=model['id'],fixture=case['id'],repeat=repeat,measurement=row,
                                         sourceCheck=check,acceptedByDetachedChecks=accepted,diagnostic=diagnostic,unity=ready(before['renderedFrames'])))
                        bench.save(out/'Cases.json',rows)
        stage.update(status='COMPLETE',gpuAfter=bench.gpu(),unity=ready())
    except Exception as error:
        stage.update(status='RUNTIME_BLOCKED',error=str(error))
    finally:
        bench.save(out/'Staging.json',[stage])
        bench.save(out/'Complete.json',dict(stage=stage,cases=len(rows),evidence='Seven single-seed voice prompts, each immediately repeated once. Tests warm prompt reuse with bounded RAM and checkpoints; no production speech qualification.'))


if __name__=='__main__':
    main()
