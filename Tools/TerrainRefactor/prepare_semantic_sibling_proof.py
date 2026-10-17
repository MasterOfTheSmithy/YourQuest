from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,difflib,xml.etree.ElementTree as ET
R=Path(__file__).resolve().parents[2];B=R/'outputs/TerrainRefactor_20261014_StreamingRetest/SemanticSiblingProof';B.mkdir(exist_ok=False);P=R/'Assets/Assets/Scripts/Generated/YQSpatialContinuationV2Validation.cs';old=P.read_text();n=old
helper=r'''
    private static string BasicProofSource(GeneratedSpatialWorldPlanV2Record parent,
        GeneratedSpatialContinuationV2Record continuation)
    {
        // note: Preserve the original snapshot and branch evaluation order. Only plain callback-free data may share a proof across the unrelated sibling envelope on its parent.
        string parentJson = JsonUtility.ToJson(parent), continuationJson = JsonUtility.ToJson(continuation);
        string parentBranches = BranchIdentity(parent?.acceptedContinuation), continuationBranches = BranchIdentity(continuation);
        if (!CallbackFreeProofGraph(parent, continuation))
            return parentJson + "|" + continuationJson + "|" + parentBranches + "|" + continuationBranches;
        return ParentGeometryProofJson(parentJson) + "|" + continuationJson + "|" + continuationBranches;
    }

    private sealed class ProofReferenceComparer : IEqualityComparer<object>
    {
        public new bool Equals(object first, object last) => ReferenceEquals(first, last);
        public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }

    private static bool CallbackFreeProofGraph(GeneratedSpatialWorldPlanV2Record parent,
        GeneratedSpatialContinuationV2Record continuation)
        => CallbackFreeProofRoots(parent, typeof(GeneratedSpatialWorldPlanV2Record), continuation, typeof(GeneratedSpatialContinuationV2Record));

    private static bool CallbackFreeProofRoots(object parent, Type parentType, object continuation, Type continuationType)
    {
        // note: This invocation-local guard refuses callbacks, runtime subtypes, foreign objects and oversized graphs; it does not issue acceptance or retain another proof cache.
        var pending = new Stack<KeyValuePair<object, Type>>();
        var visited = new HashSet<object>(new ProofReferenceComparer());
        var fields = new Dictionary<Type, System.Reflection.FieldInfo[]>();
        pending.Push(new KeyValuePair<object, Type>(parent, parentType));
        pending.Push(new KeyValuePair<object, Type>(continuation, continuationType));
        bool Leaf(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string) ||
            type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Quaternion) || type == typeof(Color);
        while (pending.Count > 0)
        {
            var entry = pending.Pop(); object value = entry.Key;
            if (value == null) continue;
            Type type = value.GetType();
            if (type != entry.Value || value is ISerializationCallbackReceiver) return false;
            if (Leaf(type)) continue;
            if (visited.Count >= 65536) return false;
            if (!visited.Add(value)) continue;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type element = type.GetGenericArguments()[0];
                if (!Leaf(element)) foreach (object item in (System.Collections.IList)value)
                    pending.Push(new KeyValuePair<object, Type>(item, element));
                continue;
            }
            if (type.IsArray)
            {
                Type element = type.GetElementType();
                if (!Leaf(element)) foreach (object item in (Array)value)
                    pending.Push(new KeyValuePair<object, Type>(item, element));
                continue;
            }
            // note: Private serialized base fields are not returned by this field walk; conservatively retain the original proof identity for every inherited record type.
            if (!type.IsValueType && type.BaseType != typeof(object)) return false;
            if (type.Assembly != typeof(GeneratedSpatialWorldPlanV2Record).Assembly ||
                typeof(UnityEngine.Object).IsAssignableFrom(type) || !type.IsSerializable) return false;
            if (!fields.TryGetValue(type, out var members))
            {
                members = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                fields.Add(type, members);
            }
            foreach (var field in members)
            {
                if (field.IsNotSerialized || !field.IsPublic &&
                    !field.IsDefined(typeof(SerializeField), false) && !field.IsDefined(typeof(SerializeReference), false)) continue;
                if (!Leaf(field.FieldType)) pending.Push(new KeyValuePair<object, Type>(field.GetValue(value), field.FieldType));
            }
        }
        return true;
    }

    private static string ParentGeometryProofJson(string source)
    {
        // note: Remove only the top-level sibling envelope value from an already captured Unity field document. All parent metadata/geometry and the explicit candidate document remain exact proof inputs.
        const string key = "\"acceptedContinuation\"";
        int depth = 0; bool quoted = false, escaped = false;
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (quoted) { if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') quoted = false; continue; }
            if (c == '"')
            {
                if (depth == 1 && i + key.Length <= source.Length && string.CompareOrdinal(source, i, key, 0, key.Length) == 0)
                {
                    int start = i + key.Length;
                    while (start < source.Length && char.IsWhiteSpace(source[start])) start++;
                    if (start < source.Length && source[start++] == ':')
                    {
                        while (start < source.Length && char.IsWhiteSpace(source[start])) start++;
                        int nested = 0; bool text = false, slash = false;
                        for (int end = start; end < source.Length; end++)
                        {
                            char item = source[end];
                            if (text) { if (slash) slash = false; else if (item == '\\') slash = true; else if (item == '"') text = false; continue; }
                            if (item == '"') { text = true; continue; }
                            if (nested == 0 && (item == ',' || item == '}')) return source.Substring(0, start) + "null" + source.Substring(end);
                            if (item == '{' || item == '[') nested++;
                            else if (item == '}' || item == ']') nested--;
                        }
                    }
                }
                quoted = true;
            }
            else if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']') depth--;
        }
        return source;
    }

'''
expr='JsonUtility.ToJson(parent) + "|" + JsonUtility.ToJson(continuation) + "|" + BranchIdentity(parent?.acceptedContinuation) + "|" + BranchIdentity(continuation)';assert n.count(expr)==2;n=n.replace(expr,'BasicProofSource(parent, continuation)');anchor='    private static YQSpatialContinuationBasicValidationResultV2 ValidateUncached(';assert n.count(anchor)==1;n=n.replace(anchor,helper+'\n'+anchor)
(B/'Validation.before.cs').write_bytes(P.read_bytes());(B/'Validation.proposal.cs').write_text(n);patch=''.join(difflib.unified_diff(old.splitlines(True),n.splitlines(True),fromfile='a/'+P.relative_to(R).as_posix(),tofile='b/'+P.relative_to(R).as_posix()));(B/'Proposal.patch').write_bytes(patch.replace('\n','\r\n').encode() if b'\r\n' in P.read_bytes() else patch.encode());(B/'SourceIdentity.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),original_sha256=hashlib.sha256(P.read_bytes()).hexdigest(),proposal_sha256=hashlib.sha256((B/'Validation.proposal.cs').read_bytes()).hexdigest(),production_path=str(P)),indent=2));C=R/'outputs/TerrainRefactor_20261014_StreamingRetest/IntegratedCompile_18/runtime/Compile.csproj';tree=ET.parse(C)
for node in tree.findall('.//Compile'):
 if Path(node.attrib['Include']).resolve()==P.resolve():node.attrib['Include']=str(B/'Validation.proposal.cs')
tree.write(B/'Overlay.csproj',encoding='utf-8');(B/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>');print(B)
