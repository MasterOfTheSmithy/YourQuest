"""Detached bounded repair orchestration; never a production scheduler or state committer."""
import copy
import hashlib
import json
import time
from dataclasses import dataclass


@dataclass
class Decision:
    accepted: bool
    diagnostic: str = ''
    candidate: str = ''
    terminal: bool = False


def fingerprint(value):
    return hashlib.sha256(json.dumps(value,sort_keys=True,ensure_ascii=False,separators=(',',':')).encode('utf-8')).hexdigest()


def run_episode(snapshot, generate, validate, review=None, current_fingerprint=None):
    """Run at most three physical generation calls and three isolated review calls.

    Callbacks receive no golden answer. Validators own repair scope; a reviewer decision
    remains a model judgment and requires independent evaluation before qualification.
    """
    # note: Ownership and attempt limits apply before and after every callback, including failures.
    identity=fingerprint(snapshot)
    current_fingerprint=current_fingerprint or (lambda: fingerprint(snapshot))
    started=time.perf_counter()
    trace=[]; previous='';diagnostic='';candidate='';gen_calls=0;review_calls=0
    disposition='AttemptsExhausted'
    for attempt in range(3):
        if current_fingerprint()!=identity:
            disposition='Superseded';break
        gen_calls+=1
        generation=generate(attempt,diagnostic,previous)
        # note: Runtime options may change for the next call; receipts retain the options actually used.
        trace.append(dict(stage='generation',ordinal=gen_calls,result=copy.deepcopy(generation)))
        if current_fingerprint()!=identity:
            disposition='Superseded';break
        checked=validate(generation,attempt)
        trace.append(dict(stage='validation',ordinal=gen_calls,accepted=checked.accepted,diagnostic=checked.diagnostic))
        if current_fingerprint()!=identity:
            disposition='Superseded';break
        previous=generation.get('raw','')
        candidate=checked.candidate
        if not checked.accepted:
            diagnostic=checked.diagnostic
            if checked.terminal:
                disposition='NonRepairable';break
            continue
        if review is not None:
            if current_fingerprint()!=identity:
                disposition='Superseded';break
            review_calls+=1
            reviewed,receipt=review(candidate,review_calls-1)
            trace.append(dict(stage='review',ordinal=review_calls,accepted=reviewed.accepted,
                              diagnostic=reviewed.diagnostic,result=copy.deepcopy(receipt)))
            if current_fingerprint()!=identity:
                disposition='Superseded';break
            if not reviewed.accepted:
                previous=candidate;diagnostic=reviewed.diagnostic
                if reviewed.terminal:
                    disposition='NonRepairable';break
                continue
        disposition='OriginalAccepted' if attempt==0 else 'RepairedAccepted'
        break
    accepted=disposition in ['OriginalAccepted','RepairedAccepted']
    return dict(disposition=disposition,acceptedByPipeline=accepted,
                candidate=candidate if accepted else '',lastCandidate=candidate or previous,
                snapshotHash=identity,generationCalls=gen_calls,reviewCalls=review_calls,
                warmEpisodeSeconds=time.perf_counter()-started,trace=trace,
                evidence='Detached proposal pipeline; acceptance by a model reviewer is not independent factual approval.')
