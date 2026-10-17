"""Bounded observation of the current benchmark and Unity; no process mutation or Play Mode claim."""
import argparse, json, subprocess, time, winreg
from pathlib import Path
import psutil

ROOT=Path(__file__).resolve().parents[2]
def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--model-pid',type=int,required=True);args=p.parse_args()
    out=args.out.resolve()
    if not out.is_relative_to(ROOT/'outputs') or out.exists():raise ValueError('Fresh workspace output required')
    with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE,r'HARDWARE\DESCRIPTION\System\CentralProcessor\0') as key:
        cpu=winreg.QueryValueEx(key,'ProcessorNameString')[0].strip()
    processes=[psutil.Process(args.model_pid)]
    for process in psutil.process_iter(['name']):
        if process.info['name']=='Unity.exe':processes.append(process)
    for process in processes:process.cpu_percent()
    rows=[]
    # note: Samples are read-only and bounded; Edit Mode resource use cannot establish player frame-time or microphone-to-audio latency.
    for _ in range(30):
        time.sleep(1)
        state=psutil.virtual_memory();observed=[]
        for process in processes:
            try:observed.append(dict(pid=process.pid,name=process.name(),rssMiB=process.memory_info().rss/1048576,cpuPercentOfMachine=process.cpu_percent()/psutil.cpu_count()))
            except (psutil.NoSuchProcess,psutil.AccessDenied):observed.append(dict(pid=process.pid,unavailable=True))
        gpu=subprocess.check_output(['nvidia-smi','--query-gpu=name,memory.used,memory.free,memory.total','--format=csv,noheader,nounits'],text=True).strip()
        rows.append(dict(wall=time.time(),systemUsedMiB=state.used/1048576,systemAvailableMiB=state.available/1048576,processes=observed,gpu=gpu))
    out.write_text(json.dumps(dict(cpu=cpu,logicalCpus=psutil.cpu_count(),systemRamGiB=state.total/1073741824,rows=rows,
        evidence='OBSERVED_TARGET_MACHINE_RESOURCE_SAMPLES_WITH_UNITY_EDIT_MODE; no PlaySafe frame-time or full speech pipeline acceptance'),indent=2),encoding='utf-8')
    print(json.dumps(dict(cpu=cpu,systemRamGiB=state.total/1073741824,samples=len(rows))))
if __name__=='__main__':main()
