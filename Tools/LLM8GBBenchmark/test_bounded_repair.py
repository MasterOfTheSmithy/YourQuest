"""Regression checks for finite attempts, stale ownership and rejected verifier results."""
import unittest
from bounded_repair import Decision,run_episode,fingerprint


class RepairTests(unittest.TestCase):
    def test_exhausted_generation_never_accepts(self):
        r=run_episode({'player':'a'},lambda *a:{'raw':'bad'},lambda *a:Decision(False,'invalid'))
        self.assertEqual((r['generationCalls'],r['reviewCalls'],r['acceptedByPipeline']),(3,0,False))

    def test_reviewer_rejection_uses_separate_budgets(self):
        r=run_episode({'player':'a'},lambda *a:{'raw':'{}'},lambda *a:Decision(True,candidate='{}'),
            lambda *a:(Decision(False,'unsupported claim'),{'raw':'review'}))
        self.assertEqual((r['generationCalls'],r['reviewCalls'],r['acceptedByPipeline']),(3,3,False))

    def test_success_stops_without_unnecessary_retry(self):
        r=run_episode({'player':'a'},lambda *a:{'raw':'{}'},lambda *a:Decision(True,candidate='{}'))
        self.assertEqual((r['generationCalls'],r['disposition']),(1,'OriginalAccepted'))

    def test_stale_validation_cannot_publish(self):
        state={'player':'a'}
        def validate(*args):
            state['player']='b';return Decision(True,candidate='{}')
        r=run_episode(state,lambda *a:{'raw':'{}'},validate)
        self.assertEqual((r['disposition'],r['candidate']),('Superseded',''))

    def test_repair_receives_failure_not_a_golden_answer(self):
        seen=[]
        def generate(attempt,diagnostic,previous):
            seen.append((attempt,diagnostic,previous));return {'raw':'bad' if attempt==0 else 'fixed'}
        r=run_episode({'player':'a'},generate,lambda r,a:Decision(a>0,'schema error',r['raw']))
        self.assertEqual(seen,[(0,'',''),(1,'schema error','bad')])
        self.assertEqual(r['disposition'],'RepairedAccepted')

    def test_context_failure_is_not_retried_by_shrinking_evidence(self):
        r=run_episode({'player':'a'},lambda *a:{'raw':''},lambda *a:Decision(False,'ContextOverflow',terminal=True))
        self.assertEqual((r['generationCalls'],r['disposition']),(1,'NonRepairable'))

    def test_receipts_keep_actual_call_options_when_runtime_changes(self):
        options={'thinking':False}
        def review(*args):
            options['thinking']=True
            return Decision(True),{'options':options}
        r=run_episode({'player':'a'},lambda *a:{'raw':'{}','options':options},
            lambda *a:Decision(True,candidate='{}'),review)
        options['thinking']=False
        self.assertFalse(r['trace'][0]['result']['options']['thinking'])
        self.assertTrue(r['trace'][2]['result']['options']['thinking'])


if __name__=='__main__':unittest.main()
