import json,subprocess,time,urllib.request,importlib.util
from pathlib import Path
ROOT=Path.cwd();OUT=ROOT/'outputs/LlmRepair_20261005';case=json.loads((OUT/'NpcCase.json').read_text(encoding='utf-8-sig'));data=json.loads((ROOT/'outputs/LlmTiming_20261005/Cases.json').read_text(encoding='utf-8-sig'));server=None
budget=json.loads((OUT/'GpuBudget.json').read_text(encoding='utf-8-sig'))
spec=importlib.util.spec_from_file_location('timing',ROOT/'Tools/measure_llm_response_times.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
base='http://127.0.0.1:11439';baseline=m.used_gpu();row={'selectedLayers':budget['layers'],'budgetFreeMb':budget['measuredOccupiedFreeMb'],'baselineMiB':baseline,'evidence':'GPU fitting alongside idle Unity, full current NPC payload; not gameplay frame-time proof'}
args=[data['llamaExecutable'],'--model',r'C:\Ai\Text Models\Qwen3.5-4B-Q4_K_M.gguf','--host','127.0.0.1','--port','11439','--ctx-size','12288','--parallel','1','--threads','4','--threads-batch','4','--batch-size','128','--ubatch-size','32','--prio','-1','--poll','0','--no-cache-prompt','--cache-ram','0','--n-gpu-layers',str(budget['layers']),'--flash-attn','on','--fit','on','--fit-target','768','--no-webui','--reasoning','off','--log-verbosity','3']
def req(path,body=None,timeout=240):
 b=None if body is None else json.dumps(body).encode();q=urllib.request.Request(base+path,data=b,headers={'Content-Type':'application/json'})
 with urllib.request.urlopen(q,timeout=timeout) as r:return json.load(r)
try:
 started=time.monotonic();server=subprocess.Popen(args,stdout=(OUT/'GpuServer.log').open('w'),stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW|subprocess.BELOW_NORMAL_PRIORITY_CLASS)
 for i in range(300):
  try:req('/health',timeout=2);break
  except Exception:
   if server.poll() is not None:raise RuntimeError('GPU server exited during fitting')
   time.sleep(.2)
 else:raise TimeoutError('GPU fit readiness timeout')
 row.update(pid=server.pid,loadSeconds=round(time.monotonic()-started,3),loadedMiB=m.used_gpu(),ownedLoaded=m.owned_gpu_snapshot(server.pid));print(json.dumps(row),flush=True)
 started=time.monotonic();api=req('/v1/chat/completions',case['payload']);row.update(seconds=round(time.monotonic()-started,3),raw=api['choices'][0]['message'].get('content',''),usage=api.get('usage'),error=None);print(json.dumps({k:row[k] for k in ['seconds','usage','error']}),flush=True)
except Exception as e:row['error']=str(e);print(json.dumps({'error':str(e)}),flush=True)
finally:
 if server:
  server.terminate()
  try:server.wait(timeout=10)
  except subprocess.TimeoutExpired:server.kill();server.wait(timeout=5)
  row['exitCode']=server.poll();row['ownedAfterExit']=m.owned_gpu_snapshot(server.pid)
 row['finalMiB']=m.used_gpu();(OUT/'NpcReply.json').write_text(json.dumps(row,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({'clean':row.get('ownedAfterExit'),'finalMiB':row['finalMiB']}),flush=True)
