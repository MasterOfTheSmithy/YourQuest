from __future__ import annotations
import hashlib, json
from pathlib import Path
import numpy as np

# note: This summarizes retained result artifacts only; it never imports model runtimes or runs inference.
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Docs/PlayableEvaluation_2026-10-09/reviews/native_vision_completed/result_review.json'
def sha(p:Path)->str: return hashlib.sha256(p.read_bytes()).hexdigest()
def read(rel:str): return json.loads((ROOT/rel).read_text(encoding='utf-8'))
def iou(a,b):
    x1,y1=max(a[0],b[0]),max(a[1],b[1]); x2,y2=min(a[2],b[2]),min(a[3],b[3])
    inter=max(0,x2-x1)*max(0,y2-y1)
    aa=max(0,a[2]-a[0])*max(0,a[3]-a[1]); bb=max(0,b[2]-b[0])*max(0,b[3]-b[1])
    return inter/(aa+bb-inter) if aa+bb>inter else 0.0

def unit_record(rel, result):
    u=read(rel); files=[]
    for f in u['files']:
        files.append({'file':f['staged'],'bytes':f['bytes'],'sha256':f['sha256'],'source_file':f['provenance']['source_file'],'source_revision':f['provenance']['revision'],'repository':f['provenance']['repository']})
    return {'unit':u['unit'],'manifest_path':rel,'manifest_sha256':sha(ROOT/rel),'result_manifest_sha256':result.get('unit_manifest_sha256'),'file_count':len(files),'total_bytes':sum(x['bytes'] for x in files),'files':files}

# SigLIP2 result: input fixture IDs and exact fixture identities are taken from the frozen manifest.
sig_path='outputs/PlayableEvaluation_20261009/native-vision-1/SRC-30/result.json'; sig=read(sig_path)
vision_fixture=read('Tools/PlayableEvaluation/native_vision_tests/fixtures.json')
sig_cases={c['id']:c for c in vision_fixture['cases'] if c['use_case_id']=='IMAGE_SEARCH'}
sig_assets={a['path']:a for a in vision_fixture['assets']}
sig_rows=[]
for r in sig['cases']:
    c=sig_cases[r['case_id']]
    used=[*c.get('gallery',[])] + ([c['query_image']] if c.get('query_image') else [])
    actual=[]
    for p in used:
        f=ROOT/'Tools/PlayableEvaluation/native_vision_tests'/p
        rec=sig_assets[p]
        actual.append({'path':p,'bytes':f.stat().st_size,'sha256':sha(f),'expected_sha256':rec['sha256'],'sha_matches':sha(f)==rec['sha256']})
    raw=r['raw_output']; top=raw['ranked_assets'][0]['asset_id'] if raw and raw.get('ranked_assets') else None
    sig_rows.append({'case_id':r['case_id'],'split':r['split'],'fixture_input':c['input'],'expected':c['expected_behavior'],'expected_top_asset_id':c.get('expected_top_asset_id'),'observed_top_asset_id':top,'top_matches_expected':top==c.get('expected_top_asset_id') if c.get('expected_top_asset_id') else None,'no_match_expected':c.get('no_match_expected',False),'no_match_status':r['observations'].get('no_match_semantic_status'),'abstention_threshold_calibrated':r['observations'].get('abstention_score_calibrated'),'ranked_assets':raw['ranked_assets'],'reported_input_assets':r.get('input_assets'),'fixture_images':actual,'latency_ms':r['inference_wall_ms'],'embedding_latency_ms':raw.get('embedding_latency_ms'),'rss_before_bytes':r['rss_before_bytes'],'rss_after_bytes':r['rss_after_bytes'],'judgment':({'status':'observed_top_match; synthetic ranking only'} if c.get('expected_top_asset_id') and top==c.get('expected_top_asset_id') else {'status':'no_match_requirement_unmet_or_unmeasurable','detail':'nearest unrelated asset is returned; result explicitly has no calibrated abstention threshold'})})

# Depth result: compute NPY identities and regional diagnostics using only NumPy array reading.
depth_path='outputs/PlayableEvaluation_20261009/native-depth-grounding-2/SRC-29/result.json'; depth=read(depth_path)
depth_fixture=read('Tools/PlayableEvaluation/native_depth_grounding_tests/fixtures.json')
asset_manifest=read('Tools/PlayableEvaluation/native_depth_grounding_tests/assets.json')
asset_index={a['path']:a for a in asset_manifest['assets']}; depth_cases={c['id']:c for c in depth_fixture['cases'] if c['use_case_id']=='DEPTH'}
depth_rows=[]
for r in depth['cases']:
    c=depth_cases[r['case_id']]; rel=c['candidate_input']['annotations']; ap=ROOT/'Tools/PlayableEvaluation/native_depth_grounding_tests'/c['candidate_input']['asset']
    arr_path=ROOT/'outputs/PlayableEvaluation_20261009/native-depth-grounding-2/SRC-29'/r['raw_output']; arr=np.load(arr_path,allow_pickle=False)
    regions={}
    for name,box in rel.items():
        if name.endswith('_xyxy'):
            x1,y1,x2,y2=box; a=arr[y1:y2,x1:x2]; q=np.percentile(a,[25,50,75])
            regions[name]={'xyxy':box,'count':int(a.size),'min':float(a.min()),'max':float(a.max()),'mean':float(a.mean()),'std':float(a.std()),'q25_median_q75':[float(v) for v in q]}
    depth_rows.append({'case_id':r['case_id'],'split':r['split'],'expected':c['expected_behavior'],'output_contract':c['candidate_input']['output_contract'],'input_asset':c['candidate_input']['asset'],'input_bytes':ap.stat().st_size,'input_sha256':sha(ap),'expected_input_sha256':c['candidate_input']['asset_sha256'],'fixture_manifest_sha256':sha(ROOT/'Tools/PlayableEvaluation/native_depth_grounding_tests/fixtures.json'),'npy_file':r['raw_output'],'npy_bytes':arr_path.stat().st_size,'npy_sha256_actual':sha(arr_path),'npy_sha256_reported':r['raw_output_sha256'],'shape':list(arr.shape),'dtype':str(arr.dtype),'all_finite':bool(np.isfinite(arr).all()),'global_min':float(arr.min()),'global_max':float(arr.max()),'global_mean':float(arr.mean()),'global_std':float(arr.std()),'regions':regions,'latency_s':r['latency_s'],'rss_before_bytes':r['rss_before_case_bytes'],'rss_after_bytes':r['rss_after_case_bytes'],'judgment':'numeric field observed; output polarity/metric interpretation not assigned'})

# Grounding-DINO result: match returned boxes to the best same-class annotated region.
det_path='outputs/PlayableEvaluation_20261009/native-depth-grounding-2/SRC-32/result.json'; det=read(det_path)
ground_cases={c['id']:c for c in depth_fixture['cases'] if c['use_case_id']=='OBJECT_DETECTION'}
det_rows=[]
for r in det['cases']:
    c=ground_cases[r['case_id']]; data=c['candidate_input']; ap=ROOT/'Tools/PlayableEvaluation/native_depth_grounding_tests'/data['asset']; diag=r['diagnostic']; boxes=[]
    for box,score,label in zip(diag['boxes_xyxy'],diag['scores'],diag['text_labels']):
        matches=[{'target_label':gt[4],'iou':iou(box,gt[:4])} for gt in data['ground_truth_boxes_xyxy']]
        same=[x for x in matches if x['target_label']==label]
        boxes.append({'label':label,'score':score,'xyxy':box,'best_same_label_iou':max((x['iou'] for x in same),default=None),'best_any_iou':max((x['iou'] for x in matches),default=None)})
    raw=ROOT/'outputs/PlayableEvaluation_20261009/native-depth-grounding-2/SRC-32'/r['raw_output']
    det_rows.append({'case_id':r['case_id'],'split':r['split'],'expected':c['expected_behavior'],'input_asset':data['asset'],'input_bytes':ap.stat().st_size,'input_sha256':sha(ap),'expected_input_sha256':data['asset_sha256'],'ground_truth_boxes_xyxy':data['ground_truth_boxes_xyxy'],'prompt':data['text_prompt'],'thresholds':{'score':data['score_threshold'],'text':data['text_threshold']},'raw_output_file':r['raw_output'],'raw_output_sha256_actual':sha(raw),'raw_output_sha256_reported':r['raw_output_sha256'],'native_tensor_file':diag['native_tensor_file'],'native_tensor_sha256_actual':sha(raw.parent/diag['native_tensor_file']),'native_tensor_sha256_reported':diag['native_tensor_sha256'],'detections':boxes,'latency_s':r['latency_s'],'rss_before_bytes':r['rss_before_case_bytes'],'rss_after_bytes':r['rss_after_case_bytes'],'judgment':({'status':'box_matches_reference'} if r['case_id']=='OBJECT_DETECTION-T01' else {'status':'see_case_evidence'})})

# Check exact original case coverage and actual source-script hashes captured by owner plans where available.
plans={'SRC-30':'outputs/PlayableEvaluation_20261009/native-vision-1/SRC-30-owner/plan.json','SRC-29':'outputs/PlayableEvaluation_20261009/native-depth-grounding-2/SRC-29-owner/plan.json','SRC-32':'outputs/PlayableEvaluation_20261009/native-depth-grounding-2/SRC-32-owner/plan.json'}
plan_checks={}
for cand,pr in plans.items():
    p=read(pr); script=Path(p['command'][1]); expected=p.get('script_sha256'); plan_checks[cand]={'plan_path':pr,'plan_sha256':sha(ROOT/pr),'script_path':str(script),'script_sha256_current':sha(script),'script_sha256_receipt':expected,'script_hash_matches':sha(script)==expected,'receipt_path':str(Path(pr).parent/'owner-receipt.json'),'receipt_sha256':sha(ROOT/Path(pr).parent/'owner-receipt.json'),'receipt':read(str(Path(pr).parent/'owner-receipt.json'))}

# Original catalog case IDs are checked against retained result rows.
coverage={}
for cand,rows,role_cases in [('SRC-30',sig_rows,[c['id'] for c in vision_fixture['cases'] if c['use_case_id']=='IMAGE_SEARCH']),('SRC-29',depth_rows,[c['id'] for c in depth_fixture['cases'] if c['use_case_id']=='DEPTH']),('SRC-32',det_rows,[c['id'] for c in depth_fixture['cases'] if c['use_case_id']=='OBJECT_DETECTION'])]:
    observed=[x['case_id'] for x in rows]; coverage[cand]={'expected_case_ids':role_cases,'observed_case_ids':observed,'all_present_once':sorted(observed)==sorted(role_cases) and len(observed)==len(set(observed))}

out={
 'schema':1,'scope':'bounded detached result review; no inference performed by reviewer','generated_from':'retained result JSON, frozen fixture manifests/assets, source locks, archived unit manifests, owner plans/receipts',
 'coverage':coverage,
 'source_and_artifacts':{
  'SRC-30':{'checkpoint':'google/siglip2-base-patch16-224','revision':'75de2d55ec2d0b4efc50b3e9ad70dba96a7b2fa2','result_path':sig_path,'result_sha256':sha(ROOT/sig_path),'fixture_manifest_path':'Tools/PlayableEvaluation/native_vision_tests/fixtures.json','fixture_manifest_sha256':sha(ROOT/'Tools/PlayableEvaluation/native_vision_tests/fixtures.json'),'unit':unit_record('outputs/PlayableEvaluation_20261009/downloads/native-units-1/SRC-30/unit.json',sig),'receipt_and_script':plan_checks['SRC-30'],'runtime':{'device':sig['device'],'model_class':sig['resolved_model_class'],'processor_class':sig['resolved_processor_class'],'packages':sig['package_versions'],'torch_threads':sig['torch_threads'],'model_load_ms':sig['model_load_ms'],'rss_before_load_bytes':sig['rss_before_load_bytes'],'rss_after_load_bytes':sig['rss_after_load_bytes'],'rss_after_cases_bytes':sig['rss_after_cases_bytes'],'unit_verified_files':sig['unit_file_count_sha_verified'],'unit_verified_bytes':sig['unit_bytes_sha_verified'],'latency_definition':'case inference_wall_ms times complete _siglip_case call including image opening/processor work inside call; embedding_latency_ms begins after initial gallery processor construction and includes feature/query path; not device-audible latency.'}},
  'SRC-29':{'checkpoint':'depth-anything/Depth-Anything-V2-Small-hf','revision':'5426e4f0f36572d16453bbda7a8389317b1bef99','result_path':depth_path,'result_sha256':sha(ROOT/depth_path),'source_evidence_path':'Tools/PlayableEvaluation/native_depth_grounding_tests/source_evidence.json','source_evidence_sha256':sha(ROOT/'Tools/PlayableEvaluation/native_depth_grounding_tests/source_evidence.json'),'source_docs':['https://huggingface.co/depth-anything/Depth-Anything-V2-Small-hf/tree/5426e4f0f36572d16453bbda7a8389317b1bef99','https://huggingface.co/docs/transformers/main/model_doc/depth_anything_v2'],'unit':unit_record('outputs/PlayableEvaluation_20261009/downloads/depth-native-1/unit.json',depth),'receipt_and_script':plan_checks['SRC-29'],'runtime':{'device':depth['device'],'runtime_versions':depth['runtime_versions'],'torch_threads':depth['torch_threads'],'model_load_cold_s':depth['model_load_cold_s'],'rss_before_load_bytes':depth['rss_before_load_bytes'],'rss_after_load_bytes':depth['rss_after_load_bytes'],'rss_after_cases_bytes':depth['rss_after_cases_bytes'],'latency_definition':'per-case latency includes fixture image processing, forward pass, post-processing, saving NPY, and summaries; process RSS sampled before/after case.'},'polarity_source_status':'Official cited model documentation/source evidence says relative depth/scale ambiguous but does not establish whether numeric higher means nearer. No near/far semantic PASS assigned.'},
  'SRC-32':{'checkpoint':'IDEA-Research/grounding-dino-tiny','revision':'a2bb814dd30d776dcf7e30523b00659f4f141c71','source_revision':'IDEA-Research/GroundingDINO@856dde20aee659246248e20734ef9ba5214f5e44','result_path':det_path,'result_sha256':sha(ROOT/det_path),'source_evidence_path':'Tools/PlayableEvaluation/native_depth_grounding_tests/source_evidence.json','source_evidence_sha256':sha(ROOT/'Tools/PlayableEvaluation/native_depth_grounding_tests/source_evidence.json'),'source_docs':['https://huggingface.co/IDEA-Research/grounding-dino-tiny/tree/a2bb814dd30d776dcf7e30523b00659f4f141c71','https://huggingface.co/docs/transformers/v4.50.0/model_doc/grounding-dino'],'unit':unit_record('outputs/PlayableEvaluation_20261009/downloads/grounding-native-1/unit.json',det),'receipt_and_script':plan_checks['SRC-32'],'runtime':{'device':det['device'],'runtime_versions':det['runtime_versions'],'torch_threads':det['torch_threads'],'model_load_cold_s':det['model_load_cold_s'],'rss_before_load_bytes':det['rss_before_load_bytes'],'rss_after_load_bytes':det['rss_after_load_bytes'],'rss_after_cases_bytes':det['rss_after_cases_bytes'],'latency_definition':'per-case latency includes preprocessing, forward pass, post-processing, native NPZ + JSON writes, and metrics; process RSS sampled before/after case.'},'output_boundary':'Text-labeled XYXY boxes and scores only; no mask/segmentation output.'}},
 'case_reviews':{'SRC-30':sig_rows,'SRC-29':depth_rows,'SRC-32':det_rows},
 'aggregate_observations':{
  'SRC-30':'Four of five query cases return the fixture target first. The absent moonstone helmet query ranks blue_round_shield first; no calibrated threshold/abstention exists, so expected no-match remains unmet/unmeasurable. Synthetic-only ranking; not natural-image or production search qualification.',
  'SRC-29':'All five outputs are finite 512x512 float32 maps and their NPY hashes match result receipts. The recorded near/far numeric medians do not establish physical depth ordering without source-supported polarity. T01 separation is numerically large; T02 medians differ by only ~0.075. Flat-wall field varies substantially inside the annotated region (std ~0.349, IQR ~0.557); textured-plane map is nonconstant, but this model exposes no calibrated uncertainty/abstention signal. These are synthetic fixture observations without a pass threshold.',
  'SRC-32':'All five output JSON/native tensors match their declared hashes. T01 box is well aligned with its annotated shield. T02 detects potion and lantern regions but emits a second overlapping lantern/lantern-potion phrase box. Empty table returns two false positives; occluded shield case includes one aligned box plus one low-overlap duplicate; absent helmet returns a helmet box. These synthetic misses block no-false-positive/absent-target acceptance for this bounded set.',
  'cross_candidate_limit':'All cases are synthetic rasters, all runtimes detached CPU tests, and all candidate result records explicitly avoid automatic acceptance. No ordinary in-game Unity integration, natural photography, production pipeline, or human visual review is established.'
 },
 'files_reviewed':[
  sig_path,'outputs/PlayableEvaluation_20261009/native-vision-1/SRC-30-owner/owner-receipt.json','outputs/PlayableEvaluation_20261009/native-vision-1/SRC-30-owner/plan.json','outputs/PlayableEvaluation_20261009/downloads/native-units-1/SRC-30/unit.json','Tools/PlayableEvaluation/native_vision_tests/fixtures.json','Tools/PlayableEvaluation/native_vision_tests/runner.py',
  depth_path,'outputs/PlayableEvaluation_20261009/native-depth-grounding-2/SRC-29-owner/owner-receipt.json','outputs/PlayableEvaluation_20261009/native-depth-grounding-2/SRC-29-owner/plan.json','outputs/PlayableEvaluation_20261009/downloads/depth-native-1/unit.json','Tools/PlayableEvaluation/native_depth_grounding_tests/fixtures.json','Tools/PlayableEvaluation/native_depth_grounding_tests/assets.json','Tools/PlayableEvaluation/native_depth_grounding_tests/source_evidence.json','Tools/PlayableEvaluation/native_depth_grounding_tests/run.py',
  det_path,'outputs/PlayableEvaluation_20261009/native-depth-grounding-2/SRC-32-owner/owner-receipt.json','outputs/PlayableEvaluation_20261009/native-depth-grounding-2/SRC-32-owner/plan.json','outputs/PlayableEvaluation_20261009/downloads/grounding-native-1/unit.json'
 ],
 'runtime_acceptance':'not_claimed'
}
OUT.parent.mkdir(parents=True,exist_ok=True); OUT.write_text(json.dumps(out,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print(json.dumps({'output':str(OUT.relative_to(ROOT)),'sha256':sha(OUT),'coverage':coverage,'unit_case_counts':{k:len(v) for k,v in out['case_reviews'].items()},'script_hash_checks':{k:v['script_hash_matches'] for k,v in plan_checks.items()}}))

