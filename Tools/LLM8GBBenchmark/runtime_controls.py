"""Bounded controls for cache, sampling and thinking; each weight is staged only once."""
import copy
import json
import sys
import time
from pathlib import Path
import psutil
import run as bench
from coexistence import BASE, read, ready, validate, source_check
from staged_model import staged_model


def separate_speaker(case):
    # note: Preserve original facts and requirements; move speaker authority to the system role instead of mixing it with player data.
    probe=copy.deepcopy(case)
    if case['track']=='goddess_current_source':
        profile,task=probe['messages'][0]['content'].split('TASK CONTRACT',1)
        probe['messages']=[dict(role='system',content=profile+'\nYou are the Goddess. The player named in the context is the person you address, not your identity.'),
                           dict(role='user',content='TASK CONTRACT'+task)]
    elif case['id']=='dialogue_vey_conversation':
        profile,task=probe['messages'][0]['content'].split('Known facts:',1)
        probe['messages']=[dict(role='system',content=profile+' Speak directly as Vey in first person. Return only his words; no narration or player actions.'),
                           dict(role='user',content='Known facts:'+task)]
    elif case['id']=='dialogue_vey_uncertainty':
        content=probe['messages'][0]['content'];start=content.index('{"evidence"')
        probe['messages']=[dict(role='system',content=content[:start]+' You are Archivist Vey, addressing the player.'),dict(role='user',content=content[start:])]
    return probe


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    out=BASE/('RuntimeControls_'+time.strftime('%Y%m%d_%H%M%S'));out.mkdir();bench.OUT=out
    inventory=read(BASE/'Inventory.json')
    cases=read(BASE/'FullSweep_20261008_172243/Fixtures.json')
    cases=[c for c in cases if c['track'] in ('reconstruction','summary','reasoning','roleplay','goddess_current_source')]
    conditions=[('cached_native','off',True,None),('fresh_native','off',False,None),
                ('fresh_low_temperature','off',False,dict(temperature=.2,presence_penalty=0)),
                ('fresh_thinking512','on',False,None),
                ('fresh_system_role','off',False,dict(temperature=.2,presence_penalty=0)),
                ('fresh_system_role_compact','off',False,dict(temperature=.2,presence_penalty=0)),
                ('fresh_system_role_compact_mtp','off',False,dict(temperature=.2,presence_penalty=0))]
    all_rows=[];stages=[]
    for name in ('Qwen3.5-2B-Q5_K_M.gguf','Qwen3.5-4B-Q4_K_M.gguf','granite-4.2-3b-Q4_K_M.gguf','Spark-X2.5-4B-Q4_K_M.gguf'):
        model=next(m for m in inventory if m['id']==name);ready()
        if any((p.info['name'] or '').lower()=='llama-server.exe' for p in psutil.process_iter(['name'])):raise RuntimeError('Another model server is still running')
        stage=dict(model=name,status='STAGING');stages.append(stage)
        try:
            with staged_model(model,out/'SSDStage',stage) as staged:
                for condition,thinking,cache,sampling in conditions:
                    if condition.endswith('_mtp') and not name.startswith('Qwen3.5-4B'):continue
                    if name.startswith(('granite','Spark-')) and not condition.startswith('fresh_system_role'):continue
                    compact='_compact' in condition
                    layers=(24 if name.startswith('Qwen3.5-4B') else 30 if name.startswith('granite') else 27 if name.startswith('Spark-') else None) if compact else None
                    selected=[c for c in cases if c['track'] in ('roleplay','goddess_current_source')] if compact else cases
                    label=name+'_'+condition
                    # note: The compact deployment profile is a combined context/cache/reserve experiment, not an isolated-factor comparison.
                    with bench.Server(staged,Path('E:/GPT LLM TEST/Runtime/b10828-cuda13.3/llama-server.exe'),context=4096 if compact else 12288,
                                      headroom=2048 if compact else 1536,cache=cache,thinking=thinking,kv_type='q8_0' if compact else 'f16',
                                      gpu_layers=layers,mtp=condition.endswith('_mtp'),reasoning_budget=512 if thinking=='on' else -1,label=label) as server:
                        for index,case in enumerate(selected):
                            before=ready();probe=separate_speaker(case) if condition.startswith('fresh_system_role') else copy.deepcopy(case)
                            if thinking=='on':
                                # note: Do not contradict the explicit thinking condition with a historical prompt's disable marker.
                                for message in probe['messages']:
                                    message['content']=message['content'].removeprefix('/no_think\n')
                            save_id=probe['id'];probe['id']=condition+'_'+save_id
                            row=bench.call(server,probe,sample=0,native_sampling=True,output_extra=768 if thinking=='on' else 0,sampling_overrides=sampling)
                            check=source_check(case,row.get('raw',''),out.name+'_'+name+'_'+condition+'_'+str(index)) if row.get('success') else None
                            accepted,diagnostic=validate(case,row,check)
                            all_rows.append(dict(model=name,condition=condition,fixture=save_id,measurement=row,sourceCheck=check,
                                                 acceptedByDetachedChecks=accepted,diagnostic=diagnostic,unity=ready(before['renderedFrames'])))
                            bench.save(out/'Cases.json',all_rows)
            stage.update(status='CONTROLS_COMPLETE',gpuAfter=bench.gpu(),unity=ready())
        except Exception as error:
            stage.update(status='RUNTIME_BLOCKED',error=str(error));ready()
        finally:bench.save(out/'Staging.json',stages)
    bench.save(out/'Complete.json',dict(stages=stages,conditions=conditions,cases=len(all_rows),unity=ready(),
        evidence='Controlled single-seed fixture comparison; differing floating point paths can change sampled text. No claim of cache corruption from differences alone.'))


if __name__=='__main__':main()
