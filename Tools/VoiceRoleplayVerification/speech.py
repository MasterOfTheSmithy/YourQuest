"""Exercise installed CPU speech over owned HTTP, with synthetic audio explicitly separated from microphone evidence."""
import base64, hashlib, http.client, json, os, struct, subprocess, threading, time, uuid
from pathlib import Path
import numpy as np
import psutil
import soundfile as sf

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'outputs/VoiceRoleplayRepair_20261011/speech'

def main():
    OUT.mkdir(parents=True,exist_ok=False)
    rows=[]
    for label, script in [('before',ROOT/'outputs/VoiceRoleplayRepair_20261011/preimages/server.py'),('after',ROOT/'Tools/LocalSpeech/server.py')]:
        token=uuid.uuid4().hex+uuid.uuid4().hex
        log=(OUT/(label+'.log')).open('w',encoding='utf-8')
        process=subprocess.Popen([str(ROOT/'outputs/InitialModelStack/venv/Scripts/python.exe'),'-u',str(script),'--cache',str(ROOT/'outputs/InitialModelStack/cache'),'--parent-pid',str(os.getpid())],
            env=dict(os.environ,YOURQUEST_SPEECH_TOKEN=token),stdout=subprocess.PIPE,stderr=log,text=True,creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            hello=json.loads(process.stdout.readline()); port=hello['port']; child=psutil.Process(hello['pid'])
            def request(route,body=None):
                conn=http.client.HTTPConnection('127.0.0.1',port,timeout=65)
                conn.request('POST' if body is not None else 'GET',route,body=json.dumps(body) if body is not None else None,
                             headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'})
                return conn,conn.getresponse()
            def call(route,body=None,status=200):
                conn,response=request(route,body); data=response.read(); conn.close()
                assert response.status==status,(route,response.status,data)
                return json.loads(data)
            began=time.perf_counter(); call('/prepare',{}); prepare=time.perf_counter()-began
            rows.append(dict(label=label,stage='prepare',seconds=prepare,rss_mb=child.memory_info().rss/1048576,health=call('/health')))
            cases=[('alba',1),('anna',1),('javert',1),('marius',1)]
            if label=='after': cases += [('anna',.92),('anna',1.06),('javert',.92),('javert',1.06)]
            for voice,speed in cases:
                began=time.perf_counter();conn,response=request('/speak',dict(text='Do not speak to me like that. I have done nothing to deserve it.',voice=voice,speed=speed))
                assert response.status==200
                header=response.read(12);assert struct.unpack('<4sII',header)==(b'YQSP',24000,1)
                parts=[];first=None
                while True:
                    count=struct.unpack('<I',response.read(4))[0]
                    if count==0:break
                    chunk=response.read(count);assert len(chunk)==count
                    if first is None:first=time.perf_counter()-began
                    parts.append(chunk)
                conn.close();raw=b''.join(parts);pcm=np.frombuffer(raw,dtype='<f4');assert np.isfinite(pcm).all() and np.max(np.abs(pcm))>.001
                file=OUT/f'{label}-{voice}-{speed}.wav';sf.write(file,pcm,24000)
                rows.append(dict(label=label,stage='speak',voice=voice,speed=speed,first_pcm_seconds=first,total_seconds=time.perf_counter()-began,
                    audio_seconds=len(pcm)/24000,pcm_sha256=hashlib.sha256(raw).hexdigest(),rss_mb=child.memory_info().rss/1048576,file=str(file)))
            if label=='after':
                for invalid in [True,0,4,'sharp']:
                    call('/speak',dict(text='Test.',voice='anna',speed=invalid),400)
                # note: A known synthetic voice is fed through real VAD/Whisper routes. This is not a microphone or recognition-quality acceptance recording.
                conn,response=request('/speak',dict(text='Fuck you cunt.',voice='marius',speed=1));assert response.status==200
                response.read(12);parts=[]
                while True:
                    count=struct.unpack('<I',response.read(4))[0]
                    if count==0:break
                    parts.append(response.read(count))
                conn.close();pcm=np.frombuffer(b''.join(parts),dtype='<f4')
                session=uuid.uuid4().hex;call('/asr/start',dict(session=session,terms=[]))
                audio=np.concatenate((pcm,np.zeros(24000,dtype=np.float32)))
                finals=[];began=time.perf_counter()
                for offset in range(0,len(audio),12000):
                    part=audio[offset:offset+12000].astype('<f4')
                    result=call('/asr/chunk',dict(session=session,pcm=base64.b64encode(part.tobytes()).decode(),rate=24000))
                    if result.get('complete'):finals.append(result['text'])
                result=call('/asr/finish',dict(session=session));assert len(finals)<=1
                if finals:assert not result['text']
                rows.append(dict(label=label,stage='synthetic_asr',source='Kokoro synthetic player words; no microphone',expected='Fuck you cunt.',finals=finals,finish=result,
                    seconds=time.perf_counter()-began,duplicate_final=False,invalid_pacing_rejected=True))
            call('/shutdown',{})
        finally:
            if process.poll() is None:process.terminate()
            process.wait(timeout=15);log.close()
            (OUT/'receipt.json').write_text(json.dumps(dict(evidence='DETACHED_REAL_CPU_SPEECH_PROTOCOL; no listening, Unity playback, microphone or frame-time acceptance',rows=rows),indent=2),encoding='utf-8')
    print(json.dumps(rows,indent=2),flush=True)

if __name__=='__main__':main()
