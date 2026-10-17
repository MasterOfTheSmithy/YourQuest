"""Isolate speech framing and structured-decoding faults using disposable inputs."""
import copy, json, os, socket, subprocess, time, urllib.request
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'outputs/SpeechRecovery_20261008/TransportProbe'
OUT.mkdir(parents=True,exist_ok=True)
BASE='http://127.0.0.1:11442'
def wire(path,body=None):
    request=urllib.request.Request(BASE+path,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(request,timeout=240) as response: return json.load(response)

packet=json.loads((ROOT/'outputs/StartupVoiceRepair_20261008/QualificationChat/CurrentPayload.json').read_text(encoding='utf-8-sig'))
base=packet['payload']
simple={'type':'object','properties':{'answer':{'type':'boolean'}},'required':['answer'],'additionalProperties':False}
exact='Return this JSON exactly: {"answer":true}'
cases=[]
for name,prompt,format,raw in [
    ('bool_schema',exact,simple,False),
    ('bool_schema_raw_chat',exact,simple,True),
    ('bool_json',exact,'json',False),
    ('review_json',base['prompt'],'json',False),
]:
    payload=copy.deepcopy(base)
    payload.update(prompt=prompt,format=format)
    if raw:
        payload.update(raw=True,prompt='<|im_start|>user\n'+prompt+'<|im_end|>\n<|im_start|>assistant\n<think>\n\n</think>\n\n')
    cases.append((name,payload))

service=None
rows=[]
try:
    with socket.socket() as probe:
        if probe.connect_ex(('127.0.0.1',11442))==0: raise RuntimeError('Diagnostic port already owned.')
    env=dict(os.environ,OLLAMA_HOST='127.0.0.1:11442',OLLAMA_MODELS='D:\\OllamaModels',OLLAMA_NUM_PARALLEL='1',OLLAMA_MAX_LOADED_MODELS='1')
    service=subprocess.Popen([r'C:\Users\Garri\AppData\Local\Programs\Ollama\ollama.exe','serve'],env=env,
        stdout=(OUT/'Server.log').open('w'),stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW|subprocess.BELOW_NORMAL_PRIORITY_CLASS)
    for _ in range(100):
        try: wire('/api/version'); break
        except OSError: time.sleep(.2)
    for name,payload in cases:
        started=time.monotonic()
        result=wire('/api/generate',payload)
        rows.append(dict(name=name,payload=payload,response=result,seconds=time.monotonic()-started))
        (OUT/'Results.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
        print(json.dumps(dict(name=name,text=result.get('response'),tokens=result.get('prompt_eval_count'),seconds=round(time.monotonic()-started,2))),flush=True)
finally:
    if service is not None:
        try: wire('/api/generate',dict(model=base['model'],keep_alive=0,stream=False))
        finally:
            service.terminate()
            try: service.wait(timeout=10)
            except subprocess.TimeoutExpired: service.kill(); service.wait()
