"""Prepare forward-only native terrain snapshot fingerprint preview; never write Assets or profile records."""
from pathlib import Path
import difflib,hashlib,json
root=Path(__file__).resolve().parents[2];out=root/'outputs/TerrainRefactor_20261014_TerrainSnapshotLoss'
path=root/'Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs';source=path.read_text()
helper='''    private const string NativeTerrainFingerprintPrefix = "terrain_native_inputs_v1:";

    private static void AppendTerrainIdentityField(StringBuilder signature, string value)
    {
        // note: Length framing prevents generated IDs or serialized layouts from aliasing adjacent terrain inputs.
        value ??= string.Empty;
        signature.Append(value.Length).Append(':').Append(value).Append('|');
    }

    private static void AppendTerrainIdentityList(StringBuilder signature, IReadOnlyList<string> values)
    {
        AppendTerrainIdentityField(signature, values == null ? "null" : values.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (values != null) foreach (string value in values) AppendTerrainIdentityField(signature, value);
    }

    private static string BuildProfileTerrainFingerprint(GeneratedWorldPlanRecord plan)
    {
        // note: Only current native V2 terrain separates visual palette intent from accepted landforms and physical site construction.
        if (plan?.spatialPlanV2?.generationVersion != GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion)
            return YQStateContract.Sha256Hex(BuildVisualSignature(plan));
        if (!YQWorldGenerationArchitecture.TryResolveRuntimeAuthority(plan, out var authority, out _) ||
            authority != YQSpatialPlanAuthority.AcceptedV2 || string.IsNullOrWhiteSpace(plan.spatialPlanV2.contentHash))
            return string.Empty;
        var signature = new StringBuilder(1024);
        AppendTerrainIdentityField(signature, YQGeneratedWorldTerrain.TerrainGenerationVersion);
        AppendTerrainIdentityField(signature, YQGeneratedWorldTerrain.WorldSize.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        AppendTerrainIdentityField(signature, YQGeneratedWorldTerrain.TerrainHeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        AppendTerrainIdentityField(signature, YQGeneratedWorldTerrain.HeightmapResolution.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendTerrainIdentityField(signature, plan.worldSeed);
        AppendTerrainIdentityField(signature, plan.spatialPlanV2.schemaVersion);
        AppendTerrainIdentityField(signature, plan.spatialPlanV2.generationVersion);
        AppendTerrainIdentityField(signature, plan.spatialPlanV2.validationVersion);
        AppendTerrainIdentityField(signature, plan.spatialPlanV2.contentHash);
        // note: Validated continuation content carries its parent, locations and committed geometry; absence has a distinct domain value.
        AppendTerrainIdentityField(signature, plan.spatialPlanV2.acceptedContinuation == null ? "absent" :
            JsonUtility.ToJson(plan.spatialPlanV2.acceptedContinuation));
        AppendTerrainIdentityField(signature, plan.settlements == null ? "null" : plan.settlements.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (plan.settlements != null) foreach (var site in plan.settlements)
        {
            AppendTerrainIdentityField(signature, site == null ? "null" : "settlement");
            if (site == null) continue;
            AppendTerrainIdentityField(signature, site.settlementId);
            AppendTerrainIdentityField(signature, site.regionId);
            AppendTerrainIdentityField(signature, site.runtimeSiteKitId);
            AppendTerrainIdentityField(signature, site.runtimeSiteBindingVersion);
            AppendTerrainIdentityField(signature, site.deterministicSeed);
            AppendTerrainIdentityField(signature, site.gridX.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendTerrainIdentityField(signature, site.gridY.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendTerrainIdentityField(signature, site.proceduralBlockTarget.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendTerrainIdentityField(signature, site.approxPopulation.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendTerrainIdentityField(signature, site.populationBand);
            AppendTerrainIdentityField(signature, site.preferMeasuredStructuralCells ? "measured" : "legacy-selection");
            AppendTerrainIdentityField(signature, site.siteStyleIntent);
            AppendTerrainIdentityField(signature, site.siteRoleIntent);
            AppendTerrainIdentityList(signature, site.cellRoleIntents);
            AppendTerrainIdentityList(signature, site.serviceSlots);
            AppendTerrainIdentityList(signature, site.residentRoles);
            AppendTerrainIdentityField(signature, site.proceduralLayout == null ? "absent" : JsonUtility.ToJson(site.proceduralLayout));
        }
        AppendTerrainIdentityField(signature, plan.encampments == null ? "null" : plan.encampments.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (plan.encampments != null) foreach (var site in plan.encampments)
        {
            AppendTerrainIdentityField(signature, site == null ? "null" : "encampment");
            if (site == null) continue;
            AppendTerrainIdentityField(signature, site.encampmentId);
            AppendTerrainIdentityField(signature, site.regionId);
            AppendTerrainIdentityField(signature, site.runtimeSiteKitId);
            AppendTerrainIdentityField(signature, site.runtimeSiteBindingVersion);
            AppendTerrainIdentityField(signature, site.deterministicSeed);
            AppendTerrainIdentityField(signature, site.gridX.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendTerrainIdentityField(signature, site.gridY.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendTerrainIdentityField(signature, site.siteStyleIntent);
            AppendTerrainIdentityField(signature, site.siteRoleIntent);
            AppendTerrainIdentityList(signature, site.cellRoleIntents);
            AppendTerrainIdentityField(signature, site.proceduralLayout == null ? "absent" : JsonUtility.ToJson(site.proceduralLayout));
        }
        return NativeTerrainFingerprintPrefix + YQStateContract.Sha256Hex(signature.ToString());
    }

    private static bool IsCompatibleTerrainFingerprint(string stored, string expected, GeneratedWorldPlanRecord plan)
    {
        // note: Legacy schema-one snapshots qualify only through their exact current visual signature; tagged snapshots never fall back to a weaker domain.
        if (string.IsNullOrWhiteSpace(stored) || string.IsNullOrWhiteSpace(expected)) return false;
        if (string.Equals(stored, expected, StringComparison.Ordinal)) return true;
        return expected.StartsWith(NativeTerrainFingerprintPrefix, StringComparison.Ordinal) &&
            !stored.StartsWith(NativeTerrainFingerprintPrefix, StringComparison.Ordinal) &&
            string.Equals(stored, YQStateContract.Sha256Hex(BuildVisualSignature(plan)), StringComparison.Ordinal);
    }

'''
marker='    // note: Carry terrain forward only when profile ownership and the exact accepted world plan still match.'
assert source.count(marker)==1
candidate=source.replace(marker,helper+marker)
for old,new in (
('() => YQStateContract.Sha256Hex(BuildVisualSignature(plan)))','() => BuildProfileTerrainFingerprint(plan))'),
('string terrainPlanFingerprint = YQStateContract.Sha256Hex(BuildVisualSignature(plan));','string terrainPlanFingerprint = BuildProfileTerrainFingerprint(plan);'),
('string currentFingerprint = YQStateContract.Sha256Hex(BuildVisualSignature(plan));','string currentFingerprint = BuildProfileTerrainFingerprint(plan);'),
('string.Equals(record.planFingerprint, expectedFingerprint, StringComparison.Ordinal);','IsCompatibleTerrainFingerprint(record.planFingerprint, expectedFingerprint, plan);')):
    assert candidate.count(old)==1,old;candidate=candidate.replace(old,new)
needle='''loadedProfileTerrainJson,
                    profileId,
                    world,
                    terrainPlanFingerprint,
                    terrain =>'''
replacement='''loadedProfileTerrainJson,
                    profileId,
                    world,
                    IsCompatibleProfileTerrainSnapshot(loadedProfileTerrainJson, profileId, world, plan, terrainPlanFingerprint)
                        ? savedTerrainRecord.planFingerprint : terrainPlanFingerprint,
                    terrain =>'''
assert candidate.count(needle)==1;candidate=candidate.replace(needle,replacement)
(out/'TerrainFingerprintHelper.proposal.cs').write_text(helper)
(out/'RuntimeBuilder.proposal.cs').write_text(candidate)
(out/'RuntimeBuilder.preimage.cs').write_text(source)
relative=path.relative_to(root).as_posix()
(out/'TerrainFingerprint.proposal.patch').write_text(''.join(difflib.unified_diff(source.splitlines(True),candidate.splitlines(True),fromfile='a/'+relative,tofile='b/'+relative)))
(out/'Proposal_Source_Identity.json').write_text(json.dumps({relative:hashlib.sha256(path.read_bytes()).hexdigest()},indent=2))
print(out/'TerrainFingerprint.proposal.patch')
