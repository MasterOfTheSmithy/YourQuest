"""Package task-only changes and evidence without promoting detached checks to gameplay acceptance."""
import difflib, hashlib, json, statistics
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'outputs/VoiceRoleplayRepair_20261011'
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def main():
    changes=[];patch=[]
    for old in sorted((OUT/'preimages').rglob('*')):
        if old.suffix not in ('.cs','.py'):continue
        relative=old.relative_to(OUT/'preimages')
        current=ROOT/('Tools/LocalSpeech' if old.suffix=='.py' else 'Assets/Assets/Scripts')/relative
        if not current.exists():raise ValueError(str(current))
        before=old.read_bytes();after=current.read_bytes()
        delta=list(difflib.unified_diff(before.decode('utf-8-sig').splitlines(True),after.decode('utf-8-sig').splitlines(True),
            fromfile='preimage/'+str(current.relative_to(ROOT)).replace('\\','/'),tofile='current/'+str(current.relative_to(ROOT)).replace('\\','/')))
        if delta:
            patch.extend(delta);changes.append(dict(file=str(current.relative_to(ROOT)),preimage_sha256=hashlib.sha256(before).hexdigest(),current_sha256=hashlib.sha256(after).hexdigest(),
                added=sum(x.startswith('+') and not x.startswith('+++') for x in delta),removed=sum(x.startswith('-') and not x.startswith('---') for x in delta)))
    (OUT/'task_changes.patch').write_text(''.join(patch),encoding='utf-8',newline='')
    before=[r for r in read(OUT/'comparison_final16/dialogue.json') if r['label']=='before']
    after=[r for r in read(OUT/'comparison_final16/dialogue.json') if r['label']=='after']
    final_packets=read(OUT/'final_checks_verified/packets.json');tested_packets=read(OUT/'revised_checks/packets.json')
    if final_packets!=tested_packets:raise ValueError('Retained production prompts/schemas differ from the measured revision')
    samples=read(OUT/'final_model16/resources.json');speech=read(OUT/'speech/receipt.json')
    times={label:{'uncached_context_median_seconds':statistics.median(r['seconds'] for r in rows if r['sample']==0),
        'prefix_cache_median_seconds':statistics.median(r['seconds'] for r in rows if r['sample']==1),
        'samples':len(rows)} for label,rows in [('before',before),('after',after)]}
    examples=[dict(case=case,player=next(r['player'] for r in after if r['id']==case),before=next(r['parsed']['npcText'] for r in before if r['id']==case and r['sample']==0),
        after=next(r['parsed']['npcText'] for r in after if r['id']==case and r['sample']==0),social=next(r['parsed']['social'] for r in after if r['id']==case and r['sample']==0),
        evidence='ACTUAL_DETACHED_GENERATOR_OUTPUT; not production reviewer, delivered speech, state commit or gameplay acceptance') for case in ['original','paraphrase','benign','ordinary','correction','refusal']]
    matrix=[
        ('direct insult','PARTIAL','2/2 model reactions; conservative private admission and replay checks PASS; ordinary delivery/audio not tested'),
        ('paraphrases/minor errors/euphemisms','PARTIAL','Holdout nonprofane reply responds to insult, one target label wrong; numeric penalties remain conservative; timid replies incorrectly apologize'),
        ('friendly banter','PARTIAL','2/2 banter labels; zero grammar penalty; generated wording repeats player'),
        ('benign profanity','PARTIAL','Both replies address blade; zero grammar penalty; social act/target labels fail'),
        ('reported insult','PARTIAL','2/2 reported_speech/other; zero grammar penalty; attribution/affect still imperfect'),
        ('target correction','PARTIAL','2/2 correction/other; responses de-escalate; previously committed numeric grievance is not undone'),
        ('credible threat','PARTIAL','2/2 threat/npc with afraid/soft; no threat event or resident defensive action registered'),
        ('repeated permitted combat escalation','NOT IMPLEMENTED','No resident speech-triggered hostility policy/transition authority found; no combat was invented'),
        ('prohibited combat alternative','PARTIAL','Verbal-only capability evidence provided; no forced attack; gameplay alternatives not exercised'),
        ('insult apology later encounter','PARTIAL','Two apology proposals and retained serialized recipient grievances; no numeric forgiveness or ordinary Continue witness'),
        ('compliment small talk opinion shared incident','PARTIAL','Ordinary blacksmith opinion tested; compliment and shared-incident live regression missing'),
        ('knowledge limitation','PARTIAL','2/2 specific unknown duke answer; generated confidence zero no longer rejects valid line'),
        ('specific refusal','PARTIAL','2/2 refusal proposals; no gift authorized; actual service-state agreement not exercised'),
        ('uncertain audio/addressee','PARTIAL','Frozen addressee and discard on switch; ASR confidence unavailable; targeted clarification under actual uncertain audio not tested'),
        ('timeout invalid cancellation','PARTIAL','Source bounded repair and distinct technical UI; closed schema, unsupported action and delivery tests PASS; live fault injection missing'),
        ('different voices identical text cache','PASS_PROTOCOL_ONLY','Four distinct PCM identities on same text; no active audio cache exists'),
        ('one speaker varied emotions','PARTIAL','Same Anna/Javert tensor identities across verified pacing; convincing affect and listening not verified'),
        ('switch interrupt despawn pool save load','PARTIAL','Immutable casting/identity and current-owner playback guard; native metadata hierarchy PASS; real playback/routing/reload stress not tested')]
    report=dict(status='PARTIAL_REPAIRS_IMPLEMENTED; FULL_ACCEPTANCE_NOT_MET',changed=changes,
        preserved=['Existing user preimages','Unity metas/GUIDs/scenes/import settings','Persistent save schema and canonical commit owners','No model installation or production backend/config swap','No active save used by these fixtures'],
        pipeline=[
            'YQInvestorDialogueUI.UpdateLiveVoice selects/fixes hearing NPC; YQLocalSpeech.BeginDictation owns microphone.',
            'Tools/LocalSpeech/server.py -> initial_stack.py: Silero VAD and WhisperTiny int8; final text trimmed, no profanity censorship.',
            'Frozen NPC receives final session ID via NpcDialogueAgent.SendPlayerMessageWithDisplayedOffers.',
            'Conservative delivered direct insults commit through YQDispositionDomain and YQProfileSaveSystem before inference.',
            'Existing recent transcript, scoped CaptureFacts/grievances and persona feed DialogueThinkService.',
            'Existing Qwen3.5-4B generator -> closed envelope/social hints -> bounded existing span reviewer/repair.',
            'Accepted exact line and delivery plus frozen YQNpcVoice identity -> Kokoro speed request -> YQSpeechPcmBuffer streaming AudioSource playback.',
            'Current-owner guards discard stale audio; stopping audio does not roll back committed heard grievances.'
        ],
        confirmed_root_causes=[
            'Source infrastructure/rejected-output fallback could return generic misunderstanding strings, misrepresenting technical failure as NPC confusion.',
            'Positive generated confidence below 0.15 could reject otherwise valid replies; this is not ASR confidence.',
            'Original direct-insult grammar admitted only idiot/fool/coward; original profanity had no private insult admission.',
            'Voice selection used resolved visual body category; accepted voice presentation could lose to a generic/opposite body fallback.',
            'Missing metadata defaulted to Alba; Vey lacked explicit authored onboarding assignment.',
            'Live capture originally reassigned final text to whichever NPC was nearest at completion; playback lacked full speaker retirement guard.',
            'Synthesis originally used one pacing and clipped accepted text; installed Kokoro supports bounded speed, not verified expressive conditioning.'
        ],
        original_failure='Exact player-visible failing turn NOT REPRODUCED. Direct baseline model samples understood the insult; source fallback branches contain the reported wording. No failing-turn ASR/raw/reviewer/audio chain was captured.',
        configured_stack=dict(generator='Qwen3.5-4B Q4_K_M / llama.cpp b10828 CUDA13.3, context12288, q8_0 KV, default fullGPU',speech='CPU Kokoro82M',asr='CPU WhisperTiny int8',vad='Silero',semantic_recall='disabled by existing explicit setting',audio_cache='none in active worker/client'),
        examples=examples,
        model_review=dict(retained_revision='comparison_final16',samples_each=24,act_target_matches=sum(r['parsed']['social']['act']==r['expected_act'] and r['parsed']['social']['target']==r['expected_target'] for r in after),
            important_caveat='Question target rubric counts unknown-duke npc versus none as a mismatch; subject/addressee ambiguity is itself unresolved. Exact label count is not a characterization/factual-grounding PASS.',
            original_insult_clarifications=0,known_failures=['Misclassified benign frustration metadata','Paraphrase target wrong in one sample','Timid NPC apologizes as if at fault','Banter echoes player','Reported insult has unsupported prior-experience and affect','Numeric apologies/corrections not reconciled','Service refusal remains verbal, not a committed service lock'],
            rejected_revision='final_model16: explicit target-definition expansion worsened labels; reverted after third repair attempt'),
        verification=dict(compile=read(OUT/'final_compile_verified/receipt.json'),pure_checks=len(read(OUT/'final_checks_verified/checks.json')),
            disposition_groups=21,dialogue_patch_groups=7,voice_selection_groups=5,native_metadata=read(ROOT/'outputs/PlayableEvaluation_20261009/voice-native/metadata-20261011T200843398Z.json'),
            speech=speech['evidence'],microphone='Deferred by user; synthetic ASR returned Fuck you, Kant. once, finish empty. Not actual microphone accuracy.',
            ordinary_PlaySafe='NOT VERIFIED',listening='NOT VERIFIED',active_save_mutation='No fixture entered an active save'),
        measurements=dict(model=times,model_configuration='Same installed model/sampler/context; isolated 16-GPU-layer worker for both. This differs from production fullGPU offload and excludes reviewer.',
            cold_load_seconds=read(OUT/'comparison_final16/environment.json')['load_seconds'],speech=speech['rows'],
            hardware=dict(cpu=samples['cpu'],ramGiB=samples['systemRamGiB'],gpu=samples['rows'][0]['gpu'].split(',')[0]),
            sampled_model_rss_mib=max(x.get('rssMiB',0) for row in samples['rows'] for x in row['processes'] if x.get('name')=='llama-server.exe'),
            sampled_unity_rss_mib=max(x.get('rssMiB',0) for row in samples['rows'] for x in row['processes'] if x.get('name')=='Unity.exe'),
            sampled_gpu_used_mib=max(float(row['gpu'].split(',')[1]) for row in samples['rows']),sampled_gpu_free_min_mib=min(float(row['gpu'].split(',')[2]) for row in samples['rows']),
            reserve_met=False,resource_evidence='30 read-only target-machine samples during rejected final prompt experiment with Unity Edit Mode; no frame-time/PlaySafe acceptance.',
            unavailable=['Actual player-speech end to final ASR','Final transcript to production accepted response including reviewer','Speech end to first valid audible reply','Unity PlaySafe frame times','Combined production LLM+TTS+game peak resources','Playback continuity/interruption listening']),
        remaining=['General semantic target/reaction reliability after three attempts','Registered persistent resident hostility/guard/service/withdrawal action path and permissions','Casting save/load and pooled production body witness','Real microphone and listening (user deferred)','Safe measured production GPU offload with Unity Play Mode','Production end-to-end fault and duplicate callback verification'])
    (OUT/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    (OUT/'acceptance_matrix.json').write_text(json.dumps([dict(scenario=s,status=t,evidence=e) for s,t,e in matrix],indent=2),encoding='utf-8')
    print(json.dumps(dict(changed_files=len(changes),checks=report['verification']['pure_checks'],model=times,act_target_matches=report['model_review']['act_target_matches'],status=report['status'])))
if __name__=='__main__':main()
