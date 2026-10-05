import importlib.util,json,os,subprocess,time,urllib.request
from pathlib import Path
ROOT=Path.cwd();OUT=ROOT/'outputs/LlmRepair_20261005';data=json.loads((OUT/'VoiceCases.json').read_text(encoding='utf-8-sig'))
base='http://127.0.0.1:11438';service=None;rows=[]
def req(path,body=None,timeout=15):
 b=None if body is None else json.dumps(body,ensure_ascii=False).encode('utf-8');q=urllib.request.Request(base+path,data=b,headers={'Content-Type':'application/json'})
 with urllib.request.urlopen(q,timeout=timeout) as r:return json.load(r)
try:
 env=dict(os.environ,OLLAMA_HOST='127.0.0.1:11438',OLLAMA_MODELS='D:/OllamaModels',OLLAMA_NUM_PARALLEL='1',OLLAMA_MAX_LOADED_MODELS='1')
 modeldata=json.loads((ROOT/'outputs/LlmTiming_20261005/Cases.json').read_text(encoding='utf-8-sig'))
 service=subprocess.Popen([modeldata['ollamaExecutable'],'serve'],env=env,stdout=(OUT/'VoiceServer.log').open('w'),stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW|subprocess.BELOW_NORMAL_PRIORITY_CLASS)
 for i in range(100):
  try:tags=req('/api/tags',timeout=2);break
  except Exception:time.sleep(.2)
 digest=next(m['digest'] for m in tags['models'] if m['name']=='smaller-test:latest');assert digest=='1dcf59c4b2d0b233363c689818a1c48dfd65e5c96f1594ba57f6d851e84f869e'
 for case in data['cases']:
  body=case['payload'];attempts=[];api={};error=None
  for attempt in range(3):
   started=time.monotonic()
   try:api=req('/api/generate',body);error=None
   except Exception as e:error=str(e)
   attempts.append(dict(seconds=round(time.monotonic()-started,3),error=error))
   if error is None:break
   body['options']['temperature']=.28
  row=dict(name=case['name'],raw=api.get('response',''),error=error,model=api.get('model'),digest=digest,attempts=attempts,loadSeconds=api.get('load_duration',0)/1e9,promptSeconds=api.get('prompt_eval_duration',0)/1e9,outputSeconds=api.get('eval_duration',0)/1e9,inputTokens=api.get('prompt_eval_count'),outputTokens=api.get('eval_count'));rows.append(row)
  (OUT/'VoiceReplies.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({k:row[k] for k in ['name','error','attempts']}),flush=True)
 req('/api/generate',{'model':'smaller-test:latest','keep_alive':0,'stream':False},timeout=20)
 for i in range(100):
  if not req('/api/ps')['models']:break
  time.sleep(.1)
 else:raise RuntimeError('Owned voice runner remains resident after unload')
 print('Voice model released before any GPU load',flush=True)
finally:
 if service:
  service.terminate()
  try:service.wait(timeout=10)
  except subprocess.TimeoutExpired:service.kill();service.wait(timeout=5)
