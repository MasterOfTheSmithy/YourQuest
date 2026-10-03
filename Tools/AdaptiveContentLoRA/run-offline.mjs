import { spawnSync } from 'node:child_process';
import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { dirname, resolve, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { hash, VERSION } from './core.mjs';

const root=dirname(fileURLToPath(import.meta.url)),repo=resolve(root,'../..');
const steps=[
  ['build',['Tools/AdaptiveContentLoRA/cli.mjs','build']],
  ['development_regressions',['--test','Tools/AdaptiveContentLoRA/tests.mjs']],
  ['candidate_validation',['Tools/AdaptiveContentLoRA/cli.mjs','check']],
  ['local_teacher_prompts',['Tools/AdaptiveContentLoRA/cli.mjs','prepare-teacher']],
  ['repository_guidance',['Tools/verify-project-guidance.mjs','--guidance-only']],
  ['training_export_guard',['Tools/AdaptiveContentLoRA/cli.mjs','export-training'],'dataset not approved for training'],
  ['paired_evaluation_guard',['Tools/AdaptiveContentLoRA/cli.mjs','pair'],'paired_condition_mismatch:']
];
const outcomes=[];
try {
  // note: Only fixed Node CPU commands are executable here. Model/teacher/trainer/network commands are deliberately absent.
  for(const[name,args,expectedRefusal]of steps){
    const result=spawnSync(process.execPath,args,{cwd:repo,encoding:'utf8',timeout:45000,maxBuffer:4*1024*1024,windowsHide:true});
    const message=(result.stdout??'')+(result.stderr??'');
    const passed=!result.error&&(expectedRefusal?result.status!==0&&message.includes(expectedRefusal):result.status===0);
    outcomes.push({step:name,status:passed?(expectedRefusal?'EXPECTED_REFUSAL':'PASS'):'FAIL',exit_code:result.status,
      output_sha256:hash(message),test_count:name==='development_regressions'?Number(message.match(/tests (\d+)/)?.[1]??0):null});
    console.log(`${name}: ${outcomes.at(-1).status}`);
    if(!passed)throw Error(`${name}: ${result.error?.message??message}`);
  }
  const errors=[],files=[];
  // note: Local private reviews/predictions are outside this public artifact receipt and are not opened.
  function inspect(directory){for(const entry of readdirSync(directory,{withFileTypes:true})){
    const path=resolve(directory,entry.name);if(entry.isSymbolicLink())throw Error('artifact_symlink_not_supported');
    if(entry.isDirectory()){inspect(path);continue;}if(entry.name.includes('.local.'))continue;
    if(!/\.(jsonl?|mjs|md)$/.test(entry.name))continue;
    const content=readFileSync(path,'utf8');files.push(relative(repo,path).replaceAll('\\','/'));
    if(/[^\S\r\n]+\r?$/m.test(content))errors.push(`trailing_whitespace:${relative(repo,path)}`);
    try{if(entry.name.endsWith('.json'))JSON.parse(content);else if(entry.name.endsWith('.jsonl'))for(const line of content.split(/\r?\n/).filter(x=>x.trim()))JSON.parse(line);}
    catch(error){errors.push(`parse:${relative(repo,path)}:${error.message}`);}
  }}
  inspect(root);inspect(resolve(repo,'Docs/AdaptiveContentLoRA'));
  if(errors.length)throw Error(errors.join('\n'));
  const dataset=JSON.parse(readFileSync(resolve(root,'reports/dataset-check.json'),'utf8'));
  const receipt={version:VERSION,verified_at_utc:new Date().toISOString(),status:'PASS_CPU_ARTIFACTS_ONLY',node_version:process.version,
    steps:outcomes,parsed_and_whitespace_checked_files:files.length,dataset_sha256:dataset.dataset_sha256,family_seal_sha256:dataset.family_seal_sha256,
    records:dataset.records,training_eligible_count:dataset.training_eligible_count,model_invocations:0,teacher_invocations:0,
    unity_checks:'NOT_RUN',training:'NOT_RUN',adapter:'NOT_CREATED',integration:'NOT_IMPLEMENTED',hidden_inputs:'RESERVED_NOT_AUTHORED',errors:[]};
  writeFileSync(resolve(root,'reports/artifact-verification.json'),JSON.stringify(receipt,null,2)+'\n','utf8');
  console.log(`artifact_parse_and_whitespace: PASS (${files.length} files); model/teacher invocations=0; training eligible=${dataset.training_eligible_count}`);
}catch(error){console.error(String(error.message??error));process.exitCode=1;}
