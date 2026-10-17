"""Bounded read-only residency and host telemetry for the Unity coexistence test."""
import json
import subprocess
import time
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
import psutil

OUT = Path(__file__).resolve().parents[2] / 'outputs/LlmRequalification_20261008'
STOP = OUT / 'Telemetry.stop'
started = time.monotonic()
count = 0
with (OUT / 'HostTelemetry.jsonl').open('a', encoding='utf-8') as stream:
    while time.monotonic() - started < 3600 and not STOP.exists():
        row = dict(utc=datetime.now(timezone.utc).isoformat(), seconds=round(time.monotonic()-started, 3))
        try:
            info = subprocess.run(['nvidia-smi', '--query-gpu=memory.total,memory.used,memory.free,utilization.gpu',
                                   '--format=csv,noheader,nounits'], capture_output=True, text=True, timeout=5,
                                  creationflags=subprocess.CREATE_NO_WINDOW)
            values = [int(value.strip()) for value in info.stdout.splitlines()[0].split(',')]
            row['gpu'] = dict(zip(['totalMiB', 'usedMiB', 'freeMiB', 'utilizationPercent'], values))
            memory = psutil.virtual_memory()
            row['ram'] = dict(availableBytes=memory.available, usedBytes=memory.used, totalBytes=memory.total,
                              paging=psutil.swap_memory()._asdict())
            row['processes'] = []
            # note: Never collect command lines; Unity may place private authentication data there.
            for process in psutil.process_iter(['pid', 'ppid', 'name', 'exe', 'create_time', 'memory_info']):
                if (process.info['name'] or '').lower() in ('unity.exe', 'llama-server.exe', 'ollama.exe', 'ollama_llama_server.exe'):
                    data = {key: process.info[key] for key in ['pid', 'ppid', 'name', 'exe', 'create_time']}
                    data['memory'] = process.info['memory_info']._asdict() if process.info['memory_info'] else None
                    row['processes'].append(data)
            try:
                with urllib.request.urlopen('http://127.0.0.1:11434/api/ps', timeout=2) as response:
                    row['externalOllamaResidency'] = json.load(response)
            except Exception as error:
                row['externalOllamaProbe'] = str(error)
            current = OUT / 'UnityBaseline/Current.json'
            if current.exists():
                row['unity'] = json.loads(current.read_text(encoding='utf-8-sig'))
        except Exception as error:
            row['error'] = str(error)
        stream.write(json.dumps(row, separators=(',', ':')) + '\n')
        stream.flush()
        (OUT / 'HostCurrent.json').write_text(json.dumps(row, indent=2), encoding='utf-8')
        count += 1
        time.sleep(2)
print(json.dumps(dict(samples=count, elapsedSeconds=round(time.monotonic()-started, 2))))
