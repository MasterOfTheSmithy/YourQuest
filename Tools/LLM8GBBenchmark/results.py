"""Join saved measurements and independent reviews without pooling unlike conditions."""
import argparse
import collections
import hashlib
import json
import statistics
from pathlib import Path

import run as bench


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    out = Path(args.out).resolve()
    if bench.ROOT not in out.parents:
        raise ValueError('Output must remain inside workspace')
    measurements = [json.loads(line) for line in (out / 'measurements.jsonl').read_text(encoding='utf-8').splitlines()]
    audited = read(out / 'StrictResponseAudit.json')['rows']
    reviews = []
    specifications = [
        ('AstraNpcBlindReview', 'AstraNpcBlindingMap'),
        ('AstraGoddessBlindReview', 'AstraGoddessBlindingMap'),
        ('AstraGoddessSupplementReview', 'AstraGoddessSupplementMap'),
        ('AstraFinalVoiceReview', 'AstraFinalVoiceMap'),
        ('AstraLastVoiceReview', None)]
    known_hashes = {hashlib.sha256(row.get('raw', '').encode('utf-8')).hexdigest() for row in measurements}
    # note: Blinded judgments are joined only after the review; exact raw hashes protect response identity.
    for filename, mapping in specifications:
        review = read(out / (filename + '.json'))
        labels = {v: k for k, v in read(out / (mapping + '.json')).items()} if mapping else {'Last_01': 'compact_cpu_goddess-current'}
        for row in review['responses']:
            if row['rawSha256'] not in known_hashes:
                raise ValueError('Review does not match a recorded response: ' + row['id'])
            label = row.get('condition', row.get('candidate'))
            reviews.append(dict(reviewFile=filename + '.json', condition=labels[label], fixture=row['fixture'],
                responseId=row['id'], rawSha256=row['rawSha256'], hardGatePass=row['hardGatePass'],
                deliveryScreenPass=row['deliveryScreenPass'], scores=row['scores'], productionQualification=False))
    grouped = collections.defaultdict(list)
    for row in reviews:
        grouped[row['condition']].append(row)
    review_summary = [dict(condition=condition, responses=len(rows),
        hardGatePasses=sum(row['hardGatePass'] for row in rows),
        deliveryScreenPasses=sum(row['deliveryScreenPass'] for row in rows),
        byFixture={fixture: dict(samples=len(subset), hardGatePasses=sum(row['hardGatePass'] for row in subset),
            deliveryScreenPasses=sum(row['deliveryScreenPass'] for row in subset))
            for fixture in dict.fromkeys(row['fixture'] for row in rows)
            for subset in [[row for row in rows if row['fixture'] == fixture]]}) for condition, rows in grouped.items()]
    mtp = []
    for model in ['qwen35-stock-mtp', 'qwen35-insraq-mtp']:
        for fixture in ['reconstruct_conflicting_prose', 'reconstruct_cross_profile', 'reconstruct_long_context']:
            pair = {mode: [row for row in measurements if row['modelId'] == model and row['fixture'] == fixture
                and row['serverLabel'].startswith('mtp_' + mode + '_')] for mode in ['off', 'on']}
            before = statistics.median(row['seconds'] for row in pair['off'])
            after = statistics.median(row['seconds'] for row in pair['on'])
            mtp.append(dict(modelId=model, fixture=fixture, samplesPerCondition=len(pair['on']),
                offMedianSeconds=before, onMedianSeconds=after, reductionPercent=100 * (1 - after / before),
                allGoldenPass=all(row['semanticPass'] and row['success'] and row.get('streamDone') for rows in pair.values() for row in rows),
                exactRawMatch=[before_row['raw'] == after_row['raw'] for before_row, after_row in zip(pair['off'], pair['on'])]))
    residency = read(out / 'ResidencyResults.json')['rows']
    residency_summary = {policy: dict(cycles=len(rows), medianSeconds=statistics.median(row['cycleSeconds'] for row in rows),
        allGoldenPass=all(row['allSemanticPass'] for row in rows),
        combinedRssMedianBytes=statistics.median(row['ownedCombinedRssBytes'] for row in rows))
        for policy in dict.fromkeys(row['policy'] for row in residency)
        for rows in [[row for row in residency if row['policy'] == policy]]}
    methods = dict(residency=residency_summary, mtp=mtp,
        choice=read(out / 'OpenJevChoiceResults.json'),
        compact=read(out / 'CompactVoiceCases.json')['evidence'],
        evidence='Unlike models, prompts, sampling, budgets, engines and placement conditions are never pooled into one quality score.')
    summary = dict(recordedChatCases=len(measurements), chatModelCount=len({row['modelId'] for row in measurements}),
        additionalChoiceCases=len(methods['choice']['rows']), distinctModelsIncludingSelector=len({row['modelId'] for row in measurements}) + 1,
        additionalResidencyCpuCalls=len(residency), reviewedResponses=len(reviews),
        reviewHardGatePasses=sum(row['hardGatePass'] for row in reviews),
        reviewDeliveryScreenPasses=sum(row['deliveryScreenPass'] for row in reviews),
        strictGoldenDeliveryPasses=sum(row['strictGoldenDeliveryPass'] for row in audited),
        productionQualified=False, unityGameplayPerformanceVerified=False,
        conditionSummaries=read(out / 'ConditionSummary.json'), reviewSummaries=review_summary,
        reviewRows=reviews, methods=methods,
        sourceChecks={name: read(out / name) for name in ['SourceChecks_all_voice.json',
            'SourceChecks_compact_final.json', 'SourceChecks_structured_final.json']})
    bench.save(out / 'LiveTestSummary.json', summary)
    print(json.dumps({key: value for key, value in summary.items() if not isinstance(value, (list, dict))}))


if __name__ == '__main__':
    main()
