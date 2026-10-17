"""Bounded read-only resource observation of Unity and its owned speech/LLM helpers."""
import argparse
import json
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path
import psutil

parser = argparse.ArgumentParser()
parser.add_argument("--unity-pid", type=int, required=True)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
unity = psutil.Process(args.unity_pid)
samples = []
tracked = {}
for _ in range(20):
    # note: Observe only the selected Editor and its children; never read microphone audio or process environments.
    processes = [unity] + unity.children(recursive=True)
    rows = []
    for process in processes:
        try:
            if process != unity and not any(name in process.name().lower() for name in ("python", "llama-server")):
                continue
            tracked.setdefault(process.pid, process)
            observed = tracked[process.pid]
            rows.append({"pid": process.pid, "name": process.name(), "rss_mb": process.memory_info().rss / 1048576,
                         "cpu_one_core_percent": observed.cpu_percent(), "affinity": process.cpu_affinity()})
            if "llama-server" in process.name().lower():
                # note: Record only placement/context flags, excluding credentials and arbitrary arguments.
                command = process.cmdline()
                approved = ("--n-gpu-layers", "--fit", "--device", "--ctx-size", "--threads", "--parallel")
                rows[-1]["placement"] = {arg: command[i + 1] for i, arg in enumerate(command[:-1]) if arg in approved}
        except (psutil.NoSuchProcess, psutil.AccessDenied):
            continue
    gpu = subprocess.run([r"C:\Windows\System32\nvidia-smi.exe", "--query-gpu=memory.used,memory.free,utilization.gpu",
                          "--format=csv,noheader,nounits"], capture_output=True, text=True, timeout=5)
    samples.append({"utc": datetime.now(timezone.utc).isoformat(), "available_ram_mb": psutil.virtual_memory().available / 1048576,
                    "processes": rows, "gpu_csv": gpu.stdout.strip(), "gpu_error": gpu.stderr.strip() if gpu.returncode else ""})
    time.sleep(1)
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps({"evidence": "BOUNDED_EDITOR_RESOURCE_OBSERVATION; no player-build FPS certification", "samples": samples}, indent=2))
print(json.dumps({"samples": len(samples), "min_available_ram_mb": min(s["available_ram_mb"] for s in samples),
                  "last_processes": samples[-1]["processes"], "last_gpu": samples[-1]["gpu_csv"]}))
