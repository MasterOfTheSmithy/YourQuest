"""Exercise real offline models through the production protocol, without Unity or microphone access."""
import argparse, base64, hashlib, http.client, json, os, struct, subprocess, threading, time, uuid
from pathlib import Path
import numpy as np
import psutil
import soundfile as sf

ROOT = Path(__file__).resolve().parents[2]

def main():
    p = argparse.ArgumentParser()
    p.add_argument('--python', type=Path, default=ROOT / 'outputs/InitialModelStack/venv/Scripts/python.exe')
    args = p.parse_args()
    out = ROOT / 'outputs/InitialModelStack/proof'
    out.mkdir(parents=True, exist_ok=True)
    token = uuid.uuid4().hex + uuid.uuid4().hex
    env = dict(os.environ, YOURQUEST_SPEECH_TOKEN=token)
    log = (out / 'worker.log').open('w', encoding='utf-8')
    flags = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0
    process = subprocess.Popen([str(args.python), '-u', str(ROOT / 'Tools/LocalSpeech/server.py'),
        '--cache', str(ROOT / 'outputs/InitialModelStack/cache'), '--parent-pid', str(os.getpid())],
        env=env, stdout=subprocess.PIPE, stderr=log, text=True, creationflags=flags)
    worker = None
    stop = threading.Event()
    report = {'evidence': 'Real detached CPU HTTP routes. Not microphone, Unity playback, dialogue persistence or game coexistence proof.', 'checks': []}
    try:
        hello = json.loads(process.stdout.readline())
        assert hello['protocol'] == 1
        port = hello['port']; worker = psutil.Process(hello['pid'])
        peak = [worker.memory_info().rss]
        def monitor():
            while not stop.wait(.025):
                try: peak[0] = max(peak[0], worker.memory_info().rss)
                except psutil.NoSuchProcess: return
        threading.Thread(target=monitor, daemon=True).start()
        report['cpu_affinity'] = worker.cpu_affinity()
        report['priority'] = worker.nice()
        def request(route, body=None, authenticated=True):
            conn = http.client.HTTPConnection('127.0.0.1', port, timeout=65)
            headers = {'Authorization': 'Bearer ' + token} if authenticated else {}
            data = json.dumps(body).encode('utf-8') if body is not None else None
            if data is not None: headers['Content-Type'] = 'application/json'
            conn.request('POST' if data is not None else 'GET', route, data, headers)
            return conn, conn.getresponse()
        def call(route, body=None, status=200, authenticated=True):
            conn, response = request(route, body, authenticated)
            actual = response.status; data = response.read();conn.close()
            assert actual == status, (route, actual, data)
            return json.loads(data)
        def check(name): report['checks'].append(name); print('PASS', name, flush=True)
        call('/health', status=401, authenticated=False);check('unauthenticated loopback access rejected')
        call('/missing', {}, status=404);check('unknown routes rejected')
        t = time.perf_counter()
        lazy = call('/retrieve', {'query': 'Where did I leave my sword?',
                                 'candidates': ['player: I left my sword in the chapel.', 'npc: Fresh bread is ready.'], 'limit': 1})
        health = call('/health')
        assert lazy['indices'] == [0] and health['retrieval_ready'] and not health['ready']
        report['lazy_retrieval_seconds'] = time.perf_counter() - t
        check('retrieval starts offline before loading voice or opening a microphone')
        t = time.perf_counter();call('/prepare', {});report['prepare_seconds'] = time.perf_counter()-t
        health = call('/health');assert health['device']=='cpu' and health['ready'] and health['ort_single_thread'];check('CPU-only models prepared offline')
        recall_cases = [
            ('Where did I put my weapon?', ['player: I left my sword beside the chapel altar.', 'npc: The baker sells fresh bread.', 'player: My sister grows roses.'], 0),
            ('How can I get across the river?', ['npc: The healer needs clean bandages.', 'npc: The ferry crosses the river near the mill.', 'player: I bought a new coat.'], 1),
            ('What did the injured person need?', ['player: The merchant lost his ledger.', 'npc: The bridge is closed.', 'npc: The wounded guard asked for a bandage.'], 2)]
        retrieval_results=[]
        for query,candidates,expected in recall_cases:
            began=time.perf_counter();ranked=call('/retrieve',{'query':query,'candidates':candidates,'limit':1})
            assert ranked['indices']==[expected] and .25 <= ranked['scores'][0] <= 1.001, ranked
            retrieval_results.append(dict(query=query,result=ranked,seconds=time.perf_counter()-began))
        report['retrieval']=retrieval_results;check('MiniLM retrieves three paraphrased memories from supplied reports')
        call('/retrieve',{'query':'Where is the sword?','candidates':['npc: Only this other NPC knows about bread.'],'limit':1})
        call('/retrieve',{'query':'test','candidates':['../arbitrary-path'],'limit':99},status=400)
        call('/retrieve',{'query':'test','candidates':['x']*129,'limit':1},status=400)
        call('/retrieve',{'query':'test','candidates':[],'limit':1},status=400)
        check('memory requests reject invalid bounds and never access a transcript store')
        t = time.perf_counter()
        call('/retrieve', {'query': 'Where is the chapel sword?',
                          'candidates': [('player: The chapel sword is behind the altar. ' * 13)[:600]] * 128, 'limit': 4})
        report['maximum_retrieval_seconds'] = time.perf_counter() - t
        assert report['maximum_retrieval_seconds'] < 8
        check('largest supported transcript finishes within the Unity request timeout')
        retired_memory = {}
        def request_retired_memory():
            try:
                call('/retrieve', {'query': 'Where is the sword?', 'candidates': ['player: The sword is in the chapel. ' * 16] * 128, 'limit': 1}, status=400)
                retired_memory['passed'] = True
            except Exception as error:
                retired_memory['error'] = str(error)
        pending_memory = threading.Thread(target=request_retired_memory)
        pending_memory.start()
        deadline = time.monotonic() + 3
        while not call('/health')['busy'] and time.monotonic() < deadline:
            time.sleep(.01)
        assert call('/health')['busy']
        call('/cancel', {})
        pending_memory.join(timeout=5)
        assert retired_memory.get('passed'), retired_memory
        check('cancelled retrieval cannot yield an old memory result')
        text = 'The northern road is closed. Stay close to me, and we will find another way home.'
        call('/speak', {'text': text, 'voice': '../arbitrary-file'}, status=400);check('unapproved voices rejected')
        began=time.perf_counter();conn,response=request('/speak', {'text':text,'voice':'alba'})
        assert response.status == 200
        raw=response.read(12);assert struct.unpack('<4sII',raw)==(b'YQSP',24000,1)
        audio=[];first=None
        while True:
            header=response.read(4);assert len(header)==4
            size=struct.unpack('<I',header)[0];raw+=header
            if size==0:break
            payload=response.read(size);assert len(payload)==size;raw+=payload
            if first is None:first=time.perf_counter()-began
            audio.append(np.frombuffer(payload,dtype='<f4').copy())
        assert response.read()==b'';conn.close()
        samples=np.concatenate(audio);assert np.isfinite(samples).all() and np.max(np.abs(samples))>.001
        (out/'speech.pcmstream').write_bytes(raw)
        sf.write(out/'route-proof.wav',samples,24000)
        report['tts_first_audio_seconds']=first;report['tts_full_seconds']=time.perf_counter()-began;report['audio_seconds']=len(samples)/24000
        check('framed Kokoro audio streamed through /speak')
        session=uuid.uuid4().hex;call('/asr/start',{'session':session,'terms':['northern road']})
        call('/retrieve', {'query': 'Where is the sword?', 'candidates': ['player: The chapel.'], 'limit': 1}, status=400)
        check('dictation also excludes memory inference')
        call('/speak',{'text':text,'voice':'alba'},status=400);check('dictation excludes concurrent synthesis')
        call('/asr/chunk',{'session':session,'rate':24000,'pcm':base64.b64encode(np.array([np.nan],dtype='<f4').tobytes()).decode()},status=400)
        check('non-finite microphone samples rejected')
        began=time.perf_counter()
        streamed=[]
        for i in range(0,len(samples),12000):
            packet=call('/asr/chunk',{'session':session,'rate':24000,'pcm':base64.b64encode(samples[i:i+12000].astype('<f4').tobytes()).decode()})
            if packet.get('complete'):streamed.append(packet['text'])
        final=call('/asr/finish',{'session':session})['text'] or ' '.join(streamed)
        report['streamed_asr_seconds']=time.perf_counter()-began;report['expected']=text;report['actual']=final
        assert 'northern road is closed' in final.lower() and 'way home' in final.lower(),final
        check('Whisper Tiny final text returned through streamed dictation routes')
        call('/asr/finish',{'session':session},status=400);check('capture cannot finalize twice')
        # note: This proves automatic end-of-utterance delivery without /finish or a held key.
        live_session=uuid.uuid4().hex;call('/asr/start',{'session':live_session,'terms':[]})
        completed=[]
        live_audio=np.concatenate([samples,np.zeros(24000*3,dtype=np.float32)])
        for i in range(0,len(live_audio),12000):
            packet=call('/asr/chunk',{'session':live_session,'rate':24000,'pcm':base64.b64encode(live_audio[i:i+12000].astype('<f4').tobytes()).decode()})
            if packet.get('complete'):completed.append(packet['text'])
        assert completed and 'northern road' in ' '.join(completed).lower(),completed
        report['vad_completed_text']=completed
        extra=call('/asr/chunk',{'session':live_session,'rate':24000,'pcm':base64.b64encode(np.zeros(12000,dtype='<f4').tobytes()).decode()})
        assert not extra['complete'];check('live speech completes once after silence without button release')
        call('/cancel',{})
        retired=uuid.uuid4().hex;call('/asr/start',{'session':retired,'terms':[]});call('/cancel',{})
        call('/asr/chunk',{'session':retired,'rate':24000,'pcm':base64.b64encode(samples[:12000].tobytes()).decode()},status=400)
        call('/asr/finish',{'session':retired},status=400);check('cancelled captures cannot yield final text')
        # note: Cancel a live streamed response and verify that the one inference slot becomes reusable.
        conn,response=request('/speak',{'text':'Please follow the lanterns and stay with the group. '*12,'voice':'javert'})
        assert response.status==200 and len(response.read(12))==12
        call('/prepare',{},status=409);check('native work is serialized without an unbounded request queue')
        call('/cancel',{});response.read();conn.close()
        limit=time.monotonic()+5
        while call('/health')['busy'] and time.monotonic()<limit:time.sleep(.05)
        assert not call('/health')['busy'];check('speech interruption releases the inference slot')
        next_session=uuid.uuid4().hex;call('/asr/start',{'session':next_session,'terms':[]})
        empty=call('/asr/finish',{'session':next_session})['text'];assert not empty.strip();check('empty capture produces no player message')
        report['peak_worker_rss_bytes']=peak[0]
        call('/shutdown',{});process.wait(timeout=5)
        assert not worker.is_running();check('owned launcher and native worker both exit')
        report['status']='DETACHED_ROUTE_PASS_UNITY_MICROPHONE_AND_GAMEPLAY_UNVERIFIED'
        report['source_sha256']={str(f.relative_to(ROOT)):hashlib.sha256(f.read_bytes()).hexdigest()
            for f in [ROOT/'Tools/LocalSpeech/server.py',ROOT/'Tools/LocalSpeech/initial_stack.py',ROOT/'Assets/Assets/Scripts/Dialogue/DialogueThinkService.cs',ROOT/'Assets/Assets/Scripts/Dialogue/YQDialogueMemorySelection.cs',ROOT/'Assets/Assets/Scripts/Dialogue/YQLocalSpeech.cs',ROOT/'Assets/Assets/Scripts/Dialogue/YQSpeechPcmBuffer.cs',ROOT/'Assets/Assets/Scripts/Tutorial/YQInvestorDialogueUI.cs']}
    finally:
        stop.set()
        if worker is not None:
            try:
                if worker.is_running():worker.terminate()
            except psutil.NoSuchProcess:pass
        if process.poll() is None:process.terminate();process.wait(timeout=5)
        log.close()
        (out/'receipt.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(report,indent=2),flush=True)

if __name__=='__main__':main()
