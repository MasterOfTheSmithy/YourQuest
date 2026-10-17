from pathlib import Path
import hashlib,json,difflib,xml.etree.ElementTree as ET
from datetime import datetime,timezone
R=Path(__file__).resolve().parents[2];B=R/'outputs/TerrainRefactor_20261014_StreamingRetest/FrontierTokenInputComparison';B.mkdir(exist_ok=False)
P=R/'Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs';s=P.read_text();n=s
n=n.replace('        private string preparedCandidateInputsJson;','        private string preparedCandidateInputsJson;\n        private string preparedPlanInputsJson;\n        private string preparedParentInputsJson;',1)
n=n.replace('                string canonicalInputs = MeasureStartupOperation("Frontier.Token.CaptureInputs", CanonicalInputsJson);','''                string planInputs = null, parentInputs = null;
                string canonicalInputs = MeasureStartupOperation("Frontier.Token.CaptureInputs",
                    () => CanonicalInputsJson(out planInputs, out parentInputs));''',1)
n=n.replace('                preparedCanonicalInputsJson = canonicalInputs;','''                preparedCanonicalInputsJson = canonicalInputs;
                preparedPlanInputsJson = planInputs;
                preparedParentInputsJson = parentInputs;''',1)
old='''        private string CanonicalInputsJson() => JsonConvert.SerializeObject(new {
            plan.worldSeed, plan.spatialPlan, plan.settlements, plan.encampments
        }, FrontierConstructionJsonSettings) + "|" + JsonConvert.SerializeObject(parent, FrontierConstructionJsonSettings);'''
new='''        private string CanonicalInputsJson() => CanonicalInputsJson(out _, out _);

        private string CanonicalInputsJson(out string planInputs, out string parentInputs)
        {
            // note: Retain the exact two immutable component strings from numeric preparation; no delimiter parsing or mutable-source memo authorizes publication.
            planInputs = JsonConvert.SerializeObject(new {
                plan.worldSeed, plan.spatialPlan, plan.settlements, plan.encampments
            }, FrontierConstructionJsonSettings);
            parentInputs = JsonConvert.SerializeObject(parent, FrontierConstructionJsonSettings);
            return planInputs + "|" + parentInputs;
        }

        private static bool CanonicalInputsMatch(GeneratedWorldPlanRecord source,
            GeneratedSpatialWorldPlanV2Record artifact, string capturedPlanInputs, string capturedParentInputs)
        {
            // note: Traverse both current sources in the original parent-first order even after mismatch, preserving serializer callbacks/errors and exact ordinal equality without whole-document allocations.
            bool parentMatches = YQProfileCommitStore.JsonEqualsCaptured(artifact, capturedParentInputs, FrontierConstructionJsonSettings);
            bool planMatches = YQProfileCommitStore.JsonEqualsCaptured(new {
                source.worldSeed, source.spatialPlan, source.settlements, source.encampments
            }, capturedPlanInputs, FrontierConstructionJsonSettings);
            return parentMatches && planMatches;
        }'''
assert old in n;n=n.replace(old,new,1)
old='''                string parentJson, canonicalInputsJson;
                // note: Measure the complete existing compiler-input proof, including parent serialization, without caching mutable sources.
                using (FrontierTiming.Begin(FrontierTiming.Operation.TokenInputs))
                {
                    parentJson = JsonConvert.SerializeObject(parent, FrontierConstructionJsonSettings);
                    canonicalInputsJson = JsonConvert.SerializeObject(new {
                        plan.worldSeed, plan.spatialPlan, plan.settlements, plan.encampments
                    }, FrontierConstructionJsonSettings) + "|" + parentJson;
                }
                var prepared = preparedProjection;
                string candidateInputsJson = preparedCandidateInputsJson;'''
new='''                string parentJson, planInputsJson, canonicalInputsJson;
                var prepared = preparedProjection;
                string candidateInputsJson = preparedCandidateInputsJson;
                using (FrontierTiming.Begin(FrontierTiming.Operation.TokenInputs))
                {
                    if (prepared != null && preparedCanonicalInputsJson != null &&
                        preparedPlanInputsJson != null && preparedParentInputsJson != null)
                    {
                        if (!CanonicalInputsMatch(plan, parent, preparedPlanInputsJson, preparedParentInputsJson))
                        { failure = "Frontier compiler inputs changed after numeric preparation."; return false; }
                        // note: Only an exact current-source traversal can reuse these captured strings; all following provider, owner and accepted-body checks still execute.
                        parentJson = preparedParentInputsJson;
                        planInputsJson = preparedPlanInputsJson;
                        canonicalInputsJson = preparedCanonicalInputsJson;
                    }
                    else
                    {
                        // note: A missing numeric proof retains the original synchronous compilation path and its parent-first serialization order.
                        parentJson = JsonConvert.SerializeObject(parent, FrontierConstructionJsonSettings);
                        planInputsJson = JsonConvert.SerializeObject(new {
                            plan.worldSeed, plan.spatialPlan, plan.settlements, plan.encampments
                        }, FrontierConstructionJsonSettings);
                        canonicalInputsJson = planInputsJson + "|" + parentJson;
                    }
                }'''
assert old in n;n=n.replace(old,new,1)
n=n.replace('                preparedCanonicalInputsJson = canonicalInputsJson;','''                preparedCanonicalInputsJson = canonicalInputsJson;
                preparedPlanInputsJson = planInputsJson;
                preparedParentInputsJson = parentJson;''',1)
old='''                string.Equals(preparedCandidateInputsJson, CandidateCompilerInputsJson(candidate, candidate.spatialPlanV2), StringComparison.Ordinal);'''
new='''                YQProfileCommitStore.JsonEqualsCaptured(new {
                    candidate.worldSeed, candidate.spatialPlan, candidate.settlements, candidate.encampments,
                    spatialPlanV2 = candidate.spatialPlanV2
                }, preparedCandidateInputsJson, FrontierConstructionJsonSettings);'''
assert old in n;n=n.replace(old,new,1)
(B/'YQGeneratedWorldRuntimeBuilder.before.cs').write_text(s);(B/'YQGeneratedWorldRuntimeBuilder.proposal.cs').write_text(n)
(B/'Proposal.patch').write_text(''.join(difflib.unified_diff(s.splitlines(True),n.splitlines(True),fromfile='a/Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs',tofile='b/Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs')))
(B/'SourceIdentity.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),original_sha256=hashlib.sha256(s.encode()).hexdigest(),proposal_sha256=hashlib.sha256(n.encode()).hexdigest(),production_path=str(P),production_untouched=P.read_text()==s),indent=2))
C=R/'outputs/TerrainRefactor_20261014_StreamingRetest/IntegratedCompile_17/runtime/Compile.csproj';tree=ET.parse(C)
for node in tree.findall('.//Compile'):
 if Path(node.attrib['Include']).resolve()==P.resolve():node.attrib['Include']=str(B/'YQGeneratedWorldRuntimeBuilder.proposal.cs')
for group in tree.findall('PropertyGroup'):
 for node in group:
  if node.tag in ['BaseIntermediateOutputPath','OutputPath']:node.text=str(B/('obj/' if node.tag.startswith('Base') else 'bin/'))
tree.write(B/'Overlay.csproj',encoding='utf-8');(B/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
print(B)
